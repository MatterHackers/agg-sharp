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

using System;
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg.UI;
using MatterHackers.GuiAutomation;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests
{
	/// <summary>
	/// What <c>testRunner.Type</c> turns a string into. This is the whole reason keyboard shortcuts can
	/// be tested at all: everything the automation runner sends a widget tree comes through here, and a
	/// string it silently misreads is a test that types garbage and asserts nothing about the shortcut it
	/// meant to press.
	/// </summary>
	public class TypedKeyParserTests
	{
		[Test]
		public async Task CaretIsTheControlModifierOnTheKeyAfterIt()
		{
			var strokes = TypedKeyParser.Parse("^z");

			await Assert.That(strokes.Count).IsEqualTo(1);
			await Assert.That(strokes[0].Key).IsEqualTo(Keys.Control | Keys.Z);
			await Assert.That(strokes[0].HasCharacter).IsFalse()
				.Because("a control chord is not a printable character, so no KeyPress follows it");
		}

		[Test]
		public async Task ControlAppliesToOneKeyOnly()
		{
			var strokes = TypedKeyParser.Parse("^zz");

			await Assert.That(strokes.Count).IsEqualTo(2);
			await Assert.That(strokes[0].Key).IsEqualTo(Keys.Control | Keys.Z);
			await Assert.That(strokes[1].Key).IsEqualTo(Keys.Z);
			await Assert.That(strokes[1].Character).IsEqualTo('z');
		}

		[Test]
		public async Task ControlShiftChordsAreSpelledCaretPlus()
		{
			var strokes = TypedKeyParser.Parse("^+z");

			await Assert.That(strokes.Count).IsEqualTo(1);
			await Assert.That(strokes[0].Key).IsEqualTo(Keys.Control | Keys.Shift | Keys.Z);
		}

		[Test]
		public async Task APlusThatIsNotPartOfAChordIsJustAPlus()
		{
			// Real SendKeys reads a bare + as shift, which would quietly break every test that types an
			// expression into a field. Only a + directly after a ^ is a modifier here.
			var strokes = TypedKeyParser.Parse("=40 + 5");

			await Assert.That(new string(strokes.Select(stroke => stroke.Character).ToArray()))
				.IsEqualTo("=40 + 5");
			await Assert.That(strokes.Any(stroke => (stroke.Key & Keys.Shift) == Keys.Shift)).IsFalse();
		}

		[Test]
		public async Task BracedTokensBecomeTheirNamedKeyAndCarryNoCharacter()
		{
			var strokes = TypedKeyParser.Parse("{Enter}");

			await Assert.That(strokes.Count).IsEqualTo(1);
			await Assert.That(strokes[0].Key).IsEqualTo(Keys.Enter);
			await Assert.That(strokes[0].HasCharacter).IsFalse();
		}

		[Test]
		[Arguments("{Esc}", Keys.Escape)]
		[Arguments("{ESC}", Keys.Escape)]
		[Arguments("{BACKSPACE}", Keys.Back)]
		[Arguments("{LEFT}", Keys.Left)]
		[Arguments("{Delete}", Keys.Delete)]
		[Arguments("{F4}", Keys.F4)]
		public async Task TheSpellingsTestsAlreadyUseAllResolve(string text, Keys expected)
		{
			var strokes = TypedKeyParser.Parse(text);

			await Assert.That(strokes.Count).IsEqualTo(1);
			await Assert.That(strokes[0].Key).IsEqualTo(expected);
		}

		[Test]
		public async Task AnUnknownTokenIsAnError()
		{
			// The failure this replaces: an unrecognised token used to be typed out one brace and letter
			// at a time, so the test went green having pressed nothing it meant to press.
			await Assert.That(() => TypedKeyParser.Parse("{NotAKey}")).Throws<ArgumentException>();
		}

		[Test]
		public async Task PlainTextIsOneStrokePerCharacter()
		{
			var strokes = TypedKeyParser.Parse("a1.");

			await Assert.That(strokes.Count).IsEqualTo(3);
			await Assert.That(strokes[0].Key).IsEqualTo(Keys.A);
			await Assert.That(strokes[0].Character).IsEqualTo('a');
			await Assert.That(strokes[1].Key).IsEqualTo(Keys.D1);

			// The period has a key code of its own; typing it as (Keys)'.' would land on a key that is not
			// on the keyboard at all.
			await Assert.That(strokes[2].Key).IsEqualTo(Keys.OemPeriod);
			await Assert.That(strokes[2].Character).IsEqualTo('.');
		}

		[Test]
		public async Task ModifiersCanPrefixANamedKey()
		{
			var strokes = TypedKeyParser.Parse("^{Home}");

			await Assert.That(strokes.Count).IsEqualTo(1);
			await Assert.That(strokes[0].Key).IsEqualTo(Keys.Control | Keys.Home);
		}
	}
}
