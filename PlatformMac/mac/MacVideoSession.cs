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

using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform.Mac;
using System;
using System.IO;
using System.Threading;
using static MatterHackers.Agg.Platform.Mac.AVFoundationInterop;

namespace MatterHackers.Agg.Platform
{
	/// <summary>
	/// One open video on the mac: an AVURLAsset and an AVAssetImageGenerator on it, set to hand back the
	/// exact frame on screen at a time, upright.
	/// </summary>
	/// <remarks>
	/// Why the image generator and not AVAssetReader: the contract is random access - times in any order,
	/// backwards included - and the generator is AVFoundation's random-access API. With both tolerances
	/// zero it returns the frame whose display interval contains the time; with
	/// appliesPreferredTrackTransform it turns the frame by the track's matrix (how phones store portrait)
	/// and crops the codec's padding, which on Windows took a page of aperture arithmetic. AVAssetReader
	/// only reads forward from the start of a time range, so every backward or distant time would mean a
	/// new reader and hand-rolled seeking. The generator decodes from the key frame before each time, but
	/// with VideoToolbox's hardware decoder that is fast: 40 evenly spaced frames of an 8.6 s 1080p HEVC
	/// Pixel clip took 1.2-1.5 s on an Apple silicon Mac, so the Windows reader's read-on window (which
	/// took the same 40 frames from 119 s to 21 s there) would buy nothing here.
	/// </remarks>
	internal sealed class MacVideoSession : IDisposable
	{
		private static readonly IntPtr SelFileUrlWithPath = ObjC.Sel("fileURLWithPath:");
		private static readonly IntPtr SelUrlAssetWithUrl = ObjC.Sel("URLAssetWithURL:options:");
		private static readonly IntPtr SelIsReadable = ObjC.Sel("isReadable");
		private static readonly IntPtr SelTracksWithMediaType = ObjC.Sel("tracksWithMediaType:");
		private static readonly IntPtr SelCount = ObjC.Sel("count");
		private static readonly IntPtr SelObjectAtIndex = ObjC.Sel("objectAtIndex:");
		private static readonly IntPtr SelDuration = ObjC.Sel("duration");
		private static readonly IntPtr SelTimeRange = ObjC.Sel("timeRange");
		private static readonly IntPtr SelNaturalSize = ObjC.Sel("naturalSize");
		private static readonly IntPtr SelPreferredTransform = ObjC.Sel("preferredTransform");
		private static readonly IntPtr SelNominalFrameRate = ObjC.Sel("nominalFrameRate");
		private static readonly IntPtr SelInitWithAsset = ObjC.Sel("initWithAsset:");
		private static readonly IntPtr SelSetAppliesPreferredTrackTransform = ObjC.Sel("setAppliesPreferredTrackTransform:");
		private static readonly IntPtr SelSetToleranceBefore = ObjC.Sel("setRequestedTimeToleranceBefore:");
		private static readonly IntPtr SelSetToleranceAfter = ObjC.Sel("setRequestedTimeToleranceAfter:");
		private static readonly IntPtr SelCopyCGImage = ObjC.Sel("copyCGImageAtTime:actualTime:error:");
		private static readonly IntPtr SelLocalizedDescription = ObjC.Sel("localizedDescription");

		private readonly string fileName;
		private IntPtr asset;
		private IntPtr generator;

		// Where the video track's last frame stops being on screen. The asset's duration is the longest
		// track's, and a phone clip's audio often runs 20-40 ms past its last frame; the generator has no
		// image for a time in that tail, so reads are clamped to this rather than to Info.Duration.
		private TimeSpan videoEnd;

		private MacVideoSession(string path)
		{
			fileName = Path.GetFileName(path);
		}

		public VideoInfo Info { get; private set; }

		/// <summary>
		/// Opens <paramref name="path"/>, or throws <see cref="VideoFrameReaderException"/> with a message the
		/// user can act on.
		/// </summary>
		public static MacVideoSession Open(string path)
		{
			ArgumentNullException.ThrowIfNull(path);
			var session = new MacVideoSession(path);
			try
			{
				session.OpenAsset(Path.GetFullPath(path));
				return session;
			}
			catch
			{
				session.Dispose();
				throw;
			}
		}

