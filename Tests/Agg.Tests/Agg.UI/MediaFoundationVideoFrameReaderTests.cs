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
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// The Windows video reader against a checked-in fixture it must always be able to decode (H.264 ships
	/// with every Windows). TestData/Video/solid-colors.mp4 is 64x48, 5 fps, 3 s, made with Media
	/// Foundation's sink writer from NV12 frames: during second n the top half is red, green then blue, the
	/// bottom half is white, and a 16x24 block at the top left is black. rotated-90.mp4 is the same picture
	/// at 64x40, written with MF_MT_VIDEO_ROTATION 90, which the sink stores as the same track matrix a
	/// Pixel phone writes for portrait video - so it is a sideways stored frame to be shown turned clockwise,
	/// and its 40 rows are padded to 48 by the codec.
	/// </summary>
	public class MediaFoundationVideoFrameReaderTests
	{
		// H.264 is lossy and the decoder's YUV to RGB matrix need not be the encoder's, so "red" is judged
		// with a tolerance rather than as 255, 0, 0 - wide enough for that, far too narrow to mistake one
		// of the fixture's colours for another.
		private const int ColorTolerance = 40;

		private static readonly IVideoFrameReader Reader = new MediaFoundationVideoFrameReader();

		[Test]
		public async Task TheWindowsDefaultIsTheMediaFoundationReader()
		{
			await Assert.That(AggContext.CreateInstanceFrom<IVideoFrameReader>(AggContext.Config.ProviderTypes.VideoFrameReaderProvider))
				.IsTypeOf<MediaFoundationVideoFrameReader>();
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
		/// Times close together are reached by decoding on from the previous frame rather than seeking; that
		/// must give exactly the frame a lone request for the same time gets. Every tenth of a second over the
		/// 5 fps fixture lands two times in most frames, on frame boundaries and past the end.
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

				await Assert.That(together[i].SequenceEqual(alone)).IsTrue().Because($"the frame at {times[i]} read on from the one before matches the frame read on its own");
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

			// Display row r is agg row 63 - r. The corners are checked, so padding left in on any side (the
			// video processor fills it with black) would fail.
			await AssertColor(frame.GetPixel(39, 63), 0, 0, 0, "the stored top-left block is at the top right");
			await AssertColor(frame.GetPixel(39, 63 - 40), 255, 0, 0, "the stored top half is the right half");
			await AssertColor(frame.GetPixel(39, 0), 255, 0, 0, "the stored top half is the right half");
			await AssertColor(frame.GetPixel(0, 63), 255, 255, 255, "the stored bottom half is the left half");
			await AssertColor(frame.GetPixel(0, 0), 255, 255, 255, "the stored bottom half is the left half");
			await AssertColor(frame.GetPixel(19, 32), 255, 255, 255, "the left half is white up to its edge");
			await AssertColor(frame.GetPixel(20, 32), 255, 0, 0, "the right half starts at x = 20");
		}

		[Test]
		public async Task AMissingFileSaysSoInWords()
		{
			var missing = Path.Combine(Path.GetTempPath(), "no-such-video-" + Guid.NewGuid() + ".mp4");

			var exception = await Assert.That(async () => await Reader.GetInfoAsync(missing, CancellationToken.None))
				.Throws<VideoFrameReaderException>();

			await Assert.That(exception.Message).StartsWith("Could not find the video");
		}

		/// <summary>
		/// Phones record HEVC and Windows ships without its decoder, so that is the failure a user will meet;
		/// the message has to name the fix.
		/// </summary>
		[Test]
		public async Task AMissingHevcDecoderNamesTheStoreExtension()
		{
			var hevc = new Guid("43564548-0000-0010-8000-00AA00389B71"); // MFVideoFormat_HEVC

			await Assert.That(MediaFoundationVideoSession.MissingDecoderMessage(hevc))
				.IsEqualTo("This video uses HEVC (H.265), which Windows cannot decode yet. "
					+ "Install 'HEVC Video Extensions' from the Microsoft Store, then try again.");
		}

		/// <summary>
		/// Windows N and KN editions ship without Media Foundation, so mfplat.dll does not load at all. That
		/// has to reach the user as the fix (the Media Feature Pack), not as the loader's text.
		/// </summary>
		[Test]
		public async Task MissingMediaFoundationNamesTheMediaFeaturePack()
		{
			var expected = "Windows is missing its media features, so it cannot read videos. "
				+ "Open Settings, go to Apps > Optional features, add 'Media Feature Pack', restart, then try again.";

			var missingDll = MediaFoundationVideoSession.MissingMediaFoundation(new DllNotFoundException("Unable to load DLL 'mfplat.dll'"));
			await Assert.That(missingDll).IsNotNull();
			await Assert.That(missingDll.Message).IsEqualTo(expected);
			await Assert.That(missingDll.InnerException).IsTypeOf<DllNotFoundException>();

			var missingEntryPoint = MediaFoundationVideoSession.MissingMediaFoundation(new EntryPointNotFoundException("MFStartup"));
			await Assert.That(missingEntryPoint).IsNotNull();
			await Assert.That(missingEntryPoint.Message).IsEqualTo(expected);

			await Assert.That(MediaFoundationVideoSession.MissingMediaFoundation(new InvalidOperationException())).IsNull();
		}

		/// <summary>
		/// Only "no decoder for the native type" (MF_E_TOPO_CODEC_NOT_FOUND) means an extension would help.
		/// Any other refusal to set RGB32 output - MF_E_INVALIDMEDIATYPE, which the source reader returns when
		/// a decoder was found but rejected the type, included - is reported as the error it is.
		/// </summary>
		[Test]
		public async Task OnlyAMissingCodecAsksForAnExtension()
		{
			var hevc = new Guid("43564548-0000-0010-8000-00AA00389B71"); // MFVideoFormat_HEVC
			const int topoCodecNotFound = unchecked((int)0xC00D5212);
			const int invalidMediaType = unchecked((int)0xC00D36B4);

			await Assert.That(MediaFoundationVideoSession.SetOutputTypeFailureMessage(topoCodecNotFound, hevc, "clip.mp4"))
				.IsEqualTo(MediaFoundationVideoSession.MissingDecoderMessage(hevc));
			await Assert.That(MediaFoundationVideoSession.SetOutputTypeFailureMessage(invalidMediaType, hevc, "clip.mp4"))
				.IsEqualTo("Windows could not read the video 'clip.mp4' (Media Foundation error 0xC00D36B4).");
			await Assert.That(MediaFoundationVideoSession.SetOutputTypeFailureMessage(unchecked((int)0x80004005), hevc, "clip.mp4"))
				.IsEqualTo("Windows could not read the video 'clip.mp4' (Media Foundation error 0x80004005).");
		}

		/// <summary>
		/// A plain (non-2D) buffer is laid out by the output type's MF_MT_DEFAULT_STRIDE. Without one, Media
		/// Foundation's rule for uncompressed RGB is the bitmap convention: bottom-up, a negative stride.
		/// </summary>
		[Test]
		public async Task AnRgb32FrameWithoutAStrideIsBottomUp()
		{
			const int attributeNotFound = unchecked((int)0xC00D36E6); // MF_E_ATTRIBUTENOTFOUND

			await Assert.That(MediaFoundationVideoSession.ResolveDefaultStride(attributeNotFound, 0, 64)).IsEqualTo(-256);
			await Assert.That(MediaFoundationVideoSession.ResolveDefaultStride(0, 256, 64)).IsEqualTo(256);
			await Assert.That(MediaFoundationVideoSession.ResolveDefaultStride(0, unchecked((uint)-256), 64)).IsEqualTo(-256);
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

			throw new FileNotFoundException($"Could not find TestData\\Video\\{name} above " + AppContext.BaseDirectory);
		}
	}
}
