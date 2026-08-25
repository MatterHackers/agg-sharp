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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using MatterHackers.Agg.UI;

namespace MatterHackers.Agg.Platform.Linux
{
	/// <summary>The atoms one <see cref="X11Selection"/> speaks, interned once per display.</summary>
	/// <remarks>
	/// A struct rather than fields on <see cref="X11Selection"/> so the encoding and target-choosing logic
	/// can be pure functions of "these atom ids" and be tested without an X server: an atom is only ever an
	/// opaque id compared for equality, so a test can invent its own set and the code cannot tell.
	/// </remarks>
	internal struct X11SelectionAtoms
	{
		/// <summary>The CLIPBOARD selection - the one Ctrl+C writes. Not PRIMARY, which is the
		/// select-to-copy middle-click one and a different user gesture entirely.</summary>
		public ulong Clipboard;

		/// <summary>The meta-target every owner must answer: "what can you convert to?".</summary>
		public ulong Targets;

		public ulong Utf8String;

		/// <summary>Xatom.h's <c>XA_STRING</c>: Latin-1, and only Latin-1.</summary>
		public ulong String;

		/// <summary>ICCCM's <c>TEXT</c> - "text in whatever encoding you like, tell me which".</summary>
		public ulong Text;

		/// <summary>The MIME spelling of UTF-8 text, which is what GTK and Qt ask for first.</summary>
		public ulong TextPlainUtf8;

		public ulong TextHtml;

		/// <summary>The type that means "this property is not the data, it is a transfer about to
		/// happen" - see <see cref="X11Selection.ReadIncrementally"/>.</summary>
		public ulong Incr;

		/// <summary>The private property on our own window that conversions are delivered into.</summary>
		public ulong Property;
	}

	/// <summary>
	/// The CLIPBOARD selection, which on X11 is not storage but a conversation: there is no clipboard
	/// daemon in the protocol, so the owning client <em>is</em> the clipboard and must answer
	/// <c>SelectionRequest</c> events for as long as it holds the claim. This owns the claim, the hidden
	/// window it is made from, and both sides of the conversation.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>The hidden window.</b> A selection owner is a window, and a conversion result is delivered as a
	/// property on a window, so both need one. It is deliberately not one of the application's real
	/// windows: a real window comes and goes with the UI, and a clipboard that stops working because a
	/// dialog closed is worse than no clipboard. A 1x1 unmapped <c>InputOnly</c> window costs nothing, is
	/// never seen, and lives as long as the display does.
	/// </para>
	/// <para>
	/// <b>Timestamps.</b> Every claim here uses <c>CurrentTime</c> rather than the timestamp of the key
	/// press that caused it, because <see cref="X11SystemWindow"/> does not keep a last-event time. ICCCM
	/// asks for the real event time so that two clients racing for the selection resolve in the order the
	/// user acted; with <c>CurrentTime</c> the server substitutes its own clock, so the loser of such a
	/// race is whoever the scheduler ran second rather than whoever the user asked second. That is a real
	/// difference and a rare one - it needs two clipboard writes inside a scheduling quantum - and closing
	/// it means threading an event time out of the input path, which is its own change.
	/// </para>
	/// <para>
	/// <b>Re-entrancy, and why input is held back.</b> A paste is a round trip through the X server and
	/// this host has one thread, so <see cref="ConvertSelection"/> runs a nested pump. That pump keeps
	/// dispatching the events a stalled window must not miss - Expose, ConfigureNotify, the WM protocols -
	/// but it does <em>not</em> dispatch input: key, button, motion and crossing events are queued by
	/// <see cref="DispatchOrDefer"/> and replayed, in order, from the outer
	/// <c>X11SystemWindow.PumpEvents</c> once the whole clipboard call has unwound. Dispatching them
	/// inline would break the callers this exists for. <c>InternalTextEditWidget.PasteFromClipboard</c>
	/// snapshots its text, calls <c>GetText</c>, and writes the result back: a keystroke delivered in the
	/// middle of that is silently overwritten by the write-back. A ButtonRelease is worse - it would run
	/// <c>SyncPointerGrab</c> and the mouse-up path underneath an outer <c>OnMouseDown</c> that has not
	/// returned. Deferral costs a keystroke up to a second of latency against a wedged clipboard owner,
	/// which is a pause; the alternative is lost input, which is a bug.
	/// </para>
	/// <para>
	/// <b>Threading.</b> Xlib here is single-threaded (see <see cref="Xlib"/>'s remarks), so everything on
	/// this type belongs to the thread that owns the display. <see cref="LinuxClipboard"/> is what keeps a
	/// call from another thread off this type, and what marshals an off-thread write back onto it.
	/// </para>
	/// </remarks>
	/// <summary>How a conversion request ended, which is not the same question as what it returned.</summary>
	internal enum X11ConversionOutcome
	{
		/// <summary>The owner produced the target.</summary>
		Answered,

		/// <summary>The owner answered, and the answer was "I cannot do that". Fast, and final for that
		/// target only - the owner is alive and worth asking about others.</summary>
		Refused,

		/// <summary>
		/// Nobody answered inside the timeout. The owner is wedged or gone, and every further target costs
		/// another full timeout to be told the same nothing - which is why this is worth distinguishing.
		/// </summary>
		TimedOut,
	}

	internal sealed unsafe class X11Selection
	{
		/// <summary>
		/// How long a paste waits for the owner to answer before giving up. Long enough for a remote X
		/// connection or a busy owner, short enough that a dead owner - a client that claimed the selection
		/// and then wedged, which the protocol gives us no way to detect - is a pause and not a hang.
		/// </summary>
		private const int ConversionTimeoutMilliseconds = 1000;

		/// <summary>
		/// How long an INCR transfer may go without <em>progress</em> before it is abandoned. Per chunk,
		/// not per transfer: a large paste is legitimately many chunks, and one budget for the whole thing
		/// would fail an 80MB transfer that is arriving perfectly steadily for no reason but its size.
		/// </summary>
		private const int IncrChunkTimeoutMilliseconds = 1000;