		private void OpenAsset(string fullPath)
		{
			// Checked up front because AVFoundation's answer for a missing file is an unreadable asset,
			// indistinguishable from a file that is not a video.
			if (!File.Exists(fullPath))
			{
				throw new VideoFrameReaderException($"Could not find the video '{fullPath}'. Check that the file still exists.");
			}

			EnsureLoaded();
			IntPtr pool = objc_autoreleasePoolPush();
			try
			{
				IntPtr url = ObjC.Send_r_r(ObjC.Class("NSURL"), SelFileUrlWithPath, ObjC.NSString(fullPath));
				asset = ObjC.Retain(Send_r_r_r(ObjC.Class("AVURLAsset"), SelUrlAssetWithUrl, url, IntPtr.Zero));

				// These synchronous property reads block while the file is parsed, which is fine off the UI
				// thread (the reader never calls this on it).
				if (asset == IntPtr.Zero || ObjC.Send_B(asset, SelIsReadable) == ObjC.NO)
				{
					throw new VideoFrameReaderException($"macOS cannot open '{fileName}' as a video. Use an .mp4 or .mov file.");
				}

				IntPtr tracks = ObjC.Send_r_r(asset, SelTracksWithMediaType, ObjC.NSString("vide")); // AVMediaTypeVideo
				if (tracks == IntPtr.Zero || ObjC.Send_Q(tracks, SelCount) == 0)
				{
					throw new VideoFrameReaderException($"'{fileName}' has no video in it.");
				}

				IntPtr track = ObjC.Send_r_Q(tracks, SelObjectAtIndex, 0);
				CGSize stored = ObjC.Send_S(track, SelNaturalSize);
				CGAffineTransform transform = Send_A(track, SelPreferredTransform);
				double framesPerSecond = ObjC.Send_f(track, SelNominalFrameRate);
				CMTimeRange videoRange = Send_TR(track, SelTimeRange);
				videoEnd = videoRange.Start.ToTimeSpan() + videoRange.Duration.ToTimeSpan();

				// The file's length, as Windows reports it (Media Foundation's presentation duration), even
				// where the audio outlasts the video: it is what a player shows.
				TimeSpan duration = Send_T(asset, SelDuration).ToTimeSpan();

				// The display size is the stored size through the track matrix; its translation only moves
				// the picture back into positive coordinates, so the linear part is all that matters.
				int width = (int)Math.Round(Math.Abs((transform.A * stored.Width) + (transform.C * stored.Height)));
				int height = (int)Math.Round(Math.Abs((transform.B * stored.Width) + (transform.D * stored.Height)));
				Info = new VideoInfo(duration, width, height, RotationOf(transform), framesPerSecond);

				generator = ObjC.Send_r_r(ObjC.Alloc(ObjC.Class("AVAssetImageGenerator")), SelInitWithAsset, asset);
				ObjC.Send_v_B(generator, SelSetAppliesPreferredTrackTransform, ObjC.YES);
				Send_v_T(generator, SelSetToleranceBefore, CMTime.Zero);
				Send_v_T(generator, SelSetToleranceAfter, CMTime.Zero);
			}
			finally
			{
				objc_autoreleasePoolPop(pool);
			}
		}

		/// <summary>
		/// Degrees clockwise the track matrix turns the stored frame for display: 0, 90, 180 or 270. In
		/// AVFoundation's y-down frame the matrix for a Pixel's portrait video is (a, b, c, d) = (0, 1, -1, 0),
		/// which is a quarter turn clockwise. A matrix that is not a whole quarter turn (a skew, a mirror at
		/// an odd angle) reports the nearest one; the generator applies the real matrix either way.
		/// </summary>
		internal static int RotationOf(CGAffineTransform transform)
		{
			double degrees = Math.Atan2(transform.B, transform.A) * 180 / Math.PI;
			int quarterTurns = (int)Math.Round(degrees / 90);
			return ((quarterTurns % 4) + 4) % 4 * 90;
		}

