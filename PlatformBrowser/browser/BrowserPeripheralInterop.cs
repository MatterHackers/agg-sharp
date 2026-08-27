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
	/// One file the user chose in the browser's file picker, with its bytes already read.
	/// </summary>
	/// <remarks>
	/// The bytes come across whole rather than as a stream because a <c>File</c> is only readable through a
	/// promise, and every agg-side consumer of an open dialog wants a path it can open synchronously. The
	/// provider's answer to that is to write them into the wasm virtual file system; see
	/// <see cref="MatterHackers.Agg.Platform.BrowserFileDialogProvider"/>.
	/// </remarks>
	public readonly struct BrowserPickedFile
	{
		public BrowserPickedFile(string name, byte[] bytes)
		{
			this.Name = name;
			this.Bytes = bytes;
		}

		/// <summary>The file's name as the user's machine spells it. No path - the browser never reveals one.</summary>
		public string Name { get; }

		/// <summary>The whole file.</summary>
		public byte[] Bytes { get; }
	}

	/// <summary>
	/// What <see cref="MatterHackers.Agg.UI.BrowserClipboard"/> needs the page for, behind one seam.
	/// </summary>
	/// <remarks>
	/// Same rule as <see cref="IBrowserWindowInterop"/>: the clipboard itself carries no browser-only API,
	/// so its cache behaviour is driven by the desktop test suite and only the implementation on the other
	/// side of this interface is <c>[SupportedOSPlatform("browser")]</c>.
	/// </remarks>
	public interface IBrowserClipboardInterop
	{
		/// <summary>
		/// Starts feeding <paramref name="onText"/> what <c>navigator.clipboard.readText()</c> reports - once
		/// now, and again every time the page takes focus.
		/// </summary>
		/// <remarks>
		/// Focus is the only moment a browser reliably allows a clipboard read, and it is also the only
		/// moment the answer can have changed: text copied in another tab or application got there while
		/// this page was not focused. Fire and forget by nature - a refused permission simply never calls
		/// back.
		/// </remarks>
		void StartWatchingSystemText(Action<string> onText);

		/// <summary>
		/// Asks the browser to put <paramref name="text"/> on the system clipboard. Returns immediately; the
		/// write may fail later and silently, which is why the cache is updated separately.
		/// </summary>
		void WriteText(string text);
	}

	/// <summary>
	/// What <see cref="MatterHackers.Agg.Platform.BrowserFileDialogProvider"/> needs the page for.
	/// </summary>
	public interface IBrowserFileDialogInterop
	{
		/// <summary>
		/// Puts up the browser's own file picker and reports what came back.
		/// </summary>
		/// <param name="accept">The <c>accept</c> attribute; empty for "any file". See
		/// <see cref="BrowserFileFilter"/>.</param>
		/// <param name="multiple">Whether more than one file may be chosen.</param>
		/// <param name="onFile">Called once per chosen file, in the order the picker listed them.</param>
		/// <param name="onComplete">Called once after the last file, or immediately on a cancel. A browser
		/// that does not raise a cancel event calls neither - see the provider's remarks.</param>
		void PickFiles(string accept, bool multiple, Action<BrowserPickedFile> onFile, Action onComplete);

		/// <summary>
		/// Hands bytes to the browser as a download, which is what "save a file" means in a page.
		/// </summary>
		void DownloadFile(string fileName, byte[] bytes);
	}

	/// <summary>
	/// What <see cref="MatterHackers.Agg.Platform.BrowserInformationProvider"/> needs the page for.
	/// </summary>
	public interface IBrowserScreenInterop
	{
		/// <summary>
		/// Reports <c>[screenWidthCssPixels, screenHeightCssPixels, devicePixelRatio,
		/// approximateMemoryGigabytes]</c>.
		/// </summary>
		/// <remarks>
		/// One array rather than four calls, for the reason <see cref="IBrowserWindowInterop.BindCanvas"/>
		/// returns one: these describe one display at one moment. The memory figure is
		/// <c>navigator.deviceMemory</c>, which only Chromium implements and which is deliberately coarse;
		/// zero means "the browser would not say".
		/// </remarks>
		double[] ReadScreenMetrics();
	}
}