		/// <summary>How long the abort-time drain will keep acknowledging chunks, in total.</summary>
		private const int IncrDrainTimeoutMilliseconds = 500;

		/// <summary>And how many, so a sender stuck in a loop cannot hold the drain open.</summary>
		private const int IncrDrainChunkLimit = 4096;

		/// <summary>
		/// How long a TARGETS answer is trusted. There is no "the clipboard changed" event for a selection
		/// somebody else owns - SelectionClear only fires on the owner losing it - so a short expiry is the
		/// only invalidation available for a foreign clipboard. Long enough that the
		/// <c>ContainsText</c>-then-<c>GetText</c> pair a context menu does costs one round trip instead of
		/// two, short enough that a copy in another window shows up on the next menu.
		/// </summary>
		private const int TargetsCacheMilliseconds = 250;

		/// <summary>
		/// Bytes of slack left under the connection's maximum request size for <c>XChangeProperty</c>'s own
		/// header and fixed fields. Generous: the header is 24 bytes and the cost of over-reserving is
		/// nothing.
		/// </summary>
		private const int ChangePropertyOverheadBytes = 64;

		/// <summary>
		/// The most input events held back across one clipboard round trip. A second of frantic mouse
		/// motion is a few hundred, so this is far past any real burst; it exists so that a pathological
		/// wait cannot grow the queue without bound.
		/// </summary>
		private const int DeferredInputLimit = 4096;

		/// <summary>
		/// Input events that arrived while a conversion was outstanding, waiting to be replayed in order.
		/// Static because there is one display and one queue: see the class remarks on re-entrancy.
		/// </summary>
		private static readonly List<XEvent> DeferredInput = new List<XEvent>();

		/// <summary>A monotonic clock for the TARGETS cache. One for the process; nothing here is per-call
		/// timing, only age.</summary>
		private static readonly Stopwatch CacheClock = Stopwatch.StartNew();

		private static X11Selection instance;

		private static bool dispatchingDeferredInput;

		private static bool warnedDeferredInputOverflow;

		private X11SelectionAtoms atoms;

		private ulong window;

		/// <summary>Whether we currently hold CLIPBOARD, as far as we have been told.</summary>
		private bool ownsClipboard;

		/// <summary>What we last put on the clipboard, and what <see cref="HandleSelectionRequest"/>
		/// serves. Null html means the HTML flavor is simply not offered.</summary>
		private string ownedText;

		private string ownedHtml;

		/// <summary>
		/// Guards <see cref="ConvertSelection"/> against re-entering itself. The nested pump dispatches to
		/// widget code, and widget code can ask for the clipboard; without this, a paste inside a paste
		/// would consume the outer request's answer.
		/// </summary>
		private bool converting;

		/// <summary>
		/// An INCR transfer was given up on with the sender still mid-stream. See
		/// <see cref="PurgeAbandonedTransfer"/> for what that costs the next conversion.
		/// </summary>
		private bool incrTransferAbandoned;

		private ulong[] cachedTargets;

		private bool cachedTargetsValid;

		/// <summary>
		/// Whether the cached TARGETS answer is missing because the owner never replied, as opposed to
		/// replying that it cannot do TARGETS. The two failures call for opposite responses - see
		/// <see cref="RemoteText"/>.
		/// </summary>
		private bool cachedTargetsTimedOut;

		private long cachedTargetsAtMilliseconds;

		private X11Selection()
		{
		}

		/// <summary>The interned atoms, for tests and for <see cref="LinuxClipboard"/>'s target checks.</summary>
		internal X11SelectionAtoms Atoms => this.atoms;

		/// <summary>Whether this process is the current CLIPBOARD owner.</summary>
		internal bool OwnsClipboard => this.ownsClipboard;

		/// <summary>The text we are serving, or null when we are not the owner.</summary>
		internal string OwnedText => this.ownsClipboard ? this.ownedText : null;

		/// <summary>The HTML we are serving, or null when we are not the owner or offered none.</summary>
		internal string OwnedHtml => this.ownsClipboard ? this.ownedHtml : null;

		/// <summary>
		/// The selection for this process, or null when X11 cannot be spoken from here - no window has
		/// opened a display yet, the process is headless, or the caller is not on the thread that owns the
		/// connection. A null answer is the caller's cue to fall back to in-process behaviour rather than
		/// an error.
		/// </summary>
		internal static X11Selection TryGet()
		{
			if (!X11SystemWindow.OnDisplayThread)
			{
				return null;
			}

			IntPtr display = X11SystemWindow.SharedDisplay;

			X11Selection selection = instance;
			if (selection == null)
			{
				selection = new X11Selection();
				if (!selection.Initialize(display))
				{
					return null;
				}

				instance = selection;
			}

			return selection;
		}

		/// <summary>
		/// Handles the selection events, which belong to the hidden window and so would be dropped by
		/// <see cref="X11SystemWindow"/>'s per-window routing.
		/// </summary>
		/// <returns>True when the event was ours and must not be routed on.</returns>
		internal static bool TryHandleEvent(ref XEvent nextEvent)
		{
			X11Selection selection = instance;
			if (selection == null || selection.window == X11.None)
			{
				return false;
			}

			switch (nextEvent.Type)
			{
				case X11.SelectionRequest:
					if (nextEvent.As<XSelectionRequestEvent>().Owner != selection.window)
					{
						return false;
					}

					try
					{
						selection.HandleSelectionRequest(ref nextEvent.As<XSelectionRequestEvent>());
					}
					catch (Exception ex)
					{
						// A requestor left without an answer waits out its own timeout, which is bad; a
						// throw escaping into the display-wide dispatch would take the event loop down,
						// which is worse.
						Console.Error.WriteLine($"X11Selection SelectionRequest handler threw {ex}");
					}

					return true;

				case X11.SelectionClear:
					if (nextEvent.As<XSelectionClearEvent>().Window != selection.window)
					{
						return false;
					}

					// The window is ours, so the event is ours to swallow either way - but only a CLIPBOARD
					// clear drops the clipboard. The same hidden window is the natural owner for any other
					// selection this host grows later (PRIMARY, or a drag), and treating one of those as a
					// clipboard loss would silently empty the clipboard when nothing had touched it.
					if (nextEvent.As<XSelectionClearEvent>().Selection == selection.atoms.Clipboard)
					{
						selection.HandleSelectionClear();
					}

					return true;

				case X11.SelectionNotify:
					// Only ever reached by an answer to a conversion we already gave up waiting for: the
					// nested pump in ConvertSelection takes the ones it asked for before dispatching.
					// Swallowed rather than routed, because no window wants it.
					return nextEvent.As<XSelectionEvent>().Requestor == selection.window;

				case X11.PropertyNotify:
					// Same: INCR chunks are taken by ReadIncrementally's own pump. Anything reaching here
					// is a leftover, and the hidden window is not a window any widget owns.
					return nextEvent.As<XPropertyEvent>().Window == selection.window;

				default:
					return false;
			}
		}

