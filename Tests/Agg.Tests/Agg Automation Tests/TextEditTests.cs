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
using TUnit.Assertions;
using TUnit.Core;
using MatterHackers.Agg.UI.Tests;

namespace MatterHackers.Agg.UI.Tests
{
    [NotInParallel(nameof(AutomationRunner.ShowWindowAndExecuteTests))] // Ensure tests in this class do not run in parallel
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

        [Test]
        public async Task CorectLineCounts()
		{
			var lines7 = @"; activate T0
; move up a bit
G91 
G1 Z1 F1500
G90
; do the switch to T0
G1 X-29.5 F6000 ; NO_PROCESSING";
			var printer7 = new TypeFacePrinter(lines7);
			await Assert.That(printer7.NumLines()).IsEqualTo(7);

			var lines8 = @"; activate T0
; move up a bit
G91 
G1 Z1 F1500
G90
; do the switch to T0
G1 X-29.5 F6000 ; NO_PROCESSING
";
			var printer8 = new TypeFacePrinter(lines8);
			await Assert.That(printer8.NumLines()).IsEqualTo(8);
		}

        [Test]
        public async Task TextEditTextSelectionTests()
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
			await Assert.That(editField1.Text == "a").IsTrue();

			// select the beginning again and type something else in it
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 0, 0, 0, 0));
			SendKey(Keys.B, 'b', container);
			await Assert.That(editField1.Text == "ba").IsTrue();

			// select the ba and delete them
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0));
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 0, 15, 0, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 0, 15, 0, 0));
			SendKey(Keys.Back, ' ', container);
			await Assert.That(editField1.Text == "").IsTrue();

			// select the other way
			editField1.Text = "ab";
			await Assert.That(editField1.Text == "ab").IsTrue();
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 15, 0, 0));
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 0, 0, 0, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 0, 0, 0, 0));
			SendKey(Keys.Back, ' ', container);
			await Assert.That(editField1.Text == "").IsTrue();

			// select the other way but start far to the right
			editField1.Text = "abc";
			await Assert.That(editField1.Text == "abc").IsTrue();
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 30, 0, 0));
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 0, 0, 0, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 0, 0, 0, 0));
			SendKey(Keys.Back, ' ', container);
			await Assert.That(editField1.Text == "").IsTrue();

			// double click empty does nothing
			// select the other way but start far to the right
			editField1.Text = "";
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 1, 0, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 0, 0, 0, 0));
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 2, 1, 0, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 0, 0, 0, 0));
			await Assert.That(editField1.Selection).IsEqualTo("");//, "First word selected");

            // double click first word selects
            editField1.Text = "abc 123";
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 1, 0, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 0, 0, 0, 0));
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 2, 1, 0, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 0, 0, 0, 0));
			await Assert.That(editField1.Selection).IsEqualTo("abc");//, "First word selected");

            // double click last word selects
            editField1.Text = "abc 123";
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 30, 0, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 0, 0, 0, 0));
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 2, 30, 0, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 0, 0, 0, 0));
			await Assert.That(editField1.Selection).IsEqualTo("123");//, "Second word selected");

            container.Close();
		}

        [Test]
        public async Task TextSelectionWithShiftClick()
		{
			const string fullText = "This is a text";

			var container = new GuiWidget(200, 200);
			var editField1 = new TextEditWidget(fullText, pixelWidth: 100);
			container.AddChild(editField1);

			// select all from left to right with shift click
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 1, 0, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 0, 1, 0, 0));
			await Assert.That(editField1.CharIndexToInsertBefore).IsEqualTo(0);
			await Assert.That(editField1.Selection).IsEqualTo("");
			Keyboard.SetKeyDownState(Keys.Shift, true);
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 100, 0, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 0, 100, 0, 0));
			Keyboard.SetKeyDownState(Keys.Shift, false);
			await Assert.That(editField1.CharIndexToInsertBefore).IsEqualTo(fullText.Length);
			await Assert.That(editField1.Selection).IsEqualTo(fullText);//, "It should select full text");

            // select all from right to left with shift click
            container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 100, 0, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 0, 100, 0, 0));
			await Assert.That(editField1.CharIndexToInsertBefore).IsEqualTo(fullText.Length);
			await Assert.That(editField1.Selection).IsEqualTo("");
			Keyboard.SetKeyDownState(Keys.Shift, true);
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 1, 0, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 0, 1, 0, 0));
			Keyboard.SetKeyDownState(Keys.Shift, false);
			await Assert.That(editField1.CharIndexToInsertBefore).IsEqualTo(0);
			await Assert.That(editField1.Selection).IsEqualTo(fullText);//, "It should select full text");

            // select parts of the text with shift click
            container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 1, 0, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 0, 1, 0, 0));
			SendKey(Keys.Control | Keys.Right, ' ', container);
			SendKey(Keys.Control | Keys.Right, ' ', container);
			await Assert.That(editField1.CharIndexToInsertBefore).IsEqualTo("This is ".Length);
			await Assert.That(editField1.Selection).IsEqualTo("");
			Keyboard.SetKeyDownState(Keys.Shift, true);
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 100, 0, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 0, 100, 0, 0));
			Keyboard.SetKeyDownState(Keys.Shift, false);
			await Assert.That(editField1.CharIndexToInsertBefore).IsEqualTo(fullText.Length);
			await Assert.That(editField1.Selection).IsEqualTo("a text");//, "It should select second part of the text");
            Keyboard.SetKeyDownState(Keys.Shift, true);
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 1, 0, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 0, 1, 0, 0));
			Keyboard.SetKeyDownState(Keys.Shift, false);
			await Assert.That(editField1.CharIndexToInsertBefore).IsEqualTo(0);
			await Assert.That(editField1.Selection).IsEqualTo("This is ");//, "It should select first part of the text");

            container.Close();
		}

        [Test]
        public async Task TextChangedEventsTests()
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
			await Assert.That(editField1.BoundsRelativeToParent.Top < 40).IsTrue();
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
			await Assert.That(textField1GotFocus).IsTrue();
			await Assert.That(textField1EditComplete).IsFalse();
			SendKey(Keys.B, 'b', container);
			await Assert.That(editField1.Text == "b").IsTrue();
			await Assert.That(textField1EditComplete).IsFalse();
			SendKey(Keys.Enter, '\n', container);
			await Assert.That(textField1EditComplete).IsTrue();
			textField1EditComplete = false;
			SendKey(Keys.A, 'a', container);
			await Assert.That(editField1.Text == "ba").IsTrue();
			await Assert.That(textField1EditComplete).IsFalse();

			await Assert.That(textField1LostFocus).IsFalse();
			textField1GotFocus = false;
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 1, 41, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 0, 1, 1, 0));
			SendKey(Keys.E, 'e', container);
			await Assert.That(textField1LostFocus).IsTrue();
			await Assert.That(textField1EditComplete).IsTrue();
			await Assert.That(editField1.Text == "ba").IsTrue();
			await Assert.That(editField2.Text == "e").IsTrue();

			textField1EditComplete = false;
			textField1LostFocus = false;
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 1, 1, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 0, 1, 1, 0));
			await Assert.That(textField1LostFocus).IsFalse();
			await Assert.That(textField1EditComplete).IsFalse();
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 1, 41, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 0, 1, 1, 0));
			await Assert.That(textField1LostFocus).IsTrue();
			await Assert.That(textField1EditComplete).IsFalse();

			container.Close();
		}

        [Test]
        public async Task TextEditGetsFocusTests()
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
			await Assert.That(editField1.Text == "").IsTrue();
			SendKey(Keys.D, 'a', container);
			await Assert.That(editField1.Text == "").IsTrue();
			await Assert.That(editField2.Text == "").IsTrue();

			// select edit field 1
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 1, 1, 0)); // we move into the widget to make sure we have separate focus and enter events.
			await Assert.That(editField1.ContainsFocus == false).IsTrue();
			await Assert.That(editField1.Focused == false).IsTrue();
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 1, 1, 0));
			await Assert.That(editField1.ContainsFocus == true).IsTrue();
			await Assert.That(editField1.Focused == false).IsTrue();
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 1, 1, 0));
			await Assert.That(editField1.ContainsFocus == true).IsTrue();
			await Assert.That(editField1.Focused == false).IsTrue();
			SendKey(Keys.B, 'b', container);
			await Assert.That(editField1.Text == "b").IsTrue();
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 150, 1, 0));
			await Assert.That(editField1.ContainsFocus == true).IsTrue();
			await Assert.That(editField1.Focused == false).IsTrue();
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 150, 1, 0));
			await Assert.That(editField1.ContainsFocus == true).IsTrue();
			await Assert.That(editField1.Focused == false).IsTrue();
			SendKey(Keys.D, 'c', container);
			await Assert.That(editField1.Text == "bc").IsTrue();

			// select edit field 2
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 1, 21, 0));
			await Assert.That(editField2.ContainsFocus == true).IsTrue();
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 1, 21, 0));
			SendKey(Keys.D, 'd', container);
			await Assert.That(editField1.Text == "bc").IsTrue();
			await Assert.That(editField2.Text == "d").IsTrue();

			container.Close();
		}

        [Test]
        public async Task AddThenDeleteCausesNoVisualChange()
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
			await Assert.That(editField1.ContainsFocus == true).IsTrue();
			SendKey(Keys.B, 'b', container);
			await Assert.That(editField1.Text == "bTest").IsTrue();
			RectangleDouble afterBLocalBounds = editField1.LocalBounds;
			await Assert.That(beforeLocalBounds.Bottom == afterBLocalBounds.Bottom && beforeLocalBounds.Top == afterBLocalBounds.Top).IsTrue();

			SendKey(Keys.Back, ' ', container);
			await Assert.That(editField1.Text == "Test").IsTrue();

			RectangleDouble afterLocalBounds = editField1.LocalBounds;
			Vector2 afterOrigin = editField1.OriginRelativeParent;

			await Assert.That(beforeLocalBounds == afterLocalBounds).IsTrue();
			await Assert.That(beforeOrigin == afterOrigin).IsTrue();

			// click off it so the cursor is not in it.
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 1, 1, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 1, 1, 0));
			await Assert.That(editField1.Focused == false).IsTrue();

			container.BackBuffer.NewGraphics2D().Clear(Color.White);
			container.OnDraw(container.BackBuffer.NewGraphics2D());
			OutputImage(container.BackBuffer, "z text edited.tga");

			await Assert.That(container.BackBuffer == beforeEditImage).IsTrue();
		}

        [Test]
        public async Task MultiLineTests()
		{
            Clipboard.SetSystemClipboard(new SimulatedClipboard());
            
			// make sure selection ranges are always working
            {
                //Clipboard.SetSystemClipboard(new SimulatedClipboard());

                var singleLine = new InternalTextEditWidget("test", 12, false, 0);

                async Task TestRange(int start, int end, string expected)
				{
					singleLine.CharIndexToInsertBefore = start;
					singleLine.SelectionIndexToStartBefore = end;
					singleLine.Selecting = true;
					await Assert.That(singleLine.Selection).IsEqualTo(expected);
					singleLine.CopySelection();

					await Assert.That(Clipboard.Instance.GetText()).IsEqualTo(expected);
				}

				// ask for some selections
				await TestRange(-10, -8, "");
				await TestRange(-8, -10, "");
				await TestRange(18, 10, "");
				await TestRange(10, 18, "");
				await TestRange(2, -10, "te");
				await TestRange(-10, 2, "te");
				await TestRange(18, 2, "st");
				await TestRange(3, 22, "t");
			}

			{
				var singleLine = new InternalTextEditWidget("test", 12, false, 0);
				var multiLine = new InternalTextEditWidget("test\ntest\ntest", 12, true, 0);
				await Assert.That(multiLine.Height >= singleLine.Height * 3).IsTrue();
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
				await Assert.That(multiLine.ContainsFocus == true).IsTrue();
				await Assert.That(multiLine.SelectionIndexToStartBefore == 4).IsTrue();
				await Assert.That(multiLine.Text == "\n\n\n\n").IsTrue();
				SendKey(Keys.A, 'a', container);
				await Assert.That(multiLine.Text == "\n\n\n\na").IsTrue();
				SendKey(Keys.Up, ' ', container);
				SendKey(Keys.A, 'a', container);
				await Assert.That(multiLine.Text == "\n\n\na\na").IsTrue();

				container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 1, multiLine.Height - 1, 0));
				container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 1, multiLine.Height - 1, 0));
				await Assert.That(multiLine.ContainsFocus == true).IsTrue();
				await Assert.That(multiLine.SelectionIndexToStartBefore == 0).IsTrue();
				await Assert.That(multiLine.Text == "\n\n\na\na").IsTrue();
				SendKey(Keys.A, 'a', container);
				await Assert.That(multiLine.Text == "a\n\n\na\na").IsTrue();
				SendKey(Keys.Down, ' ', container);
				SendKey(Keys.A | Keys.Shift, 'A', container);
				await Assert.That(multiLine.Text == "a\nA\n\na\na").IsTrue();

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
				await Assert.That(multiLine.ContainsFocus == true).IsTrue();
				await Assert.That(multiLine.InsertBarPosition.Y == -32).IsTrue();
				SendKey(Keys.Home, ' ', container);
				await Assert.That(multiLine.InsertBarPosition.Y == -32).IsTrue();
				await Assert.That(multiLine.Text == "line1\nline2\nline3").IsTrue();
				SendKey(Keys.A, 'a', container);
				await Assert.That(multiLine.Text == "line1\nline2\naline3").IsTrue();
				SendKey(Keys.Back, ' ', container);
				await Assert.That(multiLine.Text == "line1\nline2\nline3").IsTrue();
				await Assert.That(multiLine.InsertBarPosition.Y == -32).IsTrue();
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
				await Assert.That(multiLine.ContainsFocus == true).IsTrue();
				await Assert.That(multiLine.CharIndexToInsertBefore == 0).IsTrue();
				await Assert.That(multiLine.InsertBarPosition.X == 0).IsTrue();
				SendKey(Keys.Home, ' ', container);
				await Assert.That(multiLine.CharIndexToInsertBefore == 0).IsTrue();
				await Assert.That(multiLine.InsertBarPosition.X == 0).IsTrue();
				SendKey(Keys.Right, ' ', container);
				await Assert.That(multiLine.CharIndexToInsertBefore == 1).IsTrue();
				double leftOne = multiLine.InsertBarPosition.X;
				SendKey(Keys.Right, ' ', container);
				await Assert.That(multiLine.CharIndexToInsertBefore == 2).IsTrue();
				await Assert.That(multiLine.InsertBarPosition.X == leftOne * 2).IsTrue();
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
				await Assert.That(multiLine.LocalBounds.Height == 16 * 5).IsTrue();
				container.AddChild(multiLine);

				container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 1, multiLine.Height - 1, 0));
				container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 1, multiLine.Height - 1, 0));

				await Assert.That(multiLine.CharIndexToInsertBefore == 0).IsTrue();
				await Assert.That(multiLine.InsertBarPosition.Y == 0).IsTrue();

				// move past \n
				SendKey(Keys.Right, ' ', container);
				await Assert.That(multiLine.CharIndexToInsertBefore == 1).IsTrue();
				await Assert.That(multiLine.InsertBarPosition.Y == -16).IsTrue();

				// move past 1
				SendKey(Keys.Right, ' ', container);
				await Assert.That(multiLine.CharIndexToInsertBefore == 2).IsTrue();
				await Assert.That(multiLine.InsertBarPosition.Y == -16).IsTrue();

				// move past \n
				SendKey(Keys.Right, ' ', container);
				await Assert.That(multiLine.CharIndexToInsertBefore == 3).IsTrue();
				await Assert.That(multiLine.InsertBarPosition.Y == -32).IsTrue();

				// move past \n
				SendKey(Keys.Right, ' ', container);
				await Assert.That(multiLine.CharIndexToInsertBefore == 4).IsTrue();
				await Assert.That(multiLine.InsertBarPosition.Y == -48).IsTrue();

				// move past 3
				SendKey(Keys.Right, ' ', container);
				await Assert.That(multiLine.CharIndexToInsertBefore == 5).IsTrue();
				await Assert.That(multiLine.InsertBarPosition.Y == -48).IsTrue();

				// move past \n
				SendKey(Keys.Right, ' ', container);
				await Assert.That(multiLine.CharIndexToInsertBefore == 6).IsTrue();
				await Assert.That(multiLine.InsertBarPosition.Y == -64).IsTrue();
				container.Close();
			}
		}

        [Test]
        public async Task NumEditHandlesNonNumberChars()
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

			await Assert.That(numberEdit.CharIndexToInsertBefore == 0).IsTrue();
			await Assert.That(numberEdit.TopLeftOffset.Y == 0).IsTrue();

			// type a . (non numeric character)
			SendKey(Keys.Back, ' ', container);
			SendKey(Keys.Delete, ' ', container);
			SendKey(Keys.OemMinus, '-', container);
			await Assert.That(numberEdit.Value == 0).IsTrue();
			SendKey(Keys.OemPeriod, '.', container);
			await Assert.That(numberEdit.Value == 0).IsTrue();
			SendKey(Keys.D0, '.', container);
			await Assert.That(numberEdit.Value == 0).IsTrue();
			SendKey(Keys.A, 'A', container);
			await Assert.That(numberEdit.Value == 0).IsTrue();

			container.Close();
		}

