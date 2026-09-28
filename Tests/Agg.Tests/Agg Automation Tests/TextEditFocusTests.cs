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

using MatterHackers.GuiAutomation;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
    [NotInParallel(nameof(AutomationRunner.ShowWindowAndExecuteTests))] // Ensure tests in this class do not run in parallel
    public class TextEditFocusTests
	{
        [Test]
        public async Task VerifyFocusMakesTextWidgetEditable()
		{
			TextEditWidget editField = null;
			var systemWindow = new SystemWindow(300, 200)
			{
				BackgroundColor = Color.Black,
			};

			async Task TestToRun(AutomationRunner testRunner)
			{
				// Focus on the ui thread, and wait until it has landed: calling Focus from the test thread
				// races the window's own first frames, which can move focus after it.
				UiThread.RunOnIdle(editField.Focus);
				testRunner.WaitFor(() => editField.ContainsFocus);

				// Type returns once the ui thread has delivered every key.
				testRunner.Type("Test Text");

				await Assert.That(editField.Text == "Test Text").IsTrue();
				testRunner.MarkTestComplete();
			}

			editField = new TextEditWidget(pixelWidth: 200)
			{
				HAnchor = HAnchor.Center,
				VAnchor = VAnchor.Center,
			};
			systemWindow.AddChild(editField);

			await AutomationRunner.ShowWindowAndExecuteTests(systemWindow, TestToRun);
		}

        [Test]
        public async Task VerifyFocusProperty()
		{
			var systemWindow = new SystemWindow(300, 200)
			{
				BackgroundColor = Color.Black,
			};

			var editField = new TextEditWidget(pixelWidth: 200)
			{
				HAnchor = HAnchor.Center,
				VAnchor = VAnchor.Center,
			};
			systemWindow.AddChild(editField);

			async Task TestToRun(AutomationRunner testRunner)
			{
				UiThread.RunOnIdle(editField.Focus);
				testRunner.WaitFor(() => editField.ContainsFocus);
				//if (!editField.ContainsFocus) { System.Diagnostics.Debugger.Launch(); System.Diagnostics.Debugger.Break(); }
				// NOTE: Okay. During parallel testing, it seems that the avalanche of windows causes test UIs to lose control focus and get confused.
				await Assert.That(editField.ContainsFocus).IsTrue();
				testRunner.MarkTestComplete();
			}

			await AutomationRunner.ShowWindowAndExecuteTests(systemWindow, TestToRun);
		}

        [Test]
        public async Task SelectAllOnFocusCanStillClickAfterSelection()
		{
			var editField = new TextEditWidget(pixelWidth: 200)
			{
				Name = "editField",
				Text = "Some Text",
				HAnchor = HAnchor.Center,
				VAnchor = VAnchor.Center,
			};

			var systemWindow = new SystemWindow(300, 200)
			{
				BackgroundColor = Color.Gray,
			};
			systemWindow.AddChild(editField);

			async Task TestToRun(AutomationRunner testRunner)
			{
				// Set on the ui thread, and let the window settle before clicking rather than sleeping.
				UiThread.RunOnIdle(() => editField.SelectAllOnFocus = true);
				testRunner.WaitForPendingUiWork();
				testRunner.ClickByName(editField.Name);
				testRunner.WaitFor(() => editField.ContainsFocus);

				testRunner.Type("123");
				await Assert.That(editField.Text).IsEqualTo("123");//, "Text input on newly focused control should replace selection");

				testRunner.ClickByName(editField.Name);
				testRunner.WaitFor(() => editField.ContainsFocus);

				testRunner.Type("123");
				await Assert.That(editField.Text).IsEqualTo("123123");//, "Text should be appended if control is focused and has already received input");
				testRunner.MarkTestComplete();
			}

			await AutomationRunner.ShowWindowAndExecuteTests(systemWindow, TestToRun);
		}
	}
}
