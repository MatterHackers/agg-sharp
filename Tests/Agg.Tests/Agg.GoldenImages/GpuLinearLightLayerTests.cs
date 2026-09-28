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
using MatterHackers.RenderCore;
using MatterHackers.RenderGl;
using MatterHackers.RenderGl.Compat;
using MatterHackers.WebGpuRender;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.GoldenImages
{
	/// <summary>
	/// What is drawn into a linear-light retained layer (<see cref="Graphics2D.CreateRetainedLayer(bool)"/>) comes out
	/// in the right colour, or the drawing fails loudly; it is never quietly converted twice or not at all.
	/// </summary>
	[NotInParallel]
	public class GpuLinearLightLayerTests
	{
		private const int Width = 64;
		private const int Height = 48;

		private static readonly Color Paint = new Color(90, 140, 200, 255);

		public static string[] ConvertingDraws() => new[] { "fill", "nestedLayer", "nestedSsaa", "alphaMask", "image" };

		public static string[] RejectedDraws() => new[] { "blur", "blurUnder", "blurBox", "coverageGamma", "mapChannels", "gouraud", "gouraudCompound", "imageFilter" };

		[Test]
		[MethodDataSource(nameof(ConvertingDraws))]
		public async Task DrawnColourSurvivesTheLayer(string how)
		{
			using var capture = WebGpuOffscreenCapture.Create(Width, Height);
			var graphics = capture.BeginWidgetFrame(new ColorF(1, 1, 1, 1));
			var layer = graphics.CreateRetainedLayer(linearLight: true);
			using (var paint = layer.Begin(Width, Height))
			{
				DrawOpaque(capture, (Graphics2DGpu)paint.Graphics, how);
			}

			graphics.RenderRetainedLayer(layer, 0, 0);
			var image = await capture.CaptureAsync();
			await AssertNear(image, Width / 2, Height / 2, Paint, 2, how);
		}

		[Test]
		[MethodDataSource(nameof(RejectedDraws))]
		public async Task DrawsWithoutALinearVariantThrow(string effect)
		{
			using var capture = WebGpuOffscreenCapture.Create(Width, Height);
			var graphics = capture.BeginWidgetFrame(new ColorF(1, 1, 1, 1));
			var layer = graphics.CreateRetainedLayer(linearLight: true);
			var paint = layer.Begin(Width, Height);
			try
			{
				await Assert.That(() => DrawRejected(paint.Graphics, effect)).Throws<NotSupportedException>();
			}
			finally
			{
				paint.Dispose();
			}
		}

		[Test]
		public async Task FilteringALinearLayerThrows()
		{
			using var capture = WebGpuOffscreenCapture.Create(Width, Height);
			var graphics = capture.BeginWidgetFrame(new ColorF(1, 1, 1, 1));
			var layer = graphics.CreateRetainedLayer(linearLight: true);
			using (var paint = layer.Begin(16, 16))
			{
				paint.Graphics.Clear(Paint);
			}

			await Assert.That(() => ((IImageFilterGraphics)graphics).FillPathWithFilteredImage(new Ellipse(32, 24, 16, 12), layer, new ImageFilterFill(Affine.NewScaling(2))))
				.Throws<NotSupportedException>();
		}

		[Test]
		public async Task RoundedCompositeOfALinearLayerAnswersFalse()
		{
			using var capture = WebGpuOffscreenCapture.Create(Width, Height);
			var graphics = capture.BeginWidgetFrame(new ColorF(1, 1, 1, 1));
			var layer = graphics.CreateRetainedLayer(linearLight: true);
			using (var paint = layer.Begin(16, 16))
			{
				paint.Graphics.Clear(Paint);
			}

			bool drawn = ((IRoundedLayerCompositor)layer).CompositeRounded(graphics, 0, 0, 1, new RectangleDouble(0, 0, 16, 16), 4);
			await Assert.That(drawn).IsFalse();
		}

		[Test]
		public async Task WithoutADestinationReadTheLayerIsEncodedAndBlendedOver()
		{
			// Mode 2 of the linear composite: the target cannot be copied from, so the layer is encoded alone and the
			// pipeline blends it over in sRGB.
			using var capture = WebGpuOffscreenCapture.Create(Width, Height);
			var graphics = capture.BeginWidgetFrame(new ColorF(1, 1, 1, 1));
			var layer = (GpuRetainedLayer)graphics.CreateRetainedLayer(linearLight: true);
			var translucent = new Color(90, 140, 200, 128);
			using (var paint = layer.Begin(Width, Height))
			{
				paint.Graphics.FillRectangle(0, 0, Width / 2, Height, Paint);
				paint.Graphics.FillRectangle(Width / 2, 0, Width, Height, translucent);
			}

			GpuCompOp.CompositeLinearLayer(capture.Gl, layer.Target.Texture, 0, 0, 1, readDestination: false);
			var image = await capture.CaptureAsync();
			await AssertNear(image, Width / 4, Height / 2, Paint, 2, "opaque");
			double a = translucent.alpha / 255.0;
			var over = new Color(
				(int)Math.Round((translucent.red * a) + (255 * (1 - a))),
				(int)Math.Round((translucent.green * a) + (255 * (1 - a))),
				(int)Math.Round((translucent.blue * a) + (255 * (1 - a))));
			await AssertNear(image, Width * 3 / 4, Height / 2, over, 3, "translucent, blended in sRGB");
		}

		[Test]
		public async Task LinearSuffixesAgreeWithTheBackend()
		{
			await Assert.That(GlShaderKeys.LinearStraightSuffix).IsEqualTo(WgslShaderSources.LinearStraightSuffix);
			await Assert.That(GlShaderKeys.LinearPremultipliedSuffix).IsEqualTo(WgslShaderSources.LinearPremultipliedSuffix);
		}

		[Test]
		public async Task OnlyASrcAlphaBlendDrawsStraightColour()
		{
			// Everything else lands its colour as it is in the premultiplied layer, so it has to be premultiplied already.
			await Assert.That(GlShaderKeys.IsStraightAlphaDraw(true, BlendFactor.SrcAlpha)).IsTrue();
			await Assert.That(GlShaderKeys.IsStraightAlphaDraw(true, BlendFactor.One)).IsFalse();
			await Assert.That(GlShaderKeys.IsStraightAlphaDraw(false, BlendFactor.SrcAlpha)).IsFalse();
			await Assert.That(GlShaderKeys.IsStraightAlphaDraw(false, BlendFactor.One)).IsFalse();
		}

		private static void DrawOpaque(WebGpuOffscreenCapture capture, Graphics2DGpu graphics, string how)
		{
			var rect = new RoundedRect(8, 8, 56, 40, 0);
			switch (how)
			{
				case "fill":
					graphics.Render(rect, Paint);
					break;

				case "nestedLayer":
					var inner = graphics.CreateRetainedLayer();
					using (var innerPaint = inner.Begin(Width, Height))
					{
						innerPaint.Graphics.Render(rect, Paint);
					}

					graphics.RenderRetainedLayer(inner, 0, 0);
					inner.Dispose();
					break;

				case "nestedSsaa":
					using (var ssaa = new SsaaRenderTarget(capture.Gl))
					{
						using (var scope = ssaa.BeginDraw(Width, Height, 2))
						{
							scope.Graphics.Render(rect, Paint);
						}

						ssaa.Composite(graphics, 0, 0);
					}

					break;

				case "alphaMask":
					var mask = new ImageBuffer(Width, Height, 8, new blender_gray(1));
					mask.NewGraphics2D().Clear(Color.White);
					((IAlphaMaskGraphics)graphics).DrawMasked(mask, () => graphics.Render(rect, Paint));
					break;

				case "image":
					var picture = new ImageBuffer(48, 32);
					picture.NewGraphics2D().Clear(Paint);
					graphics.Render(picture, 8, 8);
					break;

				default:
					throw new ArgumentException(how, nameof(how));
			}
		}

		private static void DrawRejected(Graphics2D graphics, string effect)
		{
			var shape = new Ellipse(32, 24, 16, 12);
			Action draw = () => graphics.Render(shape, Paint);
			var region = new RoundedRect(10, 10, 50, 40, 0);
			switch (effect)
			{
				case "blur":
					((IBlurGraphics)graphics).DrawBlurred(4, draw);
					break;

				case "blurUnder":
					((IBlurGraphics)graphics).BlurUnder(region, 4);
					break;

				case "blurBox":
					((IBlurGraphics)graphics).BlurBox(region, 4, BlurKind.Recursive);
					break;

				case "coverageGamma":
					((IGammaGraphics)graphics).DrawWithCoverageGamma(new gamma_power(2.2), Paint, draw);
					break;

				case "mapChannels":
					var identity = new byte[256];
					for (int i = 0; i < 256; i++)
					{
						identity[i] = (byte)i;
					}

					((IGammaGraphics)graphics).MapChannels(new RectangleDouble(0, 0, Width, Height), identity, identity, identity);
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

				case "imageFilter":
					var picture = new ImageBuffer(16, 16);
					picture.NewGraphics2D().Clear(Color.Red);
					((IImageFilterGraphics)graphics).FillPathWithFilteredImage(shape, picture, new ImageFilterFill(Affine.NewScaling(2)));
					break;

				default:
					throw new ArgumentException(effect, nameof(effect));
			}
		}

		private static async Task AssertNear(ImageBuffer image, int x, int y, Color expected, int tolerance, string what)
		{
			var actual = image.GetPixel(x, y);
			int delta = Math.Max(Math.Max(Math.Abs(actual.red - expected.red), Math.Abs(actual.green - expected.green)), Math.Abs(actual.blue - expected.blue));
			await Assert.That(delta).IsLessThanOrEqualTo(tolerance)
				.Because($"{what} at ({x}, {y}): expected {expected}, got {actual}");
		}
	}
}
