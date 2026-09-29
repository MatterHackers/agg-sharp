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
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace MatterHackers.Agg.Platform
{
	/// <summary>
	/// The mac <see cref="IVideoFrameReader"/>: AVFoundation, which decodes whatever the OS can play (H.264
	/// and HEVC on every supported macOS, in hardware where the Mac has it). <see cref="MacVideoSession"/>
	/// does the decoding; this class only keeps it off the caller's thread and hands frames over one at a time.
	/// </summary>
	public class MacVideoFrameReader : IVideoFrameReader
	{
		/// <summary>
		/// What an Intel Mac says. Its reads would go through objc_msgSend returning CMTime, CMTimeRange and
		/// CGAffineTransform structs, which x86-64 returns through objc_msgSend_stret, not the arm64 way
		/// these declarations are written for - so rather than read garbage, the reader is unsupported there.
		/// </summary>
		public const string IntelReason = "Reading frames from a video needs a Mac with Apple silicon. "
			+ "On this Mac, save the frames you want as photos and use those instead.";

		private readonly bool isAppleSilicon;

		public MacVideoFrameReader()
			: this(RuntimeInformation.ProcessArchitecture)
		{
		}

		/// <summary>A reader as it would be on a Mac running <paramref name="processArchitecture"/>.</summary>
		internal MacVideoFrameReader(Architecture processArchitecture)
		{
			isAppleSilicon = processArchitecture == Architecture.Arm64;
		}

		public bool IsSupported => isAppleSilicon;

		public string UnsupportedReason => isAppleSilicon ? null : IntelReason;

		public Task<VideoInfo> GetInfoAsync(string path, CancellationToken cancellationToken)
		{
			if (!isAppleSilicon)
			{
				return Task.FromException<VideoInfo>(new VideoFrameReaderException(IntelReason));
			}

			return Task.Run(
				() =>
				{
					using var session = MacVideoSession.Open(path);
					return session.Info;
				},
				cancellationToken);
		}

		public async Task ReadFramesAsync(string path, IReadOnlyList<TimeSpan> times, Func<int, ImageBuffer, Task> onFrame, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(times);
			ArgumentNullException.ThrowIfNull(onFrame);
			if (!isAppleSilicon)
			{
				throw new VideoFrameReaderException(IntelReason);
			}

			// Decoding runs on the pool so a UI caller stays responsive, while onFrame runs back on the
			// caller's context. AVAssetImageGenerator's synchronous copy may be called from any thread, one
			// call at a time, which is all this does.
			var session = await Task.Run(() => MacVideoSession.Open(path), cancellationToken);
			try
			{
				for (int i = 0; i < times.Count; i++)
				{
					cancellationToken.ThrowIfCancellationRequested();
					var time = times[i];
					var frame = await Task.Run(() => session.ReadFrameAt(time, cancellationToken), cancellationToken);
					await onFrame(i, frame);
				}
			}
			finally
			{
				session.Dispose();
			}
		}
	}
}
