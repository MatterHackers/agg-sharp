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

		public RGBA_Bytes Transparent { get; set; } = new RGBA_Bytes(0, 0, 0, 0);

		public RGBA_Bytes TransparentDarkOverlay { get; set; } = new RGBA_Bytes(0, 0, 0, 50);

		public RGBA_Bytes TransparentLightOverlay { get; set; } = new RGBA_Bytes(255, 255, 255, 50);

		public RGBA_Bytes TabLabelSelected { get; set; }

		public RGBA_Bytes TabLabelUnselected { get; set; }

		public RGBA_Bytes SecondaryTextColor { get; set; }

		public RGBA_Bytes PrimaryBackgroundColor { get; set; }

		public RGBA_Bytes SecondaryBackgroundColor { get; set; }

		public RGBA_Bytes TertiaryBackgroundColor { get; set; }

		public RGBA_Bytes PrimaryTextColor { get; set; }

		public RGBA_Bytes PrimaryAccentColor { get; set; }

		public RGBA_Bytes SecondaryAccentColor { get; set; }

		public static ThemeColors Create(string name, RGBA_Bytes primary, RGBA_Bytes secondary, bool darkTheme = true)
		{
			var colors = new ThemeColors
			{
				IsDarkTheme = darkTheme,
			};

			if (darkTheme)
			{
				colors.PrimaryAccentColor = primary;
				colors.SecondaryAccentColor = secondary;

				colors.PrimaryBackgroundColor = new RGBA_Bytes(68, 68, 68);
				colors.SecondaryBackgroundColor = new RGBA_Bytes(51, 51, 51);

				colors.TabLabelSelected = new RGBA_Bytes(255, 255, 255);
				colors.TabLabelUnselected = new RGBA_Bytes(180, 180, 180);
				colors.PrimaryTextColor = new RGBA_Bytes(255, 255, 255);
				colors.SecondaryTextColor = new RGBA_Bytes(200, 200, 200);

				colors.TertiaryBackgroundColor = new RGBA_Bytes(62, 62, 62);
			}
			else
			{
				colors.PrimaryAccentColor = secondary;
				colors.SecondaryAccentColor = primary;

				colors.PrimaryBackgroundColor = new RGBA_Bytes(208, 208, 208);
				colors.SecondaryBackgroundColor = new RGBA_Bytes(185, 185, 185);
				colors.TabLabelSelected = new RGBA_Bytes(51, 51, 51);
				colors.TabLabelUnselected = new RGBA_Bytes(102, 102, 102);
				colors.PrimaryTextColor = new RGBA_Bytes(34, 34, 34);
				colors.SecondaryTextColor = new RGBA_Bytes(51, 51, 51);

				colors.TertiaryBackgroundColor = new RGBA_Bytes(190, 190, 190);
			}

			return colors;
		}
	}
}