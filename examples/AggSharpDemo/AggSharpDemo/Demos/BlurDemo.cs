/*
Copyright (c) 2026, Lars Brubaker
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
*/

using System;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.AggSharpDemo.Demos
{
	/// <summary>
	/// C++ AGG's blur.cpp: the glyph "a" over its shadow, the shadow mapped by a perspective transform onto a
	/// quadrilateral you drag and then blurred - by stack blur, by recursive (true Gaussian) blur, or one colour
	/// channel at a time.
	/// </summary>
	/// <remarks>
	/// On the GPU the same box of the frame is blurred by <see cref="IBlurGraphics.BlurBox"/>, which runs each of
	/// the three blurs as the software does, channel mask included.
	/// The "%3.2f ms" timer text is left out, as in the other ports.
	/// </remarks>
	public class BlurDemo : AggDemo
	{
		private readonly AggCtrlContainer ctrls = new AggCtrlContainer();

		private readonly VertexStorage path = new VertexStorage();

		private readonly RectangleDouble shapeBounds;

		private readonly stack_blur stackBlur = new stack_blur();

		private readonly RecursiveBlur recursiveBlur = new RecursiveBlur(new recursive_blur_calc_rgb());

		private readonly RecursiveBlur grayBlur = new RecursiveBlur(new recursive_blur_calc_gray());

		public BlurDemo()
		{
			// blur.cpp runs with flip_y = true and gives its ctrls !flip_y.
			this.MethodRbox = new RboxCtrl(10.0, 10.0, 130.0, 70.0, false);
			this.MethodRbox.SetTextSize(8);
			this.MethodRbox.AddItem("Stack blur");
			this.MethodRbox.AddItem("Recursive blur");
			this.MethodRbox.AddItem("Channels");
			this.MethodRbox.CurrentItem = 0;

			this.RadiusSlider = new SliderCtrl(130 + 10.0, 10.0 + 4.0, 130 + 300.0, 10.0 + 8.0 + 4.0, false) { Label = "Blur Radius={0:F2}" };
			this.RadiusSlider.SetRange(0.0, 40.0);
			this.RadiusSlider.Value = 15.0;

			this.RedCbox = new CboxCtrl(10.0, 80.0, "Red");
			this.GreenCbox = new CboxCtrl(10.0, 95.0, "Green") { Checked = true };
			this.BlueCbox = new CboxCtrl(10.0, 110.0, "Blue");

			this.ctrls.Add(this.MethodRbox);
			this.ctrls.Add(this.RadiusSlider);
			this.ctrls.Add(this.RedCbox);
			this.ctrls.Add(this.GreenCbox);
			this.ctrls.Add(this.BlueCbox);
			this.ctrls.Changed += (s, e) => this.Invalidate();

			this.ComposeShape();

			// C++ bounding_rect_single over conv_curve: the bounds of the flattened glyph.
			this.shapeBounds = this.Shape.GetBounds();
			this.ShadowQuad.SetPoint(0, this.shapeBounds.Left + 10, this.shapeBounds.Bottom - 10);
			this.ShadowQuad.SetPoint(1, this.shapeBounds.Right + 10, this.shapeBounds.Bottom - 10);
			this.ShadowQuad.SetPoint(2, this.shapeBounds.Right + 10, this.shapeBounds.Top - 10);
			this.ShadowQuad.SetPoint(3, this.shapeBounds.Left + 10, this.shapeBounds.Top - 10);
		}

		/// <summary>C++ <c>m_method</c>: 0 stack blur, 1 recursive blur, 2 recursive blur of the checked channels.</summary>
		public RboxCtrl MethodRbox { get; }

		/// <summary>C++ <c>m_radius</c>: the blur radius, 0 to 40.</summary>
		public SliderCtrl RadiusSlider { get; }

		/// <summary>C++ <c>m_channel_r</c>: blur the red channel in "Channels" mode.</summary>
		public CboxCtrl RedCbox { get; }

		/// <summary>C++ <c>m_channel_g</c>: blur the green channel in "Channels" mode.</summary>
		public CboxCtrl GreenCbox { get; }

		/// <summary>C++ <c>m_channel_b</c>: blur the blue channel in "Channels" mode.</summary>
		public CboxCtrl BlueCbox { get; }

		/// <summary>C++ <c>m_shadow_ctrl</c>: the quadrilateral the glyph's bounds map onto to make its shadow.</summary>
		public InteractivePolygon ShadowQuad { get; } = new InteractivePolygon(4, 5.0);

		public override string Name => "blur";

		public override string Category => "Images";

		public override string Description => "A shadow under the glyph \"a\", blurred by stack blur, recursive Gaussian blur or channel by channel. Drag the shadow's corners and change the radius.";

		public override int Width => 440;

		public override int Height => 330;

		/// <summary>C++ <c>m_shape</c>: the glyph, conv_curve over its path.</summary>
		public IVertexSource Shape => new FlattenCurves(this.path);

		/// <summary>The glyph mapped by the perspective transform from its bounds onto <see cref="ShadowQuad"/>.</summary>
		public IVertexSource Shadow
		{
			get
			{
				var quad = new double[8];
				for (int i = 0; i < 4; i++)
				{
					quad[i * 2] = this.ShadowQuad.GetPoint(i).X;
					quad[(i * 2) + 1] = this.ShadowQuad.GetPoint(i).Y;
				}

				var shadowPerspective = new Perspective(this.shapeBounds.Left, this.shapeBounds.Bottom, this.shapeBounds.Right, this.shapeBounds.Top, quad);
				return new VertexSourceApplyTransform(this.Shape, shadowPerspective);
			}
		}

		public override void Draw(Graphics2D graphics)
		{
			graphics.FillRectangle(0, 0, this.Width, this.Height, Color.White);

			IVertexSource shadow = this.Shadow;
			Color shadowColor = Rgba8.FromRgba(0.1, 0.1, 0.1);
			graphics.Render(shadow, shadowColor);
			if (graphics is ImageGraphics2D && graphics.Rasterizer != null && graphics.DestImage is IImageByte destination)
			{
				this.BlurShadow(destination, graphics.GetTransform(), shadow.GetBounds());
			}
			else if (graphics is IBlurGraphics blurGraphics && this.ShadowBox(shadow.GetBounds()) is RectangleInt box)
			{
				var region = new RoundedRect(box.Left, box.Bottom, box.Right + 1, box.Top + 1, 0);
				double radius = this.RadiusSlider.Value;
				if (this.MethodRbox.CurrentItem == 0)
				{
					blurGraphics.BlurBox(region, radius, BlurKind.Stack);
				}
				else if (this.MethodRbox.CurrentItem == 1)
				{
					blurGraphics.BlurBox(region, radius, BlurKind.Recursive);
				}
				else
				{
					var channels = (this.RedCbox.Checked ? ColorChannels.Red : ColorChannels.None)
						| (this.GreenCbox.Checked ? ColorChannels.Green : ColorChannels.None)
						| (this.BlueCbox.Checked ? ColorChannels.Blue : ColorChannels.None);
					blurGraphics.BlurBox(region, Util.uround(radius), BlurKind.Recursive, channels);
				}
			}

			graphics.Render(this.ShadowQuad, Rgba8.FromRgba(0, 0.3, 0.5, 0.3));

			graphics.Render(this.Shape, Rgba8.FromRgba(0.6, 0.9, 0.7, 0.8));

			this.ctrls.Render(graphics);
		}

		public override void OnMouseDown(int x, int y, AggInputFlags button, AggInputFlags flags)
		{
			if (this.ctrls.OnMouseDown(x, y, button) || !button.HasFlag(AggInputFlags.MouseLeft))
			{
				return;
			}

			if (this.ShadowQuad.OnMouseButtonDown(x, y))
			{
				this.Invalidate();
			}
		}

		public override void OnMouseMove(int x, int y, AggInputFlags flags)
		{
			if (this.ctrls.OnMouseMove(x, y, flags))
			{
				return;
			}

			if (flags.HasFlag(AggInputFlags.MouseLeft))
			{
				if (this.ShadowQuad.OnMouseMove(x, y))
				{
					this.Invalidate();
				}
			}
			else if (this.ShadowQuad.OnMouseButtonUp(x, y))
			{
				this.Invalidate();
			}
		}

		public override void OnMouseUp(int x, int y, AggInputFlags button, AggInputFlags flags)
		{
			this.ctrls.OnMouseUp(x, y, button);
			if (this.ShadowQuad.OnMouseButtonUp(x, y))
			{
				this.Invalidate();
			}
		}

		public override void OnKeyDown(Keys key, AggInputFlags flags)
		{
			this.ctrls.OnKeyDown(key);
		}

		// The box the blur runs over, in the demo's pixels: the shadow's bounds grown by the radius (and by another
		// radius right and up, where C++ notes the recursive blur's cut-off window otherwise over-weights the edge
		// pixels). C++ pixfmt::attach(pixf, int(x1), int(y1), int(x2), int(y2)): the inclusive integer box, clipped
		// to the window. Null when nothing is left.
		private RectangleInt? ShadowBox(RectangleDouble bounds)
		{
			double radius = this.RadiusSlider.Value;
			var box = new RectangleDouble(bounds.Left - radius, bounds.Bottom - radius, bounds.Right + (2 * radius), bounds.Top + (2 * radius));
			int x1 = Math.Max((int)box.Left, 0);
			int y1 = Math.Max((int)box.Bottom, 0);
			int x2 = Math.Min((int)box.Right, this.Width - 1);
			int y2 = Math.Min((int)box.Top, this.Height - 1);
			return x1 > x2 || y1 > y2 ? null : new RectangleInt(x1, y1, x2, y2);
		}

		// Blurs the shadow's box in place in the frame.
		private void BlurShadow(IImageByte destination, Affine toDestination, RectangleDouble bounds)
		{
			double radius = this.RadiusSlider.Value;
			if (!(this.ShadowBox(bounds) is RectangleInt box))
			{
				return;
			}

			// The graphics transform only offsets the demo inside a larger frame.
			int x1 = box.Left;
			int y1 = box.Bottom;
			int x2 = box.Right;
			int y2 = box.Top;
			int offsetX = (int)Math.Round(toDestination.tx);
			int offsetY = (int)Math.Round(toDestination.ty);
			x1 += offsetX;
			x2 += offsetX;
			y1 += offsetY;
			y2 += offsetY;
			if (x1 < 0 || y1 < 0 || x2 >= destination.Width || y2 >= destination.Height)
			{
				return;
			}

			if (this.MethodRbox.CurrentItem != 2)
			{
				ImageBuffer area = SubImage(destination, x1, y1, x2, y2, 0, destination.BitDepth, destination.GetRecieveBlender());
				if (this.MethodRbox.CurrentItem == 0)
				{
					this.stackBlur.blur(area, Util.uround(radius));
				}
				else
				{
					this.recursiveBlur.blur(area, radius);
				}

				return;
			}

			// Each checked channel as a grey image of its own, blurred by the rounded radius as C++ does.
			int step = destination.GetBytesBetweenPixelsInclusive();
			if (this.RedCbox.Checked)
			{
				this.grayBlur.blur(SubImage(destination, x1, y1, x2, y2, ImageBuffer.OrderR, 8, new blender_gray(step)), Util.uround(radius));
			}

			if (this.GreenCbox.Checked)
			{
				this.grayBlur.blur(SubImage(destination, x1, y1, x2, y2, ImageBuffer.OrderG, 8, new blender_gray(step)), Util.uround(radius));
			}

			if (this.BlueCbox.Checked)
			{
				this.grayBlur.blur(SubImage(destination, x1, y1, x2, y2, ImageBuffer.OrderB, 8, new blender_gray(step)), Util.uround(radius));
			}
		}

		// A view of the inclusive box (x1, y1)-(x2, y2) of the image, sharing its bytes: with the image's own bit depth
		// and blender for a whole pixel, or 8 bits and a grey blender for one channel at byteOffset.
		private static ImageBuffer SubImage(IImageByte image, int x1, int y1, int x2, int y2, int byteOffset, int bitDepth, IRecieveBlenderByte blender)
		{
			var area = new ImageBuffer();
			area.AttachBuffer(image.GetBuffer(), image.GetBufferOffsetXY(x1, y1) + byteOffset, x2 - x1 + 1, y2 - y1 + 1, image.StrideInBytes(), bitDepth, image.GetBytesBetweenPixelsInclusive());
			area.SetRecieveBlender(blender);
			return area;
		}

		// blur.cpp's glyph "a" in font units, scaled by 4 and moved to (150, 100) - control points included, as
		// path_storage::transform moves every vertex.
		private void ComposeShape()
		{
			VertexStorage p = this.path;
			p.MoveTo(28.47, 6.45);
			p.Curve3(21.58, 1.12, 19.82, 0.29);
			p.Curve3(17.19, -0.93, 14.21, -0.93);
			p.Curve3(9.57, -0.93, 6.57, 2.25);
			p.Curve3(3.56, 5.42, 3.56, 10.60);
			p.Curve3(3.56, 13.87, 5.03, 16.26);
			p.Curve3(7.03, 19.58, 11.99, 22.51);
			p.Curve3(16.94, 25.44, 28.47, 29.64);
			p.LineTo(28.47, 31.40);
			p.Curve3(28.47, 38.09, 26.34, 40.58);
			p.Curve3(24.22, 43.07, 20.17, 43.07);
			p.Curve3(17.09, 43.07, 15.28, 41.41);
			p.Curve3(13.43, 39.75, 13.43, 37.60);
			p.LineTo(13.53, 34.77);
			p.Curve3(13.53, 32.52, 12.38, 31.30);
			p.Curve3(11.23, 30.08, 9.38, 30.08);
			p.Curve3(7.57, 30.08, 6.42, 31.35);
			p.Curve3(5.27, 32.62, 5.27, 34.81);
			p.Curve3(5.27, 39.01, 9.57, 42.53);
			p.Curve3(13.87, 46.04, 21.63, 46.04);
			p.Curve3(27.59, 46.04, 31.40, 44.04);
			p.Curve3(34.28, 42.53, 35.64, 39.31);
			p.Curve3(36.52, 37.21, 36.52, 30.71);
			p.LineTo(36.52, 15.53);
			p.Curve3(36.52, 9.13, 36.77, 7.69);
			p.Curve3(37.01, 6.25, 37.57, 5.76);
			p.Curve3(38.13, 5.27, 38.87, 5.27);
			p.Curve3(39.65, 5.27, 40.23, 5.62);
			p.Curve3(41.26, 6.25, 44.19, 9.18);
			p.LineTo(44.19, 6.45);
			p.Curve3(38.72, -0.88, 33.74, -0.88);
			p.Curve3(31.35, -0.88, 29.93, 0.78);
			p.Curve3(28.52, 2.44, 28.47, 6.45);
			p.ClosePolygon();

			p.MoveTo(28.47, 9.62);
			p.LineTo(28.47, 26.66);
			p.Curve3(21.09, 23.73, 18.95, 22.51);
			p.Curve3(15.09, 20.36, 13.43, 18.02);
			p.Curve3(11.77, 15.67, 11.77, 12.89);
			p.Curve3(11.77, 9.38, 13.87, 7.06);
			p.Curve3(15.97, 4.74, 18.70, 4.74);
			p.Curve3(22.41, 4.74, 28.47, 9.62);
			p.ClosePolygon();

			p.TransformAllPaths(Affine.NewScaling(4.0) * Affine.NewTranslation(150, 100));
		}
	}
}
