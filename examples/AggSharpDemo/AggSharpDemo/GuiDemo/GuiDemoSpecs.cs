/*
Copyright (c) 2026, Lars Brubaker
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

using System;
using System.Collections.Generic;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;

namespace MatterHackers.AggSharpDemo.GuiDemo
{
	/// <summary>One window of the GUI demo: what it is called, where it is listed and how it first opens.</summary>
	public sealed class DemoSpec
	{
		public DemoSpec(string icon, string title, string group, bool openByDefault, double defaultWidth, double defaultHeight)
		{
			this.Icon = icon;
			this.Title = title;
			this.Group = group;
			this.OpenByDefault = openByDefault;
			this.DefaultWidth = defaultWidth;
			this.DefaultHeight = defaultHeight;
		}

		/// <summary>The Font Awesome 4 codepoint agg-gui puts in front of the title, or null for none. It is kept
		/// apart from the title because the text fonts have no Font Awesome glyphs; the window title, sidebar row
		/// and Demos menu item each draw it from <see cref="IconFont"/> beside the text.</summary>
		public string Icon { get; }

		/// <summary>The window title as agg-gui's specs.rs has it, without its icon prefix. Also the window's
		/// identity: factories (and later saved state) are keyed on it.</summary>
		public string Title { get; }

		/// <summary>The sidebar section it is listed under (one of <see cref="GuiDemoSpecs.Groups"/>).</summary>
		public string Group { get; }

		public bool OpenByDefault { get; }

		/// <summary>The size the window opens at, in design units (specs.rs's logical pixels); the host
		/// multiplies it by <see cref="GuiWidget.DeviceScale"/>.</summary>
		public double DefaultWidth { get; }

		public double DefaultHeight { get; }

		/// <summary>agg-gui's <c>auto_size</c>: the window is always exactly its content's natural size and the
		/// user cannot resize it (its content can still grow it, as the auto-sized test's Resize area does).
		/// The content is laid out with Fit anchors on both axes.</summary>
		public bool AutoSize { get; init; }

		/// <summary>agg-gui's <c>vscroll</c>: the host wraps the content in a vertical scroll area, so the window
		/// shrinks to any height. The content is laid out Stretch wide and Fit tall inside it.</summary>
		public bool VerticalScroll { get; init; }

		/// <summary>agg-gui's <c>tight_fit</c>: the window's height is held to its content's natural height -
		/// the resize floor and ceiling both - so it can neither clip the content nor add whitespace below it.
		/// The width stays free; narrowing it re-wraps the text and the window grows to match.</summary>
		public bool FitHeightToContent { get; init; }

		/// <summary>agg-gui's <c>resizable</c>: whether the user can drag the window's edges. True, the default;
		/// an <see cref="AutoSize"/>d window never is.</summary>
		public bool Resizable { get; init; } = true;

		/// <summary>The stable widget Name of this window's content.</summary>
		public string ContentName => this.Title + " Content";
	}

	/// <summary>
	/// The GUI demo's window list, mirroring agg-gui's demo-ui/src/specs.rs (DEMOS then TESTS, in that order),
	/// and the factory that builds each window's content.
	/// </summary>
	public static class GuiDemoSpecs
	{
		/// <summary>specs.rs's WIN_W: the width most windows open at.</summary>
		public const double DefaultWindowWidth = 360;

		/// <summary>specs.rs's WIN_H: the height most windows open at.</summary>
		public const double DefaultWindowHeight = 290;

		private const double W = DefaultWindowWidth;

		private const double H = DefaultWindowHeight;

		/// <summary>Sidebar sections in display order, as app_builder.rs lists them.</summary>
		public static IReadOnlyList<string> Groups { get; } = new[]
		{
			"Widgets", "Layout", "Graphics", "Interaction", "Tests", "Window Resize Test", "Tools",
		};

		/// <summary>Every window, in specs.rs order. System sits among the Graphics entries there but is filed
		/// under Tools.</summary>
		public static IReadOnlyList<DemoSpec> All { get; } = new[]
		{
			// Widgets
			new DemoSpec("", "Widget Gallery", "Widgets", true, W, H),
			new DemoSpec("", "Sliders", "Widgets", false, W, H),
			new DemoSpec("", "TextEdit", "Widgets", false, W, H),
			new DemoSpec("", "RichTextEdit", "Widgets", false, 640, 520),
			new DemoSpec("", "Mobile Keyboard", "Widgets", false, 420, 540) { FitHeightToContent = true, Resizable = false },
			new DemoSpec("", "Tooltips", "Widgets", false, W, H),
			new DemoSpec("", "Popups", "Widgets", false, W, H),
			new DemoSpec("", "Menus", "Widgets", false, 520, 320),
			new DemoSpec("", "Modals", "Widgets", false, W, H),
			new DemoSpec("", "Misc Demos", "Widgets", false, W, H),
			new DemoSpec("", "Code Editor", "Widgets", false, W, H),
			new DemoSpec("", "Code Example", "Widgets", true, W, H),
			new DemoSpec("", "Font Book", "Widgets", false, W, H),

			// Layout
			new DemoSpec("", "Frame", "Layout", false, W, H) { AutoSize = true },
			new DemoSpec("", "Panels", "Layout", false, W, H),
			new DemoSpec("", "Strip", "Layout", false, W, H),
			new DemoSpec("", "Table", "Layout", false, 720, 560),
			new DemoSpec("", "Scrolling", "Layout", false, 680, 540),
			new DemoSpec("", "Window Options", "Layout", false, W, H),
			new DemoSpec("", "Text Layout", "Layout", false, W, H),
			new DemoSpec("", "Interactive Container", "Layout", false, W, H),

			// Graphics
			new DemoSpec("", "Bézier Curve", "Graphics", false, W, H),
			new DemoSpec("", "Dancing Strings", "Graphics", false, W, H),
			new DemoSpec("", "Painting", "Graphics", false, W, H),
			new DemoSpec("", "Rendering Test", "Graphics", false, W, H),
			new DemoSpec("", "Lion", "Graphics", true, 520, 620),
			new DemoSpec("", "Screenshot", "Graphics", false, W, H),
			new DemoSpec("", "Screen Share", "Graphics", false, 380, 420),
			new DemoSpec("", "3D Animation", "Graphics", false, 300, 260),
			new DemoSpec("", "System", "Tools", false, 520, 640),

			// Interaction
			new DemoSpec("", "Drag and Drop", "Interaction", false, W, H),
			new DemoSpec("", "Multi Touch", "Interaction", false, W, H),
			new DemoSpec("", "Undo Redo", "Interaction", false, W, H),
			new DemoSpec("", "Scene", "Interaction", false, W, H),

			// Tests
			new DemoSpec("", "Clipboard Test", "Tests", false, W, H),
			new DemoSpec("", "Cursor Test", "Tests", false, 296, 560),
			new DemoSpec("", "Input Event History", "Tests", false, W, H),
			new DemoSpec("", "Input Test", "Tests", false, W, H),
			new DemoSpec("", "Flex Layout Test", "Tests", false, W, H),
			new DemoSpec("", "Manual Layout Test", "Tests", false, W, H),
			new DemoSpec("", "SVG Test", "Tests", false, 960, 620),

			// Window Resize Test: specs.rs titles these with a plain arrow rather than an icon.
			new DemoSpec(null, "↔ auto-sized", "Window Resize Test", false, 360, 240) { AutoSize = true },
			new DemoSpec(null, "↔ resizable + scroll", "Window Resize Test", false, 300, 290) { VerticalScroll = true },
			new DemoSpec(null, "↔ resizable + embedded scroll", "Window Resize Test", false, 300, 290),
			new DemoSpec(null, "↔ resizable without scroll", "Window Resize Test", false, 300, 290) { FitHeightToContent = true },
			new DemoSpec(null, "↔ resizable with TextEdit", "Window Resize Test", false, 300, 290),
			new DemoSpec(null, "↔ freely resized", "Window Resize Test", false, 250, 150),
		};

		/// <summary>The About window. Not in <see cref="All"/>: app_builder.rs adds it beside the specs.rs list and
		/// the sidebar gives it its own row above the groups, so it is in no group and no Demos submenu.</summary>
		public static DemoSpec About { get; } = new DemoSpec("", "About agg-sharp", "Tools", false, 360, 420);

		/// <summary>The Inspector window. Not in <see cref="All"/>: app_builder.rs adds it beside the specs.rs list as the
		/// one tool_entries row, which the sidebar shows in the Tools group; the backend panel's Inspector checkbox
		/// opens and closes the same window.</summary>
		public static DemoSpec Inspector { get; } = new DemoSpec("\uF188", "Inspector", "Tools", false, 320, 520);

		// Explicit rather than reflected, so the browser trimmer keeps every window and a missing entry is a
		// test failure instead of a silent gap. A window's line changes from ComingSoon to its real builder
		// when that window is built. Add syntax ({ key, builder }), not the indexer: a duplicate key - say a merge
		// that brings back a window's old ComingSoon line - then throws when the class loads instead of silently
		// replacing the real builder.
		private static readonly Dictionary<string, Func<DemoSpec, DemoTheme, GuiWidget>> Factories = new Dictionary<string, Func<DemoSpec, DemoTheme, GuiWidget>>
		{
			{ "About agg-sharp", (spec, demoTheme) => new Windows.Tools.AboutWindow(demoTheme) },
			{ "Inspector", (spec, demoTheme) => new Windows.Tools.InspectorWindow(demoTheme) },
			{ "Widget Gallery", (spec, demoTheme) => new Windows.Widgets.WidgetGalleryWindow(demoTheme) },
			{ "Sliders", (spec, demoTheme) => new Windows.Widgets.SlidersWindow(demoTheme) },
			{ "TextEdit", (spec, demoTheme) => new Windows.Widgets.TextEditWindow(demoTheme) },
			{ "RichTextEdit", (spec, demoTheme) => new Windows.Widgets.RichTextEditWindow(demoTheme) },
			{ "Mobile Keyboard", (spec, demoTheme) => new Windows.Widgets.MobileKeyboardWindow(demoTheme) },
			{ "Tooltips", (spec, demoTheme) => new Windows.Widgets.TooltipsWindow(demoTheme) },
			{ "Popups", (spec, demoTheme) => new Windows.Widgets.PopupsWindow(demoTheme) },
			{ "Menus", (spec, demoTheme) => new Windows.Widgets.MenusWindow(demoTheme) },
			{ "Modals", (spec, demoTheme) => new Windows.Widgets.ModalsWindow(demoTheme) },
			{ "Misc Demos", (spec, demoTheme) => new Windows.Widgets.MiscDemosWindow(demoTheme) },
			{ "Code Editor", (spec, demoTheme) => new Windows.Widgets.CodeEditorWindow(demoTheme) },
			{ "Code Example", (spec, demoTheme) => new Windows.Widgets.CodeExampleWindow(demoTheme) },
			{ "Font Book", (spec, demoTheme) => new Windows.Widgets.FontBookWindow(demoTheme) },
			{ "Frame", (spec, demoTheme) => new Windows.Layout.FrameWindow(demoTheme) },
			{ "Panels", (spec, demoTheme) => new Windows.Layout.PanelsWindow(demoTheme) },
			{ "Strip", (spec, demoTheme) => new Windows.Layout.StripWindow(demoTheme) },
			{ "Table", (spec, demoTheme) => new Windows.Layout.TableWindow(demoTheme) },
			{ "Scrolling", (spec, demoTheme) => new Windows.Layout.Scrolling.ScrollingWindow(demoTheme) },
			{ "Window Options", (spec, demoTheme) => new Windows.Layout.WindowOptionsWindow(demoTheme) },
			{ "Text Layout", (spec, demoTheme) => new Windows.Layout.TextLayoutWindow(demoTheme) },
			{ "Interactive Container", (spec, demoTheme) => new Windows.Layout.InteractiveContainerWindow(demoTheme) },
			{ "Bézier Curve", (spec, demoTheme) => new Windows.Graphics.BezierWindow(demoTheme) },
			{ "Dancing Strings", (spec, demoTheme) => new Windows.Graphics.DancingStringsWindow(demoTheme) },
			{ "Painting", (spec, demoTheme) => new Windows.Graphics.PaintingWindow(demoTheme) },
			{ "Rendering Test", (spec, demoTheme) => new Windows.Graphics.RenderingTestWindow(demoTheme) },
			{ "Lion", (spec, demoTheme) => new Windows.Graphics.LionWindow(demoTheme) },
			{ "Screenshot", (spec, demoTheme) => new Windows.Graphics.ScreenshotWindow(demoTheme) },
			{ "Screen Share", ComingSoon },
			{ "3D Animation", (spec, demoTheme) => new Windows.Graphics.ThreeDAnimationWindow(demoTheme) },
			{ "System", (spec, demoTheme) => new Windows.Tools.SystemTypographyWindow(demoTheme) },
			{ "Drag and Drop", (spec, demoTheme) => new Windows.Interaction.DragAndDropWindow(demoTheme) },
			{ "Multi Touch", (spec, demoTheme) => new Windows.Interaction.MultiTouchWindow(demoTheme) },
			{ "Undo Redo", (spec, demoTheme) => new Windows.Interaction.UndoRedoWindow(demoTheme) },
			{ "Scene", (spec, demoTheme) => new Windows.Interaction.SceneWindow(demoTheme) },
			{ "Clipboard Test", (spec, demoTheme) => new Windows.Tests.ClipboardTestWindow(demoTheme) },
			{ "Cursor Test", (spec, demoTheme) => new Windows.Tests.CursorTestWindow(demoTheme) },
			{ "Input Event History", (spec, demoTheme) => new Windows.Tests.InputEventHistoryWindow(demoTheme) },
			{ "Input Test", (spec, demoTheme) => new Windows.Tests.InputTestWindow(demoTheme) },
			{ "Flex Layout Test", (spec, demoTheme) => new Windows.Tests.FlexLayoutTestWindow(demoTheme) },
			{ "Manual Layout Test", (spec, demoTheme) => new Windows.Tests.ManualLayoutTestWindow(demoTheme) },
			{ "SVG Test", (spec, demoTheme) => new Windows.Tests.SvgTestWindow(demoTheme) },
			{ "↔ auto-sized", (spec, demoTheme) => Windows.Tests.ResizeTestWindows.AutoSized(demoTheme) },
			{ "↔ resizable + scroll", (spec, demoTheme) => Windows.Tests.ResizeTestWindows.ResizableWithScroll(demoTheme) },
			{ "↔ resizable + embedded scroll", (spec, demoTheme) => Windows.Tests.ResizeTestWindows.ResizableWithEmbeddedScroll(demoTheme) },
			{ "↔ resizable without scroll", (spec, demoTheme) => Windows.Tests.ResizeTestWindows.ResizableWithoutScroll(demoTheme) },
			{ "↔ resizable with TextEdit", (spec, demoTheme) => Windows.Tests.ResizeTestWindows.ResizableWithTextEdit(demoTheme) },
			{ "↔ freely resized", (spec, demoTheme) => Windows.Tests.ResizeTestWindows.FreelyResized(demoTheme) },
		};

		/// <summary>Builds the content of <paramref name="spec"/>'s window, named <see cref="DemoSpec.ContentName"/>,
		/// coloured by <paramref name="demoTheme"/> and following its changes until the content is closed.</summary>
		/// <param name="demoTheme">Null for a theme of its own (agg-gui's default).</param>
		public static GuiWidget CreateContent(DemoSpec spec, DemoTheme demoTheme = null)
		{
			if (!Factories.TryGetValue(spec.Title, out Func<DemoSpec, DemoTheme, GuiWidget> factory))
			{
				throw new ArgumentException($"No factory for GUI demo window '{spec.Title}'.", nameof(spec));
			}

			GuiWidget content = factory(spec, demoTheme ?? new DemoTheme());
			content.Name = spec.ContentName;
			return content;
		}

		/// <summary>The stand-in content of a window that has not been built yet.</summary>
		private static GuiWidget ComingSoon(DemoSpec spec, DemoTheme demoTheme)
		{
			var text = new TextWidget("Coming soon: " + spec.Title, pointSize: 11, textColor: demoTheme.Palette.TextColor)
			{
				HAnchor = HAnchor.Center,
				VAnchor = VAnchor.Center,
			};

			// The theme outlives the window, so the handler goes when the text is closed.
			void Recolor(object sender, EventArgs e) => text.TextColor = demoTheme.Palette.TextColor;
			demoTheme.ThemeChanged += Recolor;
			text.Closed += (s, e) => demoTheme.ThemeChanged -= Recolor;
			return text;
		}
	}
}