		/// <summary>
		/// Replays the input held back across a clipboard round trip, in the order it arrived. Called from
		/// <c>X11SystemWindow.PumpEvents</c> <em>before</em> it takes anything new off the queue, which is
		/// what keeps a deferred keystroke ahead of one typed after the paste finished.
		/// </summary>
		/// <remarks>
		/// Deliberately not called from <see cref="ConvertSelection"/>'s own unwind. The point of the
		/// deferral is that the widget code which asked for the clipboard - a paste that snapshots, reads
		/// and writes back - has finished, and inside that call it has not.
		/// </remarks>
		internal static void DispatchDeferredInput()
		{
			if (DeferredInput.Count == 0 || dispatchingDeferredInput)
			{
				return;
			}

			// A conversion still in flight means the stack that deferred these has not unwound.
			if (instance != null && instance.converting)
			{
				return;
			}

			dispatchingDeferredInput = true;
			try
			{
				// Taken as a snapshot: a replayed keystroke can start a paste of its own, and the events
				// that defers are newer than everything here - so they belong after this batch, not
				// interleaved with it.
				XEvent[] replay = DeferredInput.ToArray();
				DeferredInput.Clear();

				for (int i = 0; i < replay.Length; i++)
				{
					X11SystemWindow.DispatchEvent(ref replay[i]);
				}
			}
			finally
			{
				dispatchingDeferredInput = false;
			}
		}

		// -------------------------------------------------------------------------------------------
		// Pure helpers. No display, no window - see X11SelectionAtoms' remarks for why these can be
		// tested without an X server, and LinuxClipboardTests for the tests that do.
		// -------------------------------------------------------------------------------------------

		/// <summary>
		/// Whether an event arriving mid-conversion must be held back rather than dispatched. See the class
		/// remarks: these are the events that re-enter widget input handling, and the two failures that
		/// causes - a keystroke overwritten by a paste's write-back, and a mouse-up run underneath an
		/// unfinished mouse-down.
		/// </summary>
		internal static bool IsDeferredDuringConversion(int eventType)
		{
			switch (eventType)
			{
				case X11.KeyPress:
				case X11.KeyRelease:
				case X11.ButtonPress:
				case X11.ButtonRelease:
				case X11.MotionNotify:
				case X11.EnterNotify:
				case X11.LeaveNotify:
					return true;

				default:
					// Expose, ConfigureNotify, ClientMessage (WM_DELETE_WINDOW), FocusIn/FocusOut and the
					// rest keep flowing. They are what a window stalled on a clipboard owner must not miss,
					// and none of them is an input event a widget can lose the way the set above can.
					return false;
			}
		}

		/// <summary>
		/// What we advertise for <c>TARGETS</c>. Order is not protocol, but it is the order a requestor
		/// that walks the list sees, so the richest text form comes first.
		/// </summary>
		internal static ulong[] BuildTargetList(in X11SelectionAtoms atoms, bool hasHtml)
		{
			var targets = new List<ulong>(6)
			{
				atoms.Targets,
				atoms.Utf8String,
				atoms.TextPlainUtf8,
				atoms.Text,
				atoms.String,
			};

			if (hasHtml)
			{
				targets.Add(atoms.TextHtml);
			}

			return targets.ToArray();
		}

		/// <summary>
		/// The text targets to ask a foreign owner for, best first. UTF-8 before Latin-1 for the obvious
		/// reason; <c>TEXT</c> ahead of <c>STRING</c> because it lets the owner pick an encoding and say
		/// which, and <see cref="DecodeText"/> reads whatever it says.
		/// </summary>
		internal static ulong[] TextTargetPreference(in X11SelectionAtoms atoms)
			=> new[] { atoms.Utf8String, atoms.TextPlainUtf8, atoms.Text, atoms.String };

		/// <summary>
		/// Picks the best text target an owner actually offers, or <c>None</c> when it offers no text at
		/// all. An owner that has only <c>STRING</c> - an old client, or a minimal one - is still a
		/// clipboard with text on it, and refusing to look past UTF8_STRING would report it as empty.
		/// </summary>
		internal static ulong ChooseTextTarget(ulong[] offered, in X11SelectionAtoms atoms)
		{
			if (offered == null)
			{
				return X11.None;
			}

			foreach (ulong preferred in TextTargetPreference(atoms))
			{
				if (Array.IndexOf(offered, preferred) >= 0)
				{
					return preferred;
				}
			}

			return X11.None;
		}

		/// <summary>
		/// Encodes what we own for one requested target.
		/// </summary>
		/// <param name="dataType">
		/// The atom to stamp the property with, which is not always the target: <c>TEXT</c> means "your
		/// choice of encoding, and say which", so it is answered as UTF8_STRING.
		/// </param>
		/// <returns>The bytes, or null when the target is one we do not offer and must refuse.</returns>
		internal static byte[] EncodeForTarget(
			ulong target,
			string text,
			string html,
			in X11SelectionAtoms atoms,
			out ulong dataType)
		{
			if (target == atoms.Utf8String || target == atoms.TextPlainUtf8 || target == atoms.Text)
			{
				dataType = atoms.Utf8String;
				return Encoding.UTF8.GetBytes(text ?? string.Empty);
			}

			if (target == atoms.String)
			{
				// XA_STRING is Latin-1 by definition. Encoding.Latin1 substitutes '?' for anything outside
				// it, which is the lossy answer the target asked for - a requestor that wanted the accents
				// should have asked for UTF8_STRING, and every modern one does.
				dataType = atoms.String;
				return Encoding.Latin1.GetBytes(text ?? string.Empty);
			}

			if (target == atoms.TextHtml && html != null)
			{
				dataType = atoms.TextHtml;
				return Encoding.UTF8.GetBytes(html);
			}

			dataType = X11.None;
			return null;
		}

