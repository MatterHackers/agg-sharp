using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.Font;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg
{
    public class ButtonsPage : TabPage
    {
        GroupBox groupBox;

        public ButtonsPage()
            : base("Radio and Check Buttons")
        {
            FlowLayoutWidget topToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom);
            topToBottom.AddChild(new CheckBox("Simple Check Box"));

            CheckBoxViewStates fourStates = new CheckBoxViewStates(new TextWidget("normal"), new TextWidget("switch n->p"), new TextWidget("pressed"), new TextWidget("switch p->n"));
            topToBottom.AddChild(new CheckBox(fourStates));

            GuiWidget normalPressed = new TextWidget("0 4state");
            normalPressed.BackgroundColor = RGBA_Bytes.Gray;
            GuiWidget downPressed = new TextWidget("1 4state");
            downPressed.BackgroundColor = RGBA_Bytes.Gray;

            GuiWidget normalHover = new TextWidget("0 4state");
            normalHover.BackgroundColor = RGBA_Bytes.Yellow;
            GuiWidget downHover = new TextWidget("1 4state");
            downHover.BackgroundColor = RGBA_Bytes.Yellow;

            CheckBoxViewStates fourStates2 = new CheckBoxViewStates(new TextWidget("0 4state"), normalHover, normalPressed, new TextWidget("1 4state"), downHover, downPressed, new TextWidget("disabled"));
            topToBottom.AddChild(new CheckBox(fourStates2));

            topToBottom.AddChild(new RadioButton("Simple Radio Button 1"));
            topToBottom.AddChild(new RadioButton("Simple Radio Button 2"));
            topToBottom.AddChild(new RadioButton(new RadioButtonViewText("Simple Radio Button 3")));
            topToBottom.AddChild(new RadioButton(new RadioButtonViewStates(new TextWidget("O - unchecked"), new TextWidget("O - unchecked hover"), new TextWidget("O - checking"), new TextWidget("X - checked"), new TextWidget("disabled"))));

            groupBox = new GroupBox("Radio Group");
            //groupBox.LocalBounds = new RectangleDouble(0, 0, 300, 150);
            groupBox.OriginRelativeParent = new Vector2(200, 350);
            FlowLayoutWidget topToBottomRadios = new FlowLayoutWidget(FlowDirection.TopToBottom);
            topToBottomRadios.AnchorAll();
            topToBottomRadios.AddChild(new RadioButton("Simple Radio Button 1"));
            topToBottomRadios.AddChild(new RadioButton("Simple Radio Button 2"));
            topToBottomRadios.AddChild(new RadioButton("Simple Radio Button 3"));
            topToBottomRadios.SetBoundsToEncloseChildren();
            groupBox.AddChild(topToBottomRadios);
            topToBottom.AddChild(groupBox);

            AddChild(topToBottom);

            topToBottom.VAnchor = UI.VAnchor.ParentTop;
        }
    }
}
