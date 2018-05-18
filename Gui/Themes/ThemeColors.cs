/*
Copyright (c) 2016, Kevin Pope, John Lewin
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

namespace MatterHackers.Agg.UI
{
	public class ThemeColors: IThemeColors
	{
		public bool IsDarkTheme { get; set; }

		public string Name { get; set; }

		public Color Transparent { get; set; } = new Color(0, 0, 0, 0);

		public Color TransparentDarkOverlay { get; set; } = new Color(0, 0, 0, 50);

		public Color TransparentLightOverlay { get; set; } = new Color(255, 255, 255, 50);

		public Color TabLabelSelected { get; set; }

		public Color TabLabelUnselected { get; set; }

		public Color SecondaryTextColor { get; set; }

		public Color PrimaryBackgroundColor { get; set; }

		public Color SecondaryBackgroundColor { get; set; }

		public Color TertiaryBackgroundColor { get; set; }

		public Color PrimaryTextColor { get; set; }

		public Color PrimaryAccentColor { get; set; }

		public Color SourceColor { get; private set; }

		public static ThemeColors Create(string name, Color primary, bool darkTheme = true)
		{
			var color = new ThemeColors
			{
				IsDarkTheme = darkTheme,
				Name = name,
				SourceColor = primary
			};

			if (darkTheme)
			{
				color.PrimaryAccentColor = primary;

				color.PrimaryBackgroundColor = new Color(68, 68, 68);
				color.SecondaryBackgroundColor = new Color(51, 51, 51);

				color.TabLabelSelected = new Color(255, 255, 255);
				color.TabLabelUnselected = new Color(180, 180, 180);
				color.PrimaryTextColor = new Color(255, 255, 255);
				color.SecondaryTextColor = new Color(200, 200, 200);

				color.TertiaryBackgroundColor = new Color(62, 62, 62);
			}
			else
			{
				color.PrimaryAccentColor = primary;

				color.PrimaryBackgroundColor = new Color(208, 208, 208);
				color.SecondaryBackgroundColor = new Color(185, 185, 185);
				color.TabLabelSelected = new Color(51, 51, 51);
				color.TabLabelUnselected = new Color(102, 102, 102);
				color.PrimaryTextColor = new Color(34, 34, 34);
				color.SecondaryTextColor = new Color(51, 51, 51);

				color.TertiaryBackgroundColor = new Color(190, 190, 190);
			}

			color.PrimaryAccentColor = color.PrimaryAccentColor.AdjustContrast(color.PrimaryBackgroundColor).ToColor();

			return color;
		}
	}
}