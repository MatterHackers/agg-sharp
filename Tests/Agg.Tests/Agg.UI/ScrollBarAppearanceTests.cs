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
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	// ScrollBar's opt-in appearance: widths, margins, handle minimum, floating/auto-hide, colours, the corner and the
	// edge fade. Each option's default leaves the bar as it always was.
	public class ScrollBarAppearanceTests
	{
		/// <summary>A 200 x 100 view, parented so the scroll clamp runs.</summary>
		private static ScrollableWidget Make(double contentWidth = 100, double contentHeight = 500)
		{
			var container = new GuiWidget(200, 100);
			var scroll = new ScrollableWidget(200, 100, autoScroll: true) { MinimumSize = Vector2.Zero };
			container.AddChild(scroll);
			scroll.AddChild(new GuiWidget(contentWidth, contentHeight, SizeLimitsToSet.None));
			return scroll;
		}

		private static MouseEventArgs Mouse(double x, double y) => new MouseEventArgs(MouseButtons.None, 0, x, y, 0);

		[Test]
		public async Task DefaultsKeepTheLegacyGeometry()
		{
			ScrollableWidget scroll = Make();
			ScrollBar bar = scroll.VerticalScrollBar;
			await Assert.That(bar.Floating).IsFalse();
			await Assert.That(bar.Width).IsEqualTo(ScrollBar.ScrollBarWidth);
			await Assert.That(bar.Height).IsEqualTo(100);
			await Assert.That(bar.Thumb.LocalBounds.Left).IsEqualTo(ScrollBar.GrowThumbBy);
			await Assert.That(bar.Thumb.Height).IsEqualTo(100 * 100.0 / 500).Within(1e-9);
			await Assert.That(scroll.ScrollArea.DeviceMargin.Right).IsEqualTo(ScrollBar.ScrollBarWidth).Within(1e-9);
			await Assert.That(bar.Opacity).IsEqualTo(1);
			await Assert.That(scroll.Children.Count).IsEqualTo(2);
		}

		[Test]
		public async Task WidthAndMarginsWidenTheStripAndReserveIt()
		{
			ScrollableWidget scroll = Make();
			ScrollBar bar = scroll.VerticalScrollBar;
			bar.BarWidth = 20;
			bar.InnerMargin = 4;
			bar.OuterMargin = 3;
			await Assert.That(bar.Width).IsEqualTo(27);
			await Assert.That(scroll.ScrollArea.DeviceMargin.Right).IsEqualTo(27).Within(1e-9);

			// the track sits past the inner margin; the outer margin is left clear at the view's edge
			await Assert.That(bar.Track.LocalBounds.Left).IsEqualTo(4);
			await Assert.That(bar.Track.LocalBounds.Right).IsEqualTo(24);
			await Assert.That(bar.Thumb.LocalBounds.Left).IsEqualTo(4 + ScrollBar.GrowThumbBy);
			await Assert.That(bar.Thumb.LocalBounds.Right).IsEqualTo(24 - ScrollBar.GrowThumbBy);
		}

		[Test]
		public async Task ANarrowBarKeepsAVisibleThumb()
		{
			ScrollableWidget scroll = Make();
			ScrollBar bar = scroll.VerticalScrollBar;
			// the legacy inset on both sides would eat a bar this thin whole
			bar.BarWidth = 2 * ScrollBar.GrowThumbBy;
			await Assert.That(bar.Thumb.Width).IsEqualTo(ScrollBar.GrowThumbBy).Within(1e-9);
		}

		[Test]
		public async Task HandleMinLengthHoldsTheThumbAndTheDragStillReachesTheEnd()
		{
			ScrollableWidget scroll = Make(contentHeight: 10000);
			ScrollBar bar = scroll.VerticalScrollBar;
			await Assert.That(bar.Thumb.Height).IsEqualTo(1).Within(1e-9);
			bar.HandleMinLength = 30;
			await Assert.That(bar.Thumb.Height).IsEqualTo(30);
			bar.MoveThumb(new Vector2(0, -1000));
			await Assert.That(scroll.ScrollOffsetFromTop()).IsEqualTo(scroll.MaxScrollFromTop()).Within(1e-6);
		}

		[Test]
		public async Task AFloatingBarTakesNoRoomAndIsThinUntilHovered()
		{
			ScrollableWidget scroll = Make();
			ScrollBar bar = scroll.VerticalScrollBar;
			bar.BarWidth = 10;
			bar.FloatingWidth = 2;
			bar.Floating = true;
			await Assert.That(scroll.ScrollArea.DeviceMargin.Right).IsEqualTo(0);
			await Assert.That(bar.Visible).IsTrue();

			// dormant: a thin thumb against the outer edge and no track
			await Assert.That(bar.Thumb.LocalBounds.Left).IsEqualTo(8);
			await Assert.That(bar.Thumb.LocalBounds.Right).IsEqualTo(10);
			await Assert.That(bar.Track.BackgroundColor.alpha).IsEqualTo((byte)0);

			bar.OnMouseEnterBounds(Mouse(5, 50));
			await Assert.That(bar.Thumb.LocalBounds.Left).IsEqualTo(0);
			await Assert.That(bar.Thumb.LocalBounds.Right).IsEqualTo(10);

			bar.Floating = false;
			await Assert.That(scroll.ScrollArea.DeviceMargin.Right).IsEqualTo(10).Within(1e-9);
		}

		[Test]
		public async Task AFloatingBarAutoHidesAndFadesInOnHoverAndScroll()
		{
			ScrollableWidget scroll = Make();
			ScrollBar bar = scroll.VerticalScrollBar;
			bar.Floating = true;
			await Assert.That(bar.Opacity).IsEqualTo(0);
			long now = UiThread.CurrentTimerMs + 100_000;
			await Assert.That(bar.FadeTarget(now)).IsEqualTo(0);

			// hover fades it in over FadeMs
			bar.OnMouseEnterBounds(Mouse(5, 50));
			await Assert.That(bar.FadeTarget(now)).IsEqualTo(1);
			bar.StepFade(now);
			bar.StepFade(now + (long)(ScrollBar.FadeMs / 2));
			await Assert.That(bar.Opacity).IsEqualTo(0.5).Within(1e-9);
			await Assert.That(bar.StepFade(now + (long)ScrollBar.FadeMs)).IsFalse();
			await Assert.That(bar.Opacity).IsEqualTo(1);

			// leaving fades it back out
			bar.OnMouseLeaveBounds(Mouse(500, 50));
			await Assert.That(bar.FadeTarget(now)).IsEqualTo(0);

			// a scroll brings it up for a moment
			scroll.SetScrollOffsetFromTop(50);
			await Assert.That(bar.FadeTarget(UiThread.CurrentTimerMs)).IsEqualTo(1);
			await Assert.That(bar.FadeTarget(UiThread.CurrentTimerMs + 5000)).IsEqualTo(0);

			// shown Always, a floating bar does not hide
			bar.Show = ScrollBar.ShowState.Always;
			await Assert.That(bar.FadeTarget(now)).IsEqualTo(1);
		}

		[Test]
		public async Task ColorOverridesPaintTheBar()
		{
			ScrollableWidget scroll = Make();
			ScrollBar bar = scroll.VerticalScrollBar;
			bar.TrackColor = Color.Red;
			bar.ThumbColor = Color.Green;
			bar.ThumbHoverColor = Color.Blue;
			await Assert.That(bar.Track.BackgroundColor).IsEqualTo(Color.Red);
			await Assert.That(bar.Thumb.BackgroundColor).IsEqualTo(Color.Green);
			bar.OnMouseEnterBounds(Mouse(5, 50));
			await Assert.That(bar.Thumb.BackgroundColor).IsEqualTo(Color.Blue);
		}

		[Test]
		public async Task TheVerticalBarStopsAboveTheHorizontalBar()
		{
			ScrollableWidget scroll = Make(contentWidth: 600, contentHeight: 500);
			scroll.HorizontalScroll = true;
			ScrollBar vertical = scroll.VerticalScrollBar;
			double hbar = scroll.HorizontalScrollBar.Height;
			await Assert.That(scroll.HorizontalScrollBar.Visible).IsTrue();
			await Assert.That(vertical.LocalBounds.Bottom).IsEqualTo(hbar);
			await Assert.That(vertical.VerticalTrackLength).IsEqualTo(100 - hbar);
			await Assert.That(vertical.ThumbHeight).IsEqualTo((100 - hbar) * scroll.RatioOfViewToContents0To1().Y).Within(1e-9);

			// at either end the thumb meets the end of the track, not the view's bottom
			vertical.MoveThumb(new Vector2(0, -1000));
			await Assert.That(vertical.Thumb.OriginRelativeParent.Y).IsEqualTo(hbar).Within(1e-6);
			vertical.MoveThumb(new Vector2(0, 1000));
			await Assert.That(vertical.Thumb.OriginRelativeParent.Y + vertical.Thumb.Height).IsEqualTo(100).Within(1e-6);
		}

		[Test]
		public async Task TheEdgeFadeIsOptInAndFollowsTheScroll()
		{
			ScrollableWidget scroll = Make();
			await Assert.That(scroll.Children.Count).IsEqualTo(2);
			ScrollEdgeFade fade = scroll.EdgeFade;

			// between the content and the bars
			await Assert.That(scroll.Children.IndexOf(fade)).IsEqualTo(scroll.Children.IndexOf(scroll.ScrollArea) + 1);
			await Assert.That(scroll.Children.IndexOf(fade)).IsLessThan(scroll.Children.IndexOf(scroll.VerticalScrollBar));
			await Assert.That(string.Join(",", fade.FadedEdges())).IsEqualTo(string.Empty);

			fade.Strength = 0.5;
			await Assert.That(string.Join(",", fade.FadedEdges())).IsEqualTo("Bottom");
			scroll.SetScrollOffsetFromTop(50);
			await Assert.That(string.Join(",", fade.FadedEdges())).IsEqualTo("Top,Bottom");
			scroll.SetScrollOffsetFromTop(scroll.MaxScrollFromTop());
			await Assert.That(string.Join(",", fade.FadedEdges())).IsEqualTo("Top");

			fade.Color = Color.Cyan;
			await Assert.That(fade.ResolvedColor()).IsEqualTo(Color.Cyan);
			fade.Color = null;
			scroll.Parent.BackgroundColor = Color.Orange;
			await Assert.That(fade.ResolvedColor()).IsEqualTo(Color.Orange);
		}
	}
}
