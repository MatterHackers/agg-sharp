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

using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	// The WinForms half of ConstructorHygieneTests. Split out so the rest of the suite compiles on a
	// host with no WinForms; the project drops this file when WindowsBuild is false.
	public partial class ConstructorHygieneTests
	{
		private class RaisableControl : System.Windows.Forms.Control
		{
			public void RaiseKeyDown(System.Windows.Forms.Keys keys)
			{
				this.OnKeyDown(new System.Windows.Forms.KeyEventArgs(keys));
			}
		}

		[Test]
		[NotInParallel]
		public async Task WinformsEventSinkUnhookRemovesControlHandlers()
		{
			var control = new RaisableControl();
			var systemWindow = new SystemWindow(100, 100);

			int keyDownCount = 0;
			systemWindow.KeyDown += (s, e) => keyDownCount++;

			var eventSink = new WinformsEventSink(control, systemWindow);

			control.RaiseKeyDown(System.Windows.Forms.Keys.A);
			await Assert.That(keyDownCount).IsEqualTo(1);

			eventSink.Unhook();

			// After Unhook no handler wired by the constructor may still be attached.
			control.RaiseKeyDown(System.Windows.Forms.Keys.A);
			await Assert.That(keyDownCount).IsEqualTo(1);

			// Safe to call again.
			eventSink.Unhook();

			Keyboard.Clear();
		}
	}
}
