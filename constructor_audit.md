# Constructor Thread-Cleanliness Audit — agg-sharp

**Date:** 2026-07-29 · **Branch:** `thread-clean-constructors` · **Phase 1: read-only audit, no code changes**

---

## Phase-2 completion status (2026-08-01)

All seven suggested Phase-2 batches have been implemented and merged to `main`:

| Batch | Merge commit(s) |
|-------|-----------------|
| 1. Font cluster | `d8bd37d6` (batch/fonts), `1819a8f2` (batch/font-cache), `6db8c3c2` (StyledTypeFaceImageCache bounding) |
| 2. Blender saturation tables | `f473445d` (batch/blenders) |
| 3. Singleton hardening | `335e341c` (batch/singletons) |
| 4. Ctor-escape fixes | `fdbeaa5d` (batch/window), `ade8099f` (centering/latch review fixes) |
| 5. Theme statics | included in batch/singletons (`a1a537cd`) |
| 6. Event-subscription hygiene | `9cd03952` (batch/events), `0a81b5a2` (batch/medium-hygiene), `05f04849` (Batch F) |
| 7. GuiWidget base ctor + layout family | `988b0531` (batch/guiwidget-ctor), `47f36f95` (Batch H) |

A per-item verification sweep on 2026-08-01 confirmed every High finding's fix is present in
`main` (Lazy fonts with ExecutionAndPublication, static readonly blender tables, locked
singletons/latches, ToolTipManager CompareExchange activation, OnLoad-deferred animation
starts, handle-safe TitleBarHeight, locked ScrollBar theme defaults, bounded and fully locked
StyledTypeFaceImageCache, virtual-dispatch-free GuiWidget base ctors, OnClosed unhook paths).

**Known residuals (accepted, tracked in code):**

- `MarkdownWidget.Theme` — theme is now constructor-injected and the static is guarded
  (`Volatile.Read/Write` + `ReferenceEquals` skip), but the ctor still refreshes the
  process-global static; in-code TODO covers full per-widget injection.
- `InternalTextEditWidget.DefaultRightClick` — ctor check-then-`+=` race fixed under lock;
  a hypothetical external mutator of the public static field remains unsynchronized
  (benign in MatterCAD, which pre-seeds it once at startup).
- `ConsoleWidget` — dead class **deleted** (was REJECTED-as-hazard but carried real latent
  defects; zero references existed in agg-sharp or MatterCAD).
- `WorldViewExtensions.scaledLineMesh`/`unscaledLineMesh` — shared mutable Meshes mutated
  per-render-call without a lock; render-path (not ctor) hazard, deliberately out of scope.
- Low-severity layout-in-ctor widget family — largely defused by the Batch H base-ctor fix
  (no virtual dispatch before derived ctor bodies run); individual widgets not mechanically
  swept.

**Scope:** Gui/, agg/Font/, agg/ (rest), RenderGl/, RenderOpenGl/, PlatformWin32/, PlatformGtk/, GuiAutomation/, MarkdigAgg/, ImageProcessing/, DataConverters2D/, DataConverters3D/, VectorMath/. Excluded: bin/, obj/, Tests/, examples/, third-party (Typography, geometry3Sharp, clipper, Triangle).

**Method:** 8 directory-scoped reader agents + 1 cross-cutting statics sweep; every High finding was then adversarially verified by a second agent that traced concrete instantiation paths (including into the consuming MatterCAD app) and actively tried to refute the claim. Verdicts:

- **CONFIRMED** — violation accurate AND concretely reachable during UI spin-up (RootSystemWindow construction, splash, ReloadAll, first draw), with the path cited.
- **PLAUSIBLE** — code defect is real, but no spin-up path could be established from current code.
- **REJECTED** — the claim was wrong (dead code, already lock-guarded); kept in a separate section for honesty.

**Rule key:** R1 static/shared mutation · R2 thread-affine API (GL, UiThread) · R3 event subscription on singletons/long-lived objects · R4 file/settings/network I/O · R5 virtual dispatch during ctor · R6 layout/measurement during ctor · R7 heavy computation.

---

## Executive summary

