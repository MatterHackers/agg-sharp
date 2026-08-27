#!/usr/bin/env python3
#
# Copyright (c) 2026, Lars Brubaker
# All rights reserved.
#
# Redistribution and use in source and binary forms, with or without modification, are permitted
# provided that the conditions of the agg-sharp BSD 2-clause licence are met. See LICENSE.
"""The browser golden image runner: renders a scene in headless Chrome and holds it to a checked-in PNG.

This is the browser arm of the golden image suites in `Tests/Agg.Tests/Agg.GoldenImages`. It reuses
their conventions exactly - one folder per backend under `TestData/GoldenImages`, tolerance zero, and
the `AGG_REGEN_GOLDENS` regenerate discipline (here spelled `--regen`, and it still fails on purpose).
It is a script rather than a TUnit test because nothing in the .NET test host can drive a browser: the
render happens inside a wasm module in another process, and reaching it means CDP.

What it compares are the *wasm module's own* pixels, not Chrome's screenshot of the page.
`BrowserSystemWindow.CaptureScreenshotAsync` reads the WebGPU frame back into an agg `ImageBuffer` and
writes a PNG - the same code path, the same BGRA bytes and the same encoder the desktop goldens use -
and `BrowserCaptureInterop.ReadCaptureAsBase64` hands it out of the wasm filesystem. A CDP
`Page.captureScreenshot` was measured against it (W4 S5) as identical over the canvas, but it is the
wrong source anyway: it is composited, scaled by the page zoom and re-encoded by Chrome, none of which
belongs in a pixel-identity oracle.

  Compare the checked-in set:   scripts/run-browser-goldens.py
  Re-baseline it:               scripts/run-browser-goldens.py --regen     (always exits non-zero)
  Reuse an existing publish:    scripts/run-browser-goldens.py --no-publish

Needs Google Chrome (WebGPU, so a recent one) and the `wasm-tools` workload, because the publish is
the emdawnwebgpu relink - a plain build does not paint. Everything else is the Python standard
library, by decision: this runs on a mac with no PowerShell and must not grow a Playwright/Pillow
dependency to check one PNG.

What the browser set is pinned against, and what re-baselines it:

  - Not the browser's fonts. Nothing in `PlatformBrowser` calls `fillText`, `measureText` or takes a
    2D canvas context: every glyph in a capture is agg's own TTF outline, tessellated and drawn
    through the same `Graphics2DGpu` the desktop uses. A Chrome release cannot move the text.
  - But Chrome carries Dawn, and Dawn is this backend's driver. A Chrome upgrade is a re-baseline
    event for the `browser` set in exactly the way a GPU driver upgrade is for `metal` - and for the
    same reason the sets are kept apart at all. Measured on Chrome 151 / Apple Silicon: four separate
    headless launches produced byte-identical PNGs (one sha256), so within a Chrome version the
    capture is not merely pixel-stable, it is byte-stable.
"""

import argparse
import base64
import functools
import hashlib
import http.server
import json
import os
import re
import shutil
import socket
import socketserver
import struct
import subprocess
import sys
import tempfile
import threading
import time
import urllib.request
import zlib

# The capture size, pinned by the runner rather than taken from whatever window Chrome opened. It is
# WebGpuOffscreenCapture.DefaultWidth/Height on purpose: the browser set is then directly comparable,
# image for image, with the metal and d3d12 sets. Changing either dimension invalidates every browser
# golden.
CAPTURE_WIDTH = 512
CAPTURE_HEIGHT = 384

# The scenes this runner knows how to render, by golden name. Each entry says which project to publish
# and which wasm export takes the capture; the page's own code decides what is on screen.
#
# There is one, and it is BrowserHost's demo window - a window the example's own README calls "not a
# product", so editing the demo re-baselines the golden. That is a real weakness and the reason the
# entry is a table rather than a constant: the second scene should come from the desktop suites'
# scenes rather than from another demo. See docs/browser-golden-runner.md in the MatterCAD tree for
# what that needs.
SCENES = {
    "browser-host-demo": {
        "project": "examples/BrowserHost/BrowserHost.csproj",
        "publish_root": "examples/BrowserHost/bin/Debug/net10.0/publish/wwwroot",
        "capture_export": (
            "MatterHackers.Agg.Examples.BrowserHostProgram.CaptureAsync", "BrowserHost.dll"),
    },
}

