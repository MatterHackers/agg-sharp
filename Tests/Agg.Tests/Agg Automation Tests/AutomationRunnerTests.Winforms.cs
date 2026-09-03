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
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.GuiAutomation;
using TUnit.Assertions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	// The WinForms half of AutomationRunnerTests. Its subject is WinformsSystemWindow's process-wide idle
	// timer and the first-window latch that ResetFirstWindowFlag clears - neither of which any other
	// platform host has - so the project drops this file when WindowsBuild is false. Same split, and the
	// same reason, as ConstructorHygieneTests.Winforms.cs.
	public partial class AutomationRunnerTests
	{
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

        // A window shown from a thread that is not the message pump must be deferred onto the pump, not
        // answered with an Application.Run of its own: the second loop never returns, so the caller's thread
        // is swallowed and whatever it was doing - an automation step, a worker finishing a job - never
        // continues. ShowWindowAndExecuteTests gives exactly that arrangement for free, since the window's
        // Application.Run owns the calling thread while this lambda runs on another.
        [Test]
        public async Task ShowFromNonPumpThreadReturnsToItsCaller()
        {
            var systemWindow = new SystemWindow(300, 200);

            double originalCloseTimeout = AutomationRunner.CloseWindowTimeoutSeconds;
            AutomationRunner.CloseWindowTimeoutSeconds = 5;

            var showReturned = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            // Sampled at the moment the bounded wait ends, because the answer changes afterwards: under the
            // old rule the call did eventually return - when the dialog closed and its stolen message loop
            // finally unwound - so a check made after the run would pass on the very bug this pins.
            bool returnedWhileThePumpWasStillRunning = false;
            Exception showException = null;

            try
            {
                await AutomationRunner.ShowWindowAndExecuteTests(
                    systemWindow,
                    (testRunner) =>
                    {
                        var dialog = new SystemWindow(200, 100) { Title = "OffThreadShow" };

                        // A background thread of its own, so that a regression parks that thread rather than
                        // this one - the window still closes and the run still reports a failure.
                        var showThread = new Thread(() =>
                        {
                            try
                            {
                                dialog.ShowAsSystemWindow();
                                showReturned.TrySetResult(true);
                            }
                            catch (Exception exception)
                            {
                                showException = exception;
                                showReturned.TrySetResult(false);
                            }
                        });

                        showThread.IsBackground = true;
                        showThread.Start();

                        // maxSeconds is a failure bound only; the wait ends as soon as the call returns.
                        testRunner.WaitFor(() => showReturned.Task.IsCompleted, maxSeconds: 5);
                        returnedWhileThePumpWasStillRunning = showReturned.Task.IsCompleted;

                        // Queued behind the deferred Show, so the pump runs them in that order.
                        UiThread.RunOnIdle(() => dialog.Close());
                        testRunner.WaitFor(() => dialog.HasBeenClosed, maxSeconds: 5);

                        testRunner.MarkTestComplete();
                        return Task.CompletedTask;
                    },
                    secondsToTestFailure: 30);

                await Assert.That(showException).IsNull();
                await Assert.That(returnedWhileThePumpWasStillRunning).IsTrue().Because("a show from a non-pump thread must defer to the running pump rather than starting a second message loop");
            }
            finally
            {
                AutomationRunner.CloseWindowTimeoutSeconds = originalCloseTimeout;
            }
        }
	}
}
