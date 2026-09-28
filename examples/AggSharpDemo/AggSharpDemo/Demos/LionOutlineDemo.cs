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
	/// C++ AGG's lion_outline.cpp: the lion's outlines drawn with Maxim's anti-aliased line algorithm
	/// (rasterizer_outline_aa) - on the GPU those same pixels, drawn as rectangles - or with conv_stroke through the scanline rasterizer when the box is checked.
	/// Left-drag rotates and scales the lion about the middle of the window, right-drag skews it.
	/// </summary>
	public class LionOutlineDemo : AggDemo
	{
		private readonly LionShape lion = new LionShape();

		private readonly AggCtrlContainer ctrls = new AggCtrlContainer();

		private double angle;

		private double scale = 1.0;

		private double skewX;

		private double skewY;

		public LionOutlineDemo()
		{
			// lion_outline.cpp runs with flip_y = true and gives its ctrls !flip_y.
			this.WidthSlider = new SliderCtrl(5, 5, 150, 12, false)
			{
				Label = "Width {0,3:F2}",
			};
			this.WidthSlider.SetRange(0.0, 4.0);
			this.WidthSlider.Value = 1.0;

			this.ScanlineCbox = new CboxCtrl(160, 5, "Use Scanline Rasterizer");

			this.ctrls.Add(this.WidthSlider);
			this.ctrls.Add(this.ScanlineCbox);
			this.ctrls.Changed += (s, e) => this.Invalidate();
		}

		/// <summary>C++ <c>m_width_slider</c>: the line width, before the lion's scale.</summary>
		public SliderCtrl WidthSlider { get; }

		/// <summary>C++ <c>m_scanline</c>: stroke with conv_stroke and the scanline rasterizer instead.</summary>
		public CboxCtrl ScanlineCbox { get; }

		public override string Name => "lion_outline";

		public override string Category => "Vector Graphics";

		public override string Description => "The lion's outlines drawn with AGG's fast anti-aliased line algorithm, or with the scanline rasterizer to compare. Left-drag to rotate and scale, right-drag to skew.";

		public override int Width => 512;

		public override int Height => 512;

		/// <summary>
		/// Sets the lion's pose - the angle and scale a left-drag gives, the skew a right-drag gives - as
		/// lion_outline.cpp's globals hold it.
		/// </summary>
		public void SetPose(double angle, double scale, double skewX, double skewY)
		{
			this.angle = angle;
			this.scale = scale;
			this.skewX = skewX;
			this.skewY = skewY;
			this.Invalidate();
		}

		public override void Draw(Graphics2D graphics)
		{
			graphics.FillRectangle(0, 0, this.Width, this.Height, Color.White);

			Affine transform = this.lion.GetDemoTransform(this.Width, this.Height, this.angle, this.scale, this.skewX, this.skewY);

			// The outline rasterizer writes pixels itself, past the Graphics2D, so over an image it gets the graphics'
			// transform folded into the lion's (the profile width scales with it) and its clip as a pixel box. Any
			// other surface - the GPU - takes those pixels as rectangles through a Graphics2DSpanImage, which the
			// graphics' own transform and clip place.
			if (!this.ScanlineCbox.Checked && graphics is ImageGraphics2D)
			{
				transform *= graphics.GetTransform();
				RectangleDouble clip = graphics.GetClippingRect();
				var destination = new ImageClippingProxy(graphics.DestImage);
				destination.SetClippingBox(
					(int)Math.Floor(clip.Left),
					(int)Math.Floor(clip.Bottom),
					(int)Math.Ceiling(clip.Right) - 1,
					(int)Math.Ceiling(clip.Top) - 1);
				this.DrawOutlines(destination, transform);
			}
			else if (!this.ScanlineCbox.Checked)
			{
				var spans = new Graphics2DSpanImage(graphics, this.Width, this.Height);
				this.DrawOutlines(new ImageClippingProxy(spans), transform);
				spans.Flush();
			}
			else
			{
				foreach (var shape in this.lion.Shapes)
				{
					var stroke = new Stroke(shape.VertexStorage, this.WidthSlider.Value)
					{
						LineJoin = LineJoin.Round,
					};
					graphics.Render(new VertexSourceApplyTransform(stroke, transform), shape.Color);
				}
			}

			this.ctrls.Render(graphics);
		}

		/// <summary>The lion's outlines through rasterizer_outline_aa and renderer_outline_aa, as lion_outline.cpp draws them.</summary>
		private void DrawOutlines(ImageClippingProxy destination, Affine transform)
		{
			var profile = new LineProfileAnitAlias(this.WidthSlider.Value * transform.GetScale(), new gamma_none());
			var rasterizer = new rasterizer_outline_aa(new OutlineRenderer(destination, profile));
			rasterizer.line_join(rasterizer_outline_aa.outline_aa_join_e.outline_round_join);
			rasterizer.round_cap(false);

			foreach (var shape in this.lion.Shapes)
			{
				rasterizer.RenderAllPaths(new VertexSourceApplyTransform(shape.VertexStorage, transform), new[] { shape.Color }, new[] { 0 }, 1);
			}
		}

		public override void OnMouseDown(int x, int y, AggInputFlags button, AggInputFlags flags)
		{
			// C++ hands on_mouse_button_down the pressed button, so a press rotates or skews by that button alone.
			if (!this.ctrls.OnMouseDown(x, y, button))
			{
				this.Manipulate(x, y, button);
			}
		}

		public override void OnMouseMove(int x, int y, AggInputFlags flags)
		{
			if (!this.ctrls.OnMouseMove(x, y, flags))
			{
				this.Manipulate(x, y, flags);
			}
		}

		public override void OnMouseUp(int x, int y, AggInputFlags button, AggInputFlags flags)
		{
			this.ctrls.OnMouseUp(x, y, button);
		}

		public override void OnKeyDown(Keys key, AggInputFlags flags)
		{
			this.ctrls.OnKeyDown(key);
		}

		/// <summary>lion_outline.cpp on_mouse_button_down, which its on_mouse_move also calls.</summary>
		private void Manipulate(int x, int y, AggInputFlags flags)
		{
			if (flags.HasFlag(AggInputFlags.MouseLeft))
			{
				double dx = x - (this.Width / 2.0);
				double dy = y - (this.Height / 2.0);
				this.SetPose(Math.Atan2(dy, dx), Math.Sqrt((dy * dy) + (dx * dx)) / 100.0, this.skewX, this.skewY);
			}

			if (flags.HasFlag(AggInputFlags.MouseRight))
			{
				this.SetPose(this.angle, this.scale, x, y);
			}
		}
	}
}
