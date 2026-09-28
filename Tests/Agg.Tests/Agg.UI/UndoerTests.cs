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

using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	// agg-gui's undo/undoer.rs tests: the time-coalescing snapshot history behind the Undo Redo demo.
	public class UndoerTests
	{
		[Test]
		public async Task FirstFeedCreatesABaselineWithNoUndo()
		{
			var undoer = new Undoer<int>();
			undoer.FeedState(0, 0);
			await Assert.That(undoer.HasUndo(0)).IsFalse();
			await Assert.That(undoer.HasRedo(0)).IsFalse();
		}

		[Test]
		public async Task StableTimeCoalescesRapidChangesIntoOnePoint()
		{
			var undoer = new Undoer<int>();
			undoer.FeedState(0, 0);

			undoer.FeedState(0.1, 1);
			await Assert.That(undoer.IsInFlux).IsTrue();
			undoer.FeedState(0.2, 2);
			undoer.FeedState(0.3, 3);

			// 0.4 arms the stability timer; 1.1 s later the settled state is kept.
			undoer.FeedState(0.4, 3);
			undoer.FeedState(1.5, 3);
			await Assert.That(undoer.HasUndo(3)).IsTrue();
			await Assert.That(undoer.IsInFlux).IsFalse();

			await Assert.That(undoer.Undo(3, out int previous)).IsTrue();
			await Assert.That(previous).IsEqualTo(0);
			await Assert.That(undoer.HasUndo(0)).IsFalse();
		}

		[Test]
		public async Task UndoThenRedoRoundTrips()
		{
			var undoer = new Undoer<int>();
			undoer.FeedState(0, 0);
			undoer.FeedState(0.1, 5);
			undoer.FeedState(0.2, 5);
			undoer.FeedState(2.0, 5);
			await Assert.That(undoer.HasUndo(5)).IsTrue();

			await Assert.That(undoer.Undo(5, out int reverted)).IsTrue();
			await Assert.That(reverted).IsEqualTo(0);
			await Assert.That(undoer.HasRedo(0)).IsTrue();

			await Assert.That(undoer.Redo(0, out int advanced)).IsTrue();
			await Assert.That(advanced).IsEqualTo(5);
			await Assert.That(undoer.HasRedo(5)).IsFalse();
		}

		[Test]
		public async Task AutoSaveIntervalForcesAPointDuringContinuousChange()
		{
			var undoer = new Undoer<int>();
			undoer.FeedState(0, 0);
			for (int i = 1; i <= 40; i++)
			{
				undoer.FeedState(i, i);
			}

			await Assert.That(undoer.HasUndo(40)).IsTrue();
		}

		[Test]
		public async Task ChangingTheStateClearsRedos()
		{
			var undoer = new Undoer<int>();
			undoer.FeedState(0, 0);
			undoer.FeedState(0.1, 1);
			undoer.FeedState(2.0, 1);
			undoer.Undo(1, out _);
			await Assert.That(undoer.HasRedo(0)).IsTrue();

			undoer.FeedState(2.1, 9);
			await Assert.That(undoer.HasRedo(9)).IsFalse();
		}

		[Test]
		public async Task UndoingAnUnsavedChangeReturnsToTheLatestPointAndCanBeRedone()
		{
			var undoer = new Undoer<int>();
			undoer.FeedState(0, 0);
			undoer.FeedState(0.1, 7);

			await Assert.That(undoer.Undo(7, out int previous)).IsTrue();
			await Assert.That(previous).IsEqualTo(0);
			await Assert.That(undoer.IsInFlux).IsFalse();
			await Assert.That(undoer.Redo(0, out int next)).IsTrue();
			await Assert.That(next).IsEqualTo(7);
		}

		[Test]
		public async Task KeepsAtMostMaxUndos()
		{
			var undoer = new Undoer<int> { MaxUndos = 2 };
			undoer.AddUndo(1);
			undoer.AddUndo(2);
			undoer.AddUndo(3);

			undoer.Undo(3, out int previous);
			await Assert.That(previous).IsEqualTo(2);
			await Assert.That(undoer.HasUndo(2)).IsFalse();
		}
	}
}
