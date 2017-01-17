using MatterHackers.VectorMath;

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
//
// classes cbox_ctrl
//
//----------------------------------------------------------------------------
using System;

namespace MatterHackers.Agg.UI
{
	public class CheckBox : ButtonBase
	{
		public static BorderDouble DefaultMargin;// = new BorderDouble(3);

		public event EventHandler CheckedStateChanged;

		private bool isChecked = false;

		public CheckBox(GuiWidget buttonView)
			: this(0, 0, buttonView)
		{
		}

		public CheckBox(double x, double y, GuiWidget checkBoxButtonView)
			: base(x, y)
		{
			Margin = DefaultMargin;

			OriginRelativeParent = new Vector2(x, y);

			if (checkBoxButtonView != null)
			{
				checkBoxButtonView.Selectable = false;

				AddChild(checkBoxButtonView);

				SetBoundsToEncloseChildren();

				if (LocalBounds.Left != 0 || LocalBounds.Bottom != 0)
				{
					// let's make sure that a button has 0, 0 at the lower left
					// move the children so they will fit with 0, 0 at the lower left
					foreach (GuiWidget child in Children)
					{
						child.OriginRelativeParent = child.OriginRelativeParent + new Vector2(-LocalBounds.Left, -LocalBounds.Bottom);
					}

					SetBoundsToEncloseChildren();
				}

				MinimumSize = new Vector2(Width, Height);
			}

			Click += CheckBox_Click;
		}

		public CheckBox(double x, double y, string label, double textSize = 12)
			: this(x, y, new CheckBoxViewText(label, textSize))
		{
		}

		public CheckBox(string label)
			: this(0, 0, label)
		{
		}

		public CheckBox(string label, RGBA_Bytes textColor, double textSize = 12)
			: this(0, 0, label, textSize)
		{
			TextColor = textColor;
		}

		public RGBA_Bytes TextColor
		{
			get
			{
				CheckBoxViewText child = Children[0] as CheckBoxViewText;
				if (child != null)
				{
					return child.TextColor;
				}

				return RGBA_Bytes.Black;
			}

			set
			{
				CheckBoxViewText child = Children[0] as CheckBoxViewText;
				if (child != null)
				{
					child.TextColor = value;
				}
			}
		}

		private void CheckBox_Click(object sender, EventArgs mouseEvent)
		{
			Checked = !Checked;
		}

		public bool Checked
		{
			get
			{
				return isChecked;
			}

			set
			{
				if (isChecked != value)
				{
					isChecked = value;
					OnCheckStateChanged(null);
					Invalidate();
				}
			}
		}

		public virtual void OnCheckStateChanged(EventArgs e)
		{
			if (CheckedStateChanged != null)
			{
				CheckedStateChanged(this, e);
			}
		}
	}
}