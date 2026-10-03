// Copyright (c) 2026, Nicolas Musset, John Lewin, Lars Brubaker
// This file is licensed under the MIT license.
// See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Markdig.Extensions.Tables;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;

namespace Markdig.Renderers.Agg
{
	/// <summary>
	/// A markdown table: a column of <see cref="AggTableRow"/>s whose cells are sized per column. The table paints
	/// its whole grid and the zebra stripes itself, from the laid-out row and cell bounds, so every line spans the
	/// full grid and meets the others exactly (per-cell borders and separate rule widgets each ended at their own
	/// widget's edge and left gaps and overhangs at the joints).
	/// </summary>
	public class AggTable : FlowLayoutWidget
	{
		private bool inLayout;

		public List<AggTableColumn> Columns { get; }

		public List<AggTableRow> Rows { get; }

		/// <summary>
		/// No longer populated: the table draws its own rules (see <see cref="OnDraw"/>). Kept because AggTable is
		/// public library API.
		/// </summary>
		public List<HorizontalLine> HorizontalRules { get; } = new List<HorizontalLine>();

		/// <summary>
		/// The color of every grid line.
		/// </summary>
		public Color GridColor { get; set; } = Color.Black;

		/// <summary>
		/// Set to true when any cell in this table contains an image.
		/// When true, all columns use equal widths so images render at the same size.
		/// </summary>
		public bool HasImages { get; set; }

		public AggTable(Table table) : base(FlowDirection.TopToBottom)
		{
			this.Rows = new List<AggTableRow>();
			this.HAnchor = HAnchor.Stretch;
			this.Columns = table.ColumnDefinitions.Select(c => new AggTableColumn(c)).ToList();
		}

		/// <summary>
		/// The grid line thickness in whole device pixels: one pixel at DeviceScale 1, scaled and rounded above it.
		/// </summary>
		public static int GridLineThickness => Math.Max(1, (int)Math.Round(DeviceScale));

		public override void OnLayout(LayoutEventArgs layoutEventArgs)
		{
			if (inLayout)
			{
				return;
			}

			inLayout = true;
			try
			{
				ReserveGridLineSpace();

				base.OnLayout(layoutEventArgs);

				if (this.Columns?.Count > 0)
				{
					foreach (var column in this.Columns)
					{
						column.SetCellWidths();
					}

					// When any cell contains images, all columns use the same width
					// so images render at equal size — matching GitHub/VS Code table behavior.
					if (HasImages)
					{
						double maxColumnWidth = this.Columns.Max(c => c.CellWidth);

						// Expand columns to fill container width, divided equally, leaving the rows' inset and one
						// grid line per column plus the closing one.
						double rowInset = this.Rows.FirstOrDefault()?.DeviceMargin.Width ?? 0;
						double totalLineWidth = (this.Columns.Count + 1) * GridLineThickness;
						double containerSharePerColumn = Math.Floor((this.Width - rowInset - totalLineWidth) / this.Columns.Count);

						double targetWidth = Math.Max(maxColumnWidth, containerSharePerColumn);

						foreach (var column in this.Columns)
						{
							column.SetCellWidths(targetWidth);
						}
					}
				}

				// Re-run layout after columns have real measured widths.
				base.OnLayout(layoutEventArgs);
			}
			finally
			{
				inLayout = false;
			}
		}

		/// <summary>
		/// Margins open exactly one grid line of device pixels before every cell and row, and after the last ones,
		/// so the lines drawn into those gaps never touch cell content. Margins are scaled by DeviceScale, so the
		/// unscaled value is the whole-pixel thickness divided back down (redone each layout, so a DeviceScale
		/// change keeps whole-pixel gaps).
		/// </summary>
		private void ReserveGridLineSpace()
		{
			// The base constructor lays out before Rows is assigned.
			if (this.Rows == null)
			{
				return;
			}

			double line = GridLineThickness / DeviceScale;
			for (int r = 0; r < this.Rows.Count; r++)
			{
				var row = this.Rows[r];
				var rowMargin = new BorderDouble(row.Margin.Left, r == this.Rows.Count - 1 ? line : 0, row.Margin.Right, line);
				if (row.Margin != rowMargin)
				{
					row.Margin = rowMargin;
				}

				for (int c = 0; c < row.Cells.Count; c++)
				{
					var cellMargin = new BorderDouble(line, 0, c == row.Cells.Count - 1 ? line : 0, 0);
					if (row.Cells[c].Margin != cellMargin)
					{
						row.Cells[c].Margin = cellMargin;
					}
				}
			}
		}

