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
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg.Platform;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// <see cref="ZipStaticData"/> has to answer exactly what the disk-backed <see cref="StaticData"/>
	/// answers for the same asset tree - it is installed in place of it on a host with no asset folder, and
	/// every call site in the app was written against the disk one. These tests build a small tree, zip it,
	/// and compare the two providers member by member.
	/// </summary>
	/// <remarks>
	/// Keyless <c>[NotInParallel]</c>: the disk provider is a process-wide singleton over a process-wide
	/// <see cref="StaticData.RootPath"/>. Every test restores what it disturbs, as in StaticDataSeamTests.
	/// </remarks>
	public class ZipStaticDataParityTests
	{
		[Test]
		[NotInParallel]
		public async Task ExistenceAndEnumerationMatchTheSameTreeOnDisk()
		{
			using (var fixture = new AssetTreeFixture())
			{
				var disk = fixture.DiskProvider;
				var zip = fixture.ZipProvider;

				foreach (var directory in new[] { "", "Themes", Path.Combine("Themes", "System"), Path.Combine("Themes", "System", "Modern") })
				{
					await Assert.That(zip.DirectoryExists(directory)).IsEqualTo(disk.DirectoryExists(directory))
						.Because("DirectoryExists disagreed for '" + directory + "'");

					await Assert.That(AsPortablePaths(zip.GetFiles(directory))).IsEquivalentTo(AsPortablePaths(disk.GetFiles(directory)))
						.Because("GetFiles disagreed for '" + directory + "'");

					// The disk provider returns full paths and the zip provider archive-relative ones; what
					// has to agree is the set of directories found, which is what callers project out of it
					// with Path.GetFileName.
					await Assert.That(zip.GetDirectories(directory).Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal))
						.IsEquivalentTo(disk.GetDirectories(directory).Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal))
						.Because("GetDirectories disagreed for '" + directory + "'");
				}

				foreach (var file in new[] { AssetTreeFixture.TextAsset, AssetTreeFixture.IconAsset, "Themes/System/Modern/Blue.json" })
				{
					await Assert.That(zip.FileExists(file)).IsEqualTo(disk.FileExists(file))
						.Because("FileExists disagreed for '" + file + "'");
				}

				await Assert.That(zip.FileExists("Themes/System/Nothing.json")).IsFalse();
				await Assert.That(zip.DirectoryExists("Themes/Nothing")).IsFalse();

				// A directory is not a file and vice versa, the same way the filesystem sees it.
				await Assert.That(zip.FileExists("Themes/System")).IsFalse();
				await Assert.That(zip.DirectoryExists(AssetTreeFixture.TextAsset)).IsFalse();
			}
		}

		/// <summary>
		/// Paths handed back by GetFiles/GetDirectories have to survive a round trip back into the provider -
		/// DirectoryTheme feeds GetDirectories output straight into GetFiles, and GetFiles output is handed
		/// to LoadImage and OpenStream everywhere.
		/// </summary>
		[Test]
		[NotInParallel]
		public async Task EnumeratedPathsCanBeHandedStraightBackIn()
		{
			using (var fixture = new AssetTreeFixture())
			{
				var zip = fixture.ZipProvider;

				foreach (var directory in zip.GetDirectories("Themes/System"))
				{
					await Assert.That(zip.DirectoryExists(directory)).IsTrue()
						.Because("'" + directory + "' came out of GetDirectories");

					foreach (var file in zip.GetFiles(directory))
					{
						await Assert.That(zip.FileExists(file)).IsTrue()
							.Because("'" + file + "' came out of GetFiles");
					}
				}
			}
		}

		/// <summary>
		/// The zip's central directory is case-exact and uses '/', but the assets were authored on
		/// case-folding filesystems and callers build paths with Path.Combine (a backslash on Windows).
		/// Both mismatches have to be absorbed here or they detonate as blank icons in the browser.
		/// </summary>
		[Test]
		[NotInParallel]
		public async Task LookupIsCaseInsensitiveAndSeparatorAgnostic()
		{
			using (var fixture = new AssetTreeFixture())
			{
				var zip = fixture.ZipProvider;

				await Assert.That(zip.FileExists("icons/parityicon.svg")).IsTrue();
				await Assert.That(zip.FileExists("ICONS/PARITYICON.SVG")).IsTrue();
				await Assert.That(zip.DirectoryExists("themes/system")).IsTrue();

				await Assert.That(zip.FileExists("Icons\\ParityIcon.svg")).IsTrue();
				await Assert.That(zip.DirectoryExists("Themes\\System")).IsTrue();
				await Assert.That(zip.GetFiles("Themes\\System\\Modern")).IsNotEmpty();

				// A leading "./" and a trailing separator are both shapes Path.Combine and callers produce.
				await Assert.That(zip.DirectoryExists("./Themes/System/")).IsTrue();

				await Assert.That(zip.ReadAllText("text/hello.TXT")).IsEqualTo(AssetTreeFixture.TextAssetContent);
			}
		}

		[Test]
		[NotInParallel]
		public async Task TextIsReadBackByteForByteAndStreamsAreSeekable()
		{
			using (var fixture = new AssetTreeFixture())
			{
				var disk = fixture.DiskProvider;
				var zip = fixture.ZipProvider;

				await Assert.That(zip.ReadAllText(AssetTreeFixture.TextAsset)).IsEqualTo(disk.ReadAllText(AssetTreeFixture.TextAsset));
				await Assert.That(zip.ReadAllLines(AssetTreeFixture.TextAsset)).IsEquivalentTo(disk.ReadAllLines(AssetTreeFixture.TextAsset));

				using (var stream = zip.OpenStream(AssetTreeFixture.TextAsset))
				{
					// The loaders seek - ImageSharp above all - and a zip entry's own stream cannot.
					await Assert.That(stream.CanSeek).IsTrue();

					var first = new byte[4];
					stream.ReadExactly(first);
					stream.Seek(0, SeekOrigin.Begin);

					var all = new byte[stream.Length];
					stream.ReadExactly(all);

					await Assert.That(all).IsEquivalentTo(File.ReadAllBytes(Path.Combine(fixture.RootPath, "Text", "hello.txt")));
					await Assert.That(first).IsEquivalentTo(all.Take(4).ToArray());
				}

				// Missing assets fail the same way either side of the seam.
				await Assert.That(() => zip.OpenStream("Text/nope.txt")).Throws<FileNotFoundException>();
			}
		}

		/// <summary>
		/// The exact walk MatterCAD's AppContext static constructor does at boot: GetDirectories on
		/// Themes/System, GetFiles on each result, .json filter, name off the path. It is the first asset
		/// code the app runs, and a mismatch anywhere in the chain leaves the app with no themes at all.
		/// </summary>
		[Test]
		[NotInParallel]
		public async Task TheAppContextThemeWalkFindsTheSameThemesEitherSideOfTheSeam()
		{
			using (var fixture = new AssetTreeFixture())
			{
				string themesPath = Path.Combine("Themes", "System");

				await Assert.That(fixture.ZipProvider.DirectoryExists(themesPath)).IsTrue();

				await Assert.That(ThemeNames(fixture.ZipProvider, themesPath))
					.IsEquivalentTo(ThemeNames(fixture.DiskProvider, themesPath));

				// "menu" comes along because AppContext's themes dictionary walks every sub-directory -
				// only the ThemeProviders loop below skips Menus.
				await Assert.That(ThemeNames(fixture.ZipProvider, themesPath))
					.IsEquivalentTo(new[] { "Blue", "Red", "menu" });

				// That loop skips the Menus directory by name, so GetFileName has to work on whatever
				// shape of directory path the provider hands back.
				await Assert.That(fixture.ZipProvider.GetDirectories(themesPath).Select(Path.GetFileName))
					.IsEquivalentTo(new[] { "Classic", "Menus", "Modern" });
			}
		}

		/// <summary>
		/// Unlike the filesystem, an archive can hand back a stable order for free. IStaticData promises
		/// none, so this pins what ZipStaticData chose rather than a shared contract.
		/// </summary>
		[Test]
		[NotInParallel]
		public async Task EnumerationIsOrdinalSorted()
		{
			using (var fixture = new AssetTreeFixture())
			{
				var files = fixture.ZipProvider.GetFiles("Themes/System/Modern").ToList();
				await Assert.That(files).IsEquivalentTo(files.OrderBy(f => f, StringComparer.Ordinal).ToList());

				var directories = fixture.ZipProvider.GetDirectories("Themes/System").ToList();
				await Assert.That(directories).IsEquivalentTo(directories.OrderBy(d => d, StringComparer.Ordinal).ToList());
			}
		}

		/// <summary>
		/// MapPath's callers want a real file: one hands it to a mesh loader, the other reads
		/// LastWriteTimeUtc off it to date a cached thumbnail. Both have to keep working with no changes.
		/// </summary>
		[Test]
		[NotInParallel]
		public async Task MapPathExtractsOnDemandStampedWithTheZipEntrysTime()
		{
			using (var fixture = new AssetTreeFixture())
			{
				var zip = fixture.ZipProvider;

				string mapped = zip.MapPath(AssetTreeFixture.TextAsset);

				await Assert.That(File.Exists(mapped)).IsTrue();
				await Assert.That(mapped.StartsWith(zip.StagingPath, StringComparison.Ordinal)).IsTrue();
				await Assert.That(File.ReadAllText(mapped)).IsEqualTo(AssetTreeFixture.TextAssetContent);

				DateTime entryWriteTimeUtc = fixture.EntryLastWriteTimeUtc(AssetTreeFixture.TextAsset);

				// Exactly the ThumbnailsConfig shape: FileInfo over a MapPath result. Left unstamped this
				// would read as "now", and every cached thumbnail would look stale on every run.
				var info = new FileInfo(mapped);
				await Assert.That(info.LastWriteTimeUtc).IsEqualTo(entryWriteTimeUtc);

				// A zip stores MS-DOS local time at 2 second resolution, so the stamp cannot match the
				// source file's exactly - only to within that quantization.
				DateTime sourceWriteTimeUtc = new FileInfo(Path.Combine(fixture.RootPath, "Text", "hello.txt")).LastWriteTimeUtc;
				await Assert.That(Math.Abs((info.LastWriteTimeUtc - sourceWriteTimeUtc).TotalSeconds)).IsLessThanOrEqualTo(2.0);

				// Asking twice hands back the same file, and does not re-extract over a good copy.
				string mappedAgain = zip.MapPath(AssetTreeFixture.TextAsset);
				await Assert.That(mappedAgain).IsEqualTo(mapped);
				await Assert.That(new FileInfo(mappedAgain).LastWriteTimeUtc).IsEqualTo(entryWriteTimeUtc);

				// A mapped path is still a path this provider understands, the way the disk provider's
				// already-rooted MapPath result is.
				await Assert.That(zip.FileExists(mapped)).IsTrue();
				await Assert.That(zip.ReadAllText(mapped)).IsEqualTo(AssetTreeFixture.TextAssetContent);

				// A missing asset still gets a path back, exactly as the disk provider does.
				await Assert.That(File.Exists(zip.MapPath("Text/nope.txt"))).IsFalse();
			}
		}

		[Test]
		[NotInParallel]
		public async Task DisposeRemovesTheStagingDirectory()
		{
			string stagingPath;

			using (var fixture = new AssetTreeFixture())
			{
				stagingPath = fixture.ZipProvider.StagingPath;
				fixture.ZipProvider.MapPath(AssetTreeFixture.TextAsset);

				await Assert.That(Directory.Exists(stagingPath)).IsTrue();
			}

			await Assert.That(Directory.Exists(stagingPath)).IsFalse();
		}

		/// <summary>
		/// The icon pipeline - device scaling, the premultiplied blender, the transparent-pixel clear - lives
		/// in <see cref="StaticDataBase"/> so the two providers cannot drift. This pins that they do not: the
		/// same SVG through both providers must produce identical pixels.
		/// </summary>
		[Test]
		[NotInParallel]
		public async Task TheSharedIconPipelineProducesIdenticalPixelsEitherSideOfTheSeam()
		{
			double savedDeviceScale = GuiWidget.DeviceScale;

			using (var fixture = new AssetTreeFixture())
			{
				try
				{
					GuiWidget.DeviceScale = 2;

					var fromDisk = fixture.DiskProvider.LoadIcon("ParityIcon.svg", 16, 16);
					var fromZip = fixture.ZipProvider.LoadIcon("ParityIcon.svg", 16, 16);

					await Assert.That(fromZip.Width).IsEqualTo(fromDisk.Width);
					await Assert.That(fromZip.Height).IsEqualTo(fromDisk.Height);
					await Assert.That(fromZip.Width).IsEqualTo(32).Because("16 design pixels at a device scale of 2");
					await Assert.That(fromZip.GetBuffer()).IsEquivalentTo(fromDisk.GetBuffer());
				}
				finally
				{
					GuiWidget.DeviceScale = savedDeviceScale;
				}
			}
		}

		private static IEnumerable<string> ThemeNames(IStaticData staticData, string themesPath)
		{
			return staticData.GetDirectories(themesPath)
				.SelectMany(d => staticData.GetFiles(d).Where(p => Path.GetExtension(p) == ".json"))
				.Select(Path.GetFileNameWithoutExtension)
				.OrderBy(n => n, StringComparer.Ordinal)
				.ToList();
		}

		/// <summary>
		/// The two providers disagree about separators by design (the zip always uses '/'), so comparisons
		/// of enumerated paths are made in one shape.
		/// </summary>
		private static List<string> AsPortablePaths(IEnumerable<string> paths)
		{
			return paths.Select(p => p.Replace('\\', '/')).OrderBy(p => p, StringComparer.Ordinal).ToList();
		}

		/// <summary>
		/// A small asset tree on disk plus the same tree zipped, with a provider over each. The zip is built
		/// here rather than checked in: a binary fixture would hide what it contains and rot silently.
		/// </summary>
		private sealed class AssetTreeFixture : IDisposable
		{
			public const string TextAsset = "Text/hello.txt";

			public const string IconAsset = "Icons/ParityIcon.svg";

			public const string TextAssetContent = "Hello, assets!\nSecond line\n";

			private readonly string savedRootPath;

			private readonly byte[] zipBytes;

			public AssetTreeFixture()
			{
				this.RootPath = Path.Combine(Path.GetTempPath(), "ZipStaticDataParity_" + Path.GetRandomFileName());

				WriteFile(Path.Combine(RootPath, "Text", "hello.txt"), TextAssetContent);
				WriteFile(Path.Combine(RootPath, "Icons", "ParityIcon.svg"), SvgSource);
				WriteFile(Path.Combine(RootPath, "Themes", "System", "Modern", "Blue.json"), "{}");
				WriteFile(Path.Combine(RootPath, "Themes", "System", "Modern", "Blue.themeset"), "{}");
				WriteFile(Path.Combine(RootPath, "Themes", "System", "Modern", "notes.md"), "not a theme");
				WriteFile(Path.Combine(RootPath, "Themes", "System", "Classic", "Red.json"), "{}");
				WriteFile(Path.Combine(RootPath, "Themes", "System", "Menus", "menu.json"), "{}");

				string zipPath = Path.Combine(Path.GetTempPath(), "ZipStaticDataParity_" + Path.GetRandomFileName() + ".zip");
				try
				{
					ZipFile.CreateFromDirectory(RootPath, zipPath);
					this.zipBytes = File.ReadAllBytes(zipPath);
				}
				finally
				{
					File.Delete(zipPath);
				}

				this.ZipProvider = new ZipStaticData(zipBytes);

				this.savedRootPath = StaticData.RootPath;
				StaticData.RootPath = RootPath;
				this.DiskProvider = StaticData.Instance;
			}

			public string RootPath { get; }

			public IStaticData DiskProvider { get; }

			public ZipStaticData ZipProvider { get; }

			/// <summary>
			/// Reads the stamp straight out of the archive, so the extraction assertions compare against the
			/// zip's own record rather than against a value the implementation computed.
			/// </summary>
			public DateTime EntryLastWriteTimeUtc(string assetPath)
			{
				using (var archive = new ZipArchive(new MemoryStream(zipBytes), ZipArchiveMode.Read))
				{
					return archive.GetEntry(assetPath).LastWriteTime.UtcDateTime;
				}
			}

			public void Dispose()
			{
				StaticData.RootPath = savedRootPath;

				ZipProvider.Dispose();

				if (Directory.Exists(RootPath))
				{
					Directory.Delete(RootPath, true);
				}
			}

			private static void WriteFile(string path, string content)
			{
				Directory.CreateDirectory(Path.GetDirectoryName(path));
				File.WriteAllText(path, content);
			}

			private const string SvgSource = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 16 16\">"
				+ "<path d=\"M2,2 L14,2 L14,14 L2,14 Z\" fill=\"#3060c0\" />"
				+ "</svg>";
		}
	}
}
