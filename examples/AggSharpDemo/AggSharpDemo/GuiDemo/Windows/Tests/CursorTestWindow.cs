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
using MatterHackers.Agg.VertexSource;
using MatterHackers.AggSharpDemo.GuiDemo.Windows.Widgets;

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Tests
{
	/// <summary>
	/// agg-gui's "Cursor Test" window (demo-ui windows/tests/basic/controls.rs cursor_test): every cursor icon
	/// as a row that switches the OS pointer while hovered, split into two columns so the window stays compact.
	/// </summary>
	public class CursorTestWindow : FlowLayoutWidget
	{
		/// <summary>
		/// agg-gui's ALL_CURSORS, in its order and under its names, with the agg cursor each one is. Where agg
		/// already had the shape under an older name (Hand, IBeam, SizeAll, ...) that member stands in.
		/// </summary>
		public static IReadOnlyList<(string Name, Cursors Cursor)> AllCursors { get; } = new[]
		{
			("Default", Cursors.Default),
			("None", Cursors.None),
			("ContextMenu", Cursors.ContextMenu),
			("Help", Cursors.Help),
			("PointingHand", Cursors.Hand),
			("Progress", Cursors.Progress),
			("Wait", Cursors.WaitCursor),
			("Cell", Cursors.Cell),
			("Crosshair", Cursors.Cross),
			("Text", Cursors.IBeam),
			("VerticalText", Cursors.VerticalText),
			("Alias", Cursors.Alias),
			("Copy", Cursors.Copy),
			("Move", Cursors.SizeAll),
			("NoDrop", Cursors.NoDrop),
			("NotAllowed", Cursors.No),
			("Grab", Cursors.Grab),
			("Grabbing", Cursors.Grabbing),
			("AllScroll", Cursors.NoMove2D),
			("ResizeHorizontal", Cursors.SizeWE),
			("ResizeNeSw", Cursors.SizeNESW),
			("ResizeNwSe", Cursors.SizeNWSE),
			("ResizeVertical", Cursors.SizeNS),
			("ResizeEast", Cursors.ResizeEast),
			("ResizeSouthEast", Cursors.ResizeSouthEast),
			("ResizeSouth", Cursors.ResizeSouth),
			("ResizeSouthWest", Cursors.ResizeSouthWest),
			("ResizeWest", Cursors.ResizeWest),
			("ResizeNorthWest", Cursors.ResizeNorthWest),
			("ResizeNorth", Cursors.ResizeNorth),
			("ResizeNorthEast", Cursors.ResizeNorthEast),
			("ResizeColumn", Cursors.VSplit),
			("ResizeRow", Cursors.HSplit),
			("ZoomIn", Cursors.ZoomIn),
			("ZoomOut", Cursors.ZoomOut),
		};

		public CursorTestWindow(DemoTheme demoTheme)
			: base(FlowDirection.TopToBottom)
		{
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Stretch;
			this.Padding = new BorderDouble(8);
			this.BackgroundColor = demoTheme.Palette.PanelFill;

			var kit = new MiscDemoKit(demoTheme);
			TextWidget heading = kit.Label("Hover to switch cursor icon:", 13);
			heading.Margin = new BorderDouble(bottom: 4);
			this.AddChild(heading);

			var columns = new FlowLayoutWidget
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
			};
			this.AddChild(columns);

			int half = AllCursors.Count / 2;
			var rows = new List<CursorRow>();
			for (int column = 0; column < 2; column++)
			{
				var stack = new FlowLayoutWidget(FlowDirection.TopToBottom)
				{
					HAnchor = HAnchor.Stretch,
					VAnchor = VAnchor.Fit,
					Margin = new BorderDouble(left: column == 0 ? 0 : 2, right: column == 0 ? 2 : 0),
				};
				columns.AddChild(stack);

				int first = column == 0 ? 0 : half;
				int end = column == 0 ? half : AllCursors.Count;
				for (int i = first; i < end; i++)
				{
					var row = new CursorRow(AllCursors[i].Name, AllCursors[i].Cursor, kit, demoTheme);
					rows.Add(row);
					stack.AddChild(row);
				}
			}

			this.Rows = rows;

			void Recolor(object sender, EventArgs e)
			{
				this.BackgroundColor = demoTheme.Palette.PanelFill;
				kit.Recolor();
				this.Invalidate();
			}

			demoTheme.ThemeChanged += Recolor;
			this.Closed += (s, e) => demoTheme.ThemeChanged -= Recolor;
		}

		/// <summary>The rows, in <see cref="AllCursors"/> order.</summary>
		public IReadOnlyList<GuiWidget> Rows { get; }

		/// <summary>One full-width row whose own <see cref="GuiWidget.Cursor"/> is the cursor it demonstrates.</summary>
		private sealed class CursorRow : GuiWidget
		{
			private const double RowHeight = 24;

			private readonly DemoTheme demoTheme;
			private readonly ThemeConfig theme;

			public CursorRow(string name, Cursors cursor, MiscDemoKit kit, DemoTheme demoTheme)
			{
				this.demoTheme = demoTheme;
				this.theme = kit.Theme;
				this.Name = "Cursor Test " + name;
				this.Cursor = cursor;
				this.HAnchor = HAnchor.Stretch;
				this.Height = RowHeight * DeviceScale;
				this.Margin = new BorderDouble(bottom: 2);

				TextWidget label = kit.Label(name, 12);
				label.HAnchor = HAnchor.Center;
				label.VAnchor = VAnchor.Center;
				label.Selectable = false;
				this.AddChild(label);

				this.MouseEnterBounds += (s, e) => this.Invalidate();
				this.MouseLeaveBounds += (s, e) => this.Invalidate();
			}

			public override void OnDraw(Graphics2D graphics2D)
			{
				graphics2D.Render(new RoundedRect(this.LocalBounds, 3 * DeviceScale), this.demoTheme.Palette.WidgetBackground);
				if (this.UnderMouseState != UnderMouseState.NotUnderMouse)
				{
					graphics2D.Render(new RoundedRect(this.LocalBounds, 3 * DeviceScale), this.theme.SlightShade);
				}

				base.OnDraw(graphics2D);
			}
		}
	}
}
