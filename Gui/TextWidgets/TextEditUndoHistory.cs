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

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// The undo history of one <see cref="InternalTextEditWidget"/>. The field calls <see cref="RecordEdit"/> after
	/// each edit; that pairs the state left by the previous edit (or by the last undo, redo or reset) with the state
	/// now, so each step undoes to exactly where the one before it left off.
	/// </summary>
	/// <remarks>
	/// The "before" side is the state at the last recording rather than the moment just before the edit, so a caret
	/// moved without editing in between is not restored by undo - undo goes back to where the previous edit left the
	/// caret, as it did before this class existed.
	/// </remarks>
	internal class TextEditUndoHistory
	{
		private readonly InternalTextEditWidget owner;
		private readonly UndoBuffer undoBuffer = new UndoBuffer();
		private TextEditState lastRecorded;

		public TextEditUndoHistory(InternalTextEditWidget owner)
		{
			this.owner = owner;
			lastRecorded = TextEditState.Capture(owner);
		}

		/// <summary>Makes the edit the field has just made one undo step.</summary>
		public void RecordEdit()
		{
			var now = TextEditState.Capture(owner);
			undoBuffer.Add(new TextWidgetUndoCommand(owner, lastRecorded, now));
			lastRecorded = now;
		}

		public void Undo()
		{
			undoBuffer.Undo();
			lastRecorded = TextEditState.Capture(owner);
		}

		public void Redo()
		{
			undoBuffer.Redo();
			lastRecorded = TextEditState.Capture(owner);
		}

		/// <summary>
		/// Takes a change that is not its own undo step (a program setting the text) as where the next step starts,
		/// so undoing that next step goes back to the program's text rather than past it.
		/// </summary>
		public void AcceptUnrecordedChange()
		{
			lastRecorded = TextEditState.Capture(owner);
		}

		/// <summary>Forgets every step; the field's current state becomes where undo stops.</summary>
		public void Clear()
		{
			undoBuffer.ClearHistory();
			lastRecorded = TextEditState.Capture(owner);
		}
	}
}
