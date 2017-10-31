using MatterHackers.Agg.Image;
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
	public class ButtonViewThreeImage : GuiWidget
	{
		private IImageByte normalImage;
		private IImageByte hoverImage;
		private IImageByte pressedImage;
		private IImageByte disabledImage;

		private double hoverOpacity;
		private System.Diagnostics.Stopwatch timeSinceLastDraw = new System.Diagnostics.Stopwatch();

		public double NumSecondsToFade { get; set; } = 0;

		public ButtonViewThreeImage(IImageByte normal, IImageByte hover, IImageByte pressed, IImageByte disabled = null)
		{
			if (disabled == null)
			{
				disabled = normal;
			}

			hoverOpacity = 0;
			normalImage = normal;
			hoverImage = hover;
			pressedImage = pressed;
			disabledImage = disabled;

			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Stretch;
		}

		public override void OnParentChanged(EventArgs e)
		{
			Button parentButton = (Button)Parent;

			RectangleInt imageBounds = normalImage.GetBounds();
			parentButton.OriginRelativeParent += new Vector2(imageBounds.Left, imageBounds.Bottom);

			RectangleDouble bounds = parentButton.LocalBounds;
			bounds.Right += imageBounds.Width;
			bounds.Top += imageBounds.Height;
			this.LocalBounds = bounds;
			parentButton.LocalBounds = bounds;

			parentButton.MouseEnter += redrawButtonIfRequired;
			parentButton.MouseDown += redrawButtonIfRequired;
			parentButton.MouseUp += redrawButtonIfRequired;
			parentButton.MouseLeave += new EventHandler(parentButton_MouseLeave);

			base.OnParentChanged(null);
		}

		private void parentButton_MouseLeave(object sender, EventArgs e)
		{
			Invalidate();
		}

		public void redrawButtonIfRequired(object sender, EventArgs e)
		{
			Invalidate();
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			double numSecondsSinceLastDraw = timeSinceLastDraw.Elapsed.TotalSeconds;
			timeSinceLastDraw.Restart();

			Button parentButton = (Button)Parent;

			double x = parentButton.Width / 2 - normalImage.Width / 2;
			double y = parentButton.Height / 2 - normalImage.Height / 2;

			if (!parentButton.Enabled)
			{
				graphics2D.Render(disabledImage, x, y);
				base.OnDraw(graphics2D);
				return;
			}

			if (parentButton.UnderMouseState == UI.UnderMouseState.FirstUnderMouse)
			{
				if (parentButton.MouseDownOnWidget)
				{
					graphics2D.Render(pressedImage, x, y);
				}
				else
				{
					if (NumSecondsToFade > 0)
					{
						if (hoverOpacity < 1)
						{
							graphics2D.Render(normalImage, x, y);
						}
						IRecieveBlenderByte oldBlender = null;
						if (graphics2D.DestImage != null)
						{
							oldBlender = graphics2D.DestImage.GetRecieveBlender();
							graphics2D.DestImage.SetRecieveBlender(new BlenderPolyColorPreMultBGRA(new Color(1, 1, 1, hoverOpacity)));
						}
						graphics2D.Render(hoverImage, x, y);
						if (graphics2D.DestImage != null)
						{
							graphics2D.DestImage.SetRecieveBlender(oldBlender);
						}
					}
					else
					{
						graphics2D.Render(hoverImage, x, y);
					}
				}

				if (NumSecondsToFade > 0)
				{
					hoverOpacity += numSecondsSinceLastDraw / NumSecondsToFade;
				}
				if (hoverOpacity > 1) hoverOpacity = 1;
			}
			else
			{
				graphics2D.Render(normalImage, x, y);
				if (NumSecondsToFade > 0 && hoverOpacity > 0)
				{
					IRecieveBlenderByte oldBlender = null;
					if (graphics2D.DestImage != null)
					{
						oldBlender = graphics2D.DestImage.GetRecieveBlender();
						graphics2D.DestImage.SetRecieveBlender(new BlenderPolyColorPreMultBGRA(new Color(1, 1, 1, hoverOpacity)));
					}

					graphics2D.Render(hoverImage, x, y);
					if (graphics2D.DestImage != null)
					{
						graphics2D.DestImage.SetRecieveBlender(oldBlender);
					}
				}

				if (NumSecondsToFade > 0)
				{
					hoverOpacity -= numSecondsSinceLastDraw / NumSecondsToFade;
				}
				if (hoverOpacity < 0) hoverOpacity = 0;
			}

			base.OnDraw(graphics2D);
		}
	}
}