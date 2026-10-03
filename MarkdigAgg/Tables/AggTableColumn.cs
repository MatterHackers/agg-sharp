// Copyright (c) 2026, Nicolas Musset, John Lewin, Lars Brubaker
// This file is licensed under the MIT license.
// See the LICENSE.md file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using Markdig.Extensions.Tables;

namespace Markdig.Renderers.Agg
{
	public class AggTableColumn
	{
		private TableColumnDefinition ColumnDefinition;

		public AggTableColumn(TableColumnDefinition definition)
		{
			this.ColumnDefinition = definition;
		}

		public List<AggTableCell> Cells { get; } = new List<AggTableCell>();

		/// <summary>
		/// The current width applied to this column's cells.
		/// </summary>
		public double CellWidth { get; private set; }

		/// <summary>
		/// Size all cells to fit the widest content.
		/// </summary>
		public void SetCellWidths()
		{
			double cellPadding = 10;

			if (this.Cells.Count == 0)
			{
				return;
			}

			// Whole pixels, so the table's vertical grid lines land on pixel boundaries.
			double maxCellWidth = System.Math.Ceiling(this.Cells.Select(c => c.ContentWidth).Max() + cellPadding * 2);
			SetCellWidths(maxCellWidth);
		}

		/// <summary>
		/// Set all cells to the specified width.
		/// </summary>
		public void SetCellWidths(double width)
		{
			CellWidth = width;

			foreach (var cell in this.Cells)
			{
				if (cell.Width != width)
				{
					cell.Width = width;
				}
			}
		}
	}
}
