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
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	public class UndoBufferLongHashCodeTests
	{
		/// <summary>
		/// A do-nothing command; identity (reference) hashing is all the undo buffer hash relies on.
		/// </summary>
		private class NoOpCommand : IUndoRedoCommand
		{
			public NoOpCommand(string name)
			{
				this.Name = name;
			}

			public string Name { get; }

			public void Do()
			{
			}

			public void Undo()
			{
			}
		}

		[Test]
		public async Task EmptyBufferHashesToZero()
		{
			var buffer = new UndoBuffer();

			await Assert.That(buffer.GetLongHashCode()).IsEqualTo(0ul);
		}

		[Test]
		public async Task UndoAndRedoRoundTripTheHash()
		{
			// this is the property InteractiveScene.HasUnsavedChanges relies on: undoing back to
			// the state a save was taken in must reproduce the hash captured at that save point
			var buffer = new UndoBuffer();
			buffer.Add(new NoOpCommand("a"));
			buffer.Add(new NoOpCommand("b"));

			var savePointHash = buffer.GetLongHashCode();

			buffer.Add(new NoOpCommand("c"));
			var afterEditHash = buffer.GetLongHashCode();
			await Assert.That(afterEditHash).IsNotEqualTo(savePointHash);

			buffer.Undo();
			await Assert.That(buffer.GetLongHashCode()).IsEqualTo(savePointHash);

			buffer.Redo();
			await Assert.That(buffer.GetLongHashCode()).IsEqualTo(afterEditHash);
		}

		[Test]
		public async Task HistoriesEndingInTheSameCommandHashDifferently()
		{
			// the same command instance is pushed into both buffers, so its GetHashCode() is identical
			var sharedCommand = new NoOpCommand("shared");

			var twoCommandBuffer = new UndoBuffer();
			twoCommandBuffer.Add(new NoOpCommand("leading"));
			twoCommandBuffer.Add(sharedCommand);

			var oneCommandBuffer = new UndoBuffer();
			oneCommandBuffer.Add(sharedCommand);

			// the hash must fold in every command, not just the last one iterated
			await Assert.That(twoCommandBuffer.GetLongHashCode()).IsNotEqualTo(oneCommandBuffer.GetLongHashCode());
		}
	}
}
