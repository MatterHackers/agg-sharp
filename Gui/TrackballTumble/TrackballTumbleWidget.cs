/*
Copyright (c) 2018, Lars Brubaker, John Lewin
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

#define DO_LIGHTING

using System;
using System.Collections.Generic;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;
using MatterHackers.VectorMath.TrackBall;

namespace MatterHackers.Agg
{
	public static class ExtensionMethods
	{
		public static void RenderDebugAABB(this WorldView worldView, Graphics2D graphics2D, AxisAlignedBoundingBox bounds)
		{
			Vector3 renderPosition = bounds.Center;
			Vector2 objectCenterScreenSpace = worldView.GetScreenPosition(renderPosition);
			Point2D screenPositionOfObject3D = new Point2D((int)objectCenterScreenSpace.X, (int)objectCenterScreenSpace.Y);

			graphics2D.Circle(objectCenterScreenSpace, 5, Color.Magenta);

			for (int i = 0; i < 4; i++)
			{
				graphics2D.Circle(worldView.GetScreenPosition(bounds.GetTopCorner(i)), 5, Color.Magenta);
				graphics2D.Circle(worldView.GetScreenPosition(bounds.GetBottomCorner(i)), 5, Color.Magenta);
			}

			RectangleDouble screenBoundsOfObject3D = RectangleDouble.ZeroIntersection;
			for (int i = 0; i < 4; i++)
			{
				screenBoundsOfObject3D.ExpandToInclude(worldView.GetScreenPosition(bounds.GetTopCorner(i)));
				screenBoundsOfObject3D.ExpandToInclude(worldView.GetScreenPosition(bounds.GetBottomCorner(i)));
			}

			graphics2D.Circle(screenBoundsOfObject3D.Left, screenBoundsOfObject3D.Bottom, 5, Color.Cyan);
			graphics2D.Circle(screenBoundsOfObject3D.Left, screenBoundsOfObject3D.Top, 5, Color.Cyan);
			graphics2D.Circle(screenBoundsOfObject3D.Right, screenBoundsOfObject3D.Bottom, 5, Color.Cyan);
			graphics2D.Circle(screenBoundsOfObject3D.Right, screenBoundsOfObject3D.Top, 5, Color.Cyan);
		}
	}

	public class TrackballTumbleWidget : GuiWidget
	{
		public TrackBallTransformType TransformState { get; set; }

		private WorldView world;

		public TrackBallController TrackBallController { get; }

		public bool LockTrackBall { get; set; }

		private GuiWidget sourceWidget;

		private RunningInterval runningInterval;

		public TrackballTumbleWidget(WorldView world, GuiWidget sourceWidget)
		{
			AnchorAll();
			TrackBallController = new TrackBallController(world);
			this.world = world;
			this.sourceWidget = sourceWidget;
		}

		private double _centerOffsetX = 0;
		public double CenterOffsetX
		{
			get
			{
				return _centerOffsetX;
			}
			set
			{
				_centerOffsetX = value;
				RecalculateProjection();
			}
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			RecalculateProjection();

			base.OnDraw(graphics2D);
		}

		public void RecalculateProjection()
		{
			double trackingRadius = Math.Min(Width * .45, Height * .45);
			TrackBallController.ScreenCenter = new Vector2(Width / 2 - CenterOffsetX, Height / 2);

			TrackBallController.TrackBallRadius = trackingRadius;

			if (CenterOffsetX != 0)
			{
				this.world.CalculateProjectionMatrixOffCenter(sourceWidget.Width, sourceWidget.Height, CenterOffsetX);
			}
			else
			{
				this.world.CalculateProjectionMatrix(sourceWidget.Width, sourceWidget.Height);
			}

			this.world.CalculateModelviewMatrix();
		}

		internal class MotionQueue
		{
			internal struct TimeAndPosition
			{
				internal TimeAndPosition(Vector2 position, long timeMs)
				{
					this.timeMs = timeMs;
					this.position = position;
				}

				internal long timeMs;
				internal Vector2 position;
			}

			List<TimeAndPosition> motionQueue = new List<TimeAndPosition>();

			internal void AddMoveToMotionQueue(Vector2 position, long timeMs)
			{
				if (motionQueue.Count > 4)
				{
					// take off the last one
					motionQueue.RemoveAt(0);
				}

				motionQueue.Add(new TimeAndPosition(position, timeMs));
			}

			internal void Clear()
			{
				motionQueue.Clear();
			}

			internal Vector2 GetVelocityPixelsPerMs()
			{
				if (motionQueue.Count > 1)
				{
					// Get all the movement that is less 100 ms from the last time (the mouse up)
					TimeAndPosition lastTime = motionQueue[motionQueue.Count - 1];
					int firstTimeIndex = motionQueue.Count - 1;
					while (firstTimeIndex > 0 && motionQueue[firstTimeIndex - 1].timeMs + 100 > lastTime.timeMs)
					{
						firstTimeIndex--;
					}

					TimeAndPosition firstTime = motionQueue[firstTimeIndex];

					double milliseconds = lastTime.timeMs - firstTime.timeMs;
					if (milliseconds > 0)
					{
						Vector2 pixels = lastTime.position - firstTime.position;
						Vector2 pixelsPerSecond = pixels / milliseconds;

						return pixelsPerSecond;
					}
				}

				return Vector2.Zero;
			}
		}

		MotionQueue motionQueue = new MotionQueue();

		double startAngle = 0;
		double startDistanceBetweenPoints = 1;
		double pinchStartScale = 1;

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			base.OnMouseDown(mouseEvent);

			if (!LockTrackBall && MouseCaptured)
			{
				Vector2 currentMousePosition;
				if (mouseEvent.NumPositions == 1)
				{
					currentMousePosition.X = mouseEvent.X;
					currentMousePosition.Y = mouseEvent.Y;
				}
				else
				{
					currentMousePosition = (mouseEvent.GetPosition(1) + mouseEvent.GetPosition(0)) / 2;
				}

				currentVelocityPerMs = Vector2.Zero;
				motionQueue.Clear();
				motionQueue.AddMoveToMotionQueue(currentMousePosition, UiThread.CurrentTimerMs);

				if (mouseEvent.NumPositions > 1)
				{
					Vector2 position0 = mouseEvent.GetPosition(0);
					Vector2 position1 = mouseEvent.GetPosition(1);
					startDistanceBetweenPoints = (position1 - position0).Length;
					pinchStartScale = world.Scale;

					startAngle = Math.Atan2(position1.Y - position0.Y, position1.X - position0.X);

					if (TransformState != TrackBallTransformType.None)
					{
						if (!LockTrackBall && TrackBallController.CurrentTrackingType != TrackBallTransformType.None)
						{
							TrackBallController.OnMouseUp();
						}
						TrackBallController.OnMouseDown(currentMousePosition, Matrix4X4.Identity, TrackBallTransformType.Translation);
					}
				}

				if (mouseEvent.Button == MouseButtons.Left)
				{
					if (TrackBallController.CurrentTrackingType == TrackBallTransformType.None)
					{
						switch (TransformState)
						{
							case TrackBallTransformType.Rotation:
								TrackBallController.OnMouseDown(currentMousePosition, Matrix4X4.Identity, TrackBallTransformType.Rotation);
								break;

							case TrackBallTransformType.Translation:
								TrackBallController.OnMouseDown(currentMousePosition, Matrix4X4.Identity, TrackBallTransformType.Translation);
								break;

							case TrackBallTransformType.Scale:
								TrackBallController.OnMouseDown(currentMousePosition, Matrix4X4.Identity, TrackBallTransformType.Scale);
								break;
						}
					}
				}
				else if (mouseEvent.Button == MouseButtons.Middle)
				{
					if (TrackBallController.CurrentTrackingType == TrackBallTransformType.None)
					{
						TrackBallController.OnMouseDown(currentMousePosition, Matrix4X4.Identity, TrackBallTransformType.Translation);
					}
				}
				else if (mouseEvent.Button == MouseButtons.Right)
				{
					if (TrackBallController.CurrentTrackingType == TrackBallTransformType.None)
					{
						TrackBallController.OnMouseDown(currentMousePosition, Matrix4X4.Identity, TrackBallTransformType.Rotation);
					}
				}
			}
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			base.OnMouseMove(mouseEvent);

			Vector2 currentMousePosition;
			if (mouseEvent.NumPositions == 1)
			{
				currentMousePosition.X = mouseEvent.X;
				currentMousePosition.Y = mouseEvent.Y;
			}
			else
			{
				currentMousePosition = (mouseEvent.GetPosition(1) + mouseEvent.GetPosition(0)) / 2;
			}

			motionQueue.AddMoveToMotionQueue(currentMousePosition, UiThread.CurrentTimerMs);

			if (!LockTrackBall && TrackBallController.CurrentTrackingType != TrackBallTransformType.None)
			{
				TrackBallController.OnMouseMove(currentMousePosition);
				Invalidate();
			}

			// check if we should do some scaling or rotation
			if (TransformState != TrackBallTransformType.None
				&& mouseEvent.NumPositions > 1
				&& startDistanceBetweenPoints > 0)
			{
				Vector2 position0 = mouseEvent.GetPosition(0);
				Vector2 position1 = mouseEvent.GetPosition(1);
				double curDistanceBetweenPoints = (position1 - position0).Length;

				double scaleAmount = pinchStartScale * curDistanceBetweenPoints / startDistanceBetweenPoints;
				this.world.Scale = scaleAmount;

				double angle = Math.Atan2(position1.Y - position0.Y, position1.X - position0.X);
			}
		}

		Vector2 currentVelocityPerMs = new Vector2();
		public void ZeroVelocity()
		{
			currentVelocityPerMs = Vector2.Zero;
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			if (!LockTrackBall && TrackBallController.CurrentTrackingType != TrackBallTransformType.None)
			{
				if (TrackBallController.CurrentTrackingType == TrackBallTransformType.Rotation)
				{
					// try and preserve some of the velocity
					motionQueue.AddMoveToMotionQueue(mouseEvent.Position, UiThread.CurrentTimerMs);

					if (!Keyboard.IsKeyDown(Keys.ShiftKey))
					{
						currentVelocityPerMs = motionQueue.GetVelocityPixelsPerMs();
						if (currentVelocityPerMs.LengthSquared > 0)
						{
							runningInterval = UiThread.SetInterval(ApplyVelocity, 1.0 / updatesPerSecond);
						}
					}
				}

				TrackBallController.OnMouseUp();
			}

			base.OnMouseUp(mouseEvent);
		}

		int updatesPerSecond = 30;
		private void ApplyVelocity()
		{
			if (HasBeenClosed || currentVelocityPerMs.LengthSquared <= 0)
			{
				UiThread.ClearInterval(runningInterval);
			}

			double msPerUpdate = 1000.0 / updatesPerSecond;
			if (currentVelocityPerMs.LengthSquared > 0)
			{
				if (TrackBallController.CurrentTrackingType == TrackBallTransformType.None)
				{
					Vector2 center = LocalBounds.Center;
					TrackBallController.OnMouseDown(center, Matrix4X4.Identity, TrackBallTransformType.Rotation);
					TrackBallController.OnMouseMove(center + currentVelocityPerMs * msPerUpdate);
					TrackBallController.OnMouseUp();
					Invalidate();

					currentVelocityPerMs *= .85;
					if (currentVelocityPerMs.LengthSquared < .01 / msPerUpdate)
					{
						currentVelocityPerMs = Vector2.Zero;
					}
				}
			}
		}

		public override void OnMouseWheel(MouseEventArgs mouseEvent)
		{
			if (!LockTrackBall && ContainsFirstUnderMouseRecursive())
			{
				TrackBallController.OnMouseWheel(mouseEvent.WheelDelta);
				Invalidate();
			}
			base.OnMouseWheel(mouseEvent);
		}
	}
}