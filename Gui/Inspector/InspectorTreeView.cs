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
using MatterHackers.Agg.Font;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// The inspector's widget tree: one row per <see cref="InspectorModel.VisibleRows"/> entry, indented by depth,
	/// with a disclosure triangle on rows that have children. Hovering a row hovers its node (the app shows the
	/// highlight), clicking selects it, and clicking the triangle expands or collapses it. The wheel scrolls.
	/// </summary>
	/// <remarks>
	/// The rows are painted, not built as widgets, as agg-gui's inspector paints its TreeView: an app's tree runs to
	/// thousands of widgets, and a widget per row would also put the inspector's own rows into the tree it shows.
	/// </remarks>
	public class InspectorTreeView : GuiWidget
	{
		/// <summary>agg-gui's tree row height, in logical units.</summary>
		public const double LogicalRowHeight = 20;

		/// <summary>Indent per depth level, in logical units.</summary>
		public const double LogicalIndent = 12;

		private const double LogicalTriangleWidth = 14;

		private readonly InspectorModel model;

		private List<InspectorNode> rows = new List<InspectorNode>();

		private double scrollOffset;

		public InspectorTreeView(InspectorModel model, InspectorStyle style)
		{
			this.model = model;
			this.Style = style;
			this.Name = "Inspector Tree";
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Stretch;
			model.Changed += this.Model_Changed;
			this.rows = model.VisibleRows();
		}

		public InspectorStyle Style { get; set; }

		/// <summary>The rows shown, top to bottom.</summary>
		public IReadOnlyList<InspectorNode> Rows => this.rows;

		public double RowHeight => LogicalRowHeight * DeviceScale;

		/// <summary>How far the rows are scrolled up, in device pixels (0 shows the first row at the top).</summary>
		public double ScrollOffset
		{
			get => this.scrollOffset;
			set
			{
				double clamped = Math.Max(0, Math.Min(value, this.rows.Count * this.RowHeight - this.Height));
				if (clamped != this.scrollOffset)
				{
					this.scrollOffset = clamped;
					this.Invalidate();
				}
			}
		}

		/// <summary>Row <paramref name="index"/>'s rectangle in this widget's coordinates (y up; may lie outside the
		/// widget when scrolled away).</summary>
		public RectangleDouble RowBounds(int index)
		{
			double top = this.Height + this.scrollOffset - index * this.RowHeight;
			return new RectangleDouble(0, top - this.RowHeight, this.Width, top);
		}

		/// <summary>The row at <paramref name="y"/> (local, y up), or -1 when there is none.</summary>
		public int RowIndexAt(double y)
		{
			if (y < 0 || y > this.Height)
			{
				return -1;
			}

			int index = (int)Math.Floor((this.Height + this.scrollOffset - y) / this.RowHeight);
			return index >= 0 && index < this.rows.Count ? index : -1;
		}

		/// <summary>The right edge of <paramref name="node"/>'s disclosure triangle; a click left of it toggles.</summary>
		public double TriangleRight(InspectorNode node) => (4 + node.Depth * LogicalIndent + LogicalTriangleWidth) * DeviceScale;

		public override void OnDraw(Graphics2D graphics2D)
		{
			double scale = DeviceScale;
			double pointSize = 9 * scale;
			int first = Math.Max(0, (int)Math.Floor(this.scrollOffset / this.RowHeight));
			for (int i = first; i < this.rows.Count; i++)
			{
				RectangleDouble rowBounds = this.RowBounds(i);
				if (rowBounds.Top < 0)
				{
					break;
				}

				InspectorNode node = this.rows[i];
				if (node == this.model.Selected)
				{
					graphics2D.FillRectangle(rowBounds, this.Style.SelectedRow);
				}
				else if (node == this.model.Hovered)
				{
					graphics2D.FillRectangle(rowBounds, this.Style.HoveredRow);
				}

				double x = (4 + node.Depth * LogicalIndent) * scale;
				double centerY = rowBounds.Center.Y;
				if (node.ChildCount > 0)
				{
					DrawTriangle(graphics2D, x + LogicalTriangleWidth / 2 * scale, centerY, 3.5 * scale, this.model.IsExpanded(node), this.Style.DimText);
				}

				Color text = node.Visible && node.Enabled ? this.Style.Text : this.Style.DimText;
				graphics2D.DrawString(node.Label, x + LogicalTriangleWidth * scale, centerY, pointSize, Justification.Left, Baseline.BoundsCenter, text);
			}

			base.OnDraw(graphics2D);
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			this.model.SetHovered(this.NodeAt(mouseEvent.Y));
			base.OnMouseMove(mouseEvent);
		}

		public override void OnMouseLeaveBounds(MouseEventArgs mouseEvent)
		{
			this.model.SetHovered(null);
			base.OnMouseLeaveBounds(mouseEvent);
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			InspectorNode node = this.NodeAt(mouseEvent.Y);
			if (node != null && mouseEvent.Button == MouseButtons.Left)
			{
				if (node.ChildCount > 0 && (mouseEvent.X < this.TriangleRight(node) || mouseEvent.Clicks > 1))
				{
					this.model.SetExpanded(node, !this.model.IsExpanded(node));
				}
				else
				{
					this.model.Select(node);
				}

				// the arrow keys drive the tree after a click, as agg-gui's TreeView takes focus
				this.Focus();
			}

			base.OnMouseDown(mouseEvent);
		}

		/// <summary>
		/// agg-gui's TreeView keys, with the selection as its cursor: up and down move it a row, right expands a
		/// collapsed row or moves down one, left collapses an expanded row or moves to the parent, and enter or space
		/// toggle the row's expansion.
		/// </summary>
		public override void OnKeyDown(KeyEventArgs keyEvent)
		{
			InspectorNode cursor = this.model.Selected;
			bool cursorShown = cursor != null && this.rows.Contains(cursor);
			switch (keyEvent.KeyCode)
			{
				case Keys.Down:
					this.MoveCursor(1);
					break;

				case Keys.Up:
					this.MoveCursor(-1);
					break;

				case Keys.Right:
					if (cursorShown && cursor.ChildCount > 0 && !this.model.IsExpanded(cursor))
					{
						this.model.SetExpanded(cursor, true);
					}
					else if (cursorShown)
					{
						this.MoveCursor(1);
					}

					break;

				case Keys.Left:
					// a leaf counts as expanded (every node starts open), so only a row with children collapses
					if (cursor != null && cursor.ChildCount > 0 && this.model.IsExpanded(cursor))
					{
						this.model.SetExpanded(cursor, false);
					}
					else if (this.model.ParentOf(cursor) is InspectorNode parent)
					{
						this.model.Select(parent);
						this.ScrollToRow(this.rows.IndexOf(parent));
					}

					break;

				case Keys.Enter:
				case Keys.Space:
					if (cursorShown && cursor.ChildCount > 0)
					{
						this.model.SetExpanded(cursor, !this.model.IsExpanded(cursor));
					}

					break;

				default:
					base.OnKeyDown(keyEvent);
					return;
			}

			keyEvent.Handled = true;
			keyEvent.SuppressKeyPress = true;
			base.OnKeyDown(keyEvent);
		}

		/// <summary>Scrolls the least that shows row <paramref name="index"/> whole.</summary>
		public void ScrollToRow(int index)
		{
			if (index < 0 || index >= this.rows.Count)
			{
				return;
			}

			RectangleDouble row = this.RowBounds(index);
			if (row.Top > this.Height)
			{
				this.ScrollOffset = index * this.RowHeight;
			}
			else if (row.Bottom < 0)
			{
				this.ScrollOffset = (index + 1) * this.RowHeight - this.Height;
			}
		}

		/// <summary>agg-gui's move_cursor: <paramref name="delta"/> rows from the selection (from the first row when
		/// none is shown), clamped to the rows, selected and scrolled into view.</summary>
		private void MoveCursor(int delta)
		{
			if (this.rows.Count == 0)
			{
				return;
			}

			int current = Math.Max(0, this.rows.IndexOf(this.model.Selected));
			int next = Math.Max(0, Math.Min(current + delta, this.rows.Count - 1));
			this.model.Select(this.rows[next]);
			this.ScrollToRow(next);
		}

		public override void OnMouseWheel(MouseEventArgs mouseEvent)
		{
			this.ScrollOffset -= mouseEvent.WheelDelta / 120.0 * 3 * this.RowHeight;
			mouseEvent.WheelDelta = 0;
			base.OnMouseWheel(mouseEvent);
		}

		public override void OnBoundsChanged(EventArgs e)
		{
			this.ScrollOffset = this.scrollOffset;
			base.OnBoundsChanged(e);
		}

		public override void OnClosed(EventArgs e)
		{
			this.model.Changed -= this.Model_Changed;
			base.OnClosed(e);
		}

		private static void DrawTriangle(Graphics2D graphics2D, double centerX, double centerY, double size, bool expanded, Color color)
		{
			var triangle = new VertexStorage();
			if (expanded)
			{
				// pointing down
				triangle.MoveTo(centerX - size, centerY + size / 2);
				triangle.LineTo(centerX + size, centerY + size / 2);
				triangle.LineTo(centerX, centerY - size / 2);
			}
			else
			{
				// pointing right
				triangle.MoveTo(centerX - size / 2, centerY + size);
				triangle.LineTo(centerX + size / 2, centerY);
				triangle.LineTo(centerX - size / 2, centerY - size);
			}

			triangle.ClosePolygon();
			graphics2D.Render(triangle, color);
		}

		private InspectorNode NodeAt(double y)
		{
			int index = this.RowIndexAt(y);
			return index < 0 ? null : this.rows[index];
		}

		private void Model_Changed(object sender, EventArgs e)
		{
			this.rows = this.model.VisibleRows();
			this.ScrollOffset = this.scrollOffset;
			this.Invalidate();
		}
	}
}
