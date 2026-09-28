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
	/// <summary>
	/// Carries the modifier down state a browser window's events report into the process-wide
	/// <see cref="Keyboard"/>, remembering which modifiers this window put down so it can release exactly those.
	/// </summary>
	/// <remarks>
	/// Releasing narrowly, where a Keyboard.Clear() would not be: Keyboard is process-wide and other callers
	/// write to it directly (an automation test sets Shift down and then shift-clicks), so releasing only what
	/// this window applied cannot reach anything it did not put there. Same rule as
	/// MacSystemWindow.ReleaseAppliedModifierKeys.
	/// </remarks>
	internal sealed class BrowserModifierTracker
	{
		/// <summary>The three keys <c>Keyboard</c> tracks a modifier's down state under.</summary>
		private static readonly Keys[] ModifierStateKeys = { Keys.ShiftKey, Keys.ControlKey, Keys.Menu };

		private readonly HashSet<Keys> appliedModifierKeys = new HashSet<Keys>();

		/// <summary>
		/// Puts the modifier down state an event reported into <see cref="Keyboard"/>.
		/// </summary>
		/// <remarks>
		/// Every modifier is written on every call, including the ones being released: SetKeyDownState is
		/// idempotent and only raises StateChanged on a real change, and the browser tells us what is held on
		/// every input event rather than only on key events - which is how a modifier pressed while the pointer
		/// is moving is noticed at all.
		/// </remarks>
		public void Apply(IReadOnlySet<Keys> modifierDownKeys)
		{
			if (modifierDownKeys == null)
			{
				return;
			}

			foreach (Keys modifierKey in ModifierStateKeys)
			{
				bool down = modifierDownKeys.Contains(modifierKey);
				Keyboard.SetKeyDownState(modifierKey, down);
				if (down)
				{
					this.appliedModifierKeys.Add(modifierKey);
				}
				else
				{
					this.appliedModifierKeys.Remove(modifierKey);
				}
			}
		}

		/// <summary>
		/// Releases the modifiers the page held when it lost focus: one released while the page was not
		/// looking sends no event at all, and would otherwise be reported as held forever.
		/// </summary>
		public void Release(IEnumerable<Keys> heldWhenFocusLeft)
		{
			foreach (Keys modifierKey in heldWhenFocusLeft)
			{
				Keyboard.SetKeyDownState(modifierKey, false);
			}

			this.appliedModifierKeys.Clear();
		}

		/// <summary>
		/// Releases everything this window put down. For a window closing: its input is detached, so the
		/// release of a modifier it reported held can never arrive, and Keyboard outlives the window.
		/// </summary>
		public void ReleaseAll() => this.Release(new List<Keys>(this.appliedModifierKeys));
	}
}
