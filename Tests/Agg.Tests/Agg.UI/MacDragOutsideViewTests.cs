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
using MatterHackers.Agg.Platform.Mac;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using static MatterHackers.Agg.Platform.Mac.AppKitConstants;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// The mac half of the out-of-view drag fix: AppKit's numbering, translated into the terms the shared
	/// filter is written in. The filter's own behaviour is <see cref="OutOfViewMouseCaptureTests"/>; what is
	/// left here is the translation, which is where a mis-mapped NSEvent type would silently turn a drag into
	/// a hover and drop the up that ends it.
	/// </summary>
	public class MacDragOutsideViewTests
	{
		[Test]
		public async Task EveryMouseNSEventTypeIsMappedToItsKind()
		{
			// All three buttons, because a right or middle drag is captured on its own and each has its own
			// pair of NSEvent types.
			await Assert.That(MacSystemWindow.PointerEventKindFor(NSEventTypeLeftMouseDown)).IsEqualTo(PointerEventKind.Down);
			await Assert.That(MacSystemWindow.PointerEventKindFor(NSEventTypeRightMouseDown)).IsEqualTo(PointerEventKind.Down);
			await Assert.That(MacSystemWindow.PointerEventKindFor(NSEventTypeOtherMouseDown)).IsEqualTo(PointerEventKind.Down);

			await Assert.That(MacSystemWindow.PointerEventKindFor(NSEventTypeLeftMouseUp)).IsEqualTo(PointerEventKind.Up);
			await Assert.That(MacSystemWindow.PointerEventKindFor(NSEventTypeRightMouseUp)).IsEqualTo(PointerEventKind.Up);
			await Assert.That(MacSystemWindow.PointerEventKindFor(NSEventTypeOtherMouseUp)).IsEqualTo(PointerEventKind.Up);

			await Assert.That(MacSystemWindow.PointerEventKindFor(NSEventTypeLeftMouseDragged)).IsEqualTo(PointerEventKind.Drag);
			await Assert.That(MacSystemWindow.PointerEventKindFor(NSEventTypeRightMouseDragged)).IsEqualTo(PointerEventKind.Drag);
			await Assert.That(MacSystemWindow.PointerEventKindFor(NSEventTypeOtherMouseDragged)).IsEqualTo(PointerEventKind.Drag);
		}

		[Test]
		public async Task AHoverOrAScrollIsNeitherAPressNorADrag()
		{
			// Other is what the filter delivers on geometry alone, so a hover outside the view is dropped -
			// mapping one of these to Drag would deliver it to a widget that never saw a button go down.
			await Assert.That(MacSystemWindow.PointerEventKindFor(NSEventTypeMouseMoved)).IsEqualTo(PointerEventKind.Other);
			await Assert.That(MacSystemWindow.PointerEventKindFor(NSEventTypeScrollWheel)).IsEqualTo(PointerEventKind.Other);
			await Assert.That(MacSystemWindow.PointerEventKindFor(NSEventTypeMagnify)).IsEqualTo(PointerEventKind.Other);
		}

		/// <summary>
		/// The view's bounds and a point inside it come from AppKit as CGRect/CGPoint; the shared filter
		/// speaks agg's RectangleDouble/Vector2. The edges belong to the view, and a point up over the title
		/// bar does not - the same measured coordinates the shared test uses, through the mac adapters.
		/// </summary>
		[Test]
		public async Task TheAppKitAdapterAgreesWithTheSharedGeometry()
		{
			var bounds = new CGRect(0, 0, 400, 400);

			await Assert.That(MacSystemWindow.IsInsideBounds(new CGPoint(200, 216), bounds)).IsTrue();
			await Assert.That(MacSystemWindow.IsInsideBounds(new CGPoint(400, 400), bounds)).IsTrue();
			await Assert.That(MacSystemWindow.IsInsideBounds(new CGPoint(-180, 216), bounds)).IsFalse();

			await Assert.That(MacSystemWindow.IsRealPointerExit(new CGPoint(200, 216), bounds, dragInFlight: false)).IsFalse();
			await Assert.That(MacSystemWindow.IsRealPointerExit(new CGPoint(200, 437), bounds, dragInFlight: false)).IsTrue();
			await Assert.That(MacSystemWindow.IsRealPointerExit(new CGPoint(200, 437), bounds, dragInFlight: true)).IsFalse();
		}
	}
}
