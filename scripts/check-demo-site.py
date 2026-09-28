#!/usr/bin/env python3
#
# Copyright (c) 2026, Lars Brubaker
# All rights reserved.
#
# Redistribution and use in source and binary forms, with or without modification, are permitted
# provided that the conditions of the agg-sharp BSD 2-clause licence are met. See LICENSE.
"""Proves a published demo site paints: serves it under a subpath, loads it in headless Chrome, screenshots it.

  scripts/publish-demo-site.sh                       # build the site the Pages workflow deploys
  scripts/check-demo-site.py --out /tmp/site.png     # serve it at /agg-sharp/ and screenshot it
  scripts/check-demo-site.py --out /tmp/gui.png --demo "GUI Demo"   # the same, opened on the GUI demo
  scripts/check-demo-site.py --out /tmp/gui.png --persistence       # the GUI demo remembers its state across a reload

The subpath is the point: GitHub Pages serves this repo's site from https://<user>.github.io/agg-sharp/,
not a domain root, so a site that only boots at "/" would deploy and then show nothing. Serving from
/agg-sharp/ here catches that before a push does.

"Painted" is the app's own word, not a guess from pixels: the check waits until the head's
AggSharpDemoBrowserProgram.PaintState export reports a ready WebGPU renderer and at least one painted
frame. A pixel heuristic alone was tried and is unsound - the dark boot page with its "loading..." line
is already dozens of anti-aliased colours. Any page exception, console error, failed request or error
status line fails the run and is printed.

The screenshot is Chrome's composited page (CDP Page.captureScreenshot), which does include the WebGPU
canvas (measured identical to the wasm-side capture in examples/BrowserHost/README.md). After the paint
signal it must also show a picture outside the status line, so a frame that painted nothing still fails.
--persistence proves the browser head's state store (BrowserDemoStateStore, wwwroot/demoState.js) at
runtime: after the GUI demo paints, it writes a state no default run has (Light theme, Sliders open at a
set place) under the demo's localStorage key, reloads, and requires the page to paint again and the app to
have written that state back - a round trip through the app's Restore and its next save, not just the
script's own write.

The Chrome, CDP and static server plumbing is run-browser-goldens.py's, imported rather than copied.
"""

import argparse
import base64
import importlib.util
import json
import os
import re
import sys
import shutil
import tempfile
import time
import urllib.parse

_here = os.path.dirname(os.path.abspath(__file__))
_spec = importlib.util.spec_from_file_location("browser_goldens", os.path.join(_here, "run-browser-goldens.py"))
goldens = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(goldens)

# The same size the browser head asks for (SystemWindow(1200, 800) in AggSharpDemo.Browser/Program.cs).
PAGE_WIDTH = 1200
PAGE_HEIGHT = 800

# The status line sits in the bottom-left corner of index.html; the picture check ignores these rows so
# the line's own text cannot pass it.
STATUS_LINE_ROWS = 48

# A cleared canvas is one colour; the sidebar and demo are hundreds.
MIN_DISTINCT_COLORS = 64

PAINT_TIMEOUT_SECONDS = 180

PAINT_STATE = re.compile(r"paints (\d+), renderer (ready|not ready)")

# Asks the running app how far it has got. Before the runtime is up there is nothing to ask, which is
# "loading", not a failure.
PAINT_STATE_SCRIPT = (
    "(async () => {"
    " if (typeof getDotnetRuntime !== 'function') return 'loading';"
    " const runtime = getDotnetRuntime(0);"
    " if (!runtime) return 'loading';"
    " const app = await runtime.getAssemblyExports('AggSharpDemo.Browser.dll');"
    " return app.MatterHackers.AggSharpDemo.AggSharpDemoBrowserProgram.PaintState();"
    "})()")

# Two animation frames: the one the painted frame is presented in, and one after it is composited.
TWO_FRAMES_SCRIPT = "new Promise(r => requestAnimationFrame(() => requestAnimationFrame(() => r(true))))"

# BrowserDemoStateStore's key and the state --persistence plants under it: nothing a default run has.
DEMO_STATE_KEY = "AggSharpDemo.GuiDemo.State"
PLANTED_WINDOW = {"Title": "Sliders", "Open": True, "X": 123.0, "Y": 45.0, "Width": 360.0, "Height": 290.0}
PLANTED_STATE = {"Version": 1, "Theme": "Light", "Accent": "Green", "SnapEnabled": True, "BackendPanelOpen": False,
                 "Windows": [PLANTED_WINDOW], "ZOrder": ["Sliders"]}

# The app saves SaveDelaySeconds (0.5 s) after a change; this is generous for a slow CI page.
SAVE_TIMEOUT_SECONDS = 20
STARTUP_SAVE_WAIT_SECONDS = 3


