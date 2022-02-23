/*
Copyright (c) 2018, Lars Brubaker, John Lewin
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
		private static HashSet<Keys> downStates = new HashSet<Keys>();

		public static event EventHandler StateChanged;

		public static bool IsKeyDown(Keys key) => downStates.Contains(key);

		public static void SetKeyDownState(Keys key, bool down)
		{
			SaveKeyState(key, down);
			switch(key)
			{
				case Keys.LControlKey:
				case Keys.RControlKey:
				case Keys.ControlKey:
					SaveKeyState(Keys.Control, down);
					break;

				case Keys.LShiftKey:
				case Keys.RShiftKey:
				case Keys.ShiftKey:
					SaveKeyState(Keys.Shift, down);
					break;

				case Keys.Menu:
					SaveKeyState(Keys.Alt, down);
					break;
			}

			StateChanged?.Invoke(null, null);
		}

		private static void SaveKeyState(Keys key, bool down)
		{
			if (down)
			{
				downStates.Add(key);
			}
			else
			{
				downStates.Remove(key);
			}
		}

		public static void Clear()
		{
			if (downStates.Count > 0)
			{
				downStates.Clear();
				StateChanged?.Invoke(null, null);
			}
		}
	}
}
