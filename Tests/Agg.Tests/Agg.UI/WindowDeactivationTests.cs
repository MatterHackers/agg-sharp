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
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.GuiAutomation;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// <see cref="SystemWindow.Deactivated"/>: the user switching to another application closes open popups
	/// and leaves agg's focus where it was. The desktop hosts' calls into it (windowDidResignKey:, X11
	/// FocusOut, WinForms LostFocus) need a real desktop and are not run here; the browser host's is, in
	/// <c>BrowserSystemWindowTests</c>. An automation run turns the hosts' calls off
	/// (<see cref="IPlatformWindow.EnablePlatformWindowDeactivation"/>).
	/// </summary>
	// Popups close through the process-wide idle queue, which the windowed automation classes share.
	[NotInParallel(nameof(AutomationRunner.ShowWindowAndExecuteTests))]
	public class WindowDeactivationTests
	{
		[After(Test)]
		public void DrainTheIdleQueue()
		{
			for (int i = 0; i < 4; i++)
			{
				UiThread.InvokePendingActions();
			}
		}

		[Test]
		public async Task DeactivatingTheWindowClosesAnOpenMenu()
		{
			var window = new SystemWindow(600, 400);
			var anchor = new GuiWidget(50, 20) { Name = "Anchor" };
			window.AddChild(anchor);
			anchor.Position = new Vector2(10, 370);

			var menu = new PopupMenu(new ThemeConfig());
			menu.CreateMenuItem("One");
			menu.ShowMenu(anchor, Vector2.Zero);

			await Assert.That(menu.Parent).IsEqualTo(window);

			window.OnDeactivated(EventArgs.Empty);

			await Assert.That(menu.HasBeenClosed).IsTrue();
			await Assert.That(window.Children.Contains(menu)).IsFalse();
		}

		[Test]
		public async Task DeactivatingTheWindowClosesAnOpenPopupWidget()
		{
			// The other popup path: a PopupWidget with its own layout engine (MatterCAD's PopupButton).
			var window = new SystemWindow(600, 400);
			var anchor = new GuiWidget(50, 20) { Name = "Anchor" };
			window.AddChild(anchor);
			anchor.Position = new Vector2(10, 200);

			var content = new GuiWidget(100, 60);
			var popup = new PopupWidget(content, new PopupLayoutEngine(content, anchor, Direction.Down, 0, false), false);

			await Assert.That(popup.Parent).IsNotNull();

			window.OnDeactivated(EventArgs.Empty);

			await Assert.That(popup.HasBeenClosed).IsTrue();
		}

		[Test]
		public async Task DeactivatingTheWindowLeavesTextFocusWhereItWas()
		{
			// A mac app keeps its text field focused across an app switch; deactivation is not a focus change.
			var window = new SystemWindow(600, 400);
			var textField = new TextEditWidget("typing", 10, 10, pixelWidth: 200);
			window.AddChild(textField);
			textField.Focus();

			window.OnDeactivated(EventArgs.Empty);

			await Assert.That(textField.ContainsFocus).IsTrue();
		}

		[Test]
		public async Task AnAutomationRunIgnoresTheDesktopDeactivatingItsWindow()
		{
			// A full run showed the mac host's windowDidResignKey: closing menus a test had just opened: the
			// desktop hands key status around mid-run, and simulated input is not a user who switched away.
			await Assert.That(IPlatformWindow.ForwardPlatformDeactivation).IsTrue();

			bool forwardedDuringRun = true;
			var window = new SystemWindow(200, 100);
			await AutomationRunner.ShowWindowAndExecuteTests(window, testRunner =>
			{
				forwardedDuringRun = IPlatformWindow.ForwardPlatformDeactivation;
				testRunner.MarkTestComplete();
				return Task.CompletedTask;
			});

			await Assert.That(forwardedDuringRun).IsFalse();
			await Assert.That(IPlatformWindow.ForwardPlatformDeactivation).IsTrue();
		}
	}
}
