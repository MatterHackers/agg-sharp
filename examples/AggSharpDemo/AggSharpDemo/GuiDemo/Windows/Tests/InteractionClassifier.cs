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
using System.Collections.Generic;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Tests
{
	/// <summary>
	/// The consumption profile of an Input Test probe (agg-gui's ProbeKind, its counterpart of an egui Sense).
	/// </summary>
	public enum ProbeKind
	{
		/// <summary>Handles nothing; only reports contains-pointer and hover enter/leave.</summary>
		Hover,

		/// <summary>Handles press/release; classifies click / double / triple click.</summary>
		Click,

		/// <summary>Handles the press (so it keeps the pointer); reports drag start/move/stop.</summary>
		Drag,

		/// <summary>Both: clicks and drags.</summary>
		ClickAndDrag,
	}

	public enum InteractionType
	{
		HoverEnter,
		HoverLeave,
		Click,
		DragStarted,
		Dragged,
		DragStopped,
	}

	/// <summary>
	/// One classified interaction. <see cref="Count"/> is set for clicks (1 = click, 2 = double, 3+ = triple);
	/// <see cref="Dx"/>/<see cref="Dy"/> are the incremental delta of a <see cref="InteractionType.Dragged"/>.
	/// </summary>
	public readonly record struct Interaction(InteractionType Type, MouseButtons Button = MouseButtons.None, int Count = 0, double Dx = 0, double Dy = 0)
	{
		public static Interaction HoverEnter => new Interaction(InteractionType.HoverEnter);

		public static Interaction HoverLeave => new Interaction(InteractionType.HoverLeave);

		public bool IsHover => this.Type == InteractionType.HoverEnter || this.Type == InteractionType.HoverLeave;
	}

	/// <summary>
	/// agg-gui's InteractionClassifier (tests/basic/input_probe/classifier.rs): turns discrete pointer callbacks
	/// plus a monotonic millisecond clock into clicks (with double/triple counts), drag start/move/stop and hover
	/// enter/leave. It holds no widget or clock, so tests drive it deterministically.
	/// </summary>
	public sealed class InteractionClassifier
	{
		/// <summary>Maximum gap between two clicks (ms) for the second to extend the double/triple sequence.</summary>
		public const double DoubleClickMs = 400;

		/// <summary>Maximum travel for a press-release to still be a click, and for two clicks to be "the same spot".</summary>
		public const double MaxClickDistance = 6;

		/// <summary>Travel past which a press becomes a drag; the same tolerance, so a gesture is exactly one of the two.</summary>
		public const double DragThreshold = 6;

		private static readonly Interaction[] None = Array.Empty<Interaction>();

		private Press press;
		private LastClick lastClick;
		private bool inside;

		public InteractionClassifier(ProbeKind kind)
		{
			this.Kind = kind;
		}

		public ProbeKind Kind { get; }

		/// <summary>True while a button is held, i.e. a gesture is in progress.</summary>
		public bool IsPressed => this.press != null;

		private bool TracksDrag => this.Kind == ProbeKind.Drag || this.Kind == ProbeKind.ClickAndDrag;

		/// <summary>Short human title shown at the top of a probe.</summary>
		public static string Title(ProbeKind kind)
		{
			switch (kind)
			{
				case ProbeKind.Hover: return "Hover";
				case ProbeKind.Click: return "Click";
				case ProbeKind.Drag: return "Drag";
				default: return "Click + Drag";
			}
		}

		/// <summary>One line on what the probe does with mouse events, in agg-sharp terms.</summary>
		public static string Profile(ProbeKind kind)
		{
			switch (kind)
			{
				case ProbeKind.Hover: return "never handles the mouse";
				case ProbeKind.Click: return "handles press/release";
				case ProbeKind.Drag: return "handles press; keeps the pointer";
				default: return "handles press/release + drag";
			}
		}

		/// <summary>
		/// The history text of an interaction: <c>Summary</c> is the dedup key (so repeated drags coalesce with an
		/// ×N counter) and <c>Full</c> adds the drag delta.
		/// </summary>
		public static (string Summary, string Full) Describe(Interaction interaction)
		{
			string suffix = ButtonSuffix(interaction.Button);
			switch (interaction.Type)
			{
				case InteractionType.HoverEnter:
					return ("Hover enter", "Hover enter");

				case InteractionType.HoverLeave:
					return ("Hover leave", "Hover leave");

				case InteractionType.Click:
					string click = ClickLabel(interaction.Count) + suffix;
					return (click, click);

				case InteractionType.DragStarted:
					return ("Drag started" + suffix, "Drag started" + suffix);

				case InteractionType.Dragged:
					string dragged = "Dragged" + suffix;
					return (dragged, $"{dragged} (Δ{interaction.Dx:0}, {interaction.Dy:0})");

				default:
					return ("Drag stopped" + suffix, "Drag stopped" + suffix);
			}
		}

		/// <summary>1 is "Clicked", 2 "Double-clicked", 3 and up "Triple-clicked" (egui's wording).</summary>
		public static string ClickLabel(int count)
		{
			return count <= 1 ? "Clicked" : count == 2 ? "Double-clicked" : "Triple-clicked";
		}

		/// <summary>egui's " by {button} button" suffix, left out for the primary button to cut clutter.</summary>
		public static string ButtonSuffix(MouseButtons button)
		{
			switch (button)
			{
				case MouseButtons.Left:
				case MouseButtons.None:
					return string.Empty;

				case MouseButtons.Middle:
					return " by Middle button";

				case MouseButtons.Right:
					return " by Right button";

				default:
					return $" by Other({button}) button";
			}
		}

		/// <summary>A button went down inside the probe. Records the press (hover ignores it); never an interaction itself.</summary>
		public IReadOnlyList<Interaction> OnDown(MouseButtons button, Vector2 position, double nowMs)
		{
			if (this.Kind != ProbeKind.Hover)
			{
				this.press = new Press { Button = button, Origin = position, Last = position };
			}

			return None;
		}

		/// <summary>
		/// The pointer moved to <paramref name="position"/>, <paramref name="inside"/> the probe or not. Hover
		/// enter/leave only fire while no button is held, so a captured drag that leaves the bounds does not spam them.
		/// </summary>
		public IReadOnlyList<Interaction> OnMove(Vector2 position, bool inside, double nowMs)
		{
			var result = new List<Interaction>();
			if (this.press == null)
			{
				if (inside && !this.inside)
				{
					this.inside = true;
					result.Add(Interaction.HoverEnter);
				}
				else if (!inside && this.inside)
				{
					this.inside = false;
					result.Add(Interaction.HoverLeave);
				}

				return result;
			}

			if (this.TracksDrag)
			{
				if (!this.press.Dragging && (position - this.press.Origin).Length > DragThreshold)
				{
					this.press.Dragging = true;

					// A drag breaks any click sequence.
					this.lastClick = null;
					result.Add(new Interaction(InteractionType.DragStarted, this.press.Button));
				}

				if (this.press.Dragging)
				{
					Vector2 delta = position - this.press.Last;
					result.Add(new Interaction(InteractionType.Dragged, this.press.Button, Dx: delta.X, Dy: delta.Y));
				}
			}

			this.press.Last = position;
			return result;
		}

		/// <summary>A button came up at <paramref name="position"/>: completes a click or a drag, as the kind allows.</summary>
		public IReadOnlyList<Interaction> OnUp(MouseButtons button, Vector2 position, double nowMs)
		{
			// Only the one held button's gesture is tracked; another button's release leaves it intact.
			if (this.press == null || this.press.Button != button)
			{
				return None;
			}

			Press ended = this.press;
			this.press = null;
			switch (this.Kind)
			{
				case ProbeKind.Drag:
					return ended.Dragging ? new[] { new Interaction(InteractionType.DragStopped, button) } : None;

				case ProbeKind.ClickAndDrag:
					return new[] { ended.Dragging ? new Interaction(InteractionType.DragStopped, button) : this.ClassifyClick(button, position, nowMs) };

				case ProbeKind.Click:
					// Only a press-release that stayed within tolerance is a click.
					return (ended.Origin - position).Length <= MaxClickDistance ? new[] { this.ClassifyClick(button, position, nowMs) } : None;

				default:
					return None;
			}
		}

		private Interaction ClassifyClick(MouseButtons button, Vector2 position, double nowMs)
		{
			int count = 1;
			if (this.lastClick != null
				&& this.lastClick.Button == button
				&& nowMs - this.lastClick.TimeMs < DoubleClickMs
				&& (this.lastClick.Position - position).Length <= MaxClickDistance)
			{
				count = this.lastClick.Count + 1;
			}

			this.lastClick = new LastClick { Button = button, TimeMs = nowMs, Position = position, Count = count };
			return new Interaction(InteractionType.Click, button, count);
		}

		private sealed class Press
		{
			public MouseButtons Button;
			public Vector2 Origin;
			public Vector2 Last;

			/// <summary>Set once the pointer has travelled past <see cref="DragThreshold"/>.</summary>
			public bool Dragging;
		}

		private sealed class LastClick
		{
			public MouseButtons Button;
			public double TimeMs;
			public Vector2 Position;
			public int Count;
		}
	}
}
