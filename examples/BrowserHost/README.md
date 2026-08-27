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

Open the printed `http://localhost:<port>` URL. Nothing paints - the WebGPU render layer arrives
in W4 - so the page stays dark and reports itself in the status line at the bottom left and in the
devtools console:

- `agg is up on Browser, window provider WebGpuBrowserWindowProvider, canvas 900x557 @ 1x`
- `button at (agg screen space) L:351.5, B:262.8, R:422.8, T:288.8` - where to click, since you
  cannot see it. Screen space is agg's, so **y counts up from the bottom of the canvas**: a click
  at CSS y is agg y `canvasHeight - y`.
- `ticks N, paints 0, last input: ...` once a second, from an idle action that re-queues itself.
  A rising tick count is the frame loop; the line arriving at all is the idle queue draining.

Clicking the button logs `button click N`, typing logs `key down X`, and the cursor becomes an
I-beam over the text box (agg `Cursors.IBeam` -> CSS `text`).

`dotnet build`, `dotnet run` and `dotnet publish` all work with **no `wasm-tools` workload**,
because nothing native links. Publish prints a recommendation to install it; that is about AOT and
relinking, not correctness. A Release publish (which runs the trimmer) boots too - the providers
are resolved by type name through `AggContext.CreateInstanceFrom`, and ILLink kept them.

### Watching the paint throw be contained

`http://localhost:<port>/?forcepaint` sets `BrowserSystemWindow.RenderLayerReady` before there is a
render layer, so the tick tries to paint and `NewGraphics2D` throws its "no device yet" message.
The console shows the contained report - `tick phase 'paint' threw; this frame is abandoned and the
loop continues` - and the loop keeps running.

It is not a per-frame flood: `BrowserFrameTick` clears its redraw flag *before* painting, so a
failing frame costs one message per invalidation. Measured: 1 paint (and 1 message) in the first
~1500 ticks, and 3 after two button clicks. W4 replaces the switch by making `RenderLayerReady`
follow the real device's lifetime.

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
