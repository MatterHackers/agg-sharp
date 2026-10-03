// Copyright (c) 2016-2017 Nicolas Musset. All rights reserved.
// Copyright (c) 2026, John Lewin, Lars Brubaker
// This file is licensed under the MIT license.
// See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Markdig.Renderers.Agg.Inlines;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;

namespace Markdig.Renderers.Agg
{
	public class AggTableRow : FlowLayoutWidget
	{
		public AggTableRow()
		{
			this.Margin = new BorderDouble(10, 0);
			this.VAnchor = VAnchor.Absolute;
			this.Height = 25;
		}

		public bool IsHeadingRow { get; set; }

		/// <summary>
		/// The zebra stripe behind this row's cells (transparent for none). The owning AggTable paints it between
		/// its grid lines; a row background would also cover the line gaps and tint the lines.
		/// </summary>
		public Color StripeColor { get; set; } = Color.Transparent;

		public List<AggTableCell> Cells { get; } = new List<AggTableCell>();
		public double RowHeight { get; private set; }

		// Override AddChild to push styles to child elements when table rows are resolved to the tree
		public override GuiWidget AddChild(GuiWidget childToAdd, int indexInChildrenList = -1)
		{
			if (!this.IsHeadingRow)
			{
				return base.AddChild(childToAdd, indexInChildrenList);
			}

			if (childToAdd is TextWidget textWidget)
			{
				textWidget.Bold = true;
			}
			else if (childToAdd is TextLinkX textLink)
			{
				foreach (var childTextWidget in childToAdd.Children.OfType<TextWidget>())
				{
					childTextWidget.Bold = true;
				}
			}
			else
			{
				foreach (var childTextWidget in childToAdd.Descendants<TextWidget>())
				{
					childTextWidget.Bold = true;
				}
			}

			return base.AddChild(childToAdd, indexInChildrenList);
		}

		internal void CellHeightChanged(double newHeight)
		{
			double cellPadding = 2;
			// Whole pixels, so the table's rules land on pixel boundaries between rows.
			double height = Math.Ceiling(newHeight + 2 * cellPadding);

			// We need the row to be as tall as the tallest cell
			double maxCellHeight = height;
			foreach (var cell in this.Cells)
			{
				if (cell.Children.Count > 0 && cell.Children.First() is FlowLeftRightWithWrapping wrappedChild)
				{
					maxCellHeight = Math.Max(maxCellHeight, Math.Ceiling(wrappedChild.Height + 2 * cellPadding));
				}
			}

			if (this.RowHeight != maxCellHeight)
			{
				foreach (var cell in this.Cells)
				{
					using (cell.LayoutLock())
					{
						cell.Height = maxCellHeight;
					}
				}

				using (this.LayoutLock())
				{
					this.Height = this.RowHeight = maxCellHeight;
				}
			}
		}
	}
}
