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
	/// The two-archive behaviour <see cref="ZipStaticData.AddArchive(byte[])"/> exists for: a browser host
	/// downloads a small boot archive, brings the UI up on it, and layers the rest of the asset tree in
	/// afterwards. What these tests pin is that the second archive is indistinguishable from having been in
	/// the first all along, and that the window before it lands is loud rather than silent.
	/// </summary>
	public class ZipStaticDataLayeringTests
	{
		[Test]
		public async Task DeferredEntriesAreInvisibleUntilTheArchiveIsLayeredIn()
		{
			using (var assets = new ZipStaticData(LayeringFixture.BootArchive()))
			{
				await Assert.That(assets.FileExists(LayeringFixture.DeferredAsset)).IsFalse();
				await Assert.That(() => assets.OpenStream(LayeringFixture.DeferredAsset)).Throws<FileNotFoundException>();
				await Assert.That(assets.DirectoryExists("Fonts")).IsFalse();

				assets.AddArchive(LayeringFixture.DeferredArchive());

				await Assert.That(assets.FileExists(LayeringFixture.DeferredAsset)).IsTrue();
				await Assert.That(assets.ReadAllText(LayeringFixture.DeferredAsset)).IsEqualTo(LayeringFixture.DeferredContent);
				await Assert.That(assets.DirectoryExists("Fonts")).IsTrue();

				// The boot archive's own assets are untouched by the layering.
				await Assert.That(assets.ReadAllText(LayeringFixture.BootAsset)).IsEqualTo(LayeringFixture.BootContent);
			}
		}

		/// <summary>
		/// Directory enumeration has to merge, not replace: <c>Icons</c> exists in both archives, and a
		/// caller walking it after the layering must see every icon from both.
		/// </summary>
		[Test]
		public async Task EnumerationMergesTheArchivesAndStaysOrdinalSorted()
		{
			using (var assets = new ZipStaticData(LayeringFixture.BootArchive()))
			{
				await Assert.That(assets.GetFiles("Icons").Select(Path.GetFileName))
					.IsEquivalentTo(new[] { "boot.svg" });

				assets.AddArchive(LayeringFixture.DeferredArchive());

				await Assert.That(assets.GetFiles("Icons").Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal))
					.IsEquivalentTo(new[] { "boot.svg", "deferred.svg" });

				var files = assets.GetFiles("Icons").ToList();
				await Assert.That(files).IsEquivalentTo(files.OrderBy(f => f, StringComparer.Ordinal).ToList());

				// The root gains the deferred archive's top-level directory, and keeps the boot one's.
				await Assert.That(assets.GetDirectories(string.Empty).Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal))
					.IsEquivalentTo(new[] { "Fonts", "Icons", "Text" });

				// Paths out of a merged enumeration still round trip back in, from either archive.
				foreach (var file in assets.GetFiles("Icons"))
				{
					await Assert.That(assets.FileExists(file)).IsTrue().Because("'" + file + "' came out of GetFiles");
				}
			}
		}

		/// <summary>
		/// The boot archive is authoritative. An asset that is in both - which the packaging is free to do,
		/// and does for anything it cannot cleanly assign to one side - must read the same before and after
		/// the deferred archive lands, or an asset would change under a running app.
		/// </summary>
		[Test]
		public async Task TheBootArchiveWinsAPathThatIsInBoth()
		{
			using (var assets = new ZipStaticData(LayeringFixture.BootArchive()))
			{
				await Assert.That(assets.ReadAllText(LayeringFixture.SharedAsset)).IsEqualTo(LayeringFixture.BootCopyOfShared);

				assets.AddArchive(LayeringFixture.DeferredArchive());

				await Assert.That(assets.ReadAllText(LayeringFixture.SharedAsset)).IsEqualTo(LayeringFixture.BootCopyOfShared);
			}
		}

		[Test]
		public async Task CaseAndSeparatorFoldingWorksForDeferredEntriesToo()
		{
			using (var assets = new ZipStaticData(LayeringFixture.BootArchive()))
			{
				assets.AddArchive(LayeringFixture.DeferredArchive());

				await Assert.That(assets.FileExists("fonts/DEFERRED.TXT")).IsTrue();
				await Assert.That(assets.FileExists("Fonts\\Deferred.txt")).IsTrue();
				await Assert.That(assets.DirectoryExists("./fonts/")).IsTrue();
				await Assert.That(assets.ReadAllText("FONTS/deferred.txt")).IsEqualTo(LayeringFixture.DeferredContent);
			}
		}

		/// <summary>
		/// MapPath is how a mesh loader and the thumbnail cache reach an asset, so it has to work for a
		/// deferred entry - including on a path that was already asked for, and missed, before the archive
		/// landed.
		/// </summary>
		[Test]
		public async Task MapPathExtractsDeferredEntriesAfterTheyArrive()
		{
			using (var assets = new ZipStaticData(LayeringFixture.BootArchive()))
			{
				string beforeArrival = assets.MapPath(LayeringFixture.DeferredAsset);
				await Assert.That(File.Exists(beforeArrival)).IsFalse();

				assets.AddArchive(LayeringFixture.DeferredArchive());

				string mapped = assets.MapPath(LayeringFixture.DeferredAsset);

				await Assert.That(mapped).IsEqualTo(beforeArrival).Because("the staging path does not move");
				await Assert.That(File.Exists(mapped)).IsTrue();
				await Assert.That(File.ReadAllText(mapped)).IsEqualTo(LayeringFixture.DeferredContent);
			}
		}

		/// <summary>
		/// The safety net under the split: while the host says a deferred archive is outstanding and cannot
		/// say what is in it, every "no" this provider gives is reported, because the synchronous
		/// IStaticData contract has no way to wait for the archive and the caller would otherwise degrade
		/// silently.
		/// </summary>
		[Test]
		public async Task MissesDuringTheDeferredWindowAreReportedOncePerPath()
		{
			var reports = new List<string>();

			using (var assets = new ZipStaticData(LayeringFixture.BootArchive()))
			{
				assets.DeferredAssetMissed = reports.Add;
				assets.ExpectDeferredArchive();

				// Every "no" shape: existence, enumeration, read, and a map of something absent.
				assets.FileExists(LayeringFixture.DeferredAsset);
				assets.FileExists(LayeringFixture.DeferredAsset);
				assets.DirectoryExists("Fonts");
				assets.MapPath("Fonts/other.txt");

				await Assert.That(() => assets.OpenStream("Fonts/third.txt")).Throws<FileNotFoundException>();
				await Assert.That(() => assets.GetFiles("Fonts")).Throws<DirectoryNotFoundException>();

				await Assert.That(reports.Count).IsEqualTo(4)
					.Because("one report per distinct path, however many times it is asked for");
				await Assert.That(reports.Any(r => r.Contains(LayeringFixture.DeferredAsset, StringComparison.OrdinalIgnoreCase))).IsTrue();

				// A hit is not a report, and neither is anything once the archive has landed.
				reports.Clear();
				assets.FileExists(LayeringFixture.BootAsset);
				await Assert.That(reports).IsEmpty();

				assets.AddArchive(LayeringFixture.DeferredArchive());

				await Assert.That(assets.ExpectingDeferredArchive).IsFalse();

				assets.FileExists("Fonts/still-not-here.txt");
				await Assert.That(reports).IsEmpty()
					.Because("after the archive lands a miss is just a miss");
			}
		}

		/// <summary>
		/// Given the deferred archive's contents, only a miss on something actually in it is worth saying -
		/// otherwise the boot sweep this warning exists for drowns in probes for files that were never in
		/// the tree at all (the thumbnail cache asks for one per item, every time).
		/// </summary>
		[Test]
		public async Task KnowingTheDeferredContentsNarrowsTheReportToRealMisses()
		{
			var reports = new List<string>();

			using (var assets = new ZipStaticData(LayeringFixture.BootArchive()))
			{
				assets.DeferredAssetMissed = reports.Add;
				assets.ExpectDeferredArchive(new[] { LayeringFixture.DeferredAsset });

				// Not in the boot archive and not in the deferred one: it simply is not an asset.
				assets.FileExists("Icons/never-existed.png");
				await Assert.That(reports).IsEmpty();

				// In the deferred archive, asked for too early. That is the bug this exists to catch, and
				// the caller's own path case and separators must not hide it.
				assets.FileExists("fonts\\DEFERRED.txt");
				await Assert.That(reports.Count).IsEqualTo(1);
				await Assert.That(reports[0]).Contains("belongs in the boot archive");
			}
		}

		[Test]
		public async Task NothingIsReportedWhenNoDeferredArchiveIsExpected()
		{
			var reports = new List<string>();

			using (var assets = new ZipStaticData(LayeringFixture.BootArchive()))
			{
				assets.DeferredAssetMissed = reports.Add;

				assets.FileExists(LayeringFixture.DeferredAsset);
				assets.MapPath(LayeringFixture.DeferredAsset);

				await Assert.That(reports).IsEmpty();
				await Assert.That(assets.ExpectingDeferredArchive).IsFalse();
			}
		}

		/// <summary>
		/// Two small asset trees zipped in memory: what a boot archive and a deferred archive look like,
		/// including one asset that is deliberately in both.
		/// </summary>
		private static class LayeringFixture
		{
			public const string BootAsset = "Text/boot.txt";

			public const string DeferredAsset = "Fonts/deferred.txt";

			public const string SharedAsset = "Icons/boot.svg";

			public const string BootContent = "the boot archive";

			public const string DeferredContent = "the deferred archive";

			public const string BootCopyOfShared = "boot copy";

			public static byte[] BootArchive()
			{
				return Zip(new Dictionary<string, string>
				{
					[BootAsset] = BootContent,
					[SharedAsset] = BootCopyOfShared,
				});
			}

			public static byte[] DeferredArchive()
			{
				return Zip(new Dictionary<string, string>
				{
					[DeferredAsset] = DeferredContent,
					["Icons/deferred.svg"] = "deferred icon",
					[SharedAsset] = "deferred copy",
				});
			}

			private static byte[] Zip(Dictionary<string, string> entries)
			{
				using (var buffer = new MemoryStream())
				{
					using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
					{
						foreach (var entry in entries)
						{
							using (var writer = new StreamWriter(archive.CreateEntry(entry.Key).Open()))
							{
								writer.Write(entry.Value);
							}
						}
					}

					return buffer.ToArray();
				}
			}
		}
	}
}
