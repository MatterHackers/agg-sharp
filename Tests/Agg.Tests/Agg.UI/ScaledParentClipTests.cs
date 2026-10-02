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

using System.Collections.Generic;
using MatterHackers.Agg.Image;
using System.Threading.Tasks;
using MatterHackers.Agg.Transform;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// A widget whose <see cref="GuiWidget.ParentToChildTransform"/> scales its children - a zoomable canvas such
	/// as a node editor's scroll area - has to be clipped, hit, invalidated and searched where it is drawn. The
	/// visibility walks used to move a rectangle into its parent by the transform's translation alone, so a
	/// scaled child was judged against where it would be at full size: a visible one became unclickable, and a
	/// clipped-off one stayed clickable.
	/// </summary>
	public class ScaledParentClipTests
	{
		/// <summary>
		/// A 400 x 300 window holding a canvas that draws its children at <paramref name="scale"/>, shifted by
		/// <paramref name="translation"/>, with <paramref name="child"/> on the canvas.
		/// </summary>
		private static (SystemWindow window, GuiWidget container, GuiWidget canvas) BuildTree(double scale, Vector2 translation, RectangleDouble canvasBounds, GuiWidget child)
		{
			var window = new SystemWindow(400, 300);

			var container = new GuiWidget(400, 300)
			{
				Name = "container",
			};
			window.AddChild(container);

			var canvas = new GuiWidget
			{
				Name = "canvas",
			};
			container.AddChild(canvas);
			canvas.LocalBounds = canvasBounds;
			canvas.ParentToChildTransform = Affine.NewScaling(scale) * Affine.NewTranslation(translation);

			if (child != null)
			{
				canvas.AddChild(child);
			}

			return (window, container, canvas);
		}

		/// <summary>
		/// A half-scale canvas, and a child near its top that is drawn at (20, 190) to (40, 200) in the container.
		/// At full size the same child would sit at y 380 to 400, past the container's 300 - the case that made a
		/// visible child unclickable.
		/// </summary>
		private static (SystemWindow window, GuiWidget container, GuiWidget canvas, GuiWidget child) HalfScaleTreeWithAVisibleChild()
		{
			var child = new GuiWidget(40, 20)
			{
				Name = "target",
				Position = new Vector2(0, 360),
			};

			var (window, container, canvas) = BuildTree(0.5, new Vector2(20, 10), new RectangleDouble(0, 0, 600, 400), child);
			return (window, container, canvas, child);
		}

		/// <summary>
		/// A double-scale canvas, and a child drawn at y 320 to 360 in the container - above its 300 - which at
		/// full size would have looked inside.
		/// </summary>
		private static (SystemWindow window, GuiWidget child) DoubleScaleTreeWithAHiddenChild()
		{
			var child = new GuiWidget(40, 20)
			{
				Name = "target",
				Position = new Vector2(0, 160),
			};

			var (window, _, _) = BuildTree(2, Vector2.Zero, new RectangleDouble(0, 0, 200, 200), child);
			return (window, child);
		}

		[Test]
		public async Task AChildDrawnInsideAShrunkCanvasCanBeSelectedAndClicked()
		{
			var (window, _, _, child) = HalfScaleTreeWithAVisibleChild();

			await Assert.That(child.CanSelect).IsTrue()
				.Because("the child is drawn inside the container, so it has to take the mouse");

			var drawn = child.TransformToParentSpace(window, child.LocalBounds);
			await Assert.That(drawn.Left).IsEqualTo(20).Within(1e-9);
			await Assert.That(drawn.Bottom).IsEqualTo(190).Within(1e-9);

			int downs = 0;
			child.MouseDown += (s, e) => downs++;
			window.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 30, 195, 0));
			window.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 30, 195, 0));

			await Assert.That(downs).IsEqualTo(1)
				.Because("a press on the child where it is drawn has to reach it");
		}

		[Test]
		public async Task AChildDrawnOutsideAnEnlargedCanvasStaysUnselectable()
		{
			var (window, child) = DoubleScaleTreeWithAHiddenChild();

			await Assert.That(child.CanSelect).IsFalse()
				.Because("scaled up, the child is drawn above the container and cannot be seen");
			await Assert.That(child.ActuallyVisibleOnScreen()).IsFalse();
		}

		/// <summary>
		/// ActuallyVisibleOnParent looks one level up, so the scaled widget it has to judge is the canvas itself
		/// against the container.
		/// </summary>
		[Test]
		public async Task AShrunkCanvasIsVisibleOnItsParentWhereItIsDrawn()
		{
			// drawn at x 250 to 300 of the 400-wide container; at full size it would be at 500 to 600
			var (_, _, canvas) = BuildTree(0.5, Vector2.Zero, new RectangleDouble(500, 0, 600, 100), null);

			await Assert.That(canvas.ActuallyVisibleOnParent()).IsTrue();
		}

		[Test]
		public async Task AnEnlargedCanvasDrawnPastItsParentIsNotVisibleOnIt()
		{
			// drawn at x 500 to 600, past the 400-wide container; at full size it would be at 250 to 300
			var (_, _, canvas) = BuildTree(2, Vector2.Zero, new RectangleDouble(250, 0, 300, 50), null);

			await Assert.That(canvas.ActuallyVisibleOnParent()).IsFalse();
		}

		[Test]
		public async Task OnScreenBoundsOfAScaledChildAreWhereItIsDrawn()
		{
			var (window, _, _, child) = HalfScaleTreeWithAVisibleChild();

			await Assert.That(child.ActuallyVisibleOnScreen()).IsTrue();

			var clipped = child.ClippedOnScreenBounds();
			await Assert.That(clipped).IsEqualTo(new RectangleDouble(20, 190, 40, 200));
		}

		[Test]
		public async Task InvalidatingAScaledChildInvalidatesWhereItIsDrawn()
		{
			var (_, container, _, child) = HalfScaleTreeWithAVisibleChild();

			var invalidated = new List<RectangleDouble>();
			container.Invalidated += (s, e) => invalidated.Add(((InvalidateEventArgs)e).InvalidRectangle);

			child.Invalidate();

			await Assert.That(invalidated.Count).IsEqualTo(1)
				.Because("the child is visible, so its redraw has to reach the container");
			await Assert.That(invalidated[0]).IsEqualTo(new RectangleDouble(20, 190, 40, 200));
		}

		[Test]
		public async Task ASearchRegionFindsAScaledChildWhereItIsDrawn()
		{
			var (window, _, _, _) = HalfScaleTreeWithAVisibleChild();

			var found = window.FindDescendants(
				new[] { "target" },
				new List<GuiWidget.WidgetAndPosition>(),
				new RectangleDouble(25, 192, 35, 198),
				GuiWidget.SearchType.Exact);

			await Assert.That(found.Count).IsEqualTo(1)
				.Because("the region covers the middle of the child as it is drawn");

			var missed = window.FindDescendants(
				new[] { "target" },
				new List<GuiWidget.WidgetAndPosition>(),
				new RectangleDouble(45, 375, 55, 385),
				GuiWidget.SearchType.Exact);

			await Assert.That(missed.Count).IsEqualTo(0)
				.Because("that region is where the child would be at full size, not where it is drawn");
		}

		/// <summary>
		/// FindDescendants narrows the search region to each widget whose name matches, so a region beside a
		/// matching widget arrives at its children inverted (Left past Right). Moved through a child's translation
		/// it has to stay inverted - rebuilt as a valid rectangle it would find a child overflowing its parent
		/// that the region never touched.
		/// </summary>
		[Test]
		public async Task AnEmptySearchRegionStaysEmptyThroughATranslatedChild()
		{
			var window = new SystemWindow(400, 300);
			var container = new GuiWidget(50, 300)
			{
				Name = "container",
			};
			window.AddChild(container);
			container.AddChild(new GuiWidget(20, 20)
			{
				Name = "target",
				Position = new Vector2(60, 0),
			});

			var found = window.FindDescendants(
				new[] { "container", "target" },
				new List<GuiWidget.WidgetAndPosition>(),
				new RectangleDouble(100, 5, 101, 15),
				GuiWidget.SearchType.Exact);

			await Assert.That(found.Count).IsEqualTo(0)
				.Because("the region lies past both the container and the child it holds at x 60 to 80");
		}

		/// <summary>
		/// An empty rectangle invalidated on a translated child must not reach the parent as a valid one - rebuilt
		/// with Min/Max, <see cref="RectangleDouble.ZeroIntersection"/> becomes nearly unbounded and would redraw
		/// everything.
		/// </summary>
		[Test]
		public async Task InvalidatingAnEmptyRectangleOnATranslatedChildInvalidatesNothing()
		{
			var container = new GuiWidget(400, 300);
			var child = new GuiWidget(40, 20)
			{
				Position = new Vector2(10, 10),
			};
			container.AddChild(child);

			var invalidated = new List<RectangleDouble>();
			container.Invalidated += (s, e) => invalidated.Add(((InvalidateEventArgs)e).InvalidRectangle);

			child.Invalidate(RectangleDouble.ZeroIntersection);

			await Assert.That(invalidated.Count).IsEqualTo(0);
		}

		/// <summary>
		/// A flipped canvas (negative scale) maps its children mirrored, and an empty rectangle through it stays
		/// empty.
		/// </summary>
		[Test]
		public async Task AFlippedCanvasInvalidatesSelectsAndFindsWhereItsChildIsDrawn()
		{
			var child = new GuiWidget(40, 20)
			{
				Name = "target",
			};
			var (window, container, canvas) = BuildTree(1, Vector2.Zero, new RectangleDouble(-200, 0, 200, 300), child);
			canvas.ParentToChildTransform = Affine.NewScaling(-1, 1) * Affine.NewTranslation(200, 0);

			var invalidated = new List<RectangleDouble>();
			canvas.Invalidated += (s, e) => invalidated.Add(((InvalidateEventArgs)e).InvalidRectangle);
			child.Invalidate();

			await Assert.That(invalidated.Count).IsEqualTo(1);
			await Assert.That(invalidated[0]).IsEqualTo(new RectangleDouble(160, 0, 200, 20))
				.Because("x 0 to 40 mirrored is -40 to 0, then moved right by 200");

			await Assert.That(child.CanSelect).IsTrue();

			var found = window.FindDescendants(
				new[] { "target" },
				new List<GuiWidget.WidgetAndPosition>(),
				new RectangleDouble(170, 5, 190, 15),
				GuiWidget.SearchType.Exact);
			await Assert.That(found.Count).IsEqualTo(1);

			var containerInvalidated = 0;
			container.Invalidated += (s, e) => containerInvalidated++;
			canvas.Invalidate(RectangleDouble.ZeroIntersection);

			await Assert.That(containerInvalidated).IsEqualTo(0)
				.Because("an empty rectangle stays empty when mirrored");
		}

		/// <summary>
		/// A canvas that rotates its children maps a rectangle by its four corners.
		/// </summary>
		[Test]
		public async Task ARotatedCanvasInvalidatesAndFindsWhereItsChildIsDrawn()
		{
			var child = new GuiWidget(40, 20)
			{
				Name = "target",
			};
			var (window, _, canvas) = BuildTree(1, Vector2.Zero, new RectangleDouble(0, -300, 400, 300), child);
			// a quarter turn counter-clockwise, (x, y) -> (-y, x), then moved to (200, 100)
			canvas.ParentToChildTransform = Affine.NewRotation(System.Math.PI / 2) * Affine.NewTranslation(200, 100);

			var invalidated = new List<RectangleDouble>();
			canvas.Invalidated += (s, e) => invalidated.Add(((InvalidateEventArgs)e).InvalidRectangle);
			child.Invalidate();

			await Assert.That(invalidated.Count).IsEqualTo(1);
			await Assert.That(invalidated[0].Left).IsEqualTo(180).Within(1e-9);
			await Assert.That(invalidated[0].Bottom).IsEqualTo(100).Within(1e-9);
			await Assert.That(invalidated[0].Right).IsEqualTo(200).Within(1e-9);
			await Assert.That(invalidated[0].Top).IsEqualTo(140).Within(1e-9);

			var found = window.FindDescendants(
				new[] { "target" },
				new List<GuiWidget.WidgetAndPosition>(),
				new RectangleDouble(185, 110, 195, 130),
				GuiWidget.SearchType.Exact);
			await Assert.That(found.Count).IsEqualTo(1);

			var missed = window.FindDescendants(
				new[] { "target" },
				new List<GuiWidget.WidgetAndPosition>(),
				new RectangleDouble(205, 105, 235, 115),
				GuiWidget.SearchType.Exact);
			await Assert.That(missed.Count).IsEqualTo(0)
				.Because("that is where the child would be drawn unrotated");
		}

		/// <summary>
		/// A search by name starts from an unbounded region (double.MinValue to double.MaxValue). A shrinking canvas
		/// scales that to infinity on the way down, and a rotated or sheared widget below it must not turn
		/// infinity into NaN and lose everything beneath it. A shear leaves one term of the matrix exactly zero, so
		/// every corner's infinity times zero is NaN; a general rotation only loses some corners to NaN, but it
		/// must find the child too.
		/// </summary>
		[Test]
		[Arguments(true)]
		[Arguments(false)]
		public async Task ASearchByNameFindsAChildOfARotatedWidgetUnderAShrunkCanvas(bool sheared)
		{
			var turned = new GuiWidget(100, 100)
			{
				Name = "turned",
			};
			var (window, _, _) = BuildTree(0.5, Vector2.Zero, new RectangleDouble(0, 0, 600, 400), turned);
			turned.ParentToChildTransform = (sheared ? Affine.NewSkewing(0.5, 0) : Affine.NewRotation(System.Math.PI / 6))
				* Affine.NewTranslation(100, 100);
			turned.AddChild(new GuiWidget(20, 20)
			{
				Name = "target",
			});

			await Assert.That(window.FindDescendants("target").Count).IsEqualTo(1);
		}

		public static IEnumerable<(string Name, Affine CanvasTransform, double DrawnArea)> DrawnCanvases()
		{
			yield return ("doubled", Affine.NewScaling(2) * Affine.NewTranslation(5, 5), 1600);
			yield return ("halved", Affine.NewScaling(0.5) * Affine.NewTranslation(5, 5), 100);
			yield return ("flipped", Affine.NewScaling(-1, 1) * Affine.NewTranslation(100, 50), 400);
			yield return ("quarter turned", Affine.NewRotation(System.Math.PI / 2) * Affine.NewTranslation(100, 50), 400);
			yield return ("turned 30 degrees", Affine.NewRotation(System.Math.PI / 6) * Affine.NewTranslation(100, 50), 400);
		}

		/// <summary>
		/// A child of a scaled, flipped or turned canvas is drawn whole: its clip is where the canvas draws it.
		/// The clip already carries every ancestor's transform, so carrying it through the graphics transform as
		/// well applied the canvas's scale twice - a doubled child was cut to a quarter of itself.
		/// </summary>
		[Test]
		[MethodDataSource(nameof(DrawnCanvases))]
		public async Task AChildOfATransformedCanvasIsDrawnWhole(string name, Affine canvasTransform, double drawnArea)
		{
			var root = new GuiWidget(200, 200);
			var canvas = new GuiWidget();
			root.AddChild(canvas);
			canvas.LocalBounds = new RectangleDouble(0, 0, 200, 200);
			canvas.ParentToChildTransform = canvasTransform;
			canvas.AddChild(new GuiWidget(20, 20)
			{
				BackgroundColor = Color.Red,
				Position = new Vector2(10, 10),
			});

			var image = new ImageBuffer(200, 200);
			var graphics = image.NewGraphics2D();
			graphics.Clear(Color.White);
			root.OnDraw(graphics);

			int red = 0;
			for (int y = 0; y < image.Height; y++)
			{
				for (int x = 0; x < image.Width; x++)
				{
					var pixel = image.GetPixel(x, y);
					red += pixel.red > 128 && pixel.green < 128 ? 1 : 0;
				}
			}

			await Assert.That((double)red).IsEqualTo(drawnArea).Within(drawnArea * 0.1)
				.Because($"the {name} canvas draws its 20 x 20 child over {drawnArea} pixels");
		}
	}
}
