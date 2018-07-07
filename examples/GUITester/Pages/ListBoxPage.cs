using MatterHackers.Agg.UI;

namespace MatterHackers.Agg
{
	public class ListBoxPage : TabPage
	{
		private ListBox leftListBox;

		public ListBoxPage()
			: base("List Box Widget")
		{
			FlowLayoutWidget leftToRightLayout = new FlowLayoutWidget();
			leftToRightLayout.AnchorAll();
			{
				{
					leftListBox = new ListBox(new RectangleDouble(0, 0, 200, 300));
					//leftListBox.BackgroundColor = RGBA_Bytes.Red;
					leftListBox.Name = "LeftListBox";
					leftListBox.VAnchor = UI.VAnchor.Top;
					//leftListBox.DebugShowBounds = true;
					leftListBox.Margin = new BorderDouble(15);
					leftToRightLayout.AddChild(leftListBox);

					for (int i = 0; i < 1; i++)
					{
						leftListBox.AddChild(new ListBoxTextItem("hand" + i.ToString() + ".stl", "c:\\development\\hand" + i.ToString() + ".stl"));
					}
				}

				if (true)
				{
					ListBox rightListBox = new ListBox(new RectangleDouble(0, 0, 200, 300));
					rightListBox.VAnchor = UI.VAnchor.Top;
					rightListBox.Margin = new BorderDouble(15);
					leftToRightLayout.AddChild(rightListBox);

					for (int i = 0; i < 30; i++)
					{
						switch (i % 3)
						{
							case 0:
								rightListBox.AddChild(new ListBoxTextItem("ListBoxTextItem" + i.ToString() + ".stl", "c:\\development\\hand" + i.ToString() + ".stl"));
								break;

							case 1:
								rightListBox.AddChild(new Button("Button" + i.ToString() + ".stl"));
								break;

							case 2:
								rightListBox.AddChild(new RadioButton("RadioButton" + i.ToString() + ".stl"));
								break;
						}
					}
				}
			}

			AddChild(leftToRightLayout);
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			base.OnDraw(graphics2D);
		}
	}
}