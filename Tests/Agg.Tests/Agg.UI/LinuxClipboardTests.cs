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
using System.Text;
using System.Threading.Tasks;
using MatterHackers.Agg.Platform.Linux;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// The X11 clipboard, in the two halves that can be tested without a server: the pure conversion logic
	/// <see cref="X11Selection"/> answers a <c>SelectionRequest</c> with and decodes a paste through, and
	/// the in-process fallback <see cref="LinuxClipboard"/> uses when there is no display to reach.
	/// </summary>
	/// <remarks>
	/// <para>
	/// An atom is only ever an opaque id compared for equality, so the pure helpers take the atom set as a
	/// parameter and these tests invent their own - the code cannot tell the difference, which is what
	/// makes the encoding and target-choosing rules testable at all.
	/// </para>
	/// <para>
	/// The protocol conversation itself - owning CLIPBOARD, serving requests, INCR, and the deferred-input
	/// replay under a real paste - needs a second X client and a running event loop to be worth anything,
	/// and there is <b>no automated test of it in this repository</b>. It was verified by hand against
	/// Xvfb with <c>xclip</c> and <c>xdotool</c>; an opt-in test guarded on <c>DISPLAY</c> plus a
	/// <c>which xclip</c> probe was considered and left out, because it would have to show a window and
	/// spin the host's event loop inside the unit suite - an automation test in a suite whose rule is to
	/// prefer unit tests, and one that would go quietly dead on any machine without those two binaries.
	/// </para>
	/// </remarks>
	public class LinuxClipboardTests
	{
		/// <summary>
		/// Distinct, non-zero, and not any real interned value: the point is that nothing here can pass by
		/// accidentally matching a number the production code hard-codes.
		/// </summary>
		private static X11SelectionAtoms TestAtoms => new X11SelectionAtoms
		{
			Clipboard = 1001,
			Targets = 1002,
			Utf8String = 1003,
			String = 1004,
			Text = 1005,
			TextPlainUtf8 = 1006,
			TextHtml = 1007,
			Incr = 1008,
			Property = 1009,
		};

		/// <summary>
		/// TARGETS is itself a target - a requestor asking "what can you do" must find the question in the
		/// answer - and every text spelling we can honour has to be listed, because a requestor that walks
		/// the list will not ask for what is not in it.
		/// </summary>
		[Test]
		public async Task TheTargetListOffersEveryTextSpellingWeCanAnswer()
		{
			X11SelectionAtoms atoms = TestAtoms;
			ulong[] targets = X11Selection.BuildTargetList(atoms, hasHtml: false);

			await Assert.That(targets).Contains(atoms.Targets);
			await Assert.That(targets).Contains(atoms.Utf8String);
			await Assert.That(targets).Contains(atoms.TextPlainUtf8);
			await Assert.That(targets).Contains(atoms.Text);
			await Assert.That(targets).Contains(atoms.String);
		}

		/// <summary>
		/// text/html is advertised only when there is HTML. Offering a target and then refusing it is worse
		/// than never offering it: a requestor that believed the list has already thrown away the plain
		/// text alternative by the time the refusal arrives.
		/// </summary>
		[Test]
		public async Task HtmlIsOfferedOnlyWhenThereIsHtml()
		{
			X11SelectionAtoms atoms = TestAtoms;

			await Assert.That(X11Selection.BuildTargetList(atoms, hasHtml: false)).DoesNotContain(atoms.TextHtml);
			await Assert.That(X11Selection.BuildTargetList(atoms, hasHtml: true)).Contains(atoms.TextHtml);
		}

		/// <summary>
		/// The three UTF-8 spellings a modern toolkit asks for all answer with the same bytes, and all
		/// report UTF8_STRING as the type - including TEXT, which means "your choice of encoding, and say
		/// which", so the type it is answered with is not the target it was asked for.
		/// </summary>
		[Test]
		public async Task TheUtf8TargetsAllAnswerUtf8BytesTypedAsUtf8String()
		{
			X11SelectionAtoms atoms = TestAtoms;
			const string Copied = "hello from agg ✓";
			byte[] expected = Encoding.UTF8.GetBytes(Copied);

			foreach (ulong target in new[] { atoms.Utf8String, atoms.TextPlainUtf8, atoms.Text })
			{
				byte[] encoded = X11Selection.EncodeForTarget(target, Copied, null, atoms, out ulong type);

				await Assert.That(encoded).IsEquivalentTo(expected);
				await Assert.That(type).IsEqualTo(atoms.Utf8String);
			}
		}

		/// <summary>
		/// STRING is Latin-1 by definition, so it is answered in Latin-1 - lossily, which is what the
		/// target asked for. Answering UTF-8 bytes under an XA_STRING type would be the worse bug: the
		/// requestor would decode them as Latin-1 and paste mojibake with no way to tell.
		/// </summary>
		[Test]
		public async Task StringIsLatin1AndSaysSo()
		{
			X11SelectionAtoms atoms = TestAtoms;

			byte[] encoded = X11Selection.EncodeForTarget(atoms.String, "café ✓", null, atoms, out ulong type);

			await Assert.That(type).IsEqualTo(atoms.String);

			// 'é' is one byte in Latin-1 (0xE9) and two in UTF-8, and '✓' is not in Latin-1 at all, so it
			// substitutes: six characters in, six bytes out.
			await Assert.That(encoded.Length).IsEqualTo(6);
			await Assert.That(encoded[3]).IsEqualTo((byte)0xE9);
			await Assert.That(encoded[5]).IsEqualTo((byte)'?');
		}

		/// <summary>
		/// A target we cannot produce is refused, and refusing is not the same as answering with nothing:
		/// an empty byte array is a legitimate value (an empty string was copied), so "no" has to be a
		/// null and not a zero length.
		/// </summary>
		[Test]
		public async Task AnUnknownTargetIsRefusedAndSoIsHtmlWeDoNotHave()
		{
			X11SelectionAtoms atoms = TestAtoms;

			await Assert.That(X11Selection.EncodeForTarget(99999, "text", "<b>html</b>", atoms, out ulong unknownType)).IsNull();
			await Assert.That(unknownType).IsEqualTo(X11.None);

			await Assert.That(X11Selection.EncodeForTarget(atoms.TextHtml, "text", null, atoms, out ulong htmlType)).IsNull();
			await Assert.That(htmlType).IsEqualTo(X11.None);
		}

		/// <summary>Copying an empty string is a real thing to do, and it is not a refusal.</summary>
		[Test]
		public async Task AnEmptyStringEncodesToAnEmptyValueRatherThanARefusal()
		{
			X11SelectionAtoms atoms = TestAtoms;

			byte[] encoded = X11Selection.EncodeForTarget(atoms.Utf8String, string.Empty, null, atoms, out _);

			await Assert.That(encoded).IsNotNull();
			await Assert.That(encoded.Length).IsEqualTo(0);
		}

		/// <summary>
		/// The decode side is driven by the type the owner stamped on the property, not by the target that
		/// was asked for - an owner may answer TEXT with anything it likes. XA_STRING is the one type that
		/// is definitely Latin-1; everything else is read as UTF-8.
		/// </summary>
		[Test]
		public async Task DecodingFollowsTheTypeTheOwnerStamped()
		{
			X11SelectionAtoms atoms = TestAtoms;
			var latin1Bytes = new byte[] { (byte)'c', (byte)'a', (byte)'f', 0xE9 };

			await Assert.That(X11Selection.DecodeText(latin1Bytes, atoms.String, atoms)).IsEqualTo("café");

			// The same four bytes under a UTF-8 type are not valid UTF-8, which is exactly why the type has
			// to decide: guessing would silently corrupt one of the two cases.
			await Assert.That(X11Selection.DecodeText(latin1Bytes, atoms.Utf8String, atoms)).IsNotEqualTo("café");

			byte[] utf8Bytes = Encoding.UTF8.GetBytes("café ✓");
			await Assert.That(X11Selection.DecodeText(utf8Bytes, atoms.Utf8String, atoms)).IsEqualTo("café ✓");
			await Assert.That(X11Selection.DecodeText(utf8Bytes, atoms.TextHtml, atoms)).IsEqualTo("café ✓");
		}

		/// <summary>
		/// An INCR transfer arrives as a series of properties that have to be joined back in order. A
		/// multi-byte character split across a chunk boundary is the case that matters: decoding chunk by
		/// chunk would turn it into two replacement characters, which is why the join happens on bytes and
		/// the decode happens once at the end.
		/// </summary>
		[Test]
		public async Task ChunksAreJoinedOnBytesBeforeAnythingIsDecoded()
		{
			X11SelectionAtoms atoms = TestAtoms;
			byte[] whole = Encoding.UTF8.GetBytes("café");

			// Split mid-character: 'é' is 0xC3 0xA9, and the boundary falls between them.
			var chunks = new List<byte[]>
			{
				new[] { whole[0], whole[1], whole[2], whole[3] },
				new[] { whole[4] },
			};

			byte[] assembled = X11Selection.AssembleChunks(chunks);

			await Assert.That(assembled).IsEquivalentTo(whole);
			await Assert.That(X11Selection.DecodeText(assembled, atoms.Utf8String, atoms)).IsEqualTo("café");
		}

		/// <summary>The end of an INCR transfer is a zero-length chunk, so an empty list is a real input.</summary>
		[Test]
		public async Task JoiningNothingIsAnEmptyValue()
		{
			await Assert.That(X11Selection.AssembleChunks(Array.Empty<byte[]>()).Length).IsEqualTo(0);
		}

		/// <summary>
		/// A format-32 property is unpacked into C <c>long</c>s, so each atom in a TARGETS reply is
		/// <em>eight</em> bytes wide on LP64 even though the wire carried four. Reading it four at a time
		/// is the classic version of this bug: it finds twice as many targets, every other one zero.
		/// </summary>
		[Test]
		public async Task AnAtomListIsReadAsEightByteItems()
		{
			var raw = new byte[3 * sizeof(ulong)];
			BitConverter.TryWriteBytes(raw.AsSpan(0), 1003UL);
			BitConverter.TryWriteBytes(raw.AsSpan(sizeof(ulong)), 1007UL);
			BitConverter.TryWriteBytes(raw.AsSpan(2 * sizeof(ulong)), 1002UL);

			ulong[] parsed = X11Selection.ParseAtomList(raw);

			await Assert.That(parsed.Length).IsEqualTo(3);
			await Assert.That(parsed[0]).IsEqualTo(1003UL);
			await Assert.That(parsed[1]).IsEqualTo(1007UL);
			await Assert.That(parsed[2]).IsEqualTo(1002UL);
		}

		/// <summary>
		/// The read side takes whatever spelling the owner actually has, best first. An old or minimal
		/// client that offers only <c>STRING</c> still has text on its clipboard; asking for UTF8_STRING
		/// and giving up would report it as empty and paste nothing.
		/// </summary>
		[Test]
		public async Task TheBestOfferedTextSpellingIsTheOneAskedFor()
		{
			X11SelectionAtoms atoms = TestAtoms;

			// Everything on offer: the UTF-8 one wins.
			await Assert.That(X11Selection.ChooseTextTarget(
				new[] { atoms.Targets, atoms.String, atoms.Text, atoms.TextPlainUtf8, atoms.Utf8String }, atoms))
				.IsEqualTo(atoms.Utf8String);

			// No UTF8_STRING: TEXT is next, because it lets the owner name its own encoding.
			await Assert.That(X11Selection.ChooseTextTarget(new[] { atoms.Targets, atoms.String, atoms.Text }, atoms))
				.IsEqualTo(atoms.Text);

			// Latin-1 only, which is still text.
			await Assert.That(X11Selection.ChooseTextTarget(new[] { atoms.Targets, atoms.String }, atoms))
				.IsEqualTo(atoms.String);
		}

		/// <summary>
		/// An owner with no text at all - an image on the clipboard, say - has to come back as
		/// <c>None</c> rather than as a target that would then be refused. So does an owner we could not
		/// get a list out of, which is the caller's cue to try the spellings directly instead.
		/// </summary>
		[Test]
		public async Task AClipboardWithNoTextOffersNoTextTarget()
		{
			X11SelectionAtoms atoms = TestAtoms;

			await Assert.That(X11Selection.ChooseTextTarget(new[] { atoms.Targets, atoms.TextHtml }, atoms))
				.IsEqualTo(X11.None);
			await Assert.That(X11Selection.ChooseTextTarget(Array.Empty<ulong>(), atoms)).IsEqualTo(X11.None);
			await Assert.That(X11Selection.ChooseTextTarget(null, atoms)).IsEqualTo(X11.None);
		}

		/// <summary>
		/// The events held back while a clipboard conversion is outstanding. These are exactly the ones
		/// that re-enter widget input handling, and dispatching them inline breaks the two callers this
		/// whole mechanism exists for: <c>InternalTextEditWidget.PasteFromClipboard</c> snapshots its text,
		/// reads the clipboard and writes the result back, so a KeyPress delivered in the middle is
		/// overwritten by the write-back; and a ButtonRelease would run the mouse-up path - pointer grab
		/// included - underneath an outer OnMouseDown that has not returned.
		/// </summary>
		[Test]
		public async Task InputIsHeldBackAcrossAConversion()
		{
			foreach (int input in new[]
			{
				X11.KeyPress, X11.KeyRelease, X11.ButtonPress, X11.ButtonRelease,
				X11.MotionNotify, X11.EnterNotify, X11.LeaveNotify,
			})
			{
				await Assert.That(X11Selection.IsDeferredDuringConversion(input)).IsTrue();
			}
		}

		/// <summary>
		/// Everything else keeps flowing. A window stalled on a wedged clipboard owner must still repaint,
		/// still resize, and still answer WM_DELETE_WINDOW - holding those back for the length of a paste
		/// is how an application looks hung when it is merely waiting.
		/// </summary>
		[Test]
		public async Task RepaintAndWindowManagementAreNotHeldBack()
		{
			foreach (int passthrough in new[]
			{
				X11.Expose, X11.ConfigureNotify, X11.ClientMessage, X11.DestroyNotify,
				X11.FocusIn, X11.FocusOut, X11.MappingNotify, X11.PropertyNotify, X11.SelectionRequest,
			})
			{
				await Assert.That(X11Selection.IsDeferredDuringConversion(passthrough)).IsFalse();
			}
		}

		// -------------------------------------------------------------------------------------------
		// The in-process fallback. A test run has no X display, so X11Selection.TryGet answers null and
		// LinuxClipboard is these strings and nothing else - which is also the shape the answers keep when
		// there *is* a display and this process owns the selection, so these hold either way.
		// -------------------------------------------------------------------------------------------

		/// <summary>Copy then paste, which is the whole point.</summary>
		[Test]
		public async Task TextRoundTripsThroughTheFallback()
		{
			var clipboard = new LinuxClipboard();

			clipboard.SetText("hello ✓");

			await Assert.That(clipboard.ContainsText).IsTrue();
			await Assert.That(clipboard.GetText()).IsEqualTo("hello ✓");
		}

		/// <summary>
		/// The <c>!= null</c> rather than <c>!IsNullOrEmpty</c> parity with <c>MacClipboard</c>: an empty
		/// string on the clipboard is a thing that was copied, and a clipboard with nothing on it is not.
		/// Folding the two together would make copying an empty selection behave differently here than on
		/// the other two hosts.
		/// </summary>
		[Test]
		public async Task AnEmptyCopyIsPresentAndNoCopyAtAllIsNot()
		{
			var clipboard = new LinuxClipboard();

			await Assert.That(clipboard.ContainsText).IsFalse();
			await Assert.That(clipboard.ContainsHtml).IsFalse();

			clipboard.SetText(string.Empty);

			await Assert.That(clipboard.ContainsText).IsTrue();
			await Assert.That(clipboard.GetText()).IsEqualTo(string.Empty);
		}

		/// <summary>
		/// Writing plain text drops the HTML flavor, the way <c>clearContents</c> does on the mac.
		/// Otherwise a later GetHtml answers with HTML from an older, unrelated copy - and against a real
		/// display it would also go on advertising a text/html target that can no longer be honoured.
		/// </summary>
		[Test]
		public async Task WritingPlainTextClearsTheHtmlFlavor()
		{
			var clipboard = new LinuxClipboard();

			clipboard.SetTextAndHtml("bold", "<b>bold</b>");

			await Assert.That(clipboard.ContainsHtml).IsTrue();
			await Assert.That(clipboard.GetHtml()).IsEqualTo("<b>bold</b>");

			clipboard.SetText("plain");

			await Assert.That(clipboard.ContainsHtml).IsFalse();
			await Assert.That(clipboard.GetHtml()).IsEqualTo(string.Empty);
			await Assert.That(clipboard.GetText()).IsEqualTo("plain");
		}

		/// <summary>
		/// Images and file drops are declined rather than faked, matching the mac host. A getter that
		/// answers where the Contains says no is how a caller ends up pasting a blank image.
		/// </summary>
		[Test]
		public async Task ImagesAndFileDropsAreDeclinedRatherThanFaked()
		{
			var clipboard = new LinuxClipboard();

			await Assert.That(clipboard.ContainsImage).IsFalse();
			await Assert.That(clipboard.GetImage()).IsNull();
			await Assert.That(clipboard.ContainsFileDropList).IsFalse();
			await Assert.That(clipboard.GetFileDropList().Count).IsEqualTo(0);
		}
	}
}
