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
using MatterHackers.Agg.UI;

namespace MatterHackers.Agg.Platform.Browser
{
	/// <summary>What a queued browser event turns into when it is delivered to the agg window.</summary>
	public enum BrowserInputEventKind
	{
		/// <summary>A button went down inside the canvas.</summary>
		MouseDown,

		/// <summary>A button came up, or the browser cancelled the pointer.</summary>
		MouseUp,

		/// <summary>The pointer moved - hovering or dragging - or left the canvas.</summary>
		MouseMove,

		/// <summary>A wheel, a two-finger scroll, or a pinch.</summary>
		MouseWheel,

		/// <summary>A key went down. Carries the text it typed, when it typed any.</summary>
		KeyDown,

		/// <summary>A key came up.</summary>
		KeyUp,

		/// <summary>The page lost focus, so whatever was held has to be let go of.</summary>
		FocusLost,

		/// <summary>The canvas's backing store changed size or scale.</summary>
		BackingSizeChanged,
	}

	/// <summary>
	/// One browser event, already translated into agg's terms, waiting for the next tick to deliver it.
	/// </summary>
	/// <remarks>
	/// The translation happens where the event arrives rather than where it is delivered, which is what makes
	/// the queue safe to hold across a frame: a pointer position is converted against the canvas size that was
	/// current when the pointer was there, so a resize landing in the same tick cannot retro-fit a click onto
	/// the wrong pixel. The queue exists at all because agg expects to be driven from one place - a DOM
	/// listener that called straight into <c>OnMouseDown</c> would run widget code (and its layout, and its
	/// idle work) at an arbitrary point between frames, which is neither what the desktop hosts do nor
	/// something a paint can be scheduled around.
	/// </remarks>
	public sealed class BrowserInputEvent
	{
		private BrowserInputEvent(
			BrowserInputEventKind kind,
			MouseEventArgs mouseEvent,
			KeyEventArgs keyEvent,
			string typedText,
			IReadOnlySet<Keys> modifierDownKeys,
			BrowserBackingSize backingSize)
		{
			this.Kind = kind;
			this.MouseEvent = mouseEvent;
			this.KeyEvent = keyEvent;
			this.TypedText = typedText;
			this.ModifierDownKeys = modifierDownKeys;
			this.BackingSize = backingSize;
		}

		public BrowserInputEventKind Kind { get; }

		/// <summary>The agg mouse event, for the four mouse kinds. Null otherwise.</summary>
		public MouseEventArgs MouseEvent { get; }

		/// <summary>The agg key event, for the two key kinds. Null otherwise.</summary>
		public KeyEventArgs KeyEvent { get; }

		/// <summary>
		/// The character a key down typed, or null when it typed none (a named key, or a chord that is a
		/// shortcut rather than text). Taken from <c>KeyboardEvent.key</c>, which is the layout- and
		/// dead-key-resolved spelling - the same thing <c>-[NSEvent characters]</c> is on the mac host, and
		/// not the physical <c>code</c> the agg <see cref="Keys"/> value comes from.
		/// </summary>
		public string TypedText { get; }

		/// <summary>
		/// Which modifier keys the event said were held, in <c>Keyboard</c>'s spelling. Applied at delivery so
		/// agg's process-wide down state agrees with the event a widget is being given; see
		/// <see cref="BrowserKeyboard.ModifierDownStateKeys"/> for why it is a set and not a flags value.
		/// </summary>
		public IReadOnlySet<Keys> ModifierDownKeys { get; }

		/// <summary>The new backing store size, for <see cref="BrowserInputEventKind.BackingSizeChanged"/>.</summary>
		public BrowserBackingSize BackingSize { get; }

		public static BrowserInputEvent Mouse(
			BrowserInputEventKind kind,
			MouseEventArgs mouseEvent,
			IReadOnlySet<Keys> modifierDownKeys)
			=> new BrowserInputEvent(kind, mouseEvent, null, null, modifierDownKeys, default(BrowserBackingSize));

		public static BrowserInputEvent Key(
			BrowserInputEventKind kind,
			KeyEventArgs keyEvent,
			string typedText,
			IReadOnlySet<Keys> modifierDownKeys)
			=> new BrowserInputEvent(kind, null, keyEvent, typedText, modifierDownKeys, default(BrowserBackingSize));

		public static BrowserInputEvent FocusLost(IReadOnlySet<Keys> modifierDownKeys)
			=> new BrowserInputEvent(
				BrowserInputEventKind.FocusLost, null, null, null, modifierDownKeys, default(BrowserBackingSize));

		public static BrowserInputEvent BackingSizeChanged(BrowserBackingSize backingSize)
			=> new BrowserInputEvent(BrowserInputEventKind.BackingSizeChanged, null, null, null, null, backingSize);
	}
}
