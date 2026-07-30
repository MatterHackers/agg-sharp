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

using System.Reflection;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// Verifies that constructing a SystemWindow does not schedule any UiThread callbacks or
	/// subscribe events for tooltips - activation must only happen once the window is shown or
	/// first receives mouse input. This guards against callbacks observing a window while its
	/// constructor is still running.
	/// </summary>
	public class ToolTipActivationTests
	{
		private static RunningInterval GetToolTipInterval(SystemWindow window)
		{
			var field = typeof(ToolTipManager).GetField("runningInterval", BindingFlags.Instance | BindingFlags.NonPublic);
			return (RunningInterval)field.GetValue(window.ToolTipManager);
		}

		[Test]
		public async Task ConstructionDoesNotScheduleToolTipInterval()
		{
			var systemWindow = new SystemWindow(200, 200);

			// Construction alone must not have scheduled the tooltip polling interval
			await Assert.That(GetToolTipInterval(systemWindow) == null).IsTrue();

			// First mouse input activates tooltip tracking (a safe post-construction point)
			systemWindow.OnMouseMove(new MouseEventArgs(MouseButtons.None, 0, 5, 5, 0));
			var interval = GetToolTipInterval(systemWindow);
			await Assert.That(interval != null).IsTrue();

			// Activation is idempotent - further input must not schedule a second interval
			systemWindow.OnMouseMove(new MouseEventArgs(MouseButtons.None, 0, 6, 6, 0));
			await Assert.That(ReferenceEquals(GetToolTipInterval(systemWindow), interval)).IsTrue();

			// Teardown clears the interval
			systemWindow.ToolTipManager.Dispose();
			await Assert.That(GetToolTipInterval(systemWindow) == null).IsTrue();

			// Teardown is idempotent
			systemWindow.ToolTipManager.Dispose();
			await Assert.That(GetToolTipInterval(systemWindow) == null).IsTrue();
		}

		[Test]
		public async Task DisposeIsSafeWhenNeverActivated()
		{
			var systemWindow = new SystemWindow(100, 100);

			// Dispose before any activation must not throw and must leave no interval behind
			systemWindow.ToolTipManager.Dispose();
			await Assert.That(GetToolTipInterval(systemWindow) == null).IsTrue();

			// And activation gating still holds after an early dispose
			await Assert.That(systemWindow.ToolTipManager.CurrentText == "").IsTrue();
		}
	}
}
