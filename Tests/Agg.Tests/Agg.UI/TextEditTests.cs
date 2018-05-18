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

using MatterHackers.Agg.Image;
#if !__ANDROID__
using MatterHackers.GuiAutomation;
#endif
using MatterHackers.VectorMath;
using NUnit.Framework;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Threading;
using System.IO;

namespace MatterHackers.Agg.UI.Tests
{
#if !__ANDROID__
	[TestFixture, Category("Agg.UI"), Apartment(ApartmentState.STA), RunInApplicationDomain]
#endif
	public class TextEditTests
	{
		public static bool saveImagesForDebug = false;

		private void OutputImage(ImageBuffer imageToOutput, string fileName)
		{
			if (saveImagesForDebug)
			{
				var dirAndFileName = Path.Combine("C:/Temp", fileName);
				ImageTgaIO.Save(imageToOutput, dirAndFileName);
			}
		}

		public void SendKeyDown(Keys keyDown, GuiWidget reciever)
		{
			KeyEventArgs keyDownEvent = new KeyEventArgs(keyDown);
			reciever.OnKeyDown(keyDownEvent);
		}

		public void SendKey(Keys keyDown, char keyPressed, GuiWidget reciever)
		{
			KeyEventArgs keyDownEvent = new KeyEventArgs(keyDown);
			reciever.OnKeyDown(keyDownEvent);
			if (!keyDownEvent.SuppressKeyPress)
			{
				KeyPressEventArgs keyPressEvent = new KeyPressEventArgs(keyPressed);
				reciever.OnKeyPress(keyPressEvent);
			}
		}

		[Test]
		public void TextEditTextSelectionTests()
		{
			GuiWidget container = new GuiWidget();
			container.LocalBounds = new RectangleDouble(0, 0, 200, 200);
			TextEditWidget editField1 = new TextEditWidget("", 0, 0, pixelWidth: 51);
			container.AddChild(editField1);

			// select the control and type something in it
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 0, 0, 0, 0));
			SendKey(Keys.A, 'a', container);
			Assert.IsTrue(editField1.Text == "a", "It should have a in it.");

