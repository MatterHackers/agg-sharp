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

using System.Collections.Generic;

namespace MatterHackers.Agg.UI
{
	public static class ActiveTheme
	{
		public static RootedObjectEventHandler ThemeChanged = new RootedObjectEventHandler();
		
		private readonly static List<IThemeColors> themeColors = new List<IThemeColors>()
		{
			//Dark themes
			ThemeColors.Create("Red - Dark", new Color(172, 25, 61), new Color(217, 31, 77)),
			ThemeColors.Create("Pink - Dark", new Color(220, 79, 173), new Color(233, 143, 203)),
			ThemeColors.Create("Orange - Dark", new Color(255, 129, 25), new Color(255, 157, 76)),
			ThemeColors.Create("Green - Dark", new Color(0, 138, 23), new Color(0, 189, 32)),
			ThemeColors.Create("Blue - Dark", new Color(0, 75, 139), new Color(0, 103, 190)),
			ThemeColors.Create("Teal - Dark", new Color(0, 130, 153), new Color(0, 173, 204)),
			ThemeColors.Create("Light Blue - Dark", new Color(93, 178, 255), new Color(144, 202, 255)),
			ThemeColors.Create("Purple - Dark", new Color(70, 23, 180), new Color(104, 51, 229)),
			ThemeColors.Create("Magenta - Dark", new Color(140, 0, 149), new Color(188, 0, 200)),
			ThemeColors.Create("Grey - Dark", new Color(88, 88, 88), new Color(114, 114, 114)),

			//Light themes
			ThemeColors.Create("Red - Light", new Color(172, 25, 61), new Color(217, 31, 77), false),
			ThemeColors.Create("Pink - Light", new Color(220, 79, 173), new Color(233, 143, 203), false),
			ThemeColors.Create("Orange - Light", new Color(255, 129, 25), new Color(255, 157, 76), false),
			ThemeColors.Create("Green - Light", new Color(0, 138, 23), new Color(0, 189, 32), false),
			ThemeColors.Create("Blue - Light", new Color(0, 75, 139), new Color(0, 103, 190), false),
			ThemeColors.Create("Teal - Light", new Color(0, 130, 153), new Color(0, 173, 204), false),
			ThemeColors.Create("Light Blue - Light", new Color(93, 178, 255), new Color(144, 202, 255), false),
			ThemeColors.Create("Purple - Light", new Color(70, 23, 180), new Color(104, 51, 229), false),
			ThemeColors.Create("Magenta - Light", new Color(140, 0, 149), new Color(188, 0, 200), false),
			ThemeColors.Create("Grey - Light", new Color(88, 88, 88), new Color(114, 114, 114), false),
		};

		private static IThemeColors activeTheme = themeColors[0];

		public static List<IThemeColors> AvailableThemes => themeColors;

		public static IThemeColors Instance
		{
			get =>  activeTheme;
			set
			{
				if (value != activeTheme)
				{
					activeTheme = value;
					OnThemeChanged();
				}
			}
		}

		public static IThemeColors GetThemeColors(string name)
		{
			foreach (var colors in AvailableThemes)
			{
				if (colors.Name == name)
				{
					return colors;
				}
			}

			return AvailableThemes[0];
		}

		private static void OnThemeChanged()
		{
			ThemeChanged?.CallEvents(null, null);
		}
	}
}