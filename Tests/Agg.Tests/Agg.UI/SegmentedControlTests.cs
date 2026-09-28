/*
Copyright (c) 2026, Lars Brubaker, MatterHackers, Inc.
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
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>SegmentedControl selection by click, keys and code, and when SelectedIndexChanged fires.</summary>
	public class SegmentedControlTests
	{
		private static SegmentedControl Create(int selected = 0)
		{
			return new SegmentedControl(new[] { "One", "Two", "Three" }, new ThemeConfig(), selected);
		}

		[Test]
		public async Task ClickingASegmentSelectsItAndRaisesOnce()
		{
			var control = Create();
			int changes = 0;
			control.SelectedIndexChanged += (s, e) => changes++;

			control.Segments[2].InvokeClick();
			control.Segments[2].InvokeClick();

			await Assert.That(control.SelectedIndex).IsEqualTo(2);
			await Assert.That(control.SelectedLabel).IsEqualTo("Three");
			await Assert.That(changes).IsEqualTo(1).Because("re-clicking the selected segment is not a change");
		}

		[Test]
		public async Task SegmentsAreNamedJoinedAndEquallyWide()
		{
			var control = Create();

			var found = control.FindDescendants("Two Segment");
			await Assert.That(found.Count).IsEqualTo(1);
			await Assert.That(found[0].Widget).IsSameReferenceAs(control.Segments[1]);
			await Assert.That(control.Segments[1].Width).IsEqualTo(control.Segments[0].Width);
			await Assert.That(control.Segments[1].Position.X).IsEqualTo(control.Segments[0].Width);
			await Assert.That(control.Width).IsEqualTo(control.Segments[0].Width * 3);
		}

		[Test]
		public async Task ArrowKeysMoveTheSelectionAndStopAtTheEnds()
		{
			var control = Create(1);
			int changes = 0;
			control.SelectedIndexChanged += (s, e) => changes++;

			control.OnKeyDown(new KeyEventArgs(Keys.Right));
			await Assert.That(control.SelectedIndex).IsEqualTo(2);

			var atEnd = new KeyEventArgs(Keys.Right);
			control.OnKeyDown(atEnd);
			await Assert.That(control.SelectedIndex).IsEqualTo(2);
			await Assert.That(atEnd.Handled).IsTrue().Because("an end key is consumed so it cannot scroll a parent");

			control.OnKeyDown(new KeyEventArgs(Keys.Home));
			await Assert.That(control.SelectedIndex).IsEqualTo(0);
			control.OnKeyDown(new KeyEventArgs(Keys.Left));
			await Assert.That(control.SelectedIndex).IsEqualTo(0);
			await Assert.That(changes).IsEqualTo(2);
		}

		[Test]
		public async Task OutOfRangeIndicesAreClampedOrIgnored()
		{
			var control = Create(9);
			await Assert.That(control.SelectedIndex).IsEqualTo(2);

			control.SelectedIndex = -1;
			control.SelectedIndex = 3;
			await Assert.That(control.SelectedIndex).IsEqualTo(2);
		}
	}
}