		/// <summary>
		/// Decodes a text property by the type the owner stamped on it. Anything that is not explicitly
		/// Latin-1 is read as UTF-8, which covers UTF8_STRING and the MIME types, and is the least wrong
		/// guess for the compound-text encodings this does not implement.
		/// </summary>
		internal static string DecodeText(byte[] data, ulong type, in X11SelectionAtoms atoms)
		{
			if (data == null)
			{
				return null;
			}

			return type == atoms.String
				? Encoding.Latin1.GetString(data)
				: Encoding.UTF8.GetString(data);
		}

		/// <summary>Joins the chunks of an INCR transfer back into the value that was sent.</summary>
		internal static byte[] AssembleChunks(IReadOnlyList<byte[]> chunks)
		{
			int total = 0;
			for (int i = 0; i < chunks.Count; i++)
			{
				total += chunks[i].Length;
			}

			var assembled = new byte[total];
			int offset = 0;
			for (int i = 0; i < chunks.Count; i++)
			{
				Buffer.BlockCopy(chunks[i], 0, assembled, offset, chunks[i].Length);
				offset += chunks[i].Length;
			}

			return assembled;
		}

		/// <summary>
		/// Reads a format-32 property's items out of the bytes <c>XGetWindowProperty</c> returned. Each
		/// item is eight bytes and not four: a "format 32" property is unpacked into C <c>long</c>s, and on
		/// LP64 that is 64 bits even though the wire carried 32.
		/// </summary>
		internal static ulong[] ParseAtomList(byte[] data)
		{
			if (data == null)
			{
				return Array.Empty<ulong>();
			}

			var atomList = new ulong[data.Length / sizeof(ulong)];
			for (int i = 0; i < atomList.Length; i++)
			{
				atomList[i] = BitConverter.ToUInt64(data, i * sizeof(ulong));
			}

			return atomList;
		}

		// -------------------------------------------------------------------------------------------
		// Owning the selection
		// -------------------------------------------------------------------------------------------

		/// <summary>
		/// Claims CLIPBOARD and starts serving <paramref name="text"/> (and <paramref name="html"/>, when
		/// it is not null) to anyone who asks.
		/// </summary>
		internal void Claim(string text, string html)
		{
			this.ownedText = text;
			this.ownedHtml = html;

			// Whatever a foreign owner was offering a moment ago is now irrelevant: we are the owner.
			this.cachedTargetsValid = false;

			IntPtr display = X11SystemWindow.SharedDisplay;
			if (display == IntPtr.Zero)
			{
				return;
			}

			Xlib.XSetSelectionOwner(display, this.atoms.Clipboard, this.window, X11.CurrentTime);

			// The server is the authority on who owns a selection, and a claim can fail - a window that
			// has been destroyed, or a race with another client. Believing an unverified claim would make
			// every later read answer from our own copy while the real clipboard said something else.
			this.ownsClipboard = Xlib.XGetSelectionOwner(display, this.atoms.Clipboard) == this.window;
			Xlib.XFlush(display);
		}

		/// <summary>
		/// Gives up the claim, so the clipboard genuinely holds nothing of ours rather than holding an
		/// empty string. What <c>SetText(null)</c> means: "there is no text", which is a different
		/// statement from "the text is empty" and has to reach other clients as one.
		/// </summary>
		internal void Release()
		{
			this.ownedText = null;
			this.ownedHtml = null;
			this.cachedTargetsValid = false;

			IntPtr display = X11SystemWindow.SharedDisplay;

			// Only ever released when we hold it. XSetSelectionOwner(..., None) succeeds whoever the owner
			// is, so doing this unconditionally would empty another application's clipboard.
			if (display != IntPtr.Zero && this.ownsClipboard)
			{
				Xlib.XSetSelectionOwner(display, this.atoms.Clipboard, X11.None, X11.CurrentTime);
				Xlib.XFlush(display);
			}

			this.ownsClipboard = false;
		}

		/// <summary>
		/// What the current owner says it can convert to, or null when there is no owner, it does not
		/// answer, or it does not implement TARGETS. Cached for
		/// <see cref="TargetsCacheMilliseconds"/> - see that constant for why an expiry is the only
		/// invalidation a foreign clipboard offers.
		/// </summary>
		internal ulong[] RemoteTargets()
		{
			long now = CacheClock.ElapsedMilliseconds;
			if (this.cachedTargetsValid && now - this.cachedTargetsAtMilliseconds < TargetsCacheMilliseconds)
			{
				return this.cachedTargets;
			}

			// The reply's type is XA_ATOM for every well-behaved owner, and a few answer with TARGETS
			// itself - the same list under a different name - so only the format is worth checking.
			byte[] data = this.ConvertSelection(this.atoms.Targets, out _, out int format, out X11ConversionOutcome outcome);

			this.cachedTargets = data != null && format == 32 ? ParseAtomList(data) : null;
			this.cachedTargetsTimedOut = outcome == X11ConversionOutcome.TimedOut;

			// The failure is cached too, and on purpose: against an owner that has wedged, every query
			// costs a full timeout, and a context menu asking twice would stall for two seconds.
			this.cachedTargetsAtMilliseconds = CacheClock.ElapsedMilliseconds;
			this.cachedTargetsValid = true;

			return this.cachedTargets;
		}