			// select the beginning again and type something else in it
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 0, 0, 0, 0));
			SendKey(Keys.B, 'b', container);
			Assert.IsTrue(editField1.Text == "ba", "It should have ba in it.");

			// select the ba and delete them
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0));
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 0, 15, 0, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 0, 15, 0, 0));
			SendKey(Keys.Back, ' ', container);
			Assert.IsTrue(editField1.Text == "", "It should have nothing in it.");

			// select the other way
			editField1.Text = "ab";
			Assert.IsTrue(editField1.Text == "ab", "It should have ab in it.");
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 15, 0, 0));
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 0, 0, 0, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 0, 0, 0, 0));
			SendKey(Keys.Back, ' ', container);
			Assert.IsTrue(editField1.Text == "", "It should have nothing in it.");

			// select the other way but start far to the right
			editField1.Text = "abc";
			Assert.IsTrue(editField1.Text == "abc", "It should have abc in it.");
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 30, 0, 0));
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 0, 0, 0, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 0, 0, 0, 0));
			SendKey(Keys.Back, ' ', container);
			Assert.IsTrue(editField1.Text == "", "It should have nothing in it.");

			container.Close();
		}

		[Test]
		public void TextChangedEventsTests()
		{
			GuiWidget container = new GuiWidget();
			container.Name = "container";
			container.LocalBounds = new RectangleDouble(0, 0, 200, 200);
			TextEditWidget editField1 = new TextEditWidget("", 0, 0, pixelWidth: 20);
			editField1.Name = "editField1";
			Assert.IsTrue(editField1.BoundsRelativeToParent.Top < 40, "We make this assumption in the code below, so make sure it's true.");
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

			TextEditWidget editField2 = new TextEditWidget("", 0, 40, pixelWidth: 20);
			editField2.Name = "editField2";
			container.AddChild(editField2);

			// mouse select on the control when it contains nothing
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 1, 1, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 0, 1, 1, 0));
			Assert.IsTrue(textField1GotFocus);
			Assert.IsFalse(textField1EditComplete);
			SendKey(Keys.B, 'b', container);
			Assert.IsTrue(editField1.Text == "b");
			Assert.IsFalse(textField1EditComplete, "We do not change with each keystroke.");
			SendKey(Keys.Enter, '\n', container);
			Assert.IsTrue(textField1EditComplete, "Enter must send a EditComplete if changed.");
			textField1EditComplete = false;
			SendKey(Keys.A, 'a', container);
			Assert.IsTrue(editField1.Text == "ba");
			Assert.IsFalse(textField1EditComplete, "We do not change with each keystroke.");

			Assert.IsFalse(textField1LostFocus);
			textField1GotFocus = false;
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 1, 41, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 0, 1, 1, 0));
			SendKey(Keys.E, 'e', container);
			Assert.IsTrue(textField1LostFocus);
			Assert.IsTrue(textField1EditComplete, "Loosing focus should send a text changed.");
			Assert.IsTrue(editField1.Text == "ba");
			Assert.IsTrue(editField2.Text == "e");

			textField1EditComplete = false;
			textField1LostFocus = false;
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 1, 1, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 0, 1, 1, 0));
			Assert.IsFalse(textField1LostFocus);
			Assert.IsFalse(textField1EditComplete);
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 1, 41, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 0, 1, 1, 0));
			Assert.IsTrue(textField1LostFocus);
			Assert.IsFalse(textField1EditComplete, "The text did not change even though we lost focus we should not call textChanged.");

			container.Close();
		}

		[Test]
		public void TextEditGetsFocusTests()
		{
			GuiWidget container = new GuiWidget();
			container.Name = "container";
			container.LocalBounds = new RectangleDouble(0, 0, 200, 200);
			TextEditWidget editField1 = new TextEditWidget("", 0, 0, pixelWidth: 160);
			editField1.Name = "editField1";
			container.AddChild(editField1);

			TextEditWidget editField2 = new TextEditWidget("", 0, 20, pixelWidth: 160);
			editField2.Name = "editField2";
			container.AddChild(editField2);

			// select no edit field
			Assert.IsTrue(editField1.Text == "");
			SendKey(Keys.D, 'a', container);
			Assert.IsTrue(editField1.Text == "");
			Assert.IsTrue(editField2.Text == "");

			// select edit field 1
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 1, 1, 0)); // we move into the widget to make sure we have seprated focus and enter events.
			Assert.IsTrue(editField1.ContainsFocus == false);
			Assert.IsTrue(editField1.Focused == false);
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 1, 1, 0));
			Assert.IsTrue(editField1.ContainsFocus == true);
			Assert.IsTrue(editField1.Focused == false, "The internal text widget must be focused.");
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 1, 1, 0));
			Assert.IsTrue(editField1.ContainsFocus == true);
			Assert.IsTrue(editField1.Focused == false);
			SendKey(Keys.B, 'b', container);
			Assert.IsTrue(editField1.Text == "b", "It should have b a in it.");
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 150, 1, 0));
			Assert.IsTrue(editField1.ContainsFocus == true);
			Assert.IsTrue(editField1.Focused == false, "The internal text widget must be focused.");
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 150, 1, 0));
			Assert.IsTrue(editField1.ContainsFocus == true);
			Assert.IsTrue(editField1.Focused == false);
			SendKey(Keys.D, 'c', container);
			Assert.IsTrue(editField1.Text == "bc", "It should have b a in it.");

			// select edit field 2
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 1, 21, 0));
			Assert.IsTrue(editField2.ContainsFocus == true);
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 1, 21, 0));
			SendKey(Keys.D, 'd', container);
			Assert.IsTrue(editField1.Text == "bc", "It should have a bc in it.");
			Assert.IsTrue(editField2.Text == "d", "It should have d in it.");

			container.Close();
		}

		[Test]
		public void AddThenDeleteCausesNoVisualChange()
		{
			GuiWidget container = new GuiWidget();
			container.DoubleBuffer = true;
			container.LocalBounds = new RectangleDouble(0, 0, 200, 200);
			TextEditWidget editField1 = new TextEditWidget("Test", 10, 10, pixelWidth: 50);
			container.AddChild(editField1);
			container.BackBuffer.NewGraphics2D().Clear(Color.White);
			container.OnDraw(container.BackBuffer.NewGraphics2D());
			ImageBuffer beforeEditImage = new ImageBuffer(container.BackBuffer);
			RectangleDouble beforeLocalBounds = editField1.LocalBounds;
			Vector2 beforeOrigin = editField1.OriginRelativeParent;

			OutputImage(beforeEditImage, "z text un-edited.tga");

			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 10, 10, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 10, 10, 0));
			Assert.IsTrue(editField1.ContainsFocus == true);
			SendKey(Keys.B, 'b', container);
			Assert.IsTrue(editField1.Text == "bTest", "It should have b a in it.");
			RectangleDouble afterBLocalBounds = editField1.LocalBounds;
			Assert.IsTrue(beforeLocalBounds.Bottom == afterBLocalBounds.Bottom && beforeLocalBounds.Top == afterBLocalBounds.Top);

			SendKey(Keys.Back, ' ', container);
			Assert.IsTrue(editField1.Text == "Test", "It should not have b a in it.");

			RectangleDouble afterLocalBounds = editField1.LocalBounds;
			Vector2 afterOrigin = editField1.OriginRelativeParent;

			Assert.IsTrue(beforeLocalBounds == afterLocalBounds);
			Assert.IsTrue(beforeOrigin == afterOrigin);

			// click off it so the cursor is not in it.
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 1, 1, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 1, 1, 0));
			Assert.IsTrue(editField1.Focused == false);

			container.BackBuffer.NewGraphics2D().Clear(Color.White);
			container.OnDraw(container.BackBuffer.NewGraphics2D());
			OutputImage(container.BackBuffer, "z text edited.tga");

			Assert.IsTrue(container.BackBuffer == beforeEditImage);
		}

		[Test]
		public void MiltiLineTests()
		{
			{
				InternalTextEditWidget singleLine = new InternalTextEditWidget("test", 12, false, 0);
				InternalTextEditWidget multiLine = new InternalTextEditWidget("test\ntest\ntest", 12, true, 0);
				Assert.IsTrue(multiLine.Height >= singleLine.Height * 3);
			}

			// we get the typed results we expect
			{
				GuiWidget container = new GuiWidget();
				container.LocalBounds = new RectangleDouble(0, 0, 200, 200);
				InternalTextEditWidget multiLine = new InternalTextEditWidget("\n\n\n\n", 12, true, 0);
				container.AddChild(multiLine);

				container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 1, 1, 0));
				container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 1, 1, 0));
				Assert.IsTrue(multiLine.ContainsFocus == true);
				Assert.IsTrue(multiLine.SelectionIndexToStartBefore == 4);
				Assert.IsTrue(multiLine.Text == "\n\n\n\n");
				SendKey(Keys.A, 'a', container);
				Assert.IsTrue(multiLine.Text == "\n\n\n\na");
				SendKey(Keys.Up, ' ', container);
				SendKey(Keys.A, 'a', container);
				Assert.IsTrue(multiLine.Text == "\n\n\na\na");

				container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 1, multiLine.Height - 1, 0));
				container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 1, multiLine.Height - 1, 0));
				Assert.IsTrue(multiLine.ContainsFocus == true);
				Assert.IsTrue(multiLine.SelectionIndexToStartBefore == 0);
				Assert.IsTrue(multiLine.Text == "\n\n\na\na");
				SendKey(Keys.A, 'a', container);
				Assert.IsTrue(multiLine.Text == "a\n\n\na\na");
				SendKey(Keys.Down, ' ', container);
				SendKey(Keys.A | Keys.Shift, 'A', container);
				Assert.IsTrue(multiLine.Text == "a\nA\n\na\na");

				container.Close();
			}

			// make sure the insert position is correct when homed
			{
				GuiWidget container = new GuiWidget();
				container.LocalBounds = new RectangleDouble(0, 0, 200, 200);
				InternalTextEditWidget multiLine = new InternalTextEditWidget("line1\nline2\nline3", 12, true, 0);
				container.AddChild(multiLine);

				container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 5, 1, 0));
				container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 5, 1, 0));
				Assert.IsTrue(multiLine.ContainsFocus == true);
				Assert.IsTrue(multiLine.InsertBarPosition.Y == -32);
				SendKey(Keys.Home, ' ', container);
				Assert.IsTrue(multiLine.InsertBarPosition.Y == -32);
				Assert.IsTrue(multiLine.Text == "line1\nline2\nline3");
				SendKey(Keys.A, 'a', container);
				Assert.IsTrue(multiLine.Text == "line1\nline2\naline3");
				SendKey(Keys.Back, ' ', container);
				Assert.IsTrue(multiLine.Text == "line1\nline2\nline3");
				Assert.IsTrue(multiLine.InsertBarPosition.Y == -32);
				container.Close();
			}

			// make sure the insert position is correct when move left to end of line
			{
				GuiWidget container = new GuiWidget();
				container.LocalBounds = new RectangleDouble(0, 0, 200, 200);
				InternalTextEditWidget multiLine = new InternalTextEditWidget("xx", 12, true, 0);
				container.AddChild(multiLine);

				container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 1, 1, 0));
				container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 1, 1, 0));
				Assert.IsTrue(multiLine.ContainsFocus == true);
				Assert.IsTrue(multiLine.CharIndexToInsertBefore == 0);
				Assert.IsTrue(multiLine.InsertBarPosition.X == 0);
				SendKey(Keys.Home, ' ', container);
				Assert.IsTrue(multiLine.CharIndexToInsertBefore == 0);
				Assert.IsTrue(multiLine.InsertBarPosition.X == 0);
				SendKey(Keys.Right, ' ', container);
				Assert.IsTrue(multiLine.CharIndexToInsertBefore == 1);
				double leftOne = multiLine.InsertBarPosition.X;
				SendKey(Keys.Right, ' ', container);
				Assert.IsTrue(multiLine.CharIndexToInsertBefore == 2);
				Assert.IsTrue(multiLine.InsertBarPosition.X == leftOne * 2);
				container.Close();
			}

			// make sure the cursor is at the right hight when it is after a \n that is on the first line
			{
				GuiWidget container = new GuiWidget();
				container.DoubleBuffer = true;
				container.LocalBounds = new RectangleDouble(0, 0, 200, 200);
				InternalTextEditWidget multiLine = new InternalTextEditWidget("\n1\n\n3\n", 12, true, 0);
				Assert.IsTrue(multiLine.LocalBounds.Height == 16 * 5);
				container.AddChild(multiLine);

				container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 1, multiLine.Height - 1, 0));
				container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 1, multiLine.Height - 1, 0));

				Assert.IsTrue(multiLine.CharIndexToInsertBefore == 0);
				Assert.IsTrue(multiLine.InsertBarPosition.Y == 0);

				// move past \n
				SendKey(Keys.Right, ' ', container);
				Assert.IsTrue(multiLine.CharIndexToInsertBefore == 1);
				Assert.IsTrue(multiLine.InsertBarPosition.Y == -16);

				// move past 1
				SendKey(Keys.Right, ' ', container);
				Assert.IsTrue(multiLine.CharIndexToInsertBefore == 2);
				Assert.IsTrue(multiLine.InsertBarPosition.Y == -16);

				// move past \n
				SendKey(Keys.Right, ' ', container);
				Assert.IsTrue(multiLine.CharIndexToInsertBefore == 3);
				Assert.IsTrue(multiLine.InsertBarPosition.Y == -32);

				// move past \n
				SendKey(Keys.Right, ' ', container);
				Assert.IsTrue(multiLine.CharIndexToInsertBefore == 4);
				Assert.IsTrue(multiLine.InsertBarPosition.Y == -48);

				// move past 3
				SendKey(Keys.Right, ' ', container);
				Assert.IsTrue(multiLine.CharIndexToInsertBefore == 5);
				Assert.IsTrue(multiLine.InsertBarPosition.Y == -48);

				// move past \n
				SendKey(Keys.Right, ' ', container);
				Assert.IsTrue(multiLine.CharIndexToInsertBefore == 6);
				Assert.IsTrue(multiLine.InsertBarPosition.Y == -64);
				container.Close();
			}
		}

		[Test]
		public void NumEditHandlesNonNumberChars()
		{
			GuiWidget container = new GuiWidget();
			container.DoubleBuffer = true;
			container.LocalBounds = new RectangleDouble(0, 0, 200, 200);
			NumberEdit numberEdit = new NumberEdit(0, 0, 0, 12, 200, 16, true, true);
			container.AddChild(numberEdit);

			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 1, numberEdit.Height - 1, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 1, numberEdit.Height - 1, 0));

			Assert.IsTrue(numberEdit.CharIndexToInsertBefore == 0);
			Assert.IsTrue(numberEdit.TopLeftOffset.Y == 0);

			// type a . (non numeric character)
			SendKey(Keys.Back, ' ', container);
			SendKey(Keys.Delete, ' ', container);
			SendKey(Keys.OemMinus, '-', container);
			Assert.IsTrue(numberEdit.Value == 0);
			SendKey(Keys.OemPeriod, '.', container);
			Assert.IsTrue(numberEdit.Value == 0);
			SendKey(Keys.D0, '.', container);
			Assert.IsTrue(numberEdit.Value == 0);
			SendKey(Keys.A, 'A', container);
			Assert.IsTrue(numberEdit.Value == 0);

			container.Close();
		}

