/*
Copyright (c) 2026, Lars Brubaker, MatterHackers, Inc.
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
using System.Linq;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Transform;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.UI
{
	/// <summary>One cell handed to a <see cref="VirtualTable.CellPainter"/>; <see cref="Bounds"/> is in the table's pixels.</summary>
	public readonly struct TableCellInfo
	{
		public TableCellInfo(int row, int column, RectangleDouble bounds, bool selected)
		{
			this.Row = row;
			this.Column = column;
			this.Bounds = bounds;
			this.Selected = selected;
		}

		public int Row { get; }

		public int Column { get; }

		public RectangleDouble Bounds { get; }

		public bool Selected { get; }
	}

	/// <summary>
	/// A row-virtualised table (agg-gui's Table): a sticky header over a body that scrolls both ways, drawing only
	/// the rows in view through <see cref="CellPainter"/>, so a hundred thousand rows cost what a screenful does.
	/// Columns share the width by <see cref="TableColumn.DistributeWidths"/> and a resizable column's header edge
	/// can be dragged; rows can be striped, overlined, selected and clicked. Sizes are design units; painters
	/// get pixel rectangles and the drawing is clipped to each cell.
	/// </summary>
	public class VirtualTable : GuiWidget
	{
		/// <summary>Half the width, in design units, of the grab zone around a resizable header edge.</summary>
		public const double ResizeGrabHalfWidth = 4;

		/// <summary>The scrollbars' thickness in design units.</summary>
		public const double ScrollbarThickness = 8;

		private readonly List<TableColumn> columns;
		private double[] widths = Array.Empty<double>();
		private TableRows rows = TableRows.Empty;
		private double scrollOffset;
		private double horizontalOffset;

		// The header edge being dragged: its column, the pointer's content x and the column's width at the press.
		private (int Column, double StartX, double StartWidth)? columnDrag;

		// The scrollbar thumb being dragged: vertical or not, and the pointer and offset at the press.
		private (bool Vertical, double StartPointer, double StartOffset)? thumbDrag;

		public VirtualTable(IEnumerable<TableColumn> columns)
		{
			this.columns = columns.ToList();
			this.ColumnOverrides = new double?[this.columns.Count];
		}

		public IReadOnlyList<TableColumn> Columns => this.columns;

		/// <summary>The width each column had at the last layout, in design units.</summary>
		public IReadOnlyList<double> ColumnWidths => this.UpdateWidths();

		/// <summary>Widths the user dragged columns to (null where untouched); <see cref="ResetColumnWidths"/> clears them.</summary>
		public double?[] ColumnOverrides { get; }

		public TableRows Rows
		{
			get => this.rows;
			set
			{
				this.rows = value ?? TableRows.Empty;
				this.ScrollOffset = this.scrollOffset;
				if (this.HoveredRow >= this.rows.Count)
				{
					this.HoveredRow = -1;
				}

				this.Invalidate();
			}
		}

		public double HeaderHeight { get; set; } = 22;

		public bool Striped { get; set; }

		/// <summary>Whether header edges of resizable columns can be dragged.</summary>
		public bool ResizableColumns { get; set; } = true;

		/// <summary>Whether a click on a row raises <see cref="RowClicked"/>.</summary>
		public bool ClickableRows { get; set; }

		public Func<int, bool> IsRowSelected { get; set; }

		/// <summary>Rows that get an accent line along their top.</summary>
		public Func<int, bool> IsRowOverlined { get; set; }

		public Action<Graphics2D, TableCellInfo> CellPainter { get; set; }

		/// <summary>Draws a header cell; the <see cref="TableCellInfo.Row"/> is -1.</summary>
		public Action<Graphics2D, TableCellInfo> HeaderPainter { get; set; }

		/// <summary>A click on a row: its index and the column hit.</summary>
		public event Action<int, int> RowClicked;

		/// <summary>A click on a header cell away from any resize edge: the column.</summary>
		public event Action<int> HeaderClicked;

		/// <summary>The row under the pointer, or -1.</summary>
		public int HoveredRow { get; private set; } = -1;

		public Color TextColor { get; set; } = Color.Black;

		public Color SeparatorColor { get; set; } = new Color(0, 0, 0, 60);

		public Color AccentColor { get; set; } = new Color(0, 120, 215);

		public Color SelectionColor { get; set; } = new Color(0, 120, 215, 90);

		public Color ScrollbarColor { get; set; } = new Color(128, 128, 128, 140);

		/// <summary>How far the body is scrolled down, in design units, held to its travel.</summary>
		public double ScrollOffset
		{
			get => this.scrollOffset;
			set
			{
				double clamped = Math.Max(0, Math.Min(value, this.MaxScrollOffset));
				if (clamped != this.scrollOffset)
				{
					this.scrollOffset = clamped;
					this.Invalidate();
				}
			}
		}

		/// <summary>How far the body and header are scrolled right, in design units, held to their travel.</summary>
		public double HorizontalOffset
		{
			get => this.horizontalOffset;
			set
			{
				double clamped = Math.Max(0, Math.Min(value, this.MaxHorizontalOffset));
				if (clamped != this.horizontalOffset)
				{
					this.horizontalOffset = clamped;
					this.Invalidate();
				}
			}
		}

		public double MaxScrollOffset => Math.Max(0, this.rows.TotalHeight - this.BodyHeight);

		public double MaxHorizontalOffset => Math.Max(0, this.ContentWidth - this.ViewWidth);

		/// <summary>The sum of the column widths, in design units.</summary>
		public double ContentWidth => this.UpdateWidths().Sum();

		private double Scale => DeviceScale;

		private double ViewWidth => Math.Max(0, this.Width / this.Scale - ScrollbarThickness);

		private double BodyHeight => Math.Max(0, this.Height / this.Scale - this.HeaderHeight - (this.HasHorizontalBar ? ScrollbarThickness : 0));

		private bool HasHorizontalBar => this.widths.Sum() > this.ViewWidth + .5;

		/// <summary>Scrolls so <paramref name="row"/> (clamped to the rows) is at the top of the body, or as near as the travel allows.</summary>
		public void ScrollToRow(int row)
		{
			if (this.rows.Count > 0)
			{
				this.ScrollOffset = this.rows.TopOf(Math.Max(0, Math.Min(row, this.rows.Count - 1)));
			}
		}

		/// <summary>Forgets every width the user dragged a column to.</summary>
		public void ResetColumnWidths()
		{
			Array.Clear(this.ColumnOverrides, 0, this.ColumnOverrides.Length);
			this.UpdateWidths();
			this.HorizontalOffset = this.horizontalOffset;
			this.Invalidate();
		}

		/// <summary>The row and column at <paramref name="position"/> (this widget's pixels), or -1s outside the body.</summary>
		public (int Row, int Column) HitCell(Vector2 position)
		{
			double x = position.X / this.Scale;
			double bodyTop = this.Height / this.Scale - this.HeaderHeight;
			double topDown = bodyTop - position.Y / this.Scale;
			if (x < 0 || x >= this.ViewWidth || topDown < 0 || topDown >= this.BodyHeight)
			{
				return (-1, -1);
			}

			int row = this.rows.RowAt(topDown + this.scrollOffset);
			return (row, row < 0 ? -1 : this.ColumnAt(x + this.horizontalOffset));
		}

		/// <summary>
		/// <paramref name="text"/> cut to fit <paramref name="width"/> pixels at <paramref name="pointSize"/>, ending in
		/// an ellipsis when cut (agg-gui's clip_text_to_width).
		/// </summary>
		public static string ClipTextToWidth(string text, double pointSize, double width)
		{
			double Measure(string s) => new TypeFacePrinter(s, pointSize).GetSize().X;
			if (Measure(text) <= width)
			{
				return text;
			}

			const string Ellipsis = "…";
			int low = 0;
			int high = text.Length;
			while (low < high)
			{
				int mid = (low + high + 1) / 2;
				if (Measure(text.Substring(0, mid) + Ellipsis) <= width)
				{
					low = mid;
				}
				else
				{
					high = mid - 1;
				}
			}

			return low == 0 ? (Measure(Ellipsis) <= width ? Ellipsis : string.Empty) : text.Substring(0, low) + Ellipsis;
		}

		public override void OnBoundsChanged(EventArgs e)
		{
			base.OnBoundsChanged(e);
			this.UpdateWidths();
			this.ScrollOffset = this.scrollOffset;
			this.HorizontalOffset = this.horizontalOffset;
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			double s = this.Scale;
			double[] columnWidths = this.UpdateWidths();
			double contentWidth = columnWidths.Sum();
			double headerTop = this.Height;
			double headerBottom = this.Height - this.HeaderHeight * s;
			double viewRight = this.ViewWidth * s;
			double bodyBottom = headerBottom - this.BodyHeight * s;
			double left0 = -this.horizontalOffset * s;

			RectangleDouble savedClip = graphics2D.GetClippingRect();
			this.DrawBody(graphics2D, columnWidths, contentWidth, headerBottom, bodyBottom, viewRight, savedClip);

			// Header: a faint band, a rule under it, the painter's cells and the column edges.
			graphics2D.FillRectangle(0, headerBottom, viewRight, headerTop, new Color(128, 128, 128, 26));
			graphics2D.FillRectangle(0, headerBottom - s, viewRight, headerBottom, this.SeparatorColor);
			double x = left0;
			for (int column = 0; column < columnWidths.Length; column++)
			{
				var cell = new RectangleDouble(x, headerBottom, x + columnWidths[column] * s, headerTop);
				if (this.HeaderPainter != null && ClipTo(graphics2D, cell, new RectangleDouble(0, headerBottom, viewRight, headerTop), savedClip))
				{
					this.HeaderPainter(graphics2D, new TableCellInfo(-1, column, cell, false));
				}

				graphics2D.SetClippingRect(savedClip);
				x = cell.Right;
				if (column + 1 < columnWidths.Length && x < viewRight)
				{
					bool active = this.columnDrag?.Column == column;
					double lineWidth = (active ? 2 : 1) * s;
					graphics2D.FillRectangle(x - lineWidth / 2, headerBottom, x + lineWidth / 2, headerTop, active ? this.AccentColor : this.SeparatorColor);
				}
			}

			this.DrawScrollbars(graphics2D, contentWidth, headerBottom, bodyBottom, viewRight);
			base.OnDraw(graphics2D);
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			base.OnMouseDown(mouseEvent);
			if (mouseEvent.Button != MouseButtons.Left)
			{
				return;
			}

			double s = this.Scale;
			if (this.ThumbAt(mouseEvent.Position, out bool vertical))
			{
				this.thumbDrag = (vertical, vertical ? mouseEvent.Y : mouseEvent.X, vertical ? this.scrollOffset : this.horizontalOffset);
				return;
			}

			int edge = this.ResizeEdgeAt(mouseEvent.Position);
			if (edge >= 0)
			{
				this.columnDrag = (edge, mouseEvent.X / s + this.horizontalOffset, this.widths[edge]);
				this.Invalidate();
			}
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			base.OnMouseMove(mouseEvent);
			double s = this.Scale;
			if (this.thumbDrag is (bool vertical, double startPointer, double startOffset))
			{
				if (vertical)
				{
					double travel = this.BodyHeight - this.ThumbLength(this.BodyHeight, this.rows.TotalHeight);
					double delta = (startPointer - mouseEvent.Y) / s;
					this.ScrollOffset = startOffset + (travel > 0 ? delta / travel * this.MaxScrollOffset : 0);
				}
				else
				{
					double travel = this.ViewWidth - this.ThumbLength(this.ViewWidth, this.ContentWidth);
					double delta = (mouseEvent.X - startPointer) / s;
					this.HorizontalOffset = startOffset + (travel > 0 ? delta / travel * this.MaxHorizontalOffset : 0);
				}

				return;
			}

			if (this.columnDrag is (int column, double startX, double startWidth))
			{
				this.ColumnOverrides[column] = Math.Max(TableColumn.MinimumWidth, startWidth + mouseEvent.X / s + this.horizontalOffset - startX);
				this.UpdateWidths();
				this.HorizontalOffset = this.horizontalOffset;
				this.Invalidate();
				return;
			}

			this.Cursor = this.ResizeEdgeAt(mouseEvent.Position) >= 0 ? Cursors.SizeWE : Cursors.Default;
			int hovered = this.HitCell(mouseEvent.Position).Row;
			if (hovered != this.HoveredRow)
			{
				this.HoveredRow = hovered;
				this.Invalidate();
			}
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			bool wasDragging = this.columnDrag != null || this.thumbDrag != null;
			this.columnDrag = null;
			this.thumbDrag = null;
			if (!wasDragging && !mouseEvent.Cancelled && mouseEvent.Button == MouseButtons.Left && this.PositionWithinLocalBounds(mouseEvent.X, mouseEvent.Y))
			{
				double s = this.Scale;
				double headerBottom = this.Height - this.HeaderHeight * s;
				if (mouseEvent.Y >= headerBottom && mouseEvent.X < this.ViewWidth * s)
				{
					int column = this.ColumnAt(mouseEvent.X / s + this.horizontalOffset);
					if (column >= 0)
					{
						this.HeaderClicked?.Invoke(column);
					}
				}
				else if (this.ClickableRows)
				{
					(int row, int column) = this.HitCell(mouseEvent.Position);
					if (row >= 0)
					{
						this.RowClicked?.Invoke(row, column);
					}
				}
			}

			this.Invalidate();
			base.OnMouseUp(mouseEvent);
		}

		public override void OnMouseLeaveBounds(MouseEventArgs mouseEvent)
		{
			if (this.HoveredRow != -1)
			{
				this.HoveredRow = -1;
				this.Invalidate();
			}

			base.OnMouseLeaveBounds(mouseEvent);
		}

		public override void OnMouseWheel(MouseEventArgs mouseEvent)
		{
			base.OnMouseWheel(mouseEvent);

			// As ScrollableWidget: a trackpad's delta is already pixels, a wheel notch is design units.
			double wheelScale = (mouseEvent.WheelDeltaIsPreciseScroll ? 1 : DeviceScale) / this.Scale;
			double oldOffset = this.scrollOffset;
			this.ScrollOffset -= mouseEvent.WheelDelta / 5.0 * wheelScale;
			if (oldOffset != this.scrollOffset)
			{
				mouseEvent.WheelDelta = 0;
			}

			double oldHorizontal = this.horizontalOffset;
			this.HorizontalOffset -= mouseEvent.WheelDeltaX / 5.0 * wheelScale;
			if (oldHorizontal != this.horizontalOffset)
			{
				mouseEvent.WheelDeltaX = 0;
			}
		}

		/// <summary>Clips drawing to <paramref name="cell"/> within <paramref name="view"/> (local pixels) and the saved clip; false when nothing is left.</summary>
		private static bool ClipTo(Graphics2D graphics2D, RectangleDouble cell, RectangleDouble view, RectangleDouble savedClip)
		{
			var local = default(RectangleDouble);
			if (!local.IntersectRectangles(cell, view))
			{
				return false;
			}

			// The clip rectangle lives in the target's pixels; the widget transform is a translation (and scale).
			Affine transform = graphics2D.GetTransform();
			double x0 = local.Left, y0 = local.Bottom, x1 = local.Right, y1 = local.Top;
			transform.Transform(ref x0, ref y0);
			transform.Transform(ref x1, ref y1);
			var screen = new RectangleDouble(Math.Min(x0, x1), Math.Min(y0, y1), Math.Max(x0, x1), Math.Max(y0, y1));
			var clip = default(RectangleDouble);
			if (!clip.IntersectRectangles(screen, savedClip))
			{
				return false;
			}

			graphics2D.SetClippingRect(clip);
			return true;
		}

		/// <summary>The visible rows: fill (selection, stripe, hover), the painter's cells, overlines, then column dividers.</summary>
		private void DrawBody(Graphics2D graphics2D, double[] columnWidths, double contentWidth, double bodyTop, double bodyBottom, double viewRight, RectangleDouble savedClip)
		{
			double s = this.Scale;
			var view = new RectangleDouble(0, bodyBottom, viewRight, bodyTop);
			double left0 = -this.horizontalOffset * s;
			double rowRight = Math.Min(viewRight, left0 + contentWidth * s);
			Color hoverColor = this.AccentColor.WithAlpha(26);
			Color dividerColor = this.SeparatorColor.WithAlpha(this.SeparatorColor.alpha * 4 / 10);
			int first = this.rows.FirstVisibleRow(this.scrollOffset);
			for (int row = first; row < this.rows.Count; row++)
			{
				double topDown = this.rows.TopOf(row) - this.scrollOffset;
				if (topDown >= this.BodyHeight)
				{
					break;
				}

				double top = bodyTop - topDown * s;
				double bottom = top - this.rows.HeightAt(row) * s;
				bool selected = this.IsRowSelected?.Invoke(row) == true;
				var band = new RectangleDouble(0, Math.Max(bottom, bodyBottom), rowRight, Math.Min(top, bodyTop));
				if (selected)
				{
					graphics2D.FillRectangle(band, this.SelectionColor);
				}
				else
				{
					if (this.Striped && row % 2 == 0)
					{
						graphics2D.FillRectangle(band, new Color(128, 128, 128, 18));
					}

					if (row == this.HoveredRow)
					{
						graphics2D.FillRectangle(band, hoverColor);
					}
				}

				if (this.CellPainter != null)
				{
					double x = left0;
					for (int column = 0; column < columnWidths.Length && x < viewRight; column++)
					{
						var cell = new RectangleDouble(x, bottom, x + columnWidths[column] * s, top);
						if (cell.Right > 0 && ClipTo(graphics2D, cell, view, savedClip))
						{
							this.CellPainter(graphics2D, new TableCellInfo(row, column, cell, selected));
						}

						graphics2D.SetClippingRect(savedClip);
						x = cell.Right;
					}
				}

				if (this.IsRowOverlined?.Invoke(row) == true && top <= bodyTop)
				{
					graphics2D.FillRectangle(0, top - 1.5 * s, rowRight, top, this.AccentColor);
				}
			}

			double edge = left0;
			for (int column = 0; column + 1 < columnWidths.Length; column++)
			{
				edge += columnWidths[column] * s;
				if (edge > 0 && edge < viewRight)
				{
					graphics2D.FillRectangle(edge - s / 2, bodyBottom, edge + s / 2, bodyTop, dividerColor);
				}
			}
		}

		private void DrawScrollbars(Graphics2D graphics2D, double contentWidth, double bodyTop, double bodyBottom, double viewRight)
		{
			double s = this.Scale;
			double total = this.rows.TotalHeight;
			if (total > this.BodyHeight)
			{
				double length = this.ThumbLength(this.BodyHeight, total);
				double top = bodyTop - (this.BodyHeight - length) * (this.scrollOffset / this.MaxScrollOffset) * s;
				graphics2D.FillRectangle(new RectangleDouble(viewRight + 2 * s, top - length * s, this.Width - 2 * s, top), this.ScrollbarColor);
			}

			if (this.HasHorizontalBar)
			{
				double length = this.ThumbLength(this.ViewWidth, contentWidth);
				double left = (this.ViewWidth - length) * (this.horizontalOffset / Math.Max(1e-9, this.MaxHorizontalOffset)) * s;
				graphics2D.FillRectangle(new RectangleDouble(left, bodyBottom - (ScrollbarThickness - 2) * s, left + length * s, bodyBottom - 2 * s), this.ScrollbarColor);
			}
		}

		/// <summary>The thumb's length in design units for a <paramref name="view"/> over <paramref name="content"/>.</summary>
		private double ThumbLength(double view, double content) => content <= 0 ? view : Math.Max(Math.Min(20, view), view * Math.Min(1, view / content));

		private bool ThumbAt(Vector2 position, out bool vertical)
		{
			double s = this.Scale;
			double headerBottom = this.Height - this.HeaderHeight * s;
			double bodyBottom = headerBottom - this.BodyHeight * s;
			vertical = position.X >= this.ViewWidth * s && position.Y < headerBottom && position.Y >= bodyBottom && this.rows.TotalHeight > this.BodyHeight;
			bool horizontal = this.HasHorizontalBar && position.Y < bodyBottom && position.X < this.ViewWidth * s;
			return vertical || horizontal;
		}

		/// <summary>The column whose resizable right edge is under <paramref name="position"/> in the header, or -1.</summary>
		private int ResizeEdgeAt(Vector2 position)
		{
			double s = this.Scale;
			if (!this.ResizableColumns || position.Y < this.Height - this.HeaderHeight * s || position.X >= this.ViewWidth * s)
			{
				return -1;
			}

			double contentX = position.X / s + this.horizontalOffset;
			double edge = 0;
			for (int column = 0; column + 1 < this.widths.Length; column++)
			{
				edge += this.widths[column];
				if (this.columns[column].Resizable && Math.Abs(contentX - edge) <= ResizeGrabHalfWidth)
				{
					return column;
				}
			}

			return -1;
		}

		private int ColumnAt(double contentX)
		{
			double x = 0;
			for (int column = 0; column < this.widths.Length; column++)
			{
				x += this.widths[column];
				if (contentX < x)
				{
					return column;
				}
			}

			return -1;
		}

		private double[] UpdateWidths()
		{
			this.widths = TableColumn.DistributeWidths(this.columns, this.ViewWidth, this.ColumnOverrides);
			return this.widths;
		}
	}
}
