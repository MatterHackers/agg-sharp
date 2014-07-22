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

//#define AA_TIPS

using System;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;
using MatterHackers.RenderOpenGl.OpenGl;
using MatterHackers.VectorMath;
using Tesselate;

namespace MatterHackers.RenderOpenGl
{
    public abstract class VertexTesselatorAbstract : Tesselator
    {
        public abstract void AddVertex(double x, double y);
    }

    public class Graphics2DOpenGL : Graphics2D
    {
        public bool ForceTexturedEdgeAntiAliasing = true;
        RenderToGLTesselator RenderNowTesselator = new RenderToGLTesselator();

        int width;
        int height;
        public Graphics2DOpenGL(int width, int height)
        {
            this.width = width;
            this.height = height;
        }

        RectangleDouble cachedClipRect;
        public override RectangleDouble GetClippingRect()
        {
            return cachedClipRect;
        }

        public override void SetClippingRect(RectangleDouble clippingRect)
        {
			cachedClipRect = clippingRect;
            GL.Scissor((int)Math.Floor(Math.Max(clippingRect.Left, 0)), (int)Math.Floor(Math.Max(clippingRect.Bottom, 0)),
                (int)Math.Ceiling(Math.Max(clippingRect.Width, 0)), (int)Math.Ceiling(Math.Max(clippingRect.Height, 0)));
			GL.Enable(EnableCap.ScissorTest);
        }

        public override IScanlineCache ScanlineCache
        {
            get { return null; }
            set { throw new Exception("There is no scanline cache on a GL surface."); }
        }

        public void PushOrthoProjection()
        {
			GL.PushAttrib(AttribMask.TransformBit | AttribMask.EnableBit);

            GL.MatrixMode(MatrixMode.Projection);
            GL.PushMatrix();
            GL.LoadIdentity();
            GL.Ortho(0, width, 0, height, 0, 1);

            GL.MatrixMode(MatrixMode.Modelview);
            GL.PushMatrix();
            GL.LoadIdentity();
        }

        public void PopOrthoProjection()
        {
			GL.MatrixMode(MatrixMode.Projection);
            GL.PopMatrix();
            GL.MatrixMode(MatrixMode.Modelview);
            GL.PopMatrix();
            GL.PopAttrib();
        }

        public static void SendShapeToTesselator(VertexTesselatorAbstract tesselator, IVertexSource vertexSource)
        {
            tesselator.BeginPolygon();

            ShapePath.FlagsAndCommand PathAndFlags = 0;
            double x, y;
            bool haveBegunContour = false;
            while (!ShapePath.is_stop(PathAndFlags = vertexSource.vertex(out x, out y)))
            {
                if (ShapePath.is_close(PathAndFlags)
                    || (haveBegunContour && ShapePath.is_move_to(PathAndFlags)))
                {
                    tesselator.EndContour();
                    haveBegunContour = false;
                }

                if (!ShapePath.is_close(PathAndFlags))
                {
                    if (!haveBegunContour)
                    {
                        tesselator.BeginContour();
                        haveBegunContour = true;
                    }

                    tesselator.AddVertex(x, y);
                }
            }

            if (haveBegunContour)
            {
                tesselator.EndContour();
            }

            tesselator.EndPolygon();
        }

		static byte[] CreateBufferForAATexture()
		{
			byte[] hardwarePixelBuffer = new byte[1024 * 4 * 4];
			for (int y = 0; y < 4; y++)
			{
				byte alpha = 0;
				for (int x = 0; x < 1024; x++)
				{
					hardwarePixelBuffer[(y * 1024 + x) * 4 + 0] = 255;
					hardwarePixelBuffer[(y * 1024 + x) * 4 + 1] = 255;
					hardwarePixelBuffer[(y * 1024 + x) * 4 + 2] = 255;
					hardwarePixelBuffer[(y * 1024 + x) * 4 + 3] = alpha;
					alpha = 255;
				}
			}
			return hardwarePixelBuffer;
		}