#if __ANDROID__
		[Test]
#else
        [Test]
#endif
        public async Task TextEditingSpecialKeysWork()
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

			await Assert.That(textEdit.CharIndexToInsertBefore == 0).IsTrue();
			await Assert.That(textEdit.TopLeftOffset.Y == 0).IsTrue();

			// test that we move to the next character correctly
			await Assert.That(InternalTextEditWidget.IndexOfNextToken("235 12/6", 0)).IsEqualTo(4);
			await Assert.That(InternalTextEditWidget.IndexOfNextToken("235   12/6", 0)).IsEqualTo(6);
			await Assert.That(InternalTextEditWidget.IndexOfNextToken("235\n   12/6", 0)).IsEqualTo(3);
			await Assert.That(InternalTextEditWidget.IndexOfNextToken("235\n   12/6", 3)).IsEqualTo(7);
			await Assert.That(InternalTextEditWidget.IndexOfNextToken("235\n\n   12/6", 3)).IsEqualTo(4);
			await Assert.That(InternalTextEditWidget.IndexOfNextToken("235\n\n   12/6", 4)).IsEqualTo(8);
			await Assert.That(InternalTextEditWidget.IndexOfNextToken("123+ 235   12/6", 0)).IsEqualTo(3);
			await Assert.That(InternalTextEditWidget.IndexOfNextToken("235+12/6", 0)).IsEqualTo(3);
			await Assert.That(InternalTextEditWidget.IndexOfNextToken("+++++235   12/6", 0)).IsEqualTo(5);
			await Assert.That(InternalTextEditWidget.IndexOfNextToken("+++++235   12/6", 0)).IsEqualTo(5);

			// test that we move to the previous character correctly
			await Assert.That(InternalTextEditWidget.IndexOfPreviousToken("=35+12/6", 8)).IsEqualTo(7);
			await Assert.That(InternalTextEditWidget.IndexOfPreviousToken("35556+68384734", 10)).IsEqualTo(6);
			await Assert.That(InternalTextEditWidget.IndexOfPreviousToken("35556+68384734", 6)).IsEqualTo(5);
			await Assert.That(InternalTextEditWidget.IndexOfPreviousToken("35556+68384734", 5)).IsEqualTo(0);
			
			await Assert.That(InternalTextEditWidget.IndexOfPreviousToken("235\n\n   12/6", 12)).IsEqualTo(11);
			await Assert.That(InternalTextEditWidget.IndexOfPreviousToken("235\n\n   12/6", 11)).IsEqualTo(10);
			await Assert.That(InternalTextEditWidget.IndexOfPreviousToken("235\n\n   12/6", 10)).IsEqualTo(8);
			await Assert.That(InternalTextEditWidget.IndexOfPreviousToken("235\n\n   12/6", 8)).IsEqualTo(5);
			await Assert.That(InternalTextEditWidget.IndexOfPreviousToken("235\n\n   12/6", 5)).IsEqualTo(4);
			await Assert.That(InternalTextEditWidget.IndexOfPreviousToken("235\n\n   12/6", 4)).IsEqualTo(0);
			await Assert.That(InternalTextEditWidget.IndexOfPreviousToken("some starting text", 5)).IsEqualTo(0);

			async Task RunWithSpecificChar(string sep, string first, string second, string third)
			{
				var startText = $"{first}{sep}{second}{sep}{third}";
				await Assert.That(textEdit.Text == startText).IsTrue();
				// this is to select some text
				SendKey(Keys.Shift | Keys.Control | Keys.Right, ' ', container);
				await Assert.That(textEdit.Selection == first+sep).IsTrue();
				await Assert.That(textEdit.Text == startText).IsTrue();
				// this is to prove that we don't loose the selection when pressing Control
				SendKeyDown(Keys.Control, container);
				await Assert.That(textEdit.Selection == first + sep).IsTrue();
				await Assert.That(textEdit.Text == startText).IsTrue();
				// this is to prove that we don't loose the selection when pressing Shift
				SendKeyDown(Keys.Shift, container);
				await Assert.That(textEdit.Text == startText).IsTrue();
				await Assert.That(textEdit.Selection == first + sep).IsTrue();
				SendKeyDown(Keys.Right, container);
				await Assert.That(textEdit.Selection == "").IsTrue();
				SendKey(Keys.Shift | Keys.Control | Keys.Left, ' ', container);
				await Assert.That(textEdit.Selection == first + sep).IsTrue();
				SendKey(Keys.Delete, ' ', container);
				await Assert.That(textEdit.Text == $"{second}{sep}{third}").IsTrue();
				SendKey(Keys.Shift | Keys.Control | Keys.Right, ' ', container);
				await Assert.That(textEdit.Selection == $"{second}{sep}").IsTrue();

				// if this fails add
				// GuiHalWidget.SetClipboardFunctions(System.Windows.Forms.Clipboard.GetText, System.Windows.Forms.Clipboard.SetText, System.Windows.Forms.Clipboard.ContainsText);
				// before you call the unit tests
				Clipboard.SetSystemClipboard(new SimulatedClipboard());

				SendKey(Keys.Control | Keys.C, 'c', container);
				await Assert.That(textEdit.Selection == $"{second}{sep}").IsTrue();
				await Assert.That(textEdit.Text == $"{second}{sep}{third}").IsTrue();
				SendKeyDown(Keys.Right, container); // move to the right
				SendKey(Keys.Control | Keys.V, 'v', container);
				await Assert.That(textEdit.Text == $"{second}{sep}{second}{sep}{third}").IsTrue();
            
				Clipboard.SetSystemClipboard(new WindowsFormsClipboard());
            }

            async Task CheckChar(string sep)
			{
				textEdit.Text = $"some{sep}starting{sep}text";
				// spaces work as expected
				await RunWithSpecificChar(sep, "some", "starting", "text");
				textEdit.Text = $"123{sep}is{sep}number";
				await RunWithSpecificChar(sep, "123", "is", "number");
				textEdit.Text = $"123_1{sep}456_2{sep}789_3";
				await RunWithSpecificChar(sep, "123_1", "456_2", "789_3");
			}

			await CheckChar(" ");

			container.Close();
		}

        [Test]
        public async Task ScrollingToEndShowsEnd()
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
			await Assert.That(editField1.ContainsFocus == true).IsTrue();

			container.OnDraw(container.BackBuffer.NewGraphics2D());
			OutputImage(firstWordText.BackBuffer, "Control - Left.tga");
			OutputImage(lastWordText.BackBuffer, "Control - Right.tga");
			OutputImage(container.BackBuffer, "Test - Start.tga");
			container.BackBuffer.FindLeastSquaresMatch(firstWordText.BackBuffer, out _, out double bestLeastSquares);
			await Assert.That(bestLeastSquares < 2000000).IsTrue();
			container.BackBuffer.FindLeastSquaresMatch(lastWordText.BackBuffer, out _, out bestLeastSquares);
			await Assert.That(bestLeastSquares > 2000000).IsTrue();

			SendKeyDown(Keys.End, container);

			container.OnDraw(container.BackBuffer.NewGraphics2D());
			OutputImage(container.BackBuffer, "Test - Scrolled.tga");

			container.BackBuffer.FindLeastSquaresMatch(firstWordText.BackBuffer, out _, out bestLeastSquares);
			await Assert.That(bestLeastSquares > 2000000).IsTrue();
			container.BackBuffer.FindLeastSquaresMatch(lastWordText.BackBuffer, out _, out bestLeastSquares);
			await Assert.That(bestLeastSquares < 2000000).IsTrue();

			container.Close();
		}
	}

    
    [NotInParallel(nameof(AutomationRunner.ShowWindowAndExecuteTests))] // Ensure tests in this class do not run in parallel
    public class TextEditFocusTests
	{
        [Test]
        public async Task VerifyFocusMakesTextWidgetEditable()
		{
			TextEditWidget editField = null;
			var systemWindow = new SystemWindow(300, 200)
			{
				BackgroundColor = Color.Black,
			};

			async Task TestToRun(AutomationRunner testRunner)
			{
				editField.Focus();

				testRunner.Delay();
				testRunner.Type("Test Text");

				testRunner.Delay(1);
				await Assert.That(editField.Text == "Test Text").IsTrue();
			}

			editField = new TextEditWidget(pixelWidth: 200)
			{
				HAnchor = HAnchor.Center,
				VAnchor = VAnchor.Center,
			};
			systemWindow.AddChild(editField);

			await AutomationRunner.ShowWindowAndExecuteTests(systemWindow, TestToRun);
		}

        [Test]
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

			async Task TestToRun(AutomationRunner testRunner)
			{
				UiThread.RunOnIdle(editField.Focus);
				testRunner.WaitFor(() => editField.ContainsFocus);
				//if (!editField.ContainsFocus) { System.Diagnostics.Debugger.Launch(); System.Diagnostics.Debugger.Break(); }
				// NOTE: Okay. During parallel testing, it seems that the avalanche of windows causes test UIs to lose control focus and get confused.
				await Assert.That(editField.ContainsFocus).IsTrue();
			}

			await AutomationRunner.ShowWindowAndExecuteTests(systemWindow, TestToRun);
		}

        [Test]
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

			async Task TestToRun(AutomationRunner testRunner)
			{
				editField.SelectAllOnFocus = true;
				testRunner.Delay(1);
				testRunner.ClickByName(editField.Name);

				editField.SelectAllOnFocus = true;
				testRunner.Type("123");
				await Assert.That(editField.Text).IsEqualTo("123");//, "Text input on newly focused control should replace selection");

                testRunner.ClickByName(editField.Name);
				//testRunner.WaitFor(() => editField.ContainsFocus);

				testRunner.Type("123");
				//testRunner.WaitFor(() => "123123" == editField.Text, maxSeconds: 60);
				// NOTE: Used to get intermittent failures here. These issues might have been sorted out now.
				await Assert.That(editField.Text).IsEqualTo("123123");//, "Text should be appended if control is focused and has already received input");
			}

			await AutomationRunner.ShowWindowAndExecuteTests(systemWindow, TestToRun);
		}
	}
}
