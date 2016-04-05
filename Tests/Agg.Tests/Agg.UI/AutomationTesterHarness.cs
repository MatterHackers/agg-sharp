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

#if !__ANDROID__
using MatterHackers.GuiAutomation;
#endif
using NUnit.Framework;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace MatterHackers.Agg.UI.Tests
{
	public class AutomationTesterHarness
	{
		internal class TestResult
		{
			internal bool result;
			internal string description;
		}

		private List<TestResult> results = new List<TestResult>();

		public static AutomationTesterHarness ShowWindowAndExectueTests(SystemWindow initialSystemWindow, Action<AutomationTesterHarness> functionContainingTests, double secondsToTestFailure)
		{
			StackTrace st = new StackTrace(false);
			Console.WriteLine("\r\nRunning automation test: " + st.GetFrames().Skip(1).First().GetMethod().Name);

			AutomationTesterHarness testHarness = new AutomationTesterHarness(initialSystemWindow, functionContainingTests, secondsToTestFailure);

			// Dump errors to the console to aid in troubleshooting test failures
			foreach (var error in testHarness.Errors)
			{
				Console.WriteLine(error);
			}

			return testHarness;
		}

		private AutomationTesterHarness(SystemWindow initialSystemWindow, Action<AutomationTesterHarness> functionContainingTests, double secondsToTestFailure)
		{
			initialSystemWindow.Load += (sender, e) =>
			{
				Task.Run(() => CloseAfterTime(initialSystemWindow, secondsToTestFailure));

				Task.Run(() =>
				{
					functionContainingTests(this);

					initialSystemWindow.CloseOnIdle();
				});
			};

			initialSystemWindow.ShowAsSystemWindow();
		}

		public IEnumerable<string> Errors => results.Where(test => !test.result).Select(test => test.description);

		public void AddTestResult(bool pass, string resultDescription = "")
		{
			results.Add(new TestResult()
			{
				result = pass,
				description = resultDescription,
			});

			if (!pass)
			{
				// let us look at this at the time durring test run under the debugger
				GuiWidget.BreakInDebugger(resultDescription);
			}
		}

		public static void CloseAfterTime(SystemWindow windowToClose, double timeInSeconds)
		{
			Thread.Sleep((int)(timeInSeconds * 1000));
			windowToClose.CloseOnIdle();
		}

		public int TestCount
		{
			get { return results.Count; }
		}

		public bool AllTestsPassed 
		{ 
			get
			{
				foreach (TestResult testResult in results)
				{
					if (!testResult.result)
					{
						return false;
					}
				}

				return true;
			}
		}
	}
}