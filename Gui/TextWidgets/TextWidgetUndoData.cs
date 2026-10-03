//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.4
// Copyright (C) 2002-2005 Maxim Shemanarev (http://www.antigrain.com)
//
// C# port by: Lars Brubaker
//                  larsbrubaker@gmail.com
// Copyright (C) 2026
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
using MatterHackers.Localizations;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// One edit of a text field, held as the field's state on either side of it: undo puts back the state before,
	/// redo the state after.
	/// </summary>
	/// <remarks>
	/// It used to hold only the state after the edit and restore that same state for both undo and redo, so the
	/// first undo after any edit put back what was already showing and redo put back what undo had just shown.
	/// </remarks>
	public class TextWidgetUndoCommand : IUndoRedoCommand
	{
		private readonly InternalTextEditWidget textEditWidget;
		private readonly TextEditState before;
		private readonly TextEditState after;

		internal TextWidgetUndoCommand(InternalTextEditWidget textEditWidget, TextEditState before, TextEditState after)
		{
			this.textEditWidget = textEditWidget;
			this.before = before;
			this.after = after;
		}

		public string Name => "Text Change".Localize();

		public void Do()
		{
			after.RestoreTo(textEditWidget);
		}

		public void Undo()
		{
			before.RestoreTo(textEditWidget);
		}
	}

	/// <summary>The text, caret and selection of a field at one moment - what an undo step goes back to.</summary>
	internal sealed class TextEditState
	{
		private readonly string text;
		private readonly int charIndexToInsertBefore;
		private readonly int selectionIndexToStartBefore;
		private readonly bool selecting;

		private TextEditState(InternalTextEditWidget textEditWidget)
		{
			text = textEditWidget.GetActualText();
			charIndexToInsertBefore = textEditWidget.CharIndexToInsertBefore;
			selectionIndexToStartBefore = textEditWidget.SelectionIndexToStartBefore;
			selecting = textEditWidget.Selecting;
		}

		public static TextEditState Capture(InternalTextEditWidget textEditWidget) => new TextEditState(textEditWidget);

		public void RestoreTo(InternalTextEditWidget textEditWidget)
		{
			textEditWidget.SetActualTextAndUpdate(text);
			textEditWidget.CharIndexToInsertBefore = charIndexToInsertBefore;
			textEditWidget.SelectionIndexToStartBefore = selectionIndexToStartBefore;
			textEditWidget.Selecting = selecting;
		}
	}
}