def distinct_colors_above_status_line(png_bytes):
    width, height, channels, pixels = goldens.read_png(png_bytes)
    seen = set()
    for at in range(0, width * max(0, height - STATUS_LINE_ROWS) * channels, channels):
        seen.add(pixels[at:at + 3])
        if len(seen) >= MIN_DISTINCT_COLORS:
            break
    return len(seen)


def page_errors(page):
    """Exceptions, console errors and failed requests seen so far - each one a reason to fail."""
    errors = []
    for event in page.events:
        method = event.get("method")
        params = event.get("params", {})
        if method == "Runtime.exceptionThrown":
            details = params.get("exceptionDetails", {})
            errors.append("exception: " + (details.get("exception", {}).get("description")
                                           or details.get("text", "")).split("\n")[0][:300])
        elif method == "Runtime.consoleAPICalled" and params.get("type") in ("error", "assert"):
            errors.append("console error: " + " ".join(
                str(a.get("value", a.get("description", ""))) for a in params.get("args", [])).split("\n")[0][:300])
        elif method == "Network.loadingFailed" and not params.get("canceled"):
            errors.append(f"request failed: {params.get('errorText')} ({params.get('requestId')})")
        # Chrome asks for /favicon.ico at the domain root on its own; that is outside the site (on Pages it
        # is github.io's), so its 404 says nothing about the site.
        elif (method == "Network.responseReceived" and params["response"].get("status", 200) >= 400
              and not params["response"].get("url", "").endswith("/favicon.ico")):
            errors.append(f"HTTP {params['response']['status']}: {params['response'].get('url')}")
    return errors


def await_paint(page):
    """Polls the app's paint state until it has painted, failing early on any error. Returns the state."""
    started = time.time()
    state = ""
    while time.time() - started < PAINT_TIMEOUT_SECONDS:
        # Null-safe: Page.navigate returns at commit, possibly before #status is parsed.
        status = page.evaluate("document.getElementById('status')?.textContent ?? ''") or ""
        if "startup failed" in status or "does not support WebGPU" in status:
            raise RuntimeError(f"the page reported: {status}")
        errors = page_errors(page)
        if errors:
            raise RuntimeError("the page failed:\n  " + "\n  ".join(errors))
        state = page.evaluate(PAINT_STATE_SCRIPT, await_promise=True) or ""
        match = PAINT_STATE.search(state)
        if match and match.group(2) == "ready" and int(match.group(1)) >= 1:
            return state, time.time() - started
        time.sleep(0.1)
    raise TimeoutError(f"the page never painted. Last paint state: {state!r}")


def read_demo_state(page):
    return page.evaluate(f"localStorage.getItem({json.dumps(DEMO_STATE_KEY)})")


def plant_state_and_reload(page):
    """Writes PLANTED_STATE and reloads. Returns what the first load had saved, and the planted string."""
    # Reported, not required: the first load saves only because laying out the canvas moves its windows.
    started = time.time()
    startup = read_demo_state(page)
    while not startup and time.time() - started < STARTUP_SAVE_WAIT_SECONDS:
        time.sleep(0.1)
        startup = read_demo_state(page)
    planted = json.dumps(PLANTED_STATE)
    page.evaluate(f"localStorage.setItem({json.dumps(DEMO_STATE_KEY)}, {json.dumps(planted)})")
    # A marker on the old document, so the paint wait cannot read the old page's "painted" before the new one.
    page.evaluate("window.aggBeforeReload = true")
    page.call("Page.reload", {"ignoreCache": False}, timeout=60)
    started = time.time()
    while page.evaluate("window.aggBeforeReload === true"):
        if time.time() - started > 60:
            raise TimeoutError("the page never reloaded")
        time.sleep(0.05)
    return startup, planted


def await_restored_state(page, planted):
    """
    Waits for the reloaded app to save, and checks it saved the planted state back: it can only have done so
    by reading it (Restore) and writing it (the save that follows the first layout). Returns a description.
    """
    started = time.time()
    saved = None
    while time.time() - started < SAVE_TIMEOUT_SECONDS:
        saved = read_demo_state(page)
        if saved and saved != planted:
            break
        time.sleep(0.1)
    else:
        raise RuntimeError(f"the reloaded GUI demo never saved its state to localStorage '{DEMO_STATE_KEY}'"
                           f" (it still holds {'the planted state' if saved == planted else repr(saved)})")

    state = json.loads(saved)
    problems = []
    for key in ("Theme", "Accent"):
        if state.get(key) != PLANTED_STATE[key]:
            problems.append(f"{key} is {state.get(key)!r}, planted {PLANTED_STATE[key]!r}")
    window = next((w for w in state.get("Windows", []) if w.get("Title") == PLANTED_WINDOW["Title"]), None)
    if window is None:
        problems.append(f"no '{PLANTED_WINDOW['Title']}' window saved")
    else:
        for key in ("Open", "X", "Y", "Width", "Height"):
            if window.get(key) != PLANTED_WINDOW[key]:
                problems.append(f"{PLANTED_WINDOW['Title']}.{key} is {window.get(key)!r}, planted {PLANTED_WINDOW[key]!r}")
    if (state.get("ZOrder") or [None])[-1] != PLANTED_WINDOW["Title"]:
        problems.append(f"top window is not '{PLANTED_WINDOW['Title']}': {state.get('ZOrder')}")
    if problems:
        raise RuntimeError("the reloaded GUI demo did not restore the planted state:\n  " + "\n  ".join(problems))
    return (f"restored and re-saved the planted state ({len(state.get('Windows', []))} windows,"
            f" theme {state['Theme']}, accent {state['Accent']})")


