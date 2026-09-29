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
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.AggSharpDemo.GuiDemo
{
	/// <summary>
	/// The parts of agg-gui's window chrome that <see cref="WindowWidget"/> does not draw itself: a dark, bold
	/// close X, a resize grip in the bottom-right corner, and a title bar without WindowWidget's accent rule.
	/// </summary>
	internal static class DemoWindowChrome
	{
		/// <summary>Adds the close button and resize grip to <paramref name="window"/>, whose title bar was
		/// added without a close action so the library's light close glyph is not built.</summary>
		public static void Attach(WindowWidget window, DemoTheme demoTheme, Action close)
		{
			window.AddTitleBarButton(new CloseButton(demoTheme, close) { Name = window.Name + " Close" });
			window.AddChild(new ResizeGrip(window, demoTheme)
			{
				HAnchor = HAnchor.Right,
				VAnchor = VAnchor.Bottom,

				// Inside the grab border, and in from the rounded corner, where agg-gui draws it.
				Margin = new BorderDouble(0, DemoWindowHost.GrabBorder + 2, DemoWindowHost.GrabBorder + 2, 0),
			});
		}

		/// <summary>
		/// Colours the rule WindowWidget draws under its title bar. It is built in the accent colour; agg-gui has
		/// only a faint separator there.
		/// </summary>
		public static void ColorTitleRule(WindowWidget window, Color color)
		{
			// The rule is the visible panel's only HorizontalLine child; the panel is the window's first child.
			foreach (HorizontalLine rule in window.Children.First().Children.OfType<HorizontalLine>())
			{
				rule.BackgroundColor = color;
			}
		}

		/// <summary>agg-gui's window close button: a bold X in the text colour, on a soft fill when hovered.</summary>
		private sealed class CloseButton : GuiWidget
		{
			private const double SizeUnits = 16;

			private readonly DemoTheme demoTheme;

			public CloseButton(DemoTheme demoTheme, Action close)
				: base(SizeUnits * DeviceScale, SizeUnits * DeviceScale)
			{
				this.demoTheme = demoTheme;
				this.VAnchor = VAnchor.Center;
				this.Margin = new BorderDouble(2, 0, 5, 0);
				this.Cursor = Cursors.Hand;

				// Automation and the tests find the button by this tooltip, as they did the library's.
				this.ToolTipText = "Close";
				this.Click += (s, e) => close();
			}

			public override void OnMouseEnterBounds(MouseEventArgs mouseEvent)
			{
				this.Invalidate();
				base.OnMouseEnterBounds(mouseEvent);
			}

			public override void OnMouseLeaveBounds(MouseEventArgs mouseEvent)
			{
				this.Invalidate();
				base.OnMouseLeaveBounds(mouseEvent);
			}

			public override void OnDraw(Graphics2D graphics2D)
			{
				DemoPalette palette = this.demoTheme.Palette;
				if (this.FirstWidgetUnderMouse)
				{
					graphics2D.Render(new RoundedRect(this.LocalBounds, 3 * DeviceScale), palette.TextColor.WithAlpha(40));
				}

				double half = 3.5 * DeviceScale;
				double cx = this.Width / 2;
				double cy = this.Height / 2;
				double stroke = 1.8 * DeviceScale;
				graphics2D.Line(cx - half, cy - half, cx + half, cy + half, palette.TextColor, stroke);
				graphics2D.Line(cx - half, cy + half, cx + half, cy - half, palette.TextColor, stroke);
				base.OnDraw(graphics2D);
			}
		}

		/// <summary>
		/// agg-gui's resize grip: three diagonal strokes in the corner. It only draws; the window's own corner
		/// grab control still does the resizing, so the grip takes no mouse input.
		/// </summary>
		private sealed class ResizeGrip : GuiWidget
		{
			private const double SizeUnits = 10;

			private readonly WindowWidget window;

			private readonly DemoTheme demoTheme;

			public ResizeGrip(WindowWidget window, DemoTheme demoTheme)
				: base(SizeUnits * DeviceScale, SizeUnits * DeviceScale)
			{
				this.window = window;
				this.demoTheme = demoTheme;
				this.Selectable = false;
			}

			public override void OnDraw(Graphics2D graphics2D)
			{
				// No grip where the window cannot be resized from its corner.
				if (this.window.Resizable
					&& !this.window.Collapsed
					&& !this.window.Maximized
					&& !this.window.AutoSize)
				{
					Color color = this.demoTheme.Palette.TextDim.WithAlpha(150);
					double w = this.Width;
					double stroke = 1 * DeviceScale;
					for (int i = 1; i <= 3; i++)
					{
						double d = w * i / 3;
						graphics2D.Line(w - d, 0, w, d, color, stroke);
					}
				}

				base.OnDraw(graphics2D);
			}
		}
	}
}
