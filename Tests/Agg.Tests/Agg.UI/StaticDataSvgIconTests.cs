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
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// SVG icons loaded through <see cref="StaticDataBase.LoadIcon(string, int, int, bool, Func{ImageBuffer, ValueTuple{ImageBuffer, string}})"/>
	/// must honor the presentation features app icons are drawn with: fill/stroke opacity, caps, joins,
	/// attributes inherited from a group, and document paint order.
	/// </summary>
	/// <remarks>
	/// Each test builds its own provider, so the icon cache is never shared. Sizes are multiplied by
	/// DeviceScale inside LoadIcon, so pixels are sampled in viewBox units, not raw pixel positions.
	/// </remarks>
	public class StaticDataSvgIconTests
	{
		private static ImageBuffer LoadSvgIcon(string body, int size)
		{
			var provider = new InMemorySvgStaticData($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 100 100\">{body}</svg>");
			return provider.LoadIcon("test.svg", size, size);
		}

		/// <summary>The alpha at viewBox coordinates (x, y), y down from the top as SVG counts it.</summary>
		private static int AlphaAt(ImageBuffer image, double x, double y) => At(image, x, y).alpha;

		private static Color At(ImageBuffer image, double x, double y)
		{
			int px = Math.Min(image.Width - 1, (int)(x / 100 * image.Width));
			int py = Math.Min(image.Height - 1, (int)(y / 100 * image.Height));
			return image.GetPixel(px, image.Height - 1 - py);
		}

		[Test]
		public async Task FillOpacityFadesTheFill()
		{
			var image = LoadSvgIcon("<rect width=\"100\" height=\"100\" fill=\"#000\" fill-opacity=\".32\"/>", 16);
			await Assert.That(AlphaAt(image, 50, 50)).IsBetween(74, 90);
		}

		[Test]
		public async Task StrokeOpacityFadesTheStroke()
		{
			var image = LoadSvgIcon("<path d=\"M0 50 H100\" fill=\"none\" stroke=\"#000\" stroke-width=\"40\" stroke-opacity=\".32\"/>", 16);
			await Assert.That(AlphaAt(image, 50, 50)).IsBetween(74, 90);
		}

		[Test]
		public async Task RoundLinecapReachesPastTheEndpoint()
		{
			const string Line = "<path d=\"M30 50 H70\" fill=\"none\" stroke=\"#000\" stroke-width=\"20\" stroke-linecap=\"{0}\"/>";
			var round = LoadSvgIcon(string.Format(Line, "round"), 64);
			var butt = LoadSvgIcon(string.Format(Line, "butt"), 64);

			// Five units past the end, inside the round cap's radius of ten.
			await Assert.That(AlphaAt(round, 75, 50)).IsGreaterThan(200);
			await Assert.That(AlphaAt(butt, 75, 50)).IsLessThan(20);
		}

		[Test]
		public async Task RoundLinejoinCutsTheMiterTip()
		{
			// The apex's miter ratio is 1/sin(26.6 deg) = 2.24, under the default limit of 4, so the miter tip
			// reaches about 11 units above the apex; a round join reaches only the half width, 5.
			const string Chevron = "<path d=\"M10 90 L50 20 L90 90\" fill=\"none\" stroke=\"#000\" stroke-width=\"10\" stroke-linejoin=\"{0}\"/>";
			var miter = LoadSvgIcon(string.Format(Chevron, "miter"), 64);
			var round = LoadSvgIcon(string.Format(Chevron, "round"), 64);

			await Assert.That(AlphaAt(miter, 50, 11)).IsGreaterThan(150);
			await Assert.That(AlphaAt(round, 50, 11)).IsLessThan(20);
		}

		[Test]
		public async Task PathInheritsPresentationAttributesFromItsGroup()
		{
			var image = LoadSvgIcon("<g fill=\"none\" stroke=\"#000\" stroke-width=\"8\"><path d=\"M20 20 H80 V80 H20 Z\"/></g>", 64);

			// fill="none" inherited: the inside stays clear; the inherited stroke draws the edge.
			await Assert.That(AlphaAt(image, 50, 50)).IsLessThan(20);
			await Assert.That(AlphaAt(image, 20, 50)).IsGreaterThan(200);
		}

		[Test]
		public async Task LaterElementsPaintOverEarlierOnes()
		{
			var image = LoadSvgIcon("<circle cx=\"50\" cy=\"50\" r=\"40\" fill=\"#f00\"/><path d=\"M0 0 H100 V100 H0 Z\" fill=\"#00f\"/>", 16);
			var center = At(image, 50, 50);
			await Assert.That((int)center.blue).IsGreaterThan(200);
			await Assert.That((int)center.red).IsLessThan(50);
		}

		/// <summary>Serves one SVG for any path, as a zip or HTTP backed provider would from memory.</summary>
		private class InMemorySvgStaticData : StaticDataBase
		{
			private readonly string svg;

			public InMemorySvgStaticData(string svg)
			{
				this.svg = svg;
			}

			public override bool DirectoryExists(string path) => false;

			public override bool FileExists(string path) => true;

			public override IEnumerable<string> GetDirectories(string path) => Array.Empty<string>();

			public override IEnumerable<string> GetFiles(string path) => Array.Empty<string>();

			public override string MapPath(string path) => path;

			public override Stream OpenStream(string path) => new MemoryStream(Encoding.UTF8.GetBytes(svg));

			public override string[] ReadAllLines(string path) => new[] { svg };

			public override string ReadAllText(string path) => svg;
		}
	}
}
