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

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// The user's primary input device, as agg-gui's input_profile.rs has it. Distinct from the OS: a Mac with a
	/// touch screen and an iPad differ here, not in their platform. It drives features that belong only on touch
	/// devices, today the <see cref="SoftwareKeyboard"/> and the look it takes.
	/// </summary>
	public enum InputProfile
	{
		/// <summary>A physical keyboard and a precise pointer. The default: no on-screen keyboard.</summary>
		Desktop,

		/// <summary>iPhone or iPad: an on-screen keyboard in iOS's light chrome.</summary>
		MobileIOS,

		/// <summary>An Android phone or tablet: an on-screen keyboard in Material's dark chrome.</summary>
		MobileAndroid,
	}

	/// <summary>
	/// The process-wide <see cref="InputProfile"/>. It starts at <see cref="InputProfile.Desktop"/>, so an application
	/// that never sets it (MatterCAD among them) sees no on-screen keyboard. A platform host that detects a phone,
	/// or a demo that pretends to be one, sets <see cref="Current"/>.
	/// </summary>
	public static class InputProfiles
	{
		private static InputProfile current = InputProfile.Desktop;

		/// <summary>Raised after <see cref="Current"/> changes.</summary>
		public static event EventHandler Changed;

		public static InputProfile Current
		{
			get => current;
			set
			{
				if (current != value)
				{
					current = value;
					Changed?.Invoke(null, EventArgs.Empty);
				}
			}
		}

		/// <summary>True when the profile has no physical keyboard, so the on-screen keyboard should come up for text entry.</summary>
		public static bool IsMobileTouch(this InputProfile profile) => profile != InputProfile.Desktop;
	}
}
