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

using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.AggSharpDemo.Demos
{
	/// <summary>
	/// C++ AGG's component_rendering.cpp: three black circles, each drawn into a single color channel of the
	/// white window - red, green, blue - through a gray pixel format that views just that channel. Where a
	/// circle covers, its channel goes dark, so the circles show cyan, magenta and yellow and their overlaps
	/// mix like inks. The slider sets the circles' alpha.
	/// </summary>
	/// <remarks>
	/// On the GPU the channel view is the colour write mask (<see cref="IChannelMaskGraphics"/>): each circle is
	/// drawn black into its one channel, so the overlaps mix as in the software render. The GPU fill's edge
	/// anti-aliasing is its own, so edge pixels can differ from C++ by a level or so.
	/// </remarks>
	public class ComponentRenderingDemo : AggDemo
	{
		private readonly AggCtrlContainer ctrls = new AggCtrlContainer();

		public ComponentRenderingDemo()
		{
			// component_rendering.cpp runs with flip_y = true and gives its slider !flip_y.
			this.AlphaSlider = new SliderCtrl(5, 5, 320 - 5, 10 + 5, false)
			{
				Label = "Alpha={0:F0}",
			};
			this.AlphaSlider.SetRange(0, 255);
			this.AlphaSlider.Value = 255;

			this.ctrls.Add(this.AlphaSlider);
			this.ctrls.Changed += (s, e) => this.Invalidate();
		}

		/// <summary>C++ <c>m_alpha</c>: the circles' alpha, 0 to 255.</summary>
		public SliderCtrl AlphaSlider { get; }

		public override string Name => "component_rendering";

		public override string Category => "Vector Graphics";

		public override string Description => "Three black circles, each drawn into only the red, green or blue channel, so they mix like inks. Drag the slider to change their alpha.";

		public override int Width => 320;

		public override int Height => 320;

		public override void Draw(Graphics2D graphics)
		{
			graphics.FillRectangle(0, 0, this.Width, this.Height, Color.White);

			int alpha = (int)this.AlphaSlider.Value;
			double width = this.Width;
			double height = this.Height;
			var red = new Ellipse((width / 2) - (0.87 * 50), (height / 2) - (0.5 * 50), 100, 100, 100);
			var green = new Ellipse((width / 2) + (0.87 * 50), (height / 2) - (0.5 * 50), 100, 100, 100);
			var blue = new Ellipse(width / 2, (height / 2) + 50, 100, 100, 100);

			// The rasterizer is asked first so a GPU surface is never asked for DestImage, which would make it
			// allocate a full-screen CPU layer. The reference frame has no transform, so pixels are frame pixels.
			if (graphics.Rasterizer != null && graphics.DestImage is IImageByte destination)
			{
				var black = new Color(0, 0, 0, alpha);
				DrawIntoChannel(destination, ImageBuffer.OrderR, red, black);
				DrawIntoChannel(destination, ImageBuffer.OrderG, green, black);
				DrawIntoChannel(destination, ImageBuffer.OrderB, blue, black);
				destination.MarkImageChanged();
			}
			else if (graphics is IChannelMaskGraphics channelMasked)
			{
				// The GPU's colour write mask is the channel view: black at the slider's alpha, written to one
				// channel only.
				var black = new Color(0, 0, 0, alpha);
				channelMasked.DrawWithChannelMask(ColorChannels.Red, () => graphics.Render(red, black));
				channelMasked.DrawWithChannelMask(ColorChannels.Green, () => graphics.Render(green, black));
				channelMasked.DrawWithChannelMask(ColorChannels.Blue, () => graphics.Render(blue, black));
			}
			else
			{
				// A surface with neither: each circle in the color its channel view leaves on white, which is
				// right where a circle stands alone but not in the overlaps.
				graphics.Render(red, new Color(0, 255, 255, alpha));
				graphics.Render(green, new Color(255, 0, 255, alpha));
				graphics.Render(blue, new Color(255, 255, 0, alpha));
			}

			this.ctrls.Render(graphics);
		}

		public override void OnMouseDown(int x, int y, AggInputFlags button, AggInputFlags flags)
		{
			this.ctrls.OnMouseDown(x, y, button);
		}

		public override void OnMouseMove(int x, int y, AggInputFlags flags)
		{
			this.ctrls.OnMouseMove(x, y, flags);
		}

		public override void OnMouseUp(int x, int y, AggInputFlags button, AggInputFlags flags)
		{
			this.ctrls.OnMouseUp(x, y, button);
		}

		public override void OnKeyDown(Keys key, AggInputFlags flags)
		{
			this.ctrls.OnKeyDown(key);
		}

		/// <summary>
		/// C++ <c>pixfmt_alpha_blend_gray&lt;blender_gray&lt;gray8&gt;, rendering_buffer, Step, Offset&gt;</c>: an
		/// 8 bit gray view of one channel of <paramref name="destination"/>, stepping over the other channels, with
		/// <paramref name="color"/> filled into it through a scanline_p8 as render_scanlines_aa_solid does.
		/// </summary>
		private static void DrawIntoChannel(IImageByte destination, int channelOffset, IVertexSource shape, Color color)
		{
			var channel = new ImageBuffer();
			channel.Attach(destination, new BlenderGrayExact(destination.GetBytesBetweenPixelsInclusive()), destination.GetBytesBetweenPixelsInclusive(), channelOffset, 8);

			Graphics2D channelGraphics = channel.NewGraphics2D();

			// C++ clips only in the renderer, not the rasterizer.
			channelGraphics.Rasterizer.reset_clipping();
			channelGraphics.Render(shape, color);
		}
	}
}