		/// <summary>Whether the foreign owner offers text in any spelling we can read.</summary>
		internal bool RemoteHasText()
		{
			ulong[] offered = this.RemoteTargets();
			if (offered != null)
			{
				return ChooseTextTarget(offered, this.atoms) != X11.None;
			}

			if (this.cachedTargetsTimedOut)
			{
				// The owner is not answering. There may well be text on its clipboard, but nothing here can
				// reach it, and saying yes would enable a Paste that then pastes nothing.
				return false;
			}

			// It answered, it just does not implement the TARGETS meta-target - which ICCCM requires and a
			// few minimal clients skip. If somebody owns the clipboard, say so: RemoteText will ask for the
			// spellings directly, and against a live owner that costs a refusal, not a stall.
			IntPtr display = X11SystemWindow.SharedDisplay;
			return display != IntPtr.Zero
				&& Xlib.XGetSelectionOwner(display, this.atoms.Clipboard) != X11.None;
		}

		/// <summary>
		/// The foreign owner's text, in the best spelling it offers, or null when it has none.
		/// </summary>
		internal string RemoteText()
		{
			ulong[] offered = this.RemoteTargets();
			if (offered != null)
			{
				ulong target = ChooseTextTarget(offered, this.atoms);
				return target == X11.None ? null : this.RemoteTextForTarget(target);
			}

			if (this.cachedTargetsTimedOut)
			{
				// Nobody answered TARGETS, so nobody will answer four more conversions either - and each
				// one costs a full timeout. Trying them anyway is how a single wedged clipboard owner turns
				// one second of frozen UI into five.
				return null;
			}

			// A live owner that skips TARGETS. Ask for the spellings in preference order and take the first
			// that answers; a refusal from a live owner comes straight back.
			foreach (ulong target in TextTargetPreference(this.atoms))
			{
				string got = this.RemoteTextForTarget(target);
				if (got != null)
				{
					return got;
				}
			}

			return null;
		}

		/// <summary>Whether the foreign owner offers <c>text/html</c>.</summary>
		/// <remarks>
		/// No TARGETS list means no, deliberately - and this is the one place that does <i>not</i> mirror
		/// the timed-out-versus-refused split <see cref="RemoteText"/> makes. That split exists because
		/// there is a cheap stand-in for "has text": somebody owns the clipboard, so ask for the spellings
		/// only when the caller actually wants the text. There is no equivalent stand-in for html - owning
		/// the clipboard says nothing about whether html is among what is offered - so the only way to
		/// answer this for a live owner that skips TARGETS is to convert <c>text/html</c> in full, which
		/// against a large document is an INCR transfer of the whole thing to answer a bool, and then a
		/// second one when <see cref="RemoteHtml"/> is called for real.
		/// <para>
		/// The cost of being wrong here is small and one-directional: <see cref="RemoteHtml"/> does probe
		/// in that case, so html from a TARGETS-less owner is still readable by anyone who asks for it
		/// directly. Only the capability query understates, and it understates rather than promising html
		/// that may not be there.
		/// </para>
		/// </remarks>
		internal bool RemoteHasHtml()
		{
			ulong[] offered = this.RemoteTargets();
			return offered != null && Array.IndexOf(offered, this.atoms.TextHtml) >= 0;
		}

		/// <summary>The foreign owner's HTML, or null when it has none.</summary>
		internal string RemoteHtml()
		{
			ulong[] offered = this.RemoteTargets();
			if (offered == null)
			{
				// Same split as RemoteText: a wedged owner is not asked again, a live one that skips
				// TARGETS is asked directly.
				return this.cachedTargetsTimedOut ? null : this.RemoteTextForTarget(this.atoms.TextHtml);
			}

			if (Array.IndexOf(offered, this.atoms.TextHtml) < 0)
			{
				// It said what it has, and html is not in it. Asking anyway would cost a round trip to be
				// told no.
				return null;
			}

			return this.RemoteTextForTarget(this.atoms.TextHtml);
		}

		/// <summary>
		/// Converts CLIPBOARD to one text target and decodes it, or null when the owner refuses, does not
		/// answer in time, or answers with something that is not a byte property.
		/// </summary>
		private string RemoteTextForTarget(ulong target)
		{
			byte[] data = this.ConvertSelection(target, out ulong type, out int format, out _);
			if (data == null || format != 8)
			{
				return null;
			}

			return DecodeText(data, type, this.atoms);
		}

		/// <summary>
		/// Creates the hidden window and interns the atoms. False when the display went away underneath,
		/// which leaves <see cref="instance"/> unset so the next call can try again.
		/// </summary>
		private bool Initialize(IntPtr display)
		{
			this.atoms = new X11SelectionAtoms
			{
				Clipboard = Xlib.XInternAtom(display, "CLIPBOARD", 0),
				Targets = Xlib.XInternAtom(display, "TARGETS", 0),
				Utf8String = Xlib.XInternAtom(display, "UTF8_STRING", 0),
				String = X11.XA_STRING,
				Text = Xlib.XInternAtom(display, "TEXT", 0),
				TextPlainUtf8 = Xlib.XInternAtom(display, "text/plain;charset=utf-8", 0),
				TextHtml = Xlib.XInternAtom(display, "text/html", 0),
				Incr = Xlib.XInternAtom(display, "INCR", 0),
				Property = Xlib.XInternAtom(display, "AGG_SELECTION", 0),
			};

			var attributes = new XSetWindowAttributes
			{
				// Never managed, never mapped, and never seen - but override-redirect anyway, so no window
				// manager can take an interest in it.
				OverrideRedirect = X11.True,

				// PropertyChangeMask is not optional: the receiving half of an INCR transfer is driven
				// entirely by PropertyNotify on this window, so without it a large paste waits forever.
				EventMask = X11.PropertyChangeMask,
			};

			// InputOnly: no pixels, no depth, no visual, and nothing to draw. Depth and visual are
			// CopyFromParent (0 and NULL), which is the only combination InputOnly accepts.
			this.window = Xlib.XCreateWindow(
				display,
				X11SystemWindow.SharedRootWindow,
				-10,
				-10,
				1,
				1,
				0,
				X11.CopyFromParent,
				X11.InputOnly,
				IntPtr.Zero,
				X11.CWOverrideRedirect | X11.CWEventMask,
				&attributes);

			return this.window != X11.None;
		}

