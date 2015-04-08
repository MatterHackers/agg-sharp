using MatterHackers.VectorMath;

namespace MatterHackers.Agg.UI
{
	public abstract class LayoutEngine
	{
		public virtual void InitLayout()
		{
		}

		public abstract void Layout(LayoutEventArgs layoutEventArgs);

		public abstract bool GetOriginAndWidthForChild(GuiWidget parent, GuiWidget child, out Vector2 newOriginRelParent, out double newWidth);

		public abstract bool GetOriginAndHeightForChild(GuiWidget parent, GuiWidget child, out Vector2 newOriginRelParent, out double newHeight);
	}
}