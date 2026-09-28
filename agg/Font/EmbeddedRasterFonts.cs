//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.4
// Copyright (C) 2002-2005 Maxim Shemanarev (http://www.antigrain.com)
//
// C# port by: Lars Brubaker
//                  larsbrubaker@gmail.com
// Copyright (C) 2007-2026
//
// Permission to copy, use, modify, sell and distribute this software
// is granted provided this copyright notice appears in all copies.
// This software is provided "as is" without express or implied
// warranty, and with no claim as to its suitability for any purpose.
//
//----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;

namespace MatterHackers.Agg
{
	/// <summary>
	/// C++ AGG's embedded raster fonts (agg_embedded_raster_fonts.cpp): 34 small bitmap fonts - gse4x6 through
	/// verdana18_bold - in the byte layout <see cref="glyph_raster_bin"/> reads.
	/// </summary>
	/// <remarks>
	/// The bytes ship as one embedded resource (Font/EmbeddedRasterFonts.bin) that
	/// scripts/gen-embedded-raster-fonts.py packs from the C++ arrays; as C# literals they would be thousands of
	/// lines for the compiler to chew through. The resource is read once, on first use.
	/// </remarks>
	public static class EmbeddedRasterFonts
	{
		private static readonly Lazy<(List<string> Names, Dictionary<string, byte[]> Fonts)> Loaded = new Lazy<(List<string>, Dictionary<string, byte[]>)>(Load);

		/// <summary>Every font's C++ name (e.g. "gse4x6", "verdana12_bold"), in the C++ source's order.</summary>
		public static IReadOnlyList<string> Names => Loaded.Value.Names;

		/// <summary>The font named <paramref name="name"/> as its C++ array's bytes.</summary>
		/// <exception cref="KeyNotFoundException">No embedded raster font has that name.</exception>
		public static byte[] Get(string name)
		{
			if (Loaded.Value.Fonts.TryGetValue(name, out byte[] font))
			{
				return font;
			}

			throw new KeyNotFoundException($"There is no embedded raster font named '{name}'.");
		}

		// Each font: a u8 name length, the ASCII name, a u32 (little endian) data length, the data.
		private static (List<string>, Dictionary<string, byte[]>) Load()
		{
			var names = new List<string>();
			var fonts = new Dictionary<string, byte[]>();
			using Stream stream = typeof(EmbeddedRasterFonts).Assembly.GetManifestResourceStream("MatterHackers.Agg.Font.EmbeddedRasterFonts.bin");
			using var reader = new BinaryReader(stream);
			while (stream.Position < stream.Length)
			{
				string name = System.Text.Encoding.ASCII.GetString(reader.ReadBytes(reader.ReadByte()));
				int length = reader.ReadInt32();
				names.Add(name);
				fonts[name] = reader.ReadBytes(length);
			}

			return (names, fonts);
		}
	}
}
