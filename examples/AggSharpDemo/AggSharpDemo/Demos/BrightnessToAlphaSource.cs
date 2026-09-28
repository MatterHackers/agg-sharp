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
using System.IO;
using MatterHackers.Agg;

namespace MatterHackers.AggSharpDemo.Demos
{
	/// <summary>
	/// C++ <c>pattern_src_brightness_to_alpha</c> over one of the example's pictures, 1.ppm to 9.ppm, as
	/// <c>load_img</c> gives it to a flip_y = true example: row 0 is the picture's bottom row. line_patterns and
	/// line_patterns_clip share it, as the reference renderer's demos share line_pattern_source.h.
	/// </summary>
	internal class BrightnessToAlphaSource : ILineImageSource
	{
		/// <summary>
		/// C++ <c>brightness_to_alpha</c>: the alpha of a pattern pixel, indexed by its r + g + b scaled to 0-767.
		/// Dark pixels are opaque, the white background all but transparent.
		/// </summary>
		private static readonly byte[] BrightnessToAlphaTable =
		{
			255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
			255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
			255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
			255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
			255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
			255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
			255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
			255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
			255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
			255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 254, 254, 254, 254, 254, 254,
			255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
			255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
			255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
			255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
			255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
			255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
			255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
			255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
			255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
			255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
			255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
			255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
			255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
			254, 254, 254, 254, 254, 254, 254, 254, 254, 254, 254, 254, 254, 254, 253, 253,
			253, 253, 253, 253, 253, 253, 253, 253, 253, 253, 253, 253, 253, 253, 253, 252,
			252, 252, 252, 252, 252, 252, 252, 252, 252, 252, 252, 251, 251, 251, 251, 251,
			251, 251, 251, 251, 250, 250, 250, 250, 250, 250, 250, 250, 249, 249, 249, 249,
			249, 249, 249, 248, 248, 248, 248, 248, 248, 248, 247, 247, 247, 247, 247, 246,
			246, 246, 246, 246, 246, 245, 245, 245, 245, 245, 244, 244, 244, 244, 243, 243,
			243, 243, 243, 242, 242, 242, 242, 241, 241, 241, 241, 240, 240, 240, 239, 239,
			239, 239, 238, 238, 238, 238, 237, 237, 237, 236, 236, 236, 235, 235, 235, 234,
			234, 234, 233, 233, 233, 232, 232, 232, 231, 231, 230, 230, 230, 229, 229, 229,
			228, 228, 227, 227, 227, 226, 226, 225, 225, 224, 224, 224, 223, 223, 222, 222,
			221, 221, 220, 220, 219, 219, 219, 218, 218, 217, 217, 216, 216, 215, 214, 214,
			213, 213, 212, 212, 211, 211, 210, 210, 209, 209, 208, 207, 207, 206, 206, 205,
			204, 204, 203, 203, 202, 201, 201, 200, 200, 199, 198, 198, 197, 196, 196, 195,
			194, 194, 193, 192, 192, 191, 190, 190, 189, 188, 188, 187, 186, 186, 185, 184,
			183, 183, 182, 181, 180, 180, 179, 178, 177, 177, 176, 175, 174, 174, 173, 172,
			171, 171, 170, 169, 168, 167, 166, 166, 165, 164, 163, 162, 162, 161, 160, 159,
			158, 157, 156, 156, 155, 154, 153, 152, 151, 150, 149, 148, 148, 147, 146, 145,
			144, 143, 142, 141, 140, 139, 138, 137, 136, 135, 134, 133, 132, 131, 130, 129,
			128, 128, 127, 125, 124, 123, 122, 121, 120, 119, 118, 117, 116, 115, 114, 113,
			112, 111, 110, 109, 108, 107, 106, 105, 104, 102, 101, 100, 99, 98, 97, 96,
			95, 94, 93, 91, 90, 89, 88, 87, 86, 85, 84, 82, 81, 80, 79, 78,
			77, 75, 74, 73, 72, 71, 70, 69, 67, 66, 65, 64, 63, 61, 60, 59,
			58, 57, 56, 54, 53, 52, 51, 50, 48, 47, 46, 45, 44, 42, 41, 40,
			39, 37, 36, 35, 34, 33, 31, 30, 29, 28, 27, 25, 24, 23, 22, 20,
			19, 18, 17, 15, 14, 13, 12, 11, 9, 8, 7, 6, 4, 3, 2, 1,
		};

