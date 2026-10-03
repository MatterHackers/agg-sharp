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
using System.Threading;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Image;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// agg's built-in icon font: Font Awesome 6.7.2 Free Solid (fa-solid-900.ttf from the official
	/// @fortawesome/fontawesome-free npm package; font SIL OFL 1.1, icons CC BY 4.0, see
	/// Fonts/fa-solid-900-LICENSE.txt), embedded in Gui and read by agg-sharp's own TrueType engine. Any agg
	/// app or widget can draw an icon from one of the codepoints below.
	/// </summary>
	public static class IconFont
	{
		// Font Awesome 6 codepoints, checked against the 6.7.2 metadata (icon-families.json). Every one is in
		// the free Solid set; IconFontTests checks each against the font's cmap.
		public const string Bold = "";
		public const string Italic = "";
		public const string Strikethrough = "";
		public const string Code = "";
		public const string AlignLeft = "";
		public const string AlignCenter = "";
		public const string AlignRight = "";
		public const string ListUl = "";
		public const string ListOl = "";
		public const string ListCheck = "";
		public const string QuoteLeft = "";
		public const string Link = "";
		public const string FileCode = "";
		public const string Table = "";
		public const string Eye = "";
		public const string EyeSlash = "";
		public const string ChevronDown = "";

		// FA 6 keeps the FA 4 private use codepoints for these, so the names the GUI demo used still draw the
		// same icon. The FA 6 name is given where it differs.
		public const string Plus = "";
		public const string FolderOpen = "";
		public const string ArrowRotateLeft = "";
		public const string Undo = ArrowRotateLeft;
		public const string ArrowRotateRight = "";
		public const string Redo = ArrowRotateRight;
		public const string Copy = "";
		public const string Scissors = "";
		public const string Cut = Scissors;
		public const string CaretRight = "";
		public const string Comment = "";
		public const string ArrowUp = "";
		public const string ArrowDown = "";
		public const string MagnifyingGlass = "";
		public const string Search = MagnifyingGlass;
		public const string Xmark = "";
		public const string Clear = Xmark;

		private const string ResourceName = "MatterHackers.Agg.UI.Fonts.fa-solid-900.ttf";

		private static readonly Lazy<TypeFace> Instance = new Lazy<TypeFace>(Load, LazyThreadSafetyMode.ExecutionAndPublication);

		/// <summary>Gets the loaded Font Awesome face, read from the assembly on first use.</summary>
		public static TypeFace TypeFace => Instance.Value;

		/// <summary>
		/// Draws <paramref name="glyph"/> from this font into a <paramref name="size"/> pixel square, placed in
		/// the font's em box (see <see cref="GlyphIcon.RenderInEmBox"/>) so a row of icons shares one baseline
		/// and one height.
		/// </summary>
		/// <returns>The image, or null when there is no glyph or the font has no outline for it.</returns>
		public static ImageBuffer Render(string glyph, Color color, int size)
		{
			return GlyphIcon.RenderInEmBox(glyph, TypeFace, color, size);
		}

		private static TypeFace Load()
		{
			using var stream = typeof(IconFont).Assembly.GetManifestResourceStream(ResourceName)
				?? throw new InvalidOperationException(
					$"The font resource '{ResourceName}' is missing; Gui.csproj embeds it from Fonts/fa-solid-900.ttf.");
			var typeFace = new TypeFace();
			typeFace.LoadTTF(stream);
			return typeFace;
		}
	}
}
