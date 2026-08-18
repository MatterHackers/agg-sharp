/*
Copyright (c) 2026, Lars Brubaker, John Lewin
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

namespace MatterHackers.Agg.UI
{
	public static class Keyboard
	{
		private static readonly HashSet<Keys> downStates = new HashSet<Keys>();

		/// <summary>
		/// Guards <see cref="downStates"/>. This is process-wide state written from more than one thread:
		/// a platform window writes it from the UI thread while an automation test writes it from the test
		/// thread, and <see cref="ClearNonModifierKeys"/> <em>enumerates</em> the set while removing, which
		/// a concurrent add turns into an InvalidOperationException or a corrupt bucket rather than merely
		/// a torn read.
		/// </summary>
		private static readonly object downStatesLock = new object();

		/// <summary>
		/// Every key that counts as a modifier, in all of the spellings <see cref="SetKeyDownState"/> can
		/// put into the down state - the physical keys, their left/right variants, and the combined
		/// Shift/Control/Alt flags it fans out to. Used only by <see cref="ClearNonModifierKeys"/>.
		/// </summary>
		private static readonly HashSet<Keys> ModifierKeys = new HashSet<Keys>()
		{
			Keys.Shift, Keys.ShiftKey, Keys.LShiftKey, Keys.RShiftKey,
			Keys.Control, Keys.ControlKey, Keys.LControlKey, Keys.RControlKey,
			Keys.Alt, Keys.Menu, Keys.LMenu, Keys.RMenu,
			Keys.LWin, Keys.RWin,
		};

		public static event EventHandler StateChanged;

		public static bool IsKeyDown(Keys key)
		{
			lock (downStatesLock)
			{
				return downStates.Contains(key);
			}
		}

		/// <summary>
		/// Puts a key into (or out of) the down state, fanning a physical modifier out to the combined
		/// Shift/Control/Alt flag that most callers actually read.
		/// </summary>
		/// <remarks>
		/// Idempotent, and <see cref="StateChanged"/> is raised only when a down state genuinely moved.
		/// That matters because the state is fed from sources that repeat themselves - macOS delivers a
		/// flags change for modifiers agg has no notion of (caps lock, fn) and every one of them restates
		/// the modifiers agg does know - and because it lets callers write the state they want
		/// unconditionally instead of each inventing its own "has this changed?" test. Those tests are
		/// where the bugs live: comparing only <see cref="Keys.ControlKey"/> cannot see a latch left on
		/// the fanned-out <see cref="Keys.Control"/>, which is the spelling automation writes.
		/// </remarks>
		public static void SetKeyDownState(Keys key, bool down)
		{
			bool changed;

			lock (downStatesLock)
			{
				changed = SaveKeyState(key, down);
				switch(key)
				{
					case Keys.LControlKey:
					case Keys.RControlKey:
					case Keys.ControlKey:
						changed |= SaveKeyState(Keys.Control, down);
						break;

					case Keys.LShiftKey:
					case Keys.RShiftKey:
					case Keys.ShiftKey:
						changed |= SaveKeyState(Keys.Shift, down);
						break;

					case Keys.Menu:
						changed |= SaveKeyState(Keys.Alt, down);
						break;
				}
			}

			// Outside the lock: a subscriber can do anything, including take locks of its own or come back
			// into Keyboard, and holding this one across that call is a deadlock waiting to happen.
			if (changed)
			{
				StateChanged?.Invoke(null, null);
			}
		}

		/// <summary>Call under <see cref="downStatesLock"/>. Returns whether the state actually moved.</summary>
		private static bool SaveKeyState(Keys key, bool down)
		{
			return down
				? downStates.Add(key)
				: downStates.Remove(key);
		}

		public static void Clear()
		{
			bool changed;

			lock (downStatesLock)
			{
				changed = downStates.Count > 0;
				downStates.Clear();
			}

			if (changed)
			{
				StateChanged?.Invoke(null, null);
			}
		}

		/// <summary>
		/// Releases every non-modifier key, leaving Shift, Control, Alt and the Windows/Command keys
		/// exactly as they were.
		/// </summary>
		/// <remarks>
		/// This exists for macOS. AppKit does not deliver <c>keyUp:</c> for an ordinary key while Command
		/// is held, so a Cmd+A sets Keys.A down and no up event ever arrives to clear it - the key stays
		/// latched for the life of the process. The mac platform layer calls this the moment the Command
		/// flag drops, which is the one point at which it knows the suppressed ups are never coming.
		/// <para/>
		/// <see cref="Clear"/> is too blunt for that job: it would also drop the modifier state the
		/// platform layer just derived from the live modifier flags, which is precisely the state the
		/// user is still holding.
		/// <para/>
		/// It can fire early, and that is accepted. Hold Command, hold A, then release Command while A is
		/// still physically down: A is released here even though the user has not let go of it, and
		/// because the mac key-up path only forwards an up for a key agg believes is down, the widget
		/// never sees OnKeyUp for that A at all. Releasing a fraction of a second early is the better half
		/// of the trade against a key that stays latched for the life of the process.
		/// </remarks>
		public static void ClearNonModifierKeys()
		{
			bool changed;

			lock (downStatesLock)
			{
				changed = downStates.RemoveWhere(key => !ModifierKeys.Contains(key)) > 0;
			}

			if (changed)
			{
				StateChanged?.Invoke(null, null);
			}
		}
	}
}
