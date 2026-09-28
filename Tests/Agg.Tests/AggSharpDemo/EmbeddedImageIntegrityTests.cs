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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MatterHackers.AggSharpDemo.Demos;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	/// <summary>
	/// The demo's embedded pictures arrive byte for byte as committed. A Windows checkout with core.autocrlf
	/// once rewrote LinePatterns/4.ppm's header newlines to CRLF (git took the NUL-free file for text), which
	/// shifted every pixel by a byte and broke line_patterns against C++ AGG on Windows only. .gitattributes
	/// now declares the image types binary; this catches a regression through the one symptom that matters,
	/// the resource's size no longer matching what its own header says.
	/// </summary>
	public class EmbeddedImageIntegrityTests
	{
		private static readonly System.Reflection.Assembly DemoAssembly = typeof(PatternPerspectiveDemo).Assembly;

		[Test]
		public async Task PpmResourcesAreExactlyHeaderPlusPixels()
		{
			string[] names = DemoAssembly.GetManifestResourceNames().Where(n => n.EndsWith(".ppm")).ToArray();
			// agg.ppm, compositing.ppm and LinePatterns 1-9: an empty list would pass vacuously.
			await Assert.That(names.Length).IsEqualTo(11);

			foreach (string name in names)
			{
				byte[] ppm = ReadResource(name);
				int position = 0;
				await Assert.That(NextToken(ppm, ref position)).IsEqualTo("P6");
				int width = int.Parse(NextToken(ppm, ref position));
				int height = int.Parse(NextToken(ppm, ref position));
				await Assert.That(NextToken(ppm, ref position)).IsEqualTo("255");
				// Netpbm: exactly one whitespace byte separates maxval from the raster. A CR inserted before
				// that LF (autocrlf) makes the raster start one byte late and the file run long.
				position++;
				await Assert.That(ppm.Length).IsEqualTo(position + (width * height * 3))
					.Because($"{name} is {ppm.Length} bytes; its {width}x{height} header says {position + (width * height * 3)}");
			}
		}

		[Test]
		public async Task BmpResourcesMatchTheirHeaderFileSize()
		{
			string[] names = DemoAssembly.GetManifestResourceNames().Where(n => n.EndsWith(".bmp")).ToArray();
			await Assert.That(names.Length).IsGreaterThan(0);

			foreach (string name in names)
			{
				byte[] bmp = ReadResource(name);
				await Assert.That(Encoding.ASCII.GetString(bmp, 0, 2)).IsEqualTo("BM");
				// BITMAPFILEHEADER.bfSize, little-endian at offset 2.
				await Assert.That(bmp.Length).IsEqualTo(BitConverter.ToInt32(bmp, 2)).Because(name);
			}
		}

		private static byte[] ReadResource(string name)
		{
			using Stream stream = DemoAssembly.GetManifestResourceStream(name);
			using var memory = new MemoryStream();
			stream.CopyTo(memory);
			return memory.ToArray();
		}

		// A Netpbm header token: whitespace and '#' comments (to end of line) skipped.
		private static string NextToken(byte[] data, ref int position)
		{
			while (data[position] == '#' || char.IsWhiteSpace((char)data[position]))
			{
				if (data[position] == '#')
				{
					while (data[position] != '\n')
					{
						position++;
					}
				}

				position++;
			}

			int start = position;
			while (!char.IsWhiteSpace((char)data[position]))
			{
				position++;
			}

			return Encoding.ASCII.GetString(data, start, position - start);
		}
	}
}
