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
using MatterHackers.RenderGl.OpenGl;

namespace MatterHackers.RenderGl
{
	/// <summary>
	/// <see cref="Graphics2DGpu"/>'s <see cref="Agg.IChannelMaskGraphics"/>: the channels go to
	/// <see cref="GL.ColorMask"/>, which the compat layer carries into each draw's pipeline as its colour
	/// target's write mask (GlPipelineCache keys pipelines by that state, so each mask gets its own pipeline).
	/// </summary>
	internal static class GpuChannelMask
	{
		public static void Draw(GL gl, Agg.ColorChannels channels, Action draw)
		{
			if (draw == null)
			{
				throw new ArgumentNullException(nameof(draw));
			}

			gl.ColorMask(
				channels.HasFlag(Agg.ColorChannels.Red),
				channels.HasFlag(Agg.ColorChannels.Green),
				channels.HasFlag(Agg.ColorChannels.Blue),
				channels.HasFlag(Agg.ColorChannels.Alpha));
			try
			{
				draw();
			}
			finally
			{
				// Every other draw on this context expects all four channels; a mask left set would miscolor the
				// rest of the frame, and every frame after it (see CompositeLcdBuffer).
				gl.ColorMask(true, true, true, true);
			}
		}
	}
}