		/// <summary>
		/// Decodes the frame on screen at <paramref name="time"/>: the one whose display interval contains
		/// it, or the video's last frame for a time past the end.
		/// </summary>
		public ImageBuffer ReadFrameAt(TimeSpan time, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			TimeSpan target = ClampToVideo(time, videoEnd);

			IntPtr pool = objc_autoreleasePoolPush();
			try
			{
				IntPtr image = Send_CopyCGImage(generator, SelCopyCGImage, CMTime.FromTimeSpan(target), out _, out IntPtr error);
				if (image == IntPtr.Zero)
				{
					string reason = error == IntPtr.Zero ? null : ObjC.FromNSString(ObjC.Send_r(error, SelLocalizedDescription));
					throw new VideoFrameReaderException(
						$"macOS could not read a frame at {time} from '{fileName}'" + (string.IsNullOrEmpty(reason) ? "." : $" ({reason})."));
				}

				try
				{
					return CopyPixels(image);
				}
				finally
				{
					CGImageRelease(image);
				}
			}
			finally
			{
				objc_autoreleasePoolPop(pool);
			}
		}

		/// <summary>
		/// The time to ask the generator for, for a requested <paramref name="time"/>: at or past
		/// <paramref name="videoEnd"/> the generator has no frame to give, and just inside it (one tick, 100 ns)
		/// the last frame is on screen. A negative time is 0; a <paramref name="videoEnd"/> of zero (a track
		/// that does not say how long it is) clamps nothing.
		/// </summary>
		internal static TimeSpan ClampToVideo(TimeSpan time, TimeSpan videoEnd)
		{
			if (time < TimeSpan.Zero)
			{
				return TimeSpan.Zero;
			}

			return videoEnd > TimeSpan.Zero && time >= videoEnd ? videoEnd - TimeSpan.FromTicks(1) : time;
		}

		/// <summary>
		/// Draws <paramref name="image"/> into a BGRX bitmap and copies it into a new agg image with the rows
		/// flipped: a bitmap context's memory holds the top row first, agg's buffer the bottom row first.
		/// </summary>
		private unsafe ImageBuffer CopyPixels(IntPtr image)
		{
			int width = (int)CGImageGetWidth(image);
			int height = (int)CGImageGetHeight(image);
			int stride = width * 4;
			var pixels = new byte[stride * height];

			fixed (byte* data = pixels)
			{
				// Drawing in the frame's own colour space (a video's is usually BT.709) leaves its values as
				// decoded, as Windows' reader does; converting to the display's would tint every frame. Some
				// spaces cannot back a bitmap context, and those fall back to device RGB.
				IntPtr context = CGBitmapContextCreate((IntPtr)data, (nuint)width, (nuint)height, 8, (nuint)stride, CGImageGetColorSpace(image), BitmapInfoBgrx);
				if (context == IntPtr.Zero)
				{
					IntPtr deviceRgb = CGColorSpaceCreateDeviceRGB();
					context = CGBitmapContextCreate((IntPtr)data, (nuint)width, (nuint)height, 8, (nuint)stride, deviceRgb, BitmapInfoBgrx);
					CGColorSpaceRelease(deviceRgb);
				}

				if (context == IntPtr.Zero)
				{
					throw new VideoFrameReaderException($"macOS could not convert a frame of '{fileName}' ({width}x{height}).");
				}

				try
				{
					CGContextSetBlendMode(context, BlendModeCopy);
					CGContextDrawImage(context, new CGRect(0, 0, width, height), image);
				}
				finally
				{
					CGContextRelease(context);
				}
			}

			var frame = new ImageBuffer(width, height);
			byte[] destination = frame.GetBuffer();
			for (int row = 0; row < height; row++)
			{
				// agg's y runs up from the bottom, so the top display row is agg row height - 1.
				int output = frame.GetBufferOffsetXY(0, height - 1 - row);
				int input = row * stride;
				for (int x = 0; x < width; x++)
				{
					destination[output] = pixels[input];
					destination[output + 1] = pixels[input + 1];
					destination[output + 2] = pixels[input + 2];
					destination[output + 3] = 255;
					output += 4;
					input += 4;
				}
			}

			return frame;
		}

		public void Dispose()
		{
			// Releasing the last reference can autorelease internal objects (the generator's decoder
			// session, the asset's track list), and this runs on a pool thread with no pool of its own.
			IntPtr pool = objc_autoreleasePoolPush();
			try
			{
				ObjC.Release(generator);
				generator = IntPtr.Zero;
				ObjC.Release(asset);
				asset = IntPtr.Zero;
			}
			finally
			{
				objc_autoreleasePoolPop(pool);
			}
		}
	}
}
