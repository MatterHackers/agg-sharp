using System;
using System.Collections.Generic;
using MatterHackers.Agg;

#if USE_OPENGL

#else
using OpenTK.Graphics.ES11;
using MatterHackers.VectorMath;
#endif

namespace MatterHackers.RenderOpenGl.OpenGl
{
#if !USE_OPENGL
	internal class ImediateMode
	{
        public static byte[] currentColor = new byte[4];
		BeginMode mode;
		internal BeginMode Mode
		{
			get { return mode; }
			set
			{
				mode = value;
                positions3f.Clear();
                color4b.Clear();
                textureCoords2f.Clear();
			}
		}

        internal VectorPOD<float> positions3f = new VectorPOD<float>();
        internal VectorPOD<byte> color4b = new VectorPOD<byte>();
		internal VectorPOD<float> textureCoords2f = new VectorPOD<float>();
	}
#endif

    public enum FrontFaceDirection
    {
        Cw = 2304,
        Ccw = 2305,
    }

    public enum CullFaceMode
    {
        Front = 1028,
        Back = 1029,
        FrontAndBack = 1032,
    }

    public enum ColorMaterialParameter
    {
        AmbientAndDiffuse = 5634,
    }

    public enum MaterialFace
    {
        Front = 1028,
        Back = 1029,
        FrontAndBack = 1032,
    }

    public enum ShadingModel
    {
        Flat = 7424,
        Smooth = 7425,
    }

    public enum DepthFunction
    {
        Lequal = 515,
    }

    public enum LightName
    {
        Light0 = 16384,
        Light1 = 16385,
    }

    public enum LightParameter
    {
        Ambient = 4608,
        Diffuse = 4609,
        Specular = 4610,
        Position = 4611,
    }

    [Flags]
    public enum ClearBufferMask
    {
        DepthBufferBit = 256,
        ColorBufferBit = 16384,
    }

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

