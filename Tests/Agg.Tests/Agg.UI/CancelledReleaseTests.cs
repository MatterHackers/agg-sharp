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

using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	// A cancelled up (MouseEventArgs.Cancelled: the platform took the pointer, or the touch layer ruled the gesture a
	// drag or a pinch) ends the drag but must activate nothing - in every widget that acts on its release.
	public class CancelledReleaseTests
	{
		private static MouseEventArgs Press(double x, double y) => new MouseEventArgs(MouseButtons.Left, 1, x, y, 0);

		private static MouseEventArgs Cancel(double x, double y) => new MouseEventArgs(MouseButtons.Left, 1, x, y, 0) { Cancelled = true };

		[Test]
		public async Task AWidgetDoesNotClick()
		{
			var widget = new GuiWidget(50, 50);
			int clicks = 0;
			widget.Click += (s, e) => clicks++;
			widget.OnMouseDown(Press(10, 10));
			widget.OnMouseUp(Cancel(10, 10));
			await Assert.That(clicks).IsEqualTo(0);
			await Assert.That(widget.MouseCaptured).IsFalse();
		}

		[Test]
		public async Task AMenuItemIsNotSelected()
		{
			var item = new MenuItem(new GuiWidget(50, 20)) { AllowClicks = () => true };
			int selected = 0;
			item.Selected += (s, e) => selected++;
			item.OnMouseDown(Press(10, 10));
			item.OnMouseUp(Cancel(10, 10));
			await Assert.That(selected).IsEqualTo(0);

			item.OnMouseDown(Press(10, 10));
			item.OnMouseUp(Press(10, 10));
			await Assert.That(selected).IsEqualTo(1);
		}

		[Test]
		public async Task ADragValueDoesNotOpenItsEditor()
		{
			var dragValue = new DragValue(3.25, 0, 10, new ThemeConfig());
			double s = GuiWidget.DeviceScale;
			dragValue.OnMouseDown(Press(10 * s, 5));
			dragValue.OnMouseUp(Cancel(10 * s, 5));
			await Assert.That(dragValue.IsEditing).IsFalse();
		}

		[Test]
		public async Task AVirtualTableRowIsNotClicked()
		{
			double s = GuiWidget.DeviceScale;
			var table = new VirtualTable(new[] { TableColumn.Auto(56), TableColumn.Remainder(atLeast: 40) })
			{
				Width = 308 * s,
				Height = 222 * s,
				Rows = TableRows.Homogeneous(50, 20),
				ClickableRows = true,
			};
			int clicked = 0;
			table.RowClicked += (row, column) => clicked++;
			table.OnMouseDown(Press(100 * s, 150 * s));
			table.OnMouseUp(Cancel(100 * s, 150 * s));
			await Assert.That(clicked).IsEqualTo(0);
		}
	}
}