# Where in the wasm filesystem a capture is written before it is read back out. Anywhere works; it
# never touches a real disk.
WASM_CAPTURE_PATH = "/golden-capture.png"

# The heartbeat line BrowserHost writes to its status element once a second. Waiting on it - rather
# than on a fixed delay - is what keeps this run as fast as the boot allows and still deterministic.
HEARTBEAT = re.compile(r"ticks (\d+), paints (\d+), renderer (ready|not ready)")

# Painted frames to wait for before capturing. One, and it cannot be more: agg paints on invalidation,
# so a static scene paints exactly once and then the count never moves again. One is also enough - the
# capture forces its own frame and waits for that frame, so what it reads is never the frame counted
# here.
MIN_PAINTS = 1

BOOT_TIMEOUT_SECONDS = 180


# ---------------------------------------------------------------------------------------------
# PNG, in the standard library. 8-bit truecolour with or without alpha, non-interlaced - which is
# everything agg's ImageIO writes and everything this runner produces.
# ---------------------------------------------------------------------------------------------

def read_png(path_or_bytes):
    """Returns (width, height, channels, pixel bytes) for an 8-bit non-interlaced PNG."""
    if isinstance(path_or_bytes, (bytes, bytearray)):
        data = bytes(path_or_bytes)
    else:
        with open(path_or_bytes, "rb") as handle:
            data = handle.read()

    if data[:8] != b"\x89PNG\r\n\x1a\n":
        raise ValueError("not a png")

    pos = 8
    idat = b""
    width = height = color = None
    while pos < len(data):
        length, ctype = struct.unpack(">I4s", data[pos:pos + 8])
        body = data[pos + 8:pos + 8 + length]
        pos += 12 + length
        if ctype == b"IHDR":
            width, height, depth, color, _, _, interlace = struct.unpack(">IIBBBBB", body)
            if depth != 8 or interlace != 0 or color not in (2, 6):
                raise ValueError(f"unsupported png: depth {depth} colour {color} interlace {interlace}")
        elif ctype == b"IDAT":
            idat += body
        elif ctype == b"IEND":
            break

    channels = 3 if color == 2 else 4
    raw = zlib.decompress(idat)
    stride = width * channels
    out = bytearray(stride * height)
    prev = bytearray(stride)
    at = 0
    for y in range(height):
        filter_type = raw[at]
        at += 1
        line = bytearray(raw[at:at + stride])
        at += stride
        if filter_type == 1:
            for i in range(channels, stride):
                line[i] = (line[i] + line[i - channels]) & 0xFF
        elif filter_type == 2:
            for i in range(stride):
                line[i] = (line[i] + prev[i]) & 0xFF
        elif filter_type == 3:
            for i in range(stride):
                left = line[i - channels] if i >= channels else 0
                line[i] = (line[i] + ((left + prev[i]) >> 1)) & 0xFF
        elif filter_type == 4:
            for i in range(stride):
                a = line[i - channels] if i >= channels else 0
                b = prev[i]
                c = prev[i - channels] if i >= channels else 0
                p = a + b - c
                pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
                pr = a if (pa <= pb and pa <= pc) else (b if pb <= pc else c)
                line[i] = (line[i] + pr) & 0xFF
        elif filter_type != 0:
            raise ValueError(f"unknown png filter {filter_type}")
        out[y * stride:(y + 1) * stride] = line
        prev = line

    return width, height, channels, bytes(out)


