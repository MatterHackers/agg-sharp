/*
Copyright (c) 2026, Lars Brubaker
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
*/

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.GuiAutomation;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// The caret suggestion popup: keys and clicks pushed straight into a headless <see cref="SystemWindow"/>,
	/// as the menu conformance tests do. Focus stays in the field throughout - that is the contract that makes
	/// this different from a <see cref="PopupMenu"/>.
	/// </summary>
	[NotInParallel(nameof(AutomationRunner.ShowWindowAndExecuteTests))]
	public class TextSuggestionControllerTests
	{
		private static readonly string[] Words = { "apple", "apricot", "avocado", "banana", "obj." };

		[After(Test)]
		public void DrainTheIdleQueue()
		{
			for (int i = 0; i < 4; i++)
			{
				UiThread.InvokePendingActions();
			}
		}

		[Test]
		public async Task OpensWhenTypingFindsSuggestionsAndStaysClosedWhenNone()
		{
			var harness = new Harness();

			harness.Type('z');
			await Assert.That(harness.Controller.IsOpen).IsFalse();
			await Assert.That(harness.Provider.Calls.Count).IsEqualTo(1).Because("typing asks the provider even when it then has nothing");

			harness.Field.Text = "";
			harness.Type('a');
			await Assert.That(harness.Controller.IsOpen).IsTrue();
			await Assert.That(harness.Controller.Popup.Visible).IsTrue();
			await Assert.That(harness.Labels()).IsEquivalentTo(new[] { "apple", "apricot", "avocado" });
			await Assert.That(harness.Controller.HighlightIndex).IsEqualTo(0);
		}

		[Test]
		public async Task DownAndUpMoveTheHighlightAndWrap()
		{
			var harness = new Harness();
			harness.Type('a');

			harness.Key(Keys.Down);
			await Assert.That(harness.Controller.HighlightIndex).IsEqualTo(1);
			harness.Key(Keys.Down);
			harness.Key(Keys.Down);
			await Assert.That(harness.Controller.HighlightIndex).IsEqualTo(0).Because("Down past the last row wraps to the first");
			harness.Key(Keys.Up);
			await Assert.That(harness.Controller.HighlightIndex).IsEqualTo(2).Because("Up past the first row wraps to the last");
			await Assert.That(harness.Field.Text).IsEqualTo("a").Because("the arrows belong to the list while it is open");
			await Assert.That(harness.Field.InternalTextEditWidget.Focused).IsTrue();
		}

		[Test]
		public async Task EnterAcceptsKeepsFocusAndDoesNotSubmit()
		{
			var harness = new Harness();
			int enterPressed = 0;
			int editComplete = 0;
			harness.Field.EnterPressed += (s, e) => enterPressed++;
			harness.Field.EditComplete += (s, e) => editComplete++;

			harness.Type('a');
			harness.Type('p');
			harness.Key(Keys.Down);
			harness.Key(Keys.Enter);

			await Assert.That(harness.Field.Text).IsEqualTo("apricot");
			await Assert.That(harness.Field.CharIndexToInsertBefore).IsEqualTo(7);
			await Assert.That(harness.Field.InternalTextEditWidget.Focused).IsTrue();
			await Assert.That(enterPressed).IsEqualTo(0);
			await Assert.That(editComplete).IsEqualTo(0);
		}

		[Test]
		public async Task TabAcceptsAndDoesNotMoveFocus()
		{
			var harness = new Harness();
			harness.Type('b');
			harness.Key(Keys.Tab);

			await Assert.That(harness.Field.Text).IsEqualTo("banana");
			await Assert.That(harness.Field.InternalTextEditWidget.Focused).IsTrue();
			await Assert.That(harness.OtherField.ContainsFocus).IsFalse();
		}

		[Test]
		public async Task EscapeClosesWithoutAnyOtherEffect()
		{
			var harness = new Harness();
			var escapesSeenUnhandled = 0;
			harness.Window.KeyDown += (s, e) =>
			{
				if (e.KeyCode == Keys.Escape && !e.Handled)
				{
					escapesSeenUnhandled++;
				}
			};

			harness.Type('a');
			harness.Key(Keys.Escape);

			await Assert.That(harness.Controller.IsOpen).IsFalse();
			await Assert.That(harness.Field.Text).IsEqualTo("a");
			await Assert.That(harness.Field.InternalTextEditWidget.Focused).IsTrue();
			await Assert.That(escapesSeenUnhandled).IsEqualTo(0).Because("a dialog must not also close on the Escape that closed the list");

			harness.Key(Keys.Escape);
			await Assert.That(escapesSeenUnhandled).IsEqualTo(1).Because("with the list closed Escape is the field's again");
		}

		[Test]
		public async Task ClickingARowAcceptsItAndKeepsFocusInTheField()
		{
			var harness = new Harness();
			int editComplete = 0;
			harness.Field.EditComplete += (s, e) => editComplete++;
			harness.Type('a');

			var popup = harness.Controller.Popup;
			var rowCenter = popup.TransformToParentSpace(harness.Window, popup.RowBounds(2).Center);
			harness.Window.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, rowCenter.X, rowCenter.Y, 0));
			harness.Window.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, rowCenter.X, rowCenter.Y, 0));
			UiThread.InvokePendingActions();

			await Assert.That(harness.Field.Text).IsEqualTo("avocado");
			await Assert.That(harness.Field.InternalTextEditWidget.Focused).IsTrue();
			await Assert.That(editComplete).IsEqualTo(0).Because("the press on the list must never take focus off the field");
		}

		[Test]
		public async Task TypingAndBackspaceRefilter()
		{
			var harness = new Harness();
			harness.Type('a');
			harness.Type('p');

			await Assert.That(harness.Provider.Calls.Last()).IsEqualTo(("ap", 2));
			await Assert.That(harness.Labels()).IsEquivalentTo(new[] { "apple", "apricot" });

			harness.Key(Keys.Back);
			await Assert.That(harness.Provider.Calls.Last()).IsEqualTo(("a", 1));
			await Assert.That(harness.Labels()).IsEquivalentTo(new[] { "apple", "apricot", "avocado" });

			harness.Key(Keys.Home);
			await Assert.That(harness.Controller.IsOpen).IsFalse().Because("no word before the caret, so nothing to offer");
		}

		[Test]
		public async Task LosingFocusCloses()
		{
			var harness = new Harness();
			harness.Type('a');
			harness.OtherField.Focus();

			await Assert.That(harness.Controller.IsOpen).IsFalse();
			await Assert.That(harness.Controller.Popup.Visible).IsFalse();
		}

		[Test]
		public async Task AcceptQueriesAgainAtTheNewCaret()
		{
			var harness = new Harness();
			harness.Type('o');
			harness.Key(Keys.Enter);

			await Assert.That(harness.Field.Text).IsEqualTo("obj.");
			await Assert.That(harness.Provider.Calls.Last()).IsEqualTo(("obj.", 4));
			await Assert.That(harness.Controller.IsOpen).IsTrue().Because("an insert ending in '.' shows the members next");
			await Assert.That(harness.Labels()).IsEquivalentTo(new[] { "width", "height" });
		}

		/// <summary>
		/// The list hangs just below the caret's line, lined up with the start of the word being completed (as code
		/// editors do), and does not slide right as the word is typed.
		/// </summary>
		[Test]
		public async Task PopupSitsJustBelowTheWordBeingCompleted()
		{
			var harness = new Harness();
			harness.Field.Text = "1 + ";
			harness.Field.InternalTextEditWidget.SetCursorPosition(4);
			harness.Type('b');
			var firstLeft = harness.PopupBoundsInWindow().Left;
			harness.Type('a');

			var edit = harness.Field.InternalTextEditWidget;
			var wordStart = edit.Printer.GetOffsetLeftOfCharacterIndex(4);
			double fontHeight = edit.Printer.TypeFaceStyle.EmSizeInPixels;
			var lineBottom = edit.TransformToParentSpace(harness.Window, new Vector2(wordStart.X, edit.Height + wordStart.Y - fontHeight));
			var popupBounds = harness.PopupBoundsInWindow();

			await Assert.That(popupBounds.Top).IsLessThanOrEqualTo(lineBottom.Y);
			await Assert.That(lineBottom.Y - popupBounds.Top).IsLessThan(10.0);
			await Assert.That(System.Math.Abs(popupBounds.Left - lineBottom.X)).IsLessThan(1.0);
			await Assert.That(lineBottom.X).IsGreaterThan(harness.Field.TransformToParentSpace(harness.Window, Vector2.Zero).X + 5)
				.Because("the word starts after \"1 + \", so this pins the anchor to the word, not the field's left edge");
			await Assert.That(popupBounds.Left).IsEqualTo(firstLeft);
		}

		private class FakeProvider : ITextSuggestionProvider
		{
			public List<(string Text, int Caret)> Calls { get; } = new List<(string, int)>();

			public TextSuggestionList GetSuggestions(string text, int caret)
			{
				this.Calls.Add((text, caret));
				if (text.Substring(0, caret).EndsWith("obj."))
				{
					return new TextSuggestionList(caret, 0, new[]
					{
						new TextSuggestion("width", detail: "20"),
						new TextSuggestion("height", detail: "10", description: "How tall it is"),
					});
				}

				int start = caret;
				while (start > 0 && char.IsLetter(text[start - 1]))
				{
					start--;
				}

				string word = text.Substring(start, caret - start);
				if (word.Length == 0)
				{
					return TextSuggestionList.Empty;
				}

				var matches = Words.Where(w => w.StartsWith(word)).Select(w => new TextSuggestion(w)).ToArray();
				return new TextSuggestionList(start, caret - start, matches);
			}
		}

		private class Harness
		{
			public Harness()
			{
				this.Window = new SystemWindow(400, 300);
				this.Field = new TextEditWidget(pixelWidth: 200)
				{
					OriginRelativeParent = new Vector2(50, 200),
				};
				this.OtherField = new TextEditWidget(pixelWidth: 200)
				{
					OriginRelativeParent = new Vector2(50, 20),
				};
				this.Window.AddChild(this.Field);
				this.Window.AddChild(this.OtherField);
				this.Provider = new FakeProvider();
				this.Controller = new TextSuggestionController(this.Field, this.Provider);
				this.Field.Focus();
			}

			public SystemWindow Window { get; }

			public TextEditWidget Field { get; }

			public TextEditWidget OtherField { get; }

			public FakeProvider Provider { get; }

			public TextSuggestionController Controller { get; }

			public RectangleDouble PopupBoundsInWindow() => this.Controller.Popup.TransformToParentSpace(this.Window, this.Controller.Popup.LocalBounds);

			public string[] Labels() => this.Controller.Suggestions.Suggestions.Select(s => s.Label).ToArray();

			public void Type(char character)
			{
				var down = new KeyEventArgs(Keys.A);
				this.Window.OnKeyDown(down);
				if (!down.SuppressKeyPress)
				{
					this.Window.OnKeyPress(new KeyPressEventArgs(character));
				}
			}

			public void Key(Keys key)
			{
				var down = new KeyEventArgs(key);
				this.Window.OnKeyDown(down);
				if (!down.SuppressKeyPress && key == Keys.Back)
				{
					this.Window.OnKeyPress(new KeyPressEventArgs('\b'));
				}
			}
		}
	}
}
