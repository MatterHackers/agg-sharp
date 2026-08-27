# BrowserHost

The first browser-runnable artifact of the wasm port: a Blazor WebAssembly page that boots
`PlatformBrowser` and shows a plain agg `SystemWindow` in it. It is the graduated shell of
`spikes/WebGpuWasmSpike` with all the interesting parts moved out - `frameLoop.js`, `input.js`,
`peripherals.js`, `BrowserHostBootstrap`, `BrowserSystemWindow` and `WebGpuBrowserWindowProvider`
all live in `PlatformBrowser` now, so what is left here is a `Program.cs` and a page.

It exists to prove the platform, not to be a product. It knows nothing about MatterCAD.

## Running it

```
dotnet run --project examples/BrowserHost/BrowserHost.csproj
```

This runs, ticks and translates input, but it does **not paint**: `dotnet run` builds without the
emdawnwebgpu link, so the WebGPU entry points are unbacked and the device cannot be created. The
page reports that in the status line at the bottom left and in the devtools console. To see it
paint, publish with the link (below).

What the status line and console say either way:

- `agg is up on Browser, window provider WebGpuBrowserWindowProvider, canvas 900x557 @ 1x`
- `button at (agg screen space) L:351.5, B:262.8, R:422.8, T:288.8` - where a script driving the
  page clicks. Screen space is agg's, so **y counts up from the bottom of the canvas**: a click at
  CSS y is agg y `canvasHeight - y`.
- `ticks N, paints M, renderer ready|not ready, last input: ...` once a second, from an idle action
  that re-queues itself. A rising tick count is the frame loop; the line arriving at all is the idle
  queue draining.

Clicking the button logs `button click N` and updates its label, typing logs `key down X`, and the
cursor becomes an I-beam over the text box (agg `Cursors.IBeam` -> CSS `text`).

## Making it paint

```
dotnet publish examples/BrowserHost/BrowserHost.csproj -c Debug -p:LinkEmdawnWebGpu=true
```

then serve `bin/Debug/net10.0/publish/wwwroot` with any static file server. `LinkEmdawnWebGpu`
belongs to `WebGpu/build/WebGpuBrowser.targets`, which this project imports; it statically links
Dawn's Emscripten WebGPU implementation into the wasm module, which is a full `emcc` relink and
needs the `wasm-tools` workload. The first link seeds a ~215 MB Emscripten cache under
`WebGpu/Browser/` and takes ~25 s; later ones are a few seconds.

Chrome needs WebGPU available - headless runs want `--enable-unsafe-webgpu`. A browser with no
WebGPU gets `This browser does not support WebGPU, which MatterCAD requires.` in the status line and
the underlying exception in the console. There is no fallback renderer anywhere in agg, by design.

`dotnet build`, `dotnet run` and a plain `dotnet publish` still work with **no `wasm-tools`
workload**, because nothing native links without that switch. Publish prints a recommendation to
install it; that is about AOT and relinking, not correctness. A Release publish (which runs the
trimmer) boots too - the providers are resolved by type name through `AggContext.CreateInstanceFrom`,
and ILLink kept them.

## Taking a screenshot from outside the browser

`CaptureScreenshotAsync` writes a real PNG - of the WebGPU frame, not of the page - but it writes it
into Emscripten's in-memory filesystem, which nothing outside the wasm module can see. Two exports
make that reachable from a script driving the page (this is what a golden image runner will use):

```js
const rt = await getDotnetRuntime(0);
const host = await rt.getAssemblyExports('BrowserHost.dll');
await host.MatterHackers.Agg.Examples.BrowserHostProgram.CaptureAsync('/capture.png');

const agg = await rt.getAssemblyExports('agg_platform_browser.dll');
const base64 = agg.MatterHackers.Agg.Platform.Browser.BrowserCaptureInterop.ReadCaptureAsBase64('/capture.png');
```

`CaptureAsync` is demo plumbing and belongs to this example; `ReadCaptureAsBase64` is the platform's,
and is the only part meant to be permanent. Verified against a CDP `Page.captureScreenshot` of the
same moment: identical over the canvas, which is everything the page draws except its own DOM status
line.

## How the JS modules reach the page

The one thing this example had to settle. `PlatformBrowser` is a plain `Microsoft.NET.Sdk` library,
so its `wwwroot/*.js` do not automatically become static web assets of a Blazor head, and a Blazor
app serves *only* what is in the static web assets manifest - files that merely land in
`bin/.../wwwroot` are not served. (Verified: they 404'd.)

What works, and what this project does:

1. `BrowserHost.csproj` imports `PlatformBrowser/build/PlatformBrowser.targets` directly, so the
   `Content` items are declared in **this** project's evaluation, where the static web assets
   pipeline can see them. A `ProjectReference` alone is not enough; content flows to a consumer's
   *output folder*, not into its item lists.
2. That target runs `BeforeTargets="AssignTargetPaths;ResolveProjectStaticWebAssets"` so the items
   exist before `DefineStaticWebAssets` reads `@(Content)`.
3. Each item carries `ContentRoot` metadata pointing at `PlatformBrowser/wwwroot/`. Without it the
   assets are attributed to *this* project's `wwwroot/`, where the files do not exist - which
   serves a 200 with an empty body rather than an error, so it is worth knowing about.

No Razor-SDK flavour of `PlatformBrowser`, and no hand-written `StaticWebAsset` items, were needed.
