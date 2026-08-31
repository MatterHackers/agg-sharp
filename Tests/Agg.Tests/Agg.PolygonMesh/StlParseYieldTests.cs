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
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.Tests;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.PolygonMesh.UnitTests
{
	/// <summary>
	/// The awaitable half of the STL parser: a large model has to hand the UI its thread back while it comes
	/// in, or in the browser - where the parse and the paint share one thread - the frame is frozen for the
	/// whole load and the progress bar never moves.
	/// </summary>
	/// <remarks>
	/// <see cref="ProgressReporter.UiYield"/> is process global state, so every test here installs its hook
	/// inside a try/finally that restores it and the class is <c>[NotInParallel]</c>.
	/// </remarks>
	[NotInParallel]
	public class StlParseYieldTests
	{
		/// <summary>
		/// Enough faces to cross <see cref="StlProcessing.FacesPerYield"/> several times, so a yield has to
		/// land in the middle of the parse rather than only at its end.
		/// </summary>
		private const int FaceCount = StlProcessing.FacesPerYield * 3;

		/// <summary>
		/// Counts the points the UI was handed the thread, and waits out
		/// <see cref="ProgressReporter.YieldThrottleMs"/> at each - otherwise a parse this fast finishes
		/// inside one throttle window and only its first chunk boundary would yield.
		/// </summary>
		private sealed class YieldLog
		{
			public int Yields { get; private set; }

			public async Task RecordYield()
			{
				this.Yields++;

				await ProgressThrottleWait.WaitOutTheWindowAsync();
			}
		}

		/// <summary>
		/// A real binary STL, written by the production writer: a ribbon of distinct triangles, one per
		/// step along X, so no two of them merge into the same face.
		/// </summary>
		private static byte[] MultiChunkStlBytes()
		{
			var mesh = new Mesh();

			for (int i = 0; i < FaceCount; i++)
			{
				mesh.CreateFace(
					new Vector3(i, 0, 0),
					new Vector3(i, 1, 0),
					new Vector3(i, 0, 1));
			}

			var stream = new MemoryStream();
			StlProcessing.Save(mesh, stream, CancellationToken.None, new MeshOutputSettings(), leaveStreamOpen: true);

			return stream.ToArray();
		}

		/// <summary>
		/// The same ribbon as an ASCII STL. The text is generated here rather than saved through the writer
		/// because the two parse branches are separate loops with a chunk boundary each, and only the binary
		/// one is what <see cref="MultiChunkStlBytes"/> exercises.
		/// </summary>
		private static byte[] MultiChunkAsciiStlBytes()
		{
			var text = new StringBuilder();
			text.AppendLine("solid ribbon");

			for (int i = 0; i < FaceCount; i++)
			{
				text.AppendLine("  facet normal 0 0 1");
				text.AppendLine("    outer loop");
				text.AppendLine(FormattableString.Invariant($"      vertex {i} 0 0"));
				text.AppendLine(FormattableString.Invariant($"      vertex {i} 1 0"));
				text.AppendLine(FormattableString.Invariant($"      vertex {i} 0 1"));
				text.AppendLine("    endloop");
				text.AppendLine("  endfacet");
			}

			text.AppendLine("endsolid ribbon");

			return Encoding.ASCII.GetBytes(text.ToString());
		}

		private static (int vertices, int faces) SizeOf(Mesh mesh)
		{
			return (mesh.Vertices.Count, mesh.Faces.Count);
		}

		// Binary and ASCII are two separate loops in the parser with a chunk boundary each, so both have to
		// be walked or half the seam is untested.
		[Test]
		[Arguments(true)]
		[Arguments(false)]
		public async Task ALargeStlHandsTheUiItsThreadBackWhileItParses(bool binary)
		{
			var previousHook = ProgressReporter.UiYield;
			var log = new YieldLog();
			ProgressReporter.UiYield = log.RecordYield;

			Mesh parsed;

			try
			{
				using (var stream = new MemoryStream(binary ? MultiChunkStlBytes() : MultiChunkAsciiStlBytes()))
				{
					parsed = await StlProcessing.ParseFileContentsAsync(
						stream,
						CancellationToken.None,
						new ProgressReporter((ratio, message) => { }));
				}
			}
			finally
			{
				ProgressReporter.UiYield = previousHook;
			}

			await Assert.That(log.Yields).IsGreaterThanOrEqualTo(2)
				.Because("a parse that only yields once has held the frame for everything but the last chunk");

			await Assert.That(parsed.Faces.Count).IsEqualTo(FaceCount);
		}

		[Test]
		public async Task AParseNobodyIsWatchingNeverYields()
		{
			// The hook is installed, so the only reason not to hop the event loop is that there is no
			// reporter. Getting this wrong costs every non-UI caller - exports, thumbnails, tests - a UI hop
			// per chunk of every STL it reads.
			var previousHook = ProgressReporter.UiYield;
			var log = new YieldLog();
			ProgressReporter.UiYield = log.RecordYield;

			try
			{
				using (var stream = new MemoryStream(MultiChunkStlBytes()))
				{
					await StlProcessing.ParseFileContentsAsync(stream, CancellationToken.None, null);
				}

				// And the Null singleton, which is what a null Action becomes crossing the implicit
				// conversion - the shape every caller still typed as an Action arrives in.
				using (var stream = new MemoryStream(MultiChunkStlBytes()))
				{
					await StlProcessing.ParseFileContentsAsync(stream, CancellationToken.None, ProgressReporter.Null);
				}
			}
			finally
			{
				ProgressReporter.UiYield = previousHook;
			}

			await Assert.That(log.Yields).IsEqualTo(0)
				.Because("a parse with no reporter has no progress to paint");
		}

		[Test]
		[Arguments(true)]
		[Arguments(false)]
		public async Task YieldingDoesNotChangeTheParsedMesh(bool binary)
		{
			var stlBytes = binary ? MultiChunkStlBytes() : MultiChunkAsciiStlBytes();

			var previousHook = ProgressReporter.UiYield;
			var log = new YieldLog();
			ProgressReporter.UiYield = log.RecordYield;

			Mesh yielding;

			try
			{
				using (var stream = new MemoryStream(stlBytes))
				{
					yielding = await StlProcessing.ParseFileContentsAsync(
						stream,
						CancellationToken.None,
						new ProgressReporter((ratio, message) => { }));
				}
			}
			finally
			{
				ProgressReporter.UiYield = previousHook;
			}

			Mesh plain;
			using (var stream = new MemoryStream(stlBytes))
			{
				plain = StlProcessing.ParseFileContents(stream, CancellationToken.None, null);
			}

			await Assert.That(SizeOf(yielding)).IsEqualTo(SizeOf(plain))
				.Because("chunking the parse must not change the mesh it builds");

			var yieldingPositions = new List<Vector3Float>(yielding.Vertices);
			var plainPositions = new List<Vector3Float>(plain.Vertices);

			await Assert.That(yieldingPositions).IsEquivalentTo(plainPositions)
				.Because("the chunk boundaries must not reorder the vertices either");
		}
	}
}
