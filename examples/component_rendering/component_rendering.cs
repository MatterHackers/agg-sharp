/*
Copyright (c) 2014, Lars Brubaker
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

using System;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.RasterizerScanline;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.UI.Examples;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg
{
	public class ComponentRendering : GuiWidget, IDemoApp
	{
		private Slider alphaSlider;
		private CheckBox useBlackBackgroundCheckbox;

		public ComponentRendering()
			: base(320, 320)
		{
			alphaSlider = new Slider(new Vector2(5, 30), 310, 0, 255);
			alphaSlider.ValueChanged += new EventHandler(NeedInvalidate);
			alphaSlider.Text = "Alpha={0:F0}";
			alphaSlider.Value = 255;
			alphaSlider.View.TextColor = new Color(127, 127, 127);
			AddChild(alphaSlider);

			useBlackBackgroundCheckbox = new UI.CheckBox(5, 30 + 12, "Draw Black Background");
			useBlackBackgroundCheckbox.CheckedStateChanged += NeedInvalidate;
			useBlackBackgroundCheckbox.TextColor = new Color(127, 127, 127);
			AddChild(useBlackBackgroundCheckbox);
		}

		public string Title { get; } = "Component Rendering";

		public string DemoCategory { get; } = "Vector";

		public string DemoDescription { get; } = "AGG has a gray-scale renderer that can use any 8-bit color channel of an RGB or RGBA frame buffer. Most likely it will be used to draw gray-scale images directly in the alpha-channel.";

		public override void OnParentChanged(EventArgs e)
		{
			AnchorAll();
			//alphaSlider.SetAnchor(AnchorFlags.Bottom);
			base.OnParentChanged(e);
		}

		private void NeedInvalidate(object sender, EventArgs e)
		{
			Invalidate();
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			if (graphics2D.DestImage != null)
			{
				ImageBuffer widgetsSubImage = ImageBuffer.NewSubImageReference(graphics2D.DestImage, graphics2D.GetClippingRect());

				IImageByte backBuffer = widgetsSubImage;

				int distBetween = backBuffer.GetBytesBetweenPixelsInclusive();
				ImageBuffer redImageBuffer = new ImageBuffer();
				redImageBuffer.Attach(backBuffer, new blender_gray(distBetween), distBetween, 2, 8);
				ImageBuffer greenImageBuffer = new ImageBuffer();
				greenImageBuffer.Attach(backBuffer, new blender_gray(distBetween), distBetween, 1, 8);
				ImageBuffer blueImageBuffer = new ImageBuffer();
				blueImageBuffer.Attach(backBuffer, new blender_gray(distBetween), distBetween, 0, 8);

				ImageClippingProxy clippingProxy = new ImageClippingProxy(backBuffer);
				ImageClippingProxy clippingProxyRed = new ImageClippingProxy(redImageBuffer);
				ImageClippingProxy clippingProxyGreen = new ImageClippingProxy(greenImageBuffer);
				ImageClippingProxy clippingProxyBlue = new ImageClippingProxy(blueImageBuffer);

				ScanlineRasterizer ras = new ScanlineRasterizer();
				ScanlineCachePacked8 sl = new ScanlineCachePacked8();

				Color clearColor = useBlackBackgroundCheckbox.Checked ? new Color(0, 0, 0) : new Color(255, 255, 255);
				clippingProxy.clear(clearColor);
				alphaSlider.View.BackgroundColor = clearColor;

				Color FillColor = useBlackBackgroundCheckbox.Checked ? new Color(255, 255, 255, (int)(alphaSlider.Value)) : new Color(0, 0, 0, (int)(alphaSlider.Value));

				VertexSource.Ellipse er = new MatterHackers.Agg.VertexSource.Ellipse(Width / 2 - 0.87 * 50, Height / 2 - 0.5 * 50, 100, 100, 100);
				ras.add_path(er);
				ScanlineRenderer scanlineRenderer = new ScanlineRenderer();
				scanlineRenderer.RenderSolid(clippingProxyRed, ras, sl, FillColor);

				VertexSource.Ellipse eg = new MatterHackers.Agg.VertexSource.Ellipse(Width / 2 + 0.87 * 50, Height / 2 - 0.5 * 50, 100, 100, 100);
				ras.add_path(eg);
				scanlineRenderer.RenderSolid(clippingProxyGreen, ras, sl, FillColor);

				VertexSource.Ellipse eb = new MatterHackers.Agg.VertexSource.Ellipse(Width / 2, Height / 2 + 50, 100, 100, 100);
				ras.add_path(eb);
				scanlineRenderer.RenderSolid(clippingProxyBlue, ras, sl, FillColor);
			}
			else if (graphics2D.DestImageFloat != null)
			{
#if false
                IImageFloat backBuffer = graphics2D.DestImageFloat;

                int distBetween = backBuffer.GetFloatsBetweenPixelsInclusive();
                ImageBufferFloat redImageBuffer = new ImageBufferFloat();
                redImageBuffer.Attach(backBuffer, new blender_gray(distBetween), distBetween, 2, 8);
                ImageBufferFloat greenImageBuffer = new ImageBufferFloat();
                greenImageBuffer.Attach(backBuffer, new blender_gray(distBetween), distBetween, 1, 8);
                ImageBufferFloat blueImageBuffer = new ImageBufferFloat();
                blueImageBuffer.Attach(backBuffer, new blender_gray(distBetween), distBetween, 0, 8);

                ImageClippingProxy clippingProxy = new ImageClippingProxy(backBuffer);
                ImageClippingProxy clippingProxyRed = new ImageClippingProxy(redImageBuffer);
                ImageClippingProxy clippingProxyGreen = new ImageClippingProxy(greenImageBuffer);
                ImageClippingProxy clippingProxyBlue = new ImageClippingProxy(blueImageBuffer);

                ScanlineRasterizer ras = new ScanlineRasterizer();
                ScanlineCachePacked8 sl = new ScanlineCachePacked8();

                RGBA_Bytes clearColor = useBlackBackgroundCheckbox.Checked ? new RGBA_Bytes(0, 0, 0) : new RGBA_Bytes(255, 255, 255);
                clippingProxy.clear(clearColor);
                alphaSlider.View.BackGroundColor = clearColor;

                RGBA_Bytes FillColor = useBlackBackgroundCheckbox.Checked ? new RGBA_Bytes(255, 255, 255, (int)(alphaSlider.Value)) : new RGBA_Bytes(0, 0, 0, (int)(alphaSlider.Value));

                VertexSource.Ellipse er = new AGG.VertexSource.Ellipse(Width / 2 - 0.87 * 50, Height / 2 - 0.5 * 50, 100, 100, 100);
                ras.add_path(er);
                agg_renderer_scanline.Default.render_scanlines_aa_solid(clippingProxyRed, ras, sl, FillColor);

                VertexSource.Ellipse eg = new AGG.VertexSource.Ellipse(Width / 2 + 0.87 * 50, Height / 2 - 0.5 * 50, 100, 100, 100);
                ras.add_path(eg);
                agg_renderer_scanline.Default.render_scanlines_aa_solid(clippingProxyGreen, ras, sl, FillColor);

                VertexSource.Ellipse eb = new AGG.VertexSource.Ellipse(Width / 2, Height / 2 + 50, 100, 100, 100);
                ras.add_path(eb);
                agg_renderer_scanline.Default.render_scanlines_aa_solid(clippingProxyBlue, ras, sl, FillColor);
#endif
			}

			base.OnDraw(graphics2D);
		}

		[STAThread]
		public static void Main(string[] args)
		{
			Tests.AggDrawingTests.RunAllTests();

			var demoWidget = new ComponentRendering();

			var systemWindow = new SystemWindow(320, 320);
			systemWindow.Title = demoWidget.Title;
			systemWindow.AddChild(demoWidget);
			systemWindow.ShowAsSystemWindow();

		}
	}
}