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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MatterHackers.Agg.Platform.Browser;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// The mirror engine end to end: a real directory on this machine's disk standing in for the browser's
	/// MEMFS, and a dictionary standing in for IndexedDB.
	/// </summary>
	/// <remarks>
	/// The substitution is honest in the way that matters. What the engine does is <c>System.IO</c> against a
	/// tree and four asynchronous operations against a key-value store, and neither half cares which
	/// implementation it got - which is why <see cref="IBrowserStorageBackend"/> exists as an interface at
	/// all. What these cannot cover is the marshalling across the JS boundary and IndexedDB's own behaviour;
	/// that is <see cref="BrowserStorageMirrorInterop"/>'s six one-line methods and the live browser run.
	/// </remarks>
	public class BrowserStorageMirrorTests
	{
		[Test]
		public async Task RestoreWritesTheStoreBackIntoTheTree()
		{
			using (var root = new TemporaryRoot())
			{
				var backend = new FakeStorageBackend();

				backend.Entries["db/UserSetting.json"] = Bytes("[{\"Name\":\"ActiveThemeName\"}]");
				backend.Entries["Library/Plating/design.mcx"] = Bytes("{}");

				int restored = await new BrowserStorageMirror(backend, root.Policy()).RestoreAsync();

				await Assert.That(restored).IsEqualTo(2);
				await Assert.That(File.ReadAllText(root.Path("db/UserSetting.json"))).IsEqualTo("[{\"Name\":\"ActiveThemeName\"}]");

				// Nested directories the store only implied through its key separators.
				await Assert.That(File.Exists(root.Path("Library/Plating/design.mcx"))).IsTrue();
			}
		}

		[Test]
		public async Task TheFirstSweepAfterARestorePushesNothing()
		{
			using (var root = new TemporaryRoot())
			{
				var backend = new FakeStorageBackend();

				backend.Entries["db/UserSetting.json"] = Bytes("[]");

				var mirror = new BrowserStorageMirror(backend, root.Policy());

				await mirror.RestoreAsync();

				backend.Writes.Clear();

				await mirror.SweepAsync();

				await Assert.That(backend.Writes).IsEmpty();
				await Assert.That(mirror.PutCount).IsEqualTo(0);
			}
		}

		[Test]
		public async Task ANewFileReachesTheStore()
		{
			using (var root = new TemporaryRoot())
			{
				var backend = new FakeStorageBackend();
				var mirror = new BrowserStorageMirror(backend, root.Policy());

				await mirror.RestoreAsync();

				root.Write("db/UserSetting.json", "[{\"Name\":\"SoftwareLicenseAccepted\",\"Value\":\"true\"}]");

				await mirror.SweepAsync();

				await Assert.That(backend.Entries.Keys).IsEquivalentTo(new[] { "db/UserSetting.json" });
				await Assert.That(Text(backend.Entries["db/UserSetting.json"])).Contains("SoftwareLicenseAccepted");
				await Assert.That(mirror.PutCount).IsEqualTo(1);
			}
		}

		[Test]
		public async Task ADeletedFileLeavesTheStore()
		{
			using (var root = new TemporaryRoot())
			{
				var backend = new FakeStorageBackend();

				backend.Entries["Library/design.mcx"] = Bytes("{}");

				var mirror = new BrowserStorageMirror(backend, root.Policy());

				await mirror.RestoreAsync();

				File.Delete(root.Path("Library/design.mcx"));

				await mirror.SweepAsync();

				await Assert.That(backend.Entries).IsEmpty();
				await Assert.That(mirror.DeleteCount).IsEqualTo(1);
			}
		}

		[Test]
		public async Task ExcludedFilesAreNeitherWalkedNorPushed()
		{
			using (var root = new TemporaryRoot())
			{
				var backend = new FakeStorageBackend();
				var mirror = new BrowserStorageMirror(backend, root.Policy());

				await mirror.RestoreAsync();

				root.Write("data/temp/cache/thumbnail.png", "not really a png");
				root.Write("db/UserSetting.json", "[]");

				await mirror.SweepAsync();

				await Assert.That(backend.Entries.Keys).IsEquivalentTo(new[] { "db/UserSetting.json" });

				// And the walk never even looked: an excluded folder is not descended into, which is what
				// keeps a gcode cache from being statted once a second.
				await Assert.That(mirror.Walk().Select(file => file.Key)).IsEquivalentTo(new[] { "db/UserSetting.json" });
			}
		}

		[Test]
		public async Task AFlushPushesWhatTheQuietPeriodIsStillHolding()
		{
			using (var root = new TemporaryRoot())
			{
				var backend = new FakeStorageBackend();
				var policy = root.Policy();

				// Long enough that nothing settles during this test, so what is measured is the flush and not
				// a stopwatch.
				policy.QuietPeriodSeconds = 600;

				var mirror = new BrowserStorageMirror(backend, policy);

				await mirror.RestoreAsync();

				root.Write("Library/design.mcx", "{}");

				await mirror.SweepAsync();

				await Assert.That(backend.Entries).IsEmpty();

				await mirror.FlushNowAsync();

				await Assert.That(backend.Entries.Keys).IsEquivalentTo(new[] { "Library/design.mcx" });
			}
		}

		[Test]
		public async Task AFlushIsNotSkippedByASweepThatIsStillWaitingOnTheStore()
		{
			// Found live, in a browser: the page was navigated away from while an ordinary sweep sat between
			// an await and its continuation, the flush saw the in-flight sweep and returned without doing
			// anything, and the setting the user had just changed was lost. A flush has no next sweep to
			// defer to, so it never defers.
			using (var root = new TemporaryRoot())
			{
				var backend = new FakeStorageBackend();
				var mirror = new BrowserStorageMirror(backend, root.Policy());

				await mirror.RestoreAsync();

				root.Write("db/UserSetting.json", "[]");
				root.Write("db/SystemSetting.json", "[]");

				// The store answers this one write only when the test says so - which is the browser's
				// "a transaction is in flight" without a browser and without a delay.
				var held = new TaskCompletionSource<bool>();

				backend.HoldNextWrite = held;

				Task sweep = mirror.SweepAsync();

				await Assert.That(sweep.IsCompleted).IsFalse();

				await mirror.FlushNowAsync();

				await Assert.That(backend.Entries.Keys)
					.IsEquivalentTo(new[] { "db/UserSetting.json", "db/SystemSetting.json" });

				held.SetResult(true);

				await sweep;
			}
		}

		[Test]
		public async Task AStoreThatRefusesAWriteIsTriedAgainAndDoesNotThrow()
		{
			using (var root = new TemporaryRoot())
			{
				var backend = new FakeStorageBackend { FailNextWrite = true };
				var mirror = new BrowserStorageMirror(backend, root.Policy());

				await mirror.RestoreAsync();

				root.Write("db/UserSetting.json", "[]");

				// Does not throw: a browser can refuse storage outright, and an application that will not run
				// because it cannot remember things is worse than one that runs and forgets.
				await mirror.SweepAsync();

				await Assert.That(backend.Entries).IsEmpty();
				await Assert.That(mirror.LastFault).IsNotNull();

				await mirror.SweepAsync();

				await Assert.That(backend.Entries.Keys).IsEquivalentTo(new[] { "db/UserSetting.json" });
			}
		}

		[Test]
		public async Task AStoreWrittenByOneRunIsReadBackByTheNext()
		{
			// The whole feature in one test: a store, a page that ran and wrote a setting, and a second page
			// that finds it there. The live browser run is this with a real reload in the middle.
			using (var first = new TemporaryRoot())
			using (var second = new TemporaryRoot())
			{
				var backend = new FakeStorageBackend();
				var writer = new BrowserStorageMirror(backend, first.Policy());

				await writer.RestoreAsync();

				first.Write("db/UserSetting.json", "[{\"Name\":\"SoftwareLicenseAccepted\"}]");

				await writer.FlushNowAsync();

				var reader = new BrowserStorageMirror(backend, second.Policy());

				await Assert.That(await reader.RestoreAsync()).IsEqualTo(1);
				await Assert.That(File.ReadAllText(second.Path("db/UserSetting.json"))).Contains("SoftwareLicenseAccepted");
			}
		}

		private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);

		private static string Text(byte[] bytes) => Encoding.UTF8.GetString(bytes);

		/// <summary>
		/// A directory that stands in for the browser's MEMFS for the length of one test, and takes itself
		/// away afterwards.
		/// </summary>
		private sealed class TemporaryRoot : IDisposable
		{
			private readonly string root = System.IO.Path.Combine(
				System.IO.Path.GetTempPath(),
				"agg-storage-mirror-" + Guid.NewGuid().ToString("N"));

			public TemporaryRoot()
			{
				Directory.CreateDirectory(root);
			}

			/// <summary>
			/// MatterCAD's own policy shape: the temp tree excluded, and the datastore's tables pushed on the
			/// first sweep that sees them. Quiet period zero so a test measures a decision and not a delay.
			/// </summary>
			public MirrorPolicy Policy()
			{
				return new MirrorPolicy(root)
				{
					ExcludedPaths = new[] { "data/temp" },
					QuietPeriodSeconds = 0,
					PushImmediately = key => key.StartsWith("db/", StringComparison.Ordinal),
				};
			}

			public string Path(string relative)
			{
				return System.IO.Path.Combine(root, relative.Replace('/', System.IO.Path.DirectorySeparatorChar));
			}

			public void Write(string relative, string contents)
			{
				string path = Path(relative);

				Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
				File.WriteAllText(path, contents);
			}

			public void Dispose()
			{
				try
				{
					Directory.Delete(root, recursive: true);
				}
				catch (IOException)
				{
					// A leftover temp directory is not a test failure.
				}
			}
		}

		/// <summary>
		/// IndexedDB's four operations over a dictionary, with a record of what was asked of it.
		/// </summary>
		private sealed class FakeStorageBackend : IBrowserStorageBackend
		{
			public Dictionary<string, byte[]> Entries { get; } = new Dictionary<string, byte[]>(StringComparer.Ordinal);

			public List<string> Writes { get; } = new List<string>();

			/// <summary>Makes the next write fail, the way a store out of quota does.</summary>
			public bool FailNextWrite { get; set; }

			/// <summary>Holds the next write open until this is completed - a transaction in flight.</summary>
			public TaskCompletionSource<bool> HoldNextWrite { get; set; }

			public Task<string[]> ListKeysAsync()
			{
				return Task.FromResult(Entries.Keys.ToArray());
			}

			public Task<byte[]> ReadAsync(string key)
			{
				return Task.FromResult(Entries.TryGetValue(key, out byte[] bytes) ? bytes : null);
			}

			public async Task WriteAsync(string key, byte[] bytes)
			{
				if (this.FailNextWrite)
				{
					this.FailNextWrite = false;

					throw new InvalidOperationException("the store refused the write");
				}

				if (this.HoldNextWrite != null)
				{
					var held = this.HoldNextWrite;

					this.HoldNextWrite = null;

					await held.Task;
				}

				Writes.Add(key);
				Entries[key] = bytes;
			}

			public Task DeleteAsync(string key)
			{
				Entries.Remove(key);

				return Task.CompletedTask;
			}
		}
	}
}