#if (__ANDROID__)
		[Test]
#else
		[Test]
#endif
		public void TextEditingSpecialKeysWork()
		{
			GuiWidget container = new GuiWidget();
			container.DoubleBuffer = true;
			container.LocalBounds = new RectangleDouble(0, 0, 200, 200);
			TextEditWidget textEdit = new TextEditWidget("some starting text");
			container.AddChild(textEdit);

			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 1, textEdit.Height - 1, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 1, textEdit.Height - 1, 0));

			Assert.IsTrue(textEdit.CharIndexToInsertBefore == 0);
			Assert.IsTrue(textEdit.TopLeftOffset.Y == 0);

			Assert.IsTrue(textEdit.Text == "some starting text");
			// this is to select some text
			SendKey(Keys.Shift | Keys.Control | Keys.Right, ' ', container);
			Assert.IsTrue(textEdit.Selection == "some ");
			Assert.IsTrue(textEdit.Text == "some starting text");
			// this is to prove that we don't loose the selection when pressing Control
			SendKeyDown(Keys.Control, container);
			Assert.IsTrue(textEdit.Selection == "some ");
			Assert.IsTrue(textEdit.Text == "some starting text");
			// this is to prove that we don't loose the selection when pressing Shift
			SendKeyDown(Keys.Shift, container);
			Assert.IsTrue(textEdit.Text == "some starting text");
			Assert.IsTrue(textEdit.Selection == "some ");
			SendKeyDown(Keys.Right, container);
			Assert.IsTrue(textEdit.Selection == "");
			SendKey(Keys.Shift | Keys.Control | Keys.Left, ' ', container);
			Assert.IsTrue(textEdit.Selection == "some ");
			SendKey(Keys.Delete, ' ', container);
			Assert.IsTrue(textEdit.Text == "starting text");
			SendKey(Keys.Shift | Keys.Control | Keys.Right, ' ', container);
			Assert.IsTrue(textEdit.Selection == "starting ");

