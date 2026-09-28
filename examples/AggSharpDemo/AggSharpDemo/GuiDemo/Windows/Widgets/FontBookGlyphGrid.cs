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
using System.Text;
using MatterHackers.Agg;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Widgets
{
	/// <summary>
	/// The Font Book's glyph grid, agg-gui's GlyphGrid (demo-ui/src/windows/font_book/glyph_grid.rs): every
	/// code point <see cref="TypeFace"/> has, narrowed by <see cref="Filter"/>, as a wrapping grid of cells.
	/// Virtualised as agg-gui's is, but by drawing rather than by a widget pool: the grid is one widget as tall
	/// as all its rows, and each draw paints only the rows its scroll viewport shows, so a font of thousands of
	/// glyphs costs a screenful. Hovering a cell shows its tooltip; clicking one selects it and copies it.
	/// </summary>
	public class FontBookGlyphGrid : GuiWidget
	{
		/// <summary>agg-gui's CELL: a cell's edge, in device-independent units.</summary>
		public const double CellSize = 26;

		/// <summary>agg-gui's GAP between cells.</summary>
		public const double CellGap = 3;

		/// <summary>agg-gui's GLYPH_SIZE (18 pixels) in agg-sharp points.</summary>
		public const double GlyphPointSize = 18 * 72.0 / 96;

		public const string NoMatchText = "No glyphs match the current filter.";

		private readonly ThemeConfig theme;
		private TypeFace typeFace;
		private string filter = string.Empty;
		private IReadOnlyList<int> allCodePoints = Array.Empty<int>();
		private int hoveredIndex = -1;

		public FontBookGlyphGrid(ThemeConfig theme, TypeFace typeFace)
		{
			this.theme = theme;
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Absolute;
			this.TypeFace = typeFace;
		}

		/// <summary>Raised when a click selects (and copies) a glyph.</summary>
		public event EventHandler SelectionChanged;

		/// <summary>The face whose glyphs are shown. Changing it lists its code points again and clears the selection.</summary>
		public TypeFace TypeFace
		{
			get => this.typeFace;
			set
			{
				if (value != this.typeFace)
				{
					this.typeFace = value;
					this.allCodePoints = value.CodePoints().Where(Browsable).ToList();
					this.SelectedCodePoint = null;
					this.ApplyFilter();
				}
			}
		}

		/// <summary>Narrows the grid to glyphs containing this text or whose hex code point contains it (agg-gui's matches).</summary>
		public string Filter
		{
			get => this.filter;
			set
			{
				value = value?.Trim() ?? string.Empty;
				if (value != this.filter)
				{
					this.filter = value;
					this.ApplyFilter();
				}
			}
		}

		/// <summary>How many glyphs the face has, whitespace and control characters aside (agg-gui's count).</summary>
		public int CharacterCount => this.allCodePoints.Count;

		/// <summary>The glyphs shown, in code point order.</summary>
		public IReadOnlyList<int> Shown { get; private set; } = Array.Empty<int>();

		public int? SelectedCodePoint { get; private set; }

		/// <summary>How many cells the last draw painted: a screenful, however many glyphs there are.</summary>
		public int CellsDrawn { get; private set; }

		private double Pitch => (CellSize + CellGap) * DeviceScale;

		/// <summary>Cells per row at the grid's width.</summary>
		public int Columns => Math.Max(1, (int)Math.Floor((this.Width + CellGap * DeviceScale) / this.Pitch));

		public static string Text(int codePoint) => char.ConvertFromUtf32(codePoint);

		public static string Hex(int codePoint) => codePoint.ToString("X4");

		/// <summary>agg-gui's GlyphGrid::matches: the character itself or its hex code point, either case.</summary>
		public static bool Matches(int codePoint, string filter)
		{
			return string.IsNullOrEmpty(filter)
				|| Text(codePoint).Contains(filter, StringComparison.OrdinalIgnoreCase)
				|| Hex(codePoint).Contains(filter.ToUpperInvariant(), StringComparison.Ordinal);
		}

		/// <summary>agg-gui's glyph_tooltip: the character, its hex code point and its advance.</summary>
		public string Tooltip(int codePoint)
		{
			double advance = new TypeFacePrinter(Text(codePoint), new StyledTypeFace(this.typeFace, GlyphPointSize)).GetSize().X;
			return $"{Text(codePoint)}\nHex: U+{Hex(codePoint)}\nAdvance: {advance:0.0} px";
		}

		/// <summary>The cell at <paramref name="index"/> in <see cref="Shown"/>, in the grid's coordinates.</summary>
		public RectangleDouble CellBounds(int index)
		{
			int columns = this.Columns;
			double left = (index % columns) * this.Pitch;
			double top = this.Height - (index / columns) * this.Pitch;
			return new RectangleDouble(left, top - CellSize * DeviceScale, left + CellSize * DeviceScale, top);
		}

		/// <summary>The index in <see cref="Shown"/> of the cell under <paramref name="position"/>, or -1 (gaps included).</summary>
		public int IndexAt(Vector2 position)
		{
			int column = (int)Math.Floor(position.X / this.Pitch);
			int row = (int)Math.Floor((this.Height - position.Y) / this.Pitch);
			if (column < 0 || column >= this.Columns || row < 0)
			{
				return -1;
			}

			int index = row * this.Columns + column;
			return index < this.Shown.Count && this.CellBounds(index).Contains(position) ? index : -1;
		}

		public override void OnBoundsChanged(EventArgs e)
		{
			this.FitHeight();
			base.OnBoundsChanged(e);
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			this.CellsDrawn = 0;
			if (this.Shown.Count == 0)
			{
				graphics2D.DrawString(NoMatchText, 0, this.Height / 2, this.theme.DefaultFontSize, baseline: Baseline.BoundsCenter, color: this.theme.TextColor);
				base.OnDraw(graphics2D);
				return;
			}

			// Only the rows the scroll viewport shows (all of them with no scroll viewport around the grid).
			RectangleDouble visible = this.LocalBounds;
			if (this.Parents<ScrollableWidget>().FirstOrDefault() is ScrollableWidget scroll)
			{
				visible.IntersectWithRectangle(this.TransformFromParentSpace(scroll, scroll.LocalBounds));
			}

			int columns = this.Columns;
			int firstRow = Math.Max(0, (int)Math.Floor((this.Height - visible.Top) / this.Pitch));
			int lastRow = (int)Math.Ceiling((this.Height - visible.Bottom) / this.Pitch);
			int end = Math.Min(this.Shown.Count, (lastRow + 1) * columns);
			var styledTypeFace = new StyledTypeFace(this.typeFace, GlyphPointSize * DeviceScale);
			double radius = 4 * DeviceScale;
			for (int index = firstRow * columns; index < end; index++)
			{
				RectangleDouble cell = this.CellBounds(index);
				int codePoint = this.Shown[index];
				if (codePoint == this.SelectedCodePoint)
				{
					graphics2D.Render(new RoundedRect(cell, radius), this.theme.PrimaryAccentColor.WithAlpha(90));
				}
				else if (index == this.hoveredIndex)
				{
					// egui's frameless buttons: a surface only under the mouse, so the resting grid stays flat.
					graphics2D.Render(new RoundedRect(cell, radius), this.theme.SlightShade);
				}

				new TypeFacePrinter(Text(codePoint), styledTypeFace, cell.Center, Justification.Center, Baseline.BoundsCenter)
					.Render(graphics2D, this.theme.TextColor);
				this.CellsDrawn++;
			}

			base.OnDraw(graphics2D);
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			this.SetHovered(this.IndexAt(mouseEvent.Position));
			base.OnMouseMove(mouseEvent);
		}

		public override void OnMouseLeave(MouseEventArgs mouseEvent)
		{
			this.SetHovered(-1);
			base.OnMouseLeave(mouseEvent);
		}

		/// <summary>Selects <paramref name="codePoint"/> and copies it to the clipboard, as a click on its cell does.</summary>
		public void Select(int codePoint)
		{
			this.SelectedCodePoint = codePoint;
			Clipboard.Instance?.SetText(Text(codePoint));
			this.Invalidate();
			this.SelectionChanged?.Invoke(this, EventArgs.Empty);
		}

		protected override void OnClick(MouseEventArgs mouseEvent)
		{
			int index = this.IndexAt(mouseEvent.Position);
			if (index >= 0)
			{
				this.Select(this.Shown[index]);
			}

			base.OnClick(mouseEvent);
		}

		/// <summary>agg-gui lists what the font can draw visibly: whitespace and control characters are left out.</summary>
		private static bool Browsable(int codePoint)
		{
			return Rune.IsValid(codePoint)
				&& !Rune.IsWhiteSpace(new Rune(codePoint))
				&& !Rune.IsControl(new Rune(codePoint));
		}

		private void ApplyFilter()
		{
			this.Shown = this.allCodePoints.Where(c => Matches(c, this.filter)).ToList();
			this.SetHovered(-1);
			this.FitHeight();
			this.Invalidate();
		}

		/// <summary>As tall as its rows at the current width (one row for the no-match line).</summary>
		private void FitHeight()
		{
			int rows = Math.Max(1, (this.Shown.Count + this.Columns - 1) / this.Columns);
			double height = rows * this.Pitch;
			if (this.Height != height)
			{
				this.Height = height;
			}
		}

		private void SetHovered(int index)
		{
			if (index != this.hoveredIndex)
			{
				this.hoveredIndex = index;
				this.ToolTipText = index >= 0 ? this.Tooltip(this.Shown[index]) : null;
				this.Invalidate();
			}
		}
	}
}
