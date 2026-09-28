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
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.AggSharpDemo.GuiDemo.Windows.Widgets;
using MatterHackers.VectorMath;

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Layout
{
	/// <summary>
	/// The "Table" window, a port of agg-gui's table_demo (demo-ui/src/windows/text_demos/table_demo.rs, egui's
	/// TableDemo): option checkboxes, a table-type radio group, the row-count and scroll-to-row sliders, Reset,
	/// and a <see cref="VirtualTable"/> of five columns whose "Row" header reverses the order, whose "Interaction"
	/// column toggles one shared checkbox and whose other cells toggle their row's selection. Controls are named
	/// "Table &lt;what&gt;"; the table is "Table Grid".
	/// </summary>
	public class TableWindow : GuiWidget
	{
		public const string SourceUrl = "https://github.com/larsbrubaker/agg-gui/blob/main/demo-ui/src/windows/text_demos/table_demo.rs";

		public const int ManualRowCount = 20;

		public static readonly string[] TableTypes = { "Few, manual rows", "Thousands of rows of same height", "Thousands of rows of differing heights" };

		public static readonly string[] HeaderLabels = { "Row", "Clipped text", "Expanding content", "Interaction", "Content" };

		private const double TextHeight = 18;
		private const double ThinRow = 18;
		private const double ThickRow = 30;
		private const double CellPadX = 6;

		private readonly DemoTheme demoTheme;
		private readonly MiscDemoKit kit;
		private readonly RadioButton[] typeRadios = new RadioButton[3];
		private readonly GuiWidget numRowsRow;
		private readonly TextWidget numRowsValue;
		private readonly TextWidget scrollToValue;
		private readonly GuiWidget rule;
		private int tableType;
		private int numRows = 10_000;

		public TableWindow(DemoTheme demoTheme)
		{
			this.demoTheme = demoTheme;
			this.kit = new MiscDemoKit(demoTheme);
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Stretch;

			var column = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
				Padding = new BorderDouble(10),
			};
			this.AddChild(column);

			this.Grid = new VirtualTable(new[]
			{
				TableColumn.Auto(56, resizable: true),
				TableColumn.Remainder(atLeast: 40, clip: true, resizable: true),
				TableColumn.Auto(72, resizable: true),
				TableColumn.Remainder(resizable: true),
				TableColumn.Remainder(resizable: true),
			})
			{
				Name = "Table Grid",
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
				Striped = true,
				ClickableRows = true,
				Margin = new BorderDouble(0, 4, 0, 8),
				IsRowSelected = row => this.Selection.Contains(this.DisplayIndex(row)),
				IsRowOverlined = row => this.Overline && this.DisplayIndex(row) % 7 == 3,
				CellPainter = this.PaintCell,
				HeaderPainter = this.PaintHeader,
			};
			this.Grid.RowClicked += this.OnRowClicked;
			this.Grid.HeaderClicked += col =>
			{
				if (col == 0)
				{
					this.Reversed = !this.Reversed;
					this.Grid.Invalidate();
				}
			};

			// Options.
			FlowLayoutWidget options = this.kit.Row(12);
			options.AddChild(this.OptionBox("Table Striped", "Striped", true, on => this.Grid.Striped = on));
			options.AddChild(this.OptionBox("Table Overline", "Overline some rows", true, on => this.Overline = on));
			options.AddChild(this.OptionBox("Table Resizable", "Resizable columns", true, on => this.Grid.ResizableColumns = on));
			options.AddChild(this.OptionBox("Table Clickable", "Clickable rows", true, on => this.Grid.ClickableRows = on));
			column.AddChild(options);

			column.AddChild(this.kit.Label("Table type:"));
			for (int i = 0; i < TableTypes.Length; i++)
			{
				int index = i;
				this.typeRadios[i] = this.kit.Radio($"Table Type {i}", TableTypes[i]);
				this.typeRadios[i].Checked = i == 0;
				this.typeRadios[i].CheckedStateChanged += (s, e) =>
				{
					if (this.typeRadios[index].Checked)
					{
						this.SetTableType(index);
					}
				};
				column.AddChild(this.typeRadios[i]);
			}

			Slider numRowsSlider = this.kit.Slider("Table Num Rows", this.numRows, 0, 100_000, 1);
			this.numRowsValue = this.kit.Label(this.numRows.ToString());
			numRowsSlider.ValueChanged += (s, e) =>
			{
				this.numRows = (int)Math.Round(numRowsSlider.Value);
				this.numRowsValue.Text = this.numRows.ToString();
				this.UpdateRows();
			};
			this.numRowsRow = this.LabelRow("Num rows", numRowsSlider, this.numRowsValue);
			this.numRowsRow.Visible = false;
			column.AddChild(this.numRowsRow);

			Slider scrollTo = this.kit.Slider("Table Scroll To Row", 0, 0, 100_000, 1);
			this.scrollToValue = this.kit.Label("0");
			scrollTo.ValueChanged += (s, e) =>
			{
				int row = (int)Math.Round(scrollTo.Value);
				this.scrollToValue.Text = row.ToString();
				this.Grid.ScrollToRow(row);
			};
			column.AddChild(this.LabelRow("Row to scroll to", scrollTo, this.scrollToValue));

			ThemedTextButton reset = this.kit.Button("Table Reset", "Reset");
			reset.HAnchor = HAnchor.Left;
			reset.Click += (s, e) => UiThread.RunOnIdle(this.Reset);
			column.AddChild(reset);

			this.rule = new GuiWidget(1, 1 * DeviceScale) { HAnchor = HAnchor.Stretch, VAnchor = VAnchor.Absolute, Margin = new BorderDouble(0, 3) };
			column.AddChild(this.rule);
			column.AddChild(this.Grid);
			column.AddChild(new Hyperlink("(source code)", this.kit.Theme, SourceUrl)
			{
				Name = "Table Source Link",
				HAnchor = HAnchor.Left,
			});

			this.UpdateRows();
			this.Recolor();
			demoTheme.ThemeChanged += this.OnThemeChanged;
		}

		public VirtualTable Grid { get; }

		/// <summary>Selected rows by displayed index, so a selection follows its row when the order is reversed.</summary>
		public HashSet<int> Selection { get; } = new HashSet<int>();

		/// <summary>The one checkbox every "Click me" cell shows.</summary>
		public bool Checked { get; private set; }

		public bool Reversed { get; private set; }

		public bool Overline { get; private set; } = true;

		private int RowCount => this.tableType == 0 ? ManualRowCount : this.numRows;

		private static bool IsThick(int row) => row % 6 == 0;

		public override void OnClosed(EventArgs e)
		{
			// The theme outlives the window.
			this.demoTheme.ThemeChanged -= this.OnThemeChanged;
			base.OnClosed(e);
		}

		/// <summary>The row a table slot shows: slot n from the end when the order is reversed.</summary>
		public int DisplayIndex(int slot) => this.Reversed && this.RowCount > 0 ? Math.Max(0, this.RowCount - 1 - slot) : slot;

		private void SetTableType(int index)
		{
			this.tableType = index;
			this.numRowsRow.Visible = index != 0;
			this.UpdateRows();
		}

		private void UpdateRows()
		{
			IEnumerable<double> Heights(int count) => Enumerable.Range(0, count).Select(i => IsThick(i) ? ThickRow : ThinRow);
			this.Grid.Rows = this.tableType switch
			{
				0 => TableRows.Heterogeneous(Heights(ManualRowCount)),
				1 => TableRows.Homogeneous(this.numRows, TextHeight),
				_ => TableRows.Heterogeneous(Heights(this.numRows)),
			};
		}

		private void Reset()
		{
			this.Selection.Clear();
			this.Checked = false;
			this.Reversed = false;
			this.Grid.ResetColumnWidths();
		}

		private void OnRowClicked(int row, int column)
		{
			// The Interaction column hosts the shared checkbox; everywhere else toggles the row's selection.
			if (column == 3)
			{
				this.Checked = !this.Checked;
			}
			else
			{
				int index = this.DisplayIndex(row);
				if (!this.Selection.Remove(index))
				{
					this.Selection.Add(index);
				}
			}

			this.Grid.Invalidate();
		}

		private CheckBox OptionBox(string name, string label, bool isChecked, Action<bool> apply)
		{
			CheckBox box = this.kit.CheckBox(name, label, isChecked);
			box.CheckedStateChanged += (s, e) =>
			{
				apply(box.Checked);
				this.Grid.Invalidate();
			};
			return box;
		}

		/// <summary>A 110-wide label, the slider and its value.</summary>
		private GuiWidget LabelRow(string label, Slider slider, TextWidget value)
		{
			FlowLayoutWidget row = this.kit.Row(8);
			TextWidget text = this.kit.Label(label);
			text.AutoExpandBoundsToText = false;
			text.Width = 110 * DeviceScale;
			row.AddChild(text);
			row.AddChild(slider);
			row.AddChild(value);
			return row;
		}

		private double Points(double aggGuiSize) => this.kit.FontSize(aggGuiSize) * DeviceScale;

		private void DrawText(Graphics2D graphics2D, string text, double x, RectangleDouble cell, double size, Color color)
		{
			// Centred on the row the way agg-gui places its baseline at (height - size) / 2.
			double baseline = cell.Bottom + (cell.Height - size * 0.72) / 2;
			graphics2D.DrawString(text, x, baseline, size, color: color);
		}

		private void PaintCell(Graphics2D graphics2D, TableCellInfo cell)
		{
			double s = DeviceScale;
			DemoPalette palette = this.demoTheme.Palette;
			Color text = palette.TextColor;
			double size = this.Points(12);
			RectangleDouble r = cell.Bounds;
			int index = this.DisplayIndex(cell.Row);
			switch (cell.Column)
			{
				case 0:
					this.DrawText(graphics2D, index.ToString(), r.Left + CellPadX * s, r, size, text);
					break;

				case 1:
					string longText = $"Row {index} has some long text that you may want to clip, or it will take up too much horizontal space!";
					this.DrawText(graphics2D, VirtualTable.ClipTextToWidth(longText, size, Math.Max(0, r.Width - 2 * CellPadX * s)), r.Left + CellPadX * s, r, size, text);
					break;

				case 2:
					double middle = Math.Round(r.Center.Y);
					graphics2D.FillRectangle(r.Left + 4 * s, middle - s / 2, r.Right - 4 * s, middle + s / 2, palette.Separator);
					break;

				case 3:
					double box = 12 * s;
					double bx = r.Left + CellPadX * s;
					double by = r.Center.Y - box / 2;
					var outline = new RoundedRect(bx, by, bx + box, by + box, 2 * s);
					graphics2D.Render(outline, this.Checked ? this.kit.Theme.PrimaryAccentColor : palette.WidgetBackground);
					graphics2D.Render(new Stroke(outline, s), palette.WidgetStroke);
					if (this.Checked)
					{
						var check = new VertexStorage();
						check.MoveTo(bx + 2 * s, by + box / 2);
						check.LineTo(bx + box * .4, by + 2 * s);
						check.LineTo(bx + box - 2 * s, by + box - 2 * s);
						graphics2D.Render(new Stroke(check, 1.5 * s), Color.White);
					}

					double labelX = bx + box + 4 * s;
					this.DrawText(graphics2D, VirtualTable.ClipTextToWidth("Click me", size, Math.Max(0, r.Right - labelX - CellPadX * s)), labelX, r, size, text);
					break;

				case 4:
					bool thick = this.tableType != 1 && IsThick(index);
					this.DrawText(graphics2D, thick ? "Extra thick row" : "Normal row", r.Left + CellPadX * s, r, thick ? this.Points(14) : size, text);
					break;
			}
		}

		private void PaintHeader(Graphics2D graphics2D, TableCellInfo cell)
		{
			double s = DeviceScale;
			DemoPalette palette = this.demoTheme.Palette;
			RectangleDouble r = cell.Bounds;
			double size = this.Points(12.5);
			string label = cell.Column < HeaderLabels.Length ? HeaderLabels[cell.Column] : string.Empty;
			if (cell.Column != 0)
			{
				this.DrawText(graphics2D, VirtualTable.ClipTextToWidth(label, size, Math.Max(0, r.Width - 2 * CellPadX * s)), r.Left + CellPadX * s, r, size, palette.TextColor);
				return;
			}

			this.DrawText(graphics2D, label, r.Left + CellPadX * s, r, size, palette.TextColor);

			// The sort toggle: a neutral button holding a caret pointing the current way.
			double arrowWidth = 22 * s;
			double ax = r.Right - arrowWidth - 4 * s;
			double ay = r.Center.Y - 8 * s;
			var button = new RoundedRect(ax, ay, ax + arrowWidth, ay + 16 * s, 3 * s);
			graphics2D.Render(button, palette.WidgetBackground);
			graphics2D.Render(new Stroke(button, s), palette.WidgetStroke);
			double cx = ax + arrowWidth / 2;
			double cy = ay + 8 * s;
			double half = 4 * s;
			var caret = new VertexStorage();
			double tip = this.Reversed ? half * .6 : -half * .6;
			caret.MoveTo(cx - half, cy - tip);
			caret.LineTo(cx + half, cy - tip);
			caret.LineTo(cx, cy + tip);
			caret.ClosePolygon();
			graphics2D.Render(caret, palette.TextColor);
		}

		private void OnThemeChanged(object sender, EventArgs e)
		{
			this.Recolor();
			this.Invalidate();
		}

		/// <summary>Pushes the current theme into the widgets that copied their colours when they were built.</summary>
		private void Recolor()
		{
			this.kit.Recolor();
			DemoPalette palette = this.demoTheme.Palette;
			this.BackgroundColor = palette.PanelFill;
			this.rule.BackgroundColor = palette.Separator;
			this.Grid.TextColor = palette.TextColor;
			this.Grid.SeparatorColor = palette.Separator;
			this.Grid.AccentColor = this.kit.Theme.PrimaryAccentColor;
			this.Grid.SelectionColor = this.kit.Theme.PrimaryAccentColor.WithAlpha(90);
			this.Grid.ScrollbarColor = palette.TextDim.WithAlpha(140);
		}
	}
}
