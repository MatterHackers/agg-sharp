using System;
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.GoldenImages
{
	/// <summary>
	/// The GPU fill's halo anti-aliasing (<c>HaloAaTesselator</c>) through <c>Graphics2DGpu</c> on a real device.
	/// </summary>
	[NotInParallel]
	public class HaloAaFillTests
	{
		private static VertexStorage Perturbed(IVertexSource source, double amount)
		{
			var path = new VertexStorage();
			int i = 0;
			foreach (var v in source.Vertices())
			{
				if (v.IsMoveTo || v.IsLineTo)
				{
					var p = v.Position + new Vector2(i % 2 == 0 ? amount : -amount, i % 3 == 0 ? -amount : amount);
					if (v.IsMoveTo)
					{
						path.MoveTo(p);
					}
					else
					{
						path.LineTo(p);
					}

					i++;
				}
				else if (v.IsClose)
				{
					path.ClosePolygon();
				}
			}

			return path;
		}

		private static async Task<ImageBuffer> RenderAsync(IVertexSource shape)
		{
			using var capture = WebGpuOffscreenCapture.Create();
			var graphics = capture.BeginWidgetFrame(new ColorF(1, 1, 1, 1));
			graphics.Render(shape, new Color(80, 80, 80));
			return await capture.CaptureAsync();
		}

		/// <summary>
		/// Sub-ULP changes to an outline must not move a pixel. The texture-ramp fill this replaced faded
		/// each triangle against its own edge only, so a moved interior diagonal flipped pixels near outline
		/// vertices between ~50% and 100% (83 levels at (54,80) and (54,99) for the r=11 circle).
		/// </summary>
		[Test]
		[Arguments(430.0, 275.0, 60.0, 40.0)]
		[Arguments(60.0, 90.0, 11.0, 11.0)]
		[Arguments(150.25, 190.0, 90.0, 60.0)]
		public async Task PerturbedOutlineChangesNoPixelBeyondRounding(double x, double y, double rx, double ry)
		{
			var a = (await RenderAsync(Perturbed(new Ellipse(x, y, rx, ry), 0))).GetBuffer();
			var b = (await RenderAsync(Perturbed(new Ellipse(x, y, rx, ry), 1e-13))).GetBuffer();
			int max = 0;
			for (int i = 0; i < a.Length; i++)
			{
				max = Math.Max(max, Math.Abs(a[i] - b[i]));
			}

			await Assert.That(max).IsLessThanOrEqualTo(2);
		}

		/// <summary>
		/// A translated source under a scale-only transform: the fill caches the source's geometry and carries
		/// its translation separately, and that translation has to be scaled with everything else.
		/// </summary>
		[Test]
		public async Task TranslatedSourceUnderScaleLandsScaled()
		{
			using var capture = WebGpuOffscreenCapture.Create();
			var graphics = capture.BeginWidgetFrame(new ColorF(1, 1, 1, 1));
			graphics.SetTransform(Transform.Affine.NewScaling(2));
			graphics.Render(new VertexSourceApplyTransform(new Ellipse(0, 0, 4, 4), Transform.Affine.NewTranslation(20, 20)), Color.Black);
			var image = await capture.CaptureAsync();

			// Centered on (40, 40) with radius 8 - not (10, 10), where dividing the translation by the scale put
			// it. Taking the darker of a pixel and its vertical mirror keeps this independent of capture flip.
			int Red(int x, int y) => Math.Min(image.GetPixel(x, y).red, image.GetPixel(x, image.Height - 1 - y).red);
			await Assert.That(Red(40, 40)).IsEqualTo(0);
			await Assert.That(Red(10, 10)).IsEqualTo(255);
		}

		private static VertexStorage Box(double left, double bottom, double right, double top)
		{
			var box = new VertexStorage();
			box.MoveTo(left, bottom);
			box.LineTo(right, bottom);
			box.LineTo(right, top);
			box.LineTo(left, top);
			box.ClosePolygon();
			return box;
		}

		/// <summary>Total coverage of a black fill over white: each pixel's darkness, summed, in pixels.</summary>
		private static double Ink(ImageBuffer image)
		{
			double ink = 0;
			for (int y = 0; y < image.Height; y++)
			{
				for (int x = 0; x < image.Width; x++)
				{
					ink += (255 - image.GetPixel(x, y).red) / 255.0;
				}
			}

			return ink;
		}

		/// <summary>
		/// An edge on a pixel boundary covers the pixel inside it fully and the pixel outside it not at all,
		/// as the software rasterizer does. A halo that faded from the edge a whole pixel outward left the
		/// outside pixel half covered, drawing every GPU shape about a pixel fatter than software.
		/// </summary>
		[Test]
		public async Task PixelAlignedEdgeLeavesTheOutsidePixelUntouched()
		{
			using var capture = WebGpuOffscreenCapture.Create();
			var graphics = capture.BeginWidgetFrame(new ColorF(1, 1, 1, 1));
			graphics.Render(Box(40, 100, 80, 200), Color.Black);
			var image = await capture.CaptureAsync();

			// Row 150 is inside the box whichever way the capture is flipped.
			await Assert.That((int)image.GetPixel(39, 150).red).IsGreaterThanOrEqualTo(253);
			await Assert.That((int)image.GetPixel(40, 150).red).IsLessThanOrEqualTo(2);
			await Assert.That((int)image.GetPixel(79, 150).red).IsLessThanOrEqualTo(2);
			await Assert.That((int)image.GetPixel(80, 150).red).IsGreaterThanOrEqualTo(253);
		}

		/// <summary>
		/// Edges through pixel centres: the row's total coverage stays within half a pixel of the box's
		/// width. The interior is drawn opaque, so the pixel whose centre sits exactly on an edge takes full
		/// coverage when the rasterizer's fill rule gives it to the interior, and half when it gives it to
		/// the halo - it can not be half on both sides the way software AGG's area coverage is.
		/// </summary>
		[Test]
		public async Task HalfPixelOffsetEdgesStayWithinHalfAPixelOfTheWidth()
		{
			using var capture = WebGpuOffscreenCapture.Create();
			var graphics = capture.BeginWidgetFrame(new ColorF(1, 1, 1, 1));
			graphics.Render(Box(40.5, 100, 80.5, 200), Color.Black);
			var image = await capture.CaptureAsync();

			double rowInk = 0;
			for (int x = 30; x < 90; x++)
			{
				rowInk += (255 - image.GetPixel(x, 150).red) / 255.0;
			}

			await Assert.That(rowInk).IsGreaterThanOrEqualTo(39.9).And.IsLessThanOrEqualTo(40.55);
			await Assert.That((int)image.GetPixel(39, 150).red).IsGreaterThanOrEqualTo(253);
			await Assert.That((int)image.GetPixel(81, 150).red).IsGreaterThanOrEqualTo(253);
		}

		/// <summary>
		/// Diagonal edges: the GPU fill's total coverage stays close to software AGG's. Only pixels whose
		/// centre is inside the outline but within half a pixel of it may take more than their share (the
		/// interior is opaque), which averages an eighth of a pixel per unit of edge; a halo reaching a
		/// whole pixel out added half a pixel per unit of edge.
		/// </summary>
		[Test]
		public async Task DiagonalEdgeCoverageMatchesSoftware()
		{
			var diamond = new VertexStorage();
			diamond.MoveTo(200.3, 100.2);
			diamond.LineTo(260.1, 170.7);
			diamond.LineTo(190.6, 240.4);
			diamond.LineTo(130.8, 160.9);
			diamond.ClosePolygon();
			double perimeter = 0;
			var corners = new[] { new Vector2(200.3, 100.2), new Vector2(260.1, 170.7), new Vector2(190.6, 240.4), new Vector2(130.8, 160.9) };
			for (int i = 0; i < 4; i++)
			{
				perimeter += (corners[(i + 1) % 4] - corners[i]).Length;
			}

			using var capture = WebGpuOffscreenCapture.Create();
			var graphics = capture.BeginWidgetFrame(new ColorF(1, 1, 1, 1));
			graphics.Render(diamond, Color.Black);
			double gpuInk = Ink(await capture.CaptureAsync());

			var software = new ImageBuffer(capture.Width, capture.Height);
			var softwareGraphics = software.NewGraphics2D();
			softwareGraphics.Clear(Color.White);
			softwareGraphics.Render(diamond, Color.Black);
			double softwareInk = Ink(software);

			await Assert.That(Math.Abs(gpuInk - softwareInk)).IsLessThan(perimeter * .2);
		}
	}
}
