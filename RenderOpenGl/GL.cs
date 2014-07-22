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
    public enum ColorPointerType
    {
        UnsignedByte = 5121,
    }

    public enum NormalPointerType
    {
        Float = 5126,
    }

    public enum VertexPointerType
    {
        Float = 5126,
    }

    public enum DrawElementsType
    {
        UnsignedInt = 5125,
    }

    public enum BufferUsageHint
    {
        StaticDraw = 35044,
        DynamicDraw = 35048,
    }

    public enum BeginMode
    {
        Lines = 1,
        Triangles = 4,
        TriangleFan = 6,
    }

    public enum StringName
    {
        Extensions = 7939,
    }

    public enum BufferTarget
    {
        ArrayBuffer = 34962,
        ElementArrayBuffer = 34963,
        //PixelPackBuffer = 35051,
        //PixelUnpackBuffer = 35052,
        //UniformBuffer = 35345,
        //TextureBuffer = 35882,
        //TransformFeedbackBuffer = 35982,
        //CopyReadBuffer = 36662,
        //CopyWriteBuffer = 36663,
    }

    public enum ArrayCap
    {
        VertexArray = 32884,
        NormalArray = 32885,
        ColorArray = 32886,
        IndexArray = 32887,
        TextureCoordArray = 32888,
    }

    public enum TextureParameterName
    {
        TextureMagFilter = 10240,
        TextureMinFilter = 10241,
        TextureWrapS = 10242,
        TextureWrapT = 10243,
    }

    public enum TextureMagFilter
    {
        Nearest = 9728,
        Linear = 9729,
    }

    public enum TextureMinFilter
    {
        Linear = 9729,
        LinearMipmapLinear = 9987,
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
        Bgra = 32993,
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
        PolygonSmooth = 2881,
        DepthTest = 2929,
        Texture2D = 3553,
        ScissorTest = 3089,
        Lighting = 2896,
        Blend = 3042,
        PolygonOffsetFill = 32823,
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
            OpenTK.Graphics.OpenGL.GL.BlendFunc((OpenTK.Graphics.OpenGL.BlendingFactorSrc)sfactor, (OpenTK.Graphics.OpenGL.BlendingFactorDest)dfactor);
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

        public static void DisableClientState(ArrayCap array)
        {
            OpenTK.Graphics.OpenGL.GL.DisableClientState((OpenTK.Graphics.OpenGL.ArrayCap)array);
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

        public static void MultMatrix(float[] m)
        {
            OpenTK.Graphics.OpenGL.GL.MultMatrix(m);
        }

        public static void MultMatrix(double[] m)
        {
            OpenTK.Graphics.OpenGL.GL.MultMatrix(m);
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
                (OpenTK.Graphics.OpenGL.TextureTarget)target, level,
                (OpenTK.Graphics.OpenGL.PixelInternalFormat)internalFormat,
                width, height, border,
                (OpenTK.Graphics.OpenGL.PixelFormat)format,
                (OpenTK.Graphics.OpenGL.PixelType)type, pixels);
        }

        public static void Begin(BeginMode mode)
        {
            OpenTK.Graphics.OpenGL.GL.Begin((OpenTK.Graphics.OpenGL.BeginMode)mode);
        }

        public static void End()
        {
            OpenTK.Graphics.OpenGL.GL.End();
        }

        public static void TexCoord2(double x, double y)
        {
            OpenTK.Graphics.OpenGL.GL.TexCoord2(x, y);
        }

        public static void Vertex2(double x, double y)
        {
            OpenTK.Graphics.OpenGL.GL.Vertex2(x, y);
        }

        public static void DeleteTextures(int n, ref int textures)
        {
            OpenTK.Graphics.OpenGL.GL.DeleteTextures(n, ref textures);
        }

        public static string GetString(StringName name)
        {
            return OpenTK.Graphics.OpenGL.GL.GetString((OpenTK.Graphics.OpenGL.StringName)name);
        }

        public static void BindBuffer(BufferTarget target, int buffer)
        {
            OpenTK.Graphics.OpenGL.GL.BindBuffer((OpenTK.Graphics.OpenGL.BufferTarget)target, buffer);
        }

        public static void BufferData(BufferTarget target, IntPtr size, IntPtr data, BufferUsageHint usage)
        {
            OpenTK.Graphics.OpenGL.GL.BufferData((OpenTK.Graphics.OpenGL.BufferTarget)target, size, data, (OpenTK.Graphics.OpenGL.BufferUsageHint)usage);
        }

        public static void BufferData<T2>(BufferTarget target, IntPtr size, T2[] data, BufferUsageHint usage) where T2 : struct
        {
            OpenTK.Graphics.OpenGL.GL.BufferData((OpenTK.Graphics.OpenGL.BufferTarget)target, size, data, (OpenTK.Graphics.OpenGL.BufferUsageHint)usage);
        }

        public static void EnableClientState(ArrayCap arrayCap)
        {
            OpenTK.Graphics.OpenGL.GL.EnableClientState((OpenTK.Graphics.OpenGL.ArrayCap)arrayCap);
        }

        public static void GenBuffers(int n, out int buffers)
        {
            OpenTK.Graphics.OpenGL.GL.GenBuffers(n, out buffers);
        }

        public static void DeleteBuffers(int n, ref int buffers)
        {
            OpenTK.Graphics.OpenGL.GL.DeleteBuffers(n, ref buffers);
        }

        public static void ColorPointer(int size, ColorPointerType type, int stride, IntPtr pointer)
        {
            OpenTK.Graphics.OpenGL.GL.ColorPointer(size, (OpenTK.Graphics.OpenGL.ColorPointerType)type, stride, pointer);
        }

        public static void NormalPointer(NormalPointerType type, int stride, IntPtr pointer)
        {
            OpenTK.Graphics.OpenGL.GL.NormalPointer((OpenTK.Graphics.OpenGL.NormalPointerType)type, stride, pointer);
        }

        public static void VertexPointer(int size, VertexPointerType type, int stride, IntPtr pointer)
        {
            OpenTK.Graphics.OpenGL.GL.VertexPointer(size, (OpenTK.Graphics.OpenGL.VertexPointerType) type, stride, pointer);
        }

        public static void DrawRangeElements(BeginMode mode, int start, int end, int count, DrawElementsType type, IntPtr indices)
        {
            OpenTK.Graphics.OpenGL.GL.DrawRangeElements((OpenTK.Graphics.OpenGL.BeginMode)mode, start, end, count, (OpenTK.Graphics.OpenGL.DrawElementsType) type, indices);
        }

    }
}
