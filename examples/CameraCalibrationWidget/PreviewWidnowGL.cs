/*
Copyright (c) 2012, Lars Brubaker
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
using System.Collections.Generic;
using System.Linq;
using System.Text;

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;
using Tao.OpenGl;
using RayTracer;

namespace MatterHackers.MatterCad
{
    public class PreviewWindowGL : RectangleWidget
    {
        public delegate void DrawGlContentEventHandler(object sender, EventArgs e);
        public event DrawGlContentEventHandler DrawGlContent;

        float[] ambientLight = { 0.2f, 0.2f, 0.2f, 1.0f };
        float[] diffuseLight = { 0.7f, 0.7f, 0.7f, 1.0f };
        float[] specularLight = { 0.5f, 0.5f, 0.5f, 1.0f };
        float[] position = { -1, 1, 1, 0.0f };

        public TrackBallController mainTrackBallController;

        public bool LockTrackBall { get; set; }

        public PreviewWindowGL()
        {
            CanFocus = false;
        }

        public override void OnResize()
        {
            Vector2 screenCenter = new Vector2(Width / 2, Height / 2);
            double trackingRadius = Math.Min(Width * .45, Height * .45);
            if (mainTrackBallController == null)
            {
                mainTrackBallController = new TrackBallController(screenCenter, trackingRadius);
                mainTrackBallController.Scale = .1;
            }
            else
            {
                mainTrackBallController.ScreenCenter = screenCenter;
                mainTrackBallController.TrackBallRadius = trackingRadius;
            }

            base.OnResize();
        }

        public override void OnDraw(MatterHackers.Agg.Graphics2D graphics2D)
        {
            //graphics2D.Clear(new RGBA_Bytes(255, 255, 255, 0));
            //Gl.glClearColor(1, 1, 1, 1);
            Gl.glClear(Gl.GL_DEPTH_BUFFER_BIT);	// Clear the Depth Buffer
            //Gl.glClear(Gl.GL_COLOR_BUFFER_BIT);	// Clear the Depth Buffer

            SetGlContext();
            SetTransforms(Width, Height);

            Gl.glTranslatef(0.0f, 0.0f, -7.0f);

            Gl.glPushMatrix();
            Gl.glMultMatrixd(mainTrackBallController.GetTransform4X4().GetAsDoubleArray());

            OnDrawGlContent();

            Gl.glPopMatrix();
            Gl.glDisable(Gl.GL_LIGHTING);

            UnsetGlContext();

            mainTrackBallController.DrawRadius(graphics2D);

            base.OnDraw(graphics2D);
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
                        mainTrackBallController.OnMouseDown(lastMouseMovePoint, Matrix4X4.Identity, TrackBallController.MouseDownType.Rotation);
                    }
                }
                else if (mouseEvent.Button == MouseButtons.Middle)
                {
                    if (mainTrackBallController.CurrentTrackingType == TrackBallController.MouseDownType.None)
                    {
                        mainTrackBallController.OnMouseDown(lastMouseMovePoint, Matrix4X4.Identity, TrackBallController.MouseDownType.Translation);
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

        int[] oldViewport = new int[4];
        void SetGlContext()
        {
            Gl.glGetIntegerv(Gl.GL_VIEWPORT, oldViewport);
            rect_d screenRect = this.RectToScreen(LocalBounds);
            Gl.glViewport((int)screenRect.Left, (int)screenRect.Bottom, (int)screenRect.Width, (int)screenRect.Height);

            Gl.glLightfv(Gl.GL_LIGHT0, Gl.GL_AMBIENT, ambientLight);
            Gl.glLightfv(Gl.GL_LIGHT0, Gl.GL_DIFFUSE, diffuseLight);
            Gl.glLightfv(Gl.GL_LIGHT0, Gl.GL_SPECULAR, specularLight);
            Gl.glEnable(Gl.GL_LIGHT0);

            Gl.glShadeModel(Gl.GL_SMOOTH);								// enable smooth shading
            Gl.glClearDepth(1.0f);										// depth buffer setup
            Gl.glEnable(Gl.GL_DEPTH_TEST);								// enables depth testing
            Gl.glEnable(Gl.GL_BLEND);
            Gl.glEnable(Gl.GL_NORMALIZE);

            Gl.glFrontFace(Gl.GL_CCW);
            //Gl.glEnable(Gl.GL_CULL_FACE); // Enable cull face
            Gl.glCullFace(Gl.GL_BACK);    // Cull the back face (don't display)

            Gl.glDepthFunc(Gl.GL_LEQUAL);								// type of depth test
            Gl.glHint(Gl.GL_PERSPECTIVE_CORRECTION_HINT, Gl.GL_NICEST);	// nice perspective calculations

            Gl.glEnable(Gl.GL_LIGHTING);
            Gl.glColorMaterial(Gl.GL_FRONT_AND_BACK, Gl.GL_AMBIENT_AND_DIFFUSE);
            Gl.glEnable(Gl.GL_COLOR_MATERIAL);
        }

        void UnsetGlContext()
        {
            Gl.glDisable(Gl.GL_LIGHTING);
            Gl.glDisable(Gl.GL_CULL_FACE);
            Gl.glViewport(oldViewport[0], oldViewport[1], oldViewport[2], oldViewport[3]);
        }

        void SetTransforms(double width, double height)
        {
            Gl.glMatrixMode(Gl.GL_PROJECTION);
            Matrix4X4 viewMatrix = Matrix4X4.Identity;
            Matrix4X4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(45), width / height, 0.1f, 100.0f, out viewMatrix);
            Gl.glLoadMatrixd(viewMatrix.GetAsDoubleArray());

            Gl.glMatrixMode(Gl.GL_MODELVIEW);
            Gl.glLoadIdentity();

            Gl.glLightfv(Gl.GL_LIGHT0, Gl.GL_POSITION, position);
        }
    }
}
