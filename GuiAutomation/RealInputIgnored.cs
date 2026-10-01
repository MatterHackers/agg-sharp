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
using MatterHackers.Agg.UI;

namespace MatterHackers.GuiAutomation
{
	/// <summary>
	/// Turns off <see cref="IPlatformWindow.EnablePlatformWindowInput"/> for the life of an automation run,
	/// restoring what was there on dispose.
	/// </summary>
	/// <remarks>
	/// The run drives its window with simulated input, but the real cursor is still on the desktop: the
	/// person using the machine moves it, and Windows sends a mouse move or mouse leave to whatever window
	/// ends up under a still cursor whenever windows open, close or restack - which a run of automation
	/// tests does constantly. Let through, those moved agg's pointer off the widget a test had just moved
	/// to: hovers ended, menus and popups closed, clicks found nothing and focus was dropped, only in long
	/// runs and never alone. Every host gates only its input arms on this switch, so the window still
	/// paints, resizes and closes.
	/// </remarks>
	internal sealed class RealInputIgnored : IDisposable
	{
		private readonly bool wasEnabled = IPlatformWindow.EnablePlatformWindowInput;

		public RealInputIgnored()
		{
			IPlatformWindow.EnablePlatformWindowInput = false;
		}

		public void Dispose()
		{
			IPlatformWindow.EnablePlatformWindowInput = this.wasEnabled;
		}
	}
}
