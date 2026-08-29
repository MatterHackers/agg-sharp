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
using System.Collections.Specialized;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform.Browser;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// The browser <see cref="ISystemClipboard"/>, over <c>navigator.clipboard</c>. The peer of
	/// <c>MacClipboard</c>, <c>LinuxClipboard</c> and <c>WindowsFormsClipboard</c>; a head installs it with
	/// <c>Clipboard.SetSystemClipboard(new BrowserClipboard())</c>.
	/// </summary>
	/// <remarks>
	/// <para><b>The compromise, stated plainly: reads are of a cache, not of the clipboard.</b>
	/// <see cref="ISystemClipboard"/> is entirely synchronous - <see cref="GetText"/> returns a string - and
	/// the browser's clipboard API is entirely asynchronous. On wasm there is no way to bridge that: awaiting
	/// a promise means returning to the event loop, and the one thread is inside the widget's paste handler
	/// at the time. Blocking it cannot work, because the promise can only settle by returning to the very
	/// loop being blocked. So the answer has to be in hand before the question is asked.</para>
	/// <para>It is refreshed at the one moment that is both allowed and useful: when the page takes focus.
	/// Allowed, because browsers gate <c>readText()</c> on the document being focused (and Firefox on a user
	/// gesture, where it may never succeed at all); useful, because the only way the system clipboard can
	/// have changed behind this page's back is another tab or another application, and coming back to this
	/// page is how the user returns from one. Between focus events the cache cannot go stale in any way a
	/// user would notice, with one exception: text copied by a <em>second window of this same page</em>.</para>
	/// <para><b>Writes are fire and forget, and update the cache.</b> <c>writeText()</c> returns a promise
	/// this cannot wait for either, so <see cref="SetText"/> asks the browser and records the value locally
	/// in the same breath. Without the local record an in-app copy followed by an in-app paste would find
	/// nothing - the write may not have settled, and nothing would re-read until the page next took focus,
	/// which copying and pasting inside the app never causes.</para>
	/// <para><b>What this does not carry.</b> HTML, images and file-drop lists all report "not present" and
	/// return nothing, matching <c>MacClipboard</c> and <c>LinuxClipboard</c> on images and file drops. HTML
	/// is the one this loses that Linux and Windows have: the async clipboard API can carry
	/// <c>text/html</c> through <c>ClipboardItem</c>, but reading it has the same synchronous-contract
	/// problem as text and writing it is refused outright by some browsers for a non-user-gesture write. It
	/// is a later question, not a v1 one - nothing in agg's own widgets pastes HTML.</para>
	/// </remarks>
	public class BrowserClipboard : ISystemClipboard
	{
		private readonly BrowserClipboardCache cache = new BrowserClipboardCache();

		private readonly IBrowserClipboardInterop interop;

		/// <summary>
		/// The clipboard a head installs. Reaches <c>navigator.clipboard</c> in a browser and nowhere at
		/// all elsewhere, where it degrades to an in-process clipboard - which is precisely what
		/// <c>LinuxClipboard</c> does with no X display, and what makes this testable from a desktop suite.
		/// </summary>
		public BrowserClipboard()
			: this(CreatePageInterop())
		{
		}

		/// <param name="interop">The page seam, or null for a clipboard that is only this process's own.</param>
		public BrowserClipboard(IBrowserClipboardInterop interop)
		{
			this.interop = interop;

			// Started here rather than lazily on the first read, because the first read is exactly the
			// moment it is too late: readText() is a promise, and a paste cannot wait for one.
			this.interop?.StartWatchingSystemText(text => this.cache.ApplySystemRead(text));
		}

		/// <summary>
		/// The page seam in a browser, and nothing on a desktop.
		/// </summary>
		/// <remarks>
		/// A method rather than a ternary in the constructor initializer so the <c>IsBrowser()</c> guard is
		/// the plain <c>if</c> the platform compatibility analyzer reads without argument.
		/// </remarks>
		private static IBrowserClipboardInterop CreatePageInterop()
		{
			if (OperatingSystem.IsBrowser())
			{
				return new BrowserPeripherals();
			}

			return null;
		}

		/// <summary>The cached text and the rules that replace it. Public for the host's own tests.</summary>
		public BrowserClipboardCache Cache => this.cache;

		/// <inheritdoc/>
		public bool ContainsText => this.cache.ContainsText;

		/// <summary>Always false; see the class remarks.</summary>
		public bool ContainsHtml => false;

		/// <summary>Always false; see the class remarks.</summary>
		public bool ContainsImage => false;

		/// <summary>Always false. A page receives dropped files as a DOM drop event, not as a clipboard flavor.</summary>
		public bool ContainsFileDropList => false;

		/// <inheritdoc/>
		public string GetText() => this.cache.Text;

		/// <summary>Always empty; see the class remarks.</summary>
		public string GetHtml() => string.Empty;

		/// <summary>Always null; see the class remarks.</summary>
		public ImageBuffer GetImage() => null;

		/// <summary>Always empty; see the class remarks.</summary>
		public StringCollection GetFileDropList() => new StringCollection();

		/// <inheritdoc/>
		public void SetText(string text)
		{
			// The cache first: it is the half that cannot fail, and the half an immediate in-app paste
			// reads. The browser's own copy is a request that may be refused long after this returns.
			this.cache.SetLocalCopy(text);

			this.interop?.WriteText(text ?? string.Empty);
		}

		/// <summary>
		/// Writes the text and drops the HTML. This clipboard carries no HTML flavor, so the alternative
		/// would be silently losing the caller's text as well.
		/// </summary>
		public void SetTextAndHtml(string text, string html) => this.SetText(text);

		/// <summary>Does nothing; see the class remarks.</summary>
		public void SetImage(ImageBuffer imageBuffer)
		{
		}
	}
}
