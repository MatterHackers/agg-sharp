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
using MatterHackers.VectorMath;

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Interaction
{
	/// <summary>
	/// agg-gui's DragAndDropWidget (interaction/drag_and_drop.rs, egui's drag_and_drop demo): three columns of
	/// items. Press an item and move past <see cref="DragThreshold"/> to lift it; a ghost follows the cursor, the
	/// column under it tints and a line marks where it will land; release to move it there. Geometry is agg-gui's
	/// logical pixels scaled by DeviceScale, Y-up from the widget's bottom.
	/// </summary>
	public class DragAndDropBoard : GuiWidget
	{
		/// <summary>How far (logical pixels) the cursor must move from the press before a click becomes a drag.</summary>
		public const double DragThreshold = 4;

		public static readonly string[] ColumnLabels = { "Column A", "Column B", "Column C" };

		private const double HeaderHeight = 26;
		private const double ItemHeight = 26;
		private const double ItemGap = 3;
		private const double Pad = 5;
		private const double ColumnGap = 8;
		private const double GhostWidth = 100;

		private readonly DemoTheme demoTheme;
		private Vector2 pressPosition;
		private Vector2 cursor;

		public DragAndDropBoard(DemoTheme demoTheme)
		{
			this.demoTheme = demoTheme;
		}

		/// <summary>The items of each column, top to bottom.</summary>
		public List<List<string>> Columns { get; } = new List<List<string>>
		{
			new List<string> { "Item A", "Item B", "Item C", "Item D" },
			new List<string> { "Item E", "Item F", "Item G" },
			new List<string> { "Item H", "Item I", "Item J", "Item K" },
		};

		/// <summary>True once the pressed item has moved past <see cref="DragThreshold"/>.</summary>
		public bool DragActive { get; private set; }

		/// <summary>The (column, row) pressed, or null.</summary>
		public (int Column, int Row)? DragSource { get; private set; }

		/// <summary>The column and insertion row (0 = before all) the lifted item would land at, or null.</summary>
		public (int Column, int Row)? DropTarget { get; private set; }

		/// <summary>The item under the cursor while nothing is pressed, or null.</summary>
		public (int Column, int Row)? Hovered { get; private set; }

		private static double S => DeviceScale;

		private Color Accent => DemoTheme.ColorOf(this.demoTheme.Accent);

		/// <summary>The Y-up bottom of the <paramref name="index"/>th visible item in a column <paramref name="height"/> tall.</summary>
		public static double ItemBottom(double height, int index) =>
			height - ((HeaderHeight + ((index + 1) * ItemHeight) + (index * ItemGap)) * S);

		/// <summary>The insertion row for a cursor at <paramref name="y"/>: before the first item whose middle is below it.</summary>
		public static int InsertRow(double y, double height, int count)
		{
			for (int i = 0; i < count; i++)
			{
				if (y > ItemBottom(height, i) + (ItemHeight * S * 0.5))
				{
					return i;
				}
			}

			return count;
		}

		/// <summary>Column <paramref name="column"/>'s rectangle: the width split evenly less the gaps.</summary>
		public RectangleDouble ColumnBounds(int column)
		{
			int n = this.Columns.Count;
			double width = (this.Width - (ColumnGap * S * (n - 1))) / n;
			double x = column * (width + (ColumnGap * S));
			return new RectangleDouble(x, 0, x + width, this.Height);
		}

		/// <summary>The item at <paramref name="position"/>, or null outside every item.</summary>
		public (int Column, int Row)? ItemAt(Vector2 position)
		{
			for (int c = 0; c < this.Columns.Count; c++)
			{
				RectangleDouble column = this.ColumnBounds(c);
				if (position.X < column.Left + (Pad * S) || position.X > column.Right - (Pad * S))
				{
					continue;
				}

				for (int i = 0; i < this.Columns[c].Count; i++)
				{
					double bottom = ItemBottom(this.Height, i);
					if (position.Y >= bottom && position.Y <= bottom + (ItemHeight * S))
					{
						return (c, i);
					}
				}
			}

			return null;
		}

		/// <summary>The column under <paramref name="position"/> and the row an item dropped there would take, or null.</summary>
		public (int Column, int Row)? DropTargetAt(Vector2 position)
		{
			for (int c = 0; c < this.Columns.Count; c++)
			{
				RectangleDouble column = this.ColumnBounds(c);
				if (position.X >= column.Left && position.X <= column.Right)
				{
					return (c, InsertRow(position.Y, this.Height, this.VisibleCount(c)));
				}
			}

			return null;
		}

		/// <summary>
		/// Moves the item at <paramref name="source"/> to insertion row <paramref name="target"/>. Within one column
		/// a target below the source counts the lifted item, so it shifts up by one once that item is removed.
		/// </summary>
		public void Move((int Column, int Row) source, (int Column, int Row) target)
		{
			(int sc, int sr) = source;
			(int tc, int tr) = target;
			if (sc >= this.Columns.Count || sr >= this.Columns[sc].Count || tc >= this.Columns.Count)
			{
				return;
			}

			if (sc == tc && sr < tr)
			{
				tr--;
			}

			string item = this.Columns[sc][sr];
			this.Columns[sc].RemoveAt(sr);
			this.Columns[tc].Insert(Math.Min(tr, this.Columns[tc].Count), item);
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			for (int c = 0; c < this.Columns.Count; c++)
			{
				this.DrawColumn(graphics2D, c, this.ColumnBounds(c));
			}

			if (this.DragActive && this.DragSource is (int sc, int sr) && sr < this.Columns[sc].Count)
			{
				// The lifted item follows the cursor, translucent over the columns.
				DemoPalette palette = this.demoTheme.Palette;
				double w = GhostWidth * S;
				double h = ItemHeight * S;
				double x = this.cursor.X - (w / 2);
				double y = this.cursor.Y - (h / 2);
				var ghost = new RoundedRect(x, y, x + w, y + h, 4 * S);
				graphics2D.Render(ghost, palette.WidgetBackground.WithAlpha(217));
				graphics2D.Render(new Stroke(ghost, 1.5 * S), this.Accent);
				graphics2D.DrawString("≡  " + this.Columns[sc][sr], x + (8 * S), y + (h * 0.35) + (4 * S), this.Points(12), color: palette.TextColor);
			}

			base.OnDraw(graphics2D);
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			if (mouseEvent.Button == MouseButtons.Left)
			{
				this.pressPosition = mouseEvent.Position;
				this.cursor = mouseEvent.Position;
				this.DragActive = false;
				this.DragSource = this.ItemAt(mouseEvent.Position);
				this.DropTarget = this.DragSource;
				if (this.DragSource != null)
				{
					this.Invalidate();
				}
			}

			base.OnMouseDown(mouseEvent);
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			this.cursor = mouseEvent.Position;
			if (this.DragSource != null)
			{
				if (!this.DragActive && (mouseEvent.Position - this.pressPosition).Length >= DragThreshold * S)
				{
					this.DragActive = true;
				}

				if (this.DragActive)
				{
					this.DropTarget = this.DropTargetAt(mouseEvent.Position);
				}

				this.Invalidate();
			}
			else
			{
				var hovered = this.ItemAt(mouseEvent.Position);
				if (hovered != this.Hovered)
				{
					this.Hovered = hovered;
					this.Invalidate();
				}
			}

			// As agg-gui: the closed hand once a drag is under way, the open hand over anything that can be picked up.
			this.Cursor = this.DragActive ? Cursors.Grabbing : this.DragSource != null || this.Hovered != null ? Cursors.Grab : Cursors.Default;
			base.OnMouseMove(mouseEvent);
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			this.cursor = mouseEvent.Position;
			if (this.DragActive && this.DragSource is (int, int) source && this.DropTargetAt(mouseEvent.Position) is (int, int) target)
			{
				this.Move(source, target);
			}

			if (this.DragActive || this.DragSource != null)
			{
				this.Invalidate();
			}

			this.DragActive = false;
			this.DragSource = null;
			this.DropTarget = null;
			base.OnMouseUp(mouseEvent);
		}

		public override void OnMouseLeaveBounds(MouseEventArgs mouseEvent)
		{
			if (this.Hovered != null)
			{
				this.Hovered = null;
				this.Invalidate();
			}

			base.OnMouseLeaveBounds(mouseEvent);
		}

		/// <summary>agg-gui's pixel em <paramref name="aggGuiSize"/> as points at the device scale.</summary>
		private double Points(double aggGuiSize) => DemoText.Points(aggGuiSize) * S;

		/// <summary>The rows a column shows: all but the lifted item while it is being dragged.</summary>
		private int VisibleCount(int column)
		{
			int count = this.Columns[column].Count;
			return this.DragActive && this.DragSource?.Column == column ? Math.Max(0, count - 1) : count;
		}

		private void DrawColumn(Graphics2D graphics2D, int c, RectangleDouble r)
		{
			DemoPalette palette = this.demoTheme.Palette;
			double h = r.Height;
			var outline = new RoundedRect(r, 6 * S);
			graphics2D.Render(outline, palette.PanelFill);
			if (this.DragActive && this.DropTarget?.Column == c)
			{
				graphics2D.Render(outline, this.Accent.WithAlpha(20));
			}

			graphics2D.Render(new Stroke(outline, S), palette.WidgetStroke);
			graphics2D.DrawString(ColumnLabels[c], r.Left + ((Pad + 2) * S), r.Top - (((HeaderHeight * 0.5) + 5) * S), this.Points(11), color: palette.TextDim);

			// agg-gui's widget_bg_hovered, which DemoPalette does not carry.
			Color hoveredFill = palette.IsDark ? DemoPalette.Rgb(0.28, 0.28, 0.33) : DemoPalette.Rgb(0.92, 0.93, 0.95);
			int visual = 0;
			for (int i = 0; i < this.Columns[c].Count; i++)
			{
				// The lifted item is drawn as the ghost instead.
				if (this.DragActive && this.DragSource == (c, i))
				{
					continue;
				}

				double bottom = ItemBottom(h, visual++);
				if (bottom + (ItemHeight * S) < 0)
				{
					continue;
				}

				bool hovered = !this.DragActive && this.Hovered == (c, i);
				var item = new RoundedRect(r.Left + (Pad * S), bottom, r.Right - (Pad * S), bottom + (ItemHeight * S), 4 * S);
				graphics2D.Render(item, hovered ? hoveredFill : palette.WidgetBackground);
				graphics2D.DrawString("≡  " + this.Columns[c][i], r.Left + ((Pad + 8) * S), bottom + (((ItemHeight * 0.35) + 4) * S), this.Points(12.5), color: palette.TextColor);
			}

			if (this.DragActive && this.DropTarget is (int tc, int tr) && tc == c)
			{
				int count = this.VisibleCount(c);
				double lineY = tr == 0 ? ItemBottom(h, 0) + (ItemHeight * S)
					: tr >= count ? ItemBottom(h, Math.Max(0, count - 1))
					: ItemBottom(h, tr) + ((ItemHeight + (ItemGap * 0.5)) * S);
				graphics2D.Line(r.Left + (Pad * S), lineY, r.Right - (Pad * S), lineY, palette.TextColor, 2 * S);
			}
		}
	}
}
