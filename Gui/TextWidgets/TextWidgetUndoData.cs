//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.4
// Copyright (C) 2002-2005 Maxim Shemanarev (http://www.antigrain.com)
//
// C# Port port by: Lars Brubaker
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
	public class TextWidgetUndoData : IUndoData
	{
		private TextWidgetUndoData(String undoString, int charIndexToInsertBefore, int selectionIndexToStartBefore, bool selecting)
		{
			this.undoString = undoString;
			this.selecting = selecting;
			this.selectionIndexToStartBefore = selectionIndexToStartBefore;
			this.charIndexToInsertBefore = charIndexToInsertBefore;
		}

		internal TextWidgetUndoData(InternalTextEditWidget textEditWidget)
		{
			undoString = textEditWidget.Text;
			charIndexToInsertBefore = textEditWidget.CharIndexToInsertBefore;
			selectionIndexToStartBefore = textEditWidget.SelectionIndexToStartBefore;
			selecting = textEditWidget.Selecting;
		}

		public IUndoData Clone()
		{
			TextWidgetUndoData clonedUndoData = new TextWidgetUndoData(undoString, charIndexToInsertBefore, selectionIndexToStartBefore, selecting);
			return clonedUndoData;
		}

		private String undoString;
		private bool selecting;
		private int selectionIndexToStartBefore;
		private int charIndexToInsertBefore;

		internal void ExtractData(InternalTextEditWidget textEditWidget)
		{
			textEditWidget.Text = undoString;
			textEditWidget.CharIndexToInsertBefore = charIndexToInsertBefore;
			textEditWidget.SelectionIndexToStartBefore = selectionIndexToStartBefore;
			textEditWidget.Selecting = selecting;
		}
	}
}