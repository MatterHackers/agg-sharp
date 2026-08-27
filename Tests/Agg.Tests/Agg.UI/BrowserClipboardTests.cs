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

using System.Collections.Generic;
using System.Threading.Tasks;
using MatterHackers.Agg.Platform.Browser;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// The browser clipboard answers reads from a cache, because <see cref="ISystemClipboard"/> is
	/// synchronous and <c>navigator.clipboard</c> is not (see <see cref="BrowserClipboard"/>'s remarks). That
	/// makes "which write wins" the whole of the behaviour worth testing, and none of it needs a browser: the
	/// clipboard reaches the page only through <see cref="IBrowserClipboardInterop"/>.
	/// </summary>
	public class BrowserClipboardTests
	{
		/// <summary>Nothing has been copied, so there is nothing to offer a paste.</summary>
		[Test]
		public async Task AnEmptyCacheOffersNothingToPaste()
		{
			var clipboard = new BrowserClipboard(interop: null);

			await Assert.That(clipboard.ContainsText).IsFalse();
			await Assert.That(clipboard.GetText()).IsEqualTo(string.Empty);
		}

		/// <summary>
		/// The reason <see cref="BrowserClipboard.SetText"/> writes the cache as well as the browser: the
		/// promise <c>writeText</c> returned may not have settled, and nothing re-reads the system clipboard
		/// until the page next takes focus - which copying and pasting inside the app never causes.
		/// </summary>
		[Test]
		public async Task AnInAppCopyIsImmediatelyPasteable()
		{
			var page = new RecordingClipboardInterop();
			var clipboard = new BrowserClipboard(page);

			clipboard.SetText("extruded profile");

			await Assert.That(clipboard.GetText()).IsEqualTo("extruded profile");
			await Assert.That(clipboard.ContainsText).IsTrue();

			// And the browser was asked too, so another application can paste it.
			await Assert.That(page.Written).IsEqualTo("extruded profile");
		}

		/// <summary>
		/// The refresh path: text copied in another tab or another application reaches this page when the
		/// page takes focus, which is the only moment it can have changed and the only moment a browser
		/// reliably allows a read.
		/// </summary>
		[Test]
		public async Task TextCopiedElsewhereArrivesOnTheFocusRefresh()
		{
			var page = new RecordingClipboardInterop();
			var clipboard = new BrowserClipboard(page);

			// Subscribed at construction rather than on the first read, because the first read is exactly the
			// moment it is too late to start a promise.
			await Assert.That(page.IsWatching).IsTrue();

			page.RaiseSystemText("copied in another tab");

			await Assert.That(clipboard.GetText()).IsEqualTo("copied in another tab");
			await Assert.That(clipboard.ContainsText).IsTrue();
		}

		/// <summary>
		/// The documented compromise. A refused permission, an unfocused document and a browser with no async
		/// clipboard API all resolve to nothing, and treating any of them as "the clipboard is empty" would
		/// erase what this application itself just copied.
		/// </summary>
		[Test]
		[Arguments((string)null)]
		[Arguments("")]
		public async Task ARefreshThatSaysNothingLeavesThisApplicationsOwnCopyAlone(string readResult)
		{
			var page = new RecordingClipboardInterop();
			var clipboard = new BrowserClipboard(page);

			clipboard.SetText("mine");

			page.RaiseSystemText(readResult);

			await Assert.That(clipboard.GetText()).IsEqualTo("mine");
		}

		/// <summary>
		/// A refresh reporting the same text is not a change - only of interest to a caller watching for one,
		/// which is why <see cref="BrowserClipboardCache.ApplySystemRead"/> says so.
		/// </summary>
		[Test]
		public async Task TheCacheReportsWhetherARefreshChangedAnything()
		{
			var cache = new BrowserClipboardCache();

			await Assert.That(cache.ApplySystemRead("first")).IsTrue();
			await Assert.That(cache.ApplySystemRead("first")).IsFalse();
			await Assert.That(cache.ApplySystemRead("second")).IsTrue();
			await Assert.That(cache.Text).IsEqualTo("second");
		}

		/// <summary>
		/// Null is what a caller with nothing to copy passes, and <see cref="BrowserClipboard.GetText"/>
		/// promises a string - so it becomes empty rather than being handed on.
		/// </summary>
		[Test]
		public async Task CopyingNullEmptiesTheClipboardRatherThanPoisoningIt()
		{
			var clipboard = new BrowserClipboard(interop: null);

			clipboard.SetText("something");
			clipboard.SetText(null);

			await Assert.That(clipboard.GetText()).IsEqualTo(string.Empty);
			await Assert.That(clipboard.ContainsText).IsFalse();
		}

		/// <summary>
		/// This clipboard carries no HTML flavor, so the alternative to keeping the text would be losing both.
		/// </summary>
		[Test]
		public async Task SettingTextAndHtmlKeepsTheTextAndDropsTheHtml()
		{
			var clipboard = new BrowserClipboard(interop: null);

			clipboard.SetTextAndHtml("plain", "<b>plain</b>");

			await Assert.That(clipboard.GetText()).IsEqualTo("plain");
			await Assert.That(clipboard.ContainsHtml).IsFalse();
			await Assert.That(clipboard.GetHtml()).IsEqualTo(string.Empty);
		}

		/// <summary>
		/// Images and file drops are absent here as they are on mac and Linux, and a caller that asks anyway
		/// gets something it can use rather than a null it will dereference.
		/// </summary>
		[Test]
		public async Task ImagesAndFileDropsAreAbsentButAnswerSafely()
		{
			var clipboard = new BrowserClipboard(interop: null);

			await Assert.That(clipboard.ContainsImage).IsFalse();
			await Assert.That(clipboard.GetImage()).IsNull();

			await Assert.That(clipboard.ContainsFileDropList).IsFalse();
			await Assert.That(clipboard.GetFileDropList()).IsNotNull();
			await Assert.That(clipboard.GetFileDropList().Count).IsEqualTo(0);
		}

		/// <summary>navigator.clipboard, replaced by a recorder the test can also push reads through.</summary>
		private sealed class RecordingClipboardInterop : IBrowserClipboardInterop
		{
			private readonly List<string> writes = new List<string>();

			private System.Action<string> onText;

			/// <summary>Whether the clipboard subscribed to the page's focus refresh.</summary>
			public bool IsWatching => this.onText != null;

			/// <summary>The last thing handed to <c>writeText</c>.</summary>
			public string Written => this.writes.Count == 0 ? null : this.writes[this.writes.Count - 1];

			public void StartWatchingSystemText(System.Action<string> onText) => this.onText = onText;

			public void WriteText(string text) => this.writes.Add(text);

			/// <summary>Delivers what a readText() promise resolved to, the way the focus listener would.</summary>
			public void RaiseSystemText(string text) => this.onText?.Invoke(text);
		}
	}
}
