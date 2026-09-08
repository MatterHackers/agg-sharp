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
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	// CaptureState/RestoreState exist so an editor can drill into a nested edit level with a clean
	// history and then come back to exactly the history it left, all without swapping the UndoBuffer
	// instance (toolbars capture the reference and subscribe to Changed).
	public class UndoBufferTests
	{
		// A command that records nothing but its name - these tests care about stack contents and
		// ordering, not about what the commands do.
		private class NamedCommand : IUndoRedoCommand
		{
			public NamedCommand(string name)
			{
				this.Name = name;
			}

			public string Name { get; }

			public int DoCount { get; private set; }

			public int UndoCount { get; private set; }

			public void Do() => DoCount++;

			public void Undo() => UndoCount++;
		}

		private sealed class CallbackCommand : IUndoRedoCommand
		{
			public string Name => "retry";
			internal Action Apply = () => { };
			public void Do() => Apply();
			public void Undo() => Apply();
		}

		[Test]
		[Arguments(false)]
		[Arguments(true)]
		public async Task RejectedReplayPreservesHistoryAndCanBeRetried(bool redo)
		{
			var buffer = new UndoBuffer();
			var command = new CallbackCommand();
			buffer.Add(new NamedCommand("older"));
			buffer.Add(command);
			if (redo) buffer.Undo();
			var undo = buffer.UndoCount; var redoCount = buffer.RedoCount;
			var hash = buffer.GetLongHashCode(); var notifications = 0;
			buffer.Changed += (_, _) => notifications++;
			command.Apply = () => throw new InvalidOperationException("Not ready");
			var rejected = false;
			try { if (redo) buffer.Redo(); else buffer.Undo(); }
			catch (InvalidOperationException) { rejected = true; }
			await Assert.That(rejected).IsTrue();
			await Assert.That(buffer.UndoCount).IsEqualTo(undo);
			await Assert.That(buffer.RedoCount).IsEqualTo(redoCount);
			await Assert.That(buffer.GetLongHashCode()).IsEqualTo(hash);
			await Assert.That(notifications).IsEqualTo(0);
			var applied = 0;
			command.Apply = () => applied++;
			if (redo) buffer.Redo(); else buffer.Undo();
			await Assert.That(applied).IsEqualTo(1);
			await Assert.That(buffer.UndoCount).IsEqualTo(undo + (redo ? 1 : -1));
			await Assert.That(buffer.RedoCount).IsEqualTo(redoCount + (redo ? -1 : 1));
			await Assert.That(notifications).IsEqualTo(1);
			await Assert.That(DrainUndoNames(buffer)).IsEqualTo(redo ? "retry,older" : "older");
		}

		[Test]
		[Arguments(false)]
		[Arguments(true)]
		public async Task ReplayRejectsReentrantHistoryMutationWithoutLosingEitherStack(bool nestedUndo)
		{
			var buffer = new UndoBuffer();
			var command = new CallbackCommand();
			buffer.Add(new NamedCommand("older")); buffer.Add(command);
			var state = buffer.CaptureState();
			command.Apply = () => { if (nestedUndo) buffer.Undo(); else buffer.Add(new NamedCommand("intruder")); };
			var rejected = false;
			try { buffer.Undo(); } catch (InvalidOperationException) { rejected = true; }
			await Assert.That(rejected).IsTrue();
			await Assert.That(buffer.UndoCount).IsEqualTo(2);
			await Assert.That(buffer.RedoCount).IsEqualTo(0);
			await Assert.That(buffer.UndoName).IsEqualTo("retry");
			command.Apply = () => { };
			buffer.Undo();
			await Assert.That(buffer.UndoName).IsEqualTo("older");
			await Assert.That(buffer.RedoName).IsEqualTo("retry");
		}

		[Test]
		public async Task PartialReplayNotifiesCompletedTransfersBeforeLaterRejection()
		{
			var buffer = new UndoBuffer();
			var failing = new CallbackCommand { Apply = () => throw new InvalidOperationException("Not ready") };
			buffer.Add(failing); buffer.Add(new NamedCommand("first"));
			var notifications = 0;
			buffer.Changed += (_, _) => { notifications++; buffer.MaxUndos = 10; };
			try { buffer.Undo(2); } catch (InvalidOperationException) { }
			await Assert.That(buffer.UndoName).IsEqualTo("retry");
			await Assert.That(buffer.RedoName).IsEqualTo("first");
			await Assert.That(notifications).IsEqualTo(1);
			failing.Apply = () => { };
			buffer.Undo();
			await Assert.That(buffer.RedoName).IsEqualTo("retry");
			await Assert.That(notifications).IsEqualTo(2);
		}

		// Returned as a single top-to-bottom string so the assertion is unambiguously order sensitive.
		private static string DrainUndoNames(UndoBuffer buffer)
		{
			var names = new List<string>();
			while (buffer.UndoCount > 0)
			{
				names.Add(buffer.UndoName);
				buffer.Undo();
			}

			return string.Join(",", names);
		}

		[Test]
		public async Task RestoreStateRebuildsUndoAndRedoStacksInOrder()
		{
			var buffer = new UndoBuffer();
			buffer.Add(new NamedCommand("one"));
			buffer.Add(new NamedCommand("two"));
			buffer.Add(new NamedCommand("three"));
			buffer.Add(new NamedCommand("four"));

			// Undo twice so both stacks are non-empty at capture time.
			buffer.Undo();
			buffer.Undo();

			await Assert.That(buffer.UndoCount).IsEqualTo(2);
			await Assert.That(buffer.RedoCount).IsEqualTo(2);

			var state = buffer.CaptureState();

			// Simulate a nested edit level: clear everything and do unrelated work.
			buffer.ClearHistory();
			buffer.Add(new NamedCommand("nested"));
			buffer.Undo();

			buffer.RestoreState(state);

			await Assert.That(buffer.UndoCount).IsEqualTo(2);
			await Assert.That(buffer.RedoCount).IsEqualTo(2);
			await Assert.That(buffer.UndoName).IsEqualTo("two")
				.Because("the newest undo at capture time must still be on top after restore");
			await Assert.That(buffer.RedoName).IsEqualTo("three")
				.Because("the next redo at capture time must still be on top after restore");

			// Walking the whole undo stack catches a reversed-order copy that a Count check would miss.
			await Assert.That(DrainUndoNames(buffer)).IsEqualTo("two,one");
		}

		[Test]
		public async Task RestoreStateReproducesTheCaptureTimeHash()
		{
			// MatterCAD's HasUnsavedChanges compares GetLongHashCode() against the hash recorded at the
			// last save, so a restored buffer that hashes differently would report a false "dirty".
			var buffer = new UndoBuffer();
			buffer.Add(new NamedCommand("one"));
			buffer.Add(new NamedCommand("two"));
			buffer.Add(new NamedCommand("three"));
			buffer.Undo();

			var hashAtCapture = buffer.GetLongHashCode();
			var state = buffer.CaptureState();

			buffer.ClearHistory();
			buffer.Add(new NamedCommand("nested"));

			await Assert.That(buffer.GetLongHashCode()).IsNotEqualTo(hashAtCapture)
				.Because("unrelated work must change the hash, or this test could not detect a bad restore");

			buffer.RestoreState(state);

			await Assert.That(buffer.GetLongHashCode()).IsEqualTo(hashAtCapture);
		}

		[Test]
		public async Task RestoreStateRaisesChanged()
		{
			var buffer = new UndoBuffer();
			buffer.Add(new NamedCommand("one"));
			var state = buffer.CaptureState();
			buffer.ClearHistory();

			int changedCount = 0;
			buffer.Changed += (s, e) => changedCount++;

			buffer.RestoreState(state);

			await Assert.That(changedCount).IsEqualTo(1)
				.Because("undo/redo buttons and dirty indicators only refresh when Changed fires");
		}

		[Test]
		public async Task RestoreStateReplacesRatherThanMergesExistingHistory()
		{
			var buffer = new UndoBuffer();
			buffer.Add(new NamedCommand("captured"));
			var state = buffer.CaptureState();

			buffer.ClearHistory();
			buffer.Add(new NamedCommand("other one"));
			buffer.Add(new NamedCommand("other two"));
			buffer.Undo();

			buffer.RestoreState(state);

			await Assert.That(buffer.UndoCount).IsEqualTo(1);
			await Assert.That(buffer.RedoCount).IsEqualTo(0)
				.Because("the restored state had an empty redo stack, so the live redo stack must be discarded");
			await Assert.That(DrainUndoNames(buffer)).IsEqualTo("captured");
		}

		[Test]
		public async Task RestoreStateLeavesMaxUndosAlone()
		{
			var buffer = new UndoBuffer { MaxUndos = 5 };
			buffer.Add(new NamedCommand("one"));

			var state = buffer.CaptureState();
			buffer.ClearHistory();
			buffer.RestoreState(state);

			await Assert.That(buffer.MaxUndos).IsEqualTo(5)
				.Because("the undo limit is a buffer setting, not part of the captured history");
		}
	}
}
