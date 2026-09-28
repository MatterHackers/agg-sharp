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
using System.Threading;
using System.Threading.Tasks;
using Agg;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests
{
	// SystemWindow and WinformsSystemWindow enable their filters from whatever thread shows a window, while
	// every Log call on every thread reads the set. A plain HashSet mutated concurrently loses entries,
	// corrupts its buckets or throws, so the filter set has to tolerate that. Keyless [NotInParallel]: the
	// filter set is process-wide, and the filters other tests rely on must not be disturbed by this one.
	[NotInParallel]
	public class DebugLoggerFilterConcurrencyTests
	{
		private const string FilterPrefix = "DebugLoggerFilterConcurrencyTests.";

		[Test]
		public async Task FiltersSurviveConcurrentEnableDisableAndLogging()
		{
			const string stableFilter = FilterPrefix + "Stable";
			const int writerCount = 8;
			const int iterations = 5000;

			var failures = new List<string>();
			void Record(Exception ex)
			{
				lock (failures)
				{
					failures.Add(ex.GetType().Name + ": " + ex.Message);
				}
			}

			DebugLogger.EnableFilter(stableFilter);
			try
			{
				using var start = new Barrier(writerCount + 1);
				var threads = new List<Thread>();
				for (int w = 0; w < writerCount; w++)
				{
					int writer = w;
					threads.Add(new Thread(() =>
					{
						try
						{
							start.SignalAndWait();
							for (int i = 0; i < iterations; i++)
							{
								var name = $"{FilterPrefix}{writer}.{i}";
								DebugLogger.EnableFilter(name);
								if (!DebugLogger.IsFilterEnabled(name))
								{
									throw new Exception($"{name} was lost right after being enabled");
								}

								DebugLogger.DisableFilter(name);
							}
						}
						catch (Exception ex)
						{
							Record(ex);
						}
					}));
				}

				// The reader enumerates the set (GetEnabledFilters) and logs (Contains) while the writers run.
				threads.Add(new Thread(() =>
				{
					try
					{
						start.SignalAndWait();
						for (int i = 0; i < iterations; i++)
						{
							DebugLogger.GetEnabledFilters();
							DebugLogger.Log(stableFilter, "concurrency probe", DebugLevel.Message);
							if (!DebugLogger.IsFilterEnabled(stableFilter))
							{
								throw new Exception("the stable filter was lost while others were added and removed");
							}
						}
					}
					catch (Exception ex)
					{
						Record(ex);
					}
				}));

				foreach (var thread in threads)
				{
					thread.Start();
				}

				foreach (var thread in threads)
				{
					thread.Join();
				}

				await Assert.That(string.Join("\n", failures)).IsEqualTo("");
				await Assert.That(DebugLogger.IsFilterEnabled(stableFilter)).IsTrue();

				var leftovers = new List<string>();
				foreach (var filter in DebugLogger.GetEnabledFilters())
				{
					if (filter.StartsWith(FilterPrefix) && filter != stableFilter)
					{
						leftovers.Add(filter);
					}
				}

				await Assert.That(leftovers.Count).IsEqualTo(0);
			}
			finally
			{
				DebugLogger.DisableFilter(stableFilter);
			}
		}
	}
}