        static int AATextureHandle = -1;
        void CheckLineImageCache()
        {
            if (AATextureHandle == -1)
            {
                // Create the texture handle and display list handle
                GL.GenTextures(1, out AATextureHandle);

                // Set up some texture parameters for openGL
                GL.BindTexture(TextureTarget.Texture2D, AATextureHandle);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);

                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

                byte[] hardwarePixelBuffer = CreateBufferForAATexture();

                // Create the texture
                GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, 1024, 4,
                    0, PixelFormat.Rgba, PixelType.UnsignedByte, hardwarePixelBuffer);
            }
        }

        void DrawAAShape(IVertexSource vertexSource)
        {
			CheckLineImageCache();
            GL.Enable(EnableCap.Texture2D);
            GL.BindTexture(TextureTarget.Texture2D, AATextureHandle);

            AARenderToGLTesselator triangleEddgeInfo = new AARenderToGLTesselator();
            Graphics2DOpenGL.SendShapeToTesselator(triangleEddgeInfo, vertexSource);

            // now render it
            triangleEddgeInfo.RenderLastToGL();
        }

        public override void Render(IVertexSource vertexSource, int pathIndexToRender, RGBA_Bytes colorBytes)
        {
			PushOrthoProjection();

            GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);
            GL.Enable(EnableCap.Blend);

            vertexSource.rewind(pathIndexToRender);

            RGBA_Floats color = colorBytes.GetAsRGBA_Floats();

            GL.Color4(color.red, color.green, color.blue, color.alpha);

            Affine transform = GetTransform();
            if (!transform.is_identity())
            {
                vertexSource = new VertexSourceApplyTransform(vertexSource, transform);
            }

            if (ForceTexturedEdgeAntiAliasing)
            {
                DrawAAShape(vertexSource);
            }
            else
            {
                Graphics2DOpenGL.SendShapeToTesselator(RenderNowTesselator, vertexSource);
            }

            PopOrthoProjection();
        }

        public override void Render(IImageByte source,
            double x, double y,
            double angleRadians,
            double scaleX, double scaleY)
        {
#if true
            Affine transform = GetTransform();
            if (!transform.is_identity())
            {
                if (scaleX != 1 || scaleY != 1)// || angleDegrees != 0)
                {
                    throw new NotImplementedException();
                }
                // TODO: <BUG> make this do rotation and scalling
                transform.transform(ref x, ref y);
                scaleX *= transform.sx;
                scaleY *= transform.sy;
            }
#endif

#if true
            // TODO: <BUG> make this do rotation and scalling
            RectangleInt sourceBounds = source.GetBounds();
            sourceBounds.Offset((int)x, (int)y);
            RectangleInt destBounds = new RectangleInt((int)cachedClipRect.Left, (int)cachedClipRect.Bottom, (int)cachedClipRect.Right, (int)cachedClipRect.Top);

            if (!RectangleInt.DoIntersect(sourceBounds, destBounds))
            {
                if (scaleX != 1 || scaleY != 1)// || angleDegrees != 0)
                {
                    //throw new NotImplementedException();
                }
                //return;
            }
#endif

            ImageBuffer sourceAsImageBuffer = (ImageBuffer)source;
            ImageGlPlugin glPlugin = ImageGlPlugin.GetImageGlPlugin(sourceAsImageBuffer, false);

            // Prepare openGL for rendering
            PushOrthoProjection();
			GL.Disable(EnableCap.Lighting);
            GL.Enable(EnableCap.Texture2D);
            GL.Disable(EnableCap.DepthTest);
            
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);
            
            GL.Translate(x, y, 0);
            GL.Rotate(MathHelper.RadiansToDegrees(angleRadians), 0, 0, 1);
            GL.Scale(scaleX, scaleY, 1);

            RGBA_Bytes color = RGBA_Bytes.White;
            GL.Color4(color.Red0To1, color.Green0To1, color.Blue0To1, color.Alpha0To1);

            glPlugin.DrawToGL();

            //Restore openGL state
            PopOrthoProjection();
        }

        public override void Render(IImageFloat imageSource,
            double x, double y,
            double angleDegrees,
            double scaleX, double ScaleY)
        {
            throw new NotImplementedException();
        }

        public override void Clear(IColorType color)
        {
            Affine transform = GetTransform();

            RoundedRect clearRect = new RoundedRect(new RectangleDouble(
                0 - transform.tx, width - transform.ty,
                0 - transform.tx, height - transform.ty), 0);
            Render(clearRect, color.GetAsRGBA_Bytes());
        }
    }
}