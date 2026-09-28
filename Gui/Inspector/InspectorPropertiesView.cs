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
using MatterHackers.Agg.Font;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// The inspector's properties pane, after agg-gui's paint_properties (widgets/inspector_props.rs): a "PROPERTIES"
	/// label, the selected widget's type as a heading, then its name/value rows - geometry, margin, padding, anchors
	/// and its own flags - or "(select a widget)" when nothing is selected.
	/// </summary>
	/// <remarks>
	/// Values are tinted by kind as agg-gui's: margins on an orange strip, padding (shown only when some side is
	/// non-zero, and read-only) on a green one, anchors on a blue one, and flags coloured green for true and red for
	/// false. Clicking a number's left half steps it down and its right half up (the strip shows - and +); clicking an
	/// anchor picks the next one and clicking visible or enabled toggles it. The click raises
	/// <see cref="EditRequested"/>, which the panel queues and applies on the next idle. Below the rows, where the pane
	/// has room, agg-gui's box preview: the widget's shape as a blue box labelled "W x H".
	/// </remarks>
	public class InspectorPropertiesView : GuiWidget
	{
		/// <summary>The height before the panel sizes the pane to its split, in logical units.</summary>
		public const double LogicalHeight = InspectorPanel.DefaultPropertiesHeight;

		/// <summary>agg-gui's row_h.</summary>
		private const double LogicalRowHeight = 18;

		/// <summary>agg-gui's first row baseline, below the pane's top.</summary>
		private const double LogicalFirstRow = 56;

		/// <summary>A row's text sits this far below its centre (agg-gui's value strip runs from 3 below the baseline to
		/// 11 above it).</summary>
		private const double LogicalBaselineDrop = 4;

		private readonly InspectorModel model;

		public InspectorPropertiesView(InspectorModel model, InspectorStyle style)
		{
			this.model = model;
			this.Style = style;
			this.Name = "Inspector Properties";
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Absolute;
			this.Height = LogicalHeight * DeviceScale;
			model.Changed += this.Model_Changed;
		}

		/// <summary>A click on an editable value made this edit for the selected widget.</summary>
		public event Action<InspectorEdit> EditRequested;

		public InspectorStyle Style { get; set; }

		/// <summary>The rows the pane shows for <paramref name="node"/>, name then value, top to bottom.</summary>
		public static List<(string Name, string Value)> PropertiesOf(InspectorNode node)
		{
			return RowsOf(node).Select(row => (row.Name, row.Value)).ToList();
		}

		/// <summary>The rows the pane shows for <paramref name="node"/>, top to bottom, with what each can edit.</summary>
		public static List<InspectorPropertyRow> RowsOf(InspectorNode node)
		{
			RectangleDouble b = node.ScreenBounds;
			BorderDouble m = node.Margin;
			BorderDouble p = node.Padding;

			InspectorPropertyRow Size(string name, InspectorEditField field, double value) =>
				new InspectorPropertyRow(name, $"{value:0.0}", field, value, InspectorPropertyRow.NumericStep(value));

			InspectorPropertyRow Flag(string name, InspectorEditField field, bool value) =>
				new InspectorPropertyRow(name, value ? "true" : "false", field, value ? 1 : 0) { Kind = InspectorValueKind.Flag };

			InspectorPropertyRow Margin(string name, InspectorEditField field, double value) =>
				new InspectorPropertyRow(name, $"{value:0.0}", field, value, InspectorPropertyRow.InsetStep(value)) { Kind = InspectorValueKind.Margin };

			InspectorPropertyRow Pad(string name, double value) =>
				new InspectorPropertyRow(name, $"{value:0.0}") { Kind = InspectorValueKind.Padding };

			InspectorPropertyRow Anchor(string name, InspectorEditField field, int value, string text) =>
				new InspectorPropertyRow(name, text, field, value) { Kind = InspectorValueKind.Anchor };

			// agg-gui's layout: the type is the heading above the rows, and there is no name row.
			var rows = new List<InspectorPropertyRow>
			{
				new InspectorPropertyRow("x", $"{b.Left:0.0}"),
				new InspectorPropertyRow("y", $"{b.Bottom:0.0}"),
				Size("width", InspectorEditField.Width, b.Width),
				Size("height", InspectorEditField.Height, b.Height),
				new InspectorPropertyRow("depth", node.Depth.ToString()),
				Margin("margin.left", InspectorEditField.MarginLeft, m.Left),
				Margin("margin.right", InspectorEditField.MarginRight, m.Right),
				Margin("margin.top", InspectorEditField.MarginTop, m.Top),
				Margin("margin.bottom", InspectorEditField.MarginBottom, m.Bottom),
			};

			if (p.Left != 0 || p.Right != 0 || p.Top != 0 || p.Bottom != 0)
			{
				rows.Add(Pad("pad.left", p.Left));
				rows.Add(Pad("pad.right", p.Right));
				rows.Add(Pad("pad.top", p.Top));
				rows.Add(Pad("pad.bottom", p.Bottom));
			}

			rows.Add(Anchor("h_anchor", InspectorEditField.HAnchor, (int)node.HAnchor, node.HAnchor.ToString()));
			rows.Add(Anchor("v_anchor", InspectorEditField.VAnchor, (int)node.VAnchor, node.VAnchor.ToString()));
			rows.Add(Flag("visible", InspectorEditField.Visible, node.Visible));
			rows.Add(Flag("enabled", InspectorEditField.Enabled, node.Enabled));
			rows.Add(new InspectorPropertyRow("children", node.ChildCount.ToString()));
			return rows;
		}

		/// <summary>The vertical centre of row <paramref name="index"/> (of <see cref="RowsOf"/>) in the pane.</summary>
		public double RowCenterY(int index)
		{
			return this.Height - (LogicalFirstRow - LogicalBaselineDrop + index * LogicalRowHeight) * DeviceScale;
		}

		/// <summary>The strip an editable row's value sits on, agg-gui's 16-unit hit rectangle: the right half of the pane.</summary>
		public RectangleDouble ValueBounds(int index)
		{
			double y = this.RowCenterY(index);
			double half = 8 * DeviceScale;
			return new RectangleDouble(this.Width / 2, y - half, this.Width - 2 * DeviceScale, y + half);
		}

		/// <summary>
		/// agg-gui's box-model preview for the selected widget: a box of its aspect, centred in the room below the
		/// last row, or null when nothing is selected or the room is under 30 logical units.
		/// </summary>
		public RectangleDouble? BoxPreviewBounds()
		{
			InspectorNode node = this.model.Selected;
			if (node == null)
			{
				return null;
			}

			// agg-gui works in logical units up from the pane's bottom; ry is where the next row would sit
			double scale = DeviceScale;
			double ry = this.RowCenterY(RowsOf(node).Count) / scale - LogicalBaselineDrop;
			double diagramHeight = Math.Min(ry - 8, 80);
			if (diagramHeight <= 30)
			{
				return null;
			}

			RectangleDouble b = node.ScreenBounds;
			double diagramTop = diagramHeight - 4;
			double diagramWidth = this.Width / scale - 20;
			double aspect = b.Height > 0 ? b.Width / b.Height : 1;
			double boxHeight = Math.Min(diagramHeight * .6, 50);
			double boxWidth = Math.Min(boxHeight * aspect, diagramWidth * .8);
			double boxX = 10 + (diagramWidth - boxWidth) / 2;
			double boxY = diagramTop - (diagramHeight + boxHeight) / 2;
			return new RectangleDouble(boxX * scale, boxY * scale, (boxX + boxWidth) * scale, (boxY + boxHeight) * scale);
		}

		/// <summary>
		/// The pane less its top edge, which is the split's grab zone reaching down past the gap (agg-gui only edits
		/// below split - 2, and grabs the split within 5 of it).
		/// </summary>
		public override bool PositionWithinLocalBounds(double x, double y)
		{
			double grabBelowGap = (InspectorSplitBar.LogicalGrab - InspectorSplitBar.LogicalLine) * DeviceScale;
			return base.PositionWithinLocalBounds(x, y) && y < this.Height - grabBelowGap;
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			InspectorNode node = this.model.Selected;
			if (node != null && mouseEvent.Button == MouseButtons.Left)
			{
				List<InspectorPropertyRow> rows = RowsOf(node);
				for (int i = 0; i < rows.Count; i++)
				{
					RectangleDouble strip = this.ValueBounds(i);
					if (rows[i].Field != null && strip.Contains(mouseEvent.Position))
					{
						this.EditRequested?.Invoke(rows[i].EditFor(node.Widget, mouseEvent.X >= strip.XCenter));
						break;
					}
				}
			}

			base.OnMouseDown(mouseEvent);
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			// agg-gui's font sizes are pixels; agg-sharp's are points at 3/4 of that, as the tree's.
			double scale = DeviceScale;
			double pointSize = 9 * scale;
			double left = 12 * scale;
			double right = this.Width - 10 * scale;

			graphics2D.DrawString("PROPERTIES", 10 * scale, this.Height - 14 * scale, 7.5 * scale, Justification.Left, Baseline.Text, this.Style.DimText);
			graphics2D.FillRectangle(80 * scale, this.Height - 10.5 * scale, this.Width - 8 * scale, this.Height - 9.5 * scale, this.Style.Separator);

			InspectorNode node = this.model.Selected;
			if (node == null)
			{
				graphics2D.DrawString("(select a widget)", 10 * scale, this.Height - 36 * scale, pointSize, Justification.Left, Baseline.Text, this.Style.DimText);
			}
			else
			{
				graphics2D.DrawString(node.TypeName, 10 * scale, this.Height - 36 * scale, 10.5 * scale, Justification.Left, Baseline.Text, this.Style.Text);
				List<InspectorPropertyRow> rows = RowsOf(node);
				for (int i = 0; i < rows.Count; i++)
				{
					InspectorPropertyRow row = rows[i];
					double y = this.RowCenterY(i) - LogicalBaselineDrop * scale;
					if (y < 4 * scale)
					{
						break;
					}

					bool steps = row.Field != null && row.Step > 0;
					Color? tint = row.Kind switch
					{
						InspectorValueKind.Margin => this.Style.MarginValue,
						InspectorValueKind.Padding => this.Style.PaddingValue,
						InspectorValueKind.Anchor => this.Style.AnchorValue,
						InspectorValueKind.Flag => null,
						_ => row.Field != null ? this.Style.EditableValue : null,
					};
					if (tint is Color strip)
					{
						// agg-gui's strip: 3 below the baseline to 11 above, across the right half
						graphics2D.FillRectangle(this.Width / 2, y - 3 * scale, this.Width - 2 * scale, y + 11 * scale, strip);
						if (steps)
						{
							graphics2D.DrawString("-", this.Width / 2 + 4 * scale, y, pointSize, Justification.Left, Baseline.Text, this.Style.DimText);
							graphics2D.DrawString("+", right, y, pointSize, Justification.Right, Baseline.Text, this.Style.DimText);
						}
					}

					Color valueColor = row.Kind == InspectorValueKind.Flag
						? (row.Current != 0 ? this.Style.TrueValue : this.Style.FalseValue)
						: this.Style.Text;
					graphics2D.DrawString(row.Name, left, y, pointSize, Justification.Left, Baseline.Text, this.Style.DimText);
					graphics2D.DrawString(row.Value, steps ? right - 12 * scale : right, y, pointSize, Justification.Right, Baseline.Text, valueColor);

					// agg-gui's half-unit separator under each row
					graphics2D.FillRectangle(8 * scale, y - 4.25 * scale, this.Width - 8 * scale, y - 3.75 * scale, this.Style.Separator);
				}

				if (this.BoxPreviewBounds() is RectangleDouble box)
				{
					graphics2D.FillRectangle(box, new Color(26, 128, 255, 26));
					graphics2D.Rectangle(box, new Color(26, 128, 255, 128), scale);
					string size = $"{node.ScreenBounds.Width:0} × {node.ScreenBounds.Height:0}";
					if (new TypeFacePrinter(size, 10 * scale).LocalBounds.Width < box.Width - 4 * scale)
					{
						graphics2D.DrawString(size, box.XCenter, box.YCenter, 10 * scale, Justification.Center, Baseline.BoundsCenter, new Color(26, 102, 230, 204));
					}
				}
			}

			base.OnDraw(graphics2D);
		}

		public override void OnClosed(EventArgs e)
		{
			this.model.Changed -= this.Model_Changed;
			base.OnClosed(e);
		}

		private void Model_Changed(object sender, EventArgs e) => this.Invalidate();
	}
}
