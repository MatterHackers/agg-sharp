//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.4
// Copyright (C) 2002-2005 Maxim Shemanarev (http://www.antigrain.com)
//
// C# port by: Lars Brubaker
//                  larsbrubaker@gmail.com
// Copyright (C) 2007
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
using System.Collections.Generic;

namespace MatterHackers.Agg.UI
{
	public class UndoBuffer
	{
		private Stack<IUndoRedoCommand> redoBuffer = new Stack<IUndoRedoCommand>();

		private Stack<IUndoRedoCommand> undoBuffer = new Stack<IUndoRedoCommand>();

		public UndoBuffer()
		{
		}

		public void Add(IUndoRedoCommand command)
		{
			undoBuffer.Push(command);
			redoBuffer.Clear();
		}

		public void Redo(int redoCount = 1)
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
		}

		public void Undo(int undoCount = 1)
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
		}

		internal void ClearHistory()
		{
			undoBuffer.Clear();
			redoBuffer.Clear();
		}
	}

	public interface IUndoRedoCommand
	{
		void Do();

		void Undo();
	}
}