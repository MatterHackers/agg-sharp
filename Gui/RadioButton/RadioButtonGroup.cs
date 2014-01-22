//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.4
// Copyright (C) 2002-2005 Maxim Shemanarev (http://www.antigrain.com)
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
// classes rbox_ctrl_impl, rbox_ctrl
//
//----------------------------------------------------------------------------
using System;
using System.Collections.Generic;

using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.UI
{
    public class RadioButtonGroup : GuiWidget
    {
        public event EventHandler SelectionChanged;

        FlowLayoutWidget topToBottomButtons = new FlowLayoutWidget(FlowDirection.TopToBottom);
        List<RadioButton> radioButtons;

        IColorType backgroundColor;
        IColorType borderColor;
        IColorType textColor;
        IColorType inactiveColor;
        IColorType activeColor;

        public RadioButtonGroup()
            : this(new Vector2(), new Vector2())
        {
        }

        public RadioButtonGroup(Vector2 location, Vector2 size)
        {
            topToBottomButtons.Margin = new BorderDouble();
            topToBottomButtons.Padding = new BorderDouble(5);

            LocalBounds = new RectangleDouble(0, 0, size.x, size.y);
            OriginRelativeParent = location;
            radioButtons = new List<RadioButton>();
            AddChild(topToBottomButtons);

            backgroundColor = (new RGBA_Floats(1.0, 1.0, 0.9));
            borderColor = (new RGBA_Floats(0.0, 0.0, 0.0));
            textColor = (new RGBA_Floats(0.0, 0.0, 0.0));
            inactiveColor = (new RGBA_Floats(0.0, 0.0, 0.0));
            activeColor = (new RGBA_Floats(0.4, 0.0, 0.0));
        }
          

        public void background_color(IColorType c) { backgroundColor = c; }
        public void border_color(IColorType c) { borderColor = c; }
        public void text_color(IColorType c) { textColor = c; }
        public void inactive_color(IColorType c) { inactiveColor = c; }
        public void active_color(IColorType c) { activeColor = c; }

        public override void OnDraw(Graphics2D graphics2D)
        {
            RoundedRect backgroundRect = new RoundedRect(LocalBounds, 4);
            graphics2D.Render(backgroundRect, backgroundColor.GetAsRGBA_Bytes());

            graphics2D.Render(new Stroke(backgroundRect), borderColor.GetAsRGBA_Bytes());

            base.OnDraw(graphics2D);
        }

        public RadioButton AddRadioButton(string text)
        {
            RadioButton newRadioButton = new RadioButton(text);
            newRadioButton.Margin = new BorderDouble(2);
            newRadioButton.HAnchor = UI.HAnchor.ParentLeft;
            radioButtons.Add(newRadioButton);
            topToBottomButtons.AddChild(newRadioButton);

            return newRadioButton;
        }

        [Obsolete("use AddRadioButton instead", false)]
        public void add_item(string text)
        {
            AddRadioButton(text);
        }

        public int SelectedIndex
        {
            get
            {
                int curItem = 0;
                foreach (RadioButton button in radioButtons)
                {
                    if (button.Checked)
                    {
                        return curItem;
                    }
                    curItem++;
                }

                throw new Exception("no item checked");
            }

            set
            {
                if (value >= radioButtons.Count)
                {
                    throw new IndexOutOfRangeException("you have to have an index within the group");
                }
                if (!radioButtons[value].Checked)
                {
                    radioButtons[value].Checked = true;
                    if (SelectionChanged != null)
                    {
                        SelectionChanged(this, null);
                    }
                }
            }
        }

        public IColorType color(int i) 
        {
            switch(i)
            {
                case 0:
                    return backgroundColor;

                case 1:
                    return borderColor;

                case 2:
                    return textColor;

                case 3:
                    return inactiveColor;

                case 4:
                    return activeColor;

                default:
                    throw new System.IndexOutOfRangeException("There is not a color for this index");
            }
        }
    }
}