		public override void OnDrawBackground(Graphics2D graphics2D)
		{
			base.OnDrawBackground(graphics2D);

			if (!MeasureGrid(graphics2D))
			{
				return;
			}

			// Stripes fill each cell between the grid lines, so they never tint a line.
			for (int r = 0; r < gridRows.Count; r++)
			{
				var stripe = gridRows[r].StripeColor;
				if (stripe.Alpha0To255 == 0)
				{
					continue;
				}

				for (int c = 0; c + 1 < lineLefts.Count; c++)
				{
					graphics2D.FillRectangle(lineLefts[c] + lineThickness, lineBottoms[r + 1] + lineThickness, lineLefts[c + 1], lineBottoms[r], stripe);
				}
			}
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			base.OnDraw(graphics2D);

			// OnDrawBackground normally measured this frame at this same transform; measure again only when it did
			// not (a caller drawing the table without its background, or a different transform).
			var transform = graphics2D.GetTransform();
			bool measured = gridMeasured && transform.tx == measuredTx && transform.ty == measuredTy;
			if (!measured && !MeasureGrid(graphics2D))
			{
				return;
			}

			// Consumed: the next frame measures afresh.
			gridMeasured = false;

			double gridLeft = lineLefts[0];
			double gridRight = lineLefts[lineLefts.Count - 1] + lineThickness;

			for (int r = 0; r < lineBottoms.Count; r++)
			{
				graphics2D.FillRectangle(gridLeft, lineBottoms[r], gridRight, lineBottoms[r] + lineThickness, GridColor);
			}

			// Verticals run between the rules rather than across them, so the alpha color is not painted twice where
			// they cross.
			for (int c = 0; c < lineLefts.Count; c++)
			{
				double left = lineLefts[c];
				for (int r = 0; r + 1 < lineBottoms.Count; r++)
				{
					graphics2D.FillRectangle(left, lineBottoms[r + 1] + lineThickness, left + lineThickness, lineBottoms[r], GridColor);
				}
			}
		}

		// The grid measured for the frame being drawn. Kept on the table and refilled in place so drawing allocates
		// nothing per frame.
		private readonly List<AggTableRow> gridRows = new List<AggTableRow>();
		private readonly List<double> lineLefts = new List<double>();
		private readonly List<double> lineBottoms = new List<double>();
		private int lineThickness;
		private bool gridMeasured;
		private double measuredTx;
		private double measuredTy;

		/// <summary>
		/// Fills the grid line positions in table space, each snapped so it covers whole device pixels: the left
		/// edge of every vertical line (left to right) in <see cref="lineLefts"/> and the bottom edge of every
		/// horizontal rule (top to bottom) in <see cref="lineBottoms"/>. Returns false, with both empty, when the
		/// table has no cells to frame.
		/// </summary>
		private bool MeasureGrid(Graphics2D graphics2D)
		{
			gridRows.Clear();
			lineLefts.Clear();
			lineBottoms.Clear();
			gridMeasured = false;

			// Every column's cells share one width, so the row with the most cells gives every column boundary.
			AggTableRow columnRow = null;
			if (this.Rows != null)
			{
				foreach (var row in this.Rows)
				{
					if (row.Parent != this)
					{
						continue;
					}

					gridRows.Add(row);
					if (row.Cells.Count > 0 && (columnRow == null || row.Cells.Count > columnRow.Cells.Count))
					{
						columnRow = row;
					}
				}
			}

			if (columnRow == null)
			{
				gridRows.Clear();
				return false;
			}

			lineThickness = GridLineThickness;
			var transform = graphics2D.GetTransform();
			double tx = transform.tx;
			double ty = transform.ty;

			// Floor(v + .5), not Math.Round: banker's rounding sends some .5 edges down and others up (DeviceScale 1.25
			// puts edges on .5), which made columns differ by a pixel and let a line eat into a cell's padding.
			double SnapX(double x) => Math.Floor(x + tx + 0.5) - tx;
			double SnapY(double y) => Math.Floor(y + ty + 0.5) - ty;

			// Each vertical line sits in the gap its cell's left margin opened; the closing one after the last cell.
			foreach (var cell in columnRow.Cells)
			{
				lineLefts.Add(SnapX(cell.TransformToParentSpace(this, cell.LocalBounds).Left) - lineThickness);
			}

			var lastCell = columnRow.Cells[columnRow.Cells.Count - 1];
			lineLefts.Add(SnapX(lastCell.TransformToParentSpace(this, lastCell.LocalBounds).Right));

			// Each rule sits in the gap its row's top margin opened; the closing one below the last row.
			foreach (var row in gridRows)
			{
				lineBottoms.Add(SnapY(row.TransformToParentSpace(this, row.LocalBounds).Top));
			}

			var lastRow = gridRows[gridRows.Count - 1];
			lineBottoms.Add(SnapY(lastRow.TransformToParentSpace(this, lastRow.LocalBounds).Bottom) - lineThickness);

			gridMeasured = true;
			measuredTx = tx;
			measuredTy = ty;
			return true;
		}
	}
}
