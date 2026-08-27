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

namespace MatterHackers.Agg.Platform.Browser
{
	/// <summary>
	/// The one string a browser clipboard can answer with, and the rules for when it is replaced.
	/// </summary>
	/// <remarks>
	/// <para>Pulled out of <see cref="MatterHackers.Agg.UI.BrowserClipboard"/> as a plain object with no JS in
	/// it, so the part that can actually be got wrong - which write wins - runs in the desktop test suite.</para>
	/// <para><b>Why a cache at all.</b> <c>navigator.clipboard.readText()</c> returns a promise, and
	/// <c>ISystemClipboard.GetText</c> returns a string. Nothing can bridge those on wasm: awaiting the
	/// promise means returning to the event loop, and the one thread is inside the paste handler. So the
	/// answer has to already be in hand when the question is asked, which is what this holds.</para>
	/// </remarks>
	public sealed class BrowserClipboardCache
	{
		/// <summary>What <c>GetText</c> answers with. Never null, so callers need no empty check.</summary>
		public string Text { get; private set; } = string.Empty;

		/// <summary>
		/// Whether there is text to paste. Empty rather than <c>!= null</c>, which is where this parts
		/// company with <c>LinuxClipboard</c>: a browser cannot tell "the clipboard holds an empty string"
		/// from "the clipboard could not be read", so the only honest reading of an empty cache is that
		/// there is nothing to offer.
		/// </summary>
		public bool ContainsText => this.Text.Length > 0;

		/// <summary>
		/// Records what this application just copied. Called by <c>SetText</c> alongside the fire-and-forget
		/// <c>writeText</c>, which is what makes an in-app copy immediately pasteable: the write's promise
		/// may not have settled, and even once it has, nothing would re-read the system clipboard until the
		/// page next takes focus - which a user copying and pasting inside the app never causes.
		/// </summary>
		public void SetLocalCopy(string text) => this.Text = text ?? string.Empty;

		/// <summary>
		/// Applies what <c>navigator.clipboard.readText()</c> reported, so text copied in another tab or
		/// another application can be pasted here.
		/// </summary>
		/// <returns>Whether the cache changed, which is only of interest to a test.</returns>
		/// <remarks>
		/// Null and empty are both ignored, and that is the deliberate compromise. A read can fail for
		/// reasons that have nothing to do with the clipboard being empty - the permission was refused, the
		/// document was not focused at the moment the promise ran, the browser has no async clipboard API at
		/// all - and every one of those resolves to nothing here. Treating them as "the clipboard is empty"
		/// would erase what this application itself copied, breaking in-app copy/paste on the browsers that
		/// are strictest about clipboard reads. Losing an <em>intentionally</em> emptied system clipboard is
		/// the price, and it is the cheaper of the two.
		/// </remarks>
		public bool ApplySystemRead(string text)
		{
			if (string.IsNullOrEmpty(text) || text == this.Text)
			{
				return false;
			}

			this.Text = text;
			return true;
		}
	}
}
