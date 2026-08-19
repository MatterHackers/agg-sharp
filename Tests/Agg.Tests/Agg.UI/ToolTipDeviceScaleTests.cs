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
	/// A tooltip is placed a fixed distance above the mouse so it clears the cursor. The cursor is drawn by
	/// the OS at the display's scale, so that clearance is a design measurement, not a device one - left at
	/// 23 raw pixels the tooltip sat under a Retina cursor's tip and covered its own first line.
	/// </summary>
	/// <remarks>
	/// <see cref="GuiWidget.DeviceScale"/> is process wide, so this is a keyless <c>[NotInParallel]</c> and
	/// restores both it and <see cref="ToolTipManager.CreateToolTip"/> in a finally. A fixed size tooltip
	/// body is substituted so the gap being measured cannot move just because the text got bigger.
	/// </remarks>
	public class ToolTipDeviceScaleTests
	{
		[Test]
		[NotInParallel]
		public async Task TheGapAboveTheCursorGrowsWithDeviceScale()
		{
			await Assert.That(GapAboveMouse(deviceScale: 1)).IsEqualTo(23).Within(0.001);
			await Assert.That(GapAboveMouse(deviceScale: 2)).IsEqualTo(46).Within(0.001)
				.Because("the cursor the tooltip has to clear is twice as tall on a Retina display");
		}

		/// <summary>
		/// How far the bottom edge of a shown tooltip sits above the mouse.
		/// </summary>
		private static double GapAboveMouse(double deviceScale)
		{
			double savedDeviceScale = GuiWidget.DeviceScale;
			var savedCreateToolTip = ToolTipManager.CreateToolTip;
			try
			{
				GuiWidget.DeviceScale = deviceScale;
				ToolTipManager.CreateToolTip = text => (new GuiWidget(80, 40), (widget, newText) => { });

				// Roomy in every direction, so none of the screen edge nudges can fire and the only thing
				// setting the vertical position is the cursor clearance.
				var systemWindow = new SystemWindow(800, 600);
				var hovered = new GuiWidget(100, 100)
				{
					OriginRelativeParent = new Vector2(200, 350),
					ToolTipText = "some help"
				};
				systemWindow.AddChild(hovered);

				var mousePosition = new Vector2(250, 400);

				SetPrivateField(systemWindow.ToolTipManager, "mousePosition", mousePosition);
				SetPrivateField(systemWindow.ToolTipManager, "widgetThatWantsToShowToolTip", hovered);

				var doShow = typeof(ToolTipManager).GetMethod("DoShowToolTip", BindingFlags.Instance | BindingFlags.NonPublic);
				if (!(bool)doShow.Invoke(systemWindow.ToolTipManager, null))
				{
					throw new InvalidOperationException("no tooltip was shown - the test setup, not the placement, is wrong");
				}

				var toolTip = (GuiWidget)GetPrivateField(systemWindow.ToolTipManager, "toolTipWidget");

				return mousePosition.Y - toolTip.BoundsRelativeToParent.Top;
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
