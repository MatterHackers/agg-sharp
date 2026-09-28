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
using System.Linq;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.UI
{
	/// <summary>When an <see cref="AnchoredPopup"/> dismisses itself in response to clicks (egui's PopupCloseBehavior).</summary>
	public enum PopupCloseBehavior
	{
		/// <summary>Close on any click, inside the popup or out - what a menu wants.</summary>
		CloseOnClick,

		/// <summary>Close on a click outside the popup; clicks inside keep it open for its own controls.</summary>
		CloseOnClickOutside,

		/// <summary>Never close on a click; only the opener (or Escape) closes it.</summary>
		IgnoreClicks
	}

	/// <summary>What a mouse down forwarded to <see cref="AnchoredPopup.OnMouseDown"/> did.</summary>
	public readonly struct PopupClickOutcome
	{
		public PopupClickOutcome(bool inside, bool closed)
		{
			this.Inside = inside;
			this.Closed = closed;
		}

		/// <summary>The press landed on the placed popup.</summary>
		public bool Inside { get; }

		/// <summary>The press closed the popup.</summary>
		public bool Closed { get; }

		/// <summary>The host should not pass the press on - it was on the popup, or it closed it (so a click on the
		/// opener that closes the popup does not also reopen it).</summary>
		public bool Consumed => this.Inside || this.Closed;
	}

	/// <summary>
	/// The open state, placement and close behavior of one popup, with no widget of its own - the host shows
	/// whatever content it likes at <see cref="Rect"/> and forwards presses and Escape here. Ported from agg-gui's
	/// Popup (agg-gui/src/widgets/popup/behavior.rs). Rich nested content belongs in a <see cref="PopupMenu"/>.
	/// </summary>
	public class AnchoredPopup
	{
		/// <summary>Descriptions of each behavior, as egui's Popups demo shows them in hover tooltips.</summary>
		public static readonly IReadOnlyList<(PopupCloseBehavior Behavior, string Label, string Description)> CloseBehaviors = new[]
		{
			(PopupCloseBehavior.CloseOnClick, "CloseOnClick", "Closes when the user clicks anywhere (inside or outside)"),
			(PopupCloseBehavior.CloseOnClickOutside, "CloseOnClickOutside", "Closes when the user clicks outside the popup"),
			(PopupCloseBehavior.IgnoreClicks, "IgnoreClicks", "Close only when the button is clicked again"),
		};

		public bool IsOpen { get; set; }

		/// <summary>The opener's bounds, in the space the popup is placed and hit tested in.</summary>
		public RectangleDouble Anchor { get; set; }

		/// <summary>The popup's size.</summary>
		public Vector2 Size { get; set; }

		public RectAlign Align { get; set; } = RectAlign.BottomStart;

		/// <summary>Pixels between the anchor and the popup.</summary>
		public double Gap { get; set; } = 4;

		public PopupCloseBehavior CloseBehavior { get; set; } = PopupCloseBehavior.CloseOnClick;

		/// <summary>Flips the open state and returns the new one.</summary>
		public bool Toggle()
		{
			this.IsOpen = !this.IsOpen;
			return this.IsOpen;
		}

		/// <summary>
		/// The placement actually used: <see cref="Align"/> when the popup fits the viewport that way, else the first
		/// of its mirror images and then <see cref="RectAlign.MenuAligns"/> that does.
		/// </summary>
		public RectAlign EffectiveAlign(Vector2 viewport)
		{
			var content = new RectangleDouble(0, 0, viewport.X, viewport.Y);
			IEnumerable<RectAlign> candidates = new[] { this.Align }.Concat(this.Align.Symmetries()).Concat(RectAlign.MenuAligns);
			return RectAlign.FindBestAlign(candidates, content, this.Anchor, this.Gap, this.Size);
		}

		/// <summary>Where the popup goes: the best fitting placement, clamped into the viewport if even that overflows.</summary>
		public RectangleDouble Rect(Vector2 viewport)
		{
			return RectAlign.Clamp(this.EffectiveAlign(viewport).PlaceChild(this.Anchor, this.Size, this.Gap), viewport);
		}

		public bool Contains(Vector2 position, Vector2 viewport)
		{
			return this.Rect(viewport).Contains(position);
		}

		/// <summary>Escape closes any popup whatever its close behavior, so IgnoreClicks stays keyboard dismissable.
		/// True when this closed it.</summary>
		public bool OnEscape()
		{
			bool wasOpen = this.IsOpen;
			this.IsOpen = false;
			return wasOpen;
		}

		/// <summary>Applies the close behavior to a press at <paramref name="position"/>. Nothing happens while closed.</summary>
		public PopupClickOutcome OnMouseDown(Vector2 position, Vector2 viewport)
		{
			if (!this.IsOpen)
			{
				return default;
			}

			bool inside = this.Contains(position, viewport);
			bool closed = this.CloseBehavior == PopupCloseBehavior.CloseOnClick
				|| (this.CloseBehavior == PopupCloseBehavior.CloseOnClickOutside && !inside);
			if (closed)
			{
				this.IsOpen = false;
			}

			return new PopupClickOutcome(inside, closed);
		}
	}
}
