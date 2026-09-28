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
	/// A <see cref="FlowLayoutWidget"/> places its first item one padding in from its drawn edge, whatever order its
	/// padding and stretch anchor were set in.
	/// </summary>
	/// <remarks>
	/// An empty widget that fits its children takes bounds of its padding around the origin, reaching one padding
	/// left of and below it, and keeps that left and bottom when later stretched. A LeftToRight or BottomToTop flow
	/// that placed items from the origin rather than from its bounds then drew them two paddings in.
	/// <see cref="GuiWidget.DeviceScale"/> is process wide, so these are keyless <c>[NotInParallel]</c> and restore it
	/// in a finally.
	/// </remarks>
	public class FlowLayoutPaddingTests
	{
		[Test]
		[NotInParallel]
		[Arguments(1.0)]
		[Arguments(2.0)]
		public async Task ALeftToRightFlowPaddedBeforeStretchDrawsItsFirstItemOnePaddingIn(double scale)
		{
			double savedDeviceScale = GuiWidget.DeviceScale;
			try
			{
				GuiWidget.DeviceScale = scale;
				var flow = new FlowLayoutWidget(FlowDirection.LeftToRight);
				flow.Padding = new BorderDouble(5);
				flow.HAnchor = HAnchor.Stretch;

				var parent = new GuiWidget(200 * scale, 100 * scale);
				parent.AddChild(flow);
				var item = new GuiWidget(20 * scale, 20 * scale);
				flow.AddChild(item);

				double itemLeft = item.OriginRelativeParent.X + item.LocalBounds.Left;
				await Assert.That(itemLeft - flow.LocalBounds.Left).IsEqualTo(5 * scale).Within(0.001)
					.Because($"one 5 unit padding in from the flow's drawn left edge, at {scale}x");
			}
			finally
			{
				GuiWidget.DeviceScale = savedDeviceScale;
			}
		}

		[Test]
		[NotInParallel]
		[Arguments(1.0)]
		[Arguments(2.0)]
		public async Task ABottomToTopFlowPaddedBeforeStretchDrawsItsFirstItemOnePaddingIn(double scale)
		{
			double savedDeviceScale = GuiWidget.DeviceScale;
			try
			{
				GuiWidget.DeviceScale = scale;
				var flow = new FlowLayoutWidget(FlowDirection.BottomToTop);
				flow.Padding = new BorderDouble(5);
				flow.VAnchor = VAnchor.Stretch;

				var parent = new GuiWidget(100 * scale, 200 * scale);
				parent.AddChild(flow);
				var item = new GuiWidget(20 * scale, 20 * scale);
				flow.AddChild(item);

				double itemBottom = item.OriginRelativeParent.Y + item.LocalBounds.Bottom;
				await Assert.That(itemBottom - flow.LocalBounds.Bottom).IsEqualTo(5 * scale).Within(0.001)
					.Because($"one 5 unit padding up from the flow's drawn bottom edge, at {scale}x");
			}
			finally
			{
				GuiWidget.DeviceScale = savedDeviceScale;
			}
		}

		[Test]
		[NotInParallel]
		[Arguments(1.0, true)]
		[Arguments(1.0, false)]
		[Arguments(2.0, true)]
		[Arguments(2.0, false)]
		public async Task ATopToBottomFlowDrawsUnalignedChildrenOnePaddingInWhicheverOfPaddingAndStretchComesFirst(double scale, bool paddingFirst)
		{
			// The cross axis of a vertical flow does not place Absolute or Fit children: they keep x = 0. They were
			// one padding in only when Padding was set while the flow still fit its children (bounds grew one
			// padding left of the origin); setting Stretch first left the bounds at 0, so they touched the edge.
			double savedDeviceScale = GuiWidget.DeviceScale;
			try
			{
				GuiWidget.DeviceScale = scale;
				var flow = new FlowLayoutWidget(FlowDirection.TopToBottom);
				if (paddingFirst)
				{
					flow.Padding = new BorderDouble(10);
					flow.HAnchor = HAnchor.Stretch;
				}
				else
				{
					flow.HAnchor = HAnchor.Stretch;
					flow.Padding = new BorderDouble(10);
				}

				var parent = new GuiWidget(200 * scale, 100 * scale);
				parent.AddChild(flow);
				var item = new GuiWidget(20 * scale, 20 * scale);
				flow.AddChild(item);
				var text = new TextWidget("label") { AutoExpandBoundsToText = true };
				flow.AddChild(text);
				var stretched = new GuiWidget(20 * scale, 20 * scale) { HAnchor = HAnchor.Stretch };
				flow.AddChild(stretched);
				parent.PerformLayout();

				double flowLeftInParent = flow.OriginRelativeParent.X + flow.LocalBounds.Left;
				await Assert.That(flowLeftInParent).IsEqualTo(0).Within(0.001).Because("the flow still fills its parent");
				await Assert.That(flow.Width).IsEqualTo(200 * scale).Within(0.001);
				foreach (GuiWidget child in new[] { item, text, stretched })
				{
					double childLeft = child.OriginRelativeParent.X + child.LocalBounds.Left;
					await Assert.That(childLeft - flow.LocalBounds.Left).IsEqualTo(10 * scale).Within(0.001)
						.Because($"a {child.HAnchor} child sits one 10 unit padding in, padding first: {paddingFirst}, at {scale}x");
				}
			}
			finally
			{
				GuiWidget.DeviceScale = savedDeviceScale;
			}
		}
	}
}