#if (!__ANDROID__)
			// if this fails add
			// GuiHalWidget.SetClipboardFunctions(System.Windows.Forms.Clipboard.GetText, System.Windows.Forms.Clipboard.SetText, System.Windows.Forms.Clipboard.ContainsText);
			// before you call the unit tests
			Clipboard.SetSystemClipboard(new WindowsFormsClipboard());


			SendKey(Keys.Control | Keys.C, 'c', container);
			Assert.IsTrue(textEdit.Selection == "starting ");
			Assert.IsTrue(textEdit.Text == "starting text");
			SendKeyDown(Keys.Right, container); // move to the right
			SendKey(Keys.Control | Keys.V, 'v', container);
			Assert.IsTrue(textEdit.Text == "starting starting text");
#endif

			container.Close();
		}

		[Test]
		public void ScrollingToEndShowsEnd()
		{
			GuiWidget container = new GuiWidget();
			container.DoubleBuffer = true;
			container.LocalBounds = new RectangleDouble(0, 0, 110, 30);
			TextEditWidget editField1 = new TextEditWidget("This is a nice long text string", 0, 0, pixelWidth: 100);
			container.AddChild(editField1);

			TextWidget firstWordText = new TextWidget("This");
			RectangleDouble bounds = firstWordText.LocalBounds;
			bounds.Offset(bounds.Left, bounds.Bottom);
			firstWordText.LocalBounds = bounds;

			firstWordText.BackBuffer.NewGraphics2D().Clear(Color.White);
			firstWordText.OnDraw(firstWordText.BackBuffer.NewGraphics2D());
			TextWidget lastWordText = new TextWidget("string");

			bounds = lastWordText.LocalBounds;
			bounds.Offset(bounds.Left, bounds.Bottom);
			lastWordText.LocalBounds = bounds;

			lastWordText.BackBuffer.NewGraphics2D().Clear(Color.White);
			lastWordText.OnDraw(lastWordText.BackBuffer.NewGraphics2D());
			container.BackBuffer.NewGraphics2D().Clear(Color.White);
			container.BackgroundColor = Color.White;

			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 1, 1, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 0, 1, 1, 0));
			Assert.IsTrue(editField1.ContainsFocus == true);

			container.OnDraw(container.BackBuffer.NewGraphics2D());
			OutputImage(firstWordText.BackBuffer, "Control - Left.tga");
			OutputImage(lastWordText.BackBuffer, "Control - Right.tga");
			OutputImage(container.BackBuffer, "Test - Start.tga");

			Vector2 bestPosition;
			double bestLeastSquares;
			container.BackBuffer.FindLeastSquaresMatch(firstWordText.BackBuffer, out bestPosition, out bestLeastSquares);
			Assert.IsTrue(bestLeastSquares < 2000000);
			container.BackBuffer.FindLeastSquaresMatch(lastWordText.BackBuffer, out bestPosition, out bestLeastSquares);
			Assert.IsTrue(bestLeastSquares > 2000000);

			SendKeyDown(Keys.End, container);

			container.OnDraw(container.BackBuffer.NewGraphics2D());
			OutputImage(container.BackBuffer, "Test - Scrolled.tga");

			container.BackBuffer.FindLeastSquaresMatch(firstWordText.BackBuffer, out bestPosition, out bestLeastSquares);
			Assert.IsTrue(bestLeastSquares > 2000000);
			container.BackBuffer.FindLeastSquaresMatch(lastWordText.BackBuffer, out bestPosition, out bestLeastSquares);
			Assert.IsTrue(bestLeastSquares < 2000000);

			container.Close();
		}
	}

