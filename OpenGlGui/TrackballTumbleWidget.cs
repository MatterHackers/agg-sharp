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
using System;

using MatterHackers.Agg.VertexSource;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;

using MatterHackers.RenderOpenGl.OpenGl;

namespace MatterHackers.Agg.OpenGlGui
{
    public class TrackballTumbleWidget : GuiWidget
    {
        public event EventHandler DrawGlContent;

		bool doOpenGlDrawing = true;
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

        float[] ambientLight = { 0.2f, 0.2f, 0.2f, 1.0f };
        
        float[] diffuseLight0 = { 0.7f, 0.7f, 0.7f, 1.0f };
        float[] specularLight0 = { 0.5f, 0.5f, 0.5f, 1.0f };
        float[] lightDirection0 = { -1, -1, 1, 0.0f };

        float[] diffuseLight1 = { 0.5f, 0.5f, 0.5f, 1.0f };
        float[] specularLight1 = { 0.3f, 0.3f, 0.3f, 1.0f };
        float[] lightDirection1 = { 1, 1, 1, 0.0f };

        RGBA_Bytes rotationHelperCircleColor = new RGBA_Bytes(RGBA_Bytes.Black, 200);
        public RGBA_Bytes RotationHelperCircleColor
        {
            get { return rotationHelperCircleColor; }
            set { rotationHelperCircleColor = value; }
        }

        public bool DrawRotationHelperCircle { get; set; }

        TrackBallController mainTrackBallController = new TrackBallController();
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

		void TrackBallController_TransformChanged(object sender, EventArgs e)
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

        public void DrawTrackballRadius(Graphics2D graphics2D)
        {
            var elipse = new Ellipse(mainTrackBallController.ScreenCenter, mainTrackBallController.TrackBallRadius, mainTrackBallController.TrackBallRadius);
            var outline = new Stroke(elipse, 3);
            graphics2D.Render(outline, RotationHelperCircleColor);
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
                        else switch(TransformState)
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

        void GradientBand(double startHeight, double endHeight, int startColor, int endColor)
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

        void ClearToGradient()
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

        void SetGlContext()
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

        void UnsetGlContext()
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

		Matrix4X4 projectionMatrix;
		public Matrix4X4 ProjectionMatrix { get { return projectionMatrix; } }
		Matrix4X4 inverseProjectionMatrix;
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

		Matrix4X4 modelviewMatrix;
		public Matrix4X4 ModelviewMatrix { get { return modelviewMatrix; } }
		Matrix4X4 inverseModelviewMatrix;
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
