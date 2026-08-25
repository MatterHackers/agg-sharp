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
using MatterHackers.Agg.Platform.Linux;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// X11 has no wheel event: a detent is a ButtonPress/ButtonRelease pair on a synthetic button, 4 and 5
	/// for the wheel and 6 and 7 for the horizontal pair a tilt wheel sends. These cover the conversion of
	/// that button number into agg's wheel units, which agg's consumers read as Win32's 120-per-detent.
	/// </summary>
	public class X11WheelTests
	{
		[Test]
		public async Task WheelUpIsAForwardDetentAndWheelDownIsABackwardOne()
		{
			await Assert.That(WheelFor(X11.Button4).WheelDelta).IsEqualTo(120);
			await Assert.That(WheelFor(X11.Button5).WheelDelta).IsEqualTo(-120);
		}

		/// <summary>
		/// The vertical buttons carry no sideways travel, and the horizontal ones carry no forward travel.
		/// Leaking one axis into the other is what turns a tilt into a zoom.
		/// </summary>
		[Test]
		public async Task TheAxesDoNotLeakIntoEachOther()
		{
			await Assert.That(WheelFor(X11.Button4).WheelDeltaX).IsEqualTo(0);
			await Assert.That(WheelFor(X11.Button5).WheelDeltaX).IsEqualTo(0);
			await Assert.That(WheelFor(X11.Button6).WheelDelta).IsEqualTo(0);
			await Assert.That(WheelFor(X11.Button7).WheelDelta).IsEqualTo(0);
		}

		/// <summary>
		/// Button 6 is the leftward tilt. <see cref="MouseEventArgs.WheelDeltaX"/>'s sign convention is
		/// AppKit's - positive means the content should move right, revealing what is off the left edge -
		/// which is what a leftward tilt asks for.
		/// </summary>
		[Test]
		public async Task TheHorizontalPairTiltsLeftAndRight()
		{
			await Assert.That(WheelFor(X11.Button6).WheelDeltaX).IsEqualTo(120);
			await Assert.That(WheelFor(X11.Button7).WheelDeltaX).IsEqualTo(-120);
		}

		/// <summary>
		/// Never a precise scroll. A detent carries no distance at all - it is one click - so the consumer
		/// picks its own step and scales it by DeviceScale, exactly as on Windows. Claiming precision here
		/// would make a wheel click move the content by 120 pixels.
		/// </summary>
		[Test]
		public async Task ADetentIsNeverAPreciseScroll()
		{
			await Assert.That(WheelFor(X11.Button4).WheelDeltaIsPreciseScroll).IsFalse();
			await Assert.That(WheelFor(X11.Button6).WheelDeltaIsPreciseScroll).IsFalse();
		}

		/// <summary>
		/// A real button pressed while the wheel is turned must not have its axes rewritten - only 4 to 7
		/// are wheel buttons, and the guard that keeps 1 to 3 out of this path is worth stating.
		/// </summary>
		[Test]
		public async Task ARealButtonCarriesNoWheelTravel()
		{
			await Assert.That(WheelFor(X11.Button1).WheelDelta).IsEqualTo(0);
			await Assert.That(WheelFor(X11.Button1).WheelDeltaX).IsEqualTo(0);
		}

		private static MouseEventArgs WheelFor(uint button)
		{
			var args = new MouseEventArgs(MouseButtons.None, 0, 0, 0, 0);

			X11SystemWindow.ApplyButtonWheelDeltas(args, button);

			return args;
		}
	}
}
