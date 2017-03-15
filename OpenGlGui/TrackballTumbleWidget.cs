/*
Copyright (c) 2014, Lars Brubaker
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

using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.RenderOpenGl.OpenGl;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;

namespace MatterHackers.Agg.OpenGlGui
{
	public static class ExtensionMethods
    {
		public static void RenderDebugAABB(this TrackballTumbleWidget trackBall, Graphics2D graphics2D, AxisAlignedBoundingBox bounds)
		{
			Vector3 renderPosition = bounds.Center;
			Vector2 objectCenterScreenSpace = trackBall.GetScreenPosition(renderPosition);
			Point2D screenPositionOfObject3D = new Point2D((int)objectCenterScreenSpace.x, (int)objectCenterScreenSpace.y);

			graphics2D.Circle(objectCenterScreenSpace, 5, RGBA_Bytes.Magenta);

			for (int i = 0; i < 4; i++)
			{
				graphics2D.Circle(trackBall.GetScreenPosition(bounds.GetTopCorner(i)), 5, RGBA_Bytes.Magenta);
				graphics2D.Circle(trackBall.GetScreenPosition(bounds.GetBottomCorner(i)), 5, RGBA_Bytes.Magenta);
			}

			RectangleDouble screenBoundsOfObject3D = RectangleDouble.ZeroIntersection;
			for (int i = 0; i < 4; i++)
			{
				screenBoundsOfObject3D.ExpandToInclude(trackBall.GetScreenPosition(bounds.GetTopCorner(i)));
				screenBoundsOfObject3D.ExpandToInclude(trackBall.GetScreenPosition(bounds.GetBottomCorner(i)));
			}

			graphics2D.Circle(screenBoundsOfObject3D.Left, screenBoundsOfObject3D.Bottom, 5, RGBA_Bytes.Cyan);
			graphics2D.Circle(screenBoundsOfObject3D.Left, screenBoundsOfObject3D.Top, 5, RGBA_Bytes.Cyan);
			graphics2D.Circle(screenBoundsOfObject3D.Right, screenBoundsOfObject3D.Bottom, 5, RGBA_Bytes.Cyan);
			graphics2D.Circle(screenBoundsOfObject3D.Right, screenBoundsOfObject3D.Top, 5, RGBA_Bytes.Cyan);
		}
	}

	public class TrackballTumbleWidget : GuiWidget
	{
		public event EventHandler DrawGlContent;

		private bool doOpenGlDrawing = true;

		public bool DoOpenGlDrawing
		{
			get
			{
				return doOpenGlDrawing;
			}

			set
			{
				doOpenGlDrawing = value;
			}
		}

		public TrackBallController.MouseDownType TransformState
		{
			get;
			set;
		}

		private float[] ambientLight = { 0.2f, 0.2f, 0.2f, 1.0f };

		private float[] diffuseLight0 = { 0.7f, 0.7f, 0.7f, 1.0f };
		private float[] specularLight0 = { 0.5f, 0.5f, 0.5f, 1.0f };
		private float[] lightDirection0 = { -1, -1, 1, 0.0f };

		private float[] diffuseLight1 = { 0.5f, 0.5f, 0.5f, 1.0f };
		private float[] specularLight1 = { 0.3f, 0.3f, 0.3f, 1.0f };
		private float[] lightDirection1 = { 1, 1, 1, 0.0f };

		private RGBA_Bytes rotationHelperCircleColor = new RGBA_Bytes(RGBA_Bytes.Black, 200);

		public RGBA_Bytes RotationHelperCircleColor
		{
			get { return rotationHelperCircleColor; }
			set { rotationHelperCircleColor = value; }
		}

		public bool DrawRotationHelperCircle { get; set; }

		private TrackBallController mainTrackBallController = new TrackBallController();

		public TrackBallController TrackBallController
		{
			get { return mainTrackBallController; }
			set
			{
				mainTrackBallController.TransformChanged -= TrackBallController_TransformChanged;
				mainTrackBallController = value;
				mainTrackBallController.TransformChanged += TrackBallController_TransformChanged;
			}
		}

		public bool LockTrackBall { get; set; }

		public TrackballTumbleWidget()
		{
			AnchorAll();
			DrawRotationHelperCircle = true;
			TrackBallController.TransformChanged += TrackBallController_TransformChanged;
		}

		private void TrackBallController_TransformChanged(object sender, EventArgs e)
		{
			CalculateModelviewMatrix();
		}

		public override void OnBoundsChanged(EventArgs e)
		{
			Vector2 screenCenter = new Vector2(Width / 2, Height / 2);
			double trackingRadius = Math.Min(Width * .45, Height * .45);
			TrackBallController.ScreenCenter = screenCenter;
			TrackBallController.TrackBallRadius = trackingRadius;
			CalculateProjectionMatrix();
			MakeArrowIcons();

			base.OnBoundsChanged(e);
		}

		public Vector3 GetWorldPosition(Vector2 screenPosition)
		{
			Vector4 homoginizedScreenSpace = new Vector4((2.0f * (screenPosition.x / Width)) - 1,
				1 - (2 * (screenPosition.y / Height)),
				1,
				1);

			Matrix4X4 viewProjection = ModelviewMatrix * ProjectionMatrix;
			Matrix4X4 viewProjectionInverse = Matrix4X4.Invert(viewProjection);
			Vector4 woldSpace = Vector4.Transform(homoginizedScreenSpace, viewProjectionInverse);

			double perspectiveDivide = 1 / woldSpace.w;

			woldSpace.x *= perspectiveDivide;
			woldSpace.y *= perspectiveDivide;
			woldSpace.z *= perspectiveDivide;

			return new Vector3(woldSpace);
		}

		public Vector2 GetScreenPosition(Vector3 worldPosition)
		{
			Vector3 homoginizedViewPosition = Vector3.Transform(worldPosition, ModelviewMatrix);

			Vector3 homoginizedScreenPosition = Vector3.TransformPerspective(homoginizedViewPosition, ProjectionMatrix);

			Vector2 screenPosition = new Vector2(homoginizedScreenPosition.x * Width / 2 + Width / 2, homoginizedScreenPosition.y * Height / 2 + Height / 2);

			return screenPosition; 
		}

		public Vector3 GetScreenSpace(Vector3 worldPosition)
		{
			Vector3 viewPosition = Vector3.Transform(worldPosition, ModelviewMatrix);

			return Vector3.Transform(viewPosition, ProjectionMatrix);
		}

		public Ray GetRayForLocalBounds(Vector2 localPosition)
		{
			Vector4 rayClip = new Vector4();
			rayClip.x = (2.0 * localPosition.x) / Width - 1.0;
			rayClip.y = (2.0 * localPosition.y) / Height - 1.0;
			rayClip.z = -1.0;
			rayClip.w = 1.0;

			Vector4 rayEye = Vector4.Transform(rayClip, InverseProjectionMatrix);
			rayEye.z = -1; rayEye.w = 0;

			Vector4 rayWorld = Vector4.Transform(rayEye, InverseModelviewMatrix);

			Vector3 finalRayWorld = new Vector3(rayWorld).GetNormal();

			Vector3 origin = Vector3.Transform(Vector3.Zero, InverseModelviewMatrix);

			return new Ray(origin, finalRayWorld);
		}

		public override void OnDraw(MatterHackers.Agg.Graphics2D graphics2D)
		{
			if (DoOpenGlDrawing)
			{
				SetGlContext();
				OnDrawGlContent();
				UnsetGlContext();
			}

			RectangleDouble bounds = LocalBounds;
			//graphics2D.Rectangle(bounds, RGBA_Bytes.Black);

			if (DrawRotationHelperCircle)
			{
				DrawTrackballRadius(graphics2D);
			}

			base.OnDraw(graphics2D);
		}

		List<IVertexSource> insideArrows = new List<IVertexSource>();
		List<IVertexSource> outsideArrows = new List<IVertexSource>();

		public void DrawTrackballRadius(Graphics2D graphics2D)
		{
			var center = TrackBallController.ScreenCenter;
			var radius = TrackBallController.TrackBallRadius;
			var elipse = new Ellipse(center, radius, radius);
			var outline = new Stroke(elipse, 3);
			graphics2D.Render(outline, RotationHelperCircleColor);

			if (insideArrows.Count == 0)
			{
				MakeArrowIcons();
			}

			if (TrackBallController.LastMoveInsideRadius)
			{
				foreach (IVertexSource arrow in insideArrows)
				{
					graphics2D.Render(arrow, RotationHelperCircleColor);
				}
			}
			else
			{
				foreach (IVertexSource arrow in outsideArrows)
				{
					graphics2D.Render(arrow, RotationHelperCircleColor);
				}
			}
		}

		private void MakeArrowIcons()
		{
			var center = TrackBallController.ScreenCenter;
			var radius = TrackBallController.TrackBallRadius;
			insideArrows.Clear();
			// create the inside arrows
			{
				var svg = new PathStorage("M560.512 0.570216 C560.512 2.05696 280.518 560.561 280.054 560 C278.498 558.116 0 0.430888 0.512416 0.22416 C0.847112 0.089136 63.9502 27.1769 140.742 60.4192 C140.742 60.4192 280.362 120.86 280.362 120.86 C280.362 120.86 419.756 60.4298 419.756 60.4298 C496.422 27.1934 559.456 0 559.831 0 C560.205 0 560.512 0.2566 560.512 0.570216 Z");
				RectangleDouble bounds = svg.GetBounds();
				double arrowWidth = radius / 10;
				var centered = Affine.NewTranslation(-bounds.Center);
				var scaledTo1 = Affine.NewScaling(1 / bounds.Width);
				var scaledToSize = Affine.NewScaling(arrowWidth);
				var moveToRadius = Affine.NewTranslation(new Vector2(0, radius * 9 / 10));
				var moveToScreenCenter = Affine.NewTranslation(center);
				for (int i = 0; i < 4; i++)
				{
					var arrowLeftTransform = centered * scaledTo1 * scaledToSize * moveToRadius * Affine.NewRotation(MathHelper.Tau / 4 * i) * moveToScreenCenter;
					insideArrows.Add(new VertexSourceApplyTransform(svg, arrowLeftTransform));
				}
			}

			outsideArrows.Clear();
			// and the outside arrows
			{
				//var svg = new PathStorage("m 271.38288,545.86543 c -10.175,-4.94962 -23,-11.15879 -28.5,-13.79816 -5.5,-2.63937 -24.34555,-11.82177 -41.87901,-20.40534 -17.53346,-8.58356 -32.21586,-15.60648 -32.62756,-15.60648 -0.4117,0 -1.28243,-0.64329 -1.93495,-1.42954 -0.98148,-1.1826 -0.0957,-1.94177 5.12755,-4.39484 3.47268,-1.63091 16.21397,-7.7909 28.31397,-13.68887 12.1,-5.89797 30.55,-14.8788 41,-19.9574 10.45,-5.07859 25.64316,-12.49628 33.76258,-16.48374 8.11942,-3.98746 15.43192,-6.99308 16.25,-6.67916 2.02527,0.77717 1.8755,5.19031 -0.56452,16.63355 -1.11411,5.225 -2.29208,10.9625 -2.6177,12.75 l -0.59204,3.25 80.19823,0 c 75.90607,0 80.17104,-0.0937 79.69036,-1.75 -2.47254,-8.51983 -5.62648,-24.42623 -5.62674,-28.37756 -3.6e-4,-5.51447 1.61726,-5.18356 21.01872,4.29961 10.16461,4.96833 22.98111,11.1892 28.48111,13.82415 5.5,2.63496 24.34555,11.81375 41.87901,20.39732 17.53346,8.58356 32.21586,15.60648 32.62756,15.60648 0.4117,0 1.28243,0.64329 1.93495,1.42954 0.98144,1.18256 0.0956,1.94283 -5.12755,4.40048 -3.47268,1.63401 -15.98897,7.68875 -27.81397,13.45496 -11.825,5.76621 -31.625,15.41743 -44,21.44716 -12.375,6.02972 -27.79146,13.55332 -34.2588,16.71911 -6.99025,3.42175 -12.41867,5.50276 -13.38597,5.13157 -2.11241,-0.81061 -1.37413,-8.85503 2.14722,-23.39653 1.37365,-5.67253 2.49755,-10.73503 2.49755,-11.25 0,-0.57397 -31.15148,-0.93629 -80.5,-0.93629 -76.11526,0 -80.5,0.0957 -80.5,1.7566 0,0.96613 0.45587,3.32863 1.01304,5.25 1.68077,5.79599 4.98696,23.01922 4.98696,25.97902 0,5.59974 -1.53004,5.29551 -21,-4.17564 z");
				var svg = new PathStorage("M560.512 0.570216 C560.512 2.05696 280.518 560.561 280.054 560 C278.498 558.116 0 0.430888 0.512416 0.22416 C0.847112 0.089136 63.9502 27.1769 140.742 60.4192 C140.742 60.4192 280.362 120.86 280.362 120.86 C280.362 120.86 419.756 60.4298 419.756 60.4298 C496.422 27.1934 559.456 0 559.831 0 C560.205 0 560.512 0.2566 560.512 0.570216 Z");
				RectangleDouble bounds = svg.GetBounds();
				double arrowWidth = radius / 15;
				var centered = Affine.NewTranslation(-bounds.Center);
				var scaledTo1 = Affine.NewScaling(1 / bounds.Width);
				var scaledToSize = Affine.NewScaling(arrowWidth);
				var moveToRadius = Affine.NewTranslation(new Vector2(0, radius * 16 / 15));
				var moveToScreenCenter = Affine.NewTranslation(center);
				for (int i = 0; i < 4; i++)
				{
					var arrowLeftTransform = centered * scaledTo1 * scaledToSize * Affine.NewRotation(MathHelper.Tau / 4) * moveToRadius * Affine.NewRotation(MathHelper.Tau / 8 + MathHelper.Tau / 4 * i + MathHelper.Tau / 80) * moveToScreenCenter;
					outsideArrows.Add(new VertexSourceApplyTransform(svg, arrowLeftTransform));

					var arrowRightTransform = centered * scaledTo1 * scaledToSize * Affine.NewRotation(-MathHelper.Tau / 4) * moveToRadius * Affine.NewRotation(MathHelper.Tau / 8 + MathHelper.Tau / 4 * i - MathHelper.Tau / 80) * moveToScreenCenter;
					outsideArrows.Add(new VertexSourceApplyTransform(svg, arrowRightTransform));
				}
			}
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
					while (firstTimeIndex > 0 && motionQueue[firstTimeIndex-1].timeMs + 100 > lastTime.timeMs)
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
					currentMousePosition.x = mouseEvent.X;
					currentMousePosition.y = mouseEvent.Y;
				}
				else
				{
					Vector2 centerPosition = (mouseEvent.GetPosition(1) + mouseEvent.GetPosition(0)) / 2;
					currentMousePosition = centerPosition;
				}

				currentVelocityPerMs = Vector2.Zero;
				motionQueue.Clear();
				motionQueue.AddMoveToMotionQueue(currentMousePosition, UiThread.CurrentTimerMs);

				if (mouseEvent.NumPositions > 1)
				{
					Vector2 position0 = mouseEvent.GetPosition(0);
					Vector2 position1 = mouseEvent.GetPosition(1);
					startDistanceBetweenPoints = (position1 - position0).Length;
					pinchStartScale = TrackBallController.Scale;

					startAngle = Math.Atan2(position1.y - position0.y, position1.x - position0.x);

					if (TransformState != TrackBallController.MouseDownType.None)
					{
						if (!LockTrackBall && TrackBallController.CurrentTrackingType != TrackBallController.MouseDownType.None)
						{
							TrackBallController.OnMouseUp();
						}
						TrackBallController.OnMouseDown(currentMousePosition, Matrix4X4.Identity, TrackBallController.MouseDownType.Translation);
					}
				}

				if (mouseEvent.Button == MouseButtons.Left)
				{
					if (TrackBallController.CurrentTrackingType == TrackBallController.MouseDownType.None)
					{
						Keys modifierKeys = ModifierKeys;
						if (modifierKeys == Keys.Shift)
						{
							TrackBallController.OnMouseDown(currentMousePosition, Matrix4X4.Identity, TrackBallController.MouseDownType.Translation);
						}
						else if (modifierKeys == Keys.Control)
						{
							TrackBallController.OnMouseDown(currentMousePosition, Matrix4X4.Identity, TrackBallController.MouseDownType.Scale);
						}
						else if (modifierKeys == Keys.Alt)
						{
							TrackBallController.OnMouseDown(currentMousePosition, Matrix4X4.Identity, TrackBallController.MouseDownType.Rotation);
						}
						else
						{
							switch (TransformState)
							{
								case TrackBallController.MouseDownType.Rotation:
									TrackBallController.OnMouseDown(currentMousePosition, Matrix4X4.Identity, TrackBallController.MouseDownType.Rotation);
									break;

								case TrackBallController.MouseDownType.Translation:
									TrackBallController.OnMouseDown(currentMousePosition, Matrix4X4.Identity, TrackBallController.MouseDownType.Translation);
									break;

								case TrackBallController.MouseDownType.Scale:
									TrackBallController.OnMouseDown(currentMousePosition, Matrix4X4.Identity, TrackBallController.MouseDownType.Scale);
									break;
							}
						}
					}
				}
				else if (mouseEvent.Button == MouseButtons.Middle)
				{
					if (TrackBallController.CurrentTrackingType == TrackBallController.MouseDownType.None)
					{
						TrackBallController.OnMouseDown(currentMousePosition, Matrix4X4.Identity, TrackBallController.MouseDownType.Translation);
					}
				}
				else if (mouseEvent.Button == MouseButtons.Right)
				{
					if (TrackBallController.CurrentTrackingType == TrackBallController.MouseDownType.None)
					{
						TrackBallController.OnMouseDown(currentMousePosition, Matrix4X4.Identity, TrackBallController.MouseDownType.Rotation);
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
				currentMousePosition.x = mouseEvent.X;
				currentMousePosition.y = mouseEvent.Y;
				if (MouseCaptured
					&& TransformState == TrackBallController.MouseDownType.Rotation)
				{
					DrawRotationHelperCircle = true;
				}
				else
				{
					DrawRotationHelperCircle = false;
				}
			}
			else
			{
				Vector2 centerPosition = (mouseEvent.GetPosition(1) + mouseEvent.GetPosition(0)) / 2;
				currentMousePosition = centerPosition;
				DrawRotationHelperCircle = false;
			}

			motionQueue.AddMoveToMotionQueue(currentMousePosition, UiThread.CurrentTimerMs);

			if (!LockTrackBall && TrackBallController.CurrentTrackingType != TrackBallController.MouseDownType.None)
			{
				TrackBallController.OnMouseMove(currentMousePosition);
				Invalidate();
			}

			// check if we should do some scaling or rotation
			if (TransformState != TrackBallController.MouseDownType.None
				&& mouseEvent.NumPositions > 1
				&& startDistanceBetweenPoints > 0)
			{
				Vector2 position0 = mouseEvent.GetPosition(0);
				Vector2 position1 = mouseEvent.GetPosition(1);
				double curDistanceBetweenPoints = (position1 - position0).Length;

				double scaleAmount = pinchStartScale * curDistanceBetweenPoints / startDistanceBetweenPoints;
				TrackBallController.Scale = scaleAmount;

				double angle = Math.Atan2(position1.y - position0.y, position1.x - position0.x);
			}
		}

		Vector2 currentVelocityPerMs = new Vector2();
		public void ZeroVelocity()
		{
			currentVelocityPerMs = Vector2.Zero;
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			if (!LockTrackBall && TrackBallController.CurrentTrackingType != TrackBallController.MouseDownType.None)
			{
                if (TrackBallController.CurrentTrackingType == TrackBallController.MouseDownType.Rotation
					&& TrackBallController.LastMoveInsideRadius)
                {
					// try and preserve some of the velocity
					motionQueue.AddMoveToMotionQueue(mouseEvent.Position, UiThread.CurrentTimerMs);

					currentVelocityPerMs = motionQueue.GetVelocityPixelsPerMs();
                    if (currentVelocityPerMs.LengthSquared > 0)
                    {
                        UiThread.RunOnIdle(ApplyVelocity);
                    }
                }

				TrackBallController.OnMouseUp();
            }

            base.OnMouseUp(mouseEvent);
		}

		int updatesPerSecond = 30;
        private void ApplyVelocity()
        {
			double msPerUpdate = 1000.0 / updatesPerSecond;
            if (currentVelocityPerMs.LengthSquared > 0)
            {
                if (TrackBallController.CurrentTrackingType == TrackBallController.MouseDownType.None)
                {
                    Vector2 center = LocalBounds.Center;
					TrackBallController.OnMouseDown(center, Matrix4X4.Identity, TrackBallController.MouseDownType.Rotation);
					TrackBallController.OnMouseMove(center + currentVelocityPerMs * msPerUpdate);
					TrackBallController.OnMouseUp();
                    Invalidate();

                    currentVelocityPerMs *= .85;
                    if(currentVelocityPerMs.LengthSquared < .01 / msPerUpdate)
                    {
                        currentVelocityPerMs = Vector2.Zero;
                    }

                    if (currentVelocityPerMs.LengthSquared > 0)
                    {
						UiThread.RunOnIdle(ApplyVelocity, 1.0 / updatesPerSecond);
                    }
                }
            }
        }

		public override void OnMouseWheel(MouseEventArgs mouseEvent)
		{
			if (!LockTrackBall)
			{
				TrackBallController.OnMouseWheel(mouseEvent.WheelDelta);
				Invalidate();
			}
			base.OnMouseWheel(mouseEvent);
		}

		private void OnDrawGlContent()
		{
			DrawGlContent?.Invoke(this, null);
		}

		private void GradientBand(double startHeight, double endHeight, int startColor, int endColor)
		{
			// triangel 1
			{
				// top color
				GL.Color4(startColor - 5, startColor - 5, startColor, 255);
				GL.Vertex2(-1.0, startHeight);
				// bottom color
				GL.Color4(endColor - 5, endColor - 5, endColor, 255);
				GL.Vertex2(1.0, endHeight);
				GL.Vertex2(-1.0, endHeight);
			}

			// triangel 2
			{
				// top color
				GL.Color4(startColor - 5, startColor - 5, startColor, 255);
				GL.Vertex2(1.0, startHeight);
				GL.Vertex2(-1.0, startHeight);
				// bottom color
				GL.Color4(endColor - 5, endColor - 5, endColor, 255);
				GL.Vertex2(1.0, endHeight);
			}
		}

		private void ClearToGradient()
		{
			GL.MatrixMode(MatrixMode.Projection);
			GL.LoadIdentity();

			GL.MatrixMode(MatrixMode.Modelview);
			GL.LoadIdentity();

			GL.Begin(BeginMode.Triangles);

			GradientBand(1, 0, 255, 245);
			GradientBand(0, -1, 245, 220);

			GL.End();
		}

		private void SetGlContext()
		{
			GL.ClearDepth(1.0);
			GL.Clear(ClearBufferMask.DepthBufferBit);	// Clear the Depth Buffer

			GL.PushAttrib(AttribMask.ViewportBit);
			RectangleDouble screenRect = this.TransformToScreenSpace(LocalBounds);
			GL.Viewport((int)screenRect.Left, (int)screenRect.Bottom, (int)screenRect.Width, (int)screenRect.Height);

			GL.ShadeModel(ShadingModel.Smooth);

			GL.FrontFace(FrontFaceDirection.Ccw);
			GL.CullFace(CullFaceMode.Back);

			GL.DepthFunc(DepthFunction.Lequal);

			GL.Disable(EnableCap.DepthTest);
			ClearToGradient();

#if DO_LIGHTING
			GL.Light(LightName.Light0, LightParameter.Ambient, ambientLight);

			GL.Light(LightName.Light0, LightParameter.Diffuse, diffuseLight0);
			GL.Light(LightName.Light0, LightParameter.Specular, specularLight0);

			GL.Light(LightName.Light0, LightParameter.Ambient, new float[] { 0, 0, 0, 0 });
			GL.Light(LightName.Light1, LightParameter.Diffuse, diffuseLight1);
			GL.Light(LightName.Light1, LightParameter.Specular, specularLight1);

			GL.ColorMaterial(MaterialFace.FrontAndBack, ColorMaterialParameter.AmbientAndDiffuse);

			GL.Enable(EnableCap.Light0);
			GL.Enable(EnableCap.Light1);
			GL.Enable(EnableCap.DepthTest);
			GL.Enable(EnableCap.Blend);
			GL.Enable(EnableCap.Normalize);
			GL.Enable(EnableCap.Lighting);
			GL.Enable(EnableCap.ColorMaterial);

			Vector3 lightDirectionVector = new Vector3(lightDirection0[0], lightDirection0[1], lightDirection0[2]);
			lightDirectionVector.Normalize();
			lightDirection0[0] = (float)lightDirectionVector.x;
			lightDirection0[1] = (float)lightDirectionVector.y;
			lightDirection0[2] = (float)lightDirectionVector.z;
			GL.Light(LightName.Light0, LightParameter.Position, lightDirection0);
			GL.Light(LightName.Light1, LightParameter.Position, lightDirection1);
#endif

			// set the projection matrix
			GL.MatrixMode(MatrixMode.Projection);
			GL.PushMatrix();
			GL.LoadMatrix(ProjectionMatrix.GetAsDoubleArray());

			// set the modelview matrix
			GL.MatrixMode(MatrixMode.Modelview);
			GL.PushMatrix();
			GL.LoadMatrix(ModelviewMatrix.GetAsDoubleArray());
		}

		private void UnsetGlContext()
		{
			GL.MatrixMode(MatrixMode.Projection);
			GL.PopMatrix();

			GL.MatrixMode(MatrixMode.Modelview);
			GL.PopMatrix();

#if DO_LIGHTING
			GL.Disable(EnableCap.ColorMaterial);
			GL.Disable(EnableCap.Lighting);
			GL.Disable(EnableCap.Light0);
			GL.Disable(EnableCap.Light1);
#endif
			GL.Disable(EnableCap.Normalize);
			GL.Disable(EnableCap.Blend);
			GL.Disable(EnableCap.DepthTest);

			GL.PopAttrib();
		}

		private Matrix4X4 projectionMatrix;

		public Matrix4X4 ProjectionMatrix { get { return projectionMatrix; } }

		private Matrix4X4 inverseProjectionMatrix;

		public Matrix4X4 InverseProjectionMatrix { get { return inverseProjectionMatrix; } }

		public void CalculateProjectionMatrix()
		{
			projectionMatrix = Matrix4X4.Identity;
			if (Width > 0 && Height > 0)
			{
				Matrix4X4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(45), Width / Height, 0.1f, 100.0f, out projectionMatrix);
				inverseProjectionMatrix = Matrix4X4.Invert(ProjectionMatrix);
			}
		}

		private Matrix4X4 modelviewMatrix;

		public Matrix4X4 ModelviewMatrix { get { return modelviewMatrix; } }

		private Matrix4X4 inverseModelviewMatrix;

		public Matrix4X4 InverseModelviewMatrix { get { return inverseModelviewMatrix; } }

		public void CalculateModelviewMatrix()
		{
			modelviewMatrix = Matrix4X4.CreateTranslation(0, 0, -7);
			modelviewMatrix = TrackBallController.GetTransform4X4() * modelviewMatrix;
			inverseModelviewMatrix = Matrix4X4.Invert(modelviewMatrix);
		}

		public double GetWorldUnitsPerScreenPixelAtPosition(Vector3 worldPosition, double maxRatio = 5)
		{
			Vector2 screenPosition = GetScreenPosition(worldPosition);

            Ray rayFromScreen = GetRayForLocalBounds(screenPosition);
			double distanceFromOriginToWorldPos = (worldPosition - rayFromScreen.origin).Length;

			Ray rightOnePixelRay = GetRayForLocalBounds(new Vector2(screenPosition.x + 1, screenPosition.y));
			Vector3 rightOnePixel = rightOnePixelRay.origin + rightOnePixelRay.directionNormal * distanceFromOriginToWorldPos;
			double distBetweenPixelsWorldSpace = (rightOnePixel - worldPosition).Length;
			if(distBetweenPixelsWorldSpace > maxRatio)
			{
				return maxRatio;
			}
			return distBetweenPixelsWorldSpace;
		}
	}
}