#if !__ANDROID__
	[TestFixture, Category("Agg.UI"), Apartment(ApartmentState.STA), RunInApplicationDomain]
	public class TextEditFocusTests
	{
		[Test]
		public async Task VerifyFocusMakesTextWidgetEditable()
		{
			TextEditWidget editField = null;
			SystemWindow systemWindow = new SystemWindow(300, 200)
			{
				BackgroundColor = Color.Black,
			};

			AutomationTest testToRun = (testRunner) =>
			{
				UiThread.RunOnIdle(editField.Focus);

				testRunner.Type("Test Text");

				testRunner.Delay(1);
				Assert.IsTrue(editField.Text == "Test Text", "validate text is typed");

				return Task.CompletedTask;
			};

			editField = new TextEditWidget(pixelWidth: 200)
			{
				HAnchor = HAnchor.Center,
				VAnchor = VAnchor.Center,
			};
			systemWindow.AddChild(editField);

			await AutomationRunner.ShowWindowAndExecuteTests(systemWindow, testToRun);
		}

		[Test]
		public async Task VerifyFocusProperty()
		{
			SystemWindow systemWindow = new SystemWindow(300, 200)
			{
				BackgroundColor = Color.Black,
			};

			var editField = new TextEditWidget(pixelWidth: 200)
			{
				HAnchor = HAnchor.Center,
				VAnchor = VAnchor.Center,
			};
			systemWindow.AddChild(editField);

			AutomationTest testToRun = (testRunner) =>
			{
				UiThread.RunOnIdle(editField.Focus);
				testRunner.WaitFor(() => editField.ContainsFocus);
				Assert.IsTrue(editField.ContainsFocus, "Focused property should be true after invoking Focus method");

				return Task.CompletedTask;
			};

			await AutomationRunner.ShowWindowAndExecuteTests(systemWindow, testToRun);
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

			AutomationTest testToRun = (testRunner) =>
			{
				editField.SelectAllOnFocus = true;
				testRunner.Delay(1);
				testRunner.ClickByName(editField.Name);

				editField.SelectAllOnFocus = true;
				testRunner.Type("123");
				Assert.AreEqual("123", editField.Text, "Text input on newly focused control should replace selection");

				testRunner.ClickByName(editField.Name);
				testRunner.Delay(.2);

				testRunner.Type("123");
				Assert.AreEqual("123123", editField.Text, "Text should be appended if control is focused and has already received input");

				return Task.CompletedTask;
			};

			await AutomationRunner.ShowWindowAndExecuteTests(systemWindow, testToRun);
		}
	}
#endif
}
