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
    public class LayoutPage : TabPage
    {
        public LayoutPage()
            : base("Layout")
        {
            AddMinimumError();
        }

        void AddMinimumError()
        {
            GuiWidget thingToHide;
            {
                FlowLayoutWidget twoColumns = new FlowLayoutWidget();
                twoColumns.Name = "twoColumns";
                {
                    FlowLayoutWidget leftColumn = new FlowLayoutWidget(FlowDirection.TopToBottom);
                    leftColumn.Name = "leftColumn";
                    {
                        FlowLayoutWidget topLeftStuff = new FlowLayoutWidget(FlowDirection.TopToBottom);
                        topLeftStuff.Name = "topLeftStuff";

                        topLeftStuff.AddChild(new TextWidget("Top of Top Stuff"));
                        thingToHide = new Button("thing to hide");
                        topLeftStuff.AddChild(thingToHide);
                        topLeftStuff.AddChild(new TextWidget("Bottom of Top Stuff"));

                        leftColumn.AddChild(topLeftStuff);
                        //leftColumn.DebugShowBounds = true;
                    }

                    twoColumns.AddChild(leftColumn);
                }

                {
                    FlowLayoutWidget rightColumn = new FlowLayoutWidget(FlowDirection.TopToBottom);
                    rightColumn.Name = "rightColumn";
                    CheckBox hideCheckBox = new CheckBox("Hide Stuff");
                    rightColumn.AddChild(hideCheckBox);
                    hideCheckBox.CheckedStateChanged += (sender, e) =>
                    {
                        if (hideCheckBox.Checked)
                        {
                            thingToHide.Visible = false;
                        }
                        else
                        {
                            thingToHide.Visible = true;
                        }
                    };

                    twoColumns.AddChild(rightColumn);
                }

                this.AddChild(twoColumns);
            }
        }

        void AddLotsOfStuff()
        {
            {
                FlowLayoutWidget twoColumns = new FlowLayoutWidget();
                twoColumns.VAnchor = UI.VAnchor.ParentTop;

                List<GuiWidget> stuffToHide = new List<GuiWidget>();

                {
                    FlowLayoutWidget leftColumn = new FlowLayoutWidget(FlowDirection.TopToBottom);

                    {
                        FlowLayoutWidget topLeftStuff = new FlowLayoutWidget(FlowDirection.TopToBottom);
                        topLeftStuff.AddChild(new TextWidget("Top of Top Stuff"));
                        topLeftStuff.AddChild(new Button("Button 1"));
                        stuffToHide.Add(new Button("Button hide 1"));
                        topLeftStuff.AddChild(stuffToHide[0]);
                        topLeftStuff.AddChild(new Button("Button 2"));
                        stuffToHide.Add(new Button("Button hide 2"));
                        topLeftStuff.AddChild(stuffToHide[1]);
                        topLeftStuff.AddChild(new Button("Button 3"));
                        topLeftStuff.AddChild(new TextWidget("Bottom of Top Stuff"));

                        leftColumn.AddChild(topLeftStuff);
                    }

                    {
                        FlowLayoutWidget bottomLeftStuff = new FlowLayoutWidget(FlowDirection.TopToBottom);
                        bottomLeftStuff.AddChild(new TextWidget("Top of Bottom Stuff"));
                        bottomLeftStuff.AddChild(new TextWidget("Bottom of Bottom Stuff"));

                        leftColumn.AddChild(bottomLeftStuff);
                    }

                    twoColumns.AddChild(leftColumn);
                }

                {
                    FlowLayoutWidget rightColumn = new FlowLayoutWidget(FlowDirection.TopToBottom);
                    CheckBox hideCheckBox = new CheckBox("Hide Stuff");
                    rightColumn.AddChild(hideCheckBox);
                    hideCheckBox.CheckedStateChanged += (sender, e) =>
                    {
                        if (hideCheckBox.Checked)
                        {
                        }
                        else
                        {
                        }
                    };

                    twoColumns.AddChild(rightColumn);
                }

                this.AddChild(twoColumns);
            }
        }
    }
}