def write_png(path, image):
    """Writes an (width, height, channels, bytes) tuple as a PNG, replacing whatever is there."""
    width, height, channels, pixels = image
    stride = width * channels
    raw = bytearray()
    for y in range(height):
        raw.append(0)
        raw += pixels[y * stride:(y + 1) * stride]

    def chunk(tag, body):
        return (struct.pack(">I", len(body)) + tag + body
                + struct.pack(">I", zlib.crc32(tag + body) & 0xFFFFFFFF))

    header = struct.pack(">IIBBBBB", width, height, 8, 6 if channels == 4 else 2, 0, 0, 0)
    blob = (b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", header)
            + chunk(b"IDAT", zlib.compress(bytes(raw), 9)) + chunk(b"IEND", b""))

    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "wb") as handle:
        handle.write(blob)


def compare(golden, rendered, channel_tolerance=0):
    """Counts pixels differing by more than the tolerance in any channel, and the largest delta seen.

    Deliberately the same arithmetic as `GoldenImage.Compare` on the .NET side - per pixel, the
    largest absolute per-channel difference, counted if it exceeds the tolerance - so a browser
    failure message reads the same as a desktop one and means the same thing.
    """
    gw, gh, gc, gp = golden
    rw, rh, rc, rp = rendered
    if (gw, gh, gc) != (rw, rh, rc):
        return {
            "same_shape": False,
            "sizes": f"golden {gw}x{gh}x{gc}ch, rendered {rw}x{rh}x{rc}ch",
            "differing": 0,
            "total": 0,
            "max_delta": 0,
            "percent": 0.0,
        }

    differing = 0
    max_delta = 0
    mask = bytearray(gw * gh)
    for index in range(gw * gh):
        at = index * gc
        delta = 0
        for channel in range(gc):
            channel_delta = abs(gp[at + channel] - rp[at + channel])
            if channel_delta > delta:
                delta = channel_delta
        if delta > max_delta:
            max_delta = delta
        if delta > channel_tolerance:
            differing += 1
            mask[index] = 1

    total = gw * gh
    return {
        "same_shape": True,
        "sizes": None,
        "differing": differing,
        "total": total,
        "max_delta": max_delta,
        "percent": 0.0 if total == 0 else differing * 100.0 / total,
        "mask": bytes(mask),
    }


def describe(difference):
    if not difference["same_shape"]:
        return f"The sizes differ ({difference['sizes']})."
    return (f"{difference['differing']} of {difference['total']} pixels differ"
            f" ({difference['percent']:.4f}%), largest channel delta {difference['max_delta']}.")


