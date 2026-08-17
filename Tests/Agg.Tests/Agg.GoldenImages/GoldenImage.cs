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
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Tests.TestingInfrastructure;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.GoldenImages
{
	/// <summary>
	/// The rendering regression oracle: renders are compared against checked-in golden PNGs, so any change
	/// to the render path has to declare itself.
	/// </summary>
	/// <remarks>
	/// <b>Tolerance defaults to zero on purpose.</b> The port's stated goal is 1:1 pixel identity; a suite
	/// that starts out permissive can never be tightened later because nobody knows which of the allowed
	/// differences were real. Relax <c>channelTolerance</c>/<c>maxPercentDifferingPixels</c> per call only
	/// when there is evidence a difference is unavoidable, and say why at the call site.
	/// <para>
	/// <b>Regenerating.</b> Set <c>AGG_REGEN_GOLDENS=1</c> and run the suite: every check writes its render
	/// to this run's backend folder under <c>TestData\GoldenImages</c> instead of comparing, and reports
	/// what it wrote. Every check also <i>fails</i>, deliberately: nothing was verified, and a leaked
	/// environment variable must not be able to turn the suite into a green no-op. Then run again
	/// <i>without</i> the variable - a suite whose goldens do not reproduce on the machine that captured
	/// them is measuring noise, and every later parity check inherits that noise.
	/// </para>
	/// <para>
	/// <b>Goldens are GPU specific, so there is a set per backend</b> -
	/// <c>TestData\GoldenImages\d3d12</c>, <c>...\metal</c>, <c>...\vulkan</c>, named by
	/// <see cref="TestRenderBackend.NativeGoldenFolderName"/> and chosen by the OS this run is on.
	/// Rasterization tie-breaking differs between backends, vendors and driver versions: the Metal set
	/// differs from the D3D12 set on 26 of 28 images, and every one of those differences is a
	/// one-or-two-level antialiasing edge, not a difference in geometry, layout or colour. Keeping the
	/// sets apart is what lets the tolerance stay at zero, which is the only setting under which a real
	/// one-pixel regression is still visible. A mismatch inside a set is therefore a regression to
	/// investigate, not cross-machine noise to wave through - though the same caveat still applies within
	/// a backend across GPU vendors and drivers, so see the failure artifacts before concluding anything.
	/// </para>
	/// </remarks>
	public static class GoldenImage
	{
		/// <summary>Set to 1 to write goldens rather than compare against them.</summary>
		public const string RegenerateEnvironmentVariable = "AGG_REGEN_GOLDENS";

		private const string GoldenFolderName = "GoldenImages";

		private const string FailureFolderName = "GoldenImageFailures";

		private static string locatedGoldenDirectory;

		private static readonly object locateLock = new object();

		/// <summary>True when this run writes goldens instead of checking them.</summary>
		public static bool Regenerating
			=> Environment.GetEnvironmentVariable(RegenerateEnvironmentVariable) == "1";

		/// <summary>
		/// Compares <paramref name="rendered"/> against the golden named <paramref name="goldenName"/>,
		/// or writes it as the golden when <see cref="Regenerating"/>.
		/// </summary>
		/// <param name="channelTolerance">Largest absolute per-channel difference (0-255) still counted as
		/// equal. Zero means exact.</param>
		/// <param name="maxPercentDifferingPixels">Percentage of pixels (0-100) allowed to exceed
		/// <paramref name="channelTolerance"/>. Zero means none may.</param>
		public static async Task Check(
			ImageBuffer rendered,
			string goldenName,
			int channelTolerance = 0,
			double maxPercentDifferingPixels = 0)
		{
			if (rendered == null)
			{
				throw new ArgumentNullException(nameof(rendered));
			}

			string goldenPath = Path.Combine(GoldenDirectory(), goldenName + ".png");

			if (Regenerating)
			{
				WritePng(rendered, goldenPath);
				Console.WriteLine($"golden regenerated: {goldenPath} ({new FileInfo(goldenPath).Length} bytes)");

				// Nothing was verified, so the test fails on purpose. A regenerate run must never report
				// green: the variable can be left set in a shell or leak into CI, and a whole parity suite
				// that quietly rewrites its own expectations and passes is worse than no suite at all.
				await Assert.That(Regenerating).IsFalse()
					.Because($"golden '{goldenName}' was regenerated, not compared - it was written to"
						+ $" '{goldenPath}'. Re-run without {RegenerateEnvironmentVariable} to verify the suite"
						+ " actually reproduces what it just captured.");
				return;
			}

			if (!File.Exists(goldenPath))
			{
				// A missing golden fails rather than being captured on the spot: an auto-created golden is a
				// test that agrees with whatever it just rendered, and on a backend nobody has baselined yet
				// that is exactly when the render is least trustworthy.
				string capturedPath = WriteFailureArtifact(rendered, goldenName, "actual");
				bool goldenExists = File.Exists(goldenPath);
				await Assert.That(goldenExists).IsTrue().Because(
					$"there is no '{TestRenderBackend.NativeGoldenFolderName}' golden for '{goldenName}'."
					+ $" Expected '{goldenPath}'. What this run rendered is at '{capturedPath}' - check it against"
					+ $" the same golden in another backend's folder under '{GoldenRootDirectory()}', confirm the"
					+ " difference is rasterization and not a rendering bug, then re-run with"
					+ $" {RegenerateEnvironmentVariable}=1 to capture the"
					+ $" '{TestRenderBackend.NativeGoldenFolderName}' set and commit the PNGs.");
				return;
			}

			var golden = new ImageBuffer();
			ImageIO.LoadImageData(goldenPath, golden);

			var difference = Compare(golden, rendered, channelTolerance);

			bool matches = difference.SameSize
				&& difference.PercentDiffering <= maxPercentDifferingPixels;

			if (!matches)
			{
				string actualPath = WriteFailureArtifact(rendered, goldenName, "actual");
				string diffPath = difference.SameSize
					? WriteFailureArtifact(BuildDiffImage(golden, rendered, channelTolerance), goldenName, "diff")
					: "(no diff image - the sizes differ)";

				await Assert.That(matches).IsTrue().Because(
					$"'{goldenName}' does not match '{goldenPath}'. {difference.Describe()}"
					+ $" Rendered: '{actualPath}'. Diff: '{diffPath}'."
					+ $" If this is a deliberate change, re-run with {RegenerateEnvironmentVariable}=1 and commit"
					+ " the new golden.");
				return;
			}

			DeleteStaleArtifacts(goldenName);

			await Assert.That(matches).IsTrue();
		}

		/// <summary>The outcome of one comparison, in the terms the failure message reports.</summary>
		public readonly struct Difference
		{
			public Difference(bool sameSize, string sizes, long differingPixels, long totalPixels, int maxChannelDelta)
			{
				SameSize = sameSize;
				Sizes = sizes;
				DifferingPixels = differingPixels;
				TotalPixels = totalPixels;
				MaxChannelDelta = maxChannelDelta;
			}

			public bool SameSize { get; }

			public string Sizes { get; }

			public long DifferingPixels { get; }

			public long TotalPixels { get; }

			public int MaxChannelDelta { get; }

			public double PercentDiffering => TotalPixels == 0 ? 0 : DifferingPixels * 100.0 / TotalPixels;

			public string Describe()
			{
				if (!SameSize)
				{
					return $"The sizes differ ({Sizes}).";
				}

				return $"{DifferingPixels} of {TotalPixels} pixels differ ({PercentDiffering:0.####}%),"
					+ $" largest channel delta {MaxChannelDelta}.";
			}
		}

		/// <summary>
		/// Counts the pixels of <paramref name="rendered"/> that differ from <paramref name="golden"/> by
		/// more than <paramref name="channelTolerance"/> in any channel, and the largest delta seen.
		/// </summary>
		public static Difference Compare(ImageBuffer golden, ImageBuffer rendered, int channelTolerance)
		{
			if (golden.Width != rendered.Width || golden.Height != rendered.Height)
			{
				return new Difference(
					false,
					$"golden {golden.Width}x{golden.Height}, rendered {rendered.Width}x{rendered.Height}",
					0,
					0,
					0);
			}

			var goldenBuffer = golden.GetBuffer();
			var renderedBuffer = rendered.GetBuffer();

			long differing = 0;
			int maxDelta = 0;

			for (int y = 0; y < golden.Height; y++)
			{
				int goldenOffset = golden.GetBufferOffsetY(y);
				int renderedOffset = rendered.GetBufferOffsetY(y);

				for (int x = 0; x < golden.Width; x++)
				{
					int pixelDelta = 0;
					for (int channel = 0; channel < 4; channel++)
					{
						int delta = Math.Abs(
							goldenBuffer[goldenOffset + (x * 4) + channel]
							- renderedBuffer[renderedOffset + (x * 4) + channel]);
						if (delta > pixelDelta)
						{
							pixelDelta = delta;
						}
					}

					if (pixelDelta > maxDelta)
					{
						maxDelta = pixelDelta;
					}

					if (pixelDelta > channelTolerance)
					{
						differing++;
					}
				}
			}

			return new Difference(true, null, differing, (long)golden.Width * golden.Height, maxDelta);
		}

		/// <summary>
		/// Builds a human-readable diff: the golden dimmed to a gray wash, with every out-of-tolerance
		/// pixel painted magenta so a handful of stray pixels are still findable by eye at a glance.
		/// </summary>
		private static ImageBuffer BuildDiffImage(ImageBuffer golden, ImageBuffer rendered, int channelTolerance)
		{
			var diff = new ImageBuffer(golden.Width, golden.Height, 32, new BlenderBGRA());
			var goldenBuffer = golden.GetBuffer();
			var renderedBuffer = rendered.GetBuffer();
			var diffBuffer = diff.GetBuffer();

			for (int y = 0; y < golden.Height; y++)
			{
				int goldenOffset = golden.GetBufferOffsetY(y);
				int renderedOffset = rendered.GetBufferOffsetY(y);
				int diffOffset = diff.GetBufferOffsetY(y);

				for (int x = 0; x < golden.Width; x++)
				{
					int pixelDelta = 0;
					for (int channel = 0; channel < 4; channel++)
					{
						int delta = Math.Abs(
							goldenBuffer[goldenOffset + (x * 4) + channel]
							- renderedBuffer[renderedOffset + (x * 4) + channel]);
						if (delta > pixelDelta)
						{
							pixelDelta = delta;
						}
					}

					int destination = diffOffset + (x * 4);
					if (pixelDelta > channelTolerance)
					{
						diffBuffer[destination + 0] = 255;
						diffBuffer[destination + 1] = 0;
						diffBuffer[destination + 2] = 255;
						diffBuffer[destination + 3] = 255;
					}
					else
					{
						int gray = (goldenBuffer[goldenOffset + (x * 4) + 0]
							+ goldenBuffer[goldenOffset + (x * 4) + 1]
							+ goldenBuffer[goldenOffset + (x * 4) + 2]) / 3;
						byte washed = (byte)(128 + (gray / 4));
						diffBuffer[destination + 0] = washed;
						diffBuffer[destination + 1] = washed;
						diffBuffer[destination + 2] = washed;
						diffBuffer[destination + 3] = 255;
					}
				}
			}

			diff.MarkImageChanged();
			return diff;
		}

		private static string WriteFailureArtifact(ImageBuffer image, string goldenName, string suffix)
		{
			string path = Path.Combine(FailureDirectory(), $"{goldenName}.{suffix}.png");
			WritePng(image, path);
			return path;
		}

		private static void DeleteStaleArtifacts(string goldenName)
		{
			// A passing test must not leave a previous run's failure images behind: they are named after the
			// test, so a stale pair reads exactly like a fresh failure to whoever opens the folder next.
			foreach (string suffix in new[] { "actual", "diff" })
			{
				string path = Path.Combine(FailureDirectory(), $"{goldenName}.{suffix}.png");
				if (File.Exists(path))
				{
					File.Delete(path);
				}
			}
		}

		/// <summary>
		/// Writes <paramref name="image"/> as a PNG, replacing whatever is there.
		/// </summary>
		/// <remarks>
		/// <see cref="ImageIO.SaveImageData(string, IImageByte)"/> silently returns false rather than
		/// overwriting an existing file, so without the delete a regenerate run would leave every golden at
		/// its old contents and report success.
		/// </remarks>
		private static void WritePng(ImageBuffer image, string path)
		{
			Directory.CreateDirectory(Path.GetDirectoryName(path));
			if (File.Exists(path))
			{
				File.Delete(path);
			}

			if (!ImageIO.SaveImageData(path, image))
			{
				throw new IOException($"Could not write '{path}'.");
			}
		}

		private static string FailureDirectory() => Path.Combine(AppContext.BaseDirectory, FailureFolderName);

		/// <summary>
		/// The golden set this run is judged against: the
		/// <see cref="TestRenderBackend.NativeGoldenFolderName"/> folder inside
		/// <see cref="GoldenRootDirectory"/>.
		/// </summary>
		public static string GoldenDirectory()
			=> Path.Combine(GoldenRootDirectory(), TestRenderBackend.NativeGoldenFolderName);

		/// <summary>
		/// Finds <c>TestData\GoldenImages</c> in the source tree - the folder holding one subfolder per
		/// backend - falling back to a folder beside the test binary only when there is no tree to find.
		/// </summary>
		/// <remarks>
		/// Preferring the tree is <c>AggDrawingTests.ControlImageDirectory</c>'s choice for its reason: the
		/// build also copies the goldens next to the binary so the exe runs from a bare <c>bin\</c>, and a
		/// <c>PreserveNewest</c> copy that did not happen would leave that stale copy shadowing the
		/// checked-in ones - tests passing against images nobody can see in a diff. The extra probe for the
		/// solution file exists because regenerating has to be able to create the folder the first time.
		/// <para>
		/// The probe looks for the shared root rather than for this run's backend folder on purpose: on a
		/// backend nobody has baselined yet that folder does not exist, and falling through to <c>bin\</c>
		/// would hide the checked-in tree from the one run that is meant to populate it.
		/// </para>
		/// </remarks>
		public static string GoldenRootDirectory()
		{
			lock (locateLock)
			{
				if (locatedGoldenDirectory != null)
				{
					return locatedGoldenDirectory;
				}

				var probes = new List<string>();
				string probe = Path.GetDirectoryName(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
				string solutionRoot = null;
				for (int up = 0; up < 8 && probe != null; up++)
				{
					probes.Add(probe);
					if (solutionRoot == null && File.Exists(Path.Combine(probe, "agg-sharp.sln")))
					{
						solutionRoot = probe;
					}

					probe = Path.GetDirectoryName(probe);
				}

				foreach (string candidate in probes)
				{
					string goldenFolder = Path.Combine(candidate, "TestData", GoldenFolderName);
					if (Directory.Exists(goldenFolder))
					{
						locatedGoldenDirectory = goldenFolder;
						return locatedGoldenDirectory;
					}
				}

				locatedGoldenDirectory = solutionRoot != null
					? Path.Combine(solutionRoot, "TestData", GoldenFolderName)
					: Path.Combine(AppContext.BaseDirectory, GoldenFolderName);

				return locatedGoldenDirectory;
			}
		}
	}
}
