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
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests
{
	// These tests hammer process-global singletons from many threads at once, so they must not
	// run alongside other tests that read the same statics (StaticData.Instance / RootPath,
	// GuiWidget.DeviceScale). Both save and restore the global state they disturb.
	public class SingletonThreadSafetyTests
	{
		[Test]
		[NotInParallel]
		public async Task StaticDataInstanceFirstTouchFromManyThreadsYieldsOneInstance()
		{
			// StaticData has no reset API, so put the private statics back to their
			// pre-first-touch state via reflection to recreate a true first touch.
			var instanceField = typeof(StaticData).GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static);
			await Assert.That(instanceField).IsNotNull();

			var savedInstance = instanceField.GetValue(null);
			var savedRootPath = StaticData.RootPath;

			try
			{
				instanceField.SetValue(null, null);
				StaticData.RootPath = null;

				const int threadCount = 8;
				var results = new ConcurrentBag<IStaticData>();
				using (var barrier = new Barrier(threadCount))
				{
					var threads = Enumerable.Range(0, threadCount)
						.Select(_ => new Thread(() =>
						{
							// Line every thread up on the unlocked check-then-create window.
							barrier.SignalAndWait();
							results.Add(StaticData.Instance);
						}))
						.ToList();

					threads.ForEach(thread => thread.Start());
					threads.ForEach(thread => thread.Join());
				}

				await Assert.That(results.Count).IsEqualTo(threadCount);
				await Assert.That(results.Distinct().Count()).IsEqualTo(1);
				await Assert.That(results.First()).IsNotNull();

				// The constructor's default RootPath write must also have been published.
				await Assert.That(string.IsNullOrEmpty(StaticData.RootPath)).IsFalse();
			}
			finally
			{
				instanceField.SetValue(null, savedInstance);
				StaticData.RootPath = savedRootPath;
			}
		}

		[Test]
		[NotInParallel]
		public async Task DropArrowReadersNeverObservePartiallyBuiltArrows()
		{
			var savedDeviceScale = GuiWidget.DeviceScale;

			try
			{
				const int readerCount = 4;
				const int iterationsPerReader = 2000;

				var failures = new ConcurrentQueue<string>();
				using (var startBarrier = new Barrier(readerCount + 1))
				{
					var stopWriter = false;

					// Writer keeps flipping DeviceScale so readers constantly hit the
					// stale-scale rebuild path in the UpArrow/DownArrow getters.
					var writer = new Thread(() =>
					{
						startBarrier.SignalAndWait();
						var scale = 1.0;
						while (!Volatile.Read(ref stopWriter))
						{
							scale = scale == 1.0 ? 2.0 : 1.0;
							GuiWidget.DeviceScale = scale;
						}
					});

					var readers = Enumerable.Range(0, readerCount)
						.Select(_ => new Thread(() =>
						{
							startBarrier.SignalAndWait();
							for (int i = 0; i < iterationsPerReader; i++)
							{
								CheckArrow(DropArrow.DownArrow, "DownArrow", failures);
								CheckArrow(DropArrow.UpArrow, "UpArrow", failures);
							}
						}))
						.ToList();

					writer.Start();
					readers.ForEach(reader => reader.Start());
					readers.ForEach(reader => reader.Join());

					Volatile.Write(ref stopWriter, true);
					writer.Join();
				}

				await Assert.That(string.Join("; ", failures)).IsEqualTo(string.Empty);
			}
			finally
			{
				GuiWidget.DeviceScale = savedDeviceScale;
			}
		}

		private static void CheckArrow(VertexSource.VertexStorage arrow, string name, ConcurrentQueue<string> failures)
		{
			if (arrow == null)
			{
				failures.Enqueue($"{name} was null");
				return;
			}

			// Every published arrow is MoveTo + LineTo + LineTo + ClosePolygon = 4 stored
			// vertices, and Vertices() appends a trailing Stop command for 5 total. The old
			// code published the storage before adding any vertices, so a concurrent reader
			// could observe fewer.
			var vertices = arrow.Vertices().ToList();
			if (vertices.Count != 5)
			{
				failures.Enqueue($"{name} had {vertices.Count} vertices instead of 5");
				return;
			}

			// Both arrows put +arrowHeight in vertex 1 X and -arrowHeight in vertex 0 X; if a
			// rebuild mixed two DeviceScale values mid-build the magnitudes would disagree.
			var arrowHeight = Math.Abs(vertices[1].X);
			if (arrowHeight == 0
				|| Math.Abs(vertices[0].X) != arrowHeight)
			{
				failures.Enqueue($"{name} was built with inconsistent scale: {vertices[0].X}, {vertices[1].X}");
			}
		}
	}
}
