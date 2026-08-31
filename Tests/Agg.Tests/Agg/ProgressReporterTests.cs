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
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests
{
	/// <summary>
	/// <see cref="ProgressReporter.UiYield"/> is process global state, so every test that installs a hook is
	/// a keyless <c>[NotInParallel]</c> and restores the hook in a finally block. The tests that assert the
	/// no-hook path take the same treatment - they would otherwise see another test's hook.
	/// </summary>
	public class ProgressReporterTests
	{
		[Test]
		[NotInParallel]
		public async Task YieldToUiCompletesSynchronouslyWithNoHookInstalled()
		{
			var previousHook = ProgressReporter.UiYield;
			ProgressReporter.UiYield = null;

			try
			{
				var reporter = new ProgressReporter((ratio, message) => { });

				// The desktop path. Completing synchronously is the whole point: awaiting this in a job's
				// inner loop must not cost a state machine or a continuation.
				await Assert.That(reporter.YieldToUi().IsCompletedSuccessfully).IsTrue();
				await Assert.That(reporter.YieldToUi().IsCompletedSuccessfully).IsTrue();
			}
			finally
			{
				ProgressReporter.UiYield = previousHook;
			}
		}

		[Test]
		[NotInParallel]
		public async Task ThrottleCollapsesRapidYieldsIntoOne()
		{
			var previousHook = ProgressReporter.UiYield;
			int yieldCount = 0;
			ProgressReporter.UiYield = () =>
			{
				yieldCount++;
				return Task.CompletedTask;
			};

			try
			{
				var reporter = new ProgressReporter((ratio, message) => { });

				// A job reports far more often than a frame lasts; without the throttle each of these would
				// be a two idle turn hop.
				for (int report = 0; report < 20; report++)
				{
					await reporter.YieldToUi();
				}

				await Assert.That(yieldCount).IsEqualTo(1)
					.Because("only the first yield may get through until the throttle window has passed");
			}
			finally
			{
				ProgressReporter.UiYield = previousHook;
			}
		}

		[Test]
		[NotInParallel]
		public async Task YieldsAgainOnceTheThrottleWindowHasPassed()
		{
			var previousHook = ProgressReporter.UiYield;
			int yieldCount = 0;
			ProgressReporter.UiYield = () =>
			{
				yieldCount++;
				return Task.CompletedTask;
			};

			try
			{
				var reporter = new ProgressReporter((ratio, message) => { });

				await reporter.YieldToUi();
				await reporter.YieldToUi();

				// The throttle reads a real clock (Environment.TickCount64 - agg has no injectable one), so
				// proving it reopens costs one wait of the window itself, on that same coarse clock rather
				// than on a guessed margin over it - see ProgressThrottleWait.
				await ProgressThrottleWait.WaitOutTheWindowAsync();

				await reporter.YieldToUi();

				await Assert.That(yieldCount).IsEqualTo(2)
					.Because("the window reopening must let the next report yield again");
			}
			finally
			{
				ProgressReporter.UiYield = previousHook;
			}
		}

		[Test]
		[NotInParallel]
		public async Task TheReturnedValueTaskCarriesTheHopRatherThanCompletingAheadOfIt()
		{
			var previousHook = ProgressReporter.UiYield;
			var hopFinished = new TaskCompletionSource();
			ProgressReporter.UiYield = () => hopFinished.Task;

			try
			{
				var reporter = new ProgressReporter((ratio, message) => { });

				var yielding = reporter.YieldToUi();

				// The whole point of the seam: awaiting must actually park the job until the UI has had its
				// turn. A reporter that returned an already-completed ValueTask here would look identical to
				// callers and paint nothing.
				await Assert.That(yielding.IsCompleted).IsFalse()
					.Because("the caller must stay suspended until the host's yield finishes");

				hopFinished.SetResult();

				await yielding;
			}
			finally
			{
				ProgressReporter.UiYield = previousHook;
			}
		}

		[Test]
		[NotInParallel]
		public async Task NullReporterSwallowsReportsAndNeverYields()
		{
			var previousHook = ProgressReporter.UiYield;
			int yieldCount = 0;
			ProgressReporter.UiYield = () =>
			{
				yieldCount++;
				return Task.CompletedTask;
			};

			try
			{
				ProgressReporter.Null.Report(0.5, "swallowed");
				await ProgressReporter.Null.ReportAndYield(0.5, "swallowed");

				await Assert.That(yieldCount).IsEqualTo(0)
					.Because("nothing is showing this job's progress, so there is no frame worth painting");
			}
			finally
			{
				ProgressReporter.UiYield = previousHook;
			}
		}

		[Test]
		public async Task ReportReachesTheWrappedAction()
		{
			var reported = new List<(double Ratio, string Message)>();
			var reporter = new ProgressReporter((ratio, message) => reported.Add((ratio, message)));

			reporter.Report(0.25, "quarter");
			await reporter.ReportAndYield(0.75, "three quarters");

			await Assert.That(reported.Count).IsEqualTo(2);
			await Assert.That(reported[0]).IsEqualTo((0.25, "quarter"));
			await Assert.That(reported[1]).IsEqualTo((0.75, "three quarters"));
		}

		[Test]
		public async Task ConvertingToAnActionKeepsDeliveringToTheWrappedAction()
		{
			double lastRatio = 0;
			string lastMessage = null;
			var reporter = new ProgressReporter((ratio, message) =>
			{
				lastRatio = ratio;
				lastMessage = message;
			});

			// The conversion that lets a reporter be handed to the ~200 sites still typed as an Action.
			Action<double, string> asAction = reporter;
			asAction(0.4, "forwarded");

			await Assert.That(lastRatio).IsEqualTo(0.4);
			await Assert.That(lastMessage).IsEqualTo("forwarded");
		}

		[Test]
		public async Task ConvertingTheSameReporterTwiceGivesTheSameDelegate()
		{
			var reporter = new ProgressReporter((ratio, message) => { });

			Action<double, string> first = reporter;
			Action<double, string> second = reporter;

			await Assert.That(ReferenceEquals(first, second)).IsTrue()
				.Because("a forwarding site converting on every call must not allocate a delegate each time");
		}

		[Test]
		public async Task ConvertingBackFromItsOwnActionGivesTheSameReporter()
		{
			var reporter = new ProgressReporter((ratio, message) => { });

			Action<double, string> asAction = reporter;
			ProgressReporter roundTripped = asAction;

			await Assert.That(ReferenceEquals(roundTripped, reporter)).IsTrue()
				.Because("round tripping through a forwarding site must not stack another wrapper, which would carry its own throttle state");
		}

		[Test]
		public async Task ConvertingAnActionGivesAReporterThatDeliversToIt()
		{
			double lastRatio = 0;
			string lastMessage = null;

			ProgressReporter reporter = (Action<double, string>)((ratio, message) =>
			{
				lastRatio = ratio;
				lastMessage = message;
			});

			reporter.Report(0.6, "wrapped");

			await Assert.That(lastRatio).IsEqualTo(0.6);
			await Assert.That(lastMessage).IsEqualTo("wrapped");
		}

		[Test]
		public async Task TheNullReporterConvertedToAnActionIsASafeNoOp()
		{
			// The exact shape a Null reporter takes when it is forwarded into an API still typed as an
			// Action: the conversion must produce a callable delegate, not null and not a throw.
			Action<double, string> asAction = ProgressReporter.Null;

			asAction(0.5, "swallowed");

			await Assert.That(asAction).IsNotNull();
		}

		[Test]
		public async Task ConvertingANullActionGivesTheNullReporter()
		{
			ProgressReporter reporter = (Action<double, string>)null;

			await Assert.That(ReferenceEquals(reporter, ProgressReporter.Null)).IsTrue()
				.Because("today's null reporter paths must land on Null rather than on a NullReferenceException");
		}

		[Test]
		public async Task ConvertingANullReporterGivesANullAction()
		{
			Action<double, string> asAction = (ProgressReporter)null;

			// Not a no-op action: a call site that still tests its reporter for null has to see what it was
			// given.
			await Assert.That(asAction).IsNull();
		}

		[Test]
		public async Task AScaledReporterMapsItsRangeIntoTheParentsWindow()
		{
			var reports = new List<(double ratio, string message)>();
			var parent = new ProgressReporter((ratio, message) => reports.Add((ratio, message)));

			var secondHalf = parent.Scaled(0.5, 0.5);

			secondHalf.Report(0, "start");
			secondHalf.Report(0.5, "middle");
			secondHalf.Report(1, "end");

			await Assert.That(reports.ConvertAll(report => report.ratio)).IsEquivalentTo(new[] { 0.5, 0.75, 1.0 });

			// The message is the child's, untouched: the parent supplies where in the job this is, the child
			// supplies what it is doing.
			await Assert.That(reports.ConvertAll(report => report.message)).IsEquivalentTo(new[] { "start", "middle", "end" });
		}

		[Test]
		[NotInParallel]
		public async Task ScaledChildrenShareOneThrottleWindowWithTheirParent()
		{
			// The bug this pins: a hand written wrapper per phase starts with its last yield at zero, so its
			// first YieldToUi always hops. A job that makes one per item - a load with hundreds of cached
			// mesh links - then pays a hop per item for work that takes no time at all, and the throttle
			// stops meaning anything.
			var previousHook = ProgressReporter.UiYield;
			int yieldCount = 0;
			ProgressReporter.UiYield = () =>
			{
				yieldCount++;
				return Task.CompletedTask;
			};

			try
			{
				var parent = new ProgressReporter((ratio, message) => { });

				await parent.Scaled(0.5, 0).YieldToUi();
				await parent.Scaled(0.5, 0.5).YieldToUi();
				await parent.YieldToUi();

				await Assert.That(yieldCount).IsEqualTo(1)
					.Because("the throttle is one budget for the job, however many slices the job carves it into");
			}
			finally
			{
				ProgressReporter.UiYield = previousHook;
			}
		}

		[Test]
		public async Task ScalingAReporterNobodyIsWatchingGivesBackTheSameReporter()
		{
			// Nothing to report to and nothing to paint, so a wrapper would be pure cost - and, worse, a
			// live-looking object that a caller testing for "is anyone watching" cannot see through.
			await Assert.That(ReferenceEquals(ProgressReporter.Null.Scaled(0.5), ProgressReporter.Null)).IsTrue();
		}

		[Test]
		public async Task HasTargetSeesEveryShapeOfNobodyWatching()
		{
			// Not just the Null singleton: a reporter built around a null action is exactly as unwatched, and
			// a caller comparing against Null by reference would take it for a live one.
			await Assert.That(ProgressReporter.Null.HasTarget).IsFalse();
			await Assert.That(new ProgressReporter(null).HasTarget).IsFalse();
			await Assert.That(new ProgressReporter((ratio, message) => { }).HasTarget).IsTrue();
			await Assert.That(new ProgressReporter((ratio, message) => { }).Scaled(0.5).HasTarget).IsTrue();
		}
	}
}
