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

			// all the menu itmes should be added to the open menu
			Assert.IsTrue(cutMenuItem.Parent != null);
			Assert.IsTrue(copyMenuItem.Parent != null);
			Assert.IsTrue(pastMenuItem.Parent != null);

			// click on menu again to close
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 11, 300, 0));
			UiThread.InvokePendingActions();
			Assert.IsTrue(!listMenu.IsOpen);
			// all the mune itmes should be removed from the closed menu
			Assert.IsTrue(cutMenuItem.Parent == null);
			Assert.IsTrue(copyMenuItem.Parent == null);
			Assert.IsTrue(pastMenuItem.Parent == null);

			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 11, 300, 0));
			UiThread.InvokePendingActions();
			Assert.IsTrue(!listMenu.IsOpen);

			// all the menu itmes should be removed from the closed menu
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
			// all the menu itmes should be added to the open menu
			Assert.IsTrue(cutMenuItem.Parent != null);
			Assert.IsTrue(copyMenuItem.Parent != null);
			Assert.IsTrue(pastMenuItem.Parent != null);

			// click off menu to close
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 5, 299, 0));
			UiThread.InvokePendingActions();
			Assert.IsTrue(!listMenu.IsOpen);
			// all the mune itmes should be removed from the closed menu
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

		[Test, RequiresSTA, RunInApplicationDomain]
		public void MenuDisabledItemsWorkCorrectlyActual()
		{
			int item1ClickCount = 0;
			int item2ClickCount = 0;
			int item3ClickCount = 0;
			var menuTestContainer = new SystemWindow(300, 200)
			{
				BackgroundColor = RGBA_Bytes.White,
				Name = "SystemWindow",
			};

			DropDownList testList = new DropDownList("no selection", RGBA_Bytes.Blue, RGBA_Bytes.Green)
			{
				MenuItemsBackgroundColor = RGBA_Bytes.White,
				MenuItemsBackgroundHoverColor = RGBA_Bytes.LightGray,
				Name = "menu1",
			};

			Action<AutomationTesterHarness> testToRun = (AutomationTesterHarness resultsHarness) =>
			{
				try
				{
					AutomationRunner testRunner = new AutomationRunner();
					testRunner.Wait(1);

					// Now do the actions specific to this test. (replace this for new tests)
					{
						resultsHarness.AddTestResult(item1ClickCount == 0);
						resultsHarness.AddTestResult(item2ClickCount == 0);
						resultsHarness.AddTestResult(item3ClickCount == 0);

						if (true)
						{
							testRunner.ClickByName("menu1", 5);
							testRunner.ClickByName("item1", 5);
							testRunner.Wait(.1);
							resultsHarness.AddTestResult(!testList.IsOpen);
							resultsHarness.AddTestResult(item1ClickCount == 1);
							resultsHarness.AddTestResult(item2ClickCount == 0);
							resultsHarness.AddTestResult(item3ClickCount == 0);

							testRunner.ClickByName("menu1", 5);
							testRunner.ClickByName("item2", 5);
							testRunner.Wait(.1);
							resultsHarness.AddTestResult(!testList.IsOpen);
							resultsHarness.AddTestResult(item1ClickCount == 1);
							resultsHarness.AddTestResult(item2ClickCount == 1);
							resultsHarness.AddTestResult(item3ClickCount == 0);

							testRunner.ClickByName("menu1", 5);
							testRunner.ClickByName("item3", 5);
							testRunner.Wait(.1);
							resultsHarness.AddTestResult(testList.IsOpen, "It should remain open when clicking on a disabled item.");
							resultsHarness.AddTestResult(item1ClickCount == 1);
							resultsHarness.AddTestResult(item2ClickCount == 1);
							resultsHarness.AddTestResult(item3ClickCount == 0);
							testRunner.ClickByName("item2", 5);
							testRunner.Wait(.1);
							resultsHarness.AddTestResult(!testList.IsOpen);
							resultsHarness.AddTestResult(item1ClickCount == 1);
							resultsHarness.AddTestResult(item2ClickCount == 2);
							resultsHarness.AddTestResult(item3ClickCount == 0);

							testRunner.ClickByName("menu1", 5);
							testRunner.ClickByName("OffMenu", 5);
							testRunner.Wait(.1);
							resultsHarness.AddTestResult(!testList.IsOpen);
						}
						testRunner.ClickByName("menu1", 5);
						testRunner.ClickByName("item3", 5);
						testRunner.ClickByName("OffMenu", 5);
						resultsHarness.AddTestResult(!testList.IsOpen, "had a bug where after clicking a disabled item would not close clicking outside");
					}
				}
				catch
				{
					UiThread.RunOnIdle(menuTestContainer.Close, 1);
				}
			};

			testList.AddItem("item1", clickAction: (s,e) =>
			{
				item1ClickCount++;
			}).Name = "item1";
			testList.AddItem("item2", clickAction: (s, e) =>
			{
				item2ClickCount++;
			}).Name = "item2";
			var item3 = testList.AddItem("item3", clickAction: (s, e) =>
			{
				item3ClickCount++;
			});
			item3.Name = "item3";
			item3.Enabled = false;
			menuTestContainer.AddChild(testList);

			menuTestContainer.AddChild(new GuiWidget(20, 20)
			{
				OriginRelativeParent = new Vector2(160, 150),
				BackgroundColor = RGBA_Bytes.Cyan,
				Name = "OffMenu",
			});

			AutomationTesterHarness testHarness = AutomationTesterHarness.ShowWindowAndExectueTests(menuTestContainer, testToRun, 20);

			Assert.IsTrue(testHarness.AllTestsPassed);
			Assert.IsTrue(testHarness.TestCount == 21); // make sure we can all our tests
		}

		[Test]
		public void MenuDisabledItemsWorkCorrectly()
		{
			int item1ClickCount = 0;
			int item2ClickCount = 0;
			int item3ClickCount = 0;
			GuiWidget fakeSystemWindow = new GuiWidget(300, 200)
			{
				Name = "SystemWindowBase",
			};

			GuiWidget menuTestContainer = new GuiWidget(300, 200)
			{
				BackgroundColor = RGBA_Bytes.White,
				Name = "SystemWindow",
			};
			fakeSystemWindow.AddChild(menuTestContainer);

			DropDownList testList = new DropDownList("no selection", RGBA_Bytes.Blue, RGBA_Bytes.Green)
			{
				MenuItemsBackgroundColor = RGBA_Bytes.White,
				MenuItemsBackgroundHoverColor = RGBA_Bytes.LightGray,
				Name = "menu1",
			};

			testList.AddItem("item1", clickAction: (s, e) =>
			{
				item1ClickCount++;
			}).Name = "item1";
			testList.AddItem("item2", clickAction: (s, e) =>
			{
				item2ClickCount++;
			}).Name = "item2";
			var item3 = testList.AddItem("item3", clickAction: (s, e) =>
			{
				item3ClickCount++;
			});
			item3.Name = "item3";
			item3.Enabled = false;
			menuTestContainer.AddChild(testList);

			menuTestContainer.AddChild(new GuiWidget(20, 20)
			{
				OriginRelativeParent = new Vector2(160, 150),
				BackgroundColor = RGBA_Bytes.Cyan,
				Name = "OffMenu",
			});

			int itemHeight = 14;
			// Now do the actions specific to this test. (replace this for new tests)
			{
				Assert.IsTrue(item1ClickCount == 0);
				Assert.IsTrue(item2ClickCount == 0);
				Assert.IsTrue(item3ClickCount == 0);

				// "menu1"
				fakeSystemWindow.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 10, itemHeight / 2 + itemHeight * 0, 0));
				fakeSystemWindow.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 10, itemHeight /2 + itemHeight * 0, 0));
				UiThread.InvokePendingActions();
				Assert.IsTrue(testList.IsOpen);
				// "item1"
				fakeSystemWindow.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 10, itemHeight / 2 + itemHeight * 3, 0));
				fakeSystemWindow.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 10, itemHeight / 2 + itemHeight * 3, 0));
				UiThread.InvokePendingActions();
				Assert.IsTrue(!testList.IsOpen);
				Assert.IsTrue(item1ClickCount == 1);
				Assert.IsTrue(item2ClickCount == 0);
				Assert.IsTrue(item3ClickCount == 0);

				//testRunner.ClickByName("menu1", 5);
				fakeSystemWindow.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 10, itemHeight / 2 + itemHeight * 0, 0));
				fakeSystemWindow.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 10, itemHeight / 2 + itemHeight * 0, 0));
				UiThread.InvokePendingActions();
				Assert.IsTrue(testList.IsOpen);
				//testRunner.ClickByName("item2", 5);
				fakeSystemWindow.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 10, itemHeight / 2 + itemHeight * 2, 0));
				fakeSystemWindow.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 10, itemHeight / 2 + itemHeight * 2, 0));
				UiThread.InvokePendingActions();
				//testRunner.Wait(.1);
				Assert.IsTrue(!testList.IsOpen);
				Assert.IsTrue(item1ClickCount == 1);
				Assert.IsTrue(item2ClickCount == 1);
				Assert.IsTrue(item3ClickCount == 0);

				//testRunner.ClickByName("menu1", 5);
				fakeSystemWindow.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 10, itemHeight / 2 + itemHeight * 0, 0));
				fakeSystemWindow.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 10, itemHeight / 2 + itemHeight * 0, 0));
				UiThread.InvokePendingActions();
				Assert.IsTrue(testList.IsOpen);
				//testRunner.ClickByName("item3", 5);
				fakeSystemWindow.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 10, itemHeight / 2 + itemHeight * 1, 0));
				fakeSystemWindow.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 10, itemHeight / 2 + itemHeight * 1, 0));
				UiThread.InvokePendingActions();
				//testRunner.Wait(.1);
				Assert.IsTrue(testList.IsOpen, "It should remain open when clicking on a disabled item.");
				Assert.IsTrue(item1ClickCount == 1);
				Assert.IsTrue(item2ClickCount == 1);
				Assert.IsTrue(item3ClickCount == 0);
				//testRunner.ClickByName("item2", 5);
				fakeSystemWindow.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 10, itemHeight / 2 + itemHeight * 2, 0));
				fakeSystemWindow.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 10, itemHeight / 2 + itemHeight * 2, 0));
				UiThread.InvokePendingActions();
				//testRunner.Wait(.1);
				Assert.IsTrue(!testList.IsOpen);
				Assert.IsTrue(item1ClickCount == 1);
				Assert.IsTrue(item2ClickCount == 2);
				Assert.IsTrue(item3ClickCount == 0);

				//testRunner.ClickByName("menu1", 5);
				fakeSystemWindow.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 10, itemHeight / 2 + itemHeight * 0, 0));
				fakeSystemWindow.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 10, itemHeight / 2 + itemHeight * 0, 0));
				UiThread.InvokePendingActions();
				Assert.IsTrue(testList.IsOpen);
				//testRunner.ClickByName("OffMenu", 5);
				fakeSystemWindow.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 200, itemHeight / 2 + itemHeight * 3, 0));
				fakeSystemWindow.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 200, itemHeight / 2 + itemHeight * 3, 0));
				UiThread.InvokePendingActions();
				//testRunner.Wait(.1);
				Assert.IsTrue(!testList.IsOpen);
				Assert.IsTrue(item1ClickCount == 1);
				Assert.IsTrue(item2ClickCount == 2);
				Assert.IsTrue(item3ClickCount == 0);

				//testRunner.ClickByName("menu1", 5);
				fakeSystemWindow.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 10, itemHeight / 2 + itemHeight * 0, 0));
				fakeSystemWindow.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 10, itemHeight / 2 + itemHeight * 0, 0));
				UiThread.InvokePendingActions();
				Assert.IsTrue(testList.IsOpen);
				Assert.IsTrue(item1ClickCount == 1);
				Assert.IsTrue(item2ClickCount == 2);
				Assert.IsTrue(item3ClickCount == 0);
				//testRunner.ClickByName("item3", 5);
				fakeSystemWindow.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 10, itemHeight / 2 + itemHeight * 1, 0));
				fakeSystemWindow.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 10, itemHeight / 2 + itemHeight * 1, 0));
				UiThread.InvokePendingActions();
				Assert.IsTrue(testList.IsOpen);
				Assert.IsTrue(item1ClickCount == 1);
				Assert.IsTrue(item2ClickCount == 2);
				Assert.IsTrue(item3ClickCount == 0);
				//testRunner.ClickByName("OffMenu", 5);
				fakeSystemWindow.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 200, itemHeight / 2 + itemHeight * 3, 0));
				fakeSystemWindow.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 200, itemHeight / 2 + itemHeight * 3, 0));
				UiThread.InvokePendingActions();
				Assert.IsTrue(item1ClickCount == 1);
				Assert.IsTrue(item2ClickCount == 2);
				Assert.IsTrue(item3ClickCount == 0);
				Assert.IsTrue(!testList.IsOpen, "had a bug where after clicking a disabled item would not close clicking outside");
			}

			//testRunner.Wait(1);
		}

		[Test]
		public void DropDownListTests()
		{
			string menuSelected = "";

			GuiWidget container = new GuiWidget(400, 400);
			DropDownList listMenu = new DropDownList("- Select Something -", RGBA_Bytes.Black, RGBA_Bytes.Gray);
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
