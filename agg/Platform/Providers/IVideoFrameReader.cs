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

using MatterHackers.Agg.Image;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MatterHackers.Agg.Platform
{
	/// <summary>
	/// Reads still frames out of a video file, through whatever decoder the OS has. Which frames to take is
	/// the caller's policy; this only answers "what is in this file" and "what is on screen at time t".
	/// </summary>
	/// <remarks>
	/// Frames are handed over one at a time rather than returned as a list: a 4K frame is 33 MB, so a caller
	/// sampling a long video must be able to save or shrink each one and let it go before the next arrives.
	/// </remarks>
	public interface IVideoFrameReader
	{
		/// <summary>
		/// False when this platform has no video decoding at all; <see cref="UnsupportedReason"/> then says
		/// so in words a user can act on. True does not promise every file decodes - a missing codec is
		/// reported per file, by a <see cref="VideoFrameReaderException"/>.
		/// </summary>
		bool IsSupported { get; }

		/// <summary>
		/// Why <see cref="IsSupported"/> is false, written for the user; null when it is true.
		/// </summary>
		string UnsupportedReason { get; }

		/// <summary>
		/// Reads the video's length and frame size.
		/// </summary>
		/// <exception cref="VideoFrameReaderException">The file is missing, is not a video, or needs a decoder
		/// this machine does not have. The message is written for the user.</exception>
		Task<VideoInfo> GetInfoAsync(string path, CancellationToken cancellationToken);

		/// <summary>
		/// Decodes the frame on screen at each of <paramref name="times"/> - the one whose display interval
		/// contains it, or the last frame for a time past the end - and hands it to <paramref name="onFrame"/>
		/// with its index in <paramref name="times"/>, in list order, awaiting each call before decoding the
		/// next. Each frame is a new 32-bit BGRA <see cref="ImageBuffer"/> in display orientation (see
		/// <see cref="VideoInfo.Rotation"/>) with agg's row order, exactly as <c>ImageIO.LoadImage</c> would
		/// produce from a photo of it; the callee owns it.
		/// </summary>
		/// <exception cref="VideoFrameReaderException">As for <see cref="GetInfoAsync"/>.</exception>
		Task ReadFramesAsync(string path, IReadOnlyList<TimeSpan> times, Func<int, ImageBuffer, Task> onFrame, CancellationToken cancellationToken);
	}

	/// <summary>
	/// What <see cref="IVideoFrameReader.GetInfoAsync"/> learned about a video.
	/// </summary>
	/// <param name="Duration">The length of the video.</param>
	/// <param name="Width">Frame width as displayed - already swapped with the height for a 90 or 270 degree
	/// <paramref name="Rotation"/>, so it matches the frames <see cref="IVideoFrameReader.ReadFramesAsync"/>
	/// delivers.</param>
	/// <param name="Height">Frame height as displayed; see <paramref name="Width"/>.</param>
	/// <param name="Rotation">Degrees clockwise (0, 90, 180 or 270) the stored frames are turned to display
	/// upright. Phones record sideways and set this rather than re-encoding; the reader has already applied
	/// it, so this is informational.</param>
	/// <param name="FramesPerSecond">The nominal frame rate, or 0 when the file does not say.</param>
	public sealed record VideoInfo(TimeSpan Duration, int Width, int Height, int Rotation, double FramesPerSecond);

	/// <summary>
	/// A video could not be read. <see cref="Exception.Message"/> is written for the user and says what to do
	/// (for example which Store extension installs a missing decoder), so a caller can show it as is.
	/// </summary>
	public class VideoFrameReaderException : Exception
	{
		public VideoFrameReaderException(string message)
			: base(message)
		{
		}

		public VideoFrameReaderException(string message, Exception innerException)
			: base(message, innerException)
		{
		}
	}

	/// <summary>
	/// The reader for a platform with no video decoding: <see cref="IsSupported"/> is false and every read
	/// throws <see cref="VideoFrameReaderException"/> with <see cref="UnsupportedReason"/>, so a caller that
	/// skipped the check still shows the user something they can act on.
	/// </summary>
	public class UnsupportedVideoFrameReader : IVideoFrameReader
	{
		public const string DefaultReason = "Reading frames from a video is not available on this platform yet. "
			+ "Save the frames you want as photos and use those instead.";

		public bool IsSupported => false;

		public string UnsupportedReason => DefaultReason;

		public Task<VideoInfo> GetInfoAsync(string path, CancellationToken cancellationToken)
		{
			return Task.FromException<VideoInfo>(new VideoFrameReaderException(DefaultReason));
		}

		public Task ReadFramesAsync(string path, IReadOnlyList<TimeSpan> times, Func<int, ImageBuffer, Task> onFrame, CancellationToken cancellationToken)
		{
			return Task.FromException(new VideoFrameReaderException(DefaultReason));
		}
	}
}
