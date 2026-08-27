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
using MatterHackers.Agg.UI;

namespace MatterHackers.Agg.Platform.Browser
{
	/// <summary>
	/// The durable half of the browser's storage: whole files by key, asynchronously.
	/// </summary>
	/// <remarks>
	/// Four operations, deliberately - see <c>storageMirror.js</c>, which implements them over IndexedDB.
	/// The interface exists so the mirror engine above it can be driven by the desktop test suite against a
	/// dictionary, and so the backend can be replaced (OPFS access handles, once workers make them reachable)
	/// without the engine noticing.
	/// </remarks>
	public interface IBrowserStorageBackend
	{
		/// <summary>Every key in the store.</summary>
		Task<string[]> ListKeysAsync();

		/// <summary>The bytes stored under a key, or null if there are none.</summary>
		Task<byte[]> ReadAsync(string key);

		/// <summary>Stores bytes under a key, replacing whatever was there.</summary>
		Task WriteAsync(string key, byte[] bytes);

		/// <summary>Removes a key. Removing one that is not there is not an error.</summary>
		Task DeleteAsync(string key);
	}

	/// <summary>
	/// Keeps a directory and an <see cref="IBrowserStorageBackend"/> in step: restores the directory from the
	/// store at boot, then pushes changes back as they settle.
	/// </summary>
	/// <remarks>
	/// <para><b>Why a mirror at all.</b> A browser tab's filesystem (Emscripten's MEMFS, which is what
	/// <c>System.IO</c> reaches under wasm) is synchronous, fast, and erased on reload. Its storage that
	/// survives - IndexedDB - is asynchronous on the main thread, and the application code above it
	/// (<c>ISQLite</c>, <c>IStaticData</c>, every settings read in a layout pass) is synchronous and cannot
	/// await anything. Neither side can be changed, so the two are run side by side: MEMFS is the working
	/// layer the application sees, and this copies it into the durable one behind its back.</para>
	/// <para><b>What that costs, stated plainly.</b> A change is durable one sweep interval plus one quiet
	/// period after it is written, not immediately - so a tab killed inside that window loses it. The window
	/// is bounded by <see cref="MirrorPolicy.SweepIntervalSeconds"/> and
	/// <see cref="MirrorPolicy.QuietPeriodSeconds"/>, shortened to one sweep for the paths a head marks
	/// immediate, and narrowed further by <see cref="FlushNowAsync"/> when the page is hidden. It cannot be
	/// closed entirely: a page cannot delay its own destruction, and a browser will not commit an IndexedDB
	/// transaction for a renderer it is tearing down - measured, see <see cref="FlushNowAsync"/>.</para>
	/// <para><b>Nothing here is browser-only.</b> The engine is <c>System.IO</c>, a policy object and a
	/// planner, so it runs - and is tested - on the desktop against a fake backend. Only
	/// <see cref="BrowserStorageMirrorInterop"/> underneath it knows what IndexedDB is.</para>
	/// </remarks>
	public sealed class BrowserStorageMirror
	{
		private readonly IBrowserStorageBackend backend;

		private readonly MirrorPolicy policy;

		private readonly MirrorSweepPlanner planner;

		private RunningInterval sweepInterval;

		/// <summary>
		/// How many sweeps are between an await and their continuation.
		/// </summary>
		/// <remarks>
		/// A plain field and not a lock: wasm has one thread, and the only way two sweeps could overlap is the
		/// interval firing while the previous one is waiting on a store transaction. Overlapping them would
		/// plan the same files twice and push each of them twice, so the later one is simply skipped - the
		/// next interval is a second away. A count rather than a flag because a flush is allowed to overlap
		/// (see the sweep), and a flag would let its return declare the sweep it overlapped finished.
		/// </remarks>
		private int sweepsInFlight;

		public BrowserStorageMirror(IBrowserStorageBackend backend, MirrorPolicy policy)
		{
			this.backend = backend ?? throw new ArgumentNullException(nameof(backend));
			this.policy = policy ?? throw new ArgumentNullException(nameof(policy));
			this.planner = new MirrorSweepPlanner(policy);
		}

		/// <summary>What this mirror mirrors. See <see cref="MirrorPolicy"/>.</summary>
		public MirrorPolicy Policy => policy;

		/// <summary>Sweeps run, files pushed and keys removed since boot. For the host's status line.</summary>
		public int SweepCount { get; private set; }

