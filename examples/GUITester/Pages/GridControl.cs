using MatterHackers.Agg.UI;
using System.Collections.Generic;

namespace MatterHackers.Agg
{
	public class GridControlPage : TabPage
	{
		public GridControlPage()
			: base("Grid Control")
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
	}
}