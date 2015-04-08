using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg
{
	public class WindowPage : TabPage
	{
		public WindowPage()
			: base("Window")
		{
			WindowWidget widow = new WindowWidget(new RectangleDouble(20, 20, 400, 400));
			AddChild(widow);

			GroupBox groupBox = new GroupBox("Radio Group");
			groupBox.LocalBounds = new RectangleDouble(0, 0, 300, 100);
			groupBox.OriginRelativeParent = new Vector2(0, 0);
			groupBox.AddChild(new RadioButton(0, 40, "Simple Radio Button 1"));
			groupBox.AddChild(new RadioButton(0, 20, "Simple Radio Button 2"));
			groupBox.AddChild(new RadioButton(0, 0, "Simple Radio Button 3"));
			widow.AddChild(groupBox);
		}
	}
}