		public int PutCount { get; private set; }

		public int DeleteCount { get; private set; }

		/// <summary>
		/// The last failure the mirror swallowed, or null. See the class remarks on why it swallows them.
		/// </summary>
		public string LastFault { get; private set; }

		/// <summary>One line of what the mirror has done, for a host that narrates its boot.</summary>
		public string Summary
		{
			get
			{
				string fault = this.LastFault == null ? string.Empty : $", last fault: {this.LastFault}";

				return $"{this.SweepCount} sweeps, {this.PutCount} puts, {this.DeleteCount} deletes{fault}";
			}
		}

		/// <summary>
		/// Writes everything in the store back into the mirrored directory, and tells the planner that is what
		/// the store holds. Await this before anything reads the directory.
		/// </summary>
		/// <remarks>
		/// <para>Key by key rather than in one bulk read: the store holds a settings table and a handful of
		/// small documents, the four-operation backend is what keeps it swappable, and a bulk read would have
		/// to hold every mirrored byte in the JS heap and the wasm heap at once.</para>
		/// <para>Failures are reported through <see cref="LastFault"/> rather than thrown. A browser can
		/// refuse IndexedDB outright (private browsing modes have, storage can be disabled by policy), and the
		/// right answer to that is an application that starts and forgets things, not one that will not
		/// start.</para>
		/// </remarks>
		/// <returns>How many files were restored.</returns>
		public async Task<int> RestoreAsync()
		{
			var restored = new List<MirrorFileState>();

			try
			{
				Directory.CreateDirectory(policy.RootPath);

				foreach (string key in await backend.ListKeysAsync())
				{
					// A key the current policy excludes was written by a different one. Not restored and not
					// deleted either - see MirrorPolicy.ExcludedPaths.
					if (policy.IsExcluded(key))
					{
						continue;
					}

					byte[] bytes = await backend.ReadAsync(key);
					if (bytes == null)
					{
						continue;
					}

					string path = policy.PathForKey(key);

					Directory.CreateDirectory(Path.GetDirectoryName(path));
					File.WriteAllBytes(path, bytes);

					// Read back rather than assumed: the write stamped the file with the time it was written,
					// and it is that stamp the next sweep will compare against.
					restored.Add(StateOf(new FileInfo(path)));
				}
			}
			catch (Exception restoreException)
			{
				Fault("restore", restoreException);
			}

			planner.Seed(restored);

			return restored.Count;
		}

		/// <summary>
		/// Starts sweeping the mirrored directory on the UI thread's interval, pushing what has settled.
		/// </summary>
		/// <remarks>
		/// On <see cref="UiThread"/> because that is the browser host's only clock that is also the thread
		/// every write it is looking for was made on: the sweep walks a filesystem the application is writing
		/// to, and in the one-threaded browser being on the same thread is what makes the walk consistent.
		/// (It also means a hidden tab, whose animation frames stop, stops sweeping - which is what the unload
		/// flush is for.)
		/// </remarks>
		public void StartWriteBehind()
		{
			Stop();

			sweepInterval = UiThread.SetInterval(
				() =>
				{
					// Fire and forget, deliberately: the interval callback is synchronous and the sweep is a
					// chain of store transactions. Nothing in the sweep can throw out of it - see SweepCore -
					// so there is no faulted task to observe.
					_ = SweepAsync();
				},
				policy.SweepIntervalSeconds);
		}

		/// <summary>Stops sweeping. A sweep already in flight finishes.</summary>
		public void Stop()
		{
			if (sweepInterval != null)
			{
				UiThread.ClearInterval(sweepInterval);
				sweepInterval = null;
			}
		}

		/// <summary>
		/// Runs one sweep: pushes what has settled, removes what is gone.
		/// </summary>
		public Task SweepAsync()
		{
			return SweepCoreAsync(pushEverythingNow: false);
		}

		/// <summary>
		/// Pushes everything dirty right now, quiet period ignored. The page-is-hidden-or-going-away path.
		/// </summary>
		/// <remarks>
		/// <para>The caller usually cannot await it - an unload handler has nothing to await with - so this
		/// starts every write before it waits for any of them (see the sweep). What that is worth depends on
		/// which event brought it here, and <c>storageMirror.js</c> has the measurements: a hidden page is
		/// still alive and its writes commit; a page being torn down issues them and they do not.</para>
		/// <para>The hidden case is the one to care about, and not only because it works: a hidden page gets
		/// no animation frames, so the sweep - which runs on the frame-driven UI queue - has stopped. Between
		/// going hidden and coming back, this flush is the only thing persisting anything.</para>
		/// </remarks>
		public Task FlushNowAsync()
		{
			return SweepCoreAsync(pushEverythingNow: true);
		}