- **66 findings** across 8 areas; **22 High**, of which **12 CONFIRMED** reachable during spin-up, 8 PLAUSIBLE, 2 REJECTED.
- The single biggest hazard cluster is the **font static web**: `AggContext` type-init parses two ~285 KB embedded SVG fonts under the CLR type-init lock, `LiberationSansFont`/`LiberationSansBoldFont.Instance` publish a **torn, partially-parsed TypeFace** (field assigned *before* `ReadSVG` fills it), and `TextWidget`/`TypeFacePrinter` ctors force full text measurement, all first-touched from whichever thread builds widgets first. This is the cluster behind the AnchorTests parallel flakiness — verification showed the tear is observable via *direct* `LiberationSansFont.Instance` callers (tests, examples, MatterCAD's `ApplicationController.TypeFaceCache`) racing the `AggContext` path.
- Verification materially **corrected several findings** — e.g. `UiThread.SetInterval`'s list is lock-guarded (the real hazard is callbacks capturing partially-constructed objects), `PerformancePanel` is lock-safe, `ConsoleWidget` is dead code, and the `BlenderPreMultBGRA` table race is *worse* than reported (a second thread can skip the fill and read unwritten entries → wrong pixels).
- **Model fix already in-tree:** `Graphics2DGpu` (RenderGl/Renderer/Graphics2DGpu.cs) does per-GL-context caches under `contextCachesLock` with a volatile cache generation — RenderGl came back nearly clean. Use it as the template for per-context state.

---

## Top 10 highest-risk findings (ranked)

| # | Where | Rules | Verdict | What happens |
|---|-------|-------|---------|--------------|
| 1 | `AggContext` static font initializers — agg/Platform/AggContext.cs:160–166 | R1, R7 | CONFIRMED | First touch (any `TextWidget`/`TypeFacePrinter` ctor) synchronously parses BOTH embedded fonts (~570 KB SVG) inside the CLR type-init lock; every other thread needing text blocks for the full parse. Deadlock surface + hidden spin-up stall. |
| 2 | `LiberationSansFont` / `LiberationSansBoldFont.Instance` — agg/Font/LiberationSansFont.cs:15, LiberationSansBoldFont.cs:15 | R1, R7 | PLAUSIBLE (defect certain, tear needs a direct-caller race) | `instance = new TypeFace()` is published **before** `ReadSVG` populates it, unlocked. A concurrent direct caller (IVertexSourceTests, MatterCAD `ApplicationController.TypeFaceCache`) sees `UnitsPerEm == 0` → `StyledTypeFace` computes Infinity em-scaling. Best explanation found for the ~5/15 AnchorTests failures. |
| 3 | `TextWidget` ctor — Gui/TextWidgets/TextWidget.cs:135–143 | R1, R6, R7 | CONFIRMED | Every TextWidget measures its text at construction and first-touches the font static web. MatterCAD builds `MainViewWidget` in `Task.Run` while the UI thread renders splash text — two threads in the font machinery simultaneously during every startup. |
| 4 | `MarkdownWidget` ctor writes static `Theme` — MarkdigAgg/MarkdownWidget.cs:87 | R1, R6 | CONFIRMED | Every MarkdownWidget construction overwrites the process-global `MarkdownWidget.Theme`, read later by `TextLinkX`/`ImageLinkSimpleX`. Confirmed constructed on a background thread-pool thread during spin-up (UpgradeToProTabPage) while UI-thread tooltips also construct MarkdownWidgets. |
| 5 | `ThemeConfig` ctor → `RebuildTheme` — Gui/Theme/ThemeConfig.cs:370, 389–391 | R1, R7 | CONFIRMED | Writes three unguarded globals (`ScrollBar.DefaultBackgroundColor/DefaultThumbColor/DefaultThumbHoverColor`), triggers `AggContext` unguarded lazy OS/Config reflection init, software-renders two color-picker images. Constructed during startup theme setup and on-demand (`DefaultMenuTheme`) on both threads. |
| 6 | `BlenderPreMultBGRA` ctor — agg/Image/Blenders/BlenderPreMultBGRA.cs:41 | R1 | CONFIRMED | Unlocked check-then-fill of static `m_Saturate9BitToByte[512]`. Worse than idempotent: thread B sees `[2] != 0` mid-fill, skips, and blends with unwritten (zero) entries → wrong pixel output. On the core first-draw path (back-buffer alloc, glyph images, icon loads). Same pattern ×5 siblings (BGR, PolyColor, 3× gray). |
| 7 | `SystemWindow` / `ToolTipManager` ctors — Gui/SystemWindow/SystemWindow.cs:102–103, ToolTipManager.cs:78–79 | R2, R3, R5 | CONFIRMED | SystemWindow's ctor spins up ToolTipManager, which subscribes `MouseMove` and starts a 50 ms `UiThread.SetInterval` — publishing callbacks over a partially-constructed window that the UI pump can execute (e.g. while splash pumps) concurrently with ongoing construction. Also sets virtual `BackgroundColor` pre-completion. |
| 8 | `WinformsSystemWindow` ctor — PlatformWin32/win32/WinformsSystemWindow.cs:95–111 | R1, R2, R3, R6 | CONFIRMED | Three violations in one ctor: unlocked lazy static `idleCallBackTimer` (started, holding an instance handler = process-lifetime pin), unlocked `MainWindowsFormsWindow` first-window latch, and `RectangleToScreen(ClientRectangle)` forcing **premature Win32 handle creation** — the native window becomes affine to whatever thread runs the ctor, before OnLoad. |
| 9 | `StaticData` singleton — Gui/StaticData.cs:49–75 | R1 | CONFIRMED | Unguarded check-then-create `Instance` + static `RootPath` write in the private ctor; first-touched from widget ctors (TreeNode, PopupMenu, OnScreenKeyboard, MarkdigAgg cctors) during tree build. Verification found the paired `cachedImages`/`cachedIcons` dictionaries make concurrent first-touch corruption-capable. |
| 10 | `StyledTypeFaceImageCache` — agg/Font/StyledTypeFace.cs:70–113, 190–213 | R1 | PLAUSIBLE | Unlocked lazy singleton; `GetCorrectCache` locks per-TypeFace but mutates the shared top-level Dictionary for distinct TypeFaces unsynchronized, and — worse than reported — the leaf dictionary is read/written entirely **outside** any lock at render time. No eviction. Spin-up gate: only populated when `DrawFromHintedCache` is enabled (MatterControl-side). |

Honorable mentions just below the cut: `ImageSequenceWidget` ctor starting animations via `UiThread.SetInterval` (CONFIRMED via LibraryListView favorites path), `InternalTextEditWidget.DefaultRightClick` check-then-`+=` static race (dead in MatterCAD because Application.cs:498 pre-seeds it; live for any other consumer), `DropArrow` unlocked rebuild-on-DeviceScale-change publishing half-built shared `VertexStorage`.

---

## Inventory grouped by fix pattern

### A. lock-or-freeze-static — add locking, or make the static immutable/frozen after startup

| Sev | Verdict | Class / site | Rules | Notes |
|-----|---------|--------------|-------|-------|
| High | CONFIRMED | `BlenderPreMultBGRA` — agg/Image/Blenders/BlenderPreMultBGRA.cs:41 | R1 | Static ctor or `Lazy<int[]>` for the table kills the whole family. |
| Med | — | `BlenderPreMultBGR` — agg/Image/Blenders/rgb.cs:297 | R1 | Same table pattern. |
| Med | — | `BlenderPolyColorPreMultBGRA` — agg/Image/Blenders/BlenderPolyColorPreMultBGRA.cs:44 | R1 | Same. |
| Med | — | `blender_gray` — agg/Image/Blenders/Gray.cs:50 | R1 | Same. |
| Med | — | `blenderGrayFromRed` — agg/Image/Blenders/Gray.cs:155 | R1 | Same. |
| Med | — | `blenderGrayClampedMax` — agg/Image/Blenders/Gray.cs:256 | R1 | Same. |
| High | CONFIRMED | `ThemeConfig` → `ScrollBar.Default*` — Gui/Theme/ThemeConfig.cs:389–391, ScrollBar.cs:231–233 | R1, R7 | Either freeze scrollbar defaults at startup or pass theme colors per-instance. |
| High | CONFIRMED | `WinformsSystemWindow.idleCallBackTimer` — PlatformWin32/win32/WinformsSystemWindow.cs:95 | R1, R3 | Lock or `Interlocked.CompareExchange` the timer creation; handler already has handle/marshal guards (verified). |
| High | PLAUSIBLE | `WinformsSystemWindow.MainWindowsFormsWindow` — same file:105 | R1 | Single-threaded today (SingleWindowMode); latch with CompareExchange anyway. |
| High | PLAUSIBLE | `StyledTypeFaceImageCache` — agg/Font/StyledTypeFace.cs:70–113 | R1 | Needs: locked singleton, global (not per-TypeFace) guard for the top-level dict, lock or ConcurrentDictionary for the leaf dict (currently accessed lock-free at lines 191/213), eviction policy. Alternatively per-context via ConditionalWeakTable (Graphics2DGpu pattern). |
| High | PLAUSIBLE | `DropDownList` → `DropArrow` — Gui/Menu/DropDownList.cs:109, DropArrow.cs:42–90 | R1 | `BuildDropArrow` publishes `_downArrow/_upArrow` before populating. Safe at spin-up (static ctor + DeviceScale set earlier — verified); racy on later DeviceScale change. Build-then-publish + lock. |
| High | PLAUSIBLE | `InternalTextEditWidget.DefaultRightClick` — Gui/TextWidgets/InternalTextEditWidget.cs:372, :72 | R1, R6 | Check-then-`+=` on a static; dead branch in MatterCAD (pre-seeded at Application.cs:498) but live for other consumers and double-subscribes under concurrency. |
| High | REJECTED (dead code) | `ConsoleWidget` — Gui/TextWidgets/ConsoleWidget.cs:63–74 | R1, R4 | All claims true in code (static typeFace I/O init, static `_primary = this`) but **zero instantiation sites anywhere** — fix opportunistically or delete the class. |
| Med | — | `MarkdownWidget.Theme` — MarkdigAgg/MarkdownWidget.cs:87 | R1 | CONFIRMED High in ranking above; the *fix* is to stop the ctor writing the static (inject theme / set once at app init) — listed here because freezing the static is the acceptable fallback. |

### B. lazy-init — defer to first use (Lazy<T> / cached property)

| Sev | Verdict | Class / site | Rules | Notes |
|-----|---------|--------------|-------|-------|
| High | CONFIRMED | `AggContext` font initializers — agg/Platform/AggContext.cs:160–166 | R1, R7 | Make `DefaultFont*` `Lazy<TypeFace>` so type-init stops parsing fonts under the CLR lock; combine with #2 below. |
| High | PLAUSIBLE | `LiberationSansFont.Instance` — agg/Font/LiberationSansFont.cs:9–21 | R1, R7 | `Lazy<TypeFace>` with full construction *before* publication. Highest-leverage single fix for test flakiness. |
| High | PLAUSIBLE | `LiberationSansBoldFont.Instance` — agg/Font/LiberationSansBoldFont.cs:9–21 | R1, R7 | Same fix, same file shape. |
| High | CONFIRMED | `TypeFacePrinter` default ctor — agg/Font/TypeFacePrinter.cs:105–112 | R1, R7 | Becomes clean once AggContext/fonts are lazy+safe; otherwise defer font resolution to first render/measure. |
| High | CONFIRMED | `TextWidget` ctor measurement — Gui/TextWidgets/TextWidget.cs:135–143 | R1, R6, R7 | Defer `Printer.LocalBounds`-driven bounds to first layout/OnLoad; font touch becomes safe with the cluster fix. |
| High | PLAUSIBLE | `EnglishTextWrapping(double)` — agg/Font/TextWrapping.cs:73 | R1, R7 | Benign in-repo today (AggContext always pre-initialized by TextWidget — verified); same cluster fix covers it. |
| High | CONFIRMED | `StaticData.Instance` / `RootPath` — Gui/StaticData.cs:49–77 | R1 | `Lazy<StaticData>` + make RootPath set-once; `cachedImages`/`cachedIcons` already lock-guarded. |
| Med | — | `SvgWidget(string filePath)` — Gui/SvgWidget.cs:53–81 | R4, R7 | Parse/rasterize lazily or in OnLoad. |
| Med | — | `OutputScroll` printer field init — Gui/OutputScroll.cs:54 | R1, R7 | Font first-touch from field initializer. |
| Med | — | `ImageLinkSimpleX.icon` static init — MarkdigAgg/Inlines/ImageLinkSimpleX.cs:43 | R4, R1 | Type-init disk I/O mid-markdown-parse. |
| Med | — | `ImageLinkAdvancedX.icon`/`client` static init — MarkdigAgg/Inlines/ImageLinkAdvancedX.cs:43–45 | R4, R1 | Same. |
| Med | — | `WinformsSystemWindow` icon probe — PlatformWin32/win32/WinformsSystemWindow.cs:117 | R4 | CWD-relative `File.Exists` + `new Icon(path)` per window ctor; cache statically once. |
| Low | — | `Graphics2DGpu` eager 1000-item AA tesselator pool — RenderGl/Renderer/Graphics2DGpu.cs:190–193 | R7 | The per-context/generation machinery around it is the model fix — only the eager pool build should become on-demand. |

### C. move-to-OnLoad — defer to OnLoad/Initialize lifecycle hooks

| Sev | Verdict | Class / site | Rules | Notes |
|-----|---------|--------------|-------|-------|
| High | CONFIRMED | `SystemWindow` ctor — Gui/SystemWindow/SystemWindow.cs:102–103 | R2, R5 | Create ToolTipManager in OnLoad (or first-shown); avoid virtual `BackgroundColor` set in ctor. |
| High | CONFIRMED | `ToolTipManager` ctor — Gui/SystemWindow/ToolTipManager.cs:78–79 | R2, R3 | Start interval + MouseMove subscription on window load, not construction. Note (verified): `UiThread.SetInterval`'s list mutation is lock-guarded — the hazard is the callback capturing a partially-constructed pair. |
| High | CONFIRMED | `ImageSequenceWidget` ctor — Gui/ImageSequenceWidget.cs:43–53 | R2, R5 | Don't `animation.Start()` until OnLoad; widget is even constructed `Visible = false` yet ticks (verified via LibraryListView path). |
| High | PLAUSIBLE | `ResponsiveImageSequenceWidget` ctor — Gui/ResponsiveImageSequenceWidget.cs:48–72 | R2, R3 | Same animation start + **double** `ImageSequence.Invalidated` subscription, no OnClosed unsubscribe. Only reachable via markdown image links today. |
| High | CONFIRMED | `WinformsSystemWindow` handle creation — PlatformWin32/win32/WinformsSystemWindow.cs:111 | R2, R6 | Compute `TitleBarHeight` in OnLoad/OnHandleCreated, not the ctor. |
| High | REJECTED (lock-safe) | `PerformancePanel` — Gui/PerformanceTimer/PerformancePanel.cs:59–95 | — | Only reachable under `PerformanceTimer`'s global lock; subscriptions already marshaled via RunOnIdle (verified). No action needed. |
| Med | — | `OnScreenKeyboard` ctor — Gui/OnScreenKeyboard.cs:78, 143 | R4, R3, R7 | JSON read + static `Keyboard.StateChanged` lambda subscriptions (leak). |
| Med | — | `SoftKeyboardDisplayStateManager` — Gui/OnScreenKeyboard.cs:195–196 | R3, R4 | Static TextEditWidget event subscriptions, never removed. |
| Med | — | `SoftKeyboardContentOffset` — Gui/OnScreenKeyboard.cs:411–412 | R3 | Same static event pinning. |
| Med | — | `ImageWidget(listenForImageChanged: true)` — Gui/ImageWidget.cs:61, 91 | R3, R5 | Subscribes to externally-owned (often globally cached) ImageBuffer; not removed on Close. |
| Med | — | `ResponsiveImageWidget` — Gui/ResponsiveImageWidget.cs:47, 102 | R3, R5, R6 | Same + forces layout via own virtual LocalBounds during ctor. |
| Med | — | `ImageLinkAdvancedX` ctor network fetch — MarkdigAgg/Inlines/ImageLinkAdvancedX.cs:64 | R4, R2 | `HttpClient.GetAsync().ContinueWith` in ctor mutates the live widget tree from thread-pool continuations with no UiThread marshal. |
| Med | — | `TreeNode.TreeExpandWidget` — Gui/TreeView/TreeNode.cs:676 | R4 | LoadIcon I/O + recolor per node; hot in large-tree ReloadAll. |
| Med | — | `PopupMenu.CheckboxMenuItem` — Gui/Menu/PopupMenu.cs:166 | R4 | Sibling `RadioMenuItem` already defers to OnLoad (line 216) — copy that pattern. |
| Med | — | `PopupWidget` ctor → `ShowPopup` — Gui/Menu/PopupWidget.cs:100, 306–337 | R3, R6, R7 | Adds a partially-constructed widget to the SystemWindow and subscribes ancestors' events inside the ctor. |
| Med | — | `WinformsEventSink` — PlatformWin32/win32/WinformsEventSink.cs:53 | R3, R2 | 15+ handlers + `AllowDrop = true` (OLE registration) wired in ctor; no unhook path. |
| Med | — | `DiagnosticWidget` — Gui/DiagnosticsWidget.cs:32–34 | R1, R3, R5 | Calls `ShowAsSystemWindow()` from its own ctor (writes SystemWindow statics, DebugLogger filters). Debug-only tool. |
| Med | — | `PerformanceTimer` — Gui/PerformanceTimer/PerformanceTimer.cs:63 | R1, R2 | Constructing a *timer* builds UI + schedules RunOnIdle; lock-guarded but wrong-thread UI creation if ever used off-UI-thread. |
| Low | — | Layout-in-ctor family: `WindowWidget` (:59), `MessageBox` (:20), `ProgressControl` (:51), `GroupBox` (:96), `gamma_ctrl` (:146), `ScrollBar` (:79), `WrappedTextWidget` (:61), `ThemedNumberEdit` (:90), `ButtonViewText` (:45), `CheckBoxViewText` (:46), `CheckBoxViewStates` (:62), `ButtonViewStates` (:44), `RadioButtonViewStates` (:30), `Button` (:50), `RadioButton` (:187), `CheckBox` (:56), `D3D11SystemWindow` (:59), `MarkdownTextWidget` family (MarkdigAgg) | R6, R7, R5 | All force measurement/layout (and font first-touch) during construction. Fix mechanically per-batch after the High/Medium hazards land; mostly becomes benign once fonts + GuiWidget base are addressed. |

### D. per-GL-context ConditionalWeakTable (Graphics2DGpu cache-generation pattern)

| Sev | Class / site | Notes |
|-----|--------------|-------|
| — | `Graphics2DGpu` — RenderGl/Renderer/Graphics2DGpu.cs | **Already correct** — `cachesByContext` under `contextCachesLock`, volatile `cacheGeneration`, GL calls only on the context-owning thread. This is the template. |
| — | Candidate migrations: `StyledTypeFaceImageCache` (glyph images are effectively render-target state), `ImageTexturePlugin`/`MeshTrianglePlugin`/`MeshWirePlugin` caches (already lock-guarded ConditionalWeakTables — verify generation handling only). | |

### E. other / structural

| Sev | Class / site | Rules | Notes |
|-----|--------------|-------|-------|
| Low | `GuiWidget` base ctor — Gui/GUIWidget.cs:772, 789, 1101, 536 | R5, R6 | **Flagged once for the whole toolkit:** both base ctors set virtual `MinimumSize`/`LocalBounds`/`HAnchor`/`VAnchor`; the LocalBounds setter fires OnLayout/Invalidate/virtual OnBoundsChanged. Every derived widget runs virtual layout dispatch before its ctor body completes. Any real fix is a Phase-2 design decision (e.g. defer layout until AddChild/OnLoad, or a construction-suspended flag). |
| Low | `ImageBuffer.InternalImageGraphics2D` — agg/Image/ImageBuffer.cs:43 | R5 | Virtual property set in ctor; currently benign. |
| Low | `ReportTimer` — agg/Helpers/ReportTimer.cs:63 | R1 | Lock-guarded static dictionary write; unbounded growth only. |
| Low | `ScrollBar.GrowThumbBy`/`ScrollBarWidth` static initializers — Gui/ScrollableWidget/ScrollBar.cs:94–96 | R1 | Bake `GuiWidget.DeviceScale` at type-load; stale if type loads before scale is set. |

---

## Master list: statics touched from constructors (the spin-up hazard surface)

Merged from the dedicated sweep + per-area findings. **Bold = mutable and unlocked** (the real hazards). "cctor" = static constructor / static field initializer (runs lazily at first touch — possibly on a background thread).

### Font / text cluster (highest risk)
| Static | Declared in | Access from ctor context | Locked? |
|--------|-------------|--------------------------|---------|
| **`LiberationSansFont.instance`** | agg/Font/LiberationSansFont.cs | rw — AggContext cctor; TextWidget/TypeFacePrinter/EnglishTextWrapping ctors | **no** (torn publish) |
| **`LiberationSansBoldFont.instance`** | agg/Font/LiberationSansBoldFont.cs | rw — AggContext cctor; TypeFacePrinter ctor (bold) | **no** (torn publish) |
| **`StyledTypeFaceImageCache.instance`** | agg/Font/StyledTypeFace.cs | rw — lazy getter from glyph rendering | **no**; leaf dict fully unlocked |
| **`AggContext.DefaultFont` / `DefaultFontBold` / `DefaultFontItalic` / `DefaultFontBoldItalic`** | agg/Platform/AggContext.cs | w in cctor (font parse); r from many widget ctors | **no** (CLR type-init lock only) |
| `LiberationSansFont.Instance.glyphs` (shared TypeFace glyph dict) | agg/Font/TypeFace.cs | rw via measurement in TextWidget/InternalTextEditWidget ctors | yes (`lock(glyphs)`) |
| **`CodeBlockX.monoTypeFace`** | MarkdigAgg/AggCodeBlockRenderer.cs | rw — lazy first-touch (AddLine, near-ctor) | **no** |

### AggContext / platform config
| Static | Declared in | Access | Locked? |
|--------|-------------|--------|---------|
| **`AggContext._config`** | agg/Platform/AggContext.cs | rw — lazy getter (ThemeConfig ctor, DiagnosticWidget ctor) | **no** |
| **`AggContext._osInformation`** | agg/Platform/AggContext.cs | rw — lazy Activator init (ThemeConfig ctor) | **no** |
| **`AggContext._fileDialogs`** | agg/Platform/AggContext.cs | rw — lazy Activator init | **no** |

### StaticData / assets
| Static | Declared in | Access | Locked? |
|--------|-------------|--------|---------|
| **`StaticData._instance`** | Gui/StaticData.cs | rw — lazy getter from many widget ctors + MarkdigAgg cctors | **no** |
| **`StaticData.RootPath`** | Gui/StaticData.cs | rw — private ctor write | **no** |
| `StaticData.cachedIcons` / `cachedImages` | Gui/StaticData.cs | rw — LoadIcon from widget ctors | yes |
| **`ImageLinkSimpleX.icon`**, **`ImageLinkAdvancedX.icon`**, **`ImageLinkAdvancedX.client`** | MarkdigAgg/Inlines/ | rw — cctor does disk I/O / HttpClient; ctor reads | **no** |

### Widget/theme globals
| Static | Declared in | Access | Locked? |
|--------|-------------|--------|---------|
| **`ScrollBar.DefaultBackgroundColor` / `DefaultThumbColor` / `DefaultThumbHoverColor`** | Gui/ScrollableWidget/ScrollBar.cs | w from ThemeConfig ctor; r from ScrollBar ctor | **no** |
| **`ScrollBar.GrowThumbBy` / `ScrollBarWidth`** | Gui/ScrollableWidget/ScrollBar.cs | rw — cctor bakes DeviceScale; r in ctor | **no** |
| **`MarkdownWidget.Theme`** | MarkdigAgg/MarkdownWidget.cs | w from every MarkdownWidget ctor | **no** |
| **`DropArrow._downArrow` / `_upArrow` / `calculatedDeviceScale`** | Gui/Menu/DropArrow.cs | rw — cctor + unlocked rebuild via DropDownList ctor | **no** (publish-before-populate) |
| **`InternalTextEditWidget.DefaultRightClick`** | Gui/TextWidgets/InternalTextEditWidget.cs | rw — check-then-`+=` in ctor | **no** |
| `GuiWidget.DeviceScale` | Gui/GUIWidget.cs | r from ~every widget ctor; w by RootSystemWindow at startup | **no** (write-once-ish by convention only) |
| `GuiWidget.DefaultEnforceIntegerBounds` | Gui/GUIWidget.cs | r from every GuiWidget field initializer | no (read-only flag) |
| `TextWidget.DoubleBufferDefault`, `Button/CheckBox/RadioButton.DefaultMargin`, `ButtonViewText/CheckBoxViewText.DefaultPadding`, `DefaultViewFactory.SelectBlue`, `DropDownList.whiteSemiTransparent/whiteTransparent` | various | r from ctors | no (config-style reads; freeze candidates) |
| **`ToolTipManager.CreateToolTip`** | Gui/SystemWindow/ToolTipManager.cs | w — cctor initializer; mutable Func swapped by app later | **no** |

### UiThread / window machinery
| Static | Declared in | Access | Locked? |
|--------|-------------|--------|---------|
| `UiThread.intervalActions` | Gui/UiThread.cs | w via SetInterval from ToolTipManager/ImageSequenceWidget/ResponsiveImageSequenceWidget ctors | yes (list add) — hazard is the escaping callback, not the list |
| **`UiThread.callLater` / `listA`** | Gui/UiThread.cs | rw — cctor; RunOnIdle from PerformancePanel ctor | **no** |
| **`SystemWindow._openWindows` / `AllOpenSystemWindows` / `systemWindowProvider`** | Gui/SystemWindow/SystemWindow.cs | rw — cctor; DiagnosticWidget ctor via ShowAsSystemWindow (lazy reflection provider init) | **no** |
| **`WinformsSystemWindow.idleCallBackTimer`** | PlatformWin32/win32/WinformsSystemWindow.cs | rw — ctor check-then-create-start | **no** |
| **`WinformsSystemWindow.MainWindowsFormsWindow`** | PlatformWin32/win32/WinformsSystemWindow.cs | rw — ctor first-window latch | **no** |
| `SystemWindow.EnableAllowDrop` | Gui/SystemWindow/SystemWindow.cs | r — WinformsSystemWindow/WinformsEventSink ctors | no |
| **`DebugLogger.debugFilters`** | agg/DebugLogger.cs | w — DiagnosticWidget ctor path | **no** (HashSet) |

### Static events subscribed from ctors (leak + affinity)
| Static event | Subscribed from |
|--------------|-----------------|
| `Keyboard.StateChanged` (Gui/Keyboard.cs) | OnScreenKeyboard ctor lambdas (never removed) |
| `TextEditWidget.ShowSoftwareKeyboard` / `HideSoftwareKeyboard` / `KeyboardCollapsed` | SoftKeyboardDisplayStateManager / SoftKeyboardContentOffset ctors (never removed) |

### Perf/debug & render caches (lock-guarded — verify only)
`PerformancePanel.panels`*, `PerformancePanel.resultsPanels`, `PerformanceTimer.lastPanelName`/`InPerformanceMeasuring`, `ReportTimer.timers`, `Graphics2DGpu.cachesByContext`/`cacheGeneration` (volatile), `ImageTexturePlugin.*`, `MeshTrianglePlugin/MeshWirePlugin/MeshNonManifoldPlugin.pluginsByMesh`, `OverhangRender.face0WorldZAngleByMesh`, `WorldViewExtensions.TesselatorsByWorld`. (*`panels` itself is unlocked but only reachable under PerformanceTimer's global lock — verified.)
**Exception:** `WorldViewExtensions.scaledLineMesh`/`unscaledLineMesh` are shared **mutable** Meshes mutated per-render-call with no lock (mutation is in render paths, not ctors — adjacent hazard worth a Phase-2 note).

### Blender saturation tables (unlocked, ctor-filled)
`BlenderPreMultBGRA.m_Saturate9BitToByte`, `BlenderPreMultBGR.m_Saturate9BitToByte`, `BlenderPolyColorPreMultBGRA.m_Saturate9BitToByte`, `blender_gray/blenderGrayFromRed/blenderGrayClampedMax.m_Saturate9BitToByte` — all **rw, no lock**, filled by instance ctors. Also `Subtract.lookupSubtractAndClamp` (ImageProcessing/Subtract.cs — lazy static-method init, same pattern, no ctor involved).

### Inert type-init statics (recorded for completeness; no action)
`Vector2/2Float/3/3Float/4.SizeInBytes`, `Quaternion.Identity`, `Vector3.PositiveInfinity/NegativeInfinity` (read from BvhTree ctor), `WorldView.OrthographicProjectionMinimumHeight`, `VertexTextureData/VertexColorData/VertexNormalData/VertexPositionData/WireVertexData.Stride` (Marshal.SizeOf at type init), `CGSVDefaultFont.gsv_default_font`, `CsgToRayTraceable.DefaultMaterial` (note: public non-readonly — freeze candidate), GuiAutomation cctor tables (`AggInputMethods.*`, `AutomationRunner.*`, `AutomationDialogProvider.multipleFileSeparators`), `WinformsSystemWindow`/`D3D11SystemWindow`/`WinformsEventSink` boolean/property initializers (`SingleWindowMode`, `EnableInputHook`, `ShowingSystemDialog`, `SingleInvokeLock`, `processingOnIdle`, `firstWindow`, `ExitAfterXSeconds`, `ScreenshotAtFrames` — the last is a mutable List, minor freeze candidate).

---

## Rejected / downgraded findings (adversarial verification results)

- **`ConsoleWidget`** (Gui/TextWidgets/ConsoleWidget.cs) — REJECTED as a spin-up hazard: zero instantiation sites in agg-sharp *or* MatterCAD; `Primary` throws if never constructed. Dead code with real latent defects.
- **`PerformancePanel`** — REJECTED: only construction path runs under `PerformanceTimer`'s global static lock; event subscriptions are RunOnIdle-marshaled to the UI thread. Claims of unlocked init were factually wrong.
- **`LiberationSansFont`/`Bold` torn publish via AggContext** — downgraded mechanism: production access goes through AggContext's cctor, which the CLR serializes; the tear needs a concurrent *direct* `Instance` caller (tests/examples/MatterCAD `ApplicationController.TypeFaceCache` via its own unlocked `ApplicationController.Instance` lazy). Fix priority unchanged.
- **`InternalTextEditWidget.DefaultRightClick`** — race branch is dead in MatterCAD (pre-seeded synchronously at Application.cs:498 before any text edit exists); live for other consumers.
- **`DropArrow` rebuild race** — cannot fire at spin-up (static ctor under type-init lock; DeviceScale already final); fires on later text-size changes.
- **`WinformsSystemWindow` idle timer "fires before message loop"** — refuted: handler early-returns until `enableIdleProcessing` after Show(), has IsHandleCreated + InvokeRequired guards. Static init race stands.
- **`UiThread.SetInterval` static mutation claims** (ToolTipManager, ImageSequenceWidget, etc.) — the interval list is lock-guarded; the real, confirmed hazard is callbacks/subscriptions escaping partially-constructed objects to the UI pump.

---

## Suggested Phase-2 batches (pending review)

1. **Font cluster** — LiberationSans/Bold torn publish, AggContext lazy fonts, StyledTypeFaceImageCache locking/eviction, TextWidget/TypeFacePrinter deferral. Targeted unit tests: concurrent first-touch of `Instance` from N threads asserting one parse + valid UnitsPerEm; the AnchorTests flakiness should disappear as a side effect (but per rules of engagement, don't treat that alone as proof).
2. **Blender saturation tables** — mechanical: static readonly precomputed tables (6 classes + Subtract). Trivially testable.
3. **Singleton hardening** — StaticData, AggContext._config/_osInformation/_fileDialogs, SystemWindow.systemWindowProvider, WinformsSystemWindow statics → `Lazy<T>`/CompareExchange latches.
4. **Ctor-escape fixes** — SystemWindow/ToolTipManager, ImageSequenceWidget/ResponsiveImageSequenceWidget, WinformsSystemWindow TitleBarHeight, ImageLinkAdvancedX fetch → OnLoad.
5. **Theme statics** — ThemeConfig→ScrollBar defaults, MarkdownWidget.Theme injection.
6. **Event-subscription hygiene** (Medium R3 family) — OnScreenKeyboard trio, ImageWidget/ResponsiveImageWidget, PopupWidget, WinformsEventSink unhook paths.
7. **Layout-in-ctor Low family** — mechanical move-to-OnLoad sweep, last, after the base `GuiWidget` design decision.
