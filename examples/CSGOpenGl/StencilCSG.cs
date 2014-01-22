using System;
using System.Diagnostics;

using OpenTK;
using OpenTK.Input;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.PolygonMesh;

namespace MatterHackers.CSGOpenGL
{
    public class StencilCSG : GuiWidget
    {
        #region Model Related
        Mesh OperandB;
        Mesh OperandA;
        float MySphereZOffset = 0f;
        float MySphereXOffset = 0f;

        int Texture;
        #endregion Model Related

        string WindowTitle;
        bool ShowDebugWireFrame;// = true;

        double CameraZoom;
        double CameraRotX;
        double CameraRotY;
        Vector3 EyePosition = new Vector3(0f, 0f, 15f);

        #region Window
        public StencilCSG(double width, double height)
            : base(width, height)
        {
        }

        public void SetViewport()
        {
            GL.Viewport(0, 0, (int)Width, (int)Height);
            GL.MatrixMode(MatrixMode.Projection);
            Matrix4 p = Matrix4.CreatePerspectiveFieldOfView((float)MathHelper.PiOver4, (float)Width / (float)Height, 0.1f, 64.0f);
            GL.LoadMatrix(ref p);
        }

        public override void OnBoundsChanged(EventArgs e)
        {
            SetViewport();
            base.OnBoundsChanged(e);
        }
        #endregion Window

        public override void OnParentChanged(EventArgs e)
        {

            #region Abort on platforms which will not be able to execute the ops properly
            /*
            if (!GL.SupportsExtension("VERSION_1_2"))
            {
                Trace.WriteLine("Aborting. OpenGL 1.2 or later required.");
                this.Exit();
            }

            int[] t = new int[2];
            GL.GetInteger(GetPName.MajorVersion, out t[0]);
            GL.GetInteger(GetPName.MinorVersion, out t[1]);
            Trace.WriteLine("OpenGL Context Version: " + t[0] + "." + t[1]);

            GL.GetInteger(GetPName.DepthBits, out t[0]);
            Trace.WriteLine("Depth Bits: " + t[0]);
            GL.GetInteger(GetPName.StencilBits, out t[1]);
            Trace.WriteLine("Stencil Bits: " + t[1]);

            if (t[0] < 16)
            {
                Trace.WriteLine("Aborting. Need at least 16 depth bits, only " + t[0] + " available.");
                this.Exit();
            }

            if (t[1] < 1)
            {
                Trace.WriteLine("Aborting. Need at least 1 stencil bit, only " + t[1] + " available.");
                this.Exit();
            }
            */
            #endregion Abort on platforms which will not be able to execute the ops properly

            WindowTitle = "Cube-Sphere Stencil CSG  " + GL.GetString(StringName.Renderer) + " (GL " + GL.GetString(StringName.Version) + ")";

            SetGLState();

            #region Load Texture
            //Bitmap bitmap = new Bitmap("Data/Textures/logo-dark.jpg");
            //bitmap.RotateFlip(RotateFlipType.RotateNoneFlipY);

            GL.GenTextures(1, out Texture);
            GL.BindTexture(TextureTarget.Texture2D, Texture);

            //BitmapData data = bitmap.LockBits(new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            //GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, data.Width, data.Height, 0, OpenTK.Graphics.OpenGL.PixelFormat.Bgra, PixelType.UnsignedByte, data.Scan0);
            //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            //GL.Finish();
            //bitmap.UnlockBits(data);
            #endregion Load Texture

            OperandA = PlatonicSolids.CreateCube(1.5, 2.0, 2.5);
            OperandB = PlatonicSolids.CreateCube(1, 1, 1);

            #region Invert Operand B's Normals
            // only the inside of the operand is ever drawn to color buffers and lighting requires this.
            foreach (Face face in OperandB.Faces)
            {
                face.normal = (face.normal * -1).GetNormal();
            }
            #endregion Invert Operand B's Normals

            base.OnParentChanged(e);
        }

        private static void SetGLState()
        {
            GL.ClearColor(.08f, .12f, .16f, 1f);

            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Less);
            GL.ClearDepth(1.0);

            GL.Enable(EnableCap.StencilTest);
            GL.ClearStencil(0);
            GL.StencilMask(0xFFFFFFFF); // read&write

            GL.Enable(EnableCap.CullFace);
            GL.FrontFace(FrontFaceDirection.Ccw);
            GL.CullFace(CullFaceMode.Back);

            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);

            GL.Color4(1f, 1f, 1f, 1f);

