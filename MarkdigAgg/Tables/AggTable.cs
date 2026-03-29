// Copyright (c) 2026, Nicolas Musset, John Lewin, Lars Brubaker
// This file is licensed under the MIT license.
// See the LICENSE.md file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using Markdig.Extensions.Tables;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;

namespace Markdig.Renderers.Agg
{
	public class AggTable : FlowLayoutWidget
	{
		private bool inLayout;

		public List<AggTableColumn> Columns { get; }

		public List<AggTableRow> Rows { get; }

		public List<HorizontalLine> HorizontalRules { get; } = new List<HorizontalLine>();

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

		public override void OnLayout(LayoutEventArgs layoutEventArgs)
		{
			if (inLayout)
			{
				return;
			}

			inLayout = true;
			try
			{
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

						// Expand columns to fill container width, divided equally.
						// Each cell has a 1px left border; the last cell also has 1px right.
						double totalBorderWidth = this.Columns.Count + 1;
						double containerSharePerColumn = (this.Width - totalBorderWidth) / this.Columns.Count;

						double targetWidth = System.Math.Max(maxColumnWidth, containerSharePerColumn);

						foreach (var column in this.Columns)
						{
							column.SetCellWidths(targetWidth);
						}
					}
				}

				var rowWidth = (this.Rows ?? new List<AggTableRow>())
					.Where(row => row.Cells.Count > 0)
					.Select(row => row.Cells.Sum(cell => cell.Width))
					.DefaultIfEmpty(0)
					.Max();

				foreach (var rule in this.HorizontalRules)
				{
					rule.HAnchor = HAnchor.Left;
					rule.Margin = new BorderDouble(left: 9);
					rule.Width = rowWidth + 2;
				}

				// Re-run layout after columns have real measured widths.
				base.OnLayout(layoutEventArgs);
			}
			finally
			{
				inLayout = false;
			}
		}
	}
}
