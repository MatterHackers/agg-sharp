using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

#if USE_GLES
using OpenTK.Graphics.ES11;
#elif USE_OPENGL
using OpenTK.Graphics.OpenGL;
#endif

namespace MatterHackers.RenderOpenGl.OpenGl
{
    public enum TextureParameterName
    {
        TextureMagFilter = 10240,
        TextureMinFilter = 10241,
        TextureWrapS = 10242,
        TextureWrapT = 10243,
    }

    public enum TextureMagFilter
    {
        Linear = 9729,
    }

    public enum TextureMinFilter
    {
        Linear = 9729,
    }

    public enum TextureWrapMode
    {
        ClampToEdge = 33071,
    }

    public enum PixelInternalFormat
    {
        Rgba = 6408,
    }

    public enum PixelFormat
    {
        Rgba = 6408,
    }

    public enum PixelType
    {
        UnsignedByte = 5121,
    }

    public enum TextureTarget
    {
        Texture2D = 3553,
    }

    public enum BlendingFactorSrc
    {
        SrcAlpha = 770,
    }

    public enum BlendingFactorDest
    {
        OneMinusSrcAlpha = 771,
    }

    public enum AttribMask
    {
        TransformBit = 4096,
        EnableBit = 8192,
    }

    public enum EnableCap
    {
        DepthTest = 2929,
        Texture2D = 3553,
        ScissorTest = 3089,
        Lighting = 2896,
        Blend = 3042,
    }

    public enum MatrixMode
    {
        Projection = 5889,
        Modelview = 5888,
    }

    public static class GL
    {
        public static void BlendFunc(BlendingFactorSrc sfactor, BlendingFactorDest dfactor)
        { 
            OpenTK.Graphics.OpenGL.GL.BlendFunc((OpenTK.Graphics.OpenGL.BlendingFactorSrc)sfactor, (OpenTK.Graphics.OpenGL.BlendingFactorDest) dfactor);
        }

        public static void Scissor(int x, int y, int width, int height)
        {
            OpenTK.Graphics.OpenGL.GL.Scissor(x, y, width, height);
        }

        public static void Enable(EnableCap cap)
        {
#if USE_OPENGL
            OpenTK.Graphics.OpenGL.GL.Enable((OpenTK.Graphics.OpenGL.EnableCap)cap);
#elif USE_GLES
            OpenTK.Graphics.OpenGL.GL.Enable((OpenTK.Graphics.OpenGL.All)cap);
#endif
        }

        public static void Disable(EnableCap cap)
        {
            OpenTK.Graphics.OpenGL.GL.Disable((OpenTK.Graphics.OpenGL.EnableCap)cap);
        }

        public static void MatrixMode(MatrixMode mode)
        {
            OpenTK.Graphics.OpenGL.GL.MatrixMode((OpenTK.Graphics.OpenGL.MatrixMode)mode);
        }

        public static void Translate(double x, double y, int z)
        {
            OpenTK.Graphics.OpenGL.GL.Translate(x, y, z);
        }

        public static void Rotate(double angle, int x, int y, int z)
        {
            OpenTK.Graphics.OpenGL.GL.Rotate(angle, x, y, z);
        }

        public static void Scale(double x, double y, int z)
        {
            OpenTK.Graphics.OpenGL.GL.Scale(x, y, z);
        }

        public static void Color4(float red, float green, float blue, float alpha)
        {
            OpenTK.Graphics.OpenGL.GL.Color4(red, green, blue, alpha);
        }

        public static void LoadIdentity()
        {
            OpenTK.Graphics.OpenGL.GL.LoadIdentity();
        }

        public static void PushMatrix()
        {
            OpenTK.Graphics.OpenGL.GL.PushMatrix();
        }

        public static void PopMatrix()
        {
            OpenTK.Graphics.OpenGL.GL.PopMatrix();
        }

        public static void Ortho(double left, double right, double bottom, double top, double zNear, double zFar)
        {
            OpenTK.Graphics.OpenGL.GL.Ortho(left, right, bottom, top, zNear, zFar);
        }

        public static void PushAttrib(AttribMask mask)
        {
            OpenTK.Graphics.OpenGL.GL.PushAttrib((OpenTK.Graphics.OpenGL.AttribMask)mask);
        }

        public static void PopAttrib()
        {
            OpenTK.Graphics.OpenGL.GL.PopAttrib();
        }

        public static void GenTextures(int n, out int textureHandle)
        {
            OpenTK.Graphics.OpenGL.GL.GenTextures(n, out textureHandle);
        }

        public static void BindTexture(TextureTarget target, int texture)
        {
            OpenTK.Graphics.OpenGL.GL.BindTexture((OpenTK.Graphics.OpenGL.TextureTarget)target, texture);
        }

        public static void TexParameter(TextureTarget target, TextureParameterName pname, int param)
        {
            OpenTK.Graphics.OpenGL.GL.TexParameter((OpenTK.Graphics.OpenGL.TextureTarget)target, (OpenTK.Graphics.OpenGL.TextureParameterName)pname, param);
        }

        public static void TexImage2D(TextureTarget target, int level, 
            PixelInternalFormat internalFormat, 
            int width, int height, int border, 
            PixelFormat format,
            PixelType type,
            Byte[] pixels)
        {
            OpenTK.Graphics.OpenGL.GL.TexImage2D(
                (OpenTK.Graphics.OpenGL.TextureTarget) target, level, 
                (OpenTK.Graphics.OpenGL.PixelInternalFormat) internalFormat,
                width, height, border, 
                (OpenTK.Graphics.OpenGL.PixelFormat) format, 
                (OpenTK.Graphics.OpenGL.PixelType)type, pixels);
        }        
    }
}
