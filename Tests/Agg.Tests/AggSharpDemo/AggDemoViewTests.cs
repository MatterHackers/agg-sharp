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

using System.Collections.Generic;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.AggSharpDemo;
using MatterHackers.AggSharpDemo.Demos;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	// The demo view's placement (whole-number scale, centered on whole screen pixels), the input mapping into
	// C++-style whole demo pixels, and both render modes landing the demo exactly where the layout says.
	public class AggDemoViewTests
	{
		/// <summary>Records the input it is handed and how often it is drawn, and draws nothing.</summary>
		private class RecordingDemo : AggDemo
		{
			public List<string> Events { get; } = new List<string>();

			public bool YDown { get; set; }

			public override bool DrawsYDown => this.YDown;

			public int DrawCount { get; private set; }

			public override string Name => "recording";

			public override string Category => "Test";

			public override string Description => "Records input.";

			public override int Width => 400;

			public override int Height => 300;

			public override void Draw(Graphics2D graphics) => this.DrawCount++;

			public override void OnMouseDown(int x, int y, AggInputFlags button, AggInputFlags flags) => this.Events.Add($"down {x},{y} {button} [{flags}]");

			public override void OnMouseMove(int x, int y, AggInputFlags flags) => this.Events.Add($"move {x},{y} {flags}");

			public override void OnMouseUp(int x, int y, AggInputFlags button, AggInputFlags flags) => this.Events.Add($"up {x},{y} {button} [{flags}]");

			public override void OnKeyDown(Keys key, AggInputFlags flags) => this.Events.Add($"key {key} {flags}");
		}

		[Test]
		public async Task PicksTheLargestWholeScaleThatFitsAndCentersOnWholePixels()
		{
			// 900 / 400 = 2.25 and 700 / 300 = 2.33: whole scale 2, leaving 100 x 100 to split.
			var layout = new AggDemoLayout(900, 700, 400, 300);

			await Assert.That(layout.Scale).IsEqualTo(2);
			await Assert.That(layout.Offset).IsEqualTo(new Vector2(50, 50));

			// An odd leftover rounds down rather than landing the demo between pixels.
			var odd = new AggDemoLayout(401, 301, 400, 300);
			await Assert.That(odd.Scale).IsEqualTo(1);
			await Assert.That(odd.Offset).IsEqualTo(new Vector2(0, 0));
		}

		[Test]
		public async Task AViewSmallerThanTheDemoShowsItAtScaleOneCentered()
		{
			var layout = new AggDemoLayout(200, 100, 400, 300);

			await Assert.That(layout.Scale).IsEqualTo(1);
			await Assert.That(layout.Offset).IsEqualTo(new Vector2(-100, -100));
		}

		[Test]
		public async Task AViewAtAFractionalScreenPositionStillPlacesTheDemoOnWholeScreenPixels()
		{
			// What a display scale of 1.25 or 1.5 does to a view's origin.
			var origin = new Vector2(10.5, 20.25);
			var layout = new AggDemoLayout(900, 700, 400, 300, origin);

			Vector2 onScreen = origin + layout.Offset;
			await Assert.That(onScreen.X).IsEqualTo(System.Math.Floor(onScreen.X));
			await Assert.That(onScreen.Y).IsEqualTo(System.Math.Floor(onScreen.Y));

			// And still centered, to within the pixel the fraction costs.
			await Assert.That(System.Math.Abs(layout.Offset.X - 50)).IsLessThan(1);
			await Assert.That(System.Math.Abs(layout.Offset.Y - 50)).IsLessThan(1);
		}

		[Test]
		public async Task MouseReachesTheDemoAsWholeDemoPixelsYUpLikeCpp()
		{
			var demo = new RecordingDemo();
			var view = new AggDemoView(demo) { Width = 900, Height = 700 };

			// View (50, 50) is the demo's bottom-left corner at scale 2; (849, 649) is inside its last pixel.
			view.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 51, 51, 0));
			view.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 0, 849, 649, 0));

			// A drag that began inside keeps reporting, with the held button, once it leaves.
			view.OnMouseMove(new MouseEventArgs(MouseButtons.None, 0, 870, 660, 0));
			view.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 870, 660, 0));

			// (850, 650) is the demo's top-right edge: pixel (400, 300), one past the last.
			view.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 850, 650, 0));
			view.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 850, 650, 0));

			// A press in the margin, its release, and a hover there are not the demo's.
			view.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 20, 20, 0));
			view.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 100, 100, 0));
			view.OnMouseMove(new MouseEventArgs(MouseButtons.None, 0, 20, 20, 0));

			await Assert.That(string.Join("; ", demo.Events)).IsEqualTo(
				"down 0,0 MouseLeft [MouseLeft]; move 399,299 MouseLeft; move 410,305 MouseLeft; up 410,305 MouseLeft [None]");

			// DemoToView and ViewToDemo are inverses.
			Vector2 demoPoint = new Vector2(123, 45);
			double x = demoPoint.X, y = demoPoint.Y;
			view.Layout.DemoToView.Transform(ref x, ref y);
			await Assert.That(view.Layout.ViewToDemo(new Vector2(x, y))).IsEqualTo(demoPoint);
		}

		/// <summary>A y-down demo gets the row counted from the top, as C++ flip_y = false reports it.</summary>
		[Test]
		public async Task MouseReachesAYDownDemoWithRowsCountedFromTheTop()
		{
			var demo = new RecordingDemo { YDown = true };
			var view = new AggDemoView(demo) { Width = 900, Height = 700 };

			// View (51, 51) is in the demo's bottom-left pixel, (849, 649) in its top-right one.
			view.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 51, 51, 0));
			view.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 0, 849, 649, 0));
			view.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 849, 649, 0));

			// The margin is still not the demo's.
			view.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 20, 20, 0));

			await Assert.That(string.Join("; ", demo.Events)).IsEqualTo(
				"down 0,299 MouseLeft [MouseLeft]; move 399,0 MouseLeft; up 399,0 MouseLeft [None]");
		}

		/// <summary>A y-down demo's top band (its rows 0 to 9) shows at the top of its frame in both modes,
		/// at scale 1 and 2.</summary>
		[Test]
		[Arguments(AggDemoRenderMode.Gpu, 300, 300)]
		[Arguments(AggDemoRenderMode.Software, 300, 300)]
		[Arguments(AggDemoRenderMode.Gpu, 700, 700)]
		[Arguments(AggDemoRenderMode.Software, 700, 700)]
		public async Task AYDownDemoIsShownTheRightWayUp(AggDemoRenderMode mode, int viewWidth, int viewHeight)
		{
			var demo = new TopBandDemo();
			var view = new AggDemoView(demo) { Width = viewWidth, Height = viewHeight, RenderMode = mode };
			var screen = new ImageBuffer(viewWidth, viewHeight);
			view.OnDraw(screen.NewGraphics2D());

			AggDemoLayout layout = view.Layout;
			int left = (int)layout.Offset.X;
			int bottom = (int)layout.Offset.Y;
			int top = bottom + (demo.Height * layout.Scale);
			await Assert.That(screen.GetPixel(left + 5, top - 1)).IsEqualTo(Color.Red);
			await Assert.That(screen.GetPixel(left + 5, top - (10 * layout.Scale))).IsEqualTo(Color.Red);
			await Assert.That(screen.GetPixel(left + 5, top - (10 * layout.Scale) - 1)).IsEqualTo(Color.White);
			await Assert.That(screen.GetPixel(left + 5, bottom)).IsEqualTo(Color.White);
		}

		/// <summary>The software mode shows a y-down reference frame row-reversed, pixel for pixel.</summary>
		[Test]
		public async Task SoftwareModeShowsAYDownReferenceRenderFlippedPixelForPixel()
		{
			// 600 x 660 shows the 250 x 280 idea demo at scale 2, offset (50, 50).
			var demo = new IdeaDemo();
			var view = new AggDemoView(demo) { Width = 600, Height = 660, RenderMode = AggDemoRenderMode.Software };

			await Assert.That(await CountMismatches(demo, view, new Vector2(10.5, 20.25))).IsEqualTo(0);
		}

		[Test]
		public async Task APressOrReleaseNamesItsOwnButtonApartFromTheOnesHeld()
		{
			var demo = new RecordingDemo();
			var view = new AggDemoView(demo) { Width = 900, Height = 700 };

			view.OnMouseDown(new MouseEventArgs(MouseButtons.Right, 1, 101, 101, 0));
			view.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 101, 101, 0));
			view.OnMouseUp(new MouseEventArgs(MouseButtons.Right, 1, 101, 101, 0));
			view.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 101, 101, 0));

			await Assert.That(string.Join("; ", demo.Events)).IsEqualTo(
				"down 25,25 MouseRight [MouseRight]; down 25,25 MouseLeft [MouseLeft, MouseRight]; up 25,25 MouseRight [MouseLeft]; up 25,25 MouseLeft [None]");
		}

		[Test]
		public async Task KeysReachTheDemoWithTheirModifiers()
		{
			var demo = new RecordingDemo();
			var view = new AggDemoView(demo) { Width = 900, Height = 700 };

			view.OnKeyDown(new KeyEventArgs(Keys.A | Keys.Shift | Keys.Control));

			await Assert.That(string.Join("; ", demo.Events)).IsEqualTo("key A Shift, Ctrl");
		}

		[Test]
		public async Task ADemoEntirelyOutsideTheClipIsNotDrawn()
		{
			var demo = new RecordingDemo();
			var view = new AggDemoView(demo) { Width = 900, Height = 700 };
			Graphics2D graphics = new ImageBuffer(900, 700).NewGraphics2D();

			// The demo spans (50, 50)-(850, 650); this clip is the margin below and left of it.
			graphics.SetClippingRect(new RectangleDouble(0, 0, 40, 40));
			view.OnDraw(graphics);
			await Assert.That(demo.DrawCount).IsEqualTo(0);

			graphics.SetClippingRect(new RectangleDouble(0, 0, 900, 700));
			view.OnDraw(graphics);
			await Assert.That(demo.DrawCount).IsEqualTo(1);
		}

		[Test]
		public async Task GpuModeFillsExactlyTheDemosPixelsAtAFractionalScreenPosition()
		{
			var demo = new LionDemo();
			var view = new AggDemoView(demo) { Width = 900, Height = 700 };
			var screen = new ImageBuffer(920, 730);
			Graphics2D graphics = screen.NewGraphics2D();
			graphics.SetTransform(Affine.NewTranslation(10.5, 20.25));

			view.OnDraw(graphics);

			// The lion demo paints its whole frame white, and draws nothing in these corners; the screen starts transparent black.
			AggDemoLayout layout = view.Layout;
			int left = (int)(10.5 + layout.Offset.X);
			int bottom = (int)(20.25 + layout.Offset.Y);
			int right = left + demo.Width * layout.Scale;
			int top = bottom + demo.Height * layout.Scale;
			await Assert.That(screen.GetPixel(left, bottom + 1)).IsEqualTo(Color.White);
			await Assert.That(screen.GetPixel(right - 1, top - 1)).IsEqualTo(Color.White);
			await Assert.That(screen.GetPixel(left - 1, bottom + 1).alpha).IsEqualTo((byte)0);
			await Assert.That(screen.GetPixel(right, top - 1).alpha).IsEqualTo((byte)0);
			await Assert.That(screen.GetPixel(right - 1, top).alpha).IsEqualTo((byte)0);
		}

		[Test]
		public async Task SoftwareModeShowsTheReferenceRenderPixelForPixelAtAFractionalScreenPosition()
		{
			// 1100 x 860 shows the 512 x 400 lion at scale 2, offset (38, 30).
			var demo = new LionDemo();
			var view = new AggDemoView(demo) { Width = 1100, Height = 860, RenderMode = AggDemoRenderMode.Software };
			var origin = new Vector2(10.5, 20.25);

			await Assert.That(await CountMismatches(demo, view, origin)).IsEqualTo(0);

			// Drag the lion round: the presented frame is refilled in place, and must still match.
			view.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 10 + 38 + 400, 20 + 30 + 400, 0));
			view.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 0, 10 + 38 + 600, 20 + 30 + 500, 0));
			view.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 10 + 38 + 600, 20 + 30 + 500, 0));

			await Assert.That(await CountMismatches(demo, view, origin)).IsEqualTo(0);
		}

		/// <summary>A y-down demo that paints white with a red band along its top ten rows (y down 0 to 10).</summary>
		private class TopBandDemo : AggDemo
		{
			public override string Name => "top band";

			public override string Category => "Test";

			public override string Description => "A red band at the top.";

			public override int Width => 200;

			public override int Height => 100;

			public override bool DrawsYDown => true;

			public override void Draw(Graphics2D graphics)
			{
				graphics.FillRectangle(0, 0, this.Width, this.Height, Color.White);
				graphics.FillRectangle(0, 0, this.Width, 10, Color.Red);
			}
		}

		/// <summary>Draws the view with its origin at <paramref name="origin"/> on a fresh screen and counts the
		/// screen pixels that differ from a fresh reference render of the demo, replicated by the scale.</summary>
		private static async Task<int> CountMismatches(AggDemo demo, AggDemoView view, Vector2 origin)
		{
			ImageBuffer reference = AggDemoView.NewReferenceFrame(demo);
			AggDemoView.DrawReferenceFrame(demo, reference);

			var screen = new ImageBuffer(1120, 890);
			Graphics2D graphics = screen.NewGraphics2D();
			graphics.SetTransform(Affine.NewTranslation(origin));
			view.OnDraw(graphics);

			AggDemoLayout layout = view.Layout;
			await Assert.That(layout.Scale).IsEqualTo(2);

			int left = (int)(origin.X + layout.Offset.X);
			int bottom = (int)(origin.Y + layout.Offset.Y);
			int mismatches = 0;
			for (int y = 0; y < demo.Height * layout.Scale; y++)
			{
				// A y-down frame's row 0 is the top of what is shown.
				int referenceY = demo.DrawsYDown ? demo.Height - 1 - (y / layout.Scale) : y / layout.Scale;
				for (int x = 0; x < demo.Width * layout.Scale; x++)
				{
					if (reference.GetPixel(x / layout.Scale, referenceY) != screen.GetPixel(left + x, bottom + y))
					{
						mismatches++;
					}
				}
			}

			return mismatches;
		}
	}
}
