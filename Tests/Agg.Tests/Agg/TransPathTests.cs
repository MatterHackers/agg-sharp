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

using System.Collections.Generic;
using System.Threading.Tasks;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;
using TUnit.Assertions;
using TUnit.Assertions.Enums;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests
{
	/// <summary>
	/// trans_single_path, trans_double_path and conv_segmentator against C++ AGG 2.4. The expected values were
	/// printed by a small program built from agg_trans_single_path.cpp, agg_trans_double_path.cpp and
	/// agg_vpgen_segmentator.cpp (clang, -ffp-contract=off) and are compared exactly.
	/// </summary>
	public class TransPathTests
	{
		// Before the start, the start, three points along, and two past the end (extrapolation).
		private static readonly (double X, double Y)[] Probes =
		{
			(-20, 5), (0, 0), (50, 10), (150, -8), (200, 0), (299.5, 3), (330, 4),
		};

		[Test]
		public async Task SinglePathFollowsArcLength()
		{
			var curve = new TransSinglePath();
			curve.AddPath(Path1());

			// The very short last segment (261,90.5) is folded into the one before it.
			await Assert.That(curve.TotalLength).IsEqualTo(284.74993443350098);
			await AssertTransforms(curve, new[]
			{
				(-10.554804791094465, 8.4188611699158109),
				(10.0, 10.0),
				(54.271887242357316, 35.29822128134704),
				(151.54529135982469, 18.867465908808509),
				(197.09405002870122, 34.878154654610327),
				(270.15341612138457, 102.44270794961193),
				(292.49572991119601, 123.21407780107724),
			});
		}

		[Test]
		public async Task SinglePathWithoutPreserveXScaleLooksSegmentsUpByIndex()
		{
			var curve = new TransSinglePath { PreserveXScale = false };
			curve.AddPath(Path1());

			await AssertTransforms(curve, new[]
			{
				(-10.554804791094465, 8.4188611699158109),
				(10.0, 10.0),
				(54.24774203608802, 35.29017287925727),
				(144.4864341897264, 20.632180201333082),
				(188.67607090652302, 27.551395048270042),
				(270.15341612138457, 102.44270794961193),
				(292.49572991119601, 123.21407780107724),
			});
		}

		[Test]
		public async Task SinglePathBaseLengthStretchesXOverThePath()
		{
			var curve = new TransSinglePath { BaseLength = 400 };
			curve.AddPath(Path1());

			await Assert.That(curve.TotalLength).IsEqualTo(400.0);
			await AssertTransforms(curve, new[]
			{
				(-5.0880141759862552, 10.241124708285215),
				(10.0, 10.0),
				(40.604910704586786, 30.742562435423526),
				(109.6169213283509, 29.34955841667696),
				(146.08820799208354, 28.477948001979115),
				(205.08354587407575, 45.807820882509802),
				(220.79937872050513, 60.811698061031485),
			});
		}

		[Test]
		public async Task SinglePathLeavesPointsAloneUntilFinalized()
		{
			var curve = new TransSinglePath();
			curve.MoveTo(10, 10);
			curve.FinalizePath();

			double x = 50;
			double y = 7;
			curve.Transform(ref x, ref y);
			await Assert.That((x, y)).IsEqualTo((50.0, 7.0));
			await Assert.That(curve.TotalLength).IsEqualTo(0.0);
		}

		/// <summary>
		/// A path whose points all coincide (a spline through one repeated point, say) collapses to a single
		/// vertex when finalized. C++ still calls it ready and then reads past it; the port stays not-ready, so
		/// points pass through untouched instead of throwing or turning into NaN.
		/// </summary>
		[Test]
		public async Task ZeroLengthPathsLeavePointsAlone()
		{
			var single = new TransSinglePath { BaseLength = 100 };
			single.MoveTo(5, 5);
			single.LineTo(5, 5);
			single.FinalizePath();
			await AssertUntouched(single);

			var zeroLength = new VertexStorage();
			zeroLength.MoveTo(5, 5);
			zeroLength.LineTo(5, 5);

			var doubleFirst = new TransDoublePath { BaseLength = 100 };
			doubleFirst.AddPaths(zeroLength, Path2());
			await AssertUntouched(doubleFirst);
			await Assert.That(doubleFirst.TotalLength2).IsEqualTo(100.0);

			var doubleSecond = new TransDoublePath();
			doubleSecond.AddPaths(Path1(), zeroLength);
			await AssertUntouched(doubleSecond);
			await Assert.That(doubleSecond.TotalLength1).IsEqualTo(0.0);

			static async Task AssertUntouched(ITransform transform)
			{
				double x = 50;
				double y = 7;
				transform.Transform(ref x, ref y);
				await Assert.That((x, y)).IsEqualTo((50.0, 7.0));
			}
		}

		/// <summary>
		/// Index lookup at exactly the total length: C++ reads one element past the last vertex there; the port
		/// steps back a segment, which lands on the path's end point.
		/// </summary>
		[Test]
		public async Task IndexLookupAtTheTotalLengthLandsOnTheEndPoint()
		{
			var single = new TransSinglePath { PreserveXScale = false };
			single.AddPath(Path1());
			double x = single.TotalLength;
			double y = 0;
			single.Transform(ref x, ref y);
			await Assert.That(x).IsEqualTo(261.0).Within(1e-9);
			await Assert.That(y).IsEqualTo(90.5).Within(1e-9);

			var both = new TransDoublePath { PreserveXScale = false, BaseHeight = 30 };
			both.AddPaths(Path1(), Path2());
			x = both.TotalLength1;
			y = 30;
			both.Transform(ref x, ref y);
			await Assert.That(x).IsEqualTo(280.0).Within(1e-9);
			await Assert.That(y).IsEqualTo(130.0).Within(1e-9);
		}

		[Test]
		public async Task VertexProcessorAdapterClosesPolygonsWhenTheProcessorAutoCloses()
		{
			var source = new VertexStorage();
			source.MoveTo(0, 0);
			source.LineTo(10, 0);
			source.LineTo(10, 10);
			source.MoveTo(20, 20);
			source.LineTo(30, 20);
			source.LineTo(30, 30);

			var closedFlags = FlagsAndCommand.EndPoly | FlagsAndCommand.FlagClose;
			await Assert.That(Read(new VertexProcessorAdapter(source, new PassThroughProcessor(autoClose: true, autoUnclose: false))))
				.IsEquivalentTo(new List<(FlagsAndCommand, double, double)>
				{
					(FlagsAndCommand.MoveTo, 0, 0), (FlagsAndCommand.LineTo, 10, 0), (FlagsAndCommand.LineTo, 10, 10),
					(FlagsAndCommand.LineTo, 0, 0), (closedFlags, 0, 0),
					(FlagsAndCommand.MoveTo, 20, 20), (FlagsAndCommand.LineTo, 30, 20), (FlagsAndCommand.LineTo, 30, 30),
					(FlagsAndCommand.LineTo, 20, 20), (closedFlags, 0, 0),
				}, CollectionOrdering.Matching);
		}

		[Test]
		public async Task VertexProcessorAdapterDropsEndPolyWhenTheProcessorAutoUncloses()
		{
			var source = new VertexStorage();
			source.MoveTo(0, 0);
			source.LineTo(10, 0);
			source.LineTo(10, 10);
			source.ClosePolygon();

			// The closing edge is still drawn back to the start; only the end-poly command is dropped.
			await Assert.That(Read(new VertexProcessorAdapter(source, new PassThroughProcessor(autoClose: false, autoUnclose: true))))
				.IsEquivalentTo(new List<(FlagsAndCommand, double, double)>
				{
					(FlagsAndCommand.MoveTo, 0, 0), (FlagsAndCommand.LineTo, 10, 0), (FlagsAndCommand.LineTo, 10, 10),
					(FlagsAndCommand.LineTo, 0, 0),
				}, CollectionOrdering.Matching);
		}

		[Test]
		public async Task DoublePathWarpsBetweenBothPaths()
		{
			var curve = new TransDoublePath { BaseHeight = 30 };
			curve.AddPaths(Path1(), Path2());

			await Assert.That(curve.TotalLength1).IsEqualTo(284.74993443350098);
			await Assert.That(curve.TotalLength2).IsEqualTo(306.49399281286003);
			await AssertTransforms(curve, new[]
			{
				(-7.4233615487881393, 8.2723639376691853),
				(10.0, 10.0),
				(61.349309508081681, 37.826792010812177),
				(149.15252452515207, 17.705969329628154),
				(197.09405002870122, 34.878154654610327),
				(274.03273093505044, 104.2851254739041),
				(297.69681860049883, 126.09681043739219),
			});
		}

		[Test]
		public async Task DoublePathWithBaseLengthAndIndexLookup()
		{
			var curve = new TransDoublePath { BaseHeight = 30, BaseLength = 500, PreserveXScale = false };
			curve.AddPaths(Path1(), Path2());

			await Assert.That(curve.TotalLength1).IsEqualTo(500.0);
			await Assert.That(curve.TotalLength2).IsEqualTo(500.0);
			await AssertTransforms(curve, new[]
			{
				(0.79489810129778782, 11.168612144717688),
				(10.0, 10.0),
				(40.333333333333336, 30.0),
				(88.333333333333329, 26.600000000000001),
				(116.0, 36.0),
				(165.55699999999999, 27.263000000000002),
				(181.03999999999999, 24.426666666666669),
			});
		}

		[Test]
		public async Task SegmentatorSplitsEdgesAndPassesEndPolyThrough()
		{
			var shape = new VertexStorage();
			shape.MoveTo(0, 0);
			shape.LineTo(10, 0);
			shape.LineTo(10, 3);
			shape.ClosePolygon();
			shape.MoveTo(20, 20);
			shape.LineTo(21, 24);

			var segmentator = new Segmentator(shape) { ApproximationScale = 0.5 };

			// Two units per piece; the closing edge back to (0,0) is segmented too, and a piece shorter than
			// one step is never emitted - the end point takes its place.
			var expected = new List<(FlagsAndCommand, double, double)>
			{
				(FlagsAndCommand.MoveTo, 0, 0),
				(FlagsAndCommand.LineTo, 2, 0),
				(FlagsAndCommand.LineTo, 4, 0),
				(FlagsAndCommand.LineTo, 6.0000000000000009, 0),
				(FlagsAndCommand.LineTo, 10, 0),
				(FlagsAndCommand.LineTo, 10, 3),
				(FlagsAndCommand.LineTo, 8.0843474295576971, 2.4253042288673092),
				(FlagsAndCommand.LineTo, 6.1686948591153943, 1.8506084577346185),
				(FlagsAndCommand.LineTo, 4.2530422886730923, 1.2759126866019277),
				(FlagsAndCommand.LineTo, 2.3373897182307886, 0.70121691546923692),
				(FlagsAndCommand.LineTo, 0, 0),
				(FlagsAndCommand.EndPoly | FlagsAndCommand.FlagClose, 0, 0),
				(FlagsAndCommand.MoveTo, 20, 20),
				(FlagsAndCommand.LineTo, 20.485071250072664, 21.940285000290665),
				(FlagsAndCommand.LineTo, 21, 24),
			};

			await Assert.That(Read(segmentator)).IsEquivalentTo(expected, CollectionOrdering.Matching);
		}

		private static VertexStorage Path1()
		{
			var path = new VertexStorage();
			path.MoveTo(10, 10);
			path.LineTo(100, 40);
			path.LineTo(180, 20);
			path.LineTo(260, 90);
			path.LineTo(261, 90.5);
			return path;
		}

		private static VertexStorage Path2()
		{
			var path = new VertexStorage();
			path.MoveTo(20, 40);
			path.LineTo(110, 80);
			path.LineTo(200, 50);
			path.LineTo(280, 130);
			return path;
		}

		private static List<(FlagsAndCommand, double, double)> Read(IVertexSource source)
		{
			var vertices = new List<(FlagsAndCommand, double, double)>();
			source.Rewind(0);
			FlagsAndCommand command;
			while (!ShapePath.IsStop(command = source.Vertex(out double x, out double y)))
			{
				vertices.Add((command, x, y));
			}

			return vertices;
		}

		private static async Task AssertTransforms(ITransform transform, (double X, double Y)[] expected)
		{
			var actual = new (double X, double Y)[Probes.Length];
			for (int i = 0; i < Probes.Length; i++)
			{
				double x = Probes[i].X;
				double y = Probes[i].Y;
				transform.Transform(ref x, ref y);
				actual[i] = (x, y);
			}

			await Assert.That(actual).IsEquivalentTo(expected, CollectionOrdering.Matching);
		}
	
		// A vpgen that emits each vertex it is fed unchanged, with the auto-close flags under test.
		private class PassThroughProcessor : IVertexProcessor
		{
			private readonly Queue<(FlagsAndCommand, double, double)> pending = new Queue<(FlagsAndCommand, double, double)>();

			public PassThroughProcessor(bool autoClose, bool autoUnclose)
			{
				AutoClose = autoClose;
				AutoUnclose = autoUnclose;
			}

			public bool AutoClose { get; }

			public bool AutoUnclose { get; }

			public void Reset() => pending.Clear();

			public void MoveTo(double x, double y) => pending.Enqueue((FlagsAndCommand.MoveTo, x, y));

			public void LineTo(double x, double y) => pending.Enqueue((FlagsAndCommand.LineTo, x, y));

			public FlagsAndCommand Vertex(out double x, out double y)
			{
				x = 0;
				y = 0;
				if (pending.Count == 0)
				{
					return FlagsAndCommand.Stop;
				}

				(FlagsAndCommand command, double px, double py) = pending.Dequeue();
				x = px;
				y = py;
				return command;
			}
		}
	}
}
