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
	public class TextWidgetUndoData : IUndoRedoCommand
	{
		private int charIndexToInsertBefore;
		private bool selecting;
		private int selectionIndexToStartBefore;
		private InternalTextEditWidget textEditWidget;
		private String undoString;

		internal TextWidgetUndoData(InternalTextEditWidget textEditWidget)
		{
			this.textEditWidget = textEditWidget;
			undoString = textEditWidget.Text;
			charIndexToInsertBefore = textEditWidget.CharIndexToInsertBefore;
			selectionIndexToStartBefore = textEditWidget.SelectionIndexToStartBefore;
			selecting = textEditWidget.Selecting;
		}

		public void Do()
		{
			ExtractData();
		}

		public void Undo()
		{
			ExtractData();
		}

		internal void ExtractData()
		{
			textEditWidget.Text = undoString;
			textEditWidget.CharIndexToInsertBefore = charIndexToInsertBefore;
			textEditWidget.SelectionIndexToStartBefore = selectionIndexToStartBefore;
			textEditWidget.Selecting = selecting;
		}
	}
}