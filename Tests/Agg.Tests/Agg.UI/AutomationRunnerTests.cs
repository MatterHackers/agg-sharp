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

using MatterHackers.Agg.Image;
using MatterHackers.GuiAutomation;
using MatterHackers.VectorMath;
using NUnit.Framework;
using System;

namespace MatterHackers.Agg.UI.Tests
{
	[TestFixture, Category("Agg.UI"), RequiresSTA, RunInApplicationDomain]
	public class AutomationRunnerTests
	{
		[Test, RequiresSTA, RunInApplicationDomain]
		public void GetWidgetByNameTestNoRegionSingleWindow()
		{
			// single system window
			{
				int leftClickCount = 0;

				Action<AutomationTesterHarness> testToRun = (AutomationTesterHarness resultsHarness) =>
				{
					AutomationRunner testRunner = new AutomationRunner();
					testRunner.ClickByName("left");
					testRunner.Wait(.5);

					resultsHarness.AddTestResult(leftClickCount == 1, "Got left button click");
				};

				SystemWindow buttonContainer = new SystemWindow(300, 200);

				Button leftButton = new Button("left", 10, 40);
				leftButton.Name = "left";
				leftButton.Click += (sender, e) => { leftClickCount++; };
				buttonContainer.AddChild(leftButton);

				AutomationTesterHarness testHarness = AutomationTesterHarness.ShowWindowAndExectueTests(buttonContainer, testToRun, 10);

				Assert.IsTrue(testHarness.AllTestsPassed);
				Assert.IsTrue(testHarness.TestCount == 1); // make sure we can all our tests
			}
		}

		[Test, RequiresSTA, RunInApplicationDomain]
		public void GetWidgetByNameTestNoRegionMultipleWindow()
		{
		}

		[Test, RequiresSTA, RunInApplicationDomain]
		public void GetWidgetByNameTestRegionSingleWindow()
		{
			int leftClickCount = 0;

			Action<AutomationTesterHarness> testToRun = (AutomationTesterHarness resultsHarness) =>
			{
				AutomationRunner testRunner = new AutomationRunner();
				testRunner.ClickByName("left");
				testRunner.Wait(.5);
				resultsHarness.AddTestResult(leftClickCount == 1, "Got left button click");

				SearchRegion rightButtonRegion = testRunner.GetRegionByName("right");

				testRunner.ClickByName("left", searchRegion: rightButtonRegion);
				testRunner.Wait(.5);

				resultsHarness.AddTestResult(leftClickCount == 1, "Did not get left button click");
			};

			SystemWindow buttonContainer = new SystemWindow(300, 200);

			Button leftButton = new Button("left", 10, 40);
			leftButton.Name = "left";
			leftButton.Click += (sender, e) => { leftClickCount++; };
			buttonContainer.AddChild(leftButton);
			Button rightButton = new Button("right", 110, 40);
			rightButton.Name = "right";
			buttonContainer.AddChild(rightButton);

			AutomationTesterHarness testHarness = AutomationTesterHarness.ShowWindowAndExectueTests(buttonContainer, testToRun, 10);

			Assert.IsTrue(testHarness.AllTestsPassed);
			Assert.IsTrue(testHarness.TestCount == 2); // make sure we can all our tests
		}

		[Test]
		public void ClickByName()
		{
			// single system window
			// no search region specified
			// multiple system windows

			// constrained search region
			// no search region specified
			// multiple system windows
		}
		
		[Test]
		public void DragDropByName()
		{
			// single system window
			// no search region specified
			// multiple system windows

			// constrained search region
			// no search region specified
			// multiple system windows
		}
			
		[Test]
		public void DragByName()
		{
			// single system window
			// no search region specified
			// multiple system windows

			// constrained search region
			// no search region specified
			// multiple system windows
		}
			
		[Test]
		public void DropByName()
		{
			// single system window
			// no search region specified
			// multiple system windows

			// constrained search region
			// no search region specified
			// multiple system windows
		}
			
		[Test]
		public void DoubleClickByName()
		{
			// single system window
			// no search region specified
			// multiple system windows

			// constrained search region
			// no search region specified
			// multiple system windows
		}
			
		[Test]
		public void MoveToByName()
		{
			// single system window
			// no search region specified
			// multiple system windows

			// constrained search region
			// no search region specified
			// multiple system windows
		}
			
		[Test]
		public void NameExists()
		{
			// single system window
			// no search region specified
			// multiple system windows

			// constrained search region
			// no search region specified
			// multiple system windows
		}
	}
}