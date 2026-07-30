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

using System.Threading.Tasks;
using MatterHackers.Agg;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Agg.Tests.Agg
{
	/// <summary>
	/// Covers the cell accumulation invariant of <see cref="RasterizerCellsAa"/>: for a single
	/// <c>line()</c> call the sum of <c>cover</c> over every emitted cell must telescope to exactly
	/// <c>y2 - y1</c> in subpixel units. That single scalar catches the whole class of bugs where a
	/// segment gets traversed more than once (missing <c>return</c> after recursive subdivision) or
	/// where 32-bit overflow makes the subdivision guard or the midpoint garbage. The reference
	/// behaviour is the Rust port (<c>agg-rust\src\rasterizer_cells_aa.rs</c>), which does this
	/// arithmetic in i64.
	/// </summary>
	public class RasterizerCellsAaTests
	{
		// 16384 << 8: the point at which line() must subdivide rather than traverse.
		private const int DxLimit = 16384 << 8;

		/// <summary>
		/// Baseline: an ordinary short segment already satisfies the cover-sum invariant, in both
		/// y directions. Guards the helper itself so a failure in the other tests means the
		/// rasterizer, not the measurement.
		/// </summary>
		[Test]
		public async Task ShortSegmentCoverSum()
		{
			await Assert.That(CoverForLine(0, 0, 100 << 8, 10 << 8)).IsEqualTo(10 << 8);
			await Assert.That(CoverForLine(0, 10 << 8, 100 << 8, 0)).IsEqualTo(-(10 << 8));
		}

		/// <summary>
		/// A segment wider than the subdivision limit must be rendered by the two recursive halves
		/// only. Without the <c>return</c> after subdividing, the original full traversal runs as
		/// well and the accumulated cover comes out a multiple of the true value.
		/// </summary>
		[Test]
		public async Task LongSegmentCoverIsNotDoubled()
		{
			await Assert.That(CoverForLine(0, 0, 3 * DxLimit, 10 << 8)).IsEqualTo(10 << 8);
		}

		/// <summary>
		/// Pins the <c>&gt;=</c> in the subdivision guard: a <c>dx</c> of exactly the limit subdivides,
		/// one subunit less traverses directly, and both must land on the same cover sum.
		/// </summary>
		[Test]
		public async Task SubdivisionBoundaryCoverSum()
		{
			await Assert.That(CoverForLine(0, 0, DxLimit, 10 << 8)).IsEqualTo(10 << 8);
			await Assert.That(CoverForLine(0, 0, DxLimit - 1, 10 << 8)).IsEqualTo(10 << 8);
		}

		/// <summary>
		/// The widest representable segment, in both x directions. Computing <c>dx</c> or the
		/// subdivision midpoint in 32 bits wraps here (int.MaxValue - int.MinValue == -1), which skips
		/// subdivision entirely and leaves the traversal walking a wrapped cell range. The reversed
		/// case additionally exercises the <c>dx &lt; 0</c> branches, the negative-remainder
		/// corrections at extreme magnitude, and midpoint rounding of a negative sum.
		/// </summary>
		/// <remarks>
		/// A regression of the 32-bit <c>dx</c> bug shows up as a <b>hang</b>, not a failed assertion:
		/// the wrapped cell range walks ~16.7M columns while the cell array grows 4096 at a time with
		/// a full copy each step. If this test stops reporting rather than failing, that is the bug,
		/// not a flake.
		/// </remarks>
		[Test]
		public async Task ExtremeCoordinatesDoNotOverflow()
		{
			await Assert.That(CoverForLine(int.MinValue, 0, int.MaxValue, 256)).IsEqualTo(256);
			await Assert.That(CoverForLine(int.MaxValue, 0, int.MinValue, 256)).IsEqualTo(256);
		}

		private static long CoverForLine(int x1, int y1, int x2, int y2)
		{
			var ras = new RasterizerCellsAa();
			ras.line(x1, y1, x2, y2);
			ras.sort_cells();
			return TotalCover(ras);
		}

		/// <summary>
		/// Sums <c>cover</c> over every sorted cell. Must be called after <c>sort_cells()</c>, which
		/// is also what flushes the in-progress cell.
		/// </summary>
		private static long TotalCover(RasterizerCellsAa ras)
		{
			if (ras.total_cells() == 0)
			{
				// No cells were emitted, so min_y/max_y are still at their sentinel (min_y > max_y).
				return 0;
			}

			long total = 0;
			for (int y = ras.min_y(); y <= ras.max_y(); y++)
			{
				int numCells = ras.scanline_num_cells(y);
				if (numCells == 0)
				{
					continue;
				}

				ras.scanline_cells(y, out PixelCellAa[] cells, out int offset);
				for (int i = offset; i < offset + numCells; i++)
				{
					total += cells[i].cover;
				}
			}

			return total;
		}
	}
}
