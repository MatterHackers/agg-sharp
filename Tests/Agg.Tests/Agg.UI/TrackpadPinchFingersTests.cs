/*
Copyright (c) 2026, Lars Brubaker
All rights reserved.
*/

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Enums;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// A trackpad's pinch and rotate (mac NSEventMagnify / NSEventRotate) become two virtual fingers about the
	/// pointer, which <see cref="MultiTouchGesture"/> reads back as the zoom and turn the trackpad reported, and
	/// the wheel a pinch also sends is marked so pinch-aware widgets can leave it to the fingers.
	/// </summary>
	public class TrackpadPinchFingersTests
	{
		private static readonly Vector2 Pointer = new Vector2(200, 150);

		/// <summary>Feeds every frame through a recogniser, as a widget receiving the moves would.</summary>
		private static MultiTouchInfo? Feed(MultiTouchGesture gesture, IReadOnlyList<Vector2[]> frames)
		{
			MultiTouchInfo? last = null;
			foreach (Vector2[] frame in frames)
			{
				last = gesture.Update(new MouseEventArgs(MouseButtons.None, 0, frame, 0, null));
			}

			return last;
		}

		[Test]
		public async Task APinchZoomsByTheMagnificationAboutThePointer()
		{
			var fingers = new TrackpadPinchFingers(50);
			var gesture = new MultiTouchGesture();

			// The first event already carries a change: it is not lost to the recogniser's baseline frame.
			MultiTouchInfo? info = Feed(gesture, fingers.Magnify(Pointer, .2, TrackpadGesturePhase.Began));
			await Assert.That(info.Value.ZoomDelta).IsEqualTo(1.2).Within(1e-9);

			info = Feed(gesture, fingers.Magnify(Pointer, -.1, TrackpadGesturePhase.Changed));
			await Assert.That(info.Value.NumTouches).IsEqualTo(2);
			await Assert.That(info.Value.ZoomDelta).IsEqualTo(.9).Within(1e-9);
			await Assert.That(info.Value.RotationDelta).IsEqualTo(0).Within(1e-9);
			await Assert.That(info.Value.TranslationDelta.Length).IsLessThan(1e-9);
			await Assert.That((info.Value.CenterPosition - Pointer).Length).IsLessThan(1e-9);
		}

		[Test]
		public async Task ARotationTurnsCounterClockwiseByTheDegreesReported()
		{
			var fingers = new TrackpadPinchFingers(50);
			var gesture = new MultiTouchGesture();
			Feed(gesture, fingers.Rotate(Pointer, 0, TrackpadGesturePhase.Began));

			MultiTouchInfo? info = Feed(gesture, fingers.Rotate(Pointer, 30, TrackpadGesturePhase.Changed));
			await Assert.That(info.Value.RotationDelta).IsEqualTo(Math.PI / 6).Within(1e-9);
			await Assert.That(info.Value.ZoomDelta).IsEqualTo(1).Within(1e-9);

			// Pinch and rotate are one gesture: a magnify mid-rotation keeps the angle and the anchor.
			info = Feed(gesture, fingers.Magnify(Pointer + new Vector2(5, 0), .5, TrackpadGesturePhase.Changed));
			await Assert.That(info.Value.ZoomDelta).IsEqualTo(1.5).Within(1e-9);
			await Assert.That(info.Value.RotationDelta).IsEqualTo(0).Within(1e-9);
			await Assert.That((info.Value.CenterPosition - Pointer).Length).IsLessThan(1e-9);
		}

		[Test]
		public async Task TheEndLiftsTheSecondFingerAtThePointer()
		{
			var fingers = new TrackpadPinchFingers(50);
			var gesture = new MultiTouchGesture();
			Feed(gesture, fingers.Magnify(Pointer, .1, TrackpadGesturePhase.Began));

			IReadOnlyList<Vector2[]> end = fingers.Magnify(Pointer, 0, TrackpadGesturePhase.Ended);
			await Assert.That(end[end.Count - 1]).IsEquivalentTo(new[] { Pointer }, CollectionOrdering.Matching);
			await Assert.That(fingers.Active).IsFalse();
			await Assert.That(Feed(gesture, end)).IsNull();

			// The next pinch starts over where the pointer now is.
			var elsewhere = new Vector2(20, 30);
			MultiTouchInfo? info = Feed(gesture, fingers.Magnify(elsewhere, .1, TrackpadGesturePhase.Began));
			await Assert.That((info.Value.CenterPosition - elsewhere).Length).IsLessThan(1e-9);
		}

		[Test]
		public async Task AFullPinchInNeverCollapsesTheFingers()
		{
			var fingers = new TrackpadPinchFingers(50);
			IReadOnlyList<Vector2[]> frames = fingers.Magnify(Pointer, -1.5, TrackpadGesturePhase.Began);
			Vector2[] last = frames[frames.Count - 1];
			await Assert.That((last[1] - last[0]).Length).IsGreaterThan(2);
		}

		[Test]
		public async Task ThePinchMarkSurvivesRoutingIntoAChild()
		{
			var wheel = new MouseEventArgs(MouseButtons.None, 0, 10, 20, 120) { FromTrackpadPinch = true };
			await Assert.That(new MouseEventArgs(wheel, 3, 4).FromTrackpadPinch).IsTrue();
			await Assert.That(new MouseEventArgs(MouseButtons.None, 0, 10, 20, 120).FromTrackpadPinch).IsFalse();

			// And through a real parent's wheel dispatch.
			var parent = new GuiWidget(100, 100);
			var child = new GuiWidget(50, 50) { Position = new Vector2(5, 5) };
			bool? seen = null;
			child.MouseWheel += (s, e) => seen = e.FromTrackpadPinch;
			parent.AddChild(child);
			parent.OnMouseWheel(new MouseEventArgs(MouseButtons.None, 0, 20, 20, 120) { FromTrackpadPinch = true });
			await Assert.That(seen).IsEqualTo(true);
		}

		[Test]
		public async Task AScenePanZoomLeavesAPinchWheelToTheFingers()
		{
			var scene = new ScenePanZoom(new GuiWidget(100, 100)) { HAnchor = HAnchor.Absolute, VAnchor = VAnchor.Absolute };
			scene.LocalBounds = new RectangleDouble(0, 0, 400, 400);
			double zoom = scene.Transform.Zoom;

			var pinchWheel = new MouseEventArgs(MouseButtons.None, 0, 100, 100, -120) { FromTrackpadPinch = true };
			scene.OnMouseWheel(pinchWheel);
			await Assert.That(scene.Transform.Zoom).IsEqualTo(zoom);

			// ... which zoom it instead.
			var fingers = new TrackpadPinchFingers(50);
			foreach (Vector2[] frame in fingers.Magnify(new Vector2(100, 100), -.5, TrackpadGesturePhase.Began))
			{
				scene.OnMouseMove(new MouseEventArgs(MouseButtons.None, 0, frame, 0, null));
			}

			await Assert.That(scene.Transform.Zoom).IsNotEqualTo(zoom);
		}
	}
}
