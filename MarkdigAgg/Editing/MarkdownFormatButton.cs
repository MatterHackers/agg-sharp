/*
Copyright(c) 2026, Lars Brubaker
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
DISCLAIMED.IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
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
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;

namespace Markdig.Agg.Editing
{
	/// <summary>
	/// The pictures the toolbar draws as vectors rather than text: agg-sharp ships no icons (they live in the
	/// app), and the UI font has no reliable glyphs for bullets or alignment.
	/// </summary>
	public enum MarkdownFormatGlyph
	{
		None,
		Strike,
		AlignLeft,
		AlignCenter,
		AlignRight,
		BulletList,
		NumberedList,
		Quote,
		Table,
	}

	/// <summary>
	/// One formatting toolbar button: a short text label ("B", "&lt;/&gt;") or a drawn glyph, showing pressed
	/// (accent tint) when the style covers the whole selection, a faint shade when only part of it has the style,
	/// and plain otherwise - the three states word processors use for a mixed selection.
	/// </summary>
	public class MarkdownFormatButton : ThemedButton
	{
		private readonly MarkdownFormatGlyph glyph;

		private readonly TextWidget label;

		private RichStyleCoverage coverage;

		public MarkdownFormatButton(ThemeConfig theme, string text, MarkdownFormatGlyph glyph, string toolTip, bool boldLabel = false)
			: base(theme)
		{
			this.glyph = glyph;
			ToolTipText = toolTip;
			VAnchor = VAnchor.Absolute;
			Height = theme.ButtonHeight;
			MinimumSize = new Vector2(theme.ButtonHeight, 0);
			Margin = new BorderDouble(1, 0);
			BackgroundRadius = theme.ButtonRadius * DeviceScale;
			BackgroundColor = Color.Transparent;

			if (!string.IsNullOrEmpty(text))
			{
				HAnchor = HAnchor.Fit;
				Padding = new BorderDouble(6, 0);
				AddChild(label = new TextWidget(text, pointSize: theme.DefaultFontSize, textColor: theme.TextColor, bold: boldLabel)
				{
					HAnchor = HAnchor.Center,
					VAnchor = VAnchor.Center,
					AutoExpandBoundsToText = true,
					Selectable = false,
				});
			}
			else
			{
				HAnchor = HAnchor.Absolute;
				Width = theme.ButtonHeight;
			}
		}

		/// <summary>
		/// How much of the selection has this button's style: All shows pressed, Mixed a faint shade.
		/// </summary>
		public RichStyleCoverage Coverage
		{
			get => coverage;
			set
			{
				if (coverage != value)
				{
					coverage = value;
					BackgroundColor = value switch
					{
						RichStyleCoverage.All => theme.AccentTintColor,
						RichStyleCoverage.Mixed => theme.MinimalShade,
						_ => Color.Transparent,
					};
					BackgroundOutlineWidth = value == RichStyleCoverage.All ? 1 : 0;
					BorderColor = theme.AccentTintBorderColor;
					Invalidate();
				}
			}
		}

		public bool IsPressed => coverage == RichStyleCoverage.All;

		public override bool Enabled
		{
			get => base.Enabled;
			set
			{
				base.Enabled = value;
				if (label != null)
				{
					label.Enabled = value;
				}
			}
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			base.OnDraw(graphics2D);
			if (glyph == MarkdownFormatGlyph.None)
			{
				return;
			}

			var color = Enabled ? theme.TextColor : theme.TextColor.WithAlpha(90);
			double unit = Math.Max(1, Math.Round(DeviceScale));
			if (glyph == MarkdownFormatGlyph.Strike && label != null)
			{
				// Through the middle of the "S" label, so the button looks like the struck text it makes.
				var bounds = label.BoundsRelativeToParent;
				double y = Math.Round(bounds.Center.Y) + .5;
				graphics2D.Line(new Vector2(bounds.Left - unit, y), new Vector2(bounds.Right + unit, y), color, unit);
				return;
			}

			DrawGlyph(graphics2D, LocalBounds, color, unit);
		}

		/// <summary>
		/// Draws the glyph in a square of the button's height: four text lines arranged to show the format.
		/// </summary>
		private void DrawGlyph(Graphics2D graphics2D, RectangleDouble bounds, Color color, double unit)
		{
			double size = Math.Min(bounds.Width, bounds.Height) * .5;
			double left = Math.Round(bounds.Center.X - size / 2);
			double right = left + size;
			double top = Math.Round(bounds.Center.Y + size / 2);
			double step = Math.Round(size / 3);

			void Bar(double x0, double x1, int row) => graphics2D.FillRectangle(
				new RectangleDouble(x0, top - row * step - unit, x1, top - row * step + unit * .5), color);

			switch (glyph)
			{
				case MarkdownFormatGlyph.AlignLeft:
				case MarkdownFormatGlyph.AlignCenter:
				case MarkdownFormatGlyph.AlignRight:
					for (int row = 0; row < 4; row++)
					{
						// Alternate long and short lines, the short ones pushed to the alignment side.
						double width = row % 2 == 0 ? size : size * .6;
						double x0 = glyph == MarkdownFormatGlyph.AlignLeft ? left
							: glyph == MarkdownFormatGlyph.AlignRight ? right - width
							: Math.Round(bounds.Center.X - width / 2);
						Bar(x0, x0 + width, row);
					}

					break;

				case MarkdownFormatGlyph.BulletList:
				case MarkdownFormatGlyph.NumberedList:
					for (int row = 0; row < 3; row++)
					{
						double y = top - row * step * 1.5;
						if (glyph == MarkdownFormatGlyph.BulletList)
						{
							graphics2D.Circle(left + unit * 1.5, y - unit * .25, unit * 1.5, color);
						}
						else
						{
							// A tiny numeral column: short vertical ticks read as "1 2 3" at this size.
							graphics2D.FillRectangle(new RectangleDouble(left + unit, y - unit * 2, left + unit * 2, y + unit), color);
						}

						graphics2D.FillRectangle(new RectangleDouble(left + size * .35, y - unit, right, y + unit * .5), color);
					}

					break;

				case MarkdownFormatGlyph.Quote:
					graphics2D.FillRectangle(new RectangleDouble(left, top - 3 * step - unit, left + unit * 2, top + unit * .5), color);
					for (int row = 0; row < 4; row++)
					{
						Bar(left + size * .3, row == 3 ? left + size * .7 : right, row);
					}

					break;

				case MarkdownFormatGlyph.Table:
					var grid = new RectangleDouble(left, top - size, right, top);
					graphics2D.Rectangle(grid, color, unit);
					graphics2D.Line(new Vector2(left, top - size / 2), new Vector2(right, top - size / 2), color, unit);
					graphics2D.Line(new Vector2(grid.Center.X, top), new Vector2(grid.Center.X, top - size), color, unit);
					break;
			}
		}
	}
}
