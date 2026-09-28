# GUI Demo plan (Phase 2, open work)

The "GUI Demo" section of `examples/AggSharpDemo` reaches parity with agg-gui's demo app
(`~/Development/rust-apps/agg-gui`: `demo-ui/src/specs.rs` window list, `demo-ui/src/windows/*`,
shell in `demo-ui/src/{top_bar,sidebar,backend_panel,state}.rs`, widgets in `agg-gui/src/widgets/*`).
Every specs.rs window has a line in `GuiDemo/GuiDemoSpecs.cs`; the ones still `ComingSoon` there are open.

Rules: every window/widget/shell part in its own file under 800 lines; never grow frozen files
(`GUIWidget.cs`, `PopupMenu.cs`, `InternalTextEditWidget.cs`) - compose or extend from outside; no
partial classes; public API only extended additively with defaults unchanged. Stable `Name` on every
interactive widget; automation tests use event waits only. Only open windows are materialised. No MSAA;
3D uses our own SSAA offscreen target. Per window: offscreen build asserts named children + one
interaction unit test.

## Open work

- Screen Share: deferred (its GuiDemoSpecs line is still ComingSoon).
- SVG Test: the SVG module passes 11 of 58 samples, matching agg-gui's own KNOWN_INCOMPLETE.

## Deferred

Deferred from TextEdit (`Windows/Widgets/TextEditWindow.cs`, `Gui/TextWidgets/AlignedTextEditWidget.cs`): per-line
horizontal alignment inside a multi-line block (the block is aligned as a whole), word wrap, and treating an emoji
(a surrogate pair) as one character - the caret, arrow keys and backspace still step one UTF-16 char at a time, so
they can stop inside a pair or delete half of it. All live in the caret/selection maths of the frozen
InternalTextEditWidget, so they need a new editor core, not a wrapper.

Deferred from Misc Demos (`Windows/Widgets/MiscDemosWindow.cs` + `MiscDemo*.cs`, `MiscTreeSection.cs`, `Gui/ResizeArea/`):
egui's four premul/unmul colour variants are one ColorPicker (as in agg-gui).

Deferred from Frame (`Windows/Layout/FrameWindow.cs` + `FramePreview.cs`, `FourValueField.cs`, `FrameState.cs`): agg-gui's
window auto-sizes to its content; here it opens at specs.rs's 360 x 290 and scrolls (its spec does not set `AutoSize`).
The Fill picker lacks "No Color (Pass Through)" (ColorPicker has no allow_none), and the stroke picker sits under the
width rather than beside it.

Deferred from Bézier Curve (`Windows/Graphics/BezierWindow.cs` + `BezierCanvas.cs`): the control points start at
bezier.rs's fixed pixel positions, so at the 360 x 290 default the top handles sit above the (shorter) canvas until
the window is enlarged - as in agg-gui.

Deferred from Table (`Windows/Layout/TableWindow.cs`, `Gui/VirtualTable/`): VirtualTable draws its own plain
scrollbars - agg-gui's ScrollView fade/auto-hide styling (and its fade colour) is not ported.

Deferred from Scrolling (`Windows/Layout/Scrolling/`): the Align segmented control is wider than the window at 680.

Deferred from Cursor Test (`Windows/Tests/CursorTestWindow.cs`): it lists agg-sharp's 27 `Cursors`, not egui's 35
CursorIcons - None, ContextMenu, Progress, Cell, VerticalText, Alias, Copy, NoDrop, Grab/Grabbing, ZoomIn/ZoomOut and the
per-edge resizes have no agg-sharp cursor, and the enum lives in the frozen GUIWidget.cs.

Deferred from System (`Windows/Tools/SystemTypographyWindow.cs` + `SystemFontTab.cs`, `SystemSampleTextTab.cs`): point
size, Width, Interval, Faux Weight and Faux Italic are not ported - agg-sharp's text path has no process-wide setting
for them (agg-gui's are staged, not rendered). The font list is the two embedded Liberation Sans faces, a new default
font reaches only widgets built after the change (TextWidget takes its face when built), the settings are not
persisted in DemoState, and the toggle switches are check boxes. The tabs' column is inset with a margin, which the
scroll area honours only on the right, so the content sits flush left; a vertical FlowLayoutWidget now pads every
child whatever order Padding and HAnchor were set in, so the column can go back to padding.

## Risks

GPU memory with many backbuffered windows (cap and show MB); frame budget with continuous mode + the 3D
Animation window (selectable SSAA factor).