		/// <summary>
		/// Answers one request for our clipboard. Every path ends in a SelectionNotify, including refusal:
		/// a requestor that is never answered blocks on its own timeout, and on most toolkits that is the
		/// paste menu freezing for a second.
		/// </summary>
		private void HandleSelectionRequest(ref XSelectionRequestEvent request)
		{
			IntPtr display = X11SystemWindow.SharedDisplay;

			// A property of None is an obsolete client from before ICCCM; the convention is to answer on a
			// property named by the target.
			ulong property = request.Property == X11.None ? request.Target : request.Property;

			bool answered = display != IntPtr.Zero
				&& this.ownsClipboard
				&& request.Selection == this.atoms.Clipboard
				&& this.WriteRequestedTarget(display, ref request, property);

			var reply = default(XEvent);
			ref XSelectionEvent notify = ref reply.As<XSelectionEvent>();
			notify.Type = X11.SelectionNotify;
			notify.Display = display;
			notify.Requestor = request.Requestor;
			notify.Selection = request.Selection;
			notify.Target = request.Target;

			// The property field is the whole answer: it names where the data is, or it is None, which is
			// how a refusal is spelled. There is no other refusal.
			notify.Property = answered ? property : X11.None;
			notify.Time = request.Time;

			if (display != IntPtr.Zero)
			{
				// propagate False and an empty mask: ICCCM says a SelectionNotify goes to the requestor
				// whatever it has selected for, which is what an empty mask means for a sent event.
				Xlib.XSendEvent(display, request.Requestor, X11.False, X11.NoEventMask, ref reply);
				Xlib.XFlush(display);
			}
		}

		/// <summary>Writes the requested target onto the requestor's property. False means "refuse".</summary>
		private bool WriteRequestedTarget(IntPtr display, ref XSelectionRequestEvent request, ulong property)
		{
			if (request.Target == this.atoms.Targets)
			{
				ulong[] targets = BuildTargetList(this.atoms, this.ownedHtml != null);
				fixed (ulong* targetData = targets)
				{
					Xlib.XChangeProperty(
						display,
						request.Requestor,
						property,
						X11.XA_ATOM,
						32,
						X11.PropModeReplace,
						(byte*)targetData,
						targets.Length);
				}

				return true;
			}

			byte[] data = EncodeForTarget(request.Target, this.ownedText, this.ownedHtml, this.atoms, out ulong dataType);
			if (data == null)
			{
				return false;
			}

			if (data.Length > MaxPropertyBytes(display))
			{
				// The sending half of INCR is not implemented: past this size the honest answer is a
				// refusal rather than a truncated paste. The ceiling is the connection's maximum request
				// size, which BIG-REQUESTS puts in the megabytes on every server in use - far past any
				// clipboard text a user produces by hand. The receiving half *is* implemented
				// (ReadIncrementally), because other applications routinely send that way.
				Console.Error.WriteLine(
					$"X11Selection: refusing a {data.Length} byte clipboard conversion; INCR sending is not implemented.");
				return false;
			}

			// A zero-length value is legal and meaningful - an empty string was copied - but `fixed` on an
			// empty array yields a null pointer, so it needs a byte to point at that nothing reads.
			byte[] pinnable = data.Length > 0 ? data : new byte[1];
			fixed (byte* payload = pinnable)
			{
				Xlib.XChangeProperty(
					display,
					request.Requestor,
					property,
					dataType,
					8,
					X11.PropModeReplace,
					payload,
					data.Length);
			}

			return true;
		}

		/// <summary>
		/// Somebody else took the clipboard. Everything we were serving is dropped with the claim, so a
		/// later read goes back out to the new owner instead of answering from a copy that is now history.
		/// </summary>
		private void HandleSelectionClear()
		{
			this.ownsClipboard = false;
			this.ownedText = null;
			this.ownedHtml = null;

			// Whoever took it advertises its own targets, and this is the one moment a foreign clipboard
			// change is actually announced to us - so it is the one moment the cache can be invalidated
			// for the right reason rather than by expiry.
			this.cachedTargetsValid = false;
		}

		/// <summary>The most property data one <c>XChangeProperty</c> can carry on this connection.</summary>
		private static long MaxPropertyBytes(IntPtr display)
		{
			long units = Xlib.XExtendedMaxRequestSize(display);
			if (units == 0)
			{
				units = Xlib.XMaxRequestSize(display);
			}

			return Math.Max(0, (units * 4) - ChangePropertyOverheadBytes);
		}

		// -------------------------------------------------------------------------------------------
		// Reading somebody else's selection
		// -------------------------------------------------------------------------------------------

		/// <summary>
		/// Asks the owner to convert CLIPBOARD to <paramref name="target"/> and waits for the answer,
		/// pumping the event loop while it waits. Null on refusal, timeout, or no owner;
		/// <paramref name="outcome"/> is how a caller tells those apart.
		/// </summary>
		private byte[] ConvertSelection(ulong target, out ulong type, out int format, out X11ConversionOutcome outcome)
		{
			type = X11.None;
			format = 0;
			outcome = X11ConversionOutcome.Refused;

			IntPtr display = X11SystemWindow.SharedDisplay;
			if (display == IntPtr.Zero || this.window == X11.None || this.converting)
			{
				return null;
			}

			this.converting = true;
			try
			{
				this.PurgeAbandonedTransfer(display);

				// Clear the landing property first: a leftover from a conversion that timed out would
				// otherwise be read as this one's answer.
				Xlib.XDeleteProperty(display, this.window, this.atoms.Property);

				Xlib.XConvertSelection(
					display,
					this.atoms.Clipboard,
					target,
					this.atoms.Property,
					this.window,
					X11.CurrentTime);
				Xlib.XFlush(display);

				var clock = Stopwatch.StartNew();
				if (!this.PumpForSelectionNotify(display, target, clock, out XSelectionEvent notify))
				{
					outcome = X11ConversionOutcome.TimedOut;
					return null;
				}

				if (notify.Property == X11.None)
				{
					// The owner cannot produce this target. Not an error - it is how "I have no HTML"
					// is said.
					return null;
				}

				// Reading with delete=true is also the INCR handshake: deleting the property is the signal
				// that starts the transfer, so it has to happen whether or not this turns out to be one.
				if (!this.TryReadProperty(display, delete: true, out byte[] data, out type, out format))
				{
					return null;
				}

				if (type == this.atoms.Incr)
				{
					byte[] whole = this.ReadIncrementally(display, out type, out format);
					outcome = whole == null ? X11ConversionOutcome.TimedOut : X11ConversionOutcome.Answered;
					return whole;
				}

				outcome = X11ConversionOutcome.Answered;
				return data;
			}
			finally
			{
				this.converting = false;
			}
		}

