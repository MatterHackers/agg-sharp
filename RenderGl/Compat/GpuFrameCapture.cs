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

namespace MatterHackers.RenderGl.Compat
{
	/// <summary>
	/// Reads the frame a window is drawing back into a PNG. The one implementation behind every host's
	/// <c>SaveCurrentFrameAsync</c> - mac, X11, Win32 and the browser.
	/// </summary>
	/// <remarks>
	/// This was four byte-identical copies, one per platform layer, until the browser needed a fifth. The
	/// pixels a capture produces are the pixels a golden image is compared against, so "the same flip and the
	/// same encoder everywhere" is a correctness property, not tidiness: a platform that drifted here would
	/// fail goldens for a reason nobody would look for in a window host.
	/// </remarks>
	public static class GpuFrameCapture
	{
		/// <summary>
		/// Reads the colour target currently bound on <paramref name="compat"/> back into a PNG at
		/// <paramref name="path"/>. Must be called after the widget draw and before the frame is presented -
		/// once presented, the frame's texture belongs to the swapchain again.
		/// </summary>
		/// <remarks>
		/// <para><b>Everything that touches the frame texture happens before the first await, and it has to
		/// stay that way.</b> On the desktop the readback completes before its ValueTask is returned, so the
		/// split is invisible; in the browser the map resolves on a later microtask, by which time the
		/// animation frame task that owned the surface texture has ended and the canvas has been presented.
		/// The browser host relies on being able to call this <i>without</i> awaiting it - the copy records
		/// in-frame, the rest finishes afterwards - so an await added above <see cref="IRenderDevice.ReadTextureAsync"/>
		/// (an async file probe, say) would break that host and nothing else. See <c>ReadTextureAsync</c>'s
		/// "record now, wait later" remarks.</para>
		/// <para>A context with nothing bound returns quietly rather than throwing: a host that asks for a
		/// capture outside a frame gets no file, which is the same answer it got before this was shared code.</para>
		/// </remarks>
		/// <param name="compat">The context whose bound colour target is the frame.</param>
		/// <param name="path">File to write; an existing file is replaced.</param>
		/// <exception cref="InvalidOperationException">
		/// The swapchain's textures cannot be copied from, or the PNG could not be encoded.
		/// </exception>
		public static async Task SaveColorTargetAsync(GlCompatContext compat, string path)
		{
			if (compat == null || compat.Passes.ColorTarget == null)
			{
				return;
			}

			IGpuTexture target = compat.Passes.ColorTarget;
			if ((target.Descriptor.Usage & TextureUsage.CopySrc) == 0)
			{
				throw new InvalidOperationException(
					"This swapchain's textures were not created with CopySrc, so the window cannot be read back.");
			}

			// The pass has to be closed before a copy can be recorded; ReadTextureAsync submits the rest.
			compat.Submit();

			int width = (int)target.Descriptor.Width;
			int height = (int)target.Descriptor.Height;
			uint rowStride = TextureFormatInfo.AlignedRowStride(target.Descriptor.Format, (uint)width);
			var bytes = new byte[rowStride * (long)height];
			TextureReadResult read = await compat.Device.ReadTextureAsync(target, bytes);

			var image = new ImageBuffer(width, height, 32, new BlenderBGRA());
			byte[] buffer = image.GetBuffer();

			// wgpu rows run top down and agg's run bottom up.
			for (int y = 0; y < height; y++)
			{
				long sourceOffset = (height - 1 - y) * (long)read.RowStride;
				Array.Copy(bytes, sourceOffset, buffer, image.GetBufferOffsetY(y), width * 4);
			}

			image.MarkImageChanged();

			// ImageIO.SaveImageData will not overwrite, and a stale screenshot that looks fresh is worse
			// than no screenshot.
			if (File.Exists(path))
			{
				File.Delete(path);
			}

			if (!ImageIO.SaveImageData(path, image))
			{
				// ImageIO swallows encoder failures and answers false. A caller awaiting a capture was
				// promised the file exists once the task completes, so the quiet false has to become loud
				// here or that promise is a lie - and "the screenshot is blank" is not a debuggable report.
				throw new InvalidOperationException($"The captured frame could not be encoded to '{path}'.");
			}
		}
	}
}
