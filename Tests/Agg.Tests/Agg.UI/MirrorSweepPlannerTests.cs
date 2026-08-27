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
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg.Platform.Browser;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// What the browser's write-behind mirror decides to push, when, and what it leaves alone.
	/// </summary>
	/// <remarks>
	/// Every one of these questions is answered in a browser tab, against a filesystem that only exists in a
	/// wasm heap, on a clock nobody can wind forward - which is exactly why
	/// <see cref="MirrorSweepPlanner"/> answers them as a function of (what was mirrored, what is there now,
	/// what time it is) with no filesystem and no store attached. The time is a parameter, so a two second
	/// debounce is tested in microseconds and nothing here waits on a duration.
	/// </remarks>
	public class MirrorSweepPlannerTests
	{
		[Test]
		public async Task TheFirstSweepAfterARestoreDecidesNothing()
		{
			// The one that matters most: a restore has just written the whole store back into the filesystem,
			// so every file in the tree looks brand new. Without the seed, boot would push the user's entire
			// library straight back into the store it came from.
			var restored = new[]
			{
				State("db/UserSetting.json", modifiedTicks: 500, length: 120),
				State("Library/design.mcx", modifiedTicks: 700, length: 4096),
			};

			var planner = PlannerFor(new MirrorPolicy("/root") { QuietPeriodSeconds = 0 });

			planner.Seed(restored);

			var plan = planner.Plan(restored, nowMilliseconds: 0);

			await Assert.That(plan.IsEmpty).IsTrue();
			await Assert.That(planner.MirroredCount).IsEqualTo(2);
		}

		[Test]
		public async Task ANewFileIsPushedOnceItHasSettled()
		{
			var planner = PlannerFor(new MirrorPolicy("/root") { QuietPeriodSeconds = 2 });
			var created = new[] { State("Library/design.mcx", modifiedTicks: 900, length: 32) };

			// Seen, and waited on: a file that has just appeared may still be being written.
			await Assert.That(planner.Plan(created, nowMilliseconds: 1000).IsEmpty).IsTrue();
			await Assert.That(planner.Plan(created, nowMilliseconds: 2999).IsEmpty).IsTrue();

			var plan = planner.Plan(created, nowMilliseconds: 3000);

			await Assert.That(plan.Puts.Select(put => put.Key)).IsEquivalentTo(new[] { "Library/design.mcx" });
		}

		[Test]
		public async Task AFileWhoseTimestampMovedIsPushed()
		{
			// Same size, different mtime - the shape of a settings row edited in place, and the case a
			// size-only comparison would miss entirely.
			var planner = SeededWith(State("db/UserSetting.json", modifiedTicks: 100, length: 64));

			var plan = planner.Plan(
				new[] { State("db/UserSetting.json", modifiedTicks: 200, length: 64) },
				nowMilliseconds: 0);

			await Assert.That(plan.Puts.Single().ModifiedTicks).IsEqualTo(200);
		}

		[Test]
		public async Task AFileWhoseSizeChangedIsPushed()
		{
			var planner = SeededWith(State("db/UserSetting.json", modifiedTicks: 100, length: 64));

			var plan = planner.Plan(
				new[] { State("db/UserSetting.json", modifiedTicks: 100, length: 65) },
				nowMilliseconds: 0);

			await Assert.That(plan.Puts.Single().Length).IsEqualTo(65);
		}

		[Test]
		public async Task AFileEditedBackToTheMirroredStateIsNotPushed()
		{
			var mirrored = State("db/UserSetting.json", modifiedTicks: 100, length: 64);
			var planner = SeededWith(mirrored);

			// Seen changed once...
			planner.Plan(new[] { State("db/UserSetting.json", modifiedTicks: 150, length: 64) }, nowMilliseconds: 0);

			// ...and then found identical to what the store holds. Nothing to say.
			await Assert.That(planner.Plan(new[] { mirrored }, nowMilliseconds: 5000).IsEmpty).IsTrue();
		}

		[Test]
		public async Task ADeletedFileIsRemovedWithoutWaiting()
		{
			// No debounce on a delete, even with a long quiet period: a file that is gone cannot be mid-write,
			// and a key that outlives its file comes back as a resurrected file on the next reload.
			var planner = SeededWith(
				new MirrorPolicy("/root") { QuietPeriodSeconds = 60 },
				State("Library/design.mcx", modifiedTicks: 100, length: 32));

			var plan = planner.Plan(Array.Empty<MirrorFileState>(), nowMilliseconds: 0);

			await Assert.That(plan.Deletes).IsEquivalentTo(new[] { "Library/design.mcx" });
		}

		[Test]
		public async Task ExcludedPathsAreNeverPlanned()
		{
			var policy = new MirrorPolicy("/root")
			{
				ExcludedPaths = new[] { "data/temp" },
				QuietPeriodSeconds = 0,
			};

			var plan = PlannerFor(policy).Plan(
				new[]
				{
					State("data/temp/cache/thumbnail.png", modifiedTicks: 1, length: 1),
					State("data/temp/gcode/print.gcode", modifiedTicks: 1, length: 1),

					// The boundary: exclusion is on '/' boundaries, so a sibling whose name merely starts the
					// same way is still mirrored.
					State("data/temporary-notes.json", modifiedTicks: 1, length: 1),
				},
				nowMilliseconds: 0);

			await Assert.That(plan.Puts.Select(put => put.Key)).IsEquivalentTo(new[] { "data/temporary-notes.json" });
			await Assert.That(plan.Deletes.Count).IsEqualTo(0);
		}

		[Test]
		public async Task ImmediatePathsSkipTheQuietPeriod()
		{
			// MatterCAD's policy: the datastore's table files are kilobytes, are rewritten exactly once per
			// committed row, and hold every setting the user has ever changed. Everything else can wait.
			var policy = new MirrorPolicy("/root")
			{
				QuietPeriodSeconds = 60,
				PushImmediately = key => key.StartsWith("db/", StringComparison.Ordinal) && key.EndsWith(".json", StringComparison.Ordinal),
			};

			var plan = PlannerFor(policy).Plan(
				new[]
				{
					State("db/UserSetting.json", modifiedTicks: 1, length: 1),
					State("Library/design.mcx", modifiedTicks: 1, length: 1),
				},
				nowMilliseconds: 0);

			await Assert.That(plan.Puts.Select(put => put.Key)).IsEquivalentTo(new[] { "db/UserSetting.json" });
		}

		[Test]
		public async Task AFileThatKeepsChangingKeepsWaiting()
		{
			var planner = PlannerFor(new MirrorPolicy("/root") { QuietPeriodSeconds = 2 });

			// Rewritten on every sweep - a design being edited. Each change restarts the wait, which is the
			// whole point: pushing every intermediate state of a large file would keep the store behind
			// forever.
			await Assert.That(planner.Plan(new[] { State("Library/design.mcx", 1, 10) }, 1000).IsEmpty).IsTrue();
			await Assert.That(planner.Plan(new[] { State("Library/design.mcx", 2, 20) }, 2000).IsEmpty).IsTrue();
			await Assert.That(planner.Plan(new[] { State("Library/design.mcx", 3, 30) }, 3000).IsEmpty).IsTrue();

			// Settled, and now it is worth writing down.
			var settled = new[] { State("Library/design.mcx", 3, 30) };

			await Assert.That(planner.Plan(settled, 4999).IsEmpty).IsTrue();
			await Assert.That(planner.Plan(settled, 5000).Puts.Count).IsEqualTo(1);
		}

		[Test]
		public async Task AFlushPushesWhatTheQuietPeriodWouldHaveHeld()
		{
			// The page is going away: there is no next sweep to catch what is still settling.
			var planner = PlannerFor(new MirrorPolicy("/root") { QuietPeriodSeconds = 60 });
			var current = new[] { State("Library/design.mcx", 1, 10) };

			await Assert.That(planner.Plan(current, 0).IsEmpty).IsTrue();
			await Assert.That(planner.Plan(current, 0, pushEverythingNow: true).Puts.Count).IsEqualTo(1);
		}

		[Test]
		public async Task AConfirmedPushIsNotPlannedAgain()
		{
			var planner = PlannerFor(new MirrorPolicy("/root") { QuietPeriodSeconds = 0 });
			var current = new[] { State("db/UserSetting.json", 1, 10) };

			var plan = planner.Plan(current, 0);

			planner.MarkMirrored(plan.Puts.Single());

			await Assert.That(planner.Plan(current, 0).IsEmpty).IsTrue();
			await Assert.That(planner.MirroredCount).IsEqualTo(1);
		}

		[Test]
		public async Task AnUnconfirmedPushIsPlannedAgain()
		{
			// The retry policy, in full: belief only advances when a write is reported to have landed, so a
			// store that refused one has it offered again on the next sweep.
			var planner = PlannerFor(new MirrorPolicy("/root") { QuietPeriodSeconds = 0 });
			var current = new[] { State("db/UserSetting.json", 1, 10) };

			planner.Plan(current, 0);

			await Assert.That(planner.Plan(current, 0).Puts.Count).IsEqualTo(1);
		}

		[Test]
		public async Task AConfirmedDeleteIsNotPlannedAgain()
		{
			var planner = SeededWith(State("Library/design.mcx", 100, 32));

			planner.MarkDeleted("Library/design.mcx");

			await Assert.That(planner.Plan(Array.Empty<MirrorFileState>(), 0).IsEmpty).IsTrue();
			await Assert.That(planner.MirroredCount).IsEqualTo(0);
		}

		[Test]
		public async Task ADebugRootAndAReleaseRootAreDifferentDatabases()
		{
			// The reason this is worth a test: the two configurations of the same application are served from
			// one origin, so if they shared a database the Debug build's first sweep would delete every key it
			// saw no file for - which is all of the Release build's data.
			string debug = MirrorPolicy.DatabaseNameForRoot("/MatterCAD_Debug");
			string release = MirrorPolicy.DatabaseNameForRoot("/MatterCAD");

			await Assert.That(debug).IsNotEqualTo(release);
			await Assert.That(new MirrorPolicy("/MatterCAD").DatabaseName).IsEqualTo(release);

			// One directory named three ways is still one database.
			await Assert.That(MirrorPolicy.DatabaseNameForRoot("/MatterCAD/")).IsEqualTo(release);
			await Assert.That(MirrorPolicy.DatabaseNameForRoot("\\MatterCAD")).IsEqualTo(release);
		}

		private static MirrorFileState State(string key, long modifiedTicks, long length)
		{
			return new MirrorFileState(key, modifiedTicks, length);
		}

		private static MirrorSweepPlanner PlannerFor(MirrorPolicy policy)
		{
			return new MirrorSweepPlanner(policy);
		}

		private static MirrorSweepPlanner SeededWith(params MirrorFileState[] mirrored)
		{
			return SeededWith(new MirrorPolicy("/root") { QuietPeriodSeconds = 0 }, mirrored);
		}

		private static MirrorSweepPlanner SeededWith(MirrorPolicy policy, params MirrorFileState[] mirrored)
		{
			var planner = new MirrorSweepPlanner(policy);

			planner.Seed(mirrored);

			return planner;
		}
	}
}
