/*
Copyright (c) 2013, Lars Brubaker
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
using System.Linq;
using System.Text;

using NUnit.Framework;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.UI.Tests
{
    public class MenuTests
    {
        public static bool saveImagesForDebug = false;

        void OutputImage(ImageBuffer imageToOutput, string fileName)
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
            Menu listMenu = new Menu(new TextWidget("Edit"));
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
            UiThread.DoRunAllPending();
            Assert.IsTrue(listMenu.IsOpen);

            // click on menu again to close
            container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 11, 300, 0));
            UiThread.DoRunAllPending();
            container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 11, 300, 0));
            UiThread.DoRunAllPending();
            Assert.IsTrue(!listMenu.IsOpen);

            // open the menu
            container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 11, 300, 0));
            UiThread.DoRunAllPending();
            Assert.IsTrue(!listMenu.IsOpen);
            container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 11, 300, 0));
            UiThread.DoRunAllPending();
            Assert.IsTrue(listMenu.IsOpen);

            // click off menu to close
            container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 5, 299, 0));
            UiThread.DoRunAllPending();
            container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 5, 299, 0));
            UiThread.DoRunAllPending();
            Assert.IsTrue(!listMenu.IsOpen);

            // open the menu
            container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 11, 300, 0));
            UiThread.DoRunAllPending();
            Assert.IsTrue(!listMenu.IsOpen);
            container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 11, 300, 0));
            UiThread.DoRunAllPending();
            Assert.IsTrue(listMenu.IsOpen);

            // select the first item
            container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 11, 290, 0));
            UiThread.DoRunAllPending();
            Assert.IsTrue(listMenu.IsOpen);
            container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 11, 290, 0));
            UiThread.DoRunAllPending();
            Assert.IsTrue(!listMenu.IsOpen);
            Assert.IsTrue(menuSelected == "Cut");

            // open the menu
            container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 11, 300, 0));
            UiThread.DoRunAllPending();
            Assert.IsTrue(!listMenu.IsOpen);
            container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 11, 300, 0));
            UiThread.DoRunAllPending();
            Assert.IsTrue(listMenu.IsOpen);

            // select the second item
            menuSelected = "";
            container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 11, 275, 0));
            UiThread.DoRunAllPending();
            Assert.IsTrue(listMenu.IsOpen);
            container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 11, 275, 0));
            UiThread.DoRunAllPending();
            Assert.IsTrue(!listMenu.IsOpen);
            Assert.IsTrue(menuSelected == "Copy");

            // make sure click down then move off item does not select it.
            menuSelected = "";
            // open the menu
            container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 11, 300, 0));
            UiThread.DoRunAllPending();
            Assert.IsTrue(!listMenu.IsOpen);
            container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 11, 300, 0));
            UiThread.DoRunAllPending();
            Assert.IsTrue(listMenu.IsOpen);

            // click down on the first item
            container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 11, 290, 0));
            UiThread.DoRunAllPending();
            Assert.IsTrue(listMenu.IsOpen);
            // move off of it
            container.OnMouseMove(new MouseEventArgs(MouseButtons.None, 1, 5, 290, 0));
            UiThread.DoRunAllPending();
            container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 5, 290, 0));
            UiThread.DoRunAllPending();
            Assert.IsTrue(!listMenu.IsOpen);
            Assert.IsTrue(menuSelected == "");

            // make sure click down and then move to new items selects the new item.

            // click and draw down to item should work as well
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
            UiThread.DoRunAllPending();
            Assert.IsTrue(!listMenu.IsOpen);
            container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 11, 300, 0));
            UiThread.DoRunAllPending();
            Assert.IsTrue(listMenu.IsOpen);

            // click on menu again to close
            container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 11, 300, 0));
            container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 11, 300, 0));
            UiThread.DoRunAllPending();
            Assert.IsTrue(!listMenu.IsOpen);

            // open the menu
            container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 11, 300, 0));
            UiThread.DoRunAllPending();
            Assert.IsTrue(!listMenu.IsOpen);
            container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 11, 300, 0));
            UiThread.DoRunAllPending();
            Assert.IsTrue(listMenu.IsOpen);

            // click off menu to close
            container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 5, 299, 0));
            container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 5, 299, 0));
            UiThread.DoRunAllPending();
            Assert.IsTrue(!listMenu.IsOpen);

            // open the menu
            container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 11, 300, 0));
            UiThread.DoRunAllPending();
            Assert.IsTrue(!listMenu.IsOpen);
            container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 11, 300, 0));
            UiThread.DoRunAllPending();
            Assert.IsTrue(listMenu.IsOpen);

            // select the first item
            container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 11, 290, 0));
            UiThread.DoRunAllPending();
            Assert.IsTrue(listMenu.IsOpen);
            container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 11, 290, 0));
            UiThread.DoRunAllPending();
            Assert.IsTrue(!listMenu.IsOpen);
            Assert.IsTrue(menuSelected == "Cut");

            // open the menu
            container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 11, 300, 0));
            UiThread.DoRunAllPending();
            Assert.IsTrue(!listMenu.IsOpen);
            container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 11, 300, 0));
            UiThread.DoRunAllPending();
            Assert.IsTrue(listMenu.IsOpen);

            // select the second item
            menuSelected = "";
            container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 11, 275, 0));
            UiThread.DoRunAllPending();
            Assert.IsTrue(listMenu.IsOpen);
            container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 11, 275, 0));
            UiThread.DoRunAllPending();
            Assert.IsTrue(!listMenu.IsOpen);
            Assert.IsTrue(menuSelected == "Copy");

            // make sure click down then move off item does not select it.
            menuSelected = "";
            // open the menu
            container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 11, 300, 0));
            UiThread.DoRunAllPending();
            Assert.IsTrue(!listMenu.IsOpen);
            container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 11, 300, 0));
            UiThread.DoRunAllPending();
            Assert.IsTrue(listMenu.IsOpen);

            // click down on the first item
            container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 11, 290, 0));
            UiThread.DoRunAllPending();
            Assert.IsTrue(listMenu.IsOpen);
            // move off of it
            container.OnMouseMove(new MouseEventArgs(MouseButtons.None, 1, 5, 290, 0));
            container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 5, 290, 0));
            UiThread.DoRunAllPending();
            Assert.IsTrue(!listMenu.IsOpen);
            Assert.IsTrue(menuSelected == "");

            // make sure click down and then move to new items selects the new item.

            // click and draw down to item should work as well
        }
    }
}   