		/// <summary>
		/// Takes the chunks of an INCR transfer. The owner sends a value too large for one request as a
		/// series of properties, one at a time, each announced by a PropertyNotify and acknowledged by our
		/// deleting it; a zero-length property ends the sequence.
		/// </summary>
		/// <remarks>
		/// The timeout is per chunk and is restarted by every chunk that arrives, so the budget measures
		/// <em>progress</em> and not size. One budget for the whole transfer would abandon a large paste
		/// that was arriving perfectly steadily, purely for being large.
		/// </remarks>
		private byte[] ReadIncrementally(IntPtr display, out ulong type, out int format)
		{
			type = X11.None;
			format = 0;

			var chunks = new List<byte[]>();
			var chunkClock = Stopwatch.StartNew();

			while (true)
			{
				if (!this.PumpForPropertyNotify(display, chunkClock, IncrChunkTimeoutMilliseconds))
				{
					this.AbandonIncrTransfer(display, chunks.Count, "the sender stopped mid-transfer");
					return null;
				}

				if (!this.TryReadProperty(display, delete: true, out byte[] chunk, out ulong chunkType, out int chunkFormat))
				{
					this.AbandonIncrTransfer(display, chunks.Count, "a chunk could not be read");
					return null;
				}

				if (chunk.Length == 0)
				{
					// The terminator. Its type says nothing, so the type from the chunks is what stands.
					return AssembleChunks(chunks);
				}

				type = chunkType;
				format = chunkFormat;
				chunks.Add(chunk);

				// Progress. The next chunk gets a full budget of its own.
				chunkClock.Restart();
			}
		}

		/// <summary>
		/// Ends a transfer we have given up on, as tidily as the protocol allows.
		/// </summary>
		/// <remarks>
		/// <para>
		/// The problem is that our <c>XDeleteProperty</c> is not only a tidy-up, it is the acknowledgement
		/// the sender waits on - so simply walking away leaves a sender that will push one more chunk onto
		/// our window the next time the property disappears, and that chunk would be read as the answer to
		/// whatever conversion comes next.
		/// </para>
		/// <para>
		/// So the abort first <em>drains</em>: it keeps acknowledging, bounded by
		/// <see cref="IncrDrainTimeoutMilliseconds"/> and <see cref="IncrDrainChunkLimit"/>, in the hope of
		/// reaching the sender's zero-length terminator - which ends the transfer properly and leaves
		/// nothing behind. A sender that was merely slow finishes here. Only if the drain also gives up is
		/// the transfer marked abandoned, and <see cref="PurgeAbandonedTransfer"/> then pays the cost at
		/// the start of the next conversion.
		/// </para>
		/// </remarks>
		private void AbandonIncrTransfer(IntPtr display, int chunksTaken, string why)
		{
			var overall = Stopwatch.StartNew();
			var chunkClock = Stopwatch.StartNew();

			for (int drained = 0; drained < IncrDrainChunkLimit; drained++)
			{
				if (overall.ElapsedMilliseconds >= IncrDrainTimeoutMilliseconds)
				{
					break;
				}

				int remaining = (int)Math.Max(1, IncrDrainTimeoutMilliseconds - overall.ElapsedMilliseconds);
				if (!this.PumpForPropertyNotify(display, chunkClock, remaining))
				{
					break;
				}

				if (!this.TryReadProperty(display, delete: true, out byte[] chunk, out _, out _))
				{
					break;
				}

				if (chunk.Length == 0)
				{
					// The sender finished after all. Nothing is left on the window and nothing is owed.
					return;
				}

				chunkClock.Restart();
			}

			this.incrTransferAbandoned = true;
			Console.Error.WriteLine(
				$"X11Selection: abandoned an INCR clipboard transfer after {chunksTaken} chunks ({why}); "
				+ "the next paste will clear the leftover first.");
		}

		/// <summary>
		/// Clears the wreckage of an abandoned INCR transfer before a new conversion is started.
		/// </summary>
		/// <remarks>
		/// Deleting the property is also the acknowledgement a still-running sender is waiting for, so the
		/// delete is done twice with an <c>XSync</c> between: the first lets the straggler chunk be
		/// written, the sync makes that round trip happen <em>now</em> rather than interleaved with the
		/// conversion about to start, and the second removes it. A sender that keeps going past that is
		/// indistinguishable from a hostile one; the residual risk is one stale chunk landing on the
		/// property before the new owner's reply overwrites it (<c>PropModeReplace</c>), which is why the
		/// answer is only ever read after its own SelectionNotify has arrived.
		/// </remarks>
		private void PurgeAbandonedTransfer(IntPtr display)
		{
			if (!this.incrTransferAbandoned)
			{
				return;
			}

			Xlib.XDeleteProperty(display, this.window, this.atoms.Property);
			Xlib.XSync(display, X11.False);
			Xlib.XDeleteProperty(display, this.window, this.atoms.Property);
			Xlib.XSync(display, X11.False);

			this.incrTransferAbandoned = false;
		}