		private readonly Color[] pixels;

		private readonly int width;

		private BrightnessToAlphaSource(int width, int height, Color[] pixels)
		{
			this.width = width;
			this.Height = height;
			this.pixels = pixels;
		}

		public double Width => this.width;

		public double Height { get; }

		/// <summary>Picture <paramref name="number"/>, 1 to 9, from the demo's embedded resources.</summary>
		public static BrightnessToAlphaSource Load(int number)
		{
			string resourceName = $"MatterHackers.AggSharpDemo.Images.LinePatterns.{number}.ppm";
			using var stream = typeof(BrightnessToAlphaSource).Assembly.GetManifestResourceStream(resourceName)
				?? throw new InvalidOperationException(
					$"The image resource '{resourceName}' is missing; AggSharpDemo.csproj embeds it from Images/LinePatterns.");
			using var memory = new MemoryStream();
			stream.CopyTo(memory);
			return FromPpm(memory.ToArray());
		}

		public LineImageColor Pixel(int x, int y)
		{
			// The bytes go in as rgba8 does, unconverted: C++'s color_type here is the linear rgba8.
			Color c = this.pixels[(y * this.width) + x];
			return new LineImageColor(c.red / 255.0, c.green / 255.0, c.blue / 255.0, c.alpha / 255.0);
		}

		// Reads only what the pictures are: binary (P6) PPMs, 8 bits a channel, a comment allowed in the header.
		private static BrightnessToAlphaSource FromPpm(byte[] ppm)
		{
			int position = 0;
			if (NextToken(ppm, ref position) != "P6")
			{
				throw new InvalidDataException("The line pattern pictures are expected to be binary (P6) PPMs.");
			}

			int width = int.Parse(NextToken(ppm, ref position));
			int height = int.Parse(NextToken(ppm, ref position));
			NextToken(ppm, ref position);
			position++; // the one whitespace byte before the pixels

			var pixels = new Color[width * height];
			for (int y = 0; y < height; y++)
			{
				// flip_y: the picture's top row (first in the file) is the last row here.
				int row = height - 1 - y;
				for (int x = 0; x < width; x++)
				{
					int i = position + (((y * width) + x) * 3);
					pixels[(row * width) + x] = WithBrightnessAlpha(ppm[i], ppm[i + 1], ppm[i + 2]);
				}
			}

			return new BrightnessToAlphaSource(width, height, pixels);
		}

		/// <summary>C++ <c>pattern_src_brightness_to_alpha::pixel</c>, with its one-past-the-end read fixed.</summary>
		private static Color WithBrightnessAlpha(int r, int g, int b)
		{
			// C++ indexes sum * 768 / 765, which is 768 - one past the table - for a white pixel, and reads
			// whatever byte follows. The brightest pixels take the table's last entry instead; the reference
			// renderer (line_pattern_source.h) is fixed the same way. C++'s mult_cover(255, cover) is the cover.
			int i = Math.Min((r + g + b) * BrightnessToAlphaTable.Length / (3 * 255), BrightnessToAlphaTable.Length - 1);
			return new Color(r, g, b, BrightnessToAlphaTable[i]);
		}

		private static string NextToken(byte[] data, ref int position)
		{
			while (true)
			{
				if (data[position] == '#')
				{
					while (data[position] != '\n')
					{
						position++;
					}
				}
				else if (char.IsWhiteSpace((char)data[position]))
				{
					position++;
				}
				else
				{
					break;
				}
			}

			int start = position;
			while (!char.IsWhiteSpace((char)data[position]))
			{
				position++;
			}

			return System.Text.Encoding.ASCII.GetString(data, start, position - start);
		}
	}
}