def build_diff_image(golden, difference):
    """The golden dimmed to a gray wash with every differing pixel painted magenta - `BuildDiffImage`."""
    width, height, channels, pixels = golden
    out = bytearray(width * height * 3)
    mask = difference["mask"]
    for index in range(width * height):
        source = index * channels
        dest = index * 3
        if mask[index]:
            out[dest], out[dest + 1], out[dest + 2] = 255, 0, 255
        else:
            gray = (pixels[source] + pixels[source + 1] + pixels[source + 2]) // 3
            washed = 128 + (gray // 4)
            out[dest] = out[dest + 1] = out[dest + 2] = washed
    return width, height, 3, bytes(out)


# ---------------------------------------------------------------------------------------------
# Chrome DevTools Protocol: a websocket, a request/response call and an event log. Nothing else is
# needed to navigate a page, read a status line and await one promise.
# ---------------------------------------------------------------------------------------------

class DevToolsSocket:
    def __init__(self, url):
        rest = url[len("ws://"):]
        hostport, _, path = rest.partition("/")
        host, _, port = hostport.partition(":")
        self.sock = socket.create_connection((host, int(port or 80)))
        self.sock.settimeout(30)
        key = base64.b64encode(os.urandom(16)).decode()
        self.sock.sendall(
            (f"GET /{path} HTTP/1.1\r\nHost: {hostport}\r\nUpgrade: websocket\r\n"
             f"Connection: Upgrade\r\nSec-WebSocket-Key: {key}\r\n"
             f"Sec-WebSocket-Version: 13\r\n\r\n").encode())
        buffer = b""
        while b"\r\n\r\n" not in buffer:
            buffer += self.sock.recv(4096)
        if b"101" not in buffer.split(b"\r\n")[0]:
            raise RuntimeError(f"devtools refused the upgrade: {buffer[:200]!r}")
        self.buffer = buffer.split(b"\r\n\r\n", 1)[1]
        self.next_id = 1
        self.events = []

    def _read(self, count):
        while len(self.buffer) < count:
            chunk = self.sock.recv(1 << 20)
            if not chunk:
                raise EOFError("devtools closed the connection")
            self.buffer += chunk
        out, self.buffer = self.buffer[:count], self.buffer[count:]
        return out

    def _send(self, method, params):
        message = {"id": self.next_id, "method": method, "params": params or {}}
        self.next_id += 1
        payload = json.dumps(message).encode()
        header = bytearray([0x81])
        length = len(payload)
        if length < 126:
            header.append(0x80 | length)
        elif length < 65536:
            header.append(0x80 | 126)
            header += struct.pack(">H", length)
        else:
            header.append(0x80 | 127)
            header += struct.pack(">Q", length)
        mask = os.urandom(4)
        header += mask
        self.sock.sendall(bytes(header) + bytes(b ^ mask[i % 4] for i, b in enumerate(payload)))
        return message["id"]

    def _receive(self):
        _, second = self._read(2)
        length = second & 0x7F
        if length == 126:
            length = struct.unpack(">H", self._read(2))[0]
        elif length == 127:
            length = struct.unpack(">Q", self._read(8))[0]
        return json.loads(self._read(length).decode())

    def call(self, method, params=None, timeout=30):
        wanted = self._send(method, params)
        deadline = time.time() + timeout
        while time.time() < deadline:
            message = self._receive()
            if message.get("id") == wanted:
                return message
            self.events.append(message)
        raise TimeoutError(method)

    def evaluate(self, expression, await_promise=False, timeout=30):
        """Evaluates JS in the page and returns its value, raising with the JS error if it threw."""
        result = self.call("Runtime.evaluate", {
            "expression": expression,
            "awaitPromise": await_promise,
            "returnByValue": True,
        }, timeout=timeout)["result"]
        if "exceptionDetails" in result:
            raise RuntimeError("page threw: " + json.dumps(result["exceptionDetails"])[:2000])
        return result["result"].get("value")

    def console_lines(self):
        lines = []
        for event in self.events:
            if event.get("method") == "Runtime.consoleAPICalled":
                args = event["params"].get("args", [])
                lines.append(" ".join(
                    str(a.get("value", a.get("description", ""))) for a in args))
            elif event.get("method") == "Runtime.exceptionThrown":
                lines.append("EXCEPTION " + json.dumps(event["params"])[:400])
        return lines


# ---------------------------------------------------------------------------------------------
# The two processes this runner owns: a static file server over the publish output and Chrome.
# ---------------------------------------------------------------------------------------------

class StaticServer:
    """Serves the published wwwroot. A Blazor app will not boot off `file://`, so there has to be one."""

    class Handler(http.server.SimpleHTTPRequestHandler):
        extensions_map = {
            **http.server.SimpleHTTPRequestHandler.extensions_map,
            ".wasm": "application/wasm",
            ".js": "text/javascript",
            ".mjs": "text/javascript",
            ".json": "application/json",
            ".dat": "application/octet-stream",
            ".blat": "application/octet-stream",
            ".pdb": "application/octet-stream",
            "": "application/octet-stream",
        }

        def end_headers(self):
            # No caching, ever: a cached dotnet.wasm from a previous publish would be compared against
            # goldens while the tree said something else entirely.
            self.send_header("Cache-Control", "no-store")
            super().end_headers()

        def log_message(self, *args):
            pass

    def __init__(self, root):
        socketserver.TCPServer.allow_reuse_address = True
        self.httpd = socketserver.TCPServer(
            ("127.0.0.1", 0), functools.partial(StaticServer.Handler, directory=root))
        self.port = self.httpd.server_address[1]
        self.thread = threading.Thread(target=self.httpd.serve_forever, daemon=True)
        self.thread.start()

    @property
    def url(self):
        return f"http://127.0.0.1:{self.port}/"

    def close(self):
        self.httpd.shutdown()
        self.httpd.server_close()


def find_chrome():
    override = os.environ.get("CHROME")
    if override:
        return override
    candidates = [
        "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome",
        "/Applications/Chromium.app/Contents/MacOS/Chromium",
        shutil.which("google-chrome"),
        shutil.which("chromium"),
        shutil.which("chrome"),
    ]
    for candidate in candidates:
        if candidate and os.path.exists(candidate):
            return candidate
    raise RuntimeError("no Chrome found - set CHROME to its executable")


class Chrome:
    """A headless Chrome with a throwaway profile, and the devtools connection to its one page."""

    def __init__(self, log_path):
        self.profile = tempfile.mkdtemp(prefix="agg-browser-goldens-")
        self.log = open(log_path, "wb")
        self.process = subprocess.Popen([
            find_chrome(),
            "--headless=new",
            # WebGPU is not on by default in headless, and there is no fallback renderer anywhere in
            # agg: without this the page reports "this browser does not support WebGPU" and paints
            # nothing at all.
            "--enable-unsafe-webgpu",
            "--remote-debugging-port=0",
            f"--user-data-dir={self.profile}",
            f"--window-size={CAPTURE_WIDTH},{CAPTURE_HEIGHT}",
            "--no-first-run",
            "--no-default-browser-check",
            "--disable-extensions",
            "--disable-background-networking",
            "--hide-scrollbars",
            "about:blank",
        ], stdout=self.log, stderr=subprocess.STDOUT)

        self.port = self._await_debugging_port()

    def _await_debugging_port(self):
        """Reads the port Chrome actually chose out of `DevToolsActivePort`, rather than guessing one."""
        path = os.path.join(self.profile, "DevToolsActivePort")
        deadline = time.time() + 60
        while time.time() < deadline:
            if self.process.poll() is not None:
                raise RuntimeError(f"Chrome exited with {self.process.returncode} before listening")
            if os.path.exists(path):
                with open(path) as handle:
                    text = handle.read().split("\n")
                if text and text[0].strip().isdigit():
                    return int(text[0].strip())
            time.sleep(0.05)
        raise TimeoutError("Chrome never wrote DevToolsActivePort")

    def open_page(self):
        targets = [t for t in self._targets() if t["type"] == "page"]
        if not targets:
            raise RuntimeError("Chrome has no page target")
        socket_ = DevToolsSocket(targets[0]["webSocketDebuggerUrl"])
        socket_.call("Runtime.enable")
        socket_.call("Page.enable")
        socket_.call("Network.enable")
        socket_.call("Network.setCacheDisabled", {"cacheDisabled": True})
        # The capture is the canvas backing store, and that is the client size times the device pixel
        # ratio - so both are pinned here rather than inherited from whatever display Chrome thinks it
        # is on. Without the override a retina mac captures at 2x and every golden misses on size.
        socket_.call("Emulation.setDeviceMetricsOverride", {
            "width": CAPTURE_WIDTH,
            "height": CAPTURE_HEIGHT,
            "deviceScaleFactor": 1,
            "mobile": False,
        })
        return socket_

    def _targets(self):
        deadline = time.time() + 30
        while True:
            try:
                with urllib.request.urlopen(f"http://127.0.0.1:{self.port}/json") as response:
                    return json.load(response)
            except Exception:
                if time.time() > deadline:
                    raise
                time.sleep(0.1)

    def close(self):
        self.process.terminate()
        try:
            self.process.wait(timeout=10)
        except subprocess.TimeoutExpired:
            self.process.kill()
        self.log.close()
        shutil.rmtree(self.profile, ignore_errors=True)


# ---------------------------------------------------------------------------------------------
# The run itself.
# ---------------------------------------------------------------------------------------------

def repository_root():
    """The agg-sharp checkout this script lives in, found the way GoldenImage finds it: by the sln."""
    probe = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    for _ in range(8):
        if os.path.exists(os.path.join(probe, "agg-sharp.sln")):
            return probe
        probe = os.path.dirname(probe)
    raise RuntimeError("agg-sharp.sln not found above this script")


def publish(root, project):
    print(f"publishing {project} with the emdawnwebgpu link...")
    started = time.time()
    result = subprocess.run(
        ["dotnet", "publish", project, "-c", "Debug", "-p:LinkEmdawnWebGpu=true"],
        cwd=root, capture_output=True, text=True)
    if result.returncode != 0:
        sys.stderr.write(result.stdout[-4000:] + result.stderr[-4000:])
        raise RuntimeError("publish failed")
    print(f"published in {time.time() - started:.1f}s")


def await_first_paints(page, url):
    """Waits for the host to report a ready renderer and a painted frame, and returns the boot time.

    An event to wait on would be better than a poll, but the page has no channel that pushes one; the
    heartbeat status line is what BrowserHost already narrates itself through. What matters is that
    this waits on a *condition* and not on a duration - a fast machine does not sit out a timeout, and
    a slow one is not raced.
    """
    page.call("Page.navigate", {"url": url}, timeout=60)
    deadline = time.time() + BOOT_TIMEOUT_SECONDS
    started = time.time()
    last = ""
    while time.time() < deadline:
        try:
            last = page.evaluate("document.getElementById('status').textContent") or ""
        except Exception:
            last = ""
        match = HEARTBEAT.search(last)
        if match and match.group(3) == "ready" and int(match.group(2)) >= MIN_PAINTS:
            return time.time() - started
        time.sleep(0.1)

    raise TimeoutError(f"the page never painted. Last status: {last!r}")


def capture(page, scene):
    """Takes the capture inside the wasm module and reads the PNG back out as bytes."""
    export, assembly = scene["capture_export"]
    namespace, _, member = export.rpartition(".")
    result = page.evaluate(
        "(async () => {"
        " const runtime = await getDotnetRuntime(0);"
        f" const host = await runtime.getAssemblyExports('{assembly}');"
        f" await host.{namespace}.{member}('{WASM_CAPTURE_PATH}');"
        " const agg = await runtime.getAssemblyExports('agg_platform_browser.dll');"
        " const bytes = agg.MatterHackers.Agg.Platform.Browser.BrowserCaptureInterop"
        f".ReadCaptureAsBase64('{WASM_CAPTURE_PATH}');"
        " return bytes === null ? '' : bytes;"
        "})()", await_promise=True, timeout=120)

    if not result:
        raise RuntimeError(
            f"the capture at '{WASM_CAPTURE_PATH}' came back empty - the window painted but"
            " CaptureScreenshotAsync wrote nothing")
    return base64.b64decode(result)


def run(arguments):
    root = repository_root()
    scene_name = arguments.scene
    scene = SCENES[scene_name]

    golden_directory = os.path.join(root, "TestData", "GoldenImages", "browser")
    golden_path = os.path.join(golden_directory, scene_name + ".png")
    failure_directory = os.path.join(root, "examples", "BrowserHost", "bin", "GoldenImageFailures")

    if not arguments.no_publish:
        publish(root, scene["project"])

    publish_root = os.path.join(root, scene["publish_root"])
    if not os.path.isdir(publish_root):
        raise RuntimeError(f"no publish output at '{publish_root}' - drop --no-publish")

    server = StaticServer(publish_root)
    chrome = Chrome(os.path.join(tempfile.gettempdir(), "agg-browser-goldens-chrome.log"))
    page = None
    try:
        page = chrome.open_page()
        boot_seconds = await_first_paints(page, server.url)
        print(f"'{scene_name}' painted in {boot_seconds:.1f}s at {CAPTURE_WIDTH}x{CAPTURE_HEIGHT}")
        png = capture(page, scene)
    finally:
        # The console tail is printed either way. On a failure it is usually the only explanation
        # there is - a wasm exception never reaches this process any other route.
        if page is not None:
            for line in page.console_lines()[-12:]:
                print("  page: " + line.split("\n")[0][:200])
        chrome.close()
        server.close()

    # Printed every run so two runs can be compared for byte identity, not only pixel identity - the
    # encoder is part of what a golden pins, and a PNG that re-encodes differently each run would make
    # the checked-in file churn even while the pixels held still.
    print(f"capture: {len(png)} bytes, sha256 {hashlib.sha256(png).hexdigest()}")

    rendered = read_png(png)
    print(f"capture: {rendered[0]}x{rendered[1]}, {rendered[2]} channels")

    if arguments.regen:
        os.makedirs(golden_directory, exist_ok=True)
        with open(golden_path, "wb") as handle:
            # The module's own PNG bytes, not a re-encode: what is checked in is then exactly what a
            # later run produces, and byte identity is a usable signal.
            handle.write(png)
        print(f"golden regenerated: {golden_path} ({len(png)} bytes)")
        # A regenerate run fails on purpose, exactly as GoldenImage.Check does: nothing was verified,
        # and a runner that rewrote its own expectations and reported success would be worse than none.
        print(f"FAIL: '{scene_name}' was regenerated, not compared. Re-run without --regen to verify"
              " that this machine reproduces what it just captured.")
        return 2

    if not os.path.exists(golden_path):
        actual = os.path.join(failure_directory, f"{scene_name}.actual.png")
        os.makedirs(failure_directory, exist_ok=True)
        with open(actual, "wb") as handle:
            handle.write(png)
        print(f"FAIL: there is no browser golden for '{scene_name}'. Expected '{golden_path}'."
              f" What this run rendered is at '{actual}' - check it against the same scene in another"
              " backend's folder, confirm any difference is rasterization and not a rendering bug,"
              " then re-run with --regen and commit the PNG.")
        return 1

    golden = read_png(golden_path)
    difference = compare(golden, rendered, arguments.channel_tolerance)
    matched = (difference["same_shape"]
               and difference["percent"] <= arguments.max_percent_differing)

    if not matched:
        os.makedirs(failure_directory, exist_ok=True)
        actual = os.path.join(failure_directory, f"{scene_name}.actual.png")
        with open(actual, "wb") as handle:
            handle.write(png)
        diff = "(no diff image - the sizes differ)"
        if difference["same_shape"]:
            diff = os.path.join(failure_directory, f"{scene_name}.diff.png")
            write_png(diff, build_diff_image(golden, difference))
        print(f"FAIL: '{scene_name}' does not match '{golden_path}'. {describe(difference)}"
              f" Rendered: '{actual}'. Diff: '{diff}'."
              " If this is a deliberate change, re-run with --regen and commit the new golden.")
        return 1

    # A pass must not leave a previous run's failure images behind: they are named after the scene, so
    # a stale pair reads exactly like a fresh failure to whoever opens the folder next.
    for suffix in ("actual", "diff"):
        stale = os.path.join(failure_directory, f"{scene_name}.{suffix}.png")
        if os.path.exists(stale):
            os.remove(stale)

    print(f"PASS: '{scene_name}' matches '{golden_path}'. {describe(difference)}")
    return 0


def main():
    parser = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    parser.add_argument("--scene", default="browser-host-demo", choices=sorted(SCENES),
                        help="which golden scene to render")
    parser.add_argument("--regen", action="store_true",
                        help="write the golden instead of comparing (always exits non-zero)")
    parser.add_argument("--no-publish", action="store_true",
                        help="reuse the existing publish output instead of republishing")
    parser.add_argument("--channel-tolerance", type=int, default=0,
                        help="largest per-channel difference still counted as equal (default 0)")
    parser.add_argument("--max-percent-differing", type=float, default=0.0,
                        help="percentage of pixels allowed to exceed the tolerance (default 0)")
    return run(parser.parse_args())


if __name__ == "__main__":
    sys.exit(main())