def run(arguments):
    root = goldens.repository_root()
    site = os.path.abspath(arguments.site or os.path.join(
        root, "examples", "AggSharpDemo", "AggSharpDemo.Browser", "bin", "Release", "net10.0", "publish", "wwwroot"))
    if not os.path.exists(os.path.join(site, "index.html")):
        raise RuntimeError(f"no published site at '{site}' - run scripts/publish-demo-site.sh first")

    # Serve a folder whose only entry is the site under the subpath name, so "/" is not the site.
    subpath = arguments.subpath.strip("/")
    serve_root = site
    if subpath:
        serve_root = tempfile.mkdtemp(prefix="agg-demo-site-")
        os.symlink(site, os.path.join(serve_root, subpath))

    server = goldens.StaticServer(serve_root)
    chrome = goldens.Chrome(os.path.join(tempfile.gettempdir(), "agg-demo-site-chrome.log"))
    page = None
    url = server.url + (subpath + "/" if subpath else "")
    if arguments.persistence:
        arguments.demo = "GUI Demo"
    if arguments.demo:
        url += "#" + urllib.parse.quote(arguments.demo)
    try:
        page = chrome.open_page()
        page.call("Emulation.setDeviceMetricsOverride", {
            "width": PAGE_WIDTH, "height": PAGE_HEIGHT, "deviceScaleFactor": 1, "mobile": False})
        page.call("Page.navigate", {"url": url}, timeout=60)

        persistence = ""
        try:
            state, painted_seconds = await_paint(page)
            if arguments.persistence:
                startup, planted = plant_state_and_reload(page)
                startup_windows = len(json.loads(startup).get("Windows", [])) if startup else 0
                persistence = (f"first load saved its state ({startup_windows} windows); " if startup
                               else f"first load saved nothing within {STARTUP_SAVE_WAIT_SECONDS}s; ")
                state, painted_seconds = await_paint(page)
                persistence += await_restored_state(page, planted)
        except Exception as failure:
            png = base64.b64decode(page.call("Page.captureScreenshot", {"format": "png"})["result"]["data"])
            with open(arguments.out, "wb") as handle:
                handle.write(png)
            print(f"FAIL: {url}: {failure}\n  Screenshot: {arguments.out}")
            return 1

        page.evaluate(TWO_FRAMES_SCRIPT, await_promise=True)
        png = base64.b64decode(page.call("Page.captureScreenshot", {"format": "png"})["result"]["data"])
        with open(arguments.out, "wb") as handle:
            handle.write(png)

        errors = page_errors(page)
        colors = distinct_colors_above_status_line(png)
        if errors or colors < MIN_DISTINCT_COLORS:
            print(f"FAIL: {url} reported '{state}' but the screenshot shows {colors} colours above the status"
                  f" line. Screenshot: {arguments.out}" + "".join("\n  " + e for e in errors))
            return 1

        transferred = sum(
            event["params"].get("encodedDataLength", 0) for event in page.events
            if event.get("method") == "Network.loadingFinished")
        print(f"PASS: {url} painted ({state}) in {painted_seconds:.1f}s; {transferred / 1e6:.1f} MB"
              f" transferred. Screenshot: {arguments.out}" + (f"\n  persistence: {persistence}" if persistence else ""))
        return 0
    finally:
        if page is not None:
            for line in page.console_lines()[-12:]:
                print("  page: " + line.split("\n")[0][:200])
        chrome.close()
        server.close()
        if serve_root != site:
            shutil.rmtree(serve_root, ignore_errors=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    parser.add_argument("--out", required=True, help="where to write the screenshot PNG")
    parser.add_argument("--site", help="the published wwwroot (default: publish-demo-site.sh's output)")
    parser.add_argument("--subpath", default="agg-sharp",
                        help="serve the site under this path, as GitHub Pages does (default agg-sharp; '' for the root)")
    parser.add_argument("--demo", help="open the site on this demo (the URL fragment), e.g. 'GUI Demo'")
    parser.add_argument("--persistence", action="store_true",
                        help="open the GUI demo, plant a saved state, reload, and require the app to restore it")
    return run(parser.parse_args())


if __name__ == "__main__":
    sys.exit(main())
