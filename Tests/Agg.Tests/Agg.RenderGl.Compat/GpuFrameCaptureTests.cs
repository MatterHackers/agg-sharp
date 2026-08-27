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

using System;
using System.IO;
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using MatterHackers.RenderCore;
using MatterHackers.RenderCore.Testing;
using MatterHackers.RenderGl.Compat;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests
{
	/// <summary>
	/// The one screenshot implementation every window host shares. What is checked here is the part that used
	/// to be copied into each platform layer - the guard, the replace-don't-append rule, and that a file which
	/// can actually be decoded comes out the other end.
	/// </summary>
	public class GpuFrameCaptureTests
	{
		[Test]
		public async Task ACaptureIsADecodablePngTheSizeOfTheColourTarget()
		{
			GlCompatTestHarness harness = GlCompatTestHarness.Create(width: 64, height: 32);
			string path = TempCapturePath();

			try
			{
				await GpuFrameCapture.SaveColorTargetAsync(harness.Context, path);

				await Assert.That(File.Exists(path)).IsTrue();

				// Decoded rather than measured on disk: a PNG that is merely non-empty proves nothing, and a
				// host reading a capture back is what a golden comparison does.
				ImageBuffer written = ImageIO.LoadImage(path);
				await Assert.That(written.Width).IsEqualTo(64);
				await Assert.That(written.Height).IsEqualTo(32);
			}
			finally
			{
				File.Delete(path);
			}
		}

		[Test]
		public async Task ACaptureReplacesWhateverWasAtThePath()
		{
			GlCompatTestHarness harness = GlCompatTestHarness.Create(width: 8, height: 4);
			string path = TempCapturePath();

			try
			{
				// ImageIO refuses to overwrite and answers false, so without the delete this capture would
				// leave the stale file in place - a screenshot that looks fresh and is not.
				File.WriteAllText(path, "not a png");

				await GpuFrameCapture.SaveColorTargetAsync(harness.Context, path);

				ImageBuffer written = ImageIO.LoadImage(path);
				await Assert.That(written.Width).IsEqualTo(8);
			}
			finally
			{
				File.Delete(path);
			}
		}

		[Test]
		public async Task ATargetThatCannotBeCopiedFromIsRefusedRatherThanSilentlyBlank()
		{
			var device = new RecordingRenderDevice();
			IGpuTexture target = device.CreateTexture(new TextureDescriptor(
				4,
				4,
				TextureFormat.Bgra8Unorm,
				TextureUsage.RenderAttachment,
				1,
				1,
				"noCopySrc"));

			var context = new GlCompatContext(device);
			context.SetRenderTarget(target, null);

			await Assert.That(async () => await GpuFrameCapture.SaveColorTargetAsync(context, "unreachable.png"))
				.Throws<InvalidOperationException>();
		}

		[Test]
		public async Task AContextWithNothingBoundWritesNothingAndSaysNothing()
		{
			var device = new RecordingRenderDevice();
			var context = new GlCompatContext(device);
			string path = TempCapturePath();

			await GpuFrameCapture.SaveColorTargetAsync(context, path);

			await Assert.That(File.Exists(path)).IsFalse();
		}

		private static string TempCapturePath()
			=> Path.Combine(Path.GetTempPath(), $"gpu-frame-capture-{Guid.NewGuid():N}.png");
	}
}
