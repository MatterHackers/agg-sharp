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

namespace MatterHackers.Agg
{
	/// <summary>
	/// A retained offscreen picture a surface can paint into once and then draw again, frame after frame,
	/// without repainting - the surface-owned half of a GPU widget backbuffer (agg-gui's
	/// <c>BackbufferKind::GlFbo</c>).
	/// </summary>
	/// <remarks>
	/// Made by <see cref="Graphics2D.CreateRetainedLayer()"/> and drawn by
	/// <see cref="Graphics2D.RenderRetainedLayer"/>. A layer belongs to the surface family that made it (for
	/// the GPU path, one device context), and <see cref="BelongsTo"/> says whether a given surface can draw
	/// it, so a caller that keeps a layer across frames can tell when it has to make a new one.
	/// </remarks>
	public interface IRetainedLayer : IDisposable
	{
		/// <summary>Width of the last paint in the destination's logical pixels, 0 before the first.</summary>
		int Width { get; }

		/// <summary>Height of the last paint in the destination's logical pixels, 0 before the first.</summary>
		int Height { get; }

		/// <summary>How many times the layer has been painted (<see cref="Begin"/> calls).</summary>
		int DrawCount { get; }

		/// <summary>
		/// Clears the layer to transparent at the given size and returns the paint: its
		/// <see cref="IRetainedLayerPaint.Graphics"/> draws into the layer in the destination's logical
		/// pixels, origin at the bottom left, and disposing it ends the paint. Use it in a <c>using</c> so a
		/// paint that throws still gives the frame its surface back.
		/// </summary>
		/// <param name="width">Width in the destination's logical pixels.</param>
		/// <param name="height">Height in the destination's logical pixels.</param>
		IRetainedLayerPaint Begin(int width, int height);

		/// <summary>True when <paramref name="destination"/> can draw this layer through
		/// <see cref="Graphics2D.RenderRetainedLayer"/>.</summary>
		bool BelongsTo(Graphics2D destination);

		/// <summary>
		/// True when the retained picture no longer stands for what painting onto
		/// <paramref name="destination"/> would give - never painted, another device, or painted at another
		/// resolution than the destination draws at now - so a caller keeping it across frames must repaint.
		/// </summary>
		bool NeedsRepaintFor(Graphics2D destination);
	}

	/// <summary>One paint of an <see cref="IRetainedLayer"/>, from <see cref="IRetainedLayer.Begin"/> to disposal.</summary>
	public interface IRetainedLayerPaint : IDisposable
	{
		/// <summary>Draws into the layer.</summary>
		Graphics2D Graphics { get; }
	}
}
