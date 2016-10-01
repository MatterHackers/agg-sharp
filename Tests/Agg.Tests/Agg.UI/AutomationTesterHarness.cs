/*
Copyright (c) 2014, Lars Brubaker
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
using System.Threading;
using System.Threading.Tasks;

#if !__ANDROID__
using MatterHackers.GuiAutomation;
#endif

namespace MatterHackers.Agg.UI.Tests
{
	public class AutomationTesterHarness
	{
		private List<TestResult> results = new List<TestResult>();

		public static AutomationTesterHarness ShowWindowAndExecuteTests(SystemWindow initialSystemWindow, Action<AutomationTesterHarness> functionContainingTests, double secondsToTestFailure)
		{
			return new AutomationTesterHarness(initialSystemWindow, functionContainingTests, secondsToTestFailure);
		}

		private AutomationTesterHarness(SystemWindow initialSystemWindow, Action<AutomationTesterHarness> functionContainingTests, double secondsToTestFailure)
		{
			bool firstDraw = true;
			initialSystemWindow.AfterDraw += (sender, e) =>
			{
				if (firstDraw)
				{
					Task.Run(() => CloseAfterTime(initialSystemWindow, secondsToTestFailure));

					firstDraw = false;
					Task.Run(() =>
					{
						try
						{
							functionContainingTests(this);
						}
						catch(Exception ex)
						{
							Console.WriteLine("Unhandled exception in automation tests: \r\n\t{0}", ex.ToString());
						}

						initialSystemWindow.CloseOnIdle();
					});
				}
			};

			initialSystemWindow.ShowAsSystemWindow();
		}

		public void AddTestResult(bool passed, string resultDescription = "")
		{
			var testResult = new TestResult()
			{
				Passed = passed,
				Description = resultDescription,
			};

			results.Add(testResult);

			Console.WriteLine(
				" {0} {1}", 
				passed ? "-" : "!",
				testResult.ToString());
		}

		public static void CloseAfterTime(SystemWindow windowToClose, double timeInSeconds)
		{
			Thread.Sleep((int)(timeInSeconds * 1000));
			windowToClose.CloseOnIdle();
		}

		public bool AllTestsPassed(int expectedCount)
		{
			return expectedCount == results.Count 
				&& results.TrueForAll(testResult => testResult.Passed);
		}

		internal class TestResult
		{
			internal bool Passed { get; set; }
			internal string Description { get; set; }

			public override string ToString()
			{
				string status = Passed ? "Passed" : "Failed";
				return $"Test {status}: {Description}";
			}
		}
	}
}