		/// <summary>
		/// Every file under the mirror root as the mirror sees it, excluded folders skipped.
		/// </summary>
		/// <remarks>
		/// Its own recursion rather than <c>EnumerateFiles(AllDirectories)</c> so that an excluded folder is
		/// never descended into at all. That is the difference between skipping the cache and statting every
		/// file in it once a second.
		/// </remarks>
		public IEnumerable<MirrorFileState> Walk()
		{
			var directories = new Stack<string>();

			if (!Directory.Exists(policy.RootPath))
			{
				yield break;
			}

			directories.Push(policy.RootPath);

			while (directories.Count > 0)
			{
				string directory = directories.Pop();

				foreach (string child in Directory.EnumerateDirectories(directory))
				{
					if (!policy.IsExcluded(policy.KeyForPath(child)))
					{
						directories.Push(child);
					}
				}

				foreach (string file in Directory.EnumerateFiles(directory))
				{
					yield return StateOf(new FileInfo(file));
				}
			}
		}

		private MirrorFileState StateOf(FileInfo file)
		{
			return new MirrorFileState(policy.KeyForPath(file.FullName), file.LastWriteTimeUtc.Ticks, file.Length);
		}

		private async Task SweepCoreAsync(bool pushEverythingNow)
		{
			// A flush is never skipped: it is called because the page is hidden or going away, so "the next
			// sweep will catch it" is false - a hidden page has no animation frames to sweep in - and the
			// worst a flush overlapping a sweep can do is write some file's bytes twice.
			if (sweepsInFlight > 0
				&& !pushEverythingNow)
			{
				return;
			}

			sweepsInFlight++;

			try
			{
				this.SweepCount++;

				MirrorSweepPlan plan = planner.Plan(Walk(), UiThread.CurrentTimerMs, pushEverythingNow);
				List<Task> started = pushEverythingNow ? new List<Task>() : null;

				foreach (MirrorFileState file in plan.Puts)
				{
					byte[] bytes;

					try
					{
						bytes = File.ReadAllBytes(policy.PathForKey(file.Key));
					}
					catch (IOException)
					{
						// Gone or unreadable between the walk and now. Nothing to push, and the next sweep will
						// see it as a deletion if that is what it was.
						continue;
					}

					// A flush starts every write before awaiting any of them, and an ordinary sweep does them
					// one at a time. The difference is what an unload handler gets: one turn of the event loop,
					// in which every transaction it means to open has to be opened. Awaiting each write in turn
					// there would queue the first and lose the rest. An ordinary sweep has all the time it
					// needs and is gentler on the store one at a time.
					if (started != null)
					{
						started.Add(PushAsync(file, bytes));
					}
					else
					{
						await PushAsync(file, bytes);
					}
				}

				foreach (string key in plan.Deletes)
				{
					if (started != null)
					{
						started.Add(RemoveAsync(key));
					}
					else
					{
						await RemoveAsync(key);
					}
				}

				if (started != null)
				{
					await Task.WhenAll(started);
				}
			}
			catch (Exception sweepException)
			{
				// Swallowed for the reason RestoreAsync's are: storage that has stopped working is a reason to
				// stop persisting, not a reason to take the application down. Whatever was not confirmed is
				// planned again by the next sweep.
				Fault("sweep", sweepException);
			}
			finally
			{
				sweepsInFlight--;
			}
		}

		private async Task PushAsync(MirrorFileState file, byte[] bytes)
		{
			await backend.WriteAsync(file.Key, bytes);

			// Only after the write landed, and per file rather than per plan: a store that refuses one key
			// must not make the mirror forget it pushed the others.
			planner.MarkMirrored(file);
			this.PutCount++;
		}

		private async Task RemoveAsync(string key)
		{
			await backend.DeleteAsync(key);

			planner.MarkDeleted(key);
			this.DeleteCount++;
		}

		private void Fault(string what, Exception exception)
		{
			this.LastFault = $"{what}: {exception.Message}";

			Console.Error.WriteLine($"BrowserStorageMirror {what} failed: {exception}");
		}
	}
}
