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

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// A menu row's label (or its shortcut): the theme's font size, in the theme's text colour - bound, so a row
	/// in an open menu follows a theme changed in place (<see cref="ThemeBindings"/>).
	/// </summary>
	internal static class MenuLabel
	{
		public static TextWidget Create(string text, ThemeConfig theme, BorderDouble padding = default)
		{
			var label = new TextWidget(text, pointSize: theme.DefaultFontSize, textColor: theme.TextColor)
			{
				Padding = padding,
			};

			ThemeBindings.BindTextColor(label, theme, t => t.TextColor);

			return label;
		}

		/// <summary>
		/// An icon shown beside a toggle row's label (the Color menu's swatches), centred in a button-sized square.
		/// </summary>
		/// <remarks>
		/// Only a picture: it paints no fill and takes no input. It used to be a <see cref="ThemedIconButton"/>,
		/// whose opaque button fill showed as a box behind the swatch on a highlighted row, and which took a tap
		/// on the swatch for itself instead of the row.
		/// </remarks>
		public static GuiWidget CreateIcon(Image.ImageBuffer icon, ThemeConfig theme)
		{
			var square = new GuiWidget(theme.ButtonHeight, theme.ButtonHeight)
			{
				HAnchor = HAnchor.Absolute,
				VAnchor = VAnchor.Absolute | VAnchor.Center,
				Selectable = false,
			};

			square.AddChild(new ImageWidget(icon, listenForImageChanged: false)
			{
				HAnchor = HAnchor.Center,
				VAnchor = VAnchor.Center,
				Selectable = false,
			});

			return square;
		}
	}
}