    public enum TexCordPointerType
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
        TriangleStrip = 5,
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
        ViewportBit = 2048,
        TransformBit = 4096,
        EnableBit = 8192,
    }

    public enum EnableCap
    {
        ColorMaterial = 2903,
        CullFace = 2884,
        Normalize = 2977,
        Light0 = 16384,
        Light1 = 16385,
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
        internal struct ViewPortData
        {
            internal int x;
            internal int y;
            internal int width;
            internal int height;

            public ViewPortData(int x, int y, int width, int height)
            {
                // TODO: Complete member initialization
                this.x = x;
                this.y = y;
                this.width = width;
                this.height = height;
            }                
        }

        static ViewPortData currentViewport = new ViewPortData();
        static Stack<ViewPortData> viewportStack = new Stack<ViewPortData>();
        static Stack<AttribMask> pushedAttributStack = new Stack<AttribMask>();

#if !USE_OPENGL
		static ImediateMode currentImediateData = new ImediateMode();
		#endif

		public static void BlendFunc(BlendingFactorSrc sfactor, BlendingFactorDest dfactor)
        {
#if USE_OPENGL
            OpenTK.Graphics.OpenGL.GL.BlendFunc((OpenTK.Graphics.OpenGL.BlendingFactorSrc)sfactor, (OpenTK.Graphics.OpenGL.BlendingFactorDest)dfactor);
#else
			OpenTK.Graphics.ES11.GL.BlendFunc((OpenTK.Graphics.ES11.All)sfactor, (OpenTK.Graphics.ES11.All)dfactor);
#endif
        }

        public static void Scissor(int x, int y, int width, int height)
        {
#if USE_OPENGL
            OpenTK.Graphics.OpenGL.GL.Scissor(x, y, width, height);
#else
			OpenTK.Graphics.ES11.GL.Scissor(x, y, width, height);
#endif
        }

        public static void Enable(EnableCap cap)
        {
#if USE_OPENGL
            OpenTK.Graphics.OpenGL.GL.Enable((OpenTK.Graphics.OpenGL.EnableCap)cap);
#else
			OpenTK.Graphics.ES11.GL.Enable((OpenTK.Graphics.ES11.All)cap);
#endif
        }

        public static void Disable(EnableCap cap)
        {
#if USE_OPENGL
            OpenTK.Graphics.OpenGL.GL.Disable((OpenTK.Graphics.OpenGL.EnableCap)cap);
#else
			OpenTK.Graphics.ES11.GL.Disable((OpenTK.Graphics.ES11.All)cap);
#endif
        }

        public static void DisableClientState(ArrayCap array)
        {
#if USE_OPENGL
            OpenTK.Graphics.OpenGL.GL.DisableClientState((OpenTK.Graphics.OpenGL.ArrayCap)array);
#else
			OpenTK.Graphics.ES11.GL.DisableClientState((OpenTK.Graphics.ES11.All)array);
#endif
        }

        public static void LoadMatrix(double[] m)
        {
#if USE_OPENGL
            OpenTK.Graphics.OpenGL.GL.LoadMatrix(m);
#else
			float[] asFloats = new float[m.Length];
			for(int i=0; i<m.Length; i++)
			{
				asFloats[i] = (float)m[i];
			}

			OpenTK.Graphics.ES11.GL.LoadMatrix(asFloats);
#endif
        }

        public static void MatrixMode(MatrixMode mode)
        {
#if USE_OPENGL
            OpenTK.Graphics.OpenGL.GL.MatrixMode((OpenTK.Graphics.OpenGL.MatrixMode)mode);
#else
			OpenTK.Graphics.ES11.GL.MatrixMode((OpenTK.Graphics.ES11.All)mode);
#endif
        }

        public static void Translate(MatterHackers.VectorMath.Vector3 vector)
        {
            Translate(vector.x, vector.y, vector.z);
        }

        public static void Translate(double x, double y, double z)
        {
#if USE_OPENGL
            OpenTK.Graphics.OpenGL.GL.Translate(x, y, z);
#else
			OpenTK.Graphics.ES11.GL.Translate((float)x, (float)y, (float)z);
#endif
        }

        public static void Rotate(double angle, double x, double y, double z)
        {
#if USE_OPENGL
            OpenTK.Graphics.OpenGL.GL.Rotate(angle, x, y, z);
#else
			OpenTK.Graphics.ES11.GL.Rotate((float)angle, (float)x, (float)y, (float)z);
#endif
        }

        public static void Scale(double x, double y, double z)
        {
#if USE_OPENGL
            OpenTK.Graphics.OpenGL.GL.Scale(x, y, z);
#else
			OpenTK.Graphics.ES11.GL.Scale((float)x, (float)y, (float)z);
#endif
        }

        public static void Color4(int red, int green, int blue, int alpha)
        {
            Color4((byte)red, (byte)green, (byte)blue, (byte)alpha);
        }

        public static void Color4(byte red, byte green, byte blue, byte alpha)
        {
#if USE_OPENGL
            OpenTK.Graphics.OpenGL.GL.Color4(red, green, blue, alpha);
#else
            ImediateMode.currentColor[0] = (byte)red;
            ImediateMode.currentColor[1] = (byte)green;
            ImediateMode.currentColor[2] = (byte)blue;
            ImediateMode.currentColor[3] = (byte)alpha;

            OpenTK.Graphics.ES11.GL.Color4(ImediateMode.currentColor[0], ImediateMode.currentColor[1], ImediateMode.currentColor[2], ImediateMode.currentColor[3]);
#endif
        }

        public static void LoadIdentity()
        {
#if USE_OPENGL
            OpenTK.Graphics.OpenGL.GL.LoadIdentity();
#else
			OpenTK.Graphics.ES11.GL.LoadIdentity();
#endif
        }

        public static void PushMatrix()
        {
#if USE_OPENGL
            OpenTK.Graphics.OpenGL.GL.PushMatrix();
#else
			OpenTK.Graphics.ES11.GL.PushMatrix();
#endif
        }

        public static void MultMatrix(float[] m)
        {
#if USE_OPENGL
            OpenTK.Graphics.OpenGL.GL.MultMatrix(m);
#else
			OpenTK.Graphics.ES11.GL.MultMatrix(m);
#endif
        }

        public static void PopMatrix()
        {
#if USE_OPENGL
            OpenTK.Graphics.OpenGL.GL.PopMatrix();
#else
			OpenTK.Graphics.ES11.GL.PopMatrix();
#endif
        }

        public static void Ortho(double left, double right, double bottom, double top, double zNear, double zFar)
        {
#if USE_OPENGL
            OpenTK.Graphics.OpenGL.GL.Ortho(left, right, bottom, top, zNear, zFar);
#else
			OpenTK.Graphics.ES11.GL.Ortho((float)left, (float)right, (float)bottom, (float)top, (float)zNear, (float)zFar);
#endif
        }

        public static void PushAttrib(AttribMask mask)
        {
#if USE_OPENGL
            OpenTK.Graphics.OpenGL.GL.PushAttrib((OpenTK.Graphics.OpenGL.AttribMask)mask);
#else
            pushedAttributStack.Push(mask);
            if ((mask & AttribMask.ViewportBit) == AttribMask.ViewportBit)
            {
                viewportStack.Push(currentViewport);
            }

            if (mask != AttribMask.ViewportBit)
            {
                throw new Exception();
            }
#endif
        }

        public static void PopAttrib()
        {
#if USE_OPENGL
            OpenTK.Graphics.OpenGL.GL.PopAttrib();
#else
            AttribMask mask = pushedAttributStack.Pop();
            if ((mask & AttribMask.ViewportBit) == AttribMask.ViewportBit)
            {
                ViewPortData top = viewportStack.Pop();
                Viewport(top.x, top.y, top.width, top.height);
            }
            if (mask != AttribMask.ViewportBit)
            {
                throw new Exception();
            }
#endif
        }

        public static void GenTextures(int n, out int textureHandle)
        {
#if USE_OPENGL
            OpenTK.Graphics.OpenGL.GL.GenTextures(n, out textureHandle);
#else
			OpenTK.Graphics.ES11.GL.GenTextures(n, out textureHandle);
#endif
        }

        public static void BindTexture(TextureTarget target, int texture)
        {
#if USE_OPENGL
            OpenTK.Graphics.OpenGL.GL.BindTexture((OpenTK.Graphics.OpenGL.TextureTarget)target, texture);
#else
			OpenTK.Graphics.ES11.GL.BindTexture((OpenTK.Graphics.ES11.All)target, texture);
#endif
        }

		public static void Finish()
		{
			#if USE_OPENGL
			throw new NotImplementedException();
			#else
			OpenTK.Graphics.ES11.GL.Finish();
			#endif
		}

        public static void TexParameter(TextureTarget target, TextureParameterName pname, int param)
        {
#if USE_OPENGL
            OpenTK.Graphics.OpenGL.GL.TexParameter((OpenTK.Graphics.OpenGL.TextureTarget)target, (OpenTK.Graphics.OpenGL.TextureParameterName)pname, param);
#else
			OpenTK.Graphics.ES11.GL.TexParameterx ((OpenTK.Graphics.ES11.All)target,(OpenTK.Graphics.ES11.All)pname, param);
#endif
        }

        public static void TexImage2D(TextureTarget target, int level,
            PixelInternalFormat internalFormat,
            int width, int height, int border,
            PixelFormat format,
            PixelType type,
            Byte[] pixels)
        {
#if USE_OPENGL
            OpenTK.Graphics.OpenGL.GL.TexImage2D(
                (OpenTK.Graphics.OpenGL.TextureTarget)target, level,
                (OpenTK.Graphics.OpenGL.PixelInternalFormat)internalFormat,
                width, height, border,
                (OpenTK.Graphics.OpenGL.PixelFormat)format,
                (OpenTK.Graphics.OpenGL.PixelType)type, pixels);
#else
			OpenTK.Graphics.ES11.GL.TexImage2D(
				(OpenTK.Graphics.ES11.All)target, level,
				(int)internalFormat,
				width, height, border,
				(OpenTK.Graphics.ES11.All)format,
				(OpenTK.Graphics.ES11.All)type, pixels);
#endif
        }

        public static void Begin(BeginMode mode)
        {
#if USE_OPENGL
            OpenTK.Graphics.OpenGL.GL.Begin((OpenTK.Graphics.OpenGL.PrimitiveType)mode);
#else
			currentImediateData.Mode = mode;
#endif
        }

        public static void End()
		{
#if USE_OPENGL
            OpenTK.Graphics.OpenGL.GL.End();
#else
			switch (currentImediateData.Mode)
			{
				case BeginMode.TriangleFan:
				case BeginMode.Triangles:
				case BeginMode.TriangleStrip:
					{
                        GL.EnableClientState(ArrayCap.ColorArray);
                        GL.EnableClientState(ArrayCap.VertexArray);
						GL.EnableClientState(ArrayCap.TextureCoordArray);

						float[] v = currentImediateData.positions3f.Array;
                        byte[] c = currentImediateData.color4b.Array;
						float[] t = currentImediateData.textureCoords2f.Array;
						// pin the data, so that GC doesn't move them, while used
						// by native code
						unsafe
						{
							fixed (float* pv = v, pt = t)
							{
                                fixed (byte* pc = c)
                                {
                                    GL.ColorPointer(4, ColorPointerType.UnsignedByte, 0, new IntPtr(pc));
                                    GL.VertexPointer(2, VertexPointerType.Float, 0, new IntPtr(pv));
                                    GL.TexCoordPointer(2, TexCordPointerType.Float, 0, new IntPtr(pt));
                                    GL.DrawArrays(currentImediateData.Mode, 0, currentImediateData.positions3f.Count / 2);
                                }
							}
						}
						GL.DisableClientState(ArrayCap.VertexArray);
						GL.DisableClientState(ArrayCap.TextureCoordArray);
                        GL.DisableClientState(ArrayCap.ColorArray);
					}
					break;

				default:
					throw new NotImplementedException();
			}
#endif
		}

        public static void TexCoord2(double x, double y)
        {
#if USE_OPENGL
            OpenTK.Graphics.OpenGL.GL.TexCoord2(x, y);
#else
			currentImediateData.textureCoords2f.Add((float)x);
			currentImediateData.textureCoords2f.Add((float)y);
#endif
        }

        public static void Vertex2(double x, double y)
        {
#if USE_OPENGL
            OpenTK.Graphics.OpenGL.GL.Vertex2(x, y);
#else
			currentImediateData.positions3f.Add((float)x);
			currentImediateData.positions3f.Add((float)y);

            currentImediateData.color4b.add(ImediateMode.currentColor[0]);
            currentImediateData.color4b.add(ImediateMode.currentColor[1]);
            currentImediateData.color4b.add(ImediateMode.currentColor[2]);
            currentImediateData.color4b.add(ImediateMode.currentColor[3]);
#endif
        }

        public static void Vertex3(double x, double y, double z)
        {
#if USE_OPENGL
            OpenTK.Graphics.OpenGL.GL.Vertex3(x, y, z);
#else
			throw new NotImplementedException();
#endif
        }

        public static void DeleteTextures(int n, ref int textures)
        {
#if USE_OPENGL
            OpenTK.Graphics.OpenGL.GL.DeleteTextures(n, ref textures);
#else
			OpenTK.Graphics.ES11.GL.DeleteTextures(n, ref textures);
#endif
        }

        public static string GetString(StringName name)
        {
#if USE_OPENGL
            return OpenTK.Graphics.OpenGL.GL.GetString((OpenTK.Graphics.OpenGL.StringName)name);
#else
			return "";
#endif
        }

        public static void BindBuffer(BufferTarget target, int buffer)
        {
#if USE_OPENGL
            OpenTK.Graphics.OpenGL.GL.BindBuffer((OpenTK.Graphics.OpenGL.BufferTarget)target, buffer);
#else
			OpenTK.Graphics.ES11.GL.BindBuffer((OpenTK.Graphics.ES11.All)target, buffer);
#endif
        }

        public static void BufferData(BufferTarget target, IntPtr size, IntPtr data, BufferUsageHint usage)
        {
#if USE_OPENGL
            OpenTK.Graphics.OpenGL.GL.BufferData((OpenTK.Graphics.OpenGL.BufferTarget)target, size, data, (OpenTK.Graphics.OpenGL.BufferUsageHint)usage);
#else
			OpenTK.Graphics.ES11.GL.BufferData((OpenTK.Graphics.ES11.All)target, size, data, (OpenTK.Graphics.ES11.All)usage);
#endif
        }

        public static void BufferData<T2>(BufferTarget target, IntPtr size, T2[] data, BufferUsageHint usage) where T2 : struct
        {
#if USE_OPENGL
            OpenTK.Graphics.OpenGL.GL.BufferData((OpenTK.Graphics.OpenGL.BufferTarget)target, size, data, (OpenTK.Graphics.OpenGL.BufferUsageHint)usage);
#else
			OpenTK.Graphics.ES11.GL.BufferData((OpenTK.Graphics.ES11.All)target, size, data, (OpenTK.Graphics.ES11.All)usage);
#endif
        }

        public static void EnableClientState(ArrayCap arrayCap)
        {
#if USE_OPENGL
            OpenTK.Graphics.OpenGL.GL.EnableClientState((OpenTK.Graphics.OpenGL.ArrayCap)arrayCap);
#else
			OpenTK.Graphics.ES11.GL.EnableClientState((OpenTK.Graphics.ES11.All)arrayCap);
#endif
        }

        public static void GenBuffers(int n, out int buffers)
        {
#if USE_OPENGL
            OpenTK.Graphics.OpenGL.GL.GenBuffers(n, out buffers);
#else
			OpenTK.Graphics.ES11.GL.GenBuffers(n, out buffers);
#endif
        }

        public static void DeleteBuffers(int n, ref int buffers)
        {
#if USE_OPENGL
            OpenTK.Graphics.OpenGL.GL.DeleteBuffers(n, ref buffers);
#else
			OpenTK.Graphics.ES11.GL.DeleteBuffers(n, ref buffers);
#endif
        }

        public static void ColorPointer(int size, ColorPointerType type, int stride, byte[] pointer)
        {
#if USE_OPENGL
            OpenTK.Graphics.OpenGL.GL.ColorPointer(size, (OpenTK.Graphics.OpenGL.ColorPointerType)type, stride, pointer);
#else
			unsafe
			{
				fixed (byte* pArray = pointer)
				{
					ColorPointer(size, type, stride, new IntPtr(pArray));
				}
			}
#endif
        }

        public static void ColorPointer(int size, ColorPointerType type, int stride, IntPtr pointer)
        {
#if USE_OPENGL
            OpenTK.Graphics.OpenGL.GL.ColorPointer(size, (OpenTK.Graphics.OpenGL.ColorPointerType)type, stride, pointer);
#else
			OpenTK.Graphics.ES11.GL.ColorPointer(size, (OpenTK.Graphics.ES11.All)type, stride, pointer);
#endif
        }

        public static void NormalPointer(NormalPointerType type, int stride, float[] pointer)
        {
#if USE_OPENGL
            OpenTK.Graphics.OpenGL.GL.NormalPointer((OpenTK.Graphics.OpenGL.NormalPointerType)type, stride, pointer);
#else
			unsafe
			{
				fixed (float* pArray = pointer)
				{
					NormalPointer(type, stride, new IntPtr(pArray));
				}
			}
#endif
        }

        public static void NormalPointer(NormalPointerType type, int stride, IntPtr pointer)
        {
#if USE_OPENGL
            OpenTK.Graphics.OpenGL.GL.NormalPointer((OpenTK.Graphics.OpenGL.NormalPointerType)type, stride, pointer);
#else
            OpenTK.Graphics.ES11.GL.NormalPointer((OpenTK.Graphics.ES11.All)type, stride, pointer);
#endif
        }

        public static void VertexPointer(int size, VertexPointerType type, int stride, float[] pointer)
        {
            unsafe
            {
                fixed (float* pArray = pointer)
                {
                    VertexPointer(size, type, stride, new IntPtr(pArray));
                }
            }
        }

        public static void VertexPointer(int size, VertexPointerType type, int stride, IntPtr pointer)
        {
#if USE_OPENGL
            OpenTK.Graphics.OpenGL.GL.VertexPointer(size, (OpenTK.Graphics.OpenGL.VertexPointerType)type, stride, pointer);
#else
			OpenTK.Graphics.ES11.GL.VertexPointer(size, (OpenTK.Graphics.ES11.All)type, stride, pointer);
#endif
        }

        public static void TexCoordPointer(int size, TexCordPointerType type, int stride, IntPtr pointer)
        {
#if USE_OPENGL
            OpenTK.Graphics.OpenGL.GL.TexCoordPointer(size, (OpenTK.Graphics.OpenGL.TexCoordPointerType) type, stride, pointer);
#else
			OpenTK.Graphics.ES11.GL.TexCoordPointer(size, (OpenTK.Graphics.ES11.All) type, stride, pointer);
#endif
        }

        public static void DrawRangeElements(BeginMode mode, int start, int end, int count, DrawElementsType type, IntPtr indices)
        {
#if USE_OPENGL
            OpenTK.Graphics.OpenGL.GL.DrawRangeElements((OpenTK.Graphics.OpenGL.PrimitiveType)mode, start, end, count, (OpenTK.Graphics.OpenGL.DrawElementsType)type, indices);
#else
			throw new NotImplementedException();
#endif
        }

        public static void ClearDepth(double depth)
        {
#if USE_OPENGL
            OpenTK.Graphics.OpenGL.GL.ClearDepth(depth);
#else
			OpenTK.Graphics.ES11.GL.ClearDepth((float)depth);
#endif
        }

        public static void Viewport(int x, int y, int width, int height)
        {
            currentViewport = new ViewPortData(x, y, width, height); 
#if USE_OPENGL
            OpenTK.Graphics.OpenGL.GL.Viewport(x, y, width, height);
#else
			OpenTK.Graphics.ES11.GL.Viewport(x, y, width, height);
#endif
        }

        public static void Clear(ClearBufferMask mask)
        {
#if USE_OPENGL
            OpenTK.Graphics.OpenGL.GL.Clear((OpenTK.Graphics.OpenGL.ClearBufferMask)mask);
#else
			OpenTK.Graphics.ES11.GL.Clear((OpenTK.Graphics.ES11.ClearBufferMask)mask);
#endif
        }

        public static void Light(LightName light, LightParameter pname, float[] param)
        {
#if USE_OPENGL
            OpenTK.Graphics.OpenGL.GL.Light((OpenTK.Graphics.OpenGL.LightName)light, (OpenTK.Graphics.OpenGL.LightParameter)pname, param);
#else
			//throw new NotImplementedException();
#endif
        }

        public static void ShadeModel(ShadingModel model)
        {
#if USE_OPENGL
            OpenTK.Graphics.OpenGL.GL.ShadeModel((OpenTK.Graphics.OpenGL.ShadingModel)model);
#else
			OpenTK.Graphics.ES11.GL.ShadeModel ((OpenTK.Graphics.ES11.All)model);
#endif
        }

        public static void FrontFace(FrontFaceDirection mode)
        {
#if USE_OPENGL
            OpenTK.Graphics.OpenGL.GL.FrontFace((OpenTK.Graphics.OpenGL.FrontFaceDirection)mode);
#else
			OpenTK.Graphics.ES11.GL.FrontFace((OpenTK.Graphics.ES11.All)mode);
#endif
        }

        public static void CullFace(CullFaceMode mode)
        {
#if USE_OPENGL
            OpenTK.Graphics.OpenGL.GL.CullFace((OpenTK.Graphics.OpenGL.CullFaceMode)mode);
#else
			OpenTK.Graphics.ES11.GL.CullFace((OpenTK.Graphics.ES11.All)mode);
#endif
        }

        public static void DepthFunc(DepthFunction func)
        {
#if USE_OPENGL
            OpenTK.Graphics.OpenGL.GL.DepthFunc((OpenTK.Graphics.OpenGL.DepthFunction)func);
#else
			OpenTK.Graphics.ES11.GL.DepthFunc((OpenTK.Graphics.ES11.All)func);
#endif
        }

        public static void ColorMaterial(MaterialFace face, ColorMaterialParameter mode)
        {
#if USE_OPENGL
            OpenTK.Graphics.OpenGL.GL.ColorMaterial((OpenTK.Graphics.OpenGL.MaterialFace)face, (OpenTK.Graphics.OpenGL.ColorMaterialParameter)mode);
#else
			//throw new NotImplementedException();
#endif
        }

        public static void DrawArrays(BeginMode mode, int first, int count)
        {
#if USE_OPENGL
            OpenTK.Graphics.OpenGL.GL.DrawArrays((OpenTK.Graphics.OpenGL.PrimitiveType)mode, first, count);
#else
			OpenTK.Graphics.ES11.GL.DrawArrays((OpenTK.Graphics.ES11.All)mode, first, count);
#endif
        }

        public static void PolygonOffset(float factor, float units)
        {
#if USE_OPENGL
            OpenTK.Graphics.OpenGL.GL.PolygonOffset(factor, units);
#else
			throw new NotImplementedException();
#endif
        }
    }
}
