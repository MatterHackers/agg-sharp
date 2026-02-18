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

using System.Threading.Tasks;
using Agg.Tests.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.GuiAutomation;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.UI.Tests
{
    [MhTestFixture("Opens Winforms Window")]
    public class MenuTests
	{
		public static bool SaveImagesForDebug = false;

		private void OutputImage(ImageBuffer imageToOutput, string fileName)
		{
			if (SaveImagesForDebug)
			{
				ImageTgaIO.Save(imageToOutput, fileName);
			}
		}

        [MhTest]
        public async Task OpenAndCloseMenus()
		{
			int item1ClickCount = 0;
			int item2ClickCount = 0;
			int item3ClickCount = 0;

			DropDownList testList = new DropDownList("no selection", Color.Blue)
			{
				MenuItemsBackgroundColor = Color.White,
				MenuItemsBackgroundHoverColor = Color.LightGray,
				Name = "menu1",
				HoverColor = Color.Green
			};

			AutomationTest testToRun = (testRunner) =>
			{
				MhAssert.Equal(0, item1ClickCount);
				MhAssert.Equal(0, item2ClickCount);
				MhAssert.Equal(0, item3ClickCount);

				testRunner.ClickByName("menu1");
				testRunner.ClickByName("item1");

				testRunner.WaitFor(() => !testList.IsOpen, 2);
				MhAssert.True(!testList.IsOpen);
				MhAssert.Equal(1, item1ClickCount);
				MhAssert.Equal(0, item2ClickCount);
				MhAssert.Equal(0, item3ClickCount);

				testRunner.ClickByName("menu1");
				testRunner.ClickByName("item2");

				testRunner.WaitFor(() => !testList.IsOpen, 2);
				MhAssert.True(!testList.IsOpen);
				MhAssert.Equal(1, item1ClickCount);
				MhAssert.Equal(1, item2ClickCount);
				MhAssert.Equal(0, item3ClickCount);

				testRunner.ClickByName("menu1");
				testRunner.ClickByName("item3");

				testRunner.WaitFor(() => testList.IsOpen, 2);
				MhAssert.True(testList.IsOpen, "It should remain open when clicking on a disabled item.");
				MhAssert.Equal(1, item1ClickCount);
				MhAssert.Equal(1, item2ClickCount);
				MhAssert.Equal(0, item3ClickCount);
				testRunner.ClickByName("item2");

				testRunner.WaitFor(() => !testList.IsOpen, 2);
				MhAssert.True(!testList.IsOpen);
				MhAssert.Equal(1, item1ClickCount);
				MhAssert.Equal(2, item2ClickCount);
				MhAssert.Equal(0, item3ClickCount);

				testRunner.ClickByName("menu1");
				testRunner.ClickByName("OffMenu");

				testRunner.WaitFor(() => !testList.IsOpen, 2);
				//if (testList.IsOpen) { System.Diagnostics.Debugger.Launch(); System.Diagnostics.Debugger.Break(); }
				MhAssert.True(!testList.IsOpen);

				testRunner.ClickByName("menu1");
				testRunner.Delay(.1);
				//testRunner.Delay(5);
				//if (!testList.IsOpen) { System.Diagnostics.Debugger.Launch(); System.Diagnostics.Debugger.Break(); }
				MhAssert.True(testList.IsOpen);

				testRunner.ClickByName("item3");
				testRunner.Delay(.1);
				//if (!testList.IsOpen) { System.Diagnostics.Debugger.Launch(); System.Diagnostics.Debugger.Break(); }
				MhAssert.True(testList.IsOpen);

				//testRunner.Delay(5);
				testRunner.MoveToByName("OffMenu");
				// NOTE: Sometimes get failures here. Using a large delay now.
				// NOTE: The menu was once closed for those 10s...
				// Update: Failures may have been due to tests fighting over platform UI focus.
				//if (!testList.IsOpen) { System.Diagnostics.Debugger.Launch(); System.Diagnostics.Debugger.Break(); }
				MhAssert.True(testList.IsOpen);

				testRunner.ClickByName("OffMenu");
				testRunner.WaitFor(() => !testList.IsOpen, 2);
				MhAssert.False(testList.IsOpen, "Menus should close when clicking off menu");

				return Task.CompletedTask;
			};

			var menuTestContainer = new SystemWindow(300, 200)
			{
				BackgroundColor = Color.White,
				Name = "SystemWindow",
			};

			var menuItem1 = testList.AddItem("item1");
			menuItem1.Name = "item1";
			menuItem1.Selected += (s, e) => item1ClickCount++;

			var menuItem2 = testList.AddItem("item2");
			menuItem2.Name = "item2";
			menuItem2.Selected += (s, e) => item2ClickCount++;

			var menuItem3 = testList.AddItem("item3");
			menuItem3.Name = "item3";
			menuItem3.Enabled = false;
			menuItem3.Selected += (s, e) => item3ClickCount++;

			menuTestContainer.AddChild(testList);

			menuTestContainer.AddChild(new GuiWidget(20, 20)
			{
				OriginRelativeParent = new Vector2(160, 150),
				BackgroundColor = Color.Cyan,
				Name = "OffMenu",
			});

			await AutomationRunner.ShowWindowAndExecuteTests(menuTestContainer, testToRun);
		}
	}
}