		/// <summary>
		/// Runs a nested event pump until the SelectionNotify we asked for arrives or the clock runs out.
		/// Non-input events are dispatched; input is held back - see the class remarks on re-entrancy.
		/// </summary>
		private bool PumpForSelectionNotify(IntPtr display, ulong target, Stopwatch clock, out XSelectionEvent notify)
		{
			notify = default;

			while (clock.ElapsedMilliseconds < ConversionTimeoutMilliseconds)
			{
				while (Xlib.XPending(display) > 0)
				{
					Xlib.XNextEvent(display, out XEvent nextEvent);

					if (nextEvent.Type == X11.SelectionNotify
						&& nextEvent.As<XSelectionEvent>().Requestor == this.window
						&& nextEvent.As<XSelectionEvent>().Selection == this.atoms.Clipboard
						&& nextEvent.As<XSelectionEvent>().Target == target)
					{
						notify = nextEvent.As<XSelectionEvent>();
						return true;
					}

					DispatchOrDefer(ref nextEvent);
				}

				X11SystemWindow.WaitForEvents(1);
			}

			return false;
		}

		/// <summary>
		/// Runs the same nested pump until the next chunk of an INCR transfer is announced. Only
		/// PropertyNewValue counts: the deletes are the echoes of our own acknowledgements.
		/// </summary>
		private bool PumpForPropertyNotify(IntPtr display, Stopwatch clock, int timeoutMilliseconds)
		{
			while (clock.ElapsedMilliseconds < timeoutMilliseconds)
			{
				while (Xlib.XPending(display) > 0)
				{
					Xlib.XNextEvent(display, out XEvent nextEvent);

					if (nextEvent.Type == X11.PropertyNotify
						&& nextEvent.As<XPropertyEvent>().Window == this.window
						&& nextEvent.As<XPropertyEvent>().Atom == this.atoms.Property
						&& nextEvent.As<XPropertyEvent>().State == X11.PropertyNewValue)
					{
						return true;
					}

					DispatchOrDefer(ref nextEvent);
				}

				X11SystemWindow.WaitForEvents(1);
			}

			return false;
		}

		/// <summary>
		/// One event that is not the answer we are waiting for: dispatched if the application can safely
		/// see it now, queued for replay if it is input. See <see cref="IsDeferredDuringConversion"/>.
		/// </summary>
		private static void DispatchOrDefer(ref XEvent nextEvent)
		{
			if (!IsDeferredDuringConversion(nextEvent.Type))
			{
				X11SystemWindow.DispatchEvent(ref nextEvent);
				return;
			}

			if (DeferredInput.Count < DeferredInputLimit)
			{
				DeferredInput.Add(nextEvent);
				return;
			}

			if (!warnedDeferredInputOverflow)
			{
				warnedDeferredInputOverflow = true;
				Console.Error.WriteLine(
					$"X11Selection: more than {DeferredInputLimit} input events arrived during one clipboard "
					+ "round trip; the excess is being dropped.");
			}
		}

		/// <summary>
		/// Reads the landing property whole: a zero-length probe to learn the size, then as many reads as
		/// it takes to exhaust <c>bytesAfter</c>, then the delete.
		/// </summary>
		/// <remarks>
		/// The loop matters because <c>XGetWindowProperty</c> is free to return less than was asked for,
		/// and a single read that trusts the first <c>bytesAfter</c> silently truncates when it does. The
		/// delete is deliberately last rather than folded into the reads: on an INCR chunk the delete is
		/// the acknowledgement that releases the next chunk, so deleting before the current one is fully
		/// read would throw away the tail.
		/// </remarks>
		private bool TryReadProperty(IntPtr display, bool delete, out byte[] data, out ulong type, out int format)
		{
			data = null;
			type = X11.None;
			format = 0;

			int status = Xlib.XGetWindowProperty(
				display,
				this.window,
				this.atoms.Property,
				0,
				0,
				X11.False,
				X11.AnyPropertyType,
				out ulong actualType,
				out int actualFormat,
				out ulong itemCount,
				out ulong bytesAfter,
				out IntPtr prop);

			// Freed even though nothing was asked for: XGetWindowProperty allocates on every success, and
			// the zero-length read is the one everybody forgets.
			if (prop != IntPtr.Zero)
			{
				Xlib.XFree(prop);
			}

			if (status != X11.Success)
			{
				return false;
			}

			type = actualType;
			format = actualFormat;

			using var assembled = new MemoryStream();
			long offsetIn32BitUnits = 0;

			while (bytesAfter > 0)
			{
				// The offset and the length are both in 32-bit units, whatever the property's own format is.
				long remaining = (long)((bytesAfter + 3) / 4);

				status = Xlib.XGetWindowProperty(
					display,
					this.window,
					this.atoms.Property,
					offsetIn32BitUnits,
					remaining,
					X11.False,
					X11.AnyPropertyType,
					out actualType,
					out actualFormat,
					out itemCount,
					out bytesAfter,
					out prop);

				if (status != X11.Success)
				{
					if (prop != IntPtr.Zero)
					{
						Xlib.XFree(prop);
					}

					return false;
				}

				type = actualType;
				format = actualFormat;

				// A format-32 item arrives as a C long, so it is eight bytes wide here and four on the wire.
				int itemBytes = actualFormat switch
				{
					8 => 1,
					16 => 2,
					32 => sizeof(ulong),
					_ => 0,
				};

				int chunkBytes = (int)itemCount * itemBytes;
				if (chunkBytes > 0 && prop != IntPtr.Zero)
				{
					var buffer = new byte[chunkBytes];
					Marshal.Copy(prop, buffer, 0, chunkBytes);
					assembled.Write(buffer, 0, chunkBytes);
				}

				if (prop != IntPtr.Zero)
				{
					Xlib.XFree(prop);
				}

				// How far along the property this read left us, in the 32-bit units the offset counts. A
				// read that returned nothing while claiming more is left cannot be made progress on, and
				// looping on it would spin forever.
				long consumed = actualFormat == 32
					? (long)itemCount
					: ((long)itemCount * itemBytes) / 4;

				if (consumed <= 0)
				{
					break;
				}

				offsetIn32BitUnits += consumed;
			}

			if (delete)
			{
				Xlib.XDeleteProperty(display, this.window, this.atoms.Property);
			}

			data = assembled.ToArray();
			return true;
		}
	}
}
