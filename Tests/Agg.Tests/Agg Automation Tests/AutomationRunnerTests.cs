/*
Copyright (c) 2025, Lars Brubaker
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
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.GuiAutomation;
using TUnit.Assertions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
    
    [DoNotParallelize]
    public class AutomationRunnerTests
	{
        [Test]
        public async Task GetWidgetByNameTestNoRegionSingleWindow()
		{
			// single system window
			int leftClickCount = 0;

			var buttonContainer = new SystemWindow(300, 200);

			var leftButton = new Button("left", 10, 40);
			leftButton.Name = "left";
			leftButton.Click += (sender, e) => { leftClickCount++; };
			buttonContainer.AddChild(leftButton);

			await AutomationRunner.ShowWindowAndExecuteTests(buttonContainer, (testRunner) =>
			{
				testRunner.ClickByName("left");
				testRunner.Delay(.5);

				await Assert.That(leftClickCount == 1).IsTrue();

				return Task.CompletedTask;
			});
		}

        [Test]
        public async Task AutomationRunnerTimeoutTest()
		{
			// Ensure AutomationRunner throws timeout exceptions
			var systemWindow = new SystemWindow(300, 200);

			var leftButton = new Button("left", 10, 40);
			leftButton.Name = "left";
			systemWindow.AddChild(leftButton);

            // NOTE: This test once failed. Possibly due to ShowWindowAndExecuteTests using different timing sources. A Stopwatch and a Task.Delay.

            //Assert.ThrowsAsync<TimeoutException>(
            // convert to xunit
            await Assert.That(async () =>
            {
                await AutomationRunner.ShowWindowAndExecuteTests(
                    systemWindow,
                    (testRunner) =>
                    {
                        // Test method that runs for 10+ seconds
                        Thread.Sleep(10 * 1000);
                        return Task.CompletedTask;
                    },
                    // Timeout after 1 second
                    secondsToTestFailure: 1);
            }).ThrowsAsync<TimeoutException>();
        }

        [Test]
        public async Task GetWidgetByNameTestRegionSingleWindow()
		{
			int leftClickCount = 0;

			var buttonContainer = new SystemWindow(300, 200);

			var leftButton = new Button("left", 10, 40);
			leftButton.Name = "left";
			leftButton.Click += (sender, e) => { leftClickCount++; };
			buttonContainer.AddChild(leftButton);

			var rightButton = new Button("right", 110, 40);
			rightButton.Name = "right";
			buttonContainer.AddChild(rightButton);

			await AutomationRunner.ShowWindowAndExecuteTests(buttonContainer, (testRunner) =>
			{
				testRunner.ClickByName("left");
				testRunner.Delay(.5);
				await Assert.That(leftClickCount).IsEqualTo(1);

				await Assert.That(testRunner.NameExists("left")).IsTrue();

				var widget = testRunner.GetWidgetByName(
					"left",
					out _,
					5,
					testRunner.GetRegionByName("right"));

				await Assert.That(widget).IsNull();

				return Task.CompletedTask;
			});
		}
	}
}
