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

namespace MatterHackers.Agg.UI
{
	/// <summary>How a <see cref="TableColumn"/> takes its width.</summary>
	public enum TableColumnSizing
	{
		/// <summary>Starts at <see cref="TableColumn.Width"/> (agg-gui's Auto).</summary>
		Auto,

		/// <summary>Always <see cref="TableColumn.Width"/> unless the user resizes it.</summary>
		Exact,

		/// <summary>Shares what the fixed columns leave, never below <see cref="TableColumn.AtLeast"/>.</summary>
		Remainder,
	}

	/// <summary>One column of a <see cref="VirtualTable"/>, in design units (agg-gui's TableColumn).</summary>
	public class TableColumn
	{
		/// <summary>The narrowest any column can be resized to.</summary>
		public const double MinimumWidth = 16;

		public TableColumnSizing Sizing { get; set; }

		/// <summary>The width of an Auto or Exact column.</summary>
		public double Width { get; set; }

		/// <summary>The narrowest a Remainder column gets.</summary>
		public double AtLeast { get; set; } = 16;

		/// <summary>Whether the cell painter should clip its text to the column (the table always clips drawing).</summary>
		public bool Clip { get; set; }

		/// <summary>Whether the header edge right of this column can be dragged.</summary>
		public bool Resizable { get; set; }

		public static TableColumn Auto(double width, bool resizable = false) => new TableColumn { Sizing = TableColumnSizing.Auto, Width = width, Resizable = resizable };

		public static TableColumn Exact(double width, bool resizable = false) => new TableColumn { Sizing = TableColumnSizing.Exact, Width = width, Resizable = resizable };

		public static TableColumn Remainder(double atLeast = 16, bool clip = false, bool resizable = false)
			=> new TableColumn { Sizing = TableColumnSizing.Remainder, AtLeast = atLeast, Clip = clip, Resizable = resizable };

		/// <summary>
		/// Shares <paramref name="totalWidth"/> between <paramref name="columns"/>: a column with an override (a width
		/// the user dragged it to) keeps it, Auto and Exact columns keep their width, and the Remainder columns split
		/// what is left evenly, each at least its <see cref="AtLeast"/>. The result can be wider than
		/// <paramref name="totalWidth"/>; the table then scrolls sideways.
		/// </summary>
		public static double[] DistributeWidths(IReadOnlyList<TableColumn> columns, double totalWidth, IReadOnlyList<double?> overrides)
		{
			var widths = new double[columns.Count];
			var remainders = new List<int>();
			double remainderMinimum = 0;
			double fixedTotal = 0;
			for (int i = 0; i < columns.Count; i++)
			{
				double? pinned = overrides != null && i < overrides.Count ? overrides[i] : null;
				if (pinned.HasValue)
				{
					widths[i] = Math.Max(pinned.Value, MinimumWidth);
					fixedTotal += widths[i];
				}
				else if (columns[i].Sizing == TableColumnSizing.Remainder)
				{
					remainders.Add(i);
					remainderMinimum += columns[i].AtLeast;
				}
				else
				{
					widths[i] = columns[i].Width;
					fixedTotal += widths[i];
				}
			}

			if (remainders.Count > 0)
			{
				double each = Math.Max(totalWidth - fixedTotal, remainderMinimum) / remainders.Count;
				foreach (int i in remainders)
				{
					widths[i] = Math.Max(each, columns[i].AtLeast);
				}
			}

			return widths;
		}
	}
}
