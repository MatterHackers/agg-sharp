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

using System.Collections.Generic;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.RasterizerScanline;
using MatterHackers.Agg.VertexSource;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Agg.Tests.Agg
{
	// C++ agg_scanline_boolean_algebra.h's operations on one-pixel-high rectangles, whose covers are easy to know.
	public class ScanlineBooleanAlgebraTests
	{
		[Test]
		public async Task OperationsOnFullRectanglesKeepTheRightPixels()
		{
			// A covers 0..9, B covers 5..14, every pixel full.
			await Assert.That(Combine(SboolOp.Or, 0, 10, 5, 15)).IsEquivalentTo(Full(0, 15));
			await Assert.That(Combine(SboolOp.And, 0, 10, 5, 15)).IsEquivalentTo(Full(5, 10));
			await Assert.That(Combine(SboolOp.Xor, 0, 10, 5, 15)).IsEquivalentTo(Full(0, 5, 10, 15));
			await Assert.That(Combine(SboolOp.XorSaddle, 0, 10, 5, 15)).IsEquivalentTo(Full(0, 5, 10, 15));
			await Assert.That(Combine(SboolOp.XorAbsDiff, 0, 10, 5, 15)).IsEquivalentTo(Full(0, 5, 10, 15));
			await Assert.That(Combine(SboolOp.AMinusB, 0, 10, 5, 15)).IsEquivalentTo(Full(0, 5));
			await Assert.That(Combine(SboolOp.BMinusA, 0, 10, 5, 15)).IsEquivalentTo(Full(10, 15));
		}

		[Test]
		public async Task IntersectingWithAFullSolidSpanKeepsPartialCoversAsTheyAre()
		{
			// A's first pixel is half covered; B's solid span over it is full, so C++ adds A's covers untouched
			// (add_cells) rather than scaling them by 255 / 256.
			Dictionary<int, int> a = Combine(SboolOp.Or, 0.5, 10, 100, 101);
			Dictionary<int, int> and = Combine(SboolOp.And, 0.5, 10, -5, 20);
			await Assert.That(a[0]).IsEqualTo(128);
			await Assert.That(and[0]).IsEqualTo(a[0]);
		}

		[Test]
		public async Task XorFormulasMatchCpp()
		{
			await Assert.That(ScanlineBooleanAlgebra.XorLinear(200, 100)).IsEqualTo(210);
			await Assert.That(ScanlineBooleanAlgebra.XorSaddle(255, 255)).IsEqualTo(0);
			await Assert.That(ScanlineBooleanAlgebra.XorSaddle(128, 0)).IsEqualTo(130);
			await Assert.That(ScanlineBooleanAlgebra.XorAbsDiff(100, 200)).IsEqualTo(100);
		}

		[Test]
		public async Task AdjacentSolidSpansOfOneCoverMerge()
		{
			// C++ scanline_p8::add_span extends the last span when it is solid, adjacent and of the same cover.
			var scanline = new ScanlineCachePacked8();
			scanline.reset(0, 100);
			scanline.add_span(10, 5, 200);
			scanline.add_span(15, 5, 200);
			scanline.add_span(20, 5, 100);
			await Assert.That(scanline.num_spans()).IsEqualTo(2);
			await Assert.That(scanline.begin().len).IsEqualTo(-10);
		}

		[Test]
		public async Task AddCellsReadsCoversFromItsIndex()
		{
			var scanline = new ScanlineCachePacked8();
			scanline.reset(0, 100);
			scanline.add_cells(10, 2, new byte[] { 1, 2, 30, 40 }, 2);
			ScanlineSpan span = scanline.begin();
			await Assert.That(scanline.GetCovers()[span.cover_index]).IsEqualTo((byte)30);
			await Assert.That(scanline.GetCovers()[span.cover_index + 1]).IsEqualTo((byte)40);
		}

		[Test]
		public async Task StoragesCombineAsGenerators()
		{
			// C++ scanline_boolean2 stores each shape, then combines the stores into a third store.
			var a = new ScanlineStorageBin();
			var b = new ScanlineStorageBin();
			Store(Rectangle(0, 10), new scanline_bin(), a);
			Store(Rectangle(5, 15), new scanline_bin(), b);
			var result = new ScanlineStorageBin();
			ScanlineBooleanAlgebra.CombineShapesBin(SboolOp.And, a, b, new scanline_bin(), new scanline_bin(), new scanline_bin(), result);

			var aa = new ScanlineStorageAa8();
			Store(Rectangle(0.5, 10), new scanline_unpacked_8(), aa);
			var sink = new RecordingSink();
			Store(aa, new ScanlineCachePacked8(), sink);

			await Assert.That(result.rewind_scanlines()).IsTrue();
			await Assert.That((result.min_x(), result.max_x(), result.min_y(), result.max_y())).IsEqualTo((5, 9, 0, 0));
			await Assert.That(sink.Covers[0]).IsEqualTo(128);
			await Assert.That(sink.Covers[9]).IsEqualTo(255);
		}

		private static void Store(IScanlineGenerator generator, IScanlineCache scanline, IScanlineSink sink)
		{
			if (generator.rewind_scanlines())
			{
				scanline.reset(generator.min_x(), generator.max_x());
				sink.prepare();
				while (generator.sweep_scanline(scanline))
				{
					sink.render(scanline);
				}
			}
		}

		private static Dictionary<int, int> Full(params int[] ranges)
		{
			var covers = new Dictionary<int, int>();
			for (int i = 0; i < ranges.Length; i += 2)
			{
				for (int x = ranges[i]; x < ranges[i + 1]; x++)
				{
					covers[x] = 255;
				}
			}

			return covers;
		}

		private static Dictionary<int, int> Combine(SboolOp op, double a1, double a2, double b1, double b2)
		{
			var sink = new RecordingSink();
			ScanlineBooleanAlgebra.CombineShapesAa(op, Rectangle(a1, a2), Rectangle(b1, b2), new ScanlineCachePacked8(), new ScanlineCachePacked8(), new ScanlineCachePacked8(), sink);
			return sink.Covers;
		}

		private static ScanlineRasterizer Rectangle(double x1, double x2)
		{
			var rasterizer = new ScanlineRasterizer();
			rasterizer.add_path(new RoundedRect(x1, 0, x2, 1, 0));
			return rasterizer;
		}

		private class RecordingSink : IScanlineSink
		{
			public Dictionary<int, int> Covers { get; } = new Dictionary<int, int>();

			public void prepare()
			{
			}

			public void render(IScanlineCache scanline)
			{
				ScanlineSpan span = scanline.begin();
				for (int i = 0; i < scanline.num_spans(); i++)
				{
					if (i > 0)
					{
						span = scanline.GetNextScanlineSpan();
					}

					for (int x = 0; x < System.Math.Abs(span.len); x++)
					{
						this.Covers[span.x + x] = scanline.GetCovers()[span.cover_index + (span.len < 0 ? 0 : x)];
					}
				}
			}
		}
	}
}
