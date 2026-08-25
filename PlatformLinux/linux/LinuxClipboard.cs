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
using System.Collections.Specialized;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform.Linux;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// The Linux <see cref="ISystemClipboard"/>, backed by the X11 CLIPBOARD selection through
	/// <see cref="X11Selection"/>. The peer of <c>MacClipboard</c> and PlatformWin32's
	/// <c>WindowsFormsClipboard</c>; an app installs it with
	/// <c>Clipboard.SetSystemClipboard(new LinuxClipboard())</c>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Text and HTML round trip with other X11 clients. Images and file drop lists report "not present"
	/// rather than pretending, exactly as on macOS - and on X11 a file drop is not even the same protocol,
	/// it is XDND.
	/// </para>
	/// <para>
	/// <b>Two backings, one of which is a fallback.</b> X11 has no clipboard storage: the owning client
	/// <em>is</em> the clipboard. So while this process owns the selection the answer comes from the
	/// strings held here - no round trip, and no way for the answer to differ from what was copied - and
	/// when it does not, every read is a conversion request to whoever does. Those same strings are the
	/// whole implementation when there is no X display at all, which is what a headless test run gets.
	/// </para>
	/// <para>
	/// <b>Threading.</b> Xlib is single-threaded here, so only the thread that owns the display may speak
	/// to the selection. A <em>write</em> from any other thread is not dropped: it is marshalled onto the
	/// UI thread with <see cref="UiThread.RunOnIdle(Action)"/> and claimed there. A <em>read</em> cannot do
	/// that - it has to answer now - so off-thread it answers from this process's own last copy, and says
	/// so once on stderr.
	/// </para>
	/// <para>
	/// <b>Reads are re-entrant, but not to input.</b> Reading another client's clipboard pumps the event
	/// loop while it waits, so repaints and window management keep working - but key, button and motion
	/// events are held back and replayed after the call unwinds. See <see cref="X11Selection"/>'s remarks:
	/// without that, a paste's own write-back silently eats anything typed during it.
	/// </para>
	/// </remarks>
	public class LinuxClipboard : ISystemClipboard
	{
		/// <summary>Logged at most once per process - see <see cref="SelectionForRead"/>.</summary>
		private static bool warnedOffThreadRead;

		/// <summary>
		/// The last thing written here. Two jobs: the answer while this process owns the selection, and the
		/// entire clipboard when there is no X11 to reach.
		/// </summary>
		private string text;

		private string html;

		/// <summary>
		/// Whether a text flavor is available. Deliberately <c>!= null</c> rather than
		/// <c>!string.IsNullOrEmpty</c>, for parity with <c>MacClipboard</c>, whose
		/// <c>stringForType: != null</c> distinguishes "the pasteboard holds an empty string" from "the
		/// pasteboard holds no string at all". Folding those together would make copying an empty
		/// selection behave differently on Linux than on the other two hosts.
		/// </summary>
		public bool ContainsText
		{
			get
			{
				X11Selection selection = SelectionForRead();
				if (selection == null || selection.OwnsClipboard)
				{
					return this.text != null;
				}

				return selection.RemoteHasText();
			}
		}

		/// <summary>Whether an HTML flavor is available. Same deliberate <c>!= null</c> parity as
		/// <see cref="ContainsText"/>.</summary>
		public bool ContainsHtml
		{
			get
			{
				X11Selection selection = SelectionForRead();
				if (selection == null || selection.OwnsClipboard)
				{
					return this.html != null;
				}

				return selection.RemoteHasHtml();
			}
		}

		/// <summary>Always false: images are not carried, matching the mac host.</summary>
		public bool ContainsImage => false;

		/// <summary>Always false: X11 file drops are a separate protocol (XDND), not a selection target.</summary>
		public bool ContainsFileDropList => false;

		/// <inheritdoc/>
		public string GetText()
		{
			X11Selection selection = SelectionForRead();
			if (selection == null || selection.OwnsClipboard)
			{
				return this.text ?? string.Empty;
			}

			// The spelling is the owner's choice, not ours: UTF8_STRING if it has one, then the MIME name,
			// then TEXT, then STRING. An old client with only STRING still holds text, and asking for
			// UTF8_STRING alone would report its clipboard as empty.
			return selection.RemoteText() ?? string.Empty;
		}

		/// <inheritdoc/>
		public string GetHtml()
		{
			X11Selection selection = SelectionForRead();
			if (selection == null || selection.OwnsClipboard)
			{
				return this.html ?? string.Empty;
			}

			return selection.RemoteHtml() ?? string.Empty;
		}

		/// <inheritdoc/>
		public ImageBuffer GetImage() => null;

		/// <inheritdoc/>
		public StringCollection GetFileDropList() => new StringCollection();

		/// <inheritdoc/>
		public void SetText(string text)
		{
			// Writing plain text clears any HTML flavor, the way clearContents does on the mac: otherwise a
			// later GetHtml would answer with HTML from an older, unrelated copy - and here it would also
			// keep advertising a text/html target we can no longer honour.
			this.text = text;
			this.html = null;
			this.PublishToSelection();
		}

		/// <inheritdoc/>
		public void SetTextAndHtml(string text, string html)
		{
			this.text = text;
			this.html = html;
			this.PublishToSelection();
		}

		/// <inheritdoc/>
		public void SetImage(ImageBuffer imageBuffer)
		{
		}

		/// <summary>
		/// The selection to read from, or null to answer from this process's own copy. Logs once when the
		/// fallback is taken for a reason a developer would want to know about - a read off the display
		/// thread, where the answer is this process's own last copy and not what another application may
		/// have copied since.
		/// </summary>
		private static X11Selection SelectionForRead()
		{
			X11Selection selection = X11Selection.TryGet();
			if (selection != null || !X11SystemWindow.HasDisplay)
			{
				// Either it worked, or this process is headless - in which case the in-process copy is not
				// a fallback at all, it is the whole clipboard, and there is nothing to warn about.
				return selection;
			}

			if (!warnedOffThreadRead)
			{
				warnedOffThreadRead = true;
				Console.Error.WriteLine(
					"LinuxClipboard: a clipboard read arrived off the thread that owns the X display, so it "
					+ "was answered from this process's own last copy rather than the X11 CLIPBOARD "
					+ "selection. Xlib here is single-threaded and a read cannot wait for the UI thread - "
					+ "read the clipboard from the UI thread to see what other applications have copied.");
			}

			return null;
		}

		/// <summary>
		/// Puts the current strings on the X clipboard, from whichever thread called.
		/// </summary>
		/// <remarks>
		/// A write, unlike a read, has nothing to return and so can afford to wait: off the display thread
		/// it is handed to <see cref="UiThread.RunOnIdle(Action)"/> rather than dropped. The captured values
		/// are re-checked when that runs, so a write already superseded by a newer one does not resurrect
		/// itself over the top of it.
		/// </remarks>
		private void PublishToSelection()
		{
			X11Selection selection = X11Selection.TryGet();
			if (selection != null)
			{
				Publish(selection, this.text, this.html);
				return;
			}

			if (!X11SystemWindow.HasDisplay)
			{
				// Headless: the strings are the clipboard, and there is nothing to publish them to.
				return;
			}

			string pendingText = this.text;
			string pendingHtml = this.html;

			UiThread.RunOnIdle(() =>
			{
				if (this.text != pendingText || this.html != pendingHtml)
				{
					// Superseded while this was queued. The newer write has its own turn coming.
					return;
				}

				X11Selection deferred = X11Selection.TryGet();
				if (deferred != null)
				{
					Publish(deferred, pendingText, pendingHtml);
				}
			});
		}

		/// <summary>
		/// Claims the selection, or gives it up when there is nothing to offer. Releasing rather than
		/// serving an empty string matters: "there is no text" and "the text is empty" are different
		/// statements, <see cref="ContainsText"/> distinguishes them on this host as it does on the mac,
		/// and other clients can only see the difference if the claim is actually dropped.
		/// </summary>
		private static void Publish(X11Selection selection, string text, string html)
		{
			if (text == null && html == null)
			{
				selection.Release();
			}
			else
			{
				selection.Claim(text, html);
			}
		}
	}
}
