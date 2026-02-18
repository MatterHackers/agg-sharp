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
using System.IO;
using System.Threading.Tasks;
using MatterHackers.Agg.Font;
using MatterHackers.VectorMath;
using Agg.Tests.Agg;

namespace MatterHackers.Agg.UI.Tests
{
    [MhTestFixture]
	public class TextEditTests
	{
		public static bool SaveImagesForDebug = false;

		private void OutputImage(ImageBuffer imageToOutput, string fileName)
		{
			if (SaveImagesForDebug)
			{
				var dirAndFileName = Path.Combine("C:/Temp", fileName);
				ImageTgaIO.Save(imageToOutput, dirAndFileName);
			}
		}

		public void SendKeyDown(Keys keyDown, GuiWidget reciever)
		{
			var keyDownEvent = new KeyEventArgs(keyDown);
			reciever.OnKeyDown(keyDownEvent);
		}

		public void SendKey(Keys keyDown, char keyPressed, GuiWidget reciever)
		{
			var keyDownEvent = new KeyEventArgs(keyDown);
			reciever.OnKeyDown(keyDownEvent);
			if (!keyDownEvent.SuppressKeyPress)
			{
				var keyPressEvent = new KeyPressEventArgs(keyPressed);
				reciever.OnKeyPress(keyPressEvent);
			}
		}

        [MhTest]
        public void CorectLineCounts()
		{
			var lines7 = @"; activate T0
; move up a bit
G91 
G1 Z1 F1500
G90
; do the switch to T0
G1 X-29.5 F6000 ; NO_PROCESSING";
			var printer7 = new TypeFacePrinter(lines7);
			MhAssert.Equal(7, printer7.NumLines());

			var lines8 = @"; activate T0
; move up a bit
G91 
G1 Z1 F1500
G90
; do the switch to T0
G1 X-29.5 F6000 ; NO_PROCESSING
";
			var printer8 = new TypeFacePrinter(lines8);
			MhAssert.Equal(8, printer8.NumLines());
		}

        [MhTest]
        public void TextEditTextSelectionTests()
		{
			var container = new GuiWidget
			{
				LocalBounds = new RectangleDouble(0, 0, 200, 200)
			};
			var editField1 = new TextEditWidget("", 0, 0, pixelWidth: 51);
			container.AddChild(editField1);

			// select the control and type something in it
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 0, 0, 0, 0));
			SendKey(Keys.A, 'a', container);
			MhAssert.True(editField1.Text == "a", "It should have a in it.");

