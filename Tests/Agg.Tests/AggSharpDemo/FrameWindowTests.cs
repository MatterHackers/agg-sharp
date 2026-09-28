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

using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg.UI;
using MatterHackers.AggSharpDemo.GuiDemo;
using MatterHackers.AggSharpDemo.GuiDemo.Windows.Layout;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	// The GUI demo's Frame window (agg-gui's demo-ui/src/windows/frame_demo.rs).
	[NotInParallel(SharedStateKeys.ThemeConfigCurrent)] // new DemoTheme() writes ThemeConfig.Current
	public class FrameWindowTests
	{
		private static DemoSpec FrameSpec => GuiDemoSpecs.All.First(s => s.Title == "Frame");

		private static FrameWindow Build()
		{
			var page = new GuiWidget(360, 290);
			GuiWidget content = GuiDemoSpecs.CreateContent(FrameSpec);
			page.AddChild(content);
			page.PerformLayout();
			return (FrameWindow)content;
		}

		[Test]
		public async Task BuildsEveryControlNamed()
		{
			FrameWindow window = Build();
			await Assert.That(window.Name).IsEqualTo("Frame Content");

			string[] names =
			{
				"Frame Scroll", "Frame Controls", "Frame Preview", "Frame Preview Content",
				"Frame Inner margin Same", "Frame Inner margin Value", "Frame Inner margin Left", "Frame Inner margin Bottom",
				"Frame Outer margin Same", "Frame Outer margin Value", "Frame Outer margin Top",
				"Frame Corner radius Same", "Frame Corner radius Value", "Frame Corner radius NW", "Frame Corner radius SE",
				"Frame Shadow X", "Frame Shadow Y", "Frame Shadow Blur", "Frame Shadow Spread", "Frame Shadow Color",
				"Frame Fill Color", "Frame Stroke Width", "Frame Stroke Color", "Frame Reset", "Frame Source Link",
			};
			foreach (string name in names)
			{
				await Assert.That(window.FindDescendant(name)).IsNotNull();
			}

			// "same" is on, so only the single value shows; the four per-side values wait below it.
			await Assert.That(window.FindDescendant("Frame Inner margin Value").Visible).IsTrue();
			await Assert.That(window.FindDescendant("Frame Inner margin Left").Parent.Visible).IsFalse();

			// The shadow prefixes take a fixed column, so "x:" / "blur:" and "y:" / "spread:" drags line up.
			double DragLeft(string name)
			{
				GuiWidget drag = window.FindDescendant(name);
				return drag.TransformToScreenSpace(drag.LocalBounds).Left;
			}

			await Assert.That(DragLeft("Frame Shadow X")).IsEqualTo(DragLeft("Frame Shadow Blur")).Within(0.001);
			// The second pair lines up to within a unit: the first drags' widths can differ by a fraction of one.
			await Assert.That(DragLeft("Frame Shadow Y")).IsEqualTo(DragLeft("Frame Shadow Spread")).Within(1);

			// The preview is the content plus both margins: never less than 160 by 140.
			double scale = GuiWidget.DeviceScale;
			await Assert.That(window.Preview.Width).IsGreaterThanOrEqualTo(FramePreview.MinimumDesignWidth * scale);
			await Assert.That(window.Preview.Height).IsGreaterThanOrEqualTo(FramePreview.MinimumDesignHeight * scale);
		}

		[Test]
		public async Task UntickingSameEditsOneSideAndResetRestoresIt()
		{
			FrameWindow window = Build();
			var same = (CheckBox)window.FindDescendant("Frame Outer margin Same");
			same.Checked = false;
			await Assert.That(window.FindDescendant("Frame Outer margin Value").Visible).IsFalse();
			await Assert.That(window.FindDescendant("Frame Outer margin Left").Parent.Visible).IsTrue();

			// Growing the left outer margin moves the frame right and widens the preview.
			double frameLeft = window.Preview.FrameBounds.Left;
			var left = (DragValue)window.FindDescendant("Frame Outer margin Left");
			left.Value = 100;
			await Assert.That(window.State.OuterMargin[0]).IsEqualTo(100);
			await Assert.That(window.State.OuterMargin[1]).IsEqualTo(FrameState.DefaultOuterMargin);
			await Assert.That(window.Preview.FrameBounds.Left).IsGreaterThan(frameLeft);
			double scale = GuiWidget.DeviceScale;
			await Assert.That(window.Preview.Width).IsGreaterThanOrEqualTo((100 + FrameState.DefaultOuterMargin + 2 * FrameState.DefaultInnerMargin) * scale);

			// Ticking it again collapses to the average.
			same.Checked = true;
			await Assert.That(window.State.OuterMargin[3]).IsEqualTo((100 + 3 * FrameState.DefaultOuterMargin) / 4);

			window.Reset();
			await Assert.That(window.State.OuterMargin[0]).IsEqualTo(FrameState.DefaultOuterMargin);
			var value = (DragValue)window.FindDescendant("Frame Outer margin Value");
			await Assert.That(value.Value).IsEqualTo(FrameState.DefaultOuterMargin);
			await Assert.That(window.Preview.FrameBounds.Left).IsEqualTo(frameLeft);
		}
	}
}
