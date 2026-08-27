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
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// A pinch on a trackpad arrives as its own kind of event carrying an incremental magnification, and agg
	/// only understands wheel units, so the whole gesture turns on that one conversion. The gesture itself
	/// cannot be synthesised - it needs real fingers on real glass - but the conversion can, and it is where
	/// a sign error or a lost magnitude would hide. The conversion is <see cref="WheelDeltaMath"/>'s, shared
	/// by every host, so this runs everywhere.
	/// </summary>
	public class MagnificationWheelDeltaTests
	{
		[Test]
		public async Task FingersApartZoomInAndTogetherZoomOut()
		{
			// Positive magnification is fingers moving apart, which has to come out as a forward wheel,
			// because forward is what the 3D view reads as zoom in.
			await Assert.That(WheelDeltaMath.MagnificationToWheelDelta(0.05)).IsGreaterThan(0);
			await Assert.That(WheelDeltaMath.MagnificationToWheelDelta(-0.05)).IsLessThan(0);
			await Assert.That(WheelDeltaMath.MagnificationToWheelDelta(0)).IsEqualTo(0);
		}

		[Test]
		public async Task TheZoomFollowsHowFarTheFingersMoved()
		{
			int small = WheelDeltaMath.MagnificationToWheelDelta(0.02);
			int twiceAsFar = WheelDeltaMath.MagnificationToWheelDelta(0.04);

			// A single event's magnification is a hundredth or so, so it has to survive the trip to integer
			// wheel units - rounding it away would make a slow pinch do nothing at all.
			await Assert.That(small).IsGreaterThan(0);
			await Assert.That(twiceAsFar).IsEqualTo(small * 2);

			// and a whole-gesture magnification of 1 (Apple's "twice the size") is several wheel detents
			await Assert.That(WheelDeltaMath.MagnificationToWheelDelta(1)).IsEqualTo(small * 50);
		}

		[Test]
		public async Task ANonsenseMagnificationIsNoZoom()
		{
			// (int) of a NaN is a huge negative number rather than nothing, and that would fling the camera.
			await Assert.That(WheelDeltaMath.MagnificationToWheelDelta(double.NaN)).IsEqualTo(0);
			await Assert.That(WheelDeltaMath.MagnificationToWheelDelta(double.PositiveInfinity)).IsEqualTo(0);
		}
	}
}
