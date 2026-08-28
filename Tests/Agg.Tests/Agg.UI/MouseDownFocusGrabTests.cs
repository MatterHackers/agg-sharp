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

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using System.Threading.Tasks;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// The focus grab <see cref="GuiWidget.OnMouseDown"/> performs as a press unwinds: "no child of mine came
	/// out of that press holding the keyboard, so I take it". It is what moves the focus off a text field when
	/// the user presses somewhere that cannot be focused itself - which is what fires EditComplete - and it has
	/// to stand down only for a press that deliberately moved the focus somewhere else, such as one that opened
	/// a popup.
	/// </summary>
	/// <remarks>
	/// Every tree here is parented under a real <see cref="SystemWindow"/> on purpose: a widget with no parent
	/// has <see cref="GuiWidget.CanSelect"/> false, so a parentless container never runs the grab at all and a
	/// harness built on one cannot see any of this.
	/// </remarks>
	public class MouseDownFocusGrabTests
	{
		/// <summary>
		/// A widget that takes the mouse but declines the keyboard - a toolbar button, in the shape the widget
		/// tree sees it. <see cref="GuiWidget.CanFocus"/> is the only knob for this; leaving Selectable false
		/// instead would stop the press reaching it at all.
		/// </summary>
		private class DeclinesFocusWidget : GuiWidget
		{
			public DeclinesFocusWidget(double width, double height)
				: base(width, height)
			{
			}

			public override bool CanFocus => false;
		}

		/// <summary>
		/// The load-bearing case: pressing blank space takes the keyboard off whatever had it and gives it to
		/// the widget the blank space belongs to. A text field losing the focus this way is what commits its
		/// edit, so this is the path EditComplete hangs off.
		/// </summary>
		[Test]
		public async Task PressingBlankSpaceMovesTheFocusOntoTheWidgetOwningIt()
		{
			var window = new SystemWindow(600, 400);

			var host = new GuiWidget
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
				Name = "Host",
			};
			window.AddChild(host);

			var field = new GuiWidget(100, 20) { Name = "Field" };
			host.AddChild(field);
			field.OriginRelativeParent = new Vector2(10, 10);

			field.Focus();
			await Assert.That(field.Focused).IsTrue().Because("the fixture starts with the field holding the keyboard");

			// the far corner of the host, which nothing else covers
			window.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, window.Width - 20, window.Height - 20, 0));

			await Assert.That(field.Focused).IsFalse()
				.Because("pressing away from a field has to take the keyboard off it - that is what commits the edit");

			await Assert.That(host.Focused).IsTrue()
				.Because("the widget the press landed in takes the keyboard when no child of it did");
		}

		/// <summary>
		/// The press handler itself unfocusing something in another part of the tree is not the focus "moving
		/// on" - <see cref="GuiWidget.Unfocus"/> clears the widget and its descendants and never its ancestors,
		/// so the focused leaf collapses onto a shared ancestor rather than landing anywhere new. Reading that
		/// as a deliberate move made every widget on the press path stand its grab down, and when the ancestor
		/// it collapsed onto was the SystemWindow - which has no parent and so cannot be selected - nothing in
		/// the window ended up holding the keyboard at all and key events dead-ended.
		/// </summary>
		[Test]
		public async Task AHandlerUnfocusingElsewhereDoesNotStandTheGrabDown()
		{
			var window = new SystemWindow(600, 400);

			// a direct child of the window, so unfocusing it collapses the focused leaf onto the window itself
			var field = new GuiWidget(100, 20) { Name = "Field" };
			window.AddChild(field);
			field.OriginRelativeParent = new Vector2(10, 10);

			var toolbar = new GuiWidget(200, 40) { Name = "Toolbar" };
			window.AddChild(toolbar);
			toolbar.OriginRelativeParent = new Vector2(10, 300);

			var button = new DeclinesFocusWidget(50, 20) { Name = "Button" };
			toolbar.AddChild(button);
			button.OriginRelativeParent = new Vector2(5, 5);

			// what a command button does: acts on the field, which drops the field's claim on the keyboard
			button.MouseDownCaptured += (s, e) => field.Unfocus();

			field.Focus();

			var pressAt = button.TransformToScreenSpace(button.LocalBounds).Center;
			window.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, pressAt.X, pressAt.Y, 0));

			await Assert.That(window.Focused).IsFalse()
				.Because("the keyboard settling on the window itself, with nothing under it holding it, is where"
					+ " key events dead-end - they are routed down through the children that contain the focus");

			await Assert.That(toolbar.Focused).IsTrue()
				.Because("the button declines the keyboard, so its parent takes it - the focus never moved anywhere"
					+ " new, it only collapsed onto the window when the handler unfocused the field");
		}
	}
}
