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
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.GuiAutomation;
using MatterHackers.Agg.Tests.TestingInfrastructure;
using TUnit.Assertions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
    
     [NotInParallel(nameof(AutomationRunner.ShowWindowAndExecuteTests))] // Ensure tests in this class do not run in parallel
    	public class AutomationRunnerTests
	{
        // Ensure TestSetup static constructor is called to initialize AutomationRunner.InputMethod
        private static readonly bool testSetupInitialized = EnsureTestSetupInitialized();
        
        private static bool EnsureTestSetupInitialized()
        {
            // This forces the TestSetup static constructor to run
            var temp = typeof(TestSetup);
            return true;
        }

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

			await AutomationRunner.ShowWindowAndExecuteTests(buttonContainer, async (testRunner) =>
			{
				testRunner.ClickByName("left");
				testRunner.Delay(.5);

				await Assert.That(leftClickCount == 1).IsTrue();
				testRunner.MarkTestComplete();
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
            // TODO: Convert to proper TUnit exception testing syntax once available
            
            try 
            {
                await AutomationRunner.ShowWindowAndExecuteTests(
                    systemWindow,
                    (testRunner) =>
                    {
                        // Test method that runs for 10+ seconds
                        Thread.Sleep(10 * 1000);
                        testRunner.MarkTestComplete();
                        return Task.CompletedTask;
                    },
                    // Timeout after 1 second
                    secondsToTestFailure: 1);
                    
                // Should have thrown TimeoutException
                await Assert.That(false).IsTrue(); // Fail if we reach here
            }
            catch (TimeoutException)
            {
                // Expected exception - test passes
                await Assert.That(true).IsTrue();
            }
        }

        // The simulated input must deliver the same event shape real Windows input does: a single
        // click is one down(Clicks=1)/up(Clicks=1) pair, and a double click is TWO full pairs where
        // only the second down reports Clicks == 2 - ups NEVER carry the 2 (verified against real
        // WinForms). Widgets that act on the up of a double click rely on GuiWidget.IsDoubleClick
        // remembering the down, so that is asserted from the up handler here as well.
        [Test]
        public async Task DoubleClickByNameSendsTwoFullClickPairsWithProductionClickCounts()
        {
            var events = new List<string>();

            var systemWindow = new SystemWindow(300, 200);

            var target = new GuiWidget(100, 100)
            {
                Name = "target"
            };
            systemWindow.AddChild(target);

            target.MouseDown += (s, e) => events.Add($"down:{e.Clicks}");
            target.MouseUp += (s, e) => events.Add($"up:{e.Clicks}:{(target.IsDoubleClick(e) ? "double" : "single")}");

            await AutomationRunner.ShowWindowAndExecuteTests(systemWindow, async (testRunner) =>
            {
                testRunner.ClickByName("target");

                // Joined so the assertion is order-exact - the SEQUENCE is the contract.
                await Assert.That(string.Join(",", events)).IsEqualTo("down:1,up:1:single")
                    .Because("a single click is one press/release pair and must not read as a double click");

                events.Clear();
                testRunner.DoubleClickByName("target");

                await Assert.That(string.Join(",", events)).IsEqualTo("down:1,up:1:single,down:2,up:1:double")
                    .Because("a double click is two full pairs; only the second down carries Clicks == 2, and the final up - which the platform reports with Clicks == 1 - must still answer IsDoubleClick");

                testRunner.MarkTestComplete();
            });
        }

        // A window whose ShouldClose handler cancels the close (the shape of a "do you want to save?"
        // prompt) used to hang the whole run: RequestWindowClose was vetoed, the message pump never
        // exited, and the test reported Passed only after a human closed the app by hand. The close
        // phase watchdog must force the window closed and surface the hang as a failure.
        [Test]
        public async Task VetoedCloseIsForcedAndReportedAsFailure()
        {
            var systemWindow = new SystemWindow(300, 200);

            int vetoCount = 0;
            systemWindow.ShouldClose += (s, e) =>
            {
                // Always cancel - only a force close should be able to get past this.
                vetoCount++;
                e.Cancel = true;
            };

            double originalCloseTimeout = AutomationRunner.CloseWindowTimeoutSeconds;
            AutomationRunner.CloseWindowTimeoutSeconds = 2;

            try
            {
                try
                {
                    await AutomationRunner.ShowWindowAndExecuteTests(
                        systemWindow,
                        (testRunner) =>
                        {
                            testRunner.MarkTestComplete();
                            return Task.CompletedTask;
                        },
                        secondsToTestFailure: 30);

                    await Assert.That(false).IsTrue().Because("the vetoed close should have been reported as a TimeoutException");
                }
                catch (TimeoutException)
                {
                    // Expected - the close phase timed out and the watchdog forced the window closed.
                }

                await Assert.That(vetoCount).IsGreaterThan(0).Because("the test window must actually have vetoed a close for this to be a valid repro");
                await Assert.That(systemWindow.HasBeenClosed).IsTrue().Because("the watchdog must force the window closed so the run does not hang");
            }
            finally
            {
                AutomationRunner.CloseWindowTimeoutSeconds = originalCloseTimeout;
            }
        }

        // The RunOnIdle pump is driven by a single process-wide timer in WinformsSystemWindow. Tearing down
        // one window (ResetFirstWindowFlag is what the runner calls between tests, and it is what a test
        // running in parallel calls while another test's window is still up) used to stop and dispose that
        // shared timer, leaving the still-live window with a dead pump: RunOnIdle actions - including the
        // CloseOnIdle that ends the message loop and the watchdog's own force close - never ran again and
        // the whole run hung with an idle message pump. A live window must keep its pump.
        [Test]
        public async Task IdlePumpSurvivesAnotherWindowsTeardown()
        {
            var systemWindow = new SystemWindow(300, 200);

            bool idleActionRan = false;

            double originalCloseTimeout = AutomationRunner.CloseWindowTimeoutSeconds;
            AutomationRunner.CloseWindowTimeoutSeconds = 5;

            try
            {
                await AutomationRunner.ShowWindowAndExecuteTests(
                    systemWindow,
                    (testRunner) =>
                    {
                        // Exactly what the runner does when another test finishes while this window is live.
                        WinformsSystemWindow.ResetFirstWindowFlag();

                        UiThread.RunOnIdle(() => idleActionRan = true);
                        testRunner.WaitFor(() => idleActionRan, maxSeconds: 5);

                        testRunner.MarkTestComplete();
                        return Task.CompletedTask;
                    },
                    secondsToTestFailure: 30);

                await Assert.That(idleActionRan).IsTrue().Because("a live window's RunOnIdle pump must survive another window's teardown");
            }
            finally
            {
                AutomationRunner.CloseWindowTimeoutSeconds = originalCloseTimeout;
            }
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

			await AutomationRunner.ShowWindowAndExecuteTests(buttonContainer, async (testRunner) =>
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
				testRunner.MarkTestComplete();
			});
		}
	}
}
