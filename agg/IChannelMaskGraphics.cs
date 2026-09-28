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

namespace MatterHackers.Agg
{
	/// <summary>The color channels of a pixel, as flags.</summary>
	[Flags]
	public enum ColorChannels
	{
		None = 0,
		Red = 1,
		Green = 2,
		Blue = 4,
		Alpha = 8,
		All = Red | Green | Blue | Alpha,
	}

	/// <summary>
	/// A <see cref="Graphics2D"/> that can draw into only some of its target's channels - the GPU's color write
	/// mask. Drawing black at alpha a into just the red channel moves red towards 0 by a and leaves green, blue
	/// and alpha as they were, which is what C++ AGG does by drawing through a gray pixel format that views one
	/// channel of an RGB buffer (component_rendering.cpp). A software surface does that with a channel view
	/// (<c>ImageBuffer.Attach</c> at the channel's offset) instead.
	/// </summary>
	public interface IChannelMaskGraphics
	{
		/// <summary>
		/// Runs <paramref name="draw"/> with only <paramref name="channels"/> written, then writes every
		/// channel again - also when <paramref name="draw"/> throws.
		/// </summary>
		void DrawWithChannelMask(ColorChannels channels, Action draw);
	}
}
