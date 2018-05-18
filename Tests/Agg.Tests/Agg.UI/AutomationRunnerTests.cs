﻿/*
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
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.GuiAutomation;
using NUnit.Framework;

namespace MatterHackers.Agg.UI.Tests
{
	[TestFixture, Category("Agg.UI"), Apartment(ApartmentState.STA), RunInApplicationDomain]
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

				Assert.IsTrue(leftClickCount == 1, "Got left button click");

				return Task.CompletedTask;
			});
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
				Assert.AreEqual(1, leftClickCount, "Should have one left click count after click");

				Assert.IsTrue(testRunner.NameExists("left"), "Left button should exist");

				var widget = testRunner.GetWidgetByName(
					"left",
					out _,
					5,
					testRunner.GetRegionByName("right"));

				Assert.IsNull(widget, "Left button should not exist in the right button region");

				return Task.CompletedTask;
			});
		}
	}
}