			// select the beginning again and type something else in it
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 0, 0, 0, 0));
			SendKey(Keys.B, 'b', container);
			MhAssert.True(editField1.Text == "ba", "It should have ba in it.");

			// select the ba and delete them
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0));
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 0, 15, 0, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 0, 15, 0, 0));
			SendKey(Keys.Back, ' ', container);
			MhAssert.True(editField1.Text == "", "It should have nothing in it.");

			// select the other way
			editField1.Text = "ab";
			MhAssert.True(editField1.Text == "ab", "It should have ab in it.");
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 15, 0, 0));
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 0, 0, 0, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 0, 0, 0, 0));
			SendKey(Keys.Back, ' ', container);
			MhAssert.True(editField1.Text == "", "It should have nothing in it.");

			// select the other way but start far to the right
			editField1.Text = "abc";
			MhAssert.True(editField1.Text == "abc", "It should have abc in it.");
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 30, 0, 0));
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 0, 0, 0, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 0, 0, 0, 0));
			SendKey(Keys.Back, ' ', container);
			MhAssert.True(editField1.Text == "", "It should have nothing in it.");

			// double click empty does nothing
			// select the other way but start far to the right
			editField1.Text = "";
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 1, 0, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 0, 0, 0, 0));
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 2, 1, 0, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 0, 0, 0, 0));
			MhAssert.Equal("", editField1.Selection);//, "First word selected");

            // double click first word selects
            editField1.Text = "abc 123";
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 1, 0, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 0, 0, 0, 0));
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 2, 1, 0, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 0, 0, 0, 0));
			MhAssert.Equal("abc", editField1.Selection);//, "First word selected");

            // double click last word selects
            editField1.Text = "abc 123";
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 30, 0, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 0, 0, 0, 0));
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 2, 30, 0, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 0, 0, 0, 0));
			MhAssert.Equal("123", editField1.Selection);//, "Second word selected");

            container.Close();
		}

        [MhTest]
        public void TextSelectionWithShiftClick()
		{
			const string fullText = "This is a text";

			var container = new GuiWidget(200, 200);
			var editField1 = new TextEditWidget(fullText, pixelWidth: 100);
			container.AddChild(editField1);

			// select all from left to right with shift click
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 1, 0, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 0, 1, 0, 0));
			MhAssert.Equal(0, editField1.CharIndexToInsertBefore);
			MhAssert.Equal("", editField1.Selection);
			Keyboard.SetKeyDownState(Keys.Shift, true);
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 100, 0, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 0, 100, 0, 0));
			Keyboard.SetKeyDownState(Keys.Shift, false);
			MhAssert.Equal(fullText.Length, editField1.CharIndexToInsertBefore);
			MhAssert.Equal(fullText, editField1.Selection);//, "It should select full text");

            // select all from right to left with shift click
            container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 100, 0, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 0, 100, 0, 0));
			MhAssert.Equal(fullText.Length, editField1.CharIndexToInsertBefore);
			MhAssert.Equal("", editField1.Selection);
			Keyboard.SetKeyDownState(Keys.Shift, true);
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 1, 0, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 0, 1, 0, 0));
			Keyboard.SetKeyDownState(Keys.Shift, false);
			MhAssert.Equal(0, editField1.CharIndexToInsertBefore);
			MhAssert.Equal(fullText, editField1.Selection);//, "It should select full text");

            // select parts of the text with shift click
            container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 1, 0, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 0, 1, 0, 0));
			SendKey(Keys.Control | Keys.Right, ' ', container);
			SendKey(Keys.Control | Keys.Right, ' ', container);
			MhAssert.Equal("This is ".Length, editField1.CharIndexToInsertBefore);
			MhAssert.Equal("", editField1.Selection);
			Keyboard.SetKeyDownState(Keys.Shift, true);
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 100, 0, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 0, 100, 0, 0));
			Keyboard.SetKeyDownState(Keys.Shift, false);
			MhAssert.Equal(fullText.Length, editField1.CharIndexToInsertBefore);
			MhAssert.Equal("a text", editField1.Selection);//, "It should select second part of the text");
            Keyboard.SetKeyDownState(Keys.Shift, true);
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 1, 0, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 0, 1, 0, 0));
			Keyboard.SetKeyDownState(Keys.Shift, false);
			MhAssert.Equal(0, editField1.CharIndexToInsertBefore);
			MhAssert.Equal("This is ", editField1.Selection);//, "It should select first part of the text");

            container.Close();
		}

        [MhTest]
        public void TextChangedEventsTests()
		{
			var container = new GuiWidget
			{
				Name = "container",
				LocalBounds = new RectangleDouble(0, 0, 200, 200)
			};
			var editField1 = new TextEditWidget("", 0, 0, pixelWidth: 20)
			{
				Name = "editField1"
			};
			MhAssert.True(editField1.BoundsRelativeToParent.Top < 40, "We make this assumption in the code below, so make sure it's true.");
			bool textField1EditComplete = false;
			editField1.EditComplete += (sender, e) => { textField1EditComplete = true; };
			bool textField1LostFocus = false;
			bool textField1GotFocus = false;
			editField1.ContainsFocusChanged += (sender, e) =>
			{
				if ((sender as GuiWidget) != null
					&& (sender as GuiWidget).ContainsFocus)
				{
					textField1GotFocus = true;
				}
				else
				{
					textField1LostFocus = true;
				}
			};
			container.AddChild(editField1);

			var editField2 = new TextEditWidget("", 0, 40, pixelWidth: 20)
			{
				Name = "editField2"
			};
			container.AddChild(editField2);

			// mouse select on the control when it contains nothing
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 1, 1, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 0, 1, 1, 0));
			MhAssert.True(textField1GotFocus);
			MhAssert.False(textField1EditComplete);
			SendKey(Keys.B, 'b', container);
			MhAssert.True(editField1.Text == "b");
			MhAssert.False(textField1EditComplete, "We do not change with each keystroke.");
			SendKey(Keys.Enter, '\n', container);
			MhAssert.True(textField1EditComplete, "Enter must send a EditComplete if changed.");
			textField1EditComplete = false;
			SendKey(Keys.A, 'a', container);
			MhAssert.True(editField1.Text == "ba");
			MhAssert.False(textField1EditComplete, "We do not change with each keystroke.");

			MhAssert.False(textField1LostFocus);
			textField1GotFocus = false;
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 1, 41, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 0, 1, 1, 0));
			SendKey(Keys.E, 'e', container);
			MhAssert.True(textField1LostFocus);
			MhAssert.True(textField1EditComplete, "Loosing focus should send a text changed.");
			MhAssert.True(editField1.Text == "ba");
			MhAssert.True(editField2.Text == "e");

			textField1EditComplete = false;
			textField1LostFocus = false;
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 1, 1, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 0, 1, 1, 0));
			MhAssert.False(textField1LostFocus);
			MhAssert.False(textField1EditComplete);
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 1, 41, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 0, 1, 1, 0));
			MhAssert.True(textField1LostFocus);
			MhAssert.False(textField1EditComplete, "The text did not change even though we lost focus we should not call textChanged.");

			container.Close();
		}

        [MhTest]
        public void TextEditGetsFocusTests()
		{
			var container = new GuiWidget
			{
				Name = "container",
				LocalBounds = new RectangleDouble(0, 0, 200, 200)
			};
			var editField1 = new TextEditWidget("", 0, 0, pixelWidth: 160)
			{
				Name = "editField1"
			};
			container.AddChild(editField1);

			var editField2 = new TextEditWidget("", 0, 20, pixelWidth: 160)
			{
				Name = "editField2"
			};
			container.AddChild(editField2);

			// select no edit field
			MhAssert.True(editField1.Text == "");
			SendKey(Keys.D, 'a', container);
			MhAssert.True(editField1.Text == "");
			MhAssert.True(editField2.Text == "");

			// select edit field 1
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 1, 1, 0)); // we move into the widget to make sure we have separate focus and enter events.
			MhAssert.True(editField1.ContainsFocus == false);
			MhAssert.True(editField1.Focused == false);
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 1, 1, 0));
			MhAssert.True(editField1.ContainsFocus == true);
			MhAssert.True(editField1.Focused == false, "The internal text widget must be focused.");
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 1, 1, 0));
			MhAssert.True(editField1.ContainsFocus == true);
			MhAssert.True(editField1.Focused == false);
			SendKey(Keys.B, 'b', container);
			MhAssert.True(editField1.Text == "b", "It should have b a in it.");
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 150, 1, 0));
			MhAssert.True(editField1.ContainsFocus == true);
			MhAssert.True(editField1.Focused == false, "The internal text widget must be focused.");
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 150, 1, 0));
			MhAssert.True(editField1.ContainsFocus == true);
			MhAssert.True(editField1.Focused == false);
			SendKey(Keys.D, 'c', container);
			MhAssert.True(editField1.Text == "bc", "It should have b a in it.");

			// select edit field 2
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 1, 21, 0));
			MhAssert.True(editField2.ContainsFocus == true);
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 1, 21, 0));
			SendKey(Keys.D, 'd', container);
			MhAssert.True(editField1.Text == "bc", "It should have a bc in it.");
			MhAssert.True(editField2.Text == "d", "It should have d in it.");

			container.Close();
		}

        [MhTest]
        public void AddThenDeleteCausesNoVisualChange()
		{
			var container = new GuiWidget
			{
				DoubleBuffer = true,
				LocalBounds = new RectangleDouble(0, 0, 200, 200)
			};
			var editField1 = new TextEditWidget("Test", 10, 10, pixelWidth: 50);
			container.AddChild(editField1);
			container.BackBuffer.NewGraphics2D().Clear(Color.White);
			container.OnDraw(container.BackBuffer.NewGraphics2D());
			var beforeEditImage = new ImageBuffer(container.BackBuffer);
			RectangleDouble beforeLocalBounds = editField1.LocalBounds;
			Vector2 beforeOrigin = editField1.OriginRelativeParent;

			OutputImage(beforeEditImage, "z text un-edited.tga");

			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 10, 10, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 10, 10, 0));
			MhAssert.True(editField1.ContainsFocus == true);
			SendKey(Keys.B, 'b', container);
			MhAssert.True(editField1.Text == "bTest", "It should have b a in it.");
			RectangleDouble afterBLocalBounds = editField1.LocalBounds;
			MhAssert.True(beforeLocalBounds.Bottom == afterBLocalBounds.Bottom && beforeLocalBounds.Top == afterBLocalBounds.Top);

			SendKey(Keys.Back, ' ', container);
			MhAssert.True(editField1.Text == "Test", "It should not have b a in it.");

			RectangleDouble afterLocalBounds = editField1.LocalBounds;
			Vector2 afterOrigin = editField1.OriginRelativeParent;

			MhAssert.True(beforeLocalBounds == afterLocalBounds);
			MhAssert.True(beforeOrigin == afterOrigin);

			// click off it so the cursor is not in it.
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 1, 1, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 1, 1, 0));
			MhAssert.True(editField1.Focused == false);

			container.BackBuffer.NewGraphics2D().Clear(Color.White);
			container.OnDraw(container.BackBuffer.NewGraphics2D());
			OutputImage(container.BackBuffer, "z text edited.tga");

			MhAssert.True(container.BackBuffer == beforeEditImage);
		}

        [MhTest]
        public void MultiLineTests()
		{
			// make sure selection ranges are always working
			{
				//Clipboard.SetSystemClipboard(new SimulatedClipboard());

				var singleLine = new InternalTextEditWidget("test", 12, false, 0);

				void TestRange(int start, int end, string expected)
				{
					singleLine.CharIndexToInsertBefore = start;
					singleLine.SelectionIndexToStartBefore = end;
					singleLine.Selecting = true;
					MhAssert.Equal(expected, singleLine.Selection);
					singleLine.CopySelection();

					MhAssert.Equal(expected, Clipboard.Instance.GetText());
				}

				// ask for some selections
				TestRange(-10, -8, "");
				TestRange(-8, -10, "");
				TestRange(18, 10, "");
				TestRange(10, 18, "");
				TestRange(2, -10, "te");
				TestRange(-10, 2, "te");
				TestRange(18, 2, "st");
				TestRange(3, 22, "t");
			}

			{
				var singleLine = new InternalTextEditWidget("test", 12, false, 0);
				var multiLine = new InternalTextEditWidget("test\ntest\ntest", 12, true, 0);
				MhAssert.True(multiLine.Height >= singleLine.Height * 3);
			}

			// we get the typed results we expect
			{
				var container = new GuiWidget
				{
					LocalBounds = new RectangleDouble(0, 0, 200, 200)
				};
				var multiLine = new InternalTextEditWidget("\n\n\n\n", 12, true, 0);
				container.AddChild(multiLine);

				container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 1, 1, 0));
				container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 1, 1, 0));
				MhAssert.True(multiLine.ContainsFocus == true);
				MhAssert.True(multiLine.SelectionIndexToStartBefore == 4);
				MhAssert.True(multiLine.Text == "\n\n\n\n");
				SendKey(Keys.A, 'a', container);
				MhAssert.True(multiLine.Text == "\n\n\n\na");
				SendKey(Keys.Up, ' ', container);
				SendKey(Keys.A, 'a', container);
				MhAssert.True(multiLine.Text == "\n\n\na\na");

				container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 1, multiLine.Height - 1, 0));
				container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 1, multiLine.Height - 1, 0));
				MhAssert.True(multiLine.ContainsFocus == true);
				MhAssert.True(multiLine.SelectionIndexToStartBefore == 0);
				MhAssert.True(multiLine.Text == "\n\n\na\na");
				SendKey(Keys.A, 'a', container);
				MhAssert.True(multiLine.Text == "a\n\n\na\na");
				SendKey(Keys.Down, ' ', container);
				SendKey(Keys.A | Keys.Shift, 'A', container);
				MhAssert.True(multiLine.Text == "a\nA\n\na\na");

				container.Close();
			}

			// make sure the insert position is correct when homed
			{
				var container = new GuiWidget
				{
					LocalBounds = new RectangleDouble(0, 0, 200, 200)
				};
				var multiLine = new InternalTextEditWidget("line1\nline2\nline3", 12, true, 0);
				container.AddChild(multiLine);

				container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 5, 1, 0));
				container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 5, 1, 0));
				MhAssert.True(multiLine.ContainsFocus == true);
				MhAssert.True(multiLine.InsertBarPosition.Y == -32);
				SendKey(Keys.Home, ' ', container);
				MhAssert.True(multiLine.InsertBarPosition.Y == -32);
				MhAssert.True(multiLine.Text == "line1\nline2\nline3");
				SendKey(Keys.A, 'a', container);
				MhAssert.True(multiLine.Text == "line1\nline2\naline3");
				SendKey(Keys.Back, ' ', container);
				MhAssert.True(multiLine.Text == "line1\nline2\nline3");
				MhAssert.True(multiLine.InsertBarPosition.Y == -32);
				container.Close();
			}

			// make sure the insert position is correct when move left to end of line
			{
				var container = new GuiWidget
				{
					LocalBounds = new RectangleDouble(0, 0, 200, 200)
				};
				var multiLine = new InternalTextEditWidget("xx", 12, true, 0);
				container.AddChild(multiLine);

				container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 1, 1, 0));
				container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 1, 1, 0));
				MhAssert.True(multiLine.ContainsFocus == true);
				MhAssert.True(multiLine.CharIndexToInsertBefore == 0);
				MhAssert.True(multiLine.InsertBarPosition.X == 0);
				SendKey(Keys.Home, ' ', container);
				MhAssert.True(multiLine.CharIndexToInsertBefore == 0);
				MhAssert.True(multiLine.InsertBarPosition.X == 0);
				SendKey(Keys.Right, ' ', container);
				MhAssert.True(multiLine.CharIndexToInsertBefore == 1);
				double leftOne = multiLine.InsertBarPosition.X;
				SendKey(Keys.Right, ' ', container);
				MhAssert.True(multiLine.CharIndexToInsertBefore == 2);
				MhAssert.True(multiLine.InsertBarPosition.X == leftOne * 2);
				container.Close();
			}

			// make sure the cursor is at the right hight when it is after a \n that is on the first line
			{
				var container = new GuiWidget
				{
					DoubleBuffer = true,
					LocalBounds = new RectangleDouble(0, 0, 200, 200)
				};
				var multiLine = new InternalTextEditWidget("\n1\n\n3\n", 12, true, 0);
				MhAssert.True(multiLine.LocalBounds.Height == 16 * 5);
				container.AddChild(multiLine);

				container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 1, multiLine.Height - 1, 0));
				container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 1, multiLine.Height - 1, 0));

				MhAssert.True(multiLine.CharIndexToInsertBefore == 0);
				MhAssert.True(multiLine.InsertBarPosition.Y == 0);

				// move past \n
				SendKey(Keys.Right, ' ', container);
				MhAssert.True(multiLine.CharIndexToInsertBefore == 1);
				MhAssert.True(multiLine.InsertBarPosition.Y == -16);

				// move past 1
				SendKey(Keys.Right, ' ', container);
				MhAssert.True(multiLine.CharIndexToInsertBefore == 2);
				MhAssert.True(multiLine.InsertBarPosition.Y == -16);

				// move past \n
				SendKey(Keys.Right, ' ', container);
				MhAssert.True(multiLine.CharIndexToInsertBefore == 3);
				MhAssert.True(multiLine.InsertBarPosition.Y == -32);

				// move past \n
				SendKey(Keys.Right, ' ', container);
				MhAssert.True(multiLine.CharIndexToInsertBefore == 4);
				MhAssert.True(multiLine.InsertBarPosition.Y == -48);

				// move past 3
				SendKey(Keys.Right, ' ', container);
				MhAssert.True(multiLine.CharIndexToInsertBefore == 5);
				MhAssert.True(multiLine.InsertBarPosition.Y == -48);

				// move past \n
				SendKey(Keys.Right, ' ', container);
				MhAssert.True(multiLine.CharIndexToInsertBefore == 6);
				MhAssert.True(multiLine.InsertBarPosition.Y == -64);
				container.Close();
			}
		}

        [MhTest]
        public void NumEditHandlesNonNumberChars()
		{
			var container = new GuiWidget
			{
				DoubleBuffer = true,
				LocalBounds = new RectangleDouble(0, 0, 200, 200)
			};
			var numberEdit = new NumberEdit(0, 0, 0, 12, 200, 16, true, true);
			container.AddChild(numberEdit);

			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 1, numberEdit.Height - 1, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 1, numberEdit.Height - 1, 0));

			MhAssert.True(numberEdit.CharIndexToInsertBefore == 0);
			MhAssert.True(numberEdit.TopLeftOffset.Y == 0);

			// type a . (non numeric character)
			SendKey(Keys.Back, ' ', container);
			SendKey(Keys.Delete, ' ', container);
			SendKey(Keys.OemMinus, '-', container);
			MhAssert.True(numberEdit.Value == 0);
			SendKey(Keys.OemPeriod, '.', container);
			MhAssert.True(numberEdit.Value == 0);
			SendKey(Keys.D0, '.', container);
			MhAssert.True(numberEdit.Value == 0);
			SendKey(Keys.A, 'A', container);
			MhAssert.True(numberEdit.Value == 0);

			container.Close();
		}