            GL.Enable(EnableCap.Lighting);
            GL.Enable(EnableCap.Light0);
            GL.ShadeModel(ShadingModel.Flat);
        }

        public override void Close()
        {
            GL.DeleteTextures(1, ref Texture);

            base.Close();
        }

        public override void OnMouseMove(Agg.UI.MouseEventArgs mouseEvent)
        {
            CameraRotX = -mouseEvent.X * .5f;
            CameraRotY = mouseEvent.Y * .5f;

            base.OnMouseMove(mouseEvent);
        }

        public override void OnMouseWheel(Agg.UI.MouseEventArgs mouseEvent)
        {
            CameraZoom = mouseEvent.WheelDelta * .2f;
            base.OnMouseWheel(mouseEvent);
        }

        public void DrawOperandB()
        {
            GL.PushMatrix();
            GL.Translate(Math.Cos(MySphereXOffset), -1f, Math.Cos(MySphereZOffset));
            
            RenderOpenGl.RenderMeshToGl.Render(OperandB, RGBA_Bytes.Red);

            GL.PopMatrix();
        }

        public void DrawOperandA()
        {
            //GL.Enable(EnableCap.Texture2D);
            RenderOpenGl.RenderMeshToGl.Render(OperandA, RGBA_Bytes.Blue);
            //GL.Disable(EnableCap.Texture2D);
        }

        public void RenderCsg()
        {
            // first pass
            GL.Disable(EnableCap.StencilTest);

            GL.ColorMask(false, false, false, false);
            GL.CullFace(CullFaceMode.Front);
            DrawOperandB();// draw front-faces into depth buffer

            // use stencil plane to find parts of b in a 
            GL.DepthMask(false);
            GL.Enable(EnableCap.StencilTest);
            GL.StencilFunc(StencilFunction.Always, 0, 0);

            GL.StencilOp(StencilOp.Keep, StencilOp.Keep, StencilOp.Incr);
            GL.CullFace(CullFaceMode.Back);
            DrawOperandA(); // increment the stencil where the front face of a is drawn

            GL.StencilOp(StencilOp.Keep, StencilOp.Keep, StencilOp.Decr);
            GL.CullFace(CullFaceMode.Front);
            DrawOperandA(); // decrement the stencil buffer where the back face of a is drawn

            GL.DepthMask(true);
            GL.Disable(EnableCap.DepthTest);

            GL.ColorMask(true, true, true, true);
            GL.StencilFunc(StencilFunction.Notequal, 0, 1);
            DrawOperandB(); // draw the part of b that's in a

            // fix depth
            GL.ColorMask(false, false, false, false);
            GL.Enable(EnableCap.DepthTest);
            GL.Disable(EnableCap.StencilTest);
            GL.DepthFunc(DepthFunction.Always);
            DrawOperandA();
            GL.DepthFunc(DepthFunction.Less);

            // second pass
            GL.CullFace(CullFaceMode.Back);
            DrawOperandA();

            GL.DepthMask(false);
            GL.Enable(EnableCap.StencilTest);

            GL.StencilFunc(StencilFunction.Always, 0, 0);
            GL.StencilOp(StencilOp.Keep, StencilOp.Keep, StencilOp.Incr);
            DrawOperandB(); // increment the stencil where the front face of b is drawn

            GL.StencilOp(StencilOp.Keep, StencilOp.Keep, StencilOp.Decr);
            GL.CullFace(CullFaceMode.Front);
            DrawOperandB(); // decrement the stencil buffer where the back face of b is drawn

            GL.DepthMask(true);
            GL.Disable(EnableCap.DepthTest);

            GL.ColorMask(true, true, true, true);
            GL.StencilFunc(StencilFunction.Equal, 0, 1);
            GL.CullFace(CullFaceMode.Back);
            DrawOperandA(); // draw the part of a that's in b

            GL.Enable(EnableCap.DepthTest);
        }

        Stopwatch timer = new Stopwatch();
        public override void OnDraw(Graphics2D graphics2D)
        {
            SetGLState();
            SetViewport();

            SystemWindow parent = Parent as SystemWindow;
            if (parent != null)
            {
                long ms = timer.ElapsedMilliseconds;
                parent.Title = WindowTitle + "  MS: " + ms;
                MySphereZOffset += (float)(ms / 1000.0 * 3.1);
                MySphereXOffset += (float)(ms / 1000.0 * 4.2);
                timer.Restart();
            }

            #region Transform setup
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);

            // Camera
            GL.MatrixMode(MatrixMode.Modelview);
            Matrix4 mv = Matrix4.LookAt(EyePosition, Vector3.Zero, Vector3.UnitY);
            GL.LoadMatrix(ref mv);

            GL.Translate(0f, 0f, CameraZoom);
            GL.Rotate(CameraRotX, Vector3d.UnitY);
            GL.Rotate(CameraRotY, Vector3d.UnitX);
            #endregion Transform setup

            RenderCsg();

            // ---------------------------------

            if (ShowDebugWireFrame)
            {
                GL.Color3(RGBA_Floats.Green.Red0To1, RGBA_Floats.Green.Green0To1, RGBA_Floats.Green.Blue0To1);
                GL.Disable(EnableCap.StencilTest);
                GL.Disable(EnableCap.Lighting);
                //GL.Disable( EnableCap.DepthTest );
                GL.PolygonMode(MaterialFace.Front, PolygonMode.Line);
                DrawOperandB();
                GL.PolygonMode(MaterialFace.Front, PolygonMode.Fill);
                GL.Enable(EnableCap.DepthTest);
                GL.Enable(EnableCap.Lighting);
                GL.Enable(EnableCap.StencilTest);
            }

            base.OnDraw(graphics2D);
            Invalidate();
        }
    }
}
