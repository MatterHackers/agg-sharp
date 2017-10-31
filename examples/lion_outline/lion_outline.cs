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

using MatterHackers.Agg.Image;
using MatterHackers.Agg.RasterizerScanline;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;
using System;
using MatterHackers.Agg.UI.Examples;
using MatterHackers.Agg.Platform;

namespace MatterHackers.Agg
{
	public class lion_outline : GuiWidget, IDemoApp
	{
		private MatterHackers.Agg.UI.Slider widthSlider;
		private MatterHackers.Agg.UI.CheckBox renderAsScanlineCheckBox;
		private MatterHackers.Agg.UI.CheckBox renderAccurateJoinsCheckBox;

		private LionShape lionShape = new LionShape();
		private ScanlineRasterizer rasterizer = new ScanlineRasterizer();
		private ScanlineCachePacked8 scanlineCache = new ScanlineCachePacked8();
		private double angle = 0;
		private double lionScale = 1.0;
		private double skewX = 0;
		private double skewY = 0;

		public lion_outline()
		{
			widthSlider = new MatterHackers.Agg.UI.Slider(new Vector2(5, 5), 498);
			renderAsScanlineCheckBox = new MatterHackers.Agg.UI.CheckBox(160, 5, "Use Scanline Rasterizer");
			renderAsScanlineCheckBox.Checked = false;
			widthSlider.ValueChanged += new EventHandler(NeedsRedraw);
			renderAsScanlineCheckBox.CheckedStateChanged += NeedsRedraw;
			AddChild(widthSlider);
			widthSlider.OriginRelativeParent = Vector2.Zero;
			widthSlider.SetRange(0.0, 4.0);
			widthSlider.Value = 1.0;
			widthSlider.Text = "Width {0:F2}";

			AddChild(renderAsScanlineCheckBox);
			//renderAsScanlineCheckBox.Transform = Affine.NewIdentity();

			renderAccurateJoinsCheckBox = new CheckBox(200 + 10.0, 10.0 + 4.0 + 16.0, "Accurate Joins");
			AddChild(renderAccurateJoinsCheckBox);
		}

		public string Title { get; } = "Lion Outline";

		public string DemoCategory { get; } = "Vector";

		public string DemoDescription { get; } = "The example demonstrates Maxim's algorithm of drawing Anti-Aliased lines. " +
				"The algorithm works about 2.5 times faster than the scanline rasterizer but has " +
				"some restrictions, particularly, line joins can be only of the �miter� type, " +
				"and when so called miter limit is exceded, they are not as accurate as generated " +
				"by the stroke converter (conv_stroke). To see the difference, maximize the window " +
				"and try to rotate and scale the �lion� with and without using the scanline " +
				"rasterizer (a checkbox at the bottom). The difference in performance is obvious.";

		public override void OnParentChanged(EventArgs e)
		{
			AnchorAll();
			base.OnParentChanged(e);
		}

		private void NeedsRedraw(object sender, EventArgs e)
		{
			Invalidate();
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			ImageBuffer widgetsSubImage = ImageBuffer.NewSubImageReference(graphics2D.DestImage, graphics2D.GetClippingRect());

			int width = (int)widgetsSubImage.Width;
			int height = (int)widgetsSubImage.Height;

			ImageBuffer clippedSubImage = new ImageBuffer();
			clippedSubImage.Attach(widgetsSubImage, new BlenderBGRA());
			ImageClippingProxy imageClippingProxy = new ImageClippingProxy(clippedSubImage);
			imageClippingProxy.clear(new ColorF(1, 1, 1));

			Affine transform = Affine.NewIdentity();
			transform *= Affine.NewTranslation(-lionShape.Center.X, -lionShape.Center.Y);
			transform *= Affine.NewScaling(lionScale, lionScale);
			transform *= Affine.NewRotation(angle + Math.PI);
			transform *= Affine.NewSkewing(skewX / 1000.0, skewY / 1000.0);
			transform *= Affine.NewTranslation(width / 2, height / 2);

			if (renderAsScanlineCheckBox.Checked)
			{
				rasterizer.SetVectorClipBox(0, 0, width, height);

				Stroke stroke = new Stroke(lionShape.Path);
				stroke.width(widthSlider.Value);
				stroke.line_join(LineJoin.Round);
				VertexSourceApplyTransform trans = new VertexSourceApplyTransform(stroke, transform);
				ScanlineRenderer scanlineRenderer = new ScanlineRenderer();
				scanlineRenderer.RenderSolidAllPaths(imageClippingProxy, rasterizer, scanlineCache, trans, lionShape.Colors, lionShape.PathIndex, lionShape.NumPaths);
			}
			else
			{
				double w = widthSlider.Value * transform.GetScale();

				LineProfileAnitAlias lineProfile = new LineProfileAnitAlias(w, new gamma_none());
				OutlineRenderer outlineRenderer = new OutlineRenderer(imageClippingProxy, lineProfile);
				rasterizer_outline_aa rasterizer = new rasterizer_outline_aa(outlineRenderer);

				rasterizer.line_join(renderAccurateJoinsCheckBox.Checked ?
					rasterizer_outline_aa.outline_aa_join_e.outline_miter_accurate_join
					: rasterizer_outline_aa.outline_aa_join_e.outline_round_join);
				rasterizer.round_cap(true);

				VertexSourceApplyTransform trans = new VertexSourceApplyTransform(lionShape.Path, transform);

				rasterizer.RenderAllPaths(trans, lionShape.Colors, lionShape.PathIndex, lionShape.NumPaths);
			}

			base.OnDraw(graphics2D);
		}

		private void DoTransform(double width, double height, double x, double y)
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
				DoTransform(width, height, x, y);
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
				MoveTheLion(mouseEvent);
			}
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			base.OnMouseMove(mouseEvent);
			if (Focused && MouseCaptured)
			{
				MoveTheLion(mouseEvent);
			}
		}

		[STAThread]
		public static void Main(string[] args)
		{
			//AggContext.Init(embeddedResourceName: "lion_outline.config.json");

			var demoWidget = new lion_outline();

			var systemWindow = new SystemWindow(512, 512);
			systemWindow.Title = demoWidget.Title;
			systemWindow.AddChild(demoWidget);
			systemWindow.ShowAsSystemWindow();
		}
	}
}