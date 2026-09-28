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
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.GoldenImages
{
	/// <summary>
	/// The layers the GPU effects keep per context - compositing, blur, gradient fill, alpha mask, colour tables, filtered image, retained layers - are released
	/// when the context is. A leaked wgpu texture keeps its whole device alive after the device is disposed, so
	/// one leak per test grew until, on d3d12-warp in CI, an allocation failed and the submit that used the
	/// resulting error buffer aborted the test host ("Buffer with '' label is invalid").
	/// </summary>
	[NotInParallel]
	public class GpuLayerLifetimeTests
	{
		private const int Width = 64;
		private const int Height = 48;

		public static string[] Effects() => new[] { "none", "compOp", "blur", "blurUnder", "blurBoxRecursive", "blurBoxSlight", "gradient", "alphaMask", "coverageGamma", "mapChannels", "text", "image", "imageFilter", "gouraud", "gouraudCompound", "retainedLayer", "linearLightLayer" };

		[Test]
		[MethodDataSource(nameof(Effects))]
		public async Task DisposingTheCaptureReleasesEveryBufferAndTexture(string effect)
		{
			var capture = WebGpuOffscreenCapture.Create(Width, Height);
			var device = capture.Device;
			try
			{
				var graphics = capture.BeginWidgetFrame(new ColorF(1, 1, 1, 1));
				Draw(graphics, effect);
				await capture.CaptureAsync();
			}
			finally
			{
				capture.Dispose();
			}

			await Assert.That(device.LiveResources.Count).IsEqualTo(0);
		}

		private static void Draw(Graphics2D graphics, string effect)
		{
			var shape = new Ellipse(32, 24, 16, 12);
			Action draw = () => graphics.Render(shape, new Color(40, 180, 220, 200));
			switch (effect)
			{
				case "none":
					draw();
					break;

				case "compOp":
					((ICompOpGraphics)graphics).DrawWithCompOp(CompOp.Xor, draw);
					break;

				case "blur":
					((IBlurGraphics)graphics).DrawBlurred(4, draw);
					break;

				case "blurUnder":
					draw();
					((IBlurGraphics)graphics).BlurUnder(new RoundedRect(10, 10, 50, 40, 0), 4);
					break;

				case "blurBoxRecursive":
				case "blurBoxSlight":
					draw();
					((IBlurGraphics)graphics).BlurBox(new RoundedRect(10, 10, 50, 40, 0), 4, effect == "blurBoxRecursive" ? BlurKind.Recursive : BlurKind.Slight);
					break;

				case "gradient":
					var fill = new GradientFill
					{
						Shape = GradientShape.X,
						Spread = GradientSpread.Pad,
						D1 = 0,
						D2 = 40,
						ScreenToGradient = Affine.NewIdentity(),
						Colors = new gradient_linear_color(Color.Red, Color.Blue, 256),
					};
					((IGradientFillGraphics)graphics).FillPathWithGradient(shape, fill);
					break;

				case "alphaMask":
					var mask = new ImageBuffer(Width, Height, 8, new blender_gray(1));
					((IAlphaMaskGraphics)graphics).DrawMasked(mask, draw);
					break;

				case "coverageGamma":
					((IGammaGraphics)graphics).DrawWithCoverageGamma(new gamma_power(2.2), new Color(40, 180, 220, 200), draw);
					break;

				case "mapChannels":
					draw();
					var halve = new byte[256];
					for (int i = 0; i < 256; i++)
					{
						halve[i] = (byte)(i / 2);
					}

					((IGammaGraphics)graphics).MapChannels(new RectangleDouble(0, 0, Width, Height), halve, halve, halve);
					break;

				case "text":
					graphics.DrawString("Ag", 4, 10, 14, color: Color.Black);
					break;

				case "image":
					var image = new ImageBuffer(16, 16);
					image.NewGraphics2D().Clear(Color.Red);
					graphics.Render(image, 8, 8);
					break;

				case "imageFilter":
					var picture = new ImageBuffer(16, 16);
					picture.NewGraphics2D().Clear(Color.Red);
					((IImageFilterGraphics)graphics).FillPathWithFilteredImage(shape, picture, new ImageFilterFill(Affine.NewScaling(2))
					{
						Kind = ImageFilterKind.Filter,
						Filter = new ImageFilterLookUpTable(new image_filter_bicubic()),
					});
					break;

				case "gouraud":
				case "gouraudCompound":
					var triangle = new span_gouraud_rgba(Color.Red, Color.Green, Color.Blue, 4, 4, 60, 10, 30, 44, effect == "gouraud" ? 0.5 : 0);
					if (effect == "gouraud")
					{
						((IGouraudGraphics)graphics).FillGouraud(new[] { triangle }, new gamma_power(2.2));
					}
					else
					{
						((IGouraudGraphics)graphics).FillGouraudCompound(new[] { triangle });
					}

					break;

				case "retainedLayer":
					// Never disposed, as a demo or widget that outlives its surface leaves it: the context still releases
					// it. It is read back as a filtered image too, so its texture is also bound in a cached bind group.
					var layer = graphics.CreateRetainedLayer();
					using (var paint = layer.Begin(16, 16))
					{
						paint.Graphics.Clear(Color.Red);
					}

					graphics.RenderRetainedLayer(layer, 4, 4);
					((IImageFilterGraphics)graphics).FillPathWithFilteredImage(shape, layer, new ImageFilterFill(Affine.NewScaling(2)));
					break;

				case "linearLightLayer":
					// A float layer, never disposed either, painted with a compositing operator so the comp op's float
					// scratch layers are made too, then mixed onto the frame through its destination copy.
					var linearLayer = graphics.CreateRetainedLayer(linearLight: true);
					using (var paint = linearLayer.Begin(Width, Height))
					{
						paint.Graphics.Render(shape, new Color(40, 180, 220, 200));
						((ICompOpGraphics)paint.Graphics).DrawWithCompOp(CompOp.Multiply, () => paint.Graphics.Render(new Ellipse(40, 24, 16, 12), Color.Orange));
					}

					graphics.RenderRetainedLayer(linearLayer, 0, 0);
					break;

				default:
					throw new ArgumentException(effect, nameof(effect));
			}
		}
	}
}
