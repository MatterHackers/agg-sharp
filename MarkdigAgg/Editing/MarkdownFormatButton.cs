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
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;

namespace Markdig.Agg.Editing
{
	/// <summary>
	/// One formatting toolbar button: a Font Awesome glyph from <see cref="IconFont"/>, drawn in the font's em box at
	/// one size and centred in a fixed <see cref="DesignWidth"/> x <see cref="DesignHeight"/> cell, so every icon in
	/// the toolbar shares one baseline and one scale. It shows pressed (an accent tint with the icon in a deeper
	/// accent) when the style covers the whole selection, a faint shade when only part of it has the style, and
	/// plain otherwise - the three states word processors use for a mixed selection. The button sits in a
	/// <see cref="MarkdownFormatGroup"/>, which draws the outline and dividers and rounds the outer corners of its
	/// end buttons' fills.
	/// </summary>
	public class MarkdownFormatButton : ThemedButton
	{
		/// <summary>The button's width in design units.</summary>
		public const double DesignWidth = 30;

		/// <summary>The button's height in design units.</summary>
		public const double DesignHeight = 28;

		/// <summary>The icon font's em, in design units: the height of the em box every glyph is placed in.</summary>
		public const double IconDesignSize = 14;

		private RichStyleCoverage coverage;

		public MarkdownFormatButton(ThemeConfig theme, string icon, string toolTip)
			: base(theme)
		{
			Icon = icon;
			ToolTipText = toolTip;
			HAnchor = HAnchor.Absolute;
			VAnchor = VAnchor.Absolute;
			Width = DesignWidth * DeviceScale;
			Height = DesignHeight * DeviceScale;
			Margin = new BorderDouble(0);
			BackgroundColor = Color.Transparent;

			// The group draws one outline around all its buttons, so a button never draws its own (a theme that
			// outlines buttons would double every edge where two meet).
			BackgroundOutlineWidth = 0;
		}

		/// <summary>The <see cref="IconFont"/> glyph drawn on the button.</summary>
		public string Icon { get; }

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
					Invalidate();
				}
			}
		}

		public bool IsPressed => coverage == RichStyleCoverage.All;

		/// <summary>
		/// Where the icon's em box lands in the button, in local device pixels: as wide as the glyph's advance and
		/// as tall as the font's descent to ascent, centred, with the baseline and left edge on whole pixels. The
		/// height and baseline are the same for every glyph, which is what keeps a row of icons on one line.
		/// </summary>
		public RectangleDouble IconEmBox => EmBox(IconFace(), out _);

		/// <summary>
		/// The icon's colour: the text colour, a deeper accent while pressed (the accent pulled toward the text
		/// colour, so it reads darker on a light theme and lighter on a dark one), and faded while disabled.
		/// </summary>
		public Color IconColor
		{
			get
			{
				if (!Enabled)
				{
					return theme.TextColor.WithAlpha(90);
				}

				return IsPressed ? theme.PrimaryAccentColor.Blend(theme.TextColor, .4) : theme.TextColor;
			}
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			base.OnDraw(graphics2D);

			var face = IconFace();
			var box = EmBox(face, out double baseline);
			var outline = face.GetGlyphForCharacter(Icon[0]);
			if (outline != null)
			{
				graphics2D.Render(new VertexSourceApplyTransform(outline, Affine.NewTranslation(box.Left, baseline)), IconColor);
			}
		}

		/// <summary>
		/// The icon font at <see cref="IconDesignSize"/> for the current DeviceScale. Drawn as a vector straight from
		/// the face rather than through <see cref="IconFont.Render"/>, whose square image shrinks a glyph wider than
		/// its em (code and link are 640 units in a 512 em) and so would draw those two smaller than the rest.
		/// </summary>
		private static StyledTypeFace IconFace()
		{
			double pixels = IconDesignSize * DeviceScale;
			return new StyledTypeFace(IconFont.TypeFace, pixels * StyledTypeFace.PointsPerInch / StyledTypeFace.PixelsPerInch);
		}

		private RectangleDouble EmBox(StyledTypeFace face, out double baseline)
		{
			double advance = face.GetAdvanceForCharacter(Icon[0]);
			double descent = face.DescentInPixels; // negative, below the baseline
			double height = face.AscentInPixels - descent;
			var bounds = LocalBounds;
			double left = Math.Round(bounds.Center.X - advance / 2);
			baseline = Math.Round(bounds.Center.Y - (descent + height / 2));
			return new RectangleDouble(left, baseline + descent, left + advance, baseline + descent + height);
		}
	}
}
