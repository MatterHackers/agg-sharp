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

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// How a double-buffered widget stores its backbuffer pixels, and with it how the framework composites
	/// that buffer back onto the parent surface.
	/// </summary>
	/// <remarks>
	/// Ported from the agg-gui Rust reference's <c>BackbufferMode</c> (<c>widget\backbuffer.rs</c>). The
	/// choice is re-made on every paint (see <c>GuiWidget.ResolveBackbufferMode</c>) rather than stored as a
	/// widget setting, so turning LCD rendering on or off takes effect on the next frame - the reference does
	/// the same by dispatching on its global flag inside <c>backbuffer_mode()</c>
	/// (<c>widgets\label.rs:429-440</c>).
	/// </remarks>
	public enum BackbufferMode
	{
		/// <summary>
		/// A single-alpha premultiplied BGRA <see cref="Image.ImageBuffer"/>, composited with ordinary
		/// source-over. Correct for any widget, including ones that are transparent in places. Text inside is
		/// grayscale anti-aliased: one alpha per pixel cannot carry three channel coverages, and a transparent
		/// backbuffer is not a valid destination for subpixel geometry in any case.
		/// </summary>
		Rgba,

		/// <summary>
		/// A two-plane <see cref="Agg.LcdCoverage.LcdBuffer"/> - premultiplied per-channel colour plus
		/// per-channel alpha - which every fill inside the buffer reaches through the LCD pipeline, and which
		/// composites onto the parent per channel so the subpixel geometry survives the round trip.
		/// </summary>
		/// <remarks>
		/// Only chosen when the widget paints opaque content across its whole bounds, which is what makes the
		/// subpixel geometry meaningful, and only when the destination can take the two planes without
		/// flattening them.
		/// </remarks>
		LcdCoverage
	}
}
