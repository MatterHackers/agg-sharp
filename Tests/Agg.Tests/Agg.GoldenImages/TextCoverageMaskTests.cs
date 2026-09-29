using System;
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.LcdCoverage;
using MatterHackers.RenderGl;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.GoldenImages
{
	/// <summary>
	/// Grayscale GPU text goes through <see cref="TextCoverageMaskCache"/> - AGG coverage drawn as a texture -
	/// so it has the software renderer's weight, not the halo fill's half-pixel outset.
	/// </summary>
	[NotInParallel]
	public class TextCoverageMaskTests
	{
		private static async Task<T> WithGrayText<T>(Func<Task<T>> body)
		{
			bool wasLcdEnabled = LcdRenderSettings.Enabled;
			bool wasSnapping = TypeFacePrinter.SnapBaselinesToWholePixels;
			try
			{
				LcdRenderSettings.Enabled = false;
				TypeFacePrinter.SnapBaselinesToWholePixels = true;
				return await body();
			}
			finally
			{
				LcdRenderSettings.Enabled = wasLcdEnabled;
				TypeFacePrinter.SnapBaselinesToWholePixels = wasSnapping;
			}
		}

		[Test]
		[Arguments(false)]
		[Arguments(true)]
		public async Task GpuTextMatchesTheSoftwareRender(bool lightOnDark)
		{
			ColorF background = lightOnDark ? GoldenTextScenes.DarkBackground : new ColorF(1, 1, 1, 1);
			Action<Graphics2D> scene = lightOnDark ? GoldenTextScenes.SizeLadderLightOnDark : GoldenTextScenes.SizeLadderDarkOnLight;
			await WithGrayText(async () =>
			{
				using var capture = WebGpuOffscreenCapture.Create();
				var gpuGraphics = capture.BeginWidgetFrame(background);
				scene(gpuGraphics);
				var gpu = await capture.CaptureAsync();

				await AssertMatchesSoftware(gpu, background, scene);
				return 0;
			});
		}

		/// <summary>
		/// A text run wider than <see cref="TextCoverageMaskCache.MaxMaskExtent"/> - a long unwrapped line in a
		/// horizontally scrolling view, only a window of it on screen - still draws at the software weight. It used
		/// to get no mask and fall back to the halo fill, which drew those lines visibly bold.
		/// </summary>
		[Test]
		public async Task GpuTextWiderThanOneMaskMatchesTheSoftwareRender()
		{
			ColorF background = new ColorF(1, 1, 1, 1);
			string line = string.Concat(Enumerable.Repeat("Lorem ipsum dolor sit amet, consectetur adipiscing elit. ", 12));
			await Assert.That(new TypeFacePrinter(line, 14).LocalBounds.Width).IsGreaterThan(TextCoverageMaskCache.MaxMaskExtent * 2);

			// Starts off the left edge, as a run scrolled sideways does.
			Action<Graphics2D> scene = graphics =>
			{
				for (int i = 0; i < 8; i++)
				{
					graphics.DrawString(line, -300.25 - 37 * i, 30 + 40 * i, 14, color: Color.Black);
				}
			};

			await WithGrayText(async () =>
			{
				using var capture = WebGpuOffscreenCapture.Create();
				var gpuGraphics = capture.BeginWidgetFrame(background);
				scene(gpuGraphics);
				var gpu = await capture.CaptureAsync();
				await AssertMatchesSoftware(gpu, background, scene);
				return 0;
			});
		}

		[Test]
		public async Task OversizedRunRasterizesOnlyItsVisibleTiles()
		{
			TextCoverageMaskCache.Clear();
			var printer = new TypeFacePrinter(string.Concat(Enumerable.Repeat("Lorem ipsum dolor sit amet. ", 40)), 14);
			object identity = ((VertexSource.IVertexSourceRenderIdentity)printer).RenderIdentity;
			var masks = new System.Collections.Generic.List<TextCoverageMaskCache.CoverageMask>();
			int before = TextCoverageMaskCache.RasterizedCount;

			// A view 300 wide, 1000 pixels into the run: tiles 1 and 2 only, as short as the line is tall.
			TextCoverageMaskCache.GetMasks(identity, printer, Transform.Affine.NewIdentity(), new RectangleDouble(1000, -100, 1300, 100), masks);
			await Assert.That(masks.Count).IsEqualTo(2);
			await Assert.That(masks[0].OriginX).IsLessThanOrEqualTo(1000);
			await Assert.That(masks[0].OriginX + masks[0].Image.Width).IsEqualTo(masks[1].OriginX);
			await Assert.That(masks[1].OriginX + masks[1].Image.Width).IsGreaterThanOrEqualTo(1300);
			await Assert.That(masks[0].Image.Width).IsEqualTo(TextCoverageMaskCache.TileExtent);
			await Assert.That(masks[0].Image.Height).IsLessThan(40);
			// The whole-run bounds plus the two tiles.
			await Assert.That(TextCoverageMaskCache.RasterizedCount - before).IsEqualTo(3);

			masks.Clear();
			TextCoverageMaskCache.GetMasks(identity, printer, Transform.Affine.NewIdentity(), new RectangleDouble(1000, -100, 1300, 100), masks);
			await Assert.That(TextCoverageMaskCache.RasterizedCount - before).IsEqualTo(3);
			TextCoverageMaskCache.Clear();
		}

		/// <summary>Asserts <paramref name="gpu"/> is <paramref name="scene"/>'s software render to within 2 levels nearly everywhere.</summary>
		private static async Task AssertMatchesSoftware(ImageBuffer gpu, ColorF background, Action<Graphics2D> scene)
		{
			var software = new ImageBuffer(gpu.Width, gpu.Height);
			var softwareGraphics = software.NewGraphics2D();
			softwareGraphics.Clear(background);
			scene(softwareGraphics);

			byte[] a = gpu.GetBuffer();
			byte[] b = software.GetBuffer();
			int inked = 0;
			int off = 0;
			for (int i = 0; i < a.Length; i += 4)
			{
				int delta = 0;
				for (int c = 0; c < 3; c++)
				{
					delta = Math.Max(delta, Math.Abs(a[i + c] - b[i + c]));
				}

				if (b[i] != b[0] || a[i] != a[0])
				{
					inked++;
				}

				if (delta > 2)
				{
					off++;
				}
			}

			await Assert.That(inked).IsGreaterThan(1000);
			// Nearly every pixel within 2 levels: the halo fill had 15000+ pixels off by up to 216.
			await Assert.That(off).IsLessThanOrEqualTo(inked / 100);
		}

		[Test]
		public async Task ChurnStaysInTheByteBudgetAndEvictsLeastRecentlyUsed()
		{
			long wasBudget = TextCoverageMaskCache.MaxCachedBytes;
			try
			{
				TextCoverageMaskCache.Clear();
				TextCoverageMaskCache.MaxCachedBytes = 40 * 1024;

				CoverageMaskOf(new TypeFacePrinter("Visible label", 12));
				int before = TextCoverageMaskCache.RasterizedCount;

				// A scrolling console: every line distinct, while one label stays on screen and repaints.
				for (int i = 0; i < 200; i++)
				{
					CoverageMaskOf(new TypeFacePrinter($"console line {i}", 12));
					await Assert.That(TextCoverageMaskCache.CachedBytes).IsLessThanOrEqualTo(TextCoverageMaskCache.MaxCachedBytes);
					if (i % 3 == 0)
					{
						CoverageMaskOf(new TypeFacePrinter("Visible label", 12));
					}
				}

				// Only the console lines were rasterized; the label kept being used, so it was never evicted.
				await Assert.That(TextCoverageMaskCache.RasterizedCount - before).IsEqualTo(200);

				// The oldest line was.
				CoverageMaskOf(new TypeFacePrinter("console line 0", 12));
				await Assert.That(TextCoverageMaskCache.RasterizedCount - before).IsEqualTo(201);
			}
			finally
			{
				TextCoverageMaskCache.MaxCachedBytes = wasBudget;
				TextCoverageMaskCache.Clear();
			}
		}

		/// <summary>
		/// The mask is coverage for a GPU draw that blends later, never LCD: with LCD text on, the mask's own CPU
		/// raster took the subpixel path, which leaves alpha alone, so the coverage read from alpha was zero and
		/// every text run drawn into a GPU retained layer (the demo's windows, in the browser) vanished.
		/// </summary>
		[Test]
		public async Task LcdTextSettingDoesNotEmptyTheMask()
		{
			bool wasLcdEnabled = LcdRenderSettings.Enabled;
			try
			{
				var printer = new TypeFacePrinter("Widget Gallery", 14);
				TextCoverageMaskCache.Clear();
				LcdRenderSettings.Enabled = false;
				byte[] gray = CoverageMaskOf(printer).Image.GetBuffer();

				TextCoverageMaskCache.Clear();
				LcdRenderSettings.Enabled = true;
				byte[] withLcdOn = CoverageMaskOf(printer).Image.GetBuffer();

				await Assert.That(gray.Any(b => b != 0)).IsTrue();
				await Assert.That(withLcdOn.SequenceEqual(gray)).IsTrue()
					.Because("the LCD setting is for the frame the mask is drawn onto, not for the mask itself");
			}
			finally
			{
				LcdRenderSettings.Enabled = wasLcdEnabled;
				TextCoverageMaskCache.Clear();
			}
		}

		private static TextCoverageMaskCache.CoverageMask CoverageMaskOf(TypeFacePrinter printer)
			=> TextCoverageMaskCache.GetMask(((VertexSource.IVertexSourceRenderIdentity)printer).RenderIdentity, printer, Transform.Affine.NewIdentity());

		[Test]
		public async Task SecondFrameRasterizesNothing()
		{
			await WithGrayText(async () =>
			{
				using var capture = WebGpuOffscreenCapture.Create();
				var graphics = capture.BeginWidgetFrame(new ColorF(1, 1, 1, 1));
				graphics.DrawString("Cached text run", 20, 40, 14, color: Color.Black);
				await capture.CaptureAsync();
				int rasterized = TextCoverageMaskCache.RasterizedCount;

				graphics = capture.BeginWidgetFrame(new ColorF(1, 1, 1, 1));
				// Moved a whole pixel by the transform (DrawString's own position is part of the run's identity):
				// only the phase is in the mask, so it is reused.
				graphics.SetTransform(Transform.Affine.NewTranslation(1, 0));
				graphics.DrawString("Cached text run", 20, 40, 14, color: Color.Black);
				await capture.CaptureAsync();

				await Assert.That(TextCoverageMaskCache.RasterizedCount).IsEqualTo(rasterized);
				return 0;
			});
		}
	}
}
