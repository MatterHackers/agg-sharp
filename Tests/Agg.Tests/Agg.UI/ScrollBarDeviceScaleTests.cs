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
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// <see cref="ScrollBar.ScrollBarWidth"/> and <see cref="ScrollBar.GrowThumbBy"/> were static property
	/// initializers multiplied by <see cref="GuiWidget.DeviceScale"/>, so they ran once when the type first
	/// loaded and then held that monitor's scale for the life of the process. Moving to a Retina display, or
	/// simply loading the type before the scale was detected, left a hairline scroll bar forever.
	/// </summary>
	/// <remarks>
	/// Both the scale and the two defaults are process wide, so these are keyless <c>[NotInParallel]</c> and
	/// restore what they touched in a finally. The explicit "load the type first" step matters: without it
	/// the type initializer would run inside the scaled block and the old code would look correct.
	/// </remarks>
	public class ScrollBarDeviceScaleTests
	{
		[Test]
		[NotInParallel]
		public async Task DefaultWidthsFollowALaterDeviceScaleChange()
		{
			double savedDeviceScale = GuiWidget.DeviceScale;
			try
			{
				// Force the type initializer to run at scale 1, which is what a process that started on a
				// non-Retina monitor (or before the scale was known) did.
				GuiWidget.DeviceScale = 1;
				ClearExplicitDefaults();
				await Assert.That(ScrollBar.ScrollBarWidth).IsEqualTo(15).Within(0.001);
				await Assert.That(ScrollBar.GrowThumbBy).IsEqualTo(3).Within(0.001);

				GuiWidget.DeviceScale = 2;

				await Assert.That(ScrollBar.ScrollBarWidth).IsEqualTo(30).Within(0.001)
					.Because("the default scroll bar width has to be read at the scale in force now, not at type load");
				await Assert.That(ScrollBar.GrowThumbBy).IsEqualTo(6).Within(0.001);
			}
			finally
			{
				GuiWidget.DeviceScale = savedDeviceScale;
				ClearExplicitDefaults();
			}
		}

		[Test]
		[NotInParallel]
		public async Task AnExplicitWidthWinsOverTheScaledDefault()
		{
			double savedDeviceScale = GuiWidget.DeviceScale;
			try
			{
				GuiWidget.DeviceScale = 2;

				// An application that sets these owns the number outright - it is already in device pixels, so
				// nothing may scale it a second time.
				ScrollBar.ScrollBarWidth = 22;
				ScrollBar.GrowThumbBy = 4;

				await Assert.That(ScrollBar.ScrollBarWidth).IsEqualTo(22).Within(0.001);
				await Assert.That(ScrollBar.GrowThumbBy).IsEqualTo(4).Within(0.001);
			}
			finally
			{
				GuiWidget.DeviceScale = savedDeviceScale;
				ClearExplicitDefaults();
			}
		}

		/// <summary>
		/// Puts both settings back to "never set", which the public setters cannot express. Only tests need
		/// this - an application sets them once at startup.
		/// </summary>
		private static void ClearExplicitDefaults()
		{
			SetPrivateStatic("explicitScrollBarWidth", null);
			SetPrivateStatic("explicitGrowThumbBy", null);
		}

		private static void SetPrivateStatic(string fieldName, object value)
		{
			FieldInfo field = typeof(ScrollBar).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
			if (field == null)
			{
				throw new InvalidOperationException($"ScrollBar has no {fieldName} - the scaled defaults are still baked at type load");
			}

			field.SetValue(null, value);
		}
	}
}
