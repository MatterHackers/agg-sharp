//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.4
// Copyright (C) 2002-2005 Maxim Shemanarev (http://www.antigrain.com)
//
// C# port by: Lars Brubaker
//                  larsbrubaker@gmail.com
// Copyright (C) 2007-2026
//
// Permission to copy, use, modify, sell and distribute this software
// is granted provided this copyright notice appears in all copies.
// This software is provided "as is" without express or implied
// warranty, and with no claim as to its suitability for any purpose.
//
//----------------------------------------------------------------------------
// Contact: mcseem@antigrain.com
//          mcseemagg@yahoo.com
//          http://www.antigrain.com
//----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// An opaque snapshot of an <see cref="UndoBuffer"/>'s undo and redo stacks, produced by
	/// <see cref="UndoBuffer.CaptureState"/> and consumed by <see cref="UndoBuffer.RestoreState"/>.
	/// Callers stash one of these while a nested edit level owns the buffer; the commands themselves
	/// are intentionally not exposed.
	/// </summary>
	public class UndoBufferState
	{
		// Both lists are stored oldest-first so RestoreState can simply push them back in order.
		internal UndoBufferState(IEnumerable<IUndoRedoCommand> undoOldestFirst, IEnumerable<IUndoRedoCommand> redoOldestFirst)
		{
			this.UndoOldestFirst = undoOldestFirst.ToList();
			this.RedoOldestFirst = redoOldestFirst.ToList();
		}

		internal List<IUndoRedoCommand> UndoOldestFirst { get; }

		internal List<IUndoRedoCommand> RedoOldestFirst { get; }
	}

	public class UndoBuffer
	{
		public event EventHandler Changed;

		private Stack<IUndoRedoCommand> redoBuffer = new Stack<IUndoRedoCommand>();

		private LimitStack<IUndoRedoCommand> undoBuffer = new LimitStack<IUndoRedoCommand>();

		private object locker = new object();

		private bool replaying;

		private void EnsureHistoryCanChange()
		{
			if (replaying) throw new InvalidOperationException("History cannot change while an undo or redo command is executing.");
		}

		public UndoBuffer()
		{
		}

		public int UndoCount => undoBuffer.Count;

		public int RedoCount => redoBuffer.Count;

		/// <summary>
		/// Returns the top undo command without removing it, or null if the undo stack is empty.
		/// </summary>
		public IUndoRedoCommand PeekUndo()
		{
			lock (locker)
			{
				return undoBuffer.Count > 0 ? undoBuffer.Peek() : null;
			}
		}

		/// <summary>
		/// Folds every command currently on the undo stack into a single FNV-1a hash, so that
		/// two different histories hash differently even when they end with the same command.
		/// Used for unsaved-changes tracking (compare a saved hash against the current one).
		/// </summary>
		/// <returns>0 for an empty undo stack, otherwise the chained hash of all undo commands.</returns>
		public ulong GetLongHashCode()
        {
			lock (locker)
			{
				if (UndoCount == 0)
				{
					return 0;
				}

				ulong longHash = 14695981039346656037;

				try
				{
					foreach (var undo in undoBuffer.Iterate())
					{
						// pass the running hash as the seed so each command contributes to the result
						longHash = undo.GetHashCode().GetLongHashCode(longHash);
					}
				}
				catch (Exception)
				{
				}

				return longHash;
			}
		}

		public int MaxUndos
		{
			get => undoBuffer.Limit;
			set
			{
				lock (locker)
				{
					EnsureHistoryCanChange();
					undoBuffer.Limit = value;
				}
			}
		}
        public string UndoName => undoBuffer.Count > 0 ? undoBuffer.Peek()?.Name : "None";

		public string RedoName => redoBuffer.Count > 0 ? redoBuffer.Peek()?.Name : "None";

        public void Add(IUndoRedoCommand command)
		{
			lock (locker)
			{
				EnsureHistoryCanChange();
				undoBuffer.Push(command);
				redoBuffer.Clear();
				Changed?.Invoke(this, null);
			}
		}

		public void AddAndDo(IUndoRedoCommand command)
		{
			lock (locker)
			{
				EnsureHistoryCanChange();
				undoBuffer.Push(command);
				redoBuffer.Clear();
				Changed?.Invoke(this, null);

				command.Do();
			}
		}

		/// <summary>Replays commands which stay on the redo stack if they reject execution.</summary>
		public void Redo(int redoCount = 1) => Replay(redoCount, true);

		/// <summary>Undoes commands which stay on the undo stack if they reject execution.</summary>
		public void Undo(int undoCount = 1) => Replay(undoCount, false);

		private void Replay(int count, bool redo)
		{
			lock (locker)
			{
				EnsureHistoryCanChange();
				replaying = true;
				var moved = false;
				var completed = false;
				try
				{
					for (var i = 0; i < count; i++)
					{
						if ((redo ? redoBuffer.Count : undoBuffer.Count) == 0) break;
						var command = redo ? redoBuffer.Peek() : undoBuffer.Peek();
						// Commands may reject stale or temporarily busy input. Their history remains
						// retryable; reentrant mutations cannot move a different command in its place.
						if (redo) command.Do(); else command.Undo();
						if (redo) { redoBuffer.Pop(); undoBuffer.Push(command); }
						else { undoBuffer.Pop(); redoBuffer.Push(command); }
						moved = true;
					}
					completed = true;
				}
				finally
				{
					replaying = false;
					// A later command can fail after earlier transfers succeeded. Report those,
					// with normal subscriber behavior restored, even as the exception propagates.
					if (moved || completed) Changed?.Invoke(this, null);
				}
			}
		}

		/// <summary>
		/// Takes a snapshot of the current undo and redo stacks that can later be handed to
		/// <see cref="RestoreState"/>. Used to park one edit level's history while a nested level
		/// runs on this same buffer instance - the instance must not be swapped because the UI
		/// captures the reference and subscribes to <see cref="Changed"/>.
		/// </summary>
		public UndoBufferState CaptureState()
		{
			lock (locker)
			{
				// LimitStack.Iterate() yields oldest-first, while Stack<T> enumerates newest-first,
				// so the redo stack has to be reversed to match the snapshot's oldest-first contract.
				return new UndoBufferState(undoBuffer.Iterate(), redoBuffer.Reverse());
			}
		}

		/// <summary>
		/// Replaces the undo and redo stacks with the contents of <paramref name="state"/>, discarding
		/// whatever history the buffer currently holds (this is a replace, never a merge).
		/// </summary>
		public void RestoreState(UndoBufferState state)
		{
			if (state == null)
			{
				throw new ArgumentNullException(nameof(state));
			}

			lock (locker)
			{
				EnsureHistoryCanChange();
				undoBuffer.Clear();
				foreach (var command in state.UndoOldestFirst)
				{
					undoBuffer.Push(command);
				}

				redoBuffer.Clear();
				foreach (var command in state.RedoOldestFirst)
				{
					redoBuffer.Push(command);
				}
			}

			// Raised outside the lock because subscribers are UI (undo/redo buttons, dirty indicators)
			// that read back from this buffer while handling the event.
			Changed?.Invoke(this, null);
		}

		public void ClearHistory()
		{
			lock (locker)
			{
				EnsureHistoryCanChange();
				undoBuffer.Clear();
				redoBuffer.Clear();
				Changed?.Invoke(this, null);
			}
		}
	}
}