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
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using TUnit.Assertions;
using TUnit.Assertions.Enums;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// The mac video reader against the same checked-in fixtures as MediaFoundationVideoFrameReaderTests
	/// (their layout is described there): H.264, which every Mac decodes. The same questions get the same
	/// answers on both platforms, so a caller sees one contract.
	/// </summary>
	public class MacVideoFrameReaderTests
	{
		// H.264 is lossy and the decoder's YUV to RGB matrix need not be the encoder's, so "red" is judged
		// with a tolerance rather than as 255, 0, 0 - wide enough for that, far too narrow to mistake one
		// of the fixture's colours for another.
		private const int ColorTolerance = 40;

		private static readonly IVideoFrameReader Reader = new MacVideoFrameReader();

		[Test]
		public async Task TheMacDefaultIsTheAVFoundationReader()
		{
			await Assert.That(AggContext.CreateInstanceFrom<IVideoFrameReader>(AggContext.Config.ProviderTypes.VideoFrameReaderProvider))
				.IsTypeOf<MacVideoFrameReader>();
			await Assert.That(Reader.IsSupported).IsTrue();
			await Assert.That(Reader.UnsupportedReason).IsNull();
		}

		[Test]
		public async Task InfoReportsDurationSizeAndFrameRate()
		{
			var info = await Reader.GetInfoAsync(FixturePath("solid-colors.mp4"), CancellationToken.None);

			await Assert.That(info.Duration).IsEqualTo(TimeSpan.FromSeconds(3));
			await Assert.That(info.Width).IsEqualTo(64);
			await Assert.That(info.Height).IsEqualTo(48);
			await Assert.That(info.Rotation).IsEqualTo(0);
			await Assert.That(info.FramesPerSecond).IsEqualTo(5.0);
		}

		/// <summary>
		/// Each requested time gets the frame on screen then, in the order asked - including a time before
		/// the previous one (a backwards seek) and one past the end (the last frame).
		/// </summary>
		[Test]
		public async Task EachTimeGetsTheFrameShownAtThatTime()
		{
			var times = new[] { TimeSpan.FromSeconds(1.5), TimeSpan.FromSeconds(0.5), TimeSpan.FromSeconds(2.5), TimeSpan.FromSeconds(10) };
			var seen = new List<(int Index, Color TopRight)>();

			await Reader.ReadFramesAsync(FixturePath("solid-colors.mp4"), times, (index, frame) =>
			{
				seen.Add((index, frame.GetPixel(56, frame.Height - 1 - 12)));
				return Task.CompletedTask;
			}, CancellationToken.None);

			await Assert.That(seen.Count).IsEqualTo(4);
			for (int i = 0; i < seen.Count; i++)
			{
				await Assert.That(seen[i].Index).IsEqualTo(i);
			}

			await AssertColor(seen[0].TopRight, 0, 255, 0, "1.5 s is in the green second");
			await AssertColor(seen[1].TopRight, 255, 0, 0, "0.5 s is in the red second");
			await AssertColor(seen[2].TopRight, 0, 0, 255, "2.5 s is in the blue second");
			await AssertColor(seen[3].TopRight, 0, 0, 255, "a time past the end gets the last frame");
		}

		/// <summary>
		/// A frame is chosen by its display interval, [start, next start): at 5 fps a time just before a
		/// second boundary is still the previous second's colour and the boundary itself is the next one's,
		/// so an image generator that rounded to the nearest frame, or used a tolerance, would fail here.
		/// </summary>
		[Test]
		public async Task FrameBoundariesBelongToTheFrameThatStartsThere()
		{
			var times = new[] { TimeSpan.FromSeconds(1) - TimeSpan.FromMilliseconds(1), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2) - TimeSpan.FromMilliseconds(1), TimeSpan.FromSeconds(2) };
			var seen = new List<Color>();

			await Reader.ReadFramesAsync(FixturePath("solid-colors.mp4"), times, (index, frame) =>
			{
				seen.Add(frame.GetPixel(56, frame.Height - 1 - 12));
				return Task.CompletedTask;
			}, CancellationToken.None);

			await AssertColor(seen[0], 255, 0, 0, "just before 1 s is still the red second");
			await AssertColor(seen[1], 0, 255, 0, "1 s starts the green second");
			await AssertColor(seen[2], 0, 255, 0, "just before 2 s is still the green second");
			await AssertColor(seen[3], 0, 0, 255, "2 s starts the blue second");
		}

		/// <summary>
		/// Reading a list must give exactly the frames lone requests for the same times get, whatever the
		/// reader does to go faster between neighbouring times. Every tenth of a second over the 5 fps
		/// fixture lands two times in most frames, on frame boundaries and past the end.
		/// </summary>
		[Test]
		public async Task ReadingOnGivesTheSameFramesAsSeeking()
		{
			var path = FixturePath("solid-colors.mp4");
			var times = new List<TimeSpan>();
			for (int tenth = 0; tenth <= 32; tenth++)
			{
				times.Add(TimeSpan.FromSeconds(tenth / 10.0));
			}

			var together = new byte[times.Count][];
			await Reader.ReadFramesAsync(path, times, (index, frame) =>
			{
				together[index] = frame.GetBuffer().ToArray();
				return Task.CompletedTask;
			}, CancellationToken.None);

			for (int i = 0; i < times.Count; i++)
			{
				byte[] alone = null;
				await Reader.ReadFramesAsync(path, new[] { times[i] }, (index, frame) =>
				{
					alone = frame.GetBuffer().ToArray();
					return Task.CompletedTask;
				}, CancellationToken.None);

				await Assert.That(together[i].SequenceEqual(alone)).IsTrue().Because($"the frame at {times[i]} read in a list matches the frame read on its own");
			}
		}

		/// <summary>
		/// The frame comes out as ImageIO.LoadImage would load a photo of it: 32-bit BGRA, opaque, with the
		/// top of the video at the top of the image as agg draws it (agg's y = 0 is the bottom row), and the
		/// left of the video at x = 0.
		/// </summary>
		[Test]
		public async Task TopLeftOfTheVideoIsTopLeftOfTheImage()
		{
			ImageBuffer frame = null;
			await Reader.ReadFramesAsync(FixturePath("solid-colors.mp4"), new[] { TimeSpan.FromSeconds(0.5) }, (index, image) =>
			{
				frame = image;
				return Task.CompletedTask;
			}, CancellationToken.None);

			await Assert.That(frame.Width).IsEqualTo(64);
			await Assert.That(frame.Height).IsEqualTo(48);
			await Assert.That(frame.BitDepth).IsEqualTo(32);

			int top = frame.Height - 1 - 6;
			await AssertColor(frame.GetPixel(6, top), 0, 0, 0, "the top-left block is black");
			await AssertColor(frame.GetPixel(56, top), 255, 0, 0, "the rest of the top half is red");
			await AssertColor(frame.GetPixel(6, 6), 255, 255, 255, "the bottom half is white");
			await AssertColor(frame.GetPixel(56, 6), 255, 255, 255, "the bottom half is white");
			await Assert.That(frame.GetPixel(56, top).alpha).IsEqualTo((byte)255);
			await Assert.That(frame.GetPixel(6, top).alpha).IsEqualTo((byte)255);
		}

		/// <summary>
		/// A video stored sideways with a rotation (how phones record portrait) comes out upright, with the
		/// codec's padding cropped away: the stored top-left black block is at the top right once turned
		/// clockwise, the stored top half (red) is the right half, and the stored bottom (white) the left.
		/// </summary>
		[Test]
		public async Task ARotatedVideoComesOutUpright()
		{
			var path = FixturePath("rotated-90.mp4");
			var info = await Reader.GetInfoAsync(path, CancellationToken.None);
			await Assert.That(info.Rotation).IsEqualTo(90);
			await Assert.That(info.Width).IsEqualTo(40);
			await Assert.That(info.Height).IsEqualTo(64);

			ImageBuffer frame = null;
			await Reader.ReadFramesAsync(path, new[] { TimeSpan.FromSeconds(0.5) }, (index, image) =>
			{
				frame = image;
				return Task.CompletedTask;
			}, CancellationToken.None);

			await Assert.That(frame.Width).IsEqualTo(40);
			await Assert.That(frame.Height).IsEqualTo(64);

			// Display row r is agg row 63 - r. The corners are checked, so padding left in on any side would fail.
			await AssertColor(frame.GetPixel(39, 63), 0, 0, 0, "the stored top-left block is at the top right");
			await AssertColor(frame.GetPixel(39, 63 - 40), 255, 0, 0, "the stored top half is the right half");
			await AssertColor(frame.GetPixel(39, 0), 255, 0, 0, "the stored top half is the right half");
			await AssertColor(frame.GetPixel(0, 63), 255, 255, 255, "the stored bottom half is the left half");
			await AssertColor(frame.GetPixel(0, 0), 255, 255, 255, "the stored bottom half is the left half");
			await AssertColor(frame.GetPixel(19, 32), 255, 255, 255, "the left half is white up to its edge");
			await AssertColor(frame.GetPixel(21, 32), 255, 0, 0, "the right half is red just inside its edge");

			// x = 20 is the last red stored row, next to white. Apple's decoder interpolates 4:2:0 chroma
			// between rows where Media Foundation repeats it, so that row comes out a desaturated red (about
			// 161, 37, 36; the unrotated fixture's row 23 does the same, with no rotation involved) instead
			// of pure red. It still has red's luma, so it is clearly on the red side: a picture shifted
			// a pixel either way would make x = 20 white or x = 19 red.
			var edge = frame.GetPixel(20, 32);
			await Assert.That((int)edge.red).IsGreaterThan(128).Because($"the right half starts at x = 20 (got {edge.red}, {edge.green}, {edge.blue})");
			await Assert.That((int)edge.green).IsLessThan(128).Because($"the right half starts at x = 20 (got {edge.red}, {edge.green}, {edge.blue})");
			await Assert.That((int)edge.blue).IsLessThan(128).Because($"the right half starts at x = 20 (got {edge.red}, {edge.green}, {edge.blue})");
		}

		/// <summary>
		/// Cancelling stops the read before the next frame is decoded, as an OperationCanceledException -
		/// which a caller treats as the user stopping, not as a failure to report.
		/// </summary>
		[Test]
		public async Task CancellingStopsBeforeTheNextFrame()
		{
			using var cancel = new CancellationTokenSource();
			var times = new[] { TimeSpan.FromSeconds(0.5), TimeSpan.FromSeconds(1.5), TimeSpan.FromSeconds(2.5) };
			var seen = new List<int>();

			await Assert.That(async () => await Reader.ReadFramesAsync(FixturePath("solid-colors.mp4"), times, (index, frame) =>
			{
				seen.Add(index);
				cancel.Cancel();
				return Task.CompletedTask;
			}, cancel.Token)).Throws<OperationCanceledException>();

			await Assert.That(seen).IsEquivalentTo(new[] { 0 }, CollectionOrdering.Matching);

			await Assert.That(async () => await Reader.GetInfoAsync(FixturePath("solid-colors.mp4"), cancel.Token))
				.Throws<OperationCanceledException>();
		}

		/// <summary>
		/// A phone clip's audio often runs a little past its last frame, and the file's duration is the
		/// longer track's. audio-outlasts-video.mp4 is solid-colors.mp4's 3 s of video with 3.236 s of audio
		/// (TestData/Video/make-audio-outlasts-video.swift), so the times between 3 s and the file's end - and
		/// past it - have no frame of their own and must get the last one, not an error.
		/// </summary>
		[Test]
		public async Task TimesAfterTheLastFrameGetItWhenTheAudioRunsLonger()
		{
			var path = FixturePath("audio-outlasts-video.mp4");
			var info = await Reader.GetInfoAsync(path, CancellationToken.None);

			// The file's length, as Windows reports it (Media Foundation's presentation duration).
			await Assert.That(info.Duration).IsGreaterThan(TimeSpan.FromSeconds(3.2));

			var times = new[] { TimeSpan.FromSeconds(2.9), TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3.1), info.Duration, TimeSpan.FromSeconds(10) };
			var seen = new List<Color>();
			await Reader.ReadFramesAsync(path, times, (index, frame) =>
			{
				seen.Add(frame.GetPixel(56, frame.Height - 1 - 12));
				return Task.CompletedTask;
			}, CancellationToken.None);

			await Assert.That(seen.Count).IsEqualTo(times.Length);
			for (int i = 0; i < seen.Count; i++)
			{
				await AssertColor(seen[i], 0, 0, 255, $"{times[i]} is on or after the last (blue) frame");
			}
		}

		/// <summary>
		/// A time at or past the video track's end is read just inside it (one 100 ns tick), where the last
		/// frame is on screen; a negative time is read at 0; any other time as it is.
		/// </summary>
		[Test]
		public async Task ReadTimesAreClampedIntoTheVideoTrack()
		{
			var end = TimeSpan.FromSeconds(3);

			await Assert.That(MacVideoSession.ClampToVideo(TimeSpan.FromSeconds(1.5), end)).IsEqualTo(TimeSpan.FromSeconds(1.5));
			await Assert.That(MacVideoSession.ClampToVideo(end - TimeSpan.FromTicks(1), end)).IsEqualTo(end - TimeSpan.FromTicks(1));
			await Assert.That(MacVideoSession.ClampToVideo(end, end)).IsEqualTo(end - TimeSpan.FromTicks(1));
			await Assert.That(MacVideoSession.ClampToVideo(TimeSpan.FromSeconds(3.236), end)).IsEqualTo(end - TimeSpan.FromTicks(1));
			await Assert.That(MacVideoSession.ClampToVideo(TimeSpan.FromSeconds(-1), end)).IsEqualTo(TimeSpan.Zero);

			// A track that does not say how long it is gets no clamp.
			await Assert.That(MacVideoSession.ClampToVideo(TimeSpan.FromSeconds(10), TimeSpan.Zero)).IsEqualTo(TimeSpan.FromSeconds(10));
		}

		/// <summary>
		/// The reader's AVFoundation calls return structs in the arm64 way, which an Intel Mac does not share,
		/// so there the reader says it is unsupported, in words, instead of reading garbage.
		/// </summary>
		[Test]
		public async Task AnIntelMacSaysItCannotReadVideos()
		{
			var intel = new MacVideoFrameReader(Architecture.X64);

			await Assert.That(intel.IsSupported).IsFalse();
			await Assert.That(intel.UnsupportedReason).IsEqualTo(MacVideoFrameReader.IntelReason);

			var infoError = await Assert.That(async () => await intel.GetInfoAsync(FixturePath("solid-colors.mp4"), CancellationToken.None))
				.Throws<VideoFrameReaderException>();
			await Assert.That(infoError.Message).IsEqualTo(MacVideoFrameReader.IntelReason);

			var readError = await Assert.That(async () => await intel.ReadFramesAsync(
				FixturePath("solid-colors.mp4"), new[] { TimeSpan.Zero }, (index, frame) => Task.CompletedTask, CancellationToken.None))
				.Throws<VideoFrameReaderException>();
			await Assert.That(readError.Message).IsEqualTo(MacVideoFrameReader.IntelReason);

			var appleSilicon = new MacVideoFrameReader(Architecture.Arm64);
			await Assert.That(appleSilicon.IsSupported).IsTrue();
			await Assert.That(appleSilicon.UnsupportedReason).IsNull();
		}

		[Test]
		public async Task AMissingFileSaysSoInWords()
		{
			var missing = Path.Combine(Path.GetTempPath(), "no-such-video-" + Guid.NewGuid() + ".mp4");

			var exception = await Assert.That(async () => await Reader.GetInfoAsync(missing, CancellationToken.None))
				.Throws<VideoFrameReaderException>();

			await Assert.That(exception.Message).StartsWith("Could not find the video");
		}

		/// <summary>A file that is not a video - here text named .mp4 - says so, for info and for frames.</summary>
		[Test]
		public async Task AFileThatIsNotAVideoSaysSoInWords()
		{
			var notVideo = Path.Combine(Path.GetTempPath(), "not-a-video-" + Guid.NewGuid() + ".mp4");
			File.WriteAllText(notVideo, "This is a text file, not a video.");
			try
			{
				var expected = $"macOS cannot open '{Path.GetFileName(notVideo)}' as a video. Use an .mp4 or .mov file.";

				var infoError = await Assert.That(async () => await Reader.GetInfoAsync(notVideo, CancellationToken.None))
					.Throws<VideoFrameReaderException>();
				await Assert.That(infoError.Message).IsEqualTo(expected);

				var readError = await Assert.That(async () => await Reader.ReadFramesAsync(
					notVideo, new[] { TimeSpan.Zero }, (index, frame) => Task.CompletedTask, CancellationToken.None))
					.Throws<VideoFrameReaderException>();
				await Assert.That(readError.Message).IsEqualTo(expected);
			}
			finally
			{
				File.Delete(notVideo);
			}
		}

		private static async Task AssertColor(Color actual, int red, int green, int blue, string because)
		{
			await Assert.That((int)actual.red).IsEqualTo(red).Within(ColorTolerance).Because(because + $" (got {actual.red}, {actual.green}, {actual.blue})");
			await Assert.That((int)actual.green).IsEqualTo(green).Within(ColorTolerance).Because(because + $" (got {actual.red}, {actual.green}, {actual.blue})");
			await Assert.That((int)actual.blue).IsEqualTo(blue).Within(ColorTolerance).Because(because + $" (got {actual.red}, {actual.green}, {actual.blue})");
		}

		/// <summary>
		/// The repo tree copy first, as the rest of this project's test data does, then the copy beside the
		/// binary.
		/// </summary>
		private static string FixturePath(string name)
		{
			string probe = Path.GetDirectoryName(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
			for (int up = 0; up < 6 && probe != null; up++)
			{
				var candidate = Path.Combine(probe, "TestData", "Video", name);
				if (File.Exists(candidate))
				{
					return candidate;
				}

				probe = Path.GetDirectoryName(probe);
			}

			var besideBinary = Path.Combine(AppContext.BaseDirectory, "TestData", "Video", name);
			if (File.Exists(besideBinary))
			{
				return besideBinary;
			}

			throw new FileNotFoundException($"Could not find TestData/Video/{name} above " + AppContext.BaseDirectory);
		}
	}
}
