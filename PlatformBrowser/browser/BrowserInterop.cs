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

namespace MatterHackers.Agg.Platform.Browser
{
	/// <summary>
	/// Everything <see cref="BrowserSystemWindow"/> needs the DOM for, behind one seam.
	/// </summary>
	/// <remarks>
	/// The window itself carries no browser-only API and no <c>[SupportedOSPlatform("browser")]</c>, which is
	/// what lets the desktop test suite construct one and drive its tick, its resize handling and its
	/// screenshot contract with no browser anywhere. The implementation on the other side of this interface
	/// (<see cref="BrowserWindowInterop"/>) is where <c>[JSImport]</c> and the platform attribute live.
	/// <para/>
	/// The canvas selector is passed on every call rather than held here because the window owns it; this side
	/// stays stateless, and the JS module caches the element lookup.
	/// </remarks>
	public interface IBrowserWindowInterop
	{
		/// <summary>
		/// Prepares the canvas to be an agg window - focusable, no text selection, no touch scrolling - and
		/// reports the size of its backing store.
		/// </summary>
		/// <remarks>
		/// The size comes back from the bind rather than being waited for from the resize observer because the
		/// observer's first callback is delivered at the browser's next rendering opportunity, which can be
		/// after the first animation frame has already run; a window laid out at a guessed size and corrected
		/// one frame later flashes.
		/// </remarks>
		BrowserBackingSize BindCanvas(string canvasSelector);

		/// <summary>Subscribes the pointer, keyboard, wheel, focus and resize listeners.</summary>
		void AttachInput(string canvasSelector);

		/// <summary>Removes the listeners, so a closed window stops swallowing the page's keystrokes.</summary>
		void DetachInput(string canvasSelector);

		/// <summary>Sets the canvas's CSS <c>cursor</c>; see <see cref="BrowserCursorMap"/>.</summary>
		void SetCursor(string canvasSelector, string cssCursor);

		/// <summary>Sets <c>document.title</c>, which is what a window caption is in a page.</summary>
		void SetDocumentTitle(string title);

		/// <summary>Gives the canvas DOM focus, so key events are delivered to it at all.</summary>
		void Focus(string canvasSelector);
	}

	/// <summary>
	/// The <c>requestAnimationFrame</c> loop, behind a seam for the same reason
	/// <see cref="IBrowserWindowInterop"/> is: so the window can be ticked by a test.
	/// </summary>
	public interface IBrowserFrameLoop
	{
		/// <summary>
		/// Starts calling <paramref name="onFrame"/> once per animation frame, replacing any loop already
		/// running.
		/// </summary>
		void Start(Action onFrame);

		/// <summary>Stops the loop, if one is running. Safe to call when none is.</summary>
		void Stop();
	}
}
