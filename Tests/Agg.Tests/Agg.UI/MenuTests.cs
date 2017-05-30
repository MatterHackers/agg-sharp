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
using MatterHackers.Agg.Image;
using MatterHackers.GuiAutomation;
using MatterHackers.VectorMath;
using NUnit.Framework;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MatterHackers.Agg.UI.Tests
{
	[TestFixture, Category("Agg.UI")]
	public class MenuTests
	{
		public static bool saveImagesForDebug = false;

		private void OutputImage(ImageBuffer imageToOutput, string fileName)
		{
			if (saveImagesForDebug)
			{
				ImageTgaIO.Save(imageToOutput, fileName);
			}
		}

		[Test]
		public void ListMenuTests()
		{
			string menuSelected = "";

			GuiWidget container = new GuiWidget(400, 400);
			TextWidget menueView = new TextWidget("Edit");
			Menu listMenu = new Menu(menueView);
			listMenu.OriginRelativeParent = new Vector2(10, 300);

			MenuItem cutMenuItem = new MenuItem(new TextWidget("Cut"));
			cutMenuItem.Selected += (sender, e) => { menuSelected = "Cut"; };
			listMenu.MenuItems.Add(cutMenuItem);

			MenuItem copyMenuItem = new MenuItem(new TextWidget("Copy"));
			copyMenuItem.Selected += (sender, e) => { menuSelected = "Copy"; };
			listMenu.MenuItems.Add(copyMenuItem);

			MenuItem pastMenuItem = new MenuItem(new TextWidget("Paste"));
			pastMenuItem.Selected += (sender, e) => { menuSelected = "Paste"; };
			listMenu.MenuItems.Add(pastMenuItem);

			container.AddChild(listMenu);

			Assert.IsTrue(!listMenu.IsOpen);

			// open the menu
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 11, 300, 0));
			Assert.IsTrue(!listMenu.IsOpen);
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 11, 300, 0));
			UiThread.InvokePendingActions();
			Assert.IsTrue(listMenu.IsOpen);

			// all the menu items should be added to the open menu
			Assert.IsTrue(cutMenuItem.Parent != null);
			Assert.IsTrue(copyMenuItem.Parent != null);
			Assert.IsTrue(pastMenuItem.Parent != null);

			// click on menu again to close
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 11, 304, 0));
			UiThread.InvokePendingActions();
			Assert.IsTrue(!listMenu.IsOpen);
			// all the menu items should be removed from the closed menu
			Assert.IsTrue(cutMenuItem.Parent == null);
			Assert.IsTrue(copyMenuItem.Parent == null);
			Assert.IsTrue(pastMenuItem.Parent == null);

			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 11, 300, 0));
			UiThread.InvokePendingActions();
			Assert.IsTrue(!listMenu.IsOpen);

			// all the menu items should be removed from the closed menu
			Assert.IsTrue(cutMenuItem.Parent == null);
			Assert.IsTrue(copyMenuItem.Parent == null);
			Assert.IsTrue(pastMenuItem.Parent == null);

			// open the menu
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 11, 300, 0));
			UiThread.InvokePendingActions();
			Assert.IsTrue(!listMenu.IsOpen);
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 11, 300, 0));
			UiThread.InvokePendingActions();
			Assert.IsTrue(listMenu.IsOpen);
			// all the menu items should be added to the open menu
			Assert.IsTrue(cutMenuItem.Parent != null);
			Assert.IsTrue(copyMenuItem.Parent != null);
			Assert.IsTrue(pastMenuItem.Parent != null);

			// click off menu to close
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 5, 299, 0));
			UiThread.InvokePendingActions();
			Assert.IsTrue(!listMenu.IsOpen);
			// all the menu items should be removed from the closed menu
			Assert.IsTrue(cutMenuItem.Parent == null);
			Assert.IsTrue(copyMenuItem.Parent == null);
			Assert.IsTrue(pastMenuItem.Parent == null);
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 5, 299, 0));
			UiThread.InvokePendingActions();
			Assert.IsTrue(!listMenu.IsOpen);

			// open the menu again
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 11, 300, 0));
			UiThread.InvokePendingActions();
			Assert.IsTrue(!listMenu.IsOpen);
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 11, 300, 0));
			UiThread.InvokePendingActions();
			Assert.IsTrue(listMenu.IsOpen);

			// select the first item
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 11, 290, 0));
			UiThread.InvokePendingActions();
			Assert.IsTrue(listMenu.IsOpen);
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 11, 290, 0));
			UiThread.InvokePendingActions();
			Assert.IsTrue(!listMenu.IsOpen);
			Assert.IsTrue(menuSelected == "Cut");

			// open the menu
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 11, 300, 0));
			UiThread.InvokePendingActions();
			Assert.IsTrue(!listMenu.IsOpen);
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 11, 300, 0));
			UiThread.InvokePendingActions();
			Assert.IsTrue(listMenu.IsOpen);

			// select the second item
			menuSelected = "";
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 11, 275, 0));
			UiThread.InvokePendingActions();
			Assert.IsTrue(listMenu.IsOpen);
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 11, 275, 0));
			UiThread.InvokePendingActions();
			Assert.IsTrue(!listMenu.IsOpen);
			Assert.IsTrue(menuSelected == "Copy");

			// make sure click down then move off item does not select it.
			menuSelected = "";
			// open the menu
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 11, 300, 0));
			UiThread.InvokePendingActions();
			Assert.IsTrue(!listMenu.IsOpen);
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 11, 300, 0));
			UiThread.InvokePendingActions();
			Assert.IsTrue(listMenu.IsOpen);

			// click down on the first item
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 11, 290, 0));
			UiThread.InvokePendingActions();
			Assert.IsTrue(listMenu.IsOpen);
			// move off of it
			container.OnMouseMove(new MouseEventArgs(MouseButtons.None, 1, 5, 290, 0));
			UiThread.InvokePendingActions();
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 5, 290, 0));
			UiThread.InvokePendingActions();
			Assert.IsTrue(!listMenu.IsOpen);
			Assert.IsTrue(menuSelected == "");

			// make sure click down and then move to new items selects the new item.

			// click and draw down to item should work as well
		}

		[Test, Apartment(ApartmentState.STA), /* Test was unstable, putting back in rotation with updates... */]
		public async Task DisabledMenuItemsWorkCorrectly()
		{
			int item1ClickCount = 0;
			int item2ClickCount = 0;
			int item3ClickCount = 0;

			DropDownList testList = new DropDownList("no selection", RGBA_Bytes.Blue, RGBA_Bytes.Green)
			{
				MenuItemsBackgroundColor = RGBA_Bytes.White,
				MenuItemsBackgroundHoverColor = RGBA_Bytes.LightGray,
				Name = "menu1",
			};

			AutomationTest testToRun = (testRunner) =>
			{
				Assert.AreEqual(0, item1ClickCount);
				Assert.AreEqual(0, item2ClickCount);
				Assert.AreEqual(0, item3ClickCount);

				testRunner.ClickByName("menu1");
				testRunner.ClickByName("item1");

				testRunner.Delay(() => !testList.IsOpen, 2);
				Assert.IsTrue(!testList.IsOpen);
				Assert.AreEqual(1, item1ClickCount);
				Assert.AreEqual(0, item2ClickCount);
				Assert.AreEqual(0, item3ClickCount);

				testRunner.ClickByName("menu1");
				testRunner.ClickByName("item2");

				testRunner.Delay(() => !testList.IsOpen, 2);
				Assert.IsTrue(!testList.IsOpen);
				Assert.AreEqual(1, item1ClickCount);
				Assert.AreEqual(1, item2ClickCount);
				Assert.AreEqual(0, item3ClickCount);

				testRunner.ClickByName("menu1");
				testRunner.ClickByName("item3");

				testRunner.Delay(() => testList.IsOpen, 2);
				Assert.IsTrue(testList.IsOpen, "It should remain open when clicking on a disabled item.");
				Assert.AreEqual(1, item1ClickCount);
				Assert.AreEqual(1, item2ClickCount);
				Assert.AreEqual(0, item3ClickCount);
				testRunner.ClickByName("item2");

				testRunner.Delay(() => !testList.IsOpen, 2);
				Assert.IsTrue(!testList.IsOpen);
				Assert.AreEqual(1, item1ClickCount);
				Assert.AreEqual(2, item2ClickCount);
				Assert.AreEqual(0, item3ClickCount);

				testRunner.ClickByName("menu1");
				testRunner.ClickByName("OffMenu");

				testRunner.Delay(() => !testList.IsOpen, 2);
				Assert.IsTrue(!testList.IsOpen);

				testRunner.ClickByName("menu1");
				Assert.IsTrue(testList.IsOpen);
				testRunner.ClickByName("item3");
				Assert.IsTrue(testList.IsOpen);

				testRunner.MoveToByName("OffMenu");
				Assert.IsTrue(testList.IsOpen);

				testRunner.ClickByName("OffMenu");

				testRunner.Delay(() => !testList.IsOpen, 2);
				Assert.IsTrue(!testList.IsOpen, "had a bug where after clicking a disabled item would not close clicking outside");

				return Task.FromResult(0);
			};

			var menuTestContainer = new SystemWindow(300, 200)
			{
				BackgroundColor = RGBA_Bytes.White,
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
				BackgroundColor = RGBA_Bytes.Cyan,
				Name = "OffMenu",
			});

			await AutomationRunner.ShowWindowAndExecuteTests(menuTestContainer, testToRun, 20);
		}

		[Test]
		public void DropDownListTests()
		{
			string menuSelected = "";

			GuiWidget container = new GuiWidget(400, 400);
			DropDownList listMenu = new DropDownList("- Select Something -", RGBA_Bytes.Black, RGBA_Bytes.Gray);
			listMenu.OriginRelativeParent = new Vector2(10, 300);

			// Set padding to achieve targets expected by hard-coded values
			listMenu.MenuItemsPadding = new BorderDouble(0, 0, 30, 0);

			MenuItem cutMenuItem = new MenuItem(new TextWidget("Cut"));
			cutMenuItem.Selected += (sender, e) => { menuSelected = "Cut"; };
			listMenu.MenuItems.Add(cutMenuItem);

			MenuItem copyMenuItem = new MenuItem(new TextWidget("Copy"));
			copyMenuItem.Selected += (sender, e) => { menuSelected = "Copy"; };
			listMenu.MenuItems.Add(copyMenuItem);

			MenuItem pastMenuItem = new MenuItem(new TextWidget("Paste"));
			pastMenuItem.Selected += (sender, e) => { menuSelected = "Paste"; };
			listMenu.MenuItems.Add(pastMenuItem);

			container.AddChild(listMenu);

			Assert.IsTrue(!listMenu.IsOpen);

			// open the menu
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 11, 300, 0));
			UiThread.InvokePendingActions();
			Assert.IsTrue(!listMenu.IsOpen);
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 11, 300, 0));
			UiThread.InvokePendingActions();
			Assert.IsTrue(listMenu.IsOpen);

			// click on menu again to close
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 11, 300, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 11, 300, 0));
			UiThread.InvokePendingActions();
			Assert.IsTrue(!listMenu.IsOpen);

			// open the menu
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 11, 300, 0));
			UiThread.InvokePendingActions();
			Assert.IsTrue(!listMenu.IsOpen);
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 11, 300, 0));
			UiThread.InvokePendingActions();
			Assert.IsTrue(listMenu.IsOpen);

			// click off menu to close
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 5, 299, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 5, 299, 0));
			UiThread.InvokePendingActions();
			Assert.IsTrue(!listMenu.IsOpen);

			// open the menu
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 11, 300, 0));
			UiThread.InvokePendingActions();
			Assert.IsTrue(!listMenu.IsOpen);
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 11, 300, 0));
			UiThread.InvokePendingActions();
			Assert.IsTrue(listMenu.IsOpen);

			// select the first item
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 11, 290, 0));
			UiThread.InvokePendingActions();
			Assert.IsTrue(listMenu.IsOpen);
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 11, 290, 0));
			UiThread.InvokePendingActions();
			Assert.IsTrue(!listMenu.IsOpen);
			Assert.IsTrue(menuSelected == "Cut");

			// open the menu
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 11, 300, 0));
			UiThread.InvokePendingActions();
			Assert.IsTrue(!listMenu.IsOpen);
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 11, 300, 0));
			UiThread.InvokePendingActions();
			Assert.IsTrue(listMenu.IsOpen);

			// select the second item
			menuSelected = "";
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 11, 275, 0));
			UiThread.InvokePendingActions();
			Assert.IsTrue(listMenu.IsOpen);
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 11, 275, 0));
			UiThread.InvokePendingActions();
			Assert.IsTrue(!listMenu.IsOpen);
			Assert.IsTrue(menuSelected == "Copy");

			// make sure click down then move off item does not select it.
			menuSelected = "";
			// open the menu
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 11, 300, 0));
			UiThread.InvokePendingActions();
			Assert.IsTrue(!listMenu.IsOpen);
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 11, 300, 0));
			UiThread.InvokePendingActions();
			Assert.IsTrue(listMenu.IsOpen);

			// click down on the first item
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 11, 290, 0));
			UiThread.InvokePendingActions();
			Assert.IsTrue(listMenu.IsOpen);
			// move off of it
			container.OnMouseMove(new MouseEventArgs(MouseButtons.None, 1, 5, 290, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 5, 290, 0));
			UiThread.InvokePendingActions();
			Assert.IsTrue(!listMenu.IsOpen);
			Assert.IsTrue(menuSelected == "");

			// make sure click down and then move to new items selects the new item.

			// click and draw down to item should work as well
		}
	}
}
#endif
