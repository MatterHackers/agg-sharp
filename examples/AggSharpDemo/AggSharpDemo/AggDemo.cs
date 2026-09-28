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
using MatterHackers.Agg;
using MatterHackers.Agg.UI;

namespace MatterHackers.AggSharpDemo
{
	/// <summary>
	/// One port of a C++ AGG 2.4 example. The contract is shaped so that a port can be held to the C++
	/// original byte for byte: the demo draws its whole frame - background, content and the in-canvas AGG
	/// controls C++ draws with its ctrl classes - through <see cref="Graphics2D"/> at its native size, in
	/// C++ AGG's own coordinate system (origin bottom-left, y up, one unit per demo pixel).
	/// </summary>
	/// <remarks>
	/// <para>Drawing through <see cref="Graphics2D"/> rather than into an image is what lets the same
	/// <see cref="Draw"/> run on both paths <see cref="AggDemoView"/> offers: straight onto the window's GPU
	/// renderer (the default, how the site is meant to be seen), and into a demo-sized
	/// <see cref="Image.ImageBuffer"/> through the software rasterizer (the AGG reference, and what the
	/// byte-exact tests against C++ goldens render).</para>
	/// <para>A demo must not assume anything about the graphics it is handed beyond that: no clip it did not
	/// set, no pixel access, and any transform it pushes it pops. It paints its own background by filling
	/// (0, 0, Width, Height) rather than calling <c>Clear</c>: what <c>Clear</c> covers is the clip rect,
	/// and on the GPU path that is a screen-space rect the GPU renderer then runs through the transform a
	/// second time, so its extent differs between the two paths.</para>
	/// <para>C++ examples built with <c>flip_y = false</c> (aa_test, flash_rasterizer, flash_rasterizer2,
	/// idea) work y down. Their ports say so with <see cref="DrawsYDown"/> and draw exactly as C++ does; the
	/// view does the flipping. A port must not flip its own geometry: the rasterizer is not
	/// mirror-symmetric (its cell splits round toward -infinity), so mirrored geometry lands edges a few
	/// levels off C++'s.</para>
	/// </remarks>
	public abstract class AggDemo
	{
		/// <summary>Raised when the demo's frame has changed and must be drawn again.</summary>
		public event EventHandler Invalidated;

		private bool waitMode = true;

		/// <summary>The name shown in the sidebar - the C++ example's file name, as agg-rust uses.</summary>
		public abstract string Name { get; }

		/// <summary>The sidebar group this demo is listed under.</summary>
		public abstract string Category { get; }

		/// <summary>One or two sentences on what the demo shows.</summary>
		public abstract string Description { get; }

		/// <summary>The C++ example's window width, in demo pixels. Goldens are rendered at this size.</summary>
		public abstract int Width { get; }

		/// <summary>The C++ example's window height, in demo pixels.</summary>
		public abstract int Height { get; }

		/// <summary>
		/// True for a port of a C++ <c>flip_y = false</c> example: it draws, and gets mouse positions, y down,
		/// (0, 0) its top-left pixel corner, as C++ does. The reference frame is then rendered y down (row y is
		/// C++'s row y, as the C++ reference renderer writes it) and shown flipped a whole row at a time; on
		/// the GPU path the view flips the transform instead.
		/// </summary>
		public virtual bool DrawsYDown => false;

		/// <summary>
		/// Draws the whole frame. <paramref name="graphics"/> is transformed so that (0, 0) is the demo's
		/// bottom-left pixel corner and (<see cref="Width"/>, <see cref="Height"/>) its top-right - top-left
		/// and bottom-right for a demo that <see cref="DrawsYDown"/>.
		/// </summary>
		public abstract void Draw(Graphics2D graphics);

		/// <summary>
		/// False asks for continuous frames, as C++ <c>wait_mode(false)</c> does: the view then calls
		/// <see cref="OnIdle"/> once per frame. Setting it false invalidates, which is what starts the ticks.
		/// </summary>
		public bool WaitMode
		{
			get => this.waitMode;
			protected set
			{
				if (this.waitMode != value)
				{
					this.waitMode = value;
					if (!value)
					{
						this.Invalidate();
					}
				}
			}
		}

		/// <summary>One animation step while <see cref="WaitMode"/> is false, like C++ <c>on_idle</c>. Call
		/// <see cref="Invalidate"/> for the step to be drawn.</summary>
		public virtual void OnIdle()
		{
		}

		/// <summary>
		/// <paramref name="button"/> went down at (<paramref name="x"/>, <paramref name="y"/>), a whole demo
		/// pixel (y up, or y down for a demo that <see cref="DrawsYDown"/>), as C++ <c>on_mouse_button_down</c>. <paramref name="flags"/> holds every button now
		/// down (this one included) and the modifiers. Only presses inside the demo arrive.
		/// </summary>
		/// <remarks>The button is passed on its own because routing depends on it: C++ platform_support offers
		/// only a left press to its controls, whatever else is held.</remarks>
		public virtual void OnMouseDown(int x, int y, AggInputFlags button, AggInputFlags flags)
		{
		}

		/// <summary>The pointer moved; <paramref name="flags"/> says which buttons are held. Coordinates may
		/// fall outside the demo while a drag that began inside it continues.</summary>
		public virtual void OnMouseMove(int x, int y, AggInputFlags flags)
		{
		}

		/// <summary><paramref name="button"/> came up; <paramref name="flags"/> holds the buttons still down
		/// and the modifiers. Sent only for presses that began inside the demo.</summary>
		public virtual void OnMouseUp(int x, int y, AggInputFlags button, AggInputFlags flags)
		{
		}

		/// <summary>A key went down while the demo's view had focus, as C++ <c>on_key</c>.</summary>
		public virtual void OnKeyDown(Keys key, AggInputFlags flags)
		{
		}

		/// <summary>Tells whoever shows this demo that its frame must be drawn again.</summary>
		protected void Invalidate()
		{
			this.Invalidated?.Invoke(this, EventArgs.Empty);
		}
	}
}
