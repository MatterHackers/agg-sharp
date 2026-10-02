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

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using System;
using System.IO;

namespace MatterHackers.Agg.Platform
{
	/// <summary>
	/// Conveniences over <see cref="IStaticData"/> built only on its members, so every provider - including
	/// ones outside this repo - gets them without implementing anything new.
	/// </summary>
	public static class StaticDataExtensions
	{
		/// <summary>
		/// The icon name to load for <paramref name="iconName"/>: the .svg of the same base name when the app
		/// ships one in Icons, otherwise <paramref name="iconName"/> unchanged.
		/// </summary>
		/// <remarks>
		/// agg-sharp's widgets name their icons as PNGs because that is what every app used to ship. An SVG
		/// icon draws with LCD subpixel edges, which a PNG cannot, so an app that has converted an icon gets
		/// the SVG while an app that still ships only the PNG keeps working.
		/// </remarks>
		public static string PreferSvgIcon(this IStaticData staticData, string iconName)
		{
			if (Path.GetExtension(iconName).Equals(".svg", StringComparison.OrdinalIgnoreCase))
			{
				return iconName;
			}

			string svgName = Path.ChangeExtension(iconName, ".svg");
			return staticData.FileExists(Path.Combine("Icons", svgName)) ? svgName : iconName;
		}
	}
}
