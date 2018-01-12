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
using System.Collections.ObjectModel;
using System.Linq;
using System.Collections.Generic;

namespace MatterHackers.Agg.UI
{
	public interface ICheckbox
	{
		event EventHandler CheckedStateChanged;
		bool Checked { get; set; }
	}

	public interface IRadioButton: ICheckbox
	{
		IEnumerable<GuiWidget> SiblingRadioButtonList { get; set; }
	}

	public class RadioButton : GuiWidget, IRadioButton
	{
		public event EventHandler CheckedStateChanged;

		private bool isChecked = false;

		public static BorderDouble defaultMargin = new BorderDouble(5);

		public IEnumerable<GuiWidget> SiblingRadioButtonList { get; set; }

		public RadioButton(double x, double y, GuiWidget view)
		{
			Margin = defaultMargin;

			OriginRelativeParent = new Vector2(x, y);

			if (view != null)
			{
				view.Selectable = false;

				SuspendLayout();
				AddChild(view);
				ResumeLayout();

				FixBoundsAndChildrenPositions();

				MinimumSize = new Vector2(Width, Height);
			}

			Click += (s, e) => Checked = true;
		}

		public RadioButton(GuiWidget view)
			: this(0, 0, view)
		{
		}

		protected void FixBoundsAndChildrenPositions()
		{
			SetBoundsToEncloseChildren();

			if (LocalBounds.Left != 0 || LocalBounds.Bottom != 0)
			{
				SuspendLayout();
				// let's make sure that a button has 0, 0 at the lower left
				// move the children so they will fit with 0, 0 at the lower left
				foreach (GuiWidget child in Children)
				{
					child.OriginRelativeParent = child.OriginRelativeParent + new Vector2(-LocalBounds.Left, -LocalBounds.Bottom);
				}
				ResumeLayout();

				SetBoundsToEncloseChildren();
			}
		}

		public RadioButton(string label, int fontSize=12)
			: this(0, 0, label, fontSize)
		{
		}

		public RadioButton(string label, Color textColor, int fontSize = 12)
			: this(0, 0, label, fontSize)
		{
			this.TextColor = textColor;
		}

		public RadioButton(double x, double y, string label, int fontSize=12)
			: this(x, y, new RadioButtonViewText(label, fontSize))
		{
		}

		public override void OnParentChanged(EventArgs e)
		{
			if(Parent == null)
			{
				return;
			}

			if (this.SiblingRadioButtonList == null)
			{
				SiblingRadioButtonList = Parent.Children;
			}

			base.OnParentChanged(e);
		}

		public override string Text
		{
			get
			{
				if (Children.FirstOrDefault() is RadioButtonViewText buttonView)
				{
					return buttonView.Text;
				}

				return base.Text;
			}

			set
			{
				if (Children.FirstOrDefault() is RadioButtonViewText buttonView)
				{
					buttonView.Text = value;
				}

				base.Text = value;
			}
		}

		private void UncheckAllOtherRadioButtons()
		{
			if (SiblingRadioButtonList != null)
			{
				foreach (GuiWidget child in SiblingRadioButtonList.Distinct())
				{
					var radioButton = child as IRadioButton;
					if (radioButton != null && radioButton != this)
					{
						radioButton.Checked = false;
					}
				}
			}
		}

		public bool Checked
		{
			get => isChecked;
			set
			{
				if (isChecked != value)
				{
					isChecked = value;
					if (isChecked)
					{
						UncheckAllOtherRadioButtons();
					}
					OnCheckStateChanged();
					Invalidate();
				}
			}
		}

		public virtual void OnCheckStateChanged()
		{
			CheckedStateChanged?.Invoke(this, null);
		}

		public Color TextColor
		{
			get
			{
				if (Children.FirstOrDefault() is RadioButtonViewText buttonView)
				{
					return buttonView.TextColor;
				}

				return Color.White;
			}

			set
			{
				if (Children.FirstOrDefault() is RadioButtonViewText buttonView)
				{
					buttonView.TextColor = value;
				}
			}
		}
	}
}