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
using System;

namespace MatterHackers.Agg.UI
{
    public class DoUndoActions : IUndoRedoCommand
    {
        private Action doAction;
        private Action undoAction;
        public DoUndoActions(string name, Action doAction, Action undoAction)
        {
            this.doAction = doAction;
            this.Name = name;
            this.undoAction = undoAction;
        }

        public string Name { get; }
        public void Do() => doAction?.Invoke();

        public void Undo() => undoAction?.Invoke();
    }
}