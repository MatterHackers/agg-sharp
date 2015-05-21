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
			mainTrackBallController.TransformChanged += TrackBallController_TransformChanged;
		}

		private void TrackBallController_TransformChanged(object sender, EventArgs e)
		{
			CalculateModelviewMatrix();
		}

		public override void OnBoundsChanged(EventArgs e)
		{
			Vector2 screenCenter = new Vector2(Width / 2, Height / 2);
			double trackingRadius = Math.Min(Width * .45, Height * .45);
			mainTrackBallController.ScreenCenter = screenCenter;
			mainTrackBallController.TrackBallRadius = trackingRadius;
			CalculateProjectionMatrix();
			MakeArrowIcons();

			base.OnBoundsChanged(e);
		}

		public Vector2 GetScreenPosition(Vector3 worldPosition)
		{
			Vector3 viewPosition = Vector3.Transform(worldPosition, ModelviewMatrix);

			Vector3 screenPosition = Vector3.TransformPerspective(viewPosition, ProjectionMatrix);

			return new Vector2(screenPosition.x * Width / 2 + Width / 2, screenPosition.y / screenPosition.z * Height / 2 + Height / 2);
		}

		public Ray GetRayFromScreen(Vector2 screenPosition)
		{
			Vector4 rayClip = new Vector4();
			rayClip.x = (2.0 * screenPosition.x) / Width - 1.0;
			rayClip.y = (2.0 * screenPosition.y) / Height - 1.0;
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
			var center = mainTrackBallController.ScreenCenter;
			var radius = mainTrackBallController.TrackBallRadius;
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
			var center = mainTrackBallController.ScreenCenter;
			var radius = mainTrackBallController.TrackBallRadius;
			insideArrows.Clear();
			// create the inside arrows
			{
				var svg = new PathStorage("M560.512 0.570216 C560.512 2.05696 280.518 560.561 280.054 560 C278.498 558.116 0 0.430888 0.512416 0.22416 C0.847112 0.089136 63.9502 27.1769 140.742 60.4192 C140.742 60.4192 280.362 120.86 280.362 120.86 C280.362 120.86 419.756 60.4298 419.756 60.4298 C496.422 27.1934 559.456 0 559.831 0 C560.205 0 560.512 0.2566 560.512 0.570216 Z");
				RectangleDouble bounds = svg.GetBounds();
				double arrowWidth = radius / 10;
				var centered = Affine.NewTranslation(-bounds.Center);
				var scalledTo1 = Affine.NewScaling(1 / bounds.Width);
				var scaledToSize = Affine.NewScaling(arrowWidth);
				var moveToRadius = Affine.NewTranslation(new Vector2(0, radius * 9 / 10));
				var moveToScreenCenter = Affine.NewTranslation(center);
				for (int i = 0; i < 4; i++)
				{
					var arrowLeftTransform = centered * scalledTo1 * scaledToSize * moveToRadius * Affine.NewRotation(MathHelper.Tau / 4 * i) * moveToScreenCenter;
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
				var scalledTo1 = Affine.NewScaling(1 / bounds.Width);
				var scaledToSize = Affine.NewScaling(arrowWidth);
				var moveToRadius = Affine.NewTranslation(new Vector2(0, radius * 16 / 15));
				var moveToScreenCenter = Affine.NewTranslation(center);
				for (int i = 0; i < 4; i++)
				{
					var arrowLeftTransform = centered * scalledTo1 * scaledToSize * Affine.NewRotation(MathHelper.Tau / 4) * moveToRadius * Affine.NewRotation(MathHelper.Tau / 8 + MathHelper.Tau / 4 * i + MathHelper.Tau / 80) * moveToScreenCenter;
					outsideArrows.Add(new VertexSourceApplyTransform(svg, arrowLeftTransform));

					var arrowRightTransform = centered * scalledTo1 * scaledToSize * Affine.NewRotation(-MathHelper.Tau / 4) * moveToRadius * Affine.NewRotation(MathHelper.Tau / 8 + MathHelper.Tau / 4 * i - MathHelper.Tau / 80) * moveToScreenCenter;
					outsideArrows.Add(new VertexSourceApplyTransform(svg, arrowRightTransform));
				}
			}
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			base.OnMouseDown(mouseEvent);

			if (!LockTrackBall && MouseCaptured)
			{
				Vector2 lastMouseMovePoint;
				lastMouseMovePoint.x = mouseEvent.X;
				lastMouseMovePoint.y = mouseEvent.Y;
				if (mouseEvent.Button == MouseButtons.Left)
				{
					if (mainTrackBallController.CurrentTrackingType == TrackBallController.MouseDownType.None)
					{
						Keys modifierKeys = ModifierKeys;
						if (modifierKeys == Keys.Shift)
						{
							mainTrackBallController.OnMouseDown(lastMouseMovePoint, Matrix4X4.Identity, TrackBallController.MouseDownType.Translation);
						}
						else if (modifierKeys == Keys.Control)
						{
							mainTrackBallController.OnMouseDown(lastMouseMovePoint, Matrix4X4.Identity, TrackBallController.MouseDownType.Scale);
						}
						else if (modifierKeys == Keys.Alt)
						{
							mainTrackBallController.OnMouseDown(lastMouseMovePoint, Matrix4X4.Identity, TrackBallController.MouseDownType.Rotation);
						}
						else switch (TransformState)
							{
								case TrackBallController.MouseDownType.Rotation:
									mainTrackBallController.OnMouseDown(lastMouseMovePoint, Matrix4X4.Identity, TrackBallController.MouseDownType.Rotation);
									break;

								case TrackBallController.MouseDownType.Translation:
									mainTrackBallController.OnMouseDown(lastMouseMovePoint, Matrix4X4.Identity, TrackBallController.MouseDownType.Translation);
									break;

								case TrackBallController.MouseDownType.Scale:
									mainTrackBallController.OnMouseDown(lastMouseMovePoint, Matrix4X4.Identity, TrackBallController.MouseDownType.Scale);
									break;
							}
					}
				}
				else if (mouseEvent.Button == MouseButtons.Middle)
				{
					if (mainTrackBallController.CurrentTrackingType == TrackBallController.MouseDownType.None)
					{
						mainTrackBallController.OnMouseDown(lastMouseMovePoint, Matrix4X4.Identity, TrackBallController.MouseDownType.Translation);
					}
				}
				else if (mouseEvent.Button == MouseButtons.Right)
				{
					if (mainTrackBallController.CurrentTrackingType == TrackBallController.MouseDownType.None)
					{
						mainTrackBallController.OnMouseDown(lastMouseMovePoint, Matrix4X4.Identity, TrackBallController.MouseDownType.Rotation);
					}
				}
			}
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			base.OnMouseMove(mouseEvent);

			Vector2 lastMouseMovePoint;
			lastMouseMovePoint.x = mouseEvent.X;
			lastMouseMovePoint.y = mouseEvent.Y;
			if (!LockTrackBall && mainTrackBallController.CurrentTrackingType != TrackBallController.MouseDownType.None)
			{
				mainTrackBallController.OnMouseMove(lastMouseMovePoint);
				Invalidate();
			}
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			if (!LockTrackBall && mainTrackBallController.CurrentTrackingType != TrackBallController.MouseDownType.None)
			{
				mainTrackBallController.OnMouseUp();
			}

			base.OnMouseUp(mouseEvent);
		}

		public override void OnMouseWheel(MouseEventArgs mouseEvent)
		{
			if (!LockTrackBall)
			{
				mainTrackBallController.OnMouseWheel(mouseEvent.WheelDelta);
				Invalidate();
			}
			base.OnMouseWheel(mouseEvent);
		}

		private void OnDrawGlContent()
		{
			if (DrawGlContent != null)
			{
				DrawGlContent(this, null);
			}
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
			modelviewMatrix = mainTrackBallController.GetTransform4X4() * modelviewMatrix;
			inverseModelviewMatrix = Matrix4X4.Invert(modelviewMatrix);
		}

		public double GetWorldUnitsPerScreenPixelAtPosition(Vector3 worldPosition)
		{
			Vector2 screenPosition = GetScreenPosition(worldPosition);
			Ray rayFromScreen = GetRayFromScreen(screenPosition);
			double distanceFromScreenToWorldPos = (worldPosition - rayFromScreen.origin).Length;

			Ray rightOnePixelRay = GetRayFromScreen(new Vector2(screenPosition.x + 1, screenPosition.y));
			Vector3 rightOnePixel = rightOnePixelRay.origin + rightOnePixelRay.directionNormal * distanceFromScreenToWorldPos;
			double distBetweenPixelsWorldSpace = (rightOnePixel - worldPosition).Length;
			return distBetweenPixelsWorldSpace;
		}
	}
}