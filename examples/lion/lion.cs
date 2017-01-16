/*
Copyright (c) 2013, Lars Brubaker
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using System;

namespace MatterHackers.Agg
{
	public class Lion : GuiWidget
	{
		private Slider alphaSlider;
		private LionShape lionShape = new LionShape();
		private double angle = 0;
		private double lionScale = 1.0;
		private double skewX = 0;
		private double skewY = 0;

		public Lion()
		{
			BackgroundColor = RGBA_Bytes.White;
			alphaSlider = new Slider(new MatterHackers.VectorMath.Vector2(7, 27), 498);
			AddChild(alphaSlider);
			alphaSlider.ValueChanged += new EventHandler(alphaSlider_ValueChanged);
			alphaSlider.Text = "Alpha {0:F3}";
			alphaSlider.Value = 0.1;
		}

		public override void OnParentChanged(EventArgs e)
		{
			AnchorAll();
			base.OnParentChanged(e);
		}

		private void alphaSlider_ValueChanged(object sender, EventArgs e)
		{
			Invalidate();
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			byte alpha = (byte)(alphaSlider.Value * 255);
			for (int i = 0; i < lionShape.NumPaths; i++)
			{
				lionShape.Colors[i].Alpha0To255 = alpha;
			}

			Affine transform = Affine.NewIdentity();
			transform *= Affine.NewTranslation(-lionShape.Center.x, -lionShape.Center.y);
			transform *= Affine.NewScaling(lionScale, lionScale);
			transform *= Affine.NewRotation(angle + Math.PI);
			transform *= Affine.NewSkewing(skewX / 1000.0, skewY / 1000.0);
			transform *= Affine.NewTranslation(Width / 2, Height / 2);

			// This code renders the lion:
			VertexSourceApplyTransform transformedPathStorage = new VertexSourceApplyTransform(lionShape.Path, transform);
			graphics2D.Render(transformedPathStorage, lionShape.Colors, lionShape.PathIndex, lionShape.NumPaths);

			graphics2D.DrawString("test", 40, 40, 50);

			base.OnDraw(graphics2D);
		}

		private void UpdateTransform(double width, double height, double x, double y)
		{
			x -= width / 2;
			y -= height / 2;
			angle = Math.Atan2(y, x);
			lionScale = Math.Sqrt(y * y + x * x) / 100.0;
		}

		protected bool MoveTheLion(MouseEventArgs mouseEvent)
		{
			double x = mouseEvent.X;
			double y = mouseEvent.Y;
			if (mouseEvent.Button == MouseButtons.Left)
			{
				int width = (int)Width;
				int height = (int)Height;
				UpdateTransform(width, height, x, y);
				Invalidate();
				return true;
			}

			if (mouseEvent.Button == MouseButtons.Right)
			{
				skewX = x;
				skewY = y;
				Invalidate();
				return true;
			}

			return false;
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			base.OnMouseDown(mouseEvent);

			if (Focused && MouseCaptured)
			{
				if (MouseCaptured)
				{
					MoveTheLion(mouseEvent);
				}
			}
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			base.OnMouseMove(mouseEvent);

			if (Focused && MouseCaptured)
			{
				if (MouseCaptured)
				{
					MoveTheLion(mouseEvent);
				}
			}
		}

		[STAThread]
		public static void Main(string[] args)
		{
			AppWidgetFactory appWidget = new LionFactory();
			//appWidget.CreateWidgetAndRunInWindow();
			appWidget.CreateWidgetAndRunInWindow(surfaceType: AppWidgetFactory.RenderSurface.OpenGL);
		}
	}

	public class LionFactory : AppWidgetFactory
	{
		public override GuiWidget NewWidget()
		{
			return new Lion();
		}

		public override AppWidgetInfo GetAppParameters()
		{
			AppWidgetInfo appWidgetInfo = new AppWidgetInfo(
			"Vector",
			"Lion Filled",
			"Affine transformer, and basic renderers. You can rotate and scale the “Lion” with the"
					+ " left mouse button. Right mouse button adds “skewing” transformations, proportional to the “X” "
					+ "coordinate. The image is drawn over the old one with a cetrain opacity value. Change “Alpha” "
					+ "to draw funny looking “lions”. Change window size to clear the window.",
			512,
			400);

			return appWidgetInfo;
		}
	}
}