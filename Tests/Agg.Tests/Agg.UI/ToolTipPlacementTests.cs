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

using System;
using System.Reflection;
using System.Threading.Tasks;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// A tooltip belongs to the widget the mouse is over, so it is centered on that widget rather than hung
	/// off the cursor. Anchored at the mouse, a toolbar tooltip lands beside its own icon and reads as if it
	/// described the next icon over. Centering only holds while the widget is small: on a full width row it
	/// would put the tooltip in the middle of the window, so the tooltip slides back to stay under the cursor.
	/// Screen edge clamps are applied last and override both.
	/// </summary>
	/// <remarks>
	/// <see cref="GuiWidget.DeviceScale"/> and <see cref="ToolTipManager.CreateToolTip"/> are both process
	/// wide, so this is a keyless <c>[NotInParallel]</c> and restores both in a finally. A fixed size tooltip
	/// body is substituted so the measurements cannot move just because the text got bigger.
	/// </remarks>
	public class ToolTipPlacementTests
	{
		private const double ToolTipWidth = 120;
		private const double ToolTipHeight = 40;
		private const double WindowWidth = 800;
		private const double WindowHeight = 600;

		/// <summary>The screen edge gap ToolTipManager keeps, in design pixels.</summary>
		private const double EdgeInset = 3;

		/// <summary>The gap ToolTipManager leaves above itself so the tooltip clears the cursor.</summary>
		private const double CursorClearance = 23;

		[Test]
		[NotInParallel]
		public async Task ToolTipIsCenteredOnTheHoveredWidget()
		{
			// Mouse deliberately off center inside the widget - placement must follow the widget, not the mouse
			var hovered = new RectangleDouble(200, 350, 240, 380);
			var toolTip = ShowToolTip(hovered, mousePosition: new Vector2(205, 355));

			await Assert.That(toolTip.XCenter).IsEqualTo(hovered.XCenter).Within(1)
				.Because("the tooltip describes the hovered widget, so it is centered on it");
		}

		[Test]
		[NotInParallel]
		public async Task CenteringDoesNotDisturbVerticalPlacement()
		{
			var hovered = new RectangleDouble(200, 350, 240, 380);
			var mousePosition = new Vector2(205, 355);
			var toolTip = ShowToolTip(hovered, mousePosition);

			await Assert.That(mousePosition.Y - toolTip.Top).IsEqualTo(CursorClearance).Within(0.001)
				.Because("the tooltip still hangs its fixed clearance below the cursor");
		}

		[Test]
		[NotInParallel]
		public async Task ToolTipIsClampedToTheRightEdge()
		{
			// A widget hard against the right edge cannot have a centered tooltip - it has to be pulled back on screen
			var hovered = new RectangleDouble(WindowWidth - 40, 350, WindowWidth, 380);
			var toolTip = ShowToolTip(hovered, mousePosition: new Vector2(WindowWidth - 20, 355));

			await Assert.That(toolTip.Right).IsEqualTo(WindowWidth - EdgeInset).Within(1);
		}

		[Test]
		[NotInParallel]
		public async Task ToolTipIsClampedToTheLeftEdge()
		{
			var hovered = new RectangleDouble(0, 350, 40, 380);
			var toolTip = ShowToolTip(hovered, mousePosition: new Vector2(20, 355));

			await Assert.That(toolTip.Left).IsEqualTo(EdgeInset).Within(1);
		}

		[Test]
		[NotInParallel]
		public async Task ToolTipWiderThanTheWindowSitsFlushLeft()
		{
			// Centered on a mid window widget, a tooltip this wide overhangs both edges, so both clamps fire and
			// they disagree - the left one has to win or the text starts off screen. This is the case that pins
			// their order; a tooltip that fits can satisfy either ordering.
			var hovered = new RectangleDouble(380, 350, 420, 380);
			var toolTip = ShowToolTip(hovered, mousePosition: new Vector2(400, 355), toolTipWidth: WindowWidth + 100);

			await Assert.That(toolTip.Left).IsEqualTo(EdgeInset).Within(1)
				.Because("text is read from the left, so an oversized tooltip overflows to the right");
		}

		[Test]
		[NotInParallel]
		public async Task ToolTipStaysUnderTheCursorOnAWideWidget()
		{
			// A full width row's center is nowhere near the cursor, so centering alone would leave the tooltip
			// pointing at nothing
			var hovered = new RectangleDouble(0, 350, WindowWidth, 380);
			var mousePosition = new Vector2(100, 355);
			var toolTip = ShowToolTip(hovered, mousePosition);

			await Assert.That(toolTip.Left).IsLessThanOrEqualTo(mousePosition.X);
			await Assert.That(toolTip.Right).IsGreaterThanOrEqualTo(mousePosition.X);
			await Assert.That(toolTip.XCenter).IsLessThan(hovered.XCenter)
				.Because("the tooltip gave up centering to follow the cursor");
		}

		/// <summary>
		/// Shows a fixed size tooltip for a widget of the given bounds and returns where it landed,
		/// in the system window's coordinates.
		/// </summary>
		private static RectangleDouble ShowToolTip(RectangleDouble hoveredBounds, Vector2 mousePosition, double toolTipWidth = ToolTipWidth)
		{
			double savedDeviceScale = GuiWidget.DeviceScale;
			var savedCreateToolTip = ToolTipManager.CreateToolTip;
			try
			{
				// The inset and clearance the assertions use are design pixels, so pin the scale at 1:1
				GuiWidget.DeviceScale = 1;
				ToolTipManager.CreateToolTip = text => (new GuiWidget(toolTipWidth, ToolTipHeight), (widget, newText) => { });

				var systemWindow = new SystemWindow(WindowWidth, WindowHeight);
				var hovered = new GuiWidget(hoveredBounds.Width, hoveredBounds.Height)
				{
					OriginRelativeParent = new Vector2(hoveredBounds.Left, hoveredBounds.Bottom),
					ToolTipText = "some help"
				};
				systemWindow.AddChild(hovered);

				SetPrivateField(systemWindow.ToolTipManager, "mousePosition", mousePosition);
				SetPrivateField(systemWindow.ToolTipManager, "widgetThatWantsToShowToolTip", hovered);

				var doShow = typeof(ToolTipManager).GetMethod("DoShowToolTip", BindingFlags.Instance | BindingFlags.NonPublic);
				if (!(bool)doShow.Invoke(systemWindow.ToolTipManager, null))
				{
					throw new InvalidOperationException("no tooltip was shown - the test setup, not the placement, is wrong");
				}

				var toolTip = (GuiWidget)GetPrivateField(systemWindow.ToolTipManager, "toolTipWidget");

				return toolTip.BoundsRelativeToParent;
			}
			finally
			{
				ToolTipManager.CreateToolTip = savedCreateToolTip;
				GuiWidget.DeviceScale = savedDeviceScale;
			}
		}

		private static void SetPrivateField(object instance, string fieldName, object value)
		{
			typeof(ToolTipManager).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(instance, value);
		}

		private static object GetPrivateField(object instance, string fieldName)
		{
			return typeof(ToolTipManager).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(instance);
		}
	}
}
