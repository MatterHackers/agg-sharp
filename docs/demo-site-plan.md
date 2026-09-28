# agg-sharp demo site plan

One C# app, built from agg-sharp, that runs as a GitHub Pages wasm site (served from the
`larsbrubaker/agg-sharp` fork) and natively on mac (Windows later). It proves the framework
end to end. Two sections, every pixel drawn by agg-sharp (no HTML chrome):

- **AGG Demos** - agg-rust's demos (62 of its 63; conv_dash has no distinct scene there), byte-identical to C++ AGG 2.4, including their
  in-canvas controls (slider/rbox/cbox/scale/gamma/spline ctrls drawn into the image as C++ does).
- **GUI Demo** - feature parity with the agg-gui demo app (windows, shell, widgets).

## References

- agg-rust (`~/Development/rust-apps/agg-rust`): per-demo spec in `docs/track/demo-porting-status.md`;
  renderers in `demo/wasm/src/render/*.rs`; C++ originals in `cpp-references/agg-src/examples/`;
  pixel lessons in `docs/pixel_perfect_check.md`; assets listed below.
- C++ reference renderer: `tools/cpp-renderer` (see its README; `scripts/regen-agg-reference.sh`
  writes goldens to `TestData/AggReference`, `AggReferenceTests` compares them). Each new demo needs
  a headless `demo_<name>.cpp` rewrite of its `on_draw()`. The renderer builds with
  `-ffp-contract=off` (no FMA fusing, matching .NET).
- Demo app: `examples/AggSharpDemo` (library, `.Mac`, `.Browser`); `scripts/publish-demo-site.sh`
  builds the site, `scripts/check-demo-site.py` proves it paints in Chrome (run before pushing -
  CI cannot).
- agg-gui (`~/Development/rust-apps/agg-gui`): window list in `demo-ui/src/specs.rs`, windows in
  `demo-ui/src/windows/`, shell in `demo-ui/src/{top_bar,sidebar,backend_panel,state}.rs`.

## Architecture decisions

- Shared demo library + thin heads: mac head (PlatformMac) and browser head (PlatformBrowser,
  Blazor loader + emdawnwebgpu link as in `examples/BrowserHost`). Demos registered explicitly,
  not via PluginFinder reflection, so trimming cannot drop them.
- Everything on screen renders through the hardware path (WebGPU shaders via Graphics2DGpu), like
  agg-gui - including the AGG demos, and full 3D for the GUI section. No software-render-then-blit.
- An AGG demo draws once through the `Graphics2D` abstraction. On screen that is the GPU path. The
  same Draw into a demo-sized `ImageBuffer` through the software rasterizer is the reference render:
  it is what must be byte-identical to C++, and a per-demo toggle shows it for comparison.
- Pixel checks: C++ goldens (`.raw`) committed under `TestData/AggReference`, one TUnit test per demo
  (and per non-default param set) that byte-compares production render output.
- Correct beats C++: byte-exact C++ parity is the verification tool, not the goal. A genuine C++ AGG
  bug (e.g. the src_atop blue-channel typo agg-rust kept) is fixed in agg-sharp and in the reference
  renderer via patched copies under `tools/cpp-renderer/patches/` (never the external AGG checkout),
  with a code comment and a test, so goldens pin the correct behaviour.

## Phase 0 - foundation (remaining)

1. The hardware Windows GPU goldens (`TestData/GoldenImages/d3d12`) are still stale since the blenders
   round like C++, Ellipse computes angles like C++ and straight-alpha draws accumulate alpha
   correctly; they need regenerating on a real Windows GPU (the `d3d12-warp` goldens are current):
   Primitives2D.FilledPaths, Primitives2D.Gradients, Primitives2D.ImageBlits, Primitives2D.Lines,
   Primitives2D.RoundedRects, Primitives2D.Transforms, Scene.Bed, Scene.BedAlphaBlend,
   Scene.GizmoOverlay, Text.Aa.LightOnDark, Text.Aa.SizeLadder, Text.Aa.SubPixel,
   Text.Lcd.ThenOrdinary.

## Phase 2 - GUI demo parity

Steps and parity matrix in `docs/gui-demo-plan.md`.

## Before bumping MatterCAD's agg-sharp pointer

- MatterCAD sets `ThemeConfig.Current` where it sets its app theme (restyled CheckBox/RadioButton/
  Slider/DropDownList read accent and neutral colours from it; without it slider thumbs show a light
  centre on MatterCAD's dark theme).
- NodeEditorDrawnPixelTests.TheWidthSliderThumbFollowsTheMouseAlongItsDrawnTrack finds the thumb by its
  exact ThumbColor, which the restyled Slider also uses for its trailing fill (and shades on hover and
  press). The slider is right; the test needs TrailingFill off and an unshaded measure.
- Run MatterCAD's full sharded suite: premultiplied-blend edge anti-aliasing, the bilinear image filter
  and the halo GPU fills all change pixels slightly; compare MatterCAD screens before/after.
- A vertical FlowLayoutWidget now pads its unaligned children whichever of Padding and HAnchor was set
  first. Compare these MatterCAD screens, whose flows set Stretch before Padding (paths under `MatterCADLib/`):
  `DesignTools/PropertyEditor.cs:804`, `PartPreviewWindow/SelectedObjectPanel.cs:84/89`,
  `DialogPages/CheckForUpdatesPage.cs:55/140`, `SlicerConfiguration/UIFields/SurfacedEditorPage.cs:73/167`,
  `CloudServices/ProTools/JobManager/QueueDetailsWidget.cs:197`, `PendingDetailsWidget.cs:283`,
  `CheckInDetailsWidget.cs:64`, `Library/Widgets/ListView/RowListView.cs:89`,
  `PartPreviewWindow/SectionWidget.cs:77`, `PartPreviewWindow/View3D/SceneTreePanelHeader.cs:77`,
  `SetupWizard/DialogPage.cs:112`.
- ScrollableWidget's per-axis resize restore and flush-left clamp: check SheetEditorWidget and
  ThemedHorizontalScrollBar.
- GPU texture uploads keep straight alpha: check 3D textures and icons.
- Graphics2DGpu.Clear clears the screen rect: check screens that clear a sub-region.
- The halo AA edge is thinner (HaloWidth 0.5; the edge carries min(1, HaloWidth)): compare MatterCAD screens.
- VertexSourceToTesselator's end_poly fix changes meshes generated from 2D paths: run MatterCAD's mesh and
  design-tool tests.
- Every GpuRenderTarget is owned by its GlCompatContext and released with it: check MatterCAD code that keeps
  a render target past its context.

## Phase 3 - hardening

wasm size/startup, frame budget, mac .app bundle, Windows head.
