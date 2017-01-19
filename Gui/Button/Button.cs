using MatterHackers.VectorMath;

//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.4
// Copyright (C) 2007 Lars Brubaker
//                  larsbrubaker@gmail.com
//
// Permission to copy, use, modify, sell and distribute this software
// is granted provided this copyright notice appears in all copies.
// This software is provided "as is" without express or implied
// warranty, and with no claim as to its suitability for any purpose.
//
// classes ButtonWidget
//
//----------------------------------------------------------------------------
using System;

namespace MatterHackers.Agg.UI
{
	public class Button : ButtonBase
	{
		public static BorderDouble DefaultMargin = new BorderDouble(3);

		public Button(GuiWidget buttonView)
			: this(0, 0, buttonView)
		{
		}

		public Button(string buttonText, double x = 0, double y = 0)
			: this(x, y, new ButtonViewText(buttonText))
		{
		}

		public Button(double x = 0, double y = 0, GuiWidget buttonView = null)
			: base(x, y)
		{
			Margin = DefaultMargin;

			OriginRelativeParent = new Vector2(x, y);

			if (buttonView != null)
			{
				buttonView.Selectable = false;

				SuspendLayout();
				AddChild(buttonView);
				ResumeLayout();

				FixBoundsAndChildrenPositions();

				MinimumSize = new Vector2(Width, Height);
			}
		}
	}
}