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
						longHash = undo.GetHashCode().GetLongHashCode();
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
			set => undoBuffer.Limit = value;
		}
        public string UndoName => undoBuffer.Count > 0 ? undoBuffer.Peek()?.Name : "None";

		public string RedoName => redoBuffer.Count > 0 ? redoBuffer.Peek()?.Name : "None";

        public void Add(IUndoRedoCommand command)
		{
			lock (locker)
			{
				undoBuffer.Push(command);
				redoBuffer.Clear();
				Changed?.Invoke(this, null);
			}
		}

		public void AddAndDo(IUndoRedoCommand command)
		{
			lock (locker)
			{
				undoBuffer.Push(command);
				redoBuffer.Clear();
				Changed?.Invoke(this, null);

				command.Do();
			}
		}

		public void Redo(int redoCount = 1)
		{
			lock (locker)
			{
				for (int i = 1; i <= redoCount; i++)
				{
					if (redoBuffer.Count != 0)
					{
						IUndoRedoCommand command = redoBuffer.Pop();
						command.Do();
						undoBuffer.Push(command);
					}
				}
				Changed?.Invoke(this, null);
			}
		}

		public void Undo(int undoCount = 1)
		{
			lock (locker)
			{
				for (int i = 1; i <= undoCount; i++)
				{
					if (undoBuffer.Count != 0)
					{
						IUndoRedoCommand command = undoBuffer.Pop();
						command.Undo();
						redoBuffer.Push(command);
					}
				}
				Changed?.Invoke(this, null);
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
				undoBuffer.Clear();
				redoBuffer.Clear();
				Changed?.Invoke(this, null);
			}
		}
	}
}