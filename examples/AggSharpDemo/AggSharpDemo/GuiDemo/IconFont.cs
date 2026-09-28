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

namespace MatterHackers.AggSharpDemo.GuiDemo
{
	/// <summary>
	/// The GUI demo's icon font: Font Awesome 4.7, the same fa.ttf agg-gui's demo loads (SIL OFL 1.1, see
	/// Fonts/FontAwesome-LICENSE-OFL.txt), embedded in this assembly and read by agg-sharp's own TrueType engine.
	/// As in agg-gui the font belongs to the demo, not the library; the library only takes a glyph and a
	/// <see cref="TypeFace"/>.
	/// </summary>
	public static class IconFont
	{
		// Font Awesome 4 private use codepoints, named as agg-gui's demo uses them
		public const string Plus = "\uF067";
		public const string FolderOpen = "\uF07C";
		public const string Undo = "\uF0E2";
		public const string Redo = "\uF01E";
		public const string Copy = "\uF0C5";
		public const string Cut = "\uF0C4";
		public const string CaretRight = "\uF0DA";
		public const string Comment = "\uF075";
		public const string Eye = "\uF06E";
		public const string EyeSlash = "\uF070";
		public const string ArrowUp = "\uF062";
		public const string ArrowDown = "\uF063";
		public const string Search = "\uF002";
		public const string Clear = "\uF00D";

		private const string ResourceName = "MatterHackers.AggSharpDemo.Fonts.FontAwesome.ttf";

		private static readonly Lazy<TypeFace> Instance = new Lazy<TypeFace>(Load, LazyThreadSafetyMode.ExecutionAndPublication);

		/// <summary>Gets the loaded Font Awesome face, read from the assembly on first use.</summary>
		public static TypeFace TypeFace => Instance.Value;

		private static TypeFace Load()
		{
			using var stream = typeof(IconFont).Assembly.GetManifestResourceStream(ResourceName)
				?? throw new InvalidOperationException(
					$"The font resource '{ResourceName}' is missing; AggSharpDemo.csproj embeds it from Fonts/FontAwesome.ttf.");
			var typeFace = new TypeFace();
			typeFace.LoadTTF(stream);
			return typeFace;
		}
	}
}
