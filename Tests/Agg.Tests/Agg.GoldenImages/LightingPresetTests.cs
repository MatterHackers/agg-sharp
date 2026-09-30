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
using System.IO;
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.PolygonMesh;
using MatterHackers.RenderGl;
using MatterHackers.RenderGl.OpenGl;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.GoldenImages
{
	/// <summary>
	/// Every scene lighting preset renders a white part with no face gone to mud. The sphere shows every
	/// front-facing normal, so its darkest interior pixel is the darkest face the preset can produce.
	/// "Current" reproducing today's look is proved by <see cref="GoldenSceneTests"/>, whose goldens are
	/// rendered with a default <see cref="LightingData"/>.
	/// </summary>
	[NotInParallel]
	public class LightingPresetTests
	{
		// A white face must never land below this, whatever it faces.
		private const int BrightnessFloor = 38; // 0.15 of 255

		/// <summary>Set to a directory to keep one PNG per preset, for comparing the looks by eye.</summary>
		private const string DumpDirectoryVariable = "AGG_LIGHTING_PRESET_DUMP";

		[Test]
		public async Task CurrentIsTheDefaultAndTheFallback()
		{
			await Assert.That(LightingPresets.All[0]).IsSameReferenceAs(LightingPresets.Current);
			await Assert.That(LightingPresets.Find("no such preset")).IsSameReferenceAs(LightingPresets.Current);
		}

		[Test]
		public async Task EveryPresetKeepsAWhitePartAboveTheFloor()
		{
			var sphere = Golden3DScenes.CreateSphere(80, 48, 32);

			foreach (var preset in LightingPresets.All)
			{
				using var capture = WebGpuOffscreenCapture.Create();
				capture.ClearTo(new ColorF(0, 0, 0, 1));
				var world = Golden3DScenes.CreateCamera(capture.Width, capture.Height);

				capture.RenderScene(
					world,
					preset.Create(),
					() => RenderHelper.Render(capture.Gl, sphere, Color.White, Matrix4X4.Identity, RenderTypes.Shaded));

				var rendered = await capture.CaptureAsync();
				await Assert.That(capture.Device.LastUncapturedError).IsNull();

				string dumpDirectory = Environment.GetEnvironmentVariable(DumpDirectoryVariable);
				if (!string.IsNullOrEmpty(dumpDirectory))
				{
					await DumpComparisonScene(preset, dumpDirectory, "light", new ColorF(0.914f, 0.914f, 0.914f, 1));
					await DumpComparisonScene(preset, dumpDirectory, "dark", new ColorF(0.17f, 0.17f, 0.19f, 1));
				}

				int interiorPixels = 0;
				int darkest = 255;
				for (int y = 1; y < rendered.Height - 1; y++)
				{
					for (int x = 1; x < rendered.Width - 1; x++)
					{
						// Interior only: an anti-aliased silhouette pixel is blended with the black clear.
						if (!IsInterior(rendered, x, y))
						{
							continue;
						}

						var pixel = rendered.GetPixel(x, y);
						interiorPixels++;
						darkest = Math.Min(darkest, Math.Min(pixel.red, Math.Min(pixel.green, pixel.blue)));
					}
				}

				await Assert.That(interiorPixels).IsGreaterThan(1000).Because($"'{preset.Name}' drew no sphere");
				await Assert.That(darkest).IsGreaterThanOrEqualTo(BrightnessFloor)
					.Because($"'{preset.Name}' shades some face of a white part at {darkest}/255");
			}
		}

		/// <summary>
		/// A white box, a light grey slab and two coloured parts on a theme-like background, for judging a
		/// preset by eye (light = the Light-White theme's bed background).
		/// </summary>
		private static async Task DumpComparisonScene(LightingPreset preset, string directory, string theme, ColorF background)
		{
			using var capture = WebGpuOffscreenCapture.Create();
			capture.ClearTo(background);
			var world = Golden3DScenes.CreateCamera(capture.Width, capture.Height);
			var box = PlatonicSolids.CreateCube(60, 60, 60);
			var slab = PlatonicSolids.CreateCube(140, 40, 14);
			var sphere = Golden3DScenes.CreateSphere(38, 24, 16);

			capture.RenderScene(
				world,
				preset.Create(),
				() =>
				{
					RenderHelper.Render(capture.Gl, slab, new Color(225, 225, 228), Matrix4X4.CreateTranslation(0, 0, -34), RenderTypes.Shaded);
					RenderHelper.Render(capture.Gl, box, Color.White, Matrix4X4.CreateRotationZ(MathHelper.Tau * 0.06) * Matrix4X4.CreateTranslation(-52, -18, 8), RenderTypes.Shaded);
					RenderHelper.Render(capture.Gl, box, new Color(60, 130, 220), Matrix4X4.CreateRotationX(MathHelper.Tau * 0.11) * Matrix4X4.CreateTranslation(46, 12, 20), RenderTypes.Shaded);
					RenderHelper.Render(capture.Gl, sphere, new Color(210, 70, 50), Matrix4X4.CreateTranslation(0, 34, 34), RenderTypes.Shaded);
				});

			var rendered = await capture.CaptureAsync();
			Directory.CreateDirectory(directory);
			string path = Path.Combine(directory, $"{preset.Name}-{theme}.png");
			ImageIO.SaveImageData(path, rendered);
		}

		private static bool IsInterior(ImageBuffer image, int x, int y)
		{
			for (int dy = -1; dy <= 1; dy++)
			{
				for (int dx = -1; dx <= 1; dx++)
				{
					var pixel = image.GetPixel(x + dx, y + dy);
					if (pixel.red == 0 && pixel.green == 0 && pixel.blue == 0)
					{
						return false;
					}
				}
			}

			return true;
		}
	}
}
