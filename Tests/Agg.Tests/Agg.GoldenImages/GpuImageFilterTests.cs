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
	/// <see cref="IImageFilterGraphics"/> on <c>Graphics2DGpu</c> against a real device, compared with
	/// <see cref="ImageGraphics2D"/>'s - the C++ AGG span generators - filling the same rectangle with the same image,
	/// mapping, filter and edge.
	/// </summary>
	/// <remarks>
	/// Only pixels at least <see cref="Margin"/> inside the rectangle are compared, so the GPU's anti-aliasing (not
	/// AGG's) stays out of it. The shader maps each pixel centre in floats where span_interpolator_linear steps
	/// integer subpixels (and the perspective resample interpolates its scale along a span), so a subpixel can differ
	/// by one: a few units on the smooth test image.
	/// </remarks>
	[NotInParallel]
	public class GpuImageFilterTests
	{
		private const int Width = 120;
		private const int Height = 90;
		private const int Margin = 3;
		private const int Tolerance = 3;

		private static readonly IVertexSource Shape = new RoundedRect(0, 0, Width, Height, 0);

		public static string[] Cases() => new[] { "nearest", "bilinear", "bicubic", "sinc4", "bilinear2x2Perspective", "resampleAffine", "resamplePerspective", "clip", "reflect", "bilinearQuad", "brightnessToAlpha" };

		[Test]
		[MethodDataSource(nameof(Cases))]
		public async Task FilteredImageMatchesSpanGenerators(string name)
		{
			// The resamples shrink a bigger picture, so there is something to average.
			ImageBuffer image = name.StartsWith("resample") ? TestImage(160, 120) : TestImage();
			ImageFilterFill fill = Fill(name, image);

			ImageBuffer gpu = await Capture(graphics =>
			{
				graphics.FillRectangle(0, 0, Width, Height, Color.White);
				((IImageFilterGraphics)graphics).FillPathWithFilteredImage(Shape, image, fill);
			});

			var software = new ImageBuffer(Width, Height);
			Graphics2D softwareGraphics = software.NewGraphics2D();
			softwareGraphics.FillRectangle(0, 0, Width, Height, Color.White);
			((IImageFilterGraphics)softwareGraphics).FillPathWithFilteredImage(Shape, image, fill);

			// Nearest has no weights to smooth a subpixel's difference: a pixel on a boundary takes its neighbour.
			await AssertInteriorNear(gpu, software, Tolerance, allowNeighbour: name == "nearest");
		}

		[Test]
		public async Task AnImageChangedBetweenFillsIsDrawnAsItNowIs()
		{
			var image = new ImageBuffer(8, 8);
			image.NewGraphics2D().Clear(Color.Red);
			var left = new RoundedRect(0, 0, Width / 2, Height, 0);
			var right = new RoundedRect(Width / 2, 0, Width, Height, 0);

			ImageBuffer gpu = await Capture(graphics =>
			{
				var filtered = (IImageFilterGraphics)graphics;
				filtered.FillPathWithFilteredImage(left, image, new ImageFilterFill(Affine.NewIdentity()));

				// The same image object, repainted: its upload is cached until its change count moves.
				image.NewGraphics2D().Clear(Color.Blue);
				filtered.FillPathWithFilteredImage(right, image, new ImageFilterFill(Affine.NewIdentity()));
			});

			await Assert.That(gpu.GetPixel(Width / 4, Height / 2)).IsEqualTo(Color.Red);
			await Assert.That(gpu.GetPixel(Width * 3 / 4, Height / 2)).IsEqualTo(Color.Blue);
		}

		/// <summary>
		/// A translucent retained layer painted at coordinate scale 2 reads as its logical-size picture: through a
		/// bilinear quad mapping (its corners scaled to the texture), its premultiplied texels and a translucent clip
		/// background composite as software's straight-alpha image and background do.
		/// </summary>
		[Test]
		public async Task ATranslucentLayerAtScaleTwoMatchesItsPictureThroughABilinearMapping()
		{
			var red = new Color(255, 0, 0, 128);
			// Set, not drawn: drawing half-transparent red onto a transparent ImageBuffer blends it toward black.
			var picture = new ImageBuffer(LayerWidth, LayerHeight);
			for (int y = 0; y < LayerHeight; y++)
			{
				for (int x = 0; x < LayerWidth; x++)
				{
					picture.SetPixel(x, y, x < LayerWidth / 2 ? Color.Black : red);
				}
			}
			var fill = new ImageFilterFill(new Bilinear(LayerQuad, -2, -2, LayerWidth + 2, LayerHeight + 2))
			{
				Kind = ImageFilterKind.Nearest,
				Edge = ImageFilterEdge.Clip,
				Background = new Color(40, 200, 0, 100),
			};

			ImageBuffer gpu = await CaptureLayerFill(new ColorF(1, 1, 1, 1), red, fill);

			var software = new ImageBuffer(Width, Height);
			Graphics2D softwareGraphics = software.NewGraphics2D();
			softwareGraphics.FillRectangle(0, 0, Width, Height, Color.White);
			((IImageFilterGraphics)softwareGraphics).FillPathWithFilteredImage(Shape, picture, fill);

			await AssertInteriorNear(gpu, software, Tolerance, allowNeighbour: true);
		}

		/// <summary>
		/// A filter's negative lobe over a layer's premultiplied colour lowers alpha more than colour beside an edge from
		/// opaque black to half-transparent red; each channel is capped at alpha, as C++ span_image_filter_rgba caps
		/// it, so over black nothing comes out redder than the half-transparent red itself.
		/// </summary>
		[Test]
		public async Task AFilteredLayerKeepsItsColourWithinItsAlpha()
		{
			var red = new Color(255, 0, 0, 128);
			var fill = new ImageFilterFill(new Bilinear(LayerQuad, 0, 0, LayerWidth, LayerHeight))
			{
				Kind = ImageFilterKind.Filter,
				Filter = new ImageFilterLookUpTable(new image_filter_sinc(4)),
			};

			ImageBuffer gpu = await CaptureLayerFill(new ColorF(0, 0, 0, 1), red, fill);

			int reddest = 0;
			for (int y = 0; y < Height; y++)
			{
				for (int x = 0; x < Width; x++)
				{
					reddest = Math.Max(reddest, gpu.GetPixel(x, y).red);
				}
			}

			// 128 is the red's premultiplied value; one more for rounding.
			await Assert.That(reddest).IsLessThanOrEqualTo(129);
		}

		private const int LayerWidth = 20;
		private const int LayerHeight = 16;

		// Screen quad the layer is mapped onto, slightly skewed so the mapping is truly bilinear.
		private static readonly double[] LayerQuad = { 10, 8, 110, 10, 108, 82, 12, 80 };

		/// <summary>Opaque black on the left half and <paramref name="right"/> on the right, painted into a layer at
		/// coordinate scale 2, then filled into the whole frame through <paramref name="fill"/>.</summary>
		private static async Task<ImageBuffer> CaptureLayerFill(ColorF background, Color right, ImageFilterFill fill)
		{
			using var capture = WebGpuOffscreenCapture.Create(Width, Height);
			var graphics = capture.BeginWidgetFrame(background);
			using var layer = graphics.CreateRetainedLayer();
			capture.Context.CoordinateScale = 2;
			using (var paint = layer.Begin(LayerWidth, LayerHeight))
			{
				paint.Graphics.FillRectangle(0, 0, LayerWidth / 2, LayerHeight, Color.Black);
				paint.Graphics.FillRectangle(LayerWidth / 2, 0, LayerWidth, LayerHeight, right);
			}

			capture.Context.CoordinateScale = 1;
			((IImageFilterGraphics)graphics).FillPathWithFilteredImage(Shape, layer, fill);
			return await capture.CaptureAsync();
		}

		private static ImageFilterFill Fill(string name, ImageBuffer image)
		{
			// Turned, scaled up and moved onto the middle of the target, as the demos' matrices are.
			Affine enlarge = Affine.NewTranslation(-image.Width / 2.0, -image.Height / 2.0) * Affine.NewRotation(0.3) * Affine.NewScaling(2.7, 2.3) * Affine.NewTranslation(Width / 2.0 + .3, Height / 2.0 - .2);
			Affine shrink = Affine.NewTranslation(-image.Width / 2.0, -image.Height / 2.0) * Affine.NewRotation(0.3) * Affine.NewScaling(0.45) * Affine.NewTranslation(Width / 2.0, Height / 2.0);

			// A quad narrower at the top than the bottom.
			double[] quad = { 10, 5, 110, 12, 90, 85, 25, 80 };
			var perspective = new Perspective(0, 0, image.Width, image.Height, quad);

			switch (name)
			{
				case "nearest":
					return new ImageFilterFill(enlarge) { Kind = ImageFilterKind.Nearest };
				case "bilinear":
					return new ImageFilterFill(enlarge) { Kind = ImageFilterKind.Bilinear };
				case "bicubic":
					return new ImageFilterFill(enlarge) { Kind = ImageFilterKind.Filter, Filter = new ImageFilterLookUpTable(new image_filter_bicubic()) };
				case "sinc4":
					return new ImageFilterFill(enlarge) { Kind = ImageFilterKind.Filter, Filter = new ImageFilterLookUpTable(new image_filter_sinc(4)) };
				case "bilinear2x2Perspective":
					return new ImageFilterFill(perspective) { Kind = ImageFilterKind.Filter, Filter = new ImageFilterLookUpTable(new image_filter_bilinear()) };
				case "resampleAffine":
					return new ImageFilterFill(shrink) { Kind = ImageFilterKind.Resample, Filter = new ImageFilterLookUpTable(new image_filter_bicubic()) };
				case "resamplePerspective":
					return new ImageFilterFill(perspective) { Kind = ImageFilterKind.Resample, Filter = new ImageFilterLookUpTable(new image_filter_bicubic()) };
				case "bilinearQuad":
					// pattern_perspective's "bilinear" mode: the 2x2 hanning through trans_bilinear, reflected past the edge.
					return new ImageFilterFill(new Bilinear(quad, 0, 0, image.Width, image.Height)) { Kind = ImageFilterKind.Filter, Edge = ImageFilterEdge.Reflect, Filter = new ImageFilterLookUpTable(new image_filter_hanning(), true) };
				case "brightnessToAlpha":
					// image_alpha: bilinear through a transparent clip, each pixel's alpha then looked up from its brightness.
					var table = new byte[256 * 3];
					for (int i = 0; i < table.Length; i++)
					{
						table[i] = (byte)(255 - (i / 3));
					}

					return new ImageFilterFill(shrink) { Kind = ImageFilterKind.Bilinear, Edge = ImageFilterEdge.Clip, BrightnessToAlpha = table };
				case "clip":
					return new ImageFilterFill(shrink) { Kind = ImageFilterKind.Filter, Edge = ImageFilterEdge.Clip, Background = new Color(0, 120, 0, 255), Filter = new ImageFilterLookUpTable(new image_filter_bicubic()) };
				default:
					return new ImageFilterFill(shrink) { Kind = ImageFilterKind.Filter, Edge = ImageFilterEdge.Reflect, Filter = new ImageFilterLookUpTable(new image_filter_bicubic()) };
			}
		}

		/// <summary>A smooth, opaque, non-symmetric picture, so a wrong axis, edge or offset shows.</summary>
		private static ImageBuffer TestImage(int width = 40, int height = 30)
		{
			var image = new ImageBuffer(width, height);
			for (int y = 0; y < height; y++)
			{
				for (int x = 0; x < width; x++)
				{
					int red = (int)(127.5 + (127.5 * Math.Sin(x * 0.35)));
					int green = y * 255 / (height - 1);
					int blue = (int)(127.5 + (127.5 * Math.Cos((x + (2 * y)) * 0.2)));
					image.SetPixel(x, y, new Color(red, green, blue, 255));
				}
			}

			return image;
		}

		private static async Task<ImageBuffer> Capture(Action<Graphics2D> draw)
		{
			using var capture = WebGpuOffscreenCapture.Create(Width, Height);
			draw(capture.BeginWidgetFrame(new ColorF(0, 0, 0, 0)));
			return await capture.CaptureAsync();
		}

		private static int Difference(Color a, Color b)
			=> Math.Max(Math.Max(Math.Abs(a.red - b.red), Math.Abs(a.green - b.green)), Math.Max(Math.Abs(a.blue - b.blue), Math.Abs(a.alpha - b.alpha)));

		private static async Task AssertInteriorNear(ImageBuffer gpu, ImageBuffer software, int tolerance, bool allowNeighbour)
		{
			for (int y = Margin; y < Height - Margin; y++)
			{
				for (int x = Margin; x < Width - Margin; x++)
				{
					Color g = gpu.GetPixel(x, y);
					Color s = software.GetPixel(x, y);
					int worst = Difference(g, s);
					for (int i = 0; allowNeighbour && i < 4 && worst > tolerance; i++)
					{
						worst = Math.Min(worst, Difference(g, software.GetPixel(x + (i == 0 ? 1 : i == 1 ? -1 : 0), y + (i == 2 ? 1 : i == 3 ? -1 : 0))));
					}

					if (worst > tolerance)
					{
						await Assert.That(worst).IsLessThanOrEqualTo(tolerance)
							.Because($"({x}, {y}): GPU {g} vs software {s}");
					}
				}
			}
		}
	}
}