#if __ANDROID__
		[Test]
#else
        [MhTest]
#endif
        public void TextEditingSpecialKeysWork()
		{
			var container = new GuiWidget
			{
				DoubleBuffer = true,
				LocalBounds = new RectangleDouble(0, 0, 200, 200)
			};
			var textEdit = new TextEditWidget("some starting text");
			container.AddChild(textEdit);

			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 1, textEdit.Height - 1, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 1, textEdit.Height - 1, 0));

			MhAssert.True(textEdit.CharIndexToInsertBefore == 0);
			MhAssert.True(textEdit.TopLeftOffset.Y == 0);

			// test that we move to the next character correctly
			MhAssert.Equal(4, InternalTextEditWidget.IndexOfNextToken("235 12/6", 0));
			MhAssert.Equal(6, InternalTextEditWidget.IndexOfNextToken("235   12/6", 0));
			MhAssert.Equal(3, InternalTextEditWidget.IndexOfNextToken("235\n   12/6", 0));
			MhAssert.Equal(7, InternalTextEditWidget.IndexOfNextToken("235\n   12/6", 3));
			MhAssert.Equal(4, InternalTextEditWidget.IndexOfNextToken("235\n\n   12/6", 3));
			MhAssert.Equal(8, InternalTextEditWidget.IndexOfNextToken("235\n\n   12/6", 4));
			MhAssert.Equal(3, InternalTextEditWidget.IndexOfNextToken("123+ 235   12/6", 0));
			MhAssert.Equal(3, InternalTextEditWidget.IndexOfNextToken("235+12/6", 0));
			MhAssert.Equal(5, InternalTextEditWidget.IndexOfNextToken("+++++235   12/6", 0));
			MhAssert.Equal(5, InternalTextEditWidget.IndexOfNextToken("+++++235   12/6", 0));

			// test that we move to the previous character correctly
			MhAssert.Equal(7, InternalTextEditWidget.IndexOfPreviousToken("=35+12/6", 8));
			MhAssert.Equal(6, InternalTextEditWidget.IndexOfPreviousToken("35556+68384734", 10));
			MhAssert.Equal(5, InternalTextEditWidget.IndexOfPreviousToken("35556+68384734", 6));
			MhAssert.Equal(0, InternalTextEditWidget.IndexOfPreviousToken("35556+68384734", 5));
			
			MhAssert.Equal(11, InternalTextEditWidget.IndexOfPreviousToken("235\n\n   12/6", 12));
			MhAssert.Equal(10, InternalTextEditWidget.IndexOfPreviousToken("235\n\n   12/6", 11));
			MhAssert.Equal(8, InternalTextEditWidget.IndexOfPreviousToken("235\n\n   12/6", 10));
			MhAssert.Equal(5, InternalTextEditWidget.IndexOfPreviousToken("235\n\n   12/6", 8));
			MhAssert.Equal(4, InternalTextEditWidget.IndexOfPreviousToken("235\n\n   12/6", 5));
			MhAssert.Equal(0, InternalTextEditWidget.IndexOfPreviousToken("235\n\n   12/6", 4));
			MhAssert.Equal(0, InternalTextEditWidget.IndexOfPreviousToken("some starting text", 5));

			void RunWithSpecificChar(string sep, string first, string second, string third)
			{
				var startText = $"{first}{sep}{second}{sep}{third}";
				MhAssert.True(textEdit.Text == startText);
				// this is to select some text
				SendKey(Keys.Shift | Keys.Control | Keys.Right, ' ', container);
				MhAssert.True(textEdit.Selection == first+sep);
				MhAssert.True(textEdit.Text == startText);
				// this is to prove that we don't loose the selection when pressing Control
				SendKeyDown(Keys.Control, container);
				MhAssert.True(textEdit.Selection == first + sep);
				MhAssert.True(textEdit.Text == startText);
				// this is to prove that we don't loose the selection when pressing Shift
				SendKeyDown(Keys.Shift, container);
				MhAssert.True(textEdit.Text == startText);
				MhAssert.True(textEdit.Selection == first + sep);
				SendKeyDown(Keys.Right, container);
				MhAssert.True(textEdit.Selection == "");
				SendKey(Keys.Shift | Keys.Control | Keys.Left, ' ', container);
				MhAssert.True(textEdit.Selection == first + sep);
				SendKey(Keys.Delete, ' ', container);
				MhAssert.True(textEdit.Text == $"{second}{sep}{third}");
				SendKey(Keys.Shift | Keys.Control | Keys.Right, ' ', container);
				MhAssert.True(textEdit.Selection == $"{second}{sep}");

				// if this fails add
				// GuiHalWidget.SetClipboardFunctions(System.Windows.Forms.Clipboard.GetText, System.Windows.Forms.Clipboard.SetText, System.Windows.Forms.Clipboard.ContainsText);
				// before you call the unit tests
				Clipboard.SetSystemClipboard(new SimulatedClipboard());

				SendKey(Keys.Control | Keys.C, 'c', container);
				MhAssert.True(textEdit.Selection == $"{second}{sep}");
				MhAssert.True(textEdit.Text == $"{second}{sep}{third}");
				SendKeyDown(Keys.Right, container); // move to the right
				SendKey(Keys.Control | Keys.V, 'v', container);
				MhAssert.True(textEdit.Text == $"{second}{sep}{second}{sep}{third}");
            
				Clipboard.SetSystemClipboard(new WindowsFormsClipboard());
            }

            void CheckChar(string sep)
			{
				textEdit.Text = $"some{sep}starting{sep}text";
				// spaces work as expected
				RunWithSpecificChar(sep, "some", "starting", "text");
				textEdit.Text = $"123{sep}is{sep}number";
				RunWithSpecificChar(sep, "123", "is", "number");
				textEdit.Text = $"123_1{sep}456_2{sep}789_3";
				RunWithSpecificChar(sep, "123_1", "456_2", "789_3");
			}

			CheckChar(" ");

			container.Close();
		}

        [MhTest]
        public void ScrollingToEndShowsEnd()
		{
			var container = new GuiWidget
			{
				DoubleBuffer = true,
				LocalBounds = new RectangleDouble(0, 0, 110, 30)
			};
			var editField1 = new TextEditWidget("This is a nice long text string", 0, 0, pixelWidth: 100);
			container.AddChild(editField1);

			var firstWordText = new TextWidget("This");
			RectangleDouble bounds = firstWordText.LocalBounds;
			bounds.Offset(bounds.Left, bounds.Bottom);
			firstWordText.LocalBounds = bounds;

			firstWordText.BackBuffer.NewGraphics2D().Clear(Color.White);
			firstWordText.OnDraw(firstWordText.BackBuffer.NewGraphics2D());
			var lastWordText = new TextWidget("string");

			bounds = lastWordText.LocalBounds;
			bounds.Offset(bounds.Left, bounds.Bottom);
			lastWordText.LocalBounds = bounds;

			lastWordText.BackBuffer.NewGraphics2D().Clear(Color.White);
			lastWordText.OnDraw(lastWordText.BackBuffer.NewGraphics2D());
			container.BackBuffer.NewGraphics2D().Clear(Color.White);
			container.BackgroundColor = Color.White;

			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 1, 1, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 0, 1, 1, 0));
			MhAssert.True(editField1.ContainsFocus == true);

			container.OnDraw(container.BackBuffer.NewGraphics2D());
			OutputImage(firstWordText.BackBuffer, "Control - Left.tga");
			OutputImage(lastWordText.BackBuffer, "Control - Right.tga");
			OutputImage(container.BackBuffer, "Test - Start.tga");
			container.BackBuffer.FindLeastSquaresMatch(firstWordText.BackBuffer, out _, out double bestLeastSquares);
			MhAssert.True(bestLeastSquares < 2000000);
			container.BackBuffer.FindLeastSquaresMatch(lastWordText.BackBuffer, out _, out bestLeastSquares);
			MhAssert.True(bestLeastSquares > 2000000);

			SendKeyDown(Keys.End, container);

			container.OnDraw(container.BackBuffer.NewGraphics2D());
			OutputImage(container.BackBuffer, "Test - Scrolled.tga");

			container.BackBuffer.FindLeastSquaresMatch(firstWordText.BackBuffer, out _, out bestLeastSquares);
			MhAssert.True(bestLeastSquares > 2000000);
			container.BackBuffer.FindLeastSquaresMatch(lastWordText.BackBuffer, out _, out bestLeastSquares);
			MhAssert.True(bestLeastSquares < 2000000);

			container.Close();
		}
	}

    [MhTestFixture("Opens Winforms Window")]
    public class TextEditFocusTests
	{
        [MhTest]
        public async Task VerifyFocusMakesTextWidgetEditable()
		{
			TextEditWidget editField = null;
			var systemWindow = new SystemWindow(300, 200)
			{
				BackgroundColor = Color.Black,
			};

			Task TestToRun(AutomationRunner testRunner)
			{
				editField.Focus();

				testRunner.Delay();
				testRunner.Type("Test Text");

				testRunner.Delay(1);
				MhAssert.True(editField.Text == "Test Text", "validate text is typed");

				return Task.CompletedTask;
			}

			editField = new TextEditWidget(pixelWidth: 200)
			{
				HAnchor = HAnchor.Center,
				VAnchor = VAnchor.Center,
			};
			systemWindow.AddChild(editField);

			await AutomationRunner.ShowWindowAndExecuteTests(systemWindow, TestToRun);
		}

        [MhTest]
        public async Task VerifyFocusProperty()
		{
			var systemWindow = new SystemWindow(300, 200)
			{
				BackgroundColor = Color.Black,
			};

			var editField = new TextEditWidget(pixelWidth: 200)
			{
				HAnchor = HAnchor.Center,
				VAnchor = VAnchor.Center,
			};
			systemWindow.AddChild(editField);

			Task TestToRun(AutomationRunner testRunner)
			{
				UiThread.RunOnIdle(editField.Focus);
				testRunner.WaitFor(() => editField.ContainsFocus);
				//if (!editField.ContainsFocus) { System.Diagnostics.Debugger.Launch(); System.Diagnostics.Debugger.Break(); }
				// NOTE: Okay. During parallel testing, it seems that the avalanche of windows causes test UIs to lose control focus and get confused.
				MhAssert.True(editField.ContainsFocus, "Focused property should be true after invoking Focus method");

				return Task.CompletedTask;
			}

			await AutomationRunner.ShowWindowAndExecuteTests(systemWindow, TestToRun);
		}

        [MhTest]
        public async Task SelectAllOnFocusCanStillClickAfterSelection()
		{
			var editField = new TextEditWidget(pixelWidth: 200)
			{
				Name = "editField",
				Text = "Some Text",
				HAnchor = HAnchor.Center,
				VAnchor = VAnchor.Center,
			};

			var systemWindow = new SystemWindow(300, 200)
			{
				BackgroundColor = Color.Gray,
			};
			systemWindow.AddChild(editField);

			Task TestToRun(AutomationRunner testRunner)
			{
				editField.SelectAllOnFocus = true;
				testRunner.Delay(1);
				testRunner.ClickByName(editField.Name);

				editField.SelectAllOnFocus = true;
				testRunner.Type("123");
				MhAssert.Equal("123", editField.Text);//, "Text input on newly focused control should replace selection");

                testRunner.ClickByName(editField.Name);
				//testRunner.WaitFor(() => editField.ContainsFocus);

				testRunner.Type("123");
				//testRunner.WaitFor(() => "123123" == editField.Text, maxSeconds: 60);
				// NOTE: Used to get intermittent failures here. These issues might have been sorted out now.
				MhAssert.Equal("123123", editField.Text);//, "Text should be appended if control is focused and has already received input");

                return Task.CompletedTask;
			}

			await AutomationRunner.ShowWindowAndExecuteTests(systemWindow, TestToRun);
		}
	}
}
