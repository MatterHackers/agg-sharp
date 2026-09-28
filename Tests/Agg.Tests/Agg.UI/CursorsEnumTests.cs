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
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// <see cref="Cursors"/> is consumed by number outside this repo, so its existing members must never move.
	/// A member inserted rather than appended would shift every value after it and silently change the
	/// cursor every such consumer shows.
	/// </summary>
	public class CursorsEnumTests
	{
		[Test]
		[Arguments(Cursors.Arrow, 0)]
		[Arguments(Cursors.Cross, 1)]
		[Arguments(Cursors.Default, 2)]
		[Arguments(Cursors.Hand, 3)]
		[Arguments(Cursors.IBeam, 6)]
		[Arguments(Cursors.SizeAll, 19)]
		[Arguments(Cursors.SizeWE, 23)]
		[Arguments(Cursors.VSplit, 25)]
		[Arguments(Cursors.WaitCursor, 26)]
		public async Task TheOriginalMembersKeepTheirValues(Cursors cursor, int expected)
		{
			await Assert.That((int)cursor).IsEqualTo(expected);
		}

		/// <summary>Everything added since the original set sits after it, with no gaps.</summary>
		[Test]
		public async Task TheAddedMembersComeAfterTheOriginalSet()
		{
			Cursors[] all = Enum.GetValues<Cursors>();

			await Assert.That((int)Cursors.None).IsEqualTo(27);
			await Assert.That((int)Cursors.ResizeSouthWest).IsEqualTo(all.Length - 1);
		}
	}
}
