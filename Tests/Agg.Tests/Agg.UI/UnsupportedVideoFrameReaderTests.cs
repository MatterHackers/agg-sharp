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
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg.Platform;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// What a platform with no video reader answers. <see cref="AggContext.VideoFrames"/> falls back to it
	/// rather than to null, so a caller always has a message to show.
	/// </summary>
	public class UnsupportedVideoFrameReaderTests
	{
		[Test]
		public async Task ItSaysItIsUnsupportedAndWhatToDoInstead()
		{
			var reader = new UnsupportedVideoFrameReader();

			await Assert.That(reader.IsSupported).IsFalse();
			await Assert.That(reader.UnsupportedReason).IsEqualTo(
				"Reading frames from a video is not available on this platform yet. "
				+ "Save the frames you want as photos and use those instead.");
		}

		/// <summary>A caller that skipped the IsSupported check still gets the same readable message.</summary>
		[Test]
		public async Task ReadingThrowsTheSameMessage()
		{
			var reader = new UnsupportedVideoFrameReader();

			var infoError = await Assert.That(async () => await reader.GetInfoAsync("video.mp4", CancellationToken.None))
				.Throws<VideoFrameReaderException>();
			await Assert.That(infoError.Message).IsEqualTo(reader.UnsupportedReason);

			var readError = await Assert.That(async () => await reader.ReadFramesAsync(
				"video.mp4", new[] { TimeSpan.Zero }, (index, frame) => Task.CompletedTask, CancellationToken.None))
				.Throws<VideoFrameReaderException>();
			await Assert.That(readError.Message).IsEqualTo(reader.UnsupportedReason);
		}

		/// <summary>A provider name that does not resolve (a platform whose reader is not built yet) is the
		/// unsupported reader, not null.</summary>
		[Test]
		public async Task AnUnresolvableProviderFallsBackToUnsupported()
		{
			var saved = AggContext.Config.ProviderTypes.VideoFrameReaderProvider;
			var savedReader = AggContext.VideoFrames;
			try
			{
				AggContext.Config.ProviderTypes.VideoFrameReaderProvider = "No.Such.Reader, no_such_assembly";
				AggContext.VideoFrames = null;

				await Assert.That(AggContext.VideoFrames).IsTypeOf<UnsupportedVideoFrameReader>();
			}
			finally
			{
				AggContext.Config.ProviderTypes.VideoFrameReaderProvider = saved;
				AggContext.VideoFrames = savedReader;
			}
		}
	}
}
