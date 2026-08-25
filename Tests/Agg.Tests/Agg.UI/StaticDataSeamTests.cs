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
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// Covers the <see cref="IStaticData"/> seam: a host may substitute its own asset provider (a browser
	/// host serves assets over HTTP rather than from disk), and the disk implementation's path handling.
	/// </summary>
	/// <remarks>
	/// Keyless <c>[NotInParallel]</c>: <see cref="StaticData.Instance"/> and <see cref="StaticData.RootPath"/>
	/// are process wide. Every test here restores what it disturbs.
	/// </remarks>
	public class StaticDataSeamTests
	{
		[Test]
		[NotInParallel]
		public async Task AHostCanSubstituteItsOwnProvider()
		{
			var savedInstance = StaticData.Instance;

			try
			{
				var substitute = new RecordingStaticData();
				StaticData.Instance = substitute;

				await Assert.That(StaticData.Instance).IsSameReferenceAs(substitute);

				// Calls through the singleton have to land on the substitute, not on disk.
				await Assert.That(StaticData.Instance.ReadAllText("Anything.txt")).IsEqualTo("from the substitute");
				await Assert.That(substitute.RequestedPaths).IsEquivalentTo(new[] { "Anything.txt" });
			}
			finally
			{
				StaticData.Instance = savedInstance;
			}

			await Assert.That(StaticData.Instance).IsSameReferenceAs(savedInstance);
		}

		[Test]
		[NotInParallel]
		public async Task MapPathResolvesRelativePathsAgainstRootPath()
		{
			string savedRootPath = StaticData.RootPath;
			string tempRoot = Path.Combine(Path.GetTempPath(), "AggStaticDataSeam_" + Path.GetRandomFileName());

			try
			{
				Directory.CreateDirectory(tempRoot);
				StaticData.RootPath = tempRoot;

				await Assert.That(StaticData.Instance.MapPath(Path.Combine("Icons", "thing.png")))
					.IsEqualTo(Path.Combine(tempRoot, "Icons", "thing.png"));

				// An already rooted path is left alone - GetFiles hands its callers paths that get
				// mapped a second time, and DirectoryTheme passes GetDirectories output straight back in.
				string rooted = Path.Combine(tempRoot, "Themes");
				await Assert.That(StaticData.Instance.MapPath(rooted)).IsEqualTo(rooted);
			}
			finally
			{
				StaticData.RootPath = savedRootPath;

				// Guarded: if setup threw before the directory existed, deleting must not replace the
				// real failure with a DirectoryNotFoundException.
				if (Directory.Exists(tempRoot))
				{
					Directory.Delete(tempRoot, true);
				}
			}
		}

		/// <summary>
		/// GetFiles used to cut each full path at the first literal "StaticData" it contained, so a root
		/// whose parent folders mentioned StaticData produced paths that mapped nowhere.
		/// </summary>
		[Test]
		[NotInParallel]
		public async Task GetFilesReturnsPathsRelativeToRootEvenWhenTheRootMentionsStaticData()
		{
			string savedRootPath = StaticData.RootPath;

			// "StaticData_Host" appears before the real root, exactly the shape the old substring broke on.
			string tempRoot = Path.Combine(Path.GetTempPath(), "StaticData_Host_" + Path.GetRandomFileName(), "Assets");
			string nested = Path.Combine(tempRoot, "Animations", "spinner");

			try
			{
				Directory.CreateDirectory(nested);
				File.WriteAllText(Path.Combine(nested, "frame.png"), "not really a png");
				StaticData.RootPath = tempRoot;

				var files = StaticData.Instance.GetFiles(Path.Combine("Animations", "spinner")).ToList();

				await Assert.That(files).IsEquivalentTo(new[] { Path.Combine("Animations", "spinner", "frame.png") });

				// The whole point of the relative path is that it can be handed straight back in.
				await Assert.That(StaticData.Instance.FileExists(files[0])).IsTrue();
			}
			finally
			{
				StaticData.RootPath = savedRootPath;

				string hostFolder = Directory.GetParent(tempRoot).FullName;
				if (Directory.Exists(hostFolder))
				{
					Directory.Delete(hostFolder, true);
				}
			}
		}

		/// <summary>
		/// DirectoryTheme feeds GetDirectories output - which is absolute - straight into GetFiles, so
		/// GetFiles has to accept an absolute directory and still hand back root-relative paths.
		/// </summary>
		[Test]
		[NotInParallel]
		public async Task GetFilesAcceptsAnAbsoluteDirectoryAndStillReturnsRootRelativePaths()
		{
			string savedRootPath = StaticData.RootPath;
			string tempRoot = Path.Combine(Path.GetTempPath(), "AggStaticDataSeam_" + Path.GetRandomFileName());
			string themes = Path.Combine(tempRoot, "Themes", "Modern");

			try
			{
				Directory.CreateDirectory(themes);
				File.WriteAllText(Path.Combine(themes, "Blue.json"), "{}");
				StaticData.RootPath = tempRoot;

				// Exactly what DirectoryTheme does: absolute paths out of GetDirectories, back into GetFiles.
				string absoluteDirectory = StaticData.Instance.GetDirectories("Themes").Single();
				await Assert.That(Path.IsPathRooted(absoluteDirectory)).IsTrue();

				var files = StaticData.Instance.GetFiles(absoluteDirectory).ToList();

				await Assert.That(files).IsEquivalentTo(new[] { Path.Combine("Themes", "Modern", "Blue.json") });

				// Root-relative means it maps back to the same file.
				await Assert.That(StaticData.Instance.FileExists(files[0])).IsTrue();
			}
			finally
			{
				StaticData.RootPath = savedRootPath;

				if (Directory.Exists(tempRoot))
				{
					Directory.Delete(tempRoot, true);
				}
			}
		}

		/// <summary>
		/// The kind of provider a non-filesystem host installs: everything is served from memory.
		/// </summary>
		private class RecordingStaticData : IStaticData
		{
			public List<string> RequestedPaths { get; } = new List<string>();

			public string ReadAllText(string path)
			{
				RequestedPaths.Add(path);
				return "from the substitute";
			}

			public void PurgeCache()
			{
			}

			public bool DirectoryExists(string path) => false;

			public bool FileExists(string path) => false;

			public IEnumerable<string> GetDirectories(string path) => Array.Empty<string>();

			public IEnumerable<string> GetFiles(string path) => Array.Empty<string>();

			public ImageBuffer LoadIcon(string path) => null;

			public ImageBuffer LoadIcon(string path, int width, int height, bool invertImage = false, Func<ImageBuffer, (ImageBuffer processed, string key)> processSource = null) => null;

			public ImageBuffer LoadImage(string path) => null;

			public void LoadImageData(Stream imageStream, ImageBuffer destImage)
			{
			}

			public void LoadImageSequenceData(Stream stream, ImageSequence sequence)
			{
			}

			public ImageSequence LoadSequence(string path) => null;

			public Stream OpenStream(string path) => Stream.Null;

			public string[] ReadAllLines(string path) => Array.Empty<string>();

			public string MapPath(string path) => path;
		}
	}
}
