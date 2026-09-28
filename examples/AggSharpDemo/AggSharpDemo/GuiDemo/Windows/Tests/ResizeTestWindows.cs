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

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using System;
using System.Collections.Generic;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.AggSharpDemo.GuiDemo.Windows.Widgets;
using MatterHackers.VectorMath;

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Tests
{
	/// <summary>
	/// The content of agg-gui's six "Window Resize Test" windows (demo-ui/src/windows/tests/resize.rs, after egui's
	/// window_resize_test.rs). Each window's resize behaviour - auto-size, window-level scroll, height held to the
	/// content - is a flag on its <see cref="DemoSpec"/> that <see cref="DemoWindowHost"/> applies; this builds
	/// only what is inside.
	/// </summary>
	/// <remarks>
	/// Deviation: the auto-sized window's Resize area does not hold a floor at its text's natural size (agg-sharp's
	/// <see cref="ResizeArea"/> clamps to its MinimumSize only).
	/// </remarks>
	public static class ResizeTestWindows
	{
		public const string LoremIpsum = "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor "
			+ "incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco "
			+ "laboris nisi ut aliquip ex ea commodo consequat.";

		public const string LoremIpsumLong = "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod "
			+ "tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation "
			+ "ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in "
			+ "voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non "
			+ "proident, sunt in culpa qui officia deserunt mollit anim id est laborum.\n\n"
			+ "Sed ut perspiciatis unde omnis iste natus error sit voluptatem accusantium doloremque laudantium, totam "
			+ "rem aperiam, eaque ipsa quae ab illo inventore veritatis et quasi architecto beatae vitae dicta sunt "
			+ "explicabo. Nemo enim ipsam voluptatem quia voluptas sit aspernatur aut odit aut fugit, sed quia "
			+ "consequuntur magni dolores eos qui ratione voluptatem sequi nesciunt.\n\n"
			+ "At vero eos et accusamus et iusto odio dignissimos ducimus qui blanditiis praesentium voluptatum "
			+ "deleniti atque corrupti quos dolores et quas molestias excepturi sint occaecati cupiditate non "
			+ "provident, similique sunt in culpa qui officia deserunt mollitia animi, id est laborum et dolorum fuga.";

		private const string SourceUrl = "https://github.com/MatterHackers/agg-sharp/blob/main/examples/AggSharpDemo/AggSharpDemo/GuiDemo/Windows/Tests/ResizeTestWindows.cs";

		/// <summary>Window 1: a fixed-width note, then a user-resizable area whose size the window follows.</summary>
		public static GuiWidget AutoSized(DemoTheme demoTheme)
		{
			var page = new Page(demoTheme, gap: 6);
			double s = GuiWidget.DeviceScale;

			// A fixed width, so the note wraps taller rather than setting the auto-sized window's width.
			var note = new GuiWidget { HAnchor = HAnchor.Left | HAnchor.Absolute, VAnchor = VAnchor.Fit, Width = 320 * s };
			note.AddChild(page.Kit.Wrapped("This window will auto-size based on its contents."));
			page.Add(note);
			page.Add(page.Kit.Label("Resize this area:", 14));

			var resizeArea = new ResizeArea(320 * s, 120 * s, page.Kit.Theme)
			{
				Name = "Resize Test Area",
				MinimumSize = new Vector2(120 * s, 60 * s),
				MaximumSize = new Vector2(4000 * s, 3000 * s),
			};
			WrappedTextWidget lorem = page.Kit.Wrapped(LoremIpsum, 11.5);
			lorem.VAnchor = VAnchor.Top | VAnchor.Fit;
			lorem.Margin = new BorderDouble(8);
			resizeArea.AddChild(lorem);
			page.Add(resizeArea);

			page.Add(page.Kit.Label("Resize the above area!", 14));
			page.AddSourceLink();
			return page.Finish();
		}

		/// <summary>Window 2: a long text; the host's window-level scroll gives it range.</summary>
		public static GuiWidget ResizableWithScroll(DemoTheme demoTheme)
		{
			var page = new Page(demoTheme);
			page.Add(page.Kit.Wrapped("This window is resizable and has a scroll area. You can shrink it to any size."));
			page.AddSeparator();
			page.Add(page.Kit.Wrapped(LoremIpsumLong, 11.5));
			page.AddSourceLink();
			return page.Finish();
		}

		/// <summary>Window 3: no window scroll, but a scroll area of its own that takes the height left over.</summary>
		public static GuiWidget ResizableWithEmbeddedScroll(DemoTheme demoTheme)
		{
			var page = new Page(demoTheme);
			page.Add(page.Kit.Wrapped("This window is resizable but has no built-in scroll area."));
			page.Add(page.Kit.Wrapped("However, we have a sub-region with a scroll bar:"));
			page.AddSeparator();

			var scroll = new ScrollableWidget(autoScroll: true)
			{
				Name = "Resize Test Embedded Scroll",
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
			};
			scroll.ScrollArea.HAnchor = HAnchor.Stretch;
			var inner = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
				Padding = new BorderDouble(4),
			};
			inner.AddChild(page.Kit.Wrapped(LoremIpsumLong + "\n\n" + LoremIpsumLong, 11.5));
			scroll.AddChild(inner);
			page.Add(scroll);

			page.AddSourceLink();
			return page.Finish();
		}

		/// <summary>Window 4: no scroll at all; the host holds the window to the content's height.</summary>
		public static GuiWidget ResizableWithoutScroll(DemoTheme demoTheme)
		{
			var page = new Page(demoTheme);
			page.Add(page.Kit.Wrapped("This window is resizable but has no scroll area. This means it can only be resized to "
				+ "a size where all the contents is visible."));
			page.Add(page.Kit.Wrapped("agg-sharp will not clip the contents of a window, nor add whitespace to it."));
			page.AddSeparator();
			page.Add(page.Kit.Wrapped(LoremIpsum, 11.5));
			page.AddSourceLink();
			return page.Finish();
		}

		/// <summary>Window 5: a multi-line editor that fills whatever the window leaves it, wrapping its text.</summary>
		public static GuiWidget ResizableWithTextEdit(DemoTheme demoTheme)
		{
			var page = new Page(demoTheme);
			page.Add(page.Kit.Wrapped("Shows how you can fill an area with a widget."));

			var editor = new CodeEditor(LoremIpsumLong, pointSize: page.Kit.FontSize(12.5))
			{
				Name = "Resize Test TextEdit",
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
				Border = 1,
				ShowLineNumbers = false,
				WordWrap = true,
				AggGuiEditing = true,
			};
			page.Add(editor);
			page.OnRecolor(palette =>
			{
				editor.BackgroundColor = palette.WidgetBackground;
				editor.BorderColor = palette.Separator;
				editor.TextColor = palette.TextColor;
				editor.CaretColor = palette.TextColor;
				editor.SelectionColor = page.Kit.Theme.PrimaryAccentColor.WithAlpha(90);
				editor.ScrollbarColor = palette.TextColor.WithAlpha(70);
				editor.ScrollbarDragColor = palette.TextColor.WithAlpha(130);
			});

			page.AddSourceLink();
			return page.Finish();
		}

		/// <summary>Window 6: a note over empty space that takes up whatever room the window has.</summary>
		public static GuiWidget FreelyResized(DemoTheme demoTheme)
		{
			var page = new Page(demoTheme);
			page.Add(page.Kit.Wrapped("This window has empty space that fills up the available space, preventing auto-shrink."));
			page.AddSourceLink();
			page.Add(new GuiWidget { HAnchor = HAnchor.Stretch, VAnchor = VAnchor.Stretch });
			return page.Finish();
		}

		/// <summary>
		/// One window's panel-coloured column (agg-gui's FlexColumn with padding 10 and a gap), recoloured with
		/// the theme until it is closed.
		/// </summary>
		private sealed class Page
		{
			private readonly double gap;
			private readonly List<GuiWidget> separators = new List<GuiWidget>();
			private readonly List<Action<DemoPalette>> recolors = new List<Action<DemoPalette>>();

			public Page(DemoTheme demoTheme, double gap = 8)
			{
				this.gap = gap * GuiWidget.DeviceScale;
				this.Kit = new MiscDemoKit(demoTheme);
				this.Column = new FlowLayoutWidget(FlowDirection.TopToBottom)
				{
					HAnchor = HAnchor.Stretch,
					VAnchor = VAnchor.Stretch,
					Padding = new BorderDouble(10),
				};

				void Recolor(object sender, EventArgs e) => this.Recolor(demoTheme.Palette);
				demoTheme.ThemeChanged += Recolor;
				this.Column.Closed += (sender, e) => demoTheme.ThemeChanged -= Recolor;
				this.demoTheme = demoTheme;
			}

			private readonly DemoTheme demoTheme;

			public MiscDemoKit Kit { get; }

			/// <summary>Colours everything added so far and hands back the column.</summary>
			public GuiWidget Finish()
			{
				this.Recolor(this.demoTheme.Palette);
				return this.Column;
			}

			private void Recolor(DemoPalette palette)
			{
				this.Column.BackgroundColor = palette.PanelFill;
				foreach (GuiWidget separator in this.separators)
				{
					separator.BackgroundColor = palette.Separator;
				}

				foreach (Action<DemoPalette> recolor in this.recolors)
				{
					recolor(palette);
				}

				this.Kit.Recolor();
			}

			public FlowLayoutWidget Column { get; }

			public void Add(GuiWidget child)
			{
				if (this.Column.Children.Count > 0)
				{
					child.Margin = child.Margin + new BorderDouble(top: this.gap);
				}

				this.Column.AddChild(child);
			}

			public void AddSeparator()
			{
				var separator = new GuiWidget { HAnchor = HAnchor.Stretch, Height = Math.Max(1, Math.Round(GuiWidget.DeviceScale)) };
				this.separators.Add(separator);
				this.Add(separator);
			}

			/// <summary>agg-gui's "(source code)" footer.</summary>
			public void AddSourceLink()
			{
				this.Add(new Hyperlink("(source code)", this.Kit.Theme, SourceUrl) { HAnchor = HAnchor.Center });
			}

			public void OnRecolor(Action<DemoPalette> recolor) => this.recolors.Add(recolor);
		}
	}
}
