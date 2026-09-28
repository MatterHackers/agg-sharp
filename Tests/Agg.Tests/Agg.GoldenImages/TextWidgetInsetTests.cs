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

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using System;
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.GoldenImages
{
	/// <summary>
	/// A TextWidget placed by its parent's Padding or its own Margin draws its text at that inset, on the
	/// software rasterizer and on the GPU (where the TextWidget's default double buffer composites a GPU
	/// texture and its glyphs go through the text coverage mask). The widgets anchor Left: an Absolute
	/// HAnchor (the default) keeps whatever X the widget has and ignores Margin and Padding by design.
	/// </summary>
	[NotInParallel]
	public class TextWidgetInsetTests
	{
		private const int FrameWidth = 160;
		private const int FrameHeight = 64;

		private static readonly ColorF Background = new ColorF(0, 0, 0, 1);

		private static GuiWidget BuildMarginTree()
		{
			var root = new GuiWidget(FrameWidth, FrameHeight);
			var bar = new GuiWidget()
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
			};
			root.AddChild(bar);
			bar.AddChild(new TextWidget("GUI Demo", pointSize: 10, textColor: Color.White)
			{
				HAnchor = HAnchor.Left,
				VAnchor = VAnchor.Center,
				Margin = new BorderDouble(left: 20),
			});
			return root;
		}

		private static GuiWidget BuildPaddingTree()
		{
			var root = new GuiWidget(FrameWidth, FrameHeight);
			var column = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
				Padding = new BorderDouble(20, 8, 8, 8),
			};
			root.AddChild(column);
			column.AddChild(new TextWidget("Demos", pointSize: 11, textColor: Color.White, bold: true)
			{
				HAnchor = HAnchor.Left,
			});
			return root;
		}

		private static int LeftmostInkedColumn(ImageBuffer image)
		{
			for (int x = 0; x < image.Width; x++)
			{
				for (int y = 0; y < image.Height; y++)
				{
					if (image.GetPixel(x, y).red > 64)
					{
						return x;
					}
				}
			}

			return -1;
		}

		private static ImageBuffer RenderSoftware(GuiWidget root)
		{
			var image = new ImageBuffer(FrameWidth, FrameHeight);
			var graphics = image.NewGraphics2D();
			graphics.Clear(Background);
			root.OnDrawBackground(graphics);
			root.OnDraw(graphics);
			return image;
		}

		private static async Task<ImageBuffer> RenderGpu(GuiWidget root)
		{
			using var capture = WebGpuOffscreenCapture.Create(FrameWidth, FrameHeight);
			var frame = capture.BeginWidgetFrame(Background);
			root.OnDrawBackground(frame);
			root.OnDraw(frame);
			var image = await capture.CaptureAsync();
			await Assert.That(capture.Device.LastUncapturedError).IsNull();
			return image;
		}

		[Test]
		[Arguments(false, false)]
		[Arguments(false, true)]
		[Arguments(true, false)]
		[Arguments(true, true)]
		public async Task TextStartsAtTheInset(bool gpu, bool padding)
		{
			GuiWidget root = padding ? BuildPaddingTree() : BuildMarginTree();
			ImageBuffer image = gpu ? await RenderGpu(root) : RenderSoftware(root);
			int left = LeftmostInkedColumn(image);

			// Glyph side bearings put the first ink a pixel or two right of the inset, never left of it.
			await Assert.That(left).IsGreaterThanOrEqualTo(20).And.IsLessThanOrEqualTo(23)
				.Because($"gpu {gpu}, padding {padding}: the text should start at the 20px inset, first ink at x {left}");
			root.Close();
		}
	}
}
