/*
Copyright (c) 2013, Lars Brubaker
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

using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using MatterHackers.Agg;
using MatterHackers.Agg.VertexSource;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;

using OpenTK.Graphics.OpenGL;

namespace MatterHackers.Agg.OpenGlGui
{
    public class TrackballTumbleWidget : GuiWidget
    {
        public event EventHandler DrawGlContent;

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
            set { mainTrackBallController = value; }
        }

        public bool LockTrackBall { get; set; }

        public TrackballTumbleWidget()
        {
            AnchorAll();
            DrawRotationHelperCircle = true;
        }

        public override void OnBoundsChanged(EventArgs e)
        {
            Vector2 screenCenter = new Vector2(Width / 2, Height / 2);
            double trackingRadius = Math.Min(Width * .45, Height * .45);
            mainTrackBallController.ScreenCenter = screenCenter;
            mainTrackBallController.TrackBallRadius = trackingRadius;

            base.OnBoundsChanged(e);
        }

        Ray lastScreenRay = new Ray(Vector3.Zero, Vector3.UnitZ);
        public Ray LastScreenRay
        {
            get { return lastScreenRay; }
        }

        void GetRayFromScreen(Vector2 screenPosition)
        {
            GuiWidget topWidget = this;
            while (topWidget.Parent != null)
            {
                topWidget = topWidget.Parent;
            }

            Vector4 rayClip = new Vector4();
            rayClip.x = (2.0 * screenPosition.x) / Width - 1.0;
            rayClip.y = (2.0 * screenPosition.y) / Height - 1.0;
            rayClip.z = -1.0;
            rayClip.w = 1.0;

            Matrix4X4 projectionMatrix = GetProjectionMatrix();

            Matrix4X4 modelviewMatrix = GetModelviewMatrix();

            Vector4 rayEye = Vector4.Transform(rayClip, Matrix4X4.Invert(projectionMatrix));
            rayEye.z = -1; rayEye.w = 0;

            Vector4 rayWorld = Vector4.Transform(rayEye, Matrix4X4.Invert(modelviewMatrix));

            Vector3 finalRayWorld = new Vector3(rayWorld).GetNormal();

            Matrix4X4 invTransform = Matrix4X4.Invert(modelviewMatrix);
            Vector3 origin = Vector3.Transform(Vector3.Zero, invTransform);
            lastScreenRay = new Ray(origin, finalRayWorld);
        }

        static NamedExecutionTimer TrackballTumbelOnDraw = new NamedExecutionTimer("TrackballTumbelOnDraw");
        static NamedExecutionTimer TrackballTumbelOnDraw1 = new NamedExecutionTimer("TrackballTumbelOnDraw1");
        static NamedExecutionTimer TrackballTumbelOnDraw2 = new NamedExecutionTimer("TrackballTumbelOnDraw2");
        static NamedExecutionTimer TrackballTumbelOnDraw3 = new NamedExecutionTimer("TrackballTumbelOnDraw3");
        static NamedExecutionTimer TrackballTumbelOnDraw4 = new NamedExecutionTimer("TrackballTumbelOnDraw4");
        static NamedExecutionTimer TrackballTumbelOnDraw5 = new NamedExecutionTimer("TrackballTumbelOnDraw5");
        public override void OnDraw(MatterHackers.Agg.Graphics2D graphics2D)
        {
            TrackballTumbelOnDraw.Start();
            TrackballTumbelOnDraw1.Start();
            SetGlContext();
            TrackballTumbelOnDraw1.Stop();
            TrackballTumbelOnDraw2.Start();
            OnDrawGlContent();
            TrackballTumbelOnDraw2.Stop();
            TrackballTumbelOnDraw3.Start();
            UnsetGlContext();
            TrackballTumbelOnDraw3.Stop();

            if (DrawRotationHelperCircle)
            {
                TrackballTumbelOnDraw4.Start();
                DrawTrackballRadius(graphics2D);
                TrackballTumbelOnDraw4.Stop();
            }

            TrackballTumbelOnDraw5.Start();
            base.OnDraw(graphics2D);
            TrackballTumbelOnDraw5.Stop();
            TrackballTumbelOnDraw.Stop();
        }

        public void DrawTrackballRadius(Graphics2D graphics2D)
        {
            var elipse = new Ellipse(mainTrackBallController.ScreenCenter, mainTrackBallController.TrackBallRadius, mainTrackBallController.TrackBallRadius);
            var outline = new Stroke(elipse, 3);
            graphics2D.Render(outline, RotationHelperCircleColor);
        }

        public override void OnMouseDown(MouseEventArgs mouseEvent)
        {
            GetRayFromScreen(mouseEvent.Position);

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
                        mainTrackBallController.OnMouseDown(lastMouseMovePoint, Matrix4X4.Identity, TrackBallController.MouseDownType.Scale);
                    }
                }
            }
        }

        public override void OnMouseMove(MouseEventArgs mouseEvent)
        {
            GetRayFromScreen(mouseEvent.Position);

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

        static NamedExecutionTimer TimerPushAttrib_Trackball = new NamedExecutionTimer("GL.PushAttrib_Trackball");
        void SetGlContext()
        {
            GL.ClearDepth(1.0);
            //GL.ClearColor(1, 1, 1, 1);
            GL.Clear(ClearBufferMask.DepthBufferBit);	// Clear the Depth Buffer
            //GL.Clear(GL._COLOR_BUFFER_BIT);	// Clear the Depth Buffer

            TimerPushAttrib_Trackball.Start();
            GL.PushAttrib(AttribMask.ViewportBit);
            TimerPushAttrib_Trackball.Stop();
            RectangleDouble screenRect = this.TransformRectangleToScreenSpace(LocalBounds);
            GL.Viewport((int)screenRect.Left, (int)screenRect.Bottom, (int)screenRect.Width, (int)screenRect.Height);

            GL.Light(LightName.Light0, LightParameter.Ambient, ambientLight);

            GL.Light(LightName.Light0, LightParameter.Diffuse, diffuseLight0);
            GL.Light(LightName.Light0, LightParameter.Specular, specularLight0);

            GL.Light(LightName.Light0, LightParameter.Ambient, new float[] { 0, 0, 0, 0 });
            GL.Light(LightName.Light1, LightParameter.Diffuse, diffuseLight1);
            GL.Light(LightName.Light1, LightParameter.Specular, specularLight1);

            GL.ShadeModel(ShadingModel.Smooth);

            GL.FrontFace(FrontFaceDirection.Ccw);
            GL.CullFace(CullFaceMode.Back);

            GL.DepthFunc(DepthFunction.Lequal);
            GL.Hint(HintTarget.PerspectiveCorrectionHint, HintMode.Nicest);

            GL.ColorMaterial(MaterialFace.FrontAndBack, ColorMaterialParameter.AmbientAndDiffuse);

            GL.Enable(EnableCap.Light0);
            GL.Enable(EnableCap.Light1);
            GL.Enable(EnableCap.DepthTest);
            GL.Enable(EnableCap.Blend);
            GL.Enable(EnableCap.Normalize);
            GL.Enable(EnableCap.Lighting);
            GL.Enable(EnableCap.ColorMaterial);

            // set the projection matrix
            GL.MatrixMode(MatrixMode.Projection);
            GL.PushMatrix();
            GL.LoadMatrix(GetProjectionMatrix().GetAsDoubleArray());

            // set the modelview matrix
            GL.MatrixMode(MatrixMode.Modelview);
            GL.PushMatrix();
            GL.LoadMatrix(GetModelviewMatrix().GetAsDoubleArray());

            Vector3 lightDirectionVector = new Vector3(lightDirection0[0], lightDirection0[1], lightDirection0[2]);
            lightDirectionVector.Normalize();
            lightDirection0[0] = (float)lightDirectionVector.x;
            lightDirection0[1] = (float)lightDirectionVector.y;
            lightDirection0[2] = (float)lightDirectionVector.z;
            GL.Light(LightName.Light0, LightParameter.Position, lightDirection0);
            GL.Light(LightName.Light1, LightParameter.Position, lightDirection1);
        }

        void UnsetGlContext()
        {
            GL.MatrixMode(MatrixMode.Projection);
            GL.PopMatrix();

            GL.MatrixMode(MatrixMode.Modelview);
            GL.PopMatrix();

            GL.Disable(EnableCap.ColorMaterial);
            GL.Disable(EnableCap.Lighting);
            GL.Disable(EnableCap.Normalize);
            GL.Disable(EnableCap.Blend);
            GL.Disable(EnableCap.DepthTest);
            GL.Disable(EnableCap.Light0);
            GL.Disable(EnableCap.Light1);

            GL.PopAttrib();
        }

        public Matrix4X4 GetProjectionMatrix()
        {
            Matrix4X4 projectionMatrix = Matrix4X4.Identity;
            Matrix4X4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(45), Width / Height, 0.1f, 100.0f, out projectionMatrix);

            return projectionMatrix;
        }

        public Matrix4X4 GetModelviewMatrix()
        {
            Matrix4X4 total = Matrix4X4.CreateTranslation(0, 0, -7);
            total = mainTrackBallController.GetTransform4X4() * total;
            return total;
        }
    }
}
