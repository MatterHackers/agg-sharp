/*
Copyright (c) 2018, Lars Brubaker
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
using MatterHackers.RenderOpenGl.OpenGl;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.UI
{
#if !USE_OPENGL
	internal class ImediateMode
	{
        public static byte[] currentColor = new byte[4];
		BeginMode mode;
		internal BeginMode Mode
		{
			get
			{
				return mode; 
			}
			set
			{
				mode = value;
                positions3f.Clear();
                color4b.Clear();
                textureCoords2f.Clear();
			}
		}

		internal int vertexCount;
        internal VectorPOD<float> positions3f = new VectorPOD<float>();
        internal VectorPOD<byte> color4b = new VectorPOD<byte>();
		internal VectorPOD<float> textureCoords2f = new VectorPOD<float>();
	}
#endif

	public class OpenTkGl : IOpenGL
	{
#if __ANDROID__
		bool glHasBufferObjects = false;
#else
#endif

		public bool HardwareAvailable { get; set; } = true;

		public bool GlHasBufferObjects { get; private set; } = true;

		public void DisableGlBuffers()
		{
			GlHasBufferObjects = false;
		}

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

		private ViewPortData currentViewport = new ViewPortData();
		private Stack<ViewPortData> viewportStack = new Stack<ViewPortData>();
		private Stack<AttribMask> pushedAttributStack = new Stack<AttribMask>();

#if !USE_OPENGL
		ImediateMode currentImediateData = new ImediateMode();
#endif

		public void BlendFunc(BlendingFactorSrc sfactor, BlendingFactorDest dfactor)
		{
#if USE_OPENGL
			if (HardwareAvailable)
			{
				OpenTK.Graphics.OpenGL.GL.BlendFunc((OpenTK.Graphics.OpenGL.BlendingFactorSrc)sfactor, (OpenTK.Graphics.OpenGL.BlendingFactorDest)dfactor);
			}
#else
			OpenTK.Graphics.ES11.GL.BlendFunc((OpenTK.Graphics.ES11.All)sfactor, (OpenTK.Graphics.ES11.All)dfactor);
#endif
		}

		public void TexEnv(TextureEnvironmentTarget target, TextureEnvParameter pname, float param)
		{
#if USE_OPENGL
			OpenTK.Graphics.OpenGL.GL.TexEnv((OpenTK.Graphics.OpenGL.TextureEnvTarget)target, (OpenTK.Graphics.OpenGL.TextureEnvParameter)pname, param);
#else
			OpenTK.Graphics.ES11.GL.TexEnv((OpenTK.Graphics.ES11.All)target, (OpenTK.Graphics.ES11.All)pname, param);
#endif
		}

		public void Scissor(int x, int y, int width, int height)
		{
#if USE_OPENGL
			if (HardwareAvailable)
			{
				OpenTK.Graphics.OpenGL.GL.Scissor(x, y, width, height);
			}
#else
			OpenTK.Graphics.ES11.GL.Scissor(x, y, width, height);
#endif
		}

		public void Enable(EnableCap cap)
		{
#if USE_OPENGL
			if (HardwareAvailable)
			{
				OpenTK.Graphics.OpenGL.GL.Enable((OpenTK.Graphics.OpenGL.EnableCap)cap);
			}
#else
			OpenTK.Graphics.ES11.GL.Enable((OpenTK.Graphics.ES11.All)cap);
#endif
		}

		public void ColorMask(bool red, bool green, bool blue, bool alpha)
		{
#if USE_OPENGL
			if (HardwareAvailable)
			{
				OpenTK.Graphics.OpenGL.GL.ColorMask(red, green, blue, alpha);
			}
#else
			OpenTK.Graphics.ES11.GL.ColorMask(red, green, blue, alpha);
#endif

		}


		public void Disable(EnableCap cap)
		{
#if USE_OPENGL
			if (HardwareAvailable)
			{
				OpenTK.Graphics.OpenGL.GL.Disable((OpenTK.Graphics.OpenGL.EnableCap)cap);
			}
#else
			OpenTK.Graphics.ES11.GL.Disable((OpenTK.Graphics.ES11.All)cap);
#endif
		}

		public void DisableClientState(ArrayCap array)
		{
#if USE_OPENGL
			if (HardwareAvailable)
			{
				OpenTK.Graphics.OpenGL.GL.DisableClientState((OpenTK.Graphics.OpenGL.ArrayCap)array);
			}
#else
			OpenTK.Graphics.ES11.GL.DisableClientState((OpenTK.Graphics.ES11.All)array);
#endif
		}

		public void LoadMatrix(double[] m)
		{
#if USE_OPENGL
			if (HardwareAvailable)
			{
				OpenTK.Graphics.OpenGL.GL.LoadMatrix(m);
			}
#else
			float[] asFloats = new float[m.Length];
			for(int i=0; i<m.Length; i++)
			{
				asFloats[i] = (float)m[i];
			}

			OpenTK.Graphics.ES11.GL.LoadMatrix(asFloats);
#endif
		}

		public void MatrixMode(MatrixMode mode)
		{
#if USE_OPENGL
			if (HardwareAvailable)
			{
				OpenTK.Graphics.OpenGL.GL.MatrixMode((OpenTK.Graphics.OpenGL.MatrixMode)mode);
			}
#else
			OpenTK.Graphics.ES11.GL.MatrixMode((OpenTK.Graphics.ES11.All)mode);
#endif
		}

		public void Translate(MatterHackers.VectorMath.Vector3 vector)
		{
			if (HardwareAvailable)
			{
				Translate(vector.X, vector.Y, vector.Z);
			}
		}

		public void Translate(double x, double y, double z)
		{
#if USE_OPENGL
			if (HardwareAvailable)
			{
				OpenTK.Graphics.OpenGL.GL.Translate(x, y, z);
			}
#else
			OpenTK.Graphics.ES11.GL.Translate((float)x, (float)y, (float)z);
#endif
		}

		public void Rotate(double angle, double x, double y, double z)
		{
#if USE_OPENGL
			if (HardwareAvailable)
			{
				OpenTK.Graphics.OpenGL.GL.Rotate(angle, x, y, z);
			}
#else
			OpenTK.Graphics.ES11.GL.Rotate((float)angle, (float)x, (float)y, (float)z);
#endif
		}

		public void Scale(double x, double y, double z)
		{
#if USE_OPENGL
			if (HardwareAvailable)
			{
				OpenTK.Graphics.OpenGL.GL.Scale(x, y, z);
			}
#else
			OpenTK.Graphics.ES11.GL.Scale((float)x, (float)y, (float)z);
#endif
		}

		public void Color4(Color color)
		{
			Color4(color.red, color.green, color.blue, color.alpha);
		}

		public void Color4(int red, int green, int blue, int alpha)
		{
			Color4((byte)red, (byte)green, (byte)blue, (byte)alpha);
		}

		public void Color4(byte red, byte green, byte blue, byte alpha)
		{
#if USE_OPENGL
			if (HardwareAvailable)
			{
				OpenTK.Graphics.OpenGL.GL.Color4(red, green, blue, alpha);
			}
#else
            ImediateMode.currentColor[0] = (byte)red;
            ImediateMode.currentColor[1] = (byte)green;
            ImediateMode.currentColor[2] = (byte)blue;
            ImediateMode.currentColor[3] = (byte)alpha;

            OpenTK.Graphics.ES11.GL.Color4(ImediateMode.currentColor[0], ImediateMode.currentColor[1], ImediateMode.currentColor[2], ImediateMode.currentColor[3]);
#endif
		}

		public void LoadIdentity()
		{
#if USE_OPENGL
			if (HardwareAvailable)
			{
				OpenTK.Graphics.OpenGL.GL.LoadIdentity();
			}
#else
			OpenTK.Graphics.ES11.GL.LoadIdentity();
#endif
		}

		public void PushMatrix()
		{
#if USE_OPENGL
			if (HardwareAvailable)
			{
				OpenTK.Graphics.OpenGL.GL.PushMatrix();
			}
#else
			OpenTK.Graphics.ES11.GL.PushMatrix();
#endif
		}

		public void MultMatrix(float[] m)
		{
#if USE_OPENGL
			if (HardwareAvailable)
			{
				OpenTK.Graphics.OpenGL.GL.MultMatrix(m);
			}
#else
			OpenTK.Graphics.ES11.GL.MultMatrix(m);
#endif
		}

		public void PopMatrix()
		{
#if USE_OPENGL
			if (HardwareAvailable)
			{
				OpenTK.Graphics.OpenGL.GL.PopMatrix();
			}
#else
			OpenTK.Graphics.ES11.GL.PopMatrix();
#endif
		}

		public void Ortho(double left, double right, double bottom, double top, double zNear, double zFar)
		{
#if USE_OPENGL
			if (HardwareAvailable)
			{
				OpenTK.Graphics.OpenGL.GL.Ortho(left, right, bottom, top, zNear, zFar);
			}
#else
			OpenTK.Graphics.ES11.GL.Ortho((float)left, (float)right, (float)bottom, (float)top, (float)zNear, (float)zFar);
#endif
		}

		public void PushAttrib(AttribMask mask)
		{
#if USE_OPENGL
			if (HardwareAvailable)
			{
				OpenTK.Graphics.OpenGL.GL.PushAttrib((OpenTK.Graphics.OpenGL.AttribMask)mask);
			}
#else
            pushedAttributStack.Push(mask);
            if ((mask & AttribMask.ViewportBit) == AttribMask.ViewportBit)
            {
                viewportStack.Push(currentViewport);
            }

            if (mask != AttribMask.ViewportBit)
            {
                //throw new Exception();
            }
#endif
		}

		public void PopAttrib()
		{
#if USE_OPENGL
			if (HardwareAvailable)
			{
				OpenTK.Graphics.OpenGL.GL.PopAttrib();
			}
#else
            AttribMask mask = pushedAttributStack.Pop();
            if ((mask & AttribMask.ViewportBit) == AttribMask.ViewportBit)
            {
                ViewPortData top = viewportStack.Pop();
                Viewport(top.x, top.y, top.width, top.height);
            }
            if (mask != AttribMask.ViewportBit)
            {
                //throw new Exception();
            }
#endif
		}

		public int GenTexture()
		{
#if USE_OPENGL
			OpenTK.Graphics.OpenGL.GL.GenTextures(1, out int textureHandle);
			return textureHandle;
#else
			OpenTK.Graphics.ES11.GL.GenTextures(n, out textureHandle);
#endif
		}

		public void BindTexture(TextureTarget target, int texture)
		{
#if USE_OPENGL
			if (HardwareAvailable)
			{
				OpenTK.Graphics.OpenGL.GL.BindTexture((OpenTK.Graphics.OpenGL.TextureTarget)target, texture);
			}
#else
			OpenTK.Graphics.ES11.GL.BindTexture((OpenTK.Graphics.ES11.All)target, texture);
#endif
		}

		public void Finish()
		{
#if USE_OPENGL
			throw new NotImplementedException();
#else
			OpenTK.Graphics.ES11.GL.Finish();
#endif
		}

		public void TexParameter(TextureTarget target, TextureParameterName pname, int param)
		{
#if USE_OPENGL
			if (HardwareAvailable)
			{
				OpenTK.Graphics.OpenGL.GL.TexParameter((OpenTK.Graphics.OpenGL.TextureTarget)target, (OpenTK.Graphics.OpenGL.TextureParameterName)pname, param);
			}
#else
			OpenTK.Graphics.ES11.GL.TexParameterx ((OpenTK.Graphics.ES11.All)target,(OpenTK.Graphics.ES11.All)pname, param);
#endif
		}

		public void TexImage2D(TextureTarget target, int level,
			PixelInternalFormat internalFormat,
			int width, int height, int border,
			PixelFormat format,
			PixelType type,
			Byte[] pixels)
		{
#if USE_OPENGL
			if (HardwareAvailable)
			{
				OpenTK.Graphics.OpenGL.GL.TexImage2D(
					(OpenTK.Graphics.OpenGL.TextureTarget)target, level,
					(OpenTK.Graphics.OpenGL.PixelInternalFormat)internalFormat,
					width, height, border,
					(OpenTK.Graphics.OpenGL.PixelFormat)format,
					(OpenTK.Graphics.OpenGL.PixelType)type, pixels);
			}
#else
			OpenTK.Graphics.ES11.GL.TexImage2D(
				(OpenTK.Graphics.ES11.All)target, level,
				(int)internalFormat,
				width, height, border,
				(OpenTK.Graphics.ES11.All)format,
				(OpenTK.Graphics.ES11.All)type, pixels);
#endif
		}

		public ErrorCode GetError()
		{
#if USE_OPENGL
			var error = (ErrorCode)OpenTK.Graphics.OpenGL.GL.GetError();

			return error;
#else
			throw new NotImplementedException();
#endif
		}

		public void Begin(BeginMode mode)
		{
#if USE_OPENGL
			if (HardwareAvailable)
			{
				OpenTK.Graphics.OpenGL.GL.Begin((OpenTK.Graphics.OpenGL.BeginMode)mode);
			}
#else
			currentImediateData.Mode = mode;
#endif
		}

		public void End()
		{
#if USE_OPENGL
			if (HardwareAvailable)
			{
				OpenTK.Graphics.OpenGL.GL.End();
			}
#else
			switch (currentImediateData.Mode)
			{
				case BeginMode.Lines:
					{
						GL.EnableClientState(ArrayCap.ColorArray);
						GL.EnableClientState(ArrayCap.VertexArray);

						float[] v = currentImediateData.positions3f.Array;
						byte[] c = currentImediateData.color4b.Array;
						// pin the data, so that GC doesn't move them, while used
						// by native code
						unsafe
						{
							fixed (float* pv = v)
							{
								fixed (byte* pc = c)
								{
									GL.ColorPointer(4, ColorPointerType.UnsignedByte, 0, new IntPtr(pc));
									GL.VertexPointer(currentImediateData.vertexCount, VertexPointerType.Float, 0, new IntPtr(pv));
									GL.DrawArrays(currentImediateData.Mode, 0, currentImediateData.positions3f.Count / currentImediateData.vertexCount);
								}
							}
						}
						GL.DisableClientState(ArrayCap.VertexArray);
						GL.DisableClientState(ArrayCap.ColorArray);
					}
					break;

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
									GL.VertexPointer(currentImediateData.vertexCount, VertexPointerType.Float, 0, new IntPtr(pv));
                                    GL.TexCoordPointer(2, TexCordPointerType.Float, 0, new IntPtr(pt));
                                    GL.DrawArrays(currentImediateData.Mode, 0, currentImediateData.positions3f.Count / currentImediateData.vertexCount);
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

		public void TexCoord2(Vector2 uv)
		{
			TexCoord2(uv.X, uv.Y);
		}

		public void TexCoord2(double x, double y)
		{
#if USE_OPENGL
			if (HardwareAvailable)
			{
				OpenTK.Graphics.OpenGL.GL.TexCoord2(x, y);
			}
#else
			currentImediateData.textureCoords2f.Add((float)x);
			currentImediateData.textureCoords2f.Add((float)y);
#endif
		}

		public void Vertex2(Vector2 position)
		{
			Vertex2(position.X, position.Y);
		}

		public void Vertex2(double x, double y)
		{
#if USE_OPENGL
			if (HardwareAvailable)
			{
				OpenTK.Graphics.OpenGL.GL.Vertex2(x, y);
			}
#else
			currentImediateData.vertexCount = 2;
			currentImediateData.positions3f.Add((float)x);
			currentImediateData.positions3f.Add((float)y);

            currentImediateData.color4b.add(ImediateMode.currentColor[0]);
            currentImediateData.color4b.add(ImediateMode.currentColor[1]);
            currentImediateData.color4b.add(ImediateMode.currentColor[2]);
            currentImediateData.color4b.add(ImediateMode.currentColor[3]);
#endif
		}

		public void Normal3(double x, double y, double z)
		{
#if USE_OPENGL
			if (HardwareAvailable)
			{
				OpenTK.Graphics.OpenGL.GL.Normal3(x, y, z);
			}
#else
#endif
		}

		public void Vertex3(Vector3 position)
		{
			Vertex3(position.X, position.Y, position.Z);
		}

		public void Vertex3(double x, double y, double z)
		{
#if USE_OPENGL
			if (HardwareAvailable)
			{
				OpenTK.Graphics.OpenGL.GL.Vertex3(x, y, z);
			}
#else
			currentImediateData.vertexCount = 3;
			currentImediateData.positions3f.Add((float)x);
			currentImediateData.positions3f.Add((float)y);
			currentImediateData.positions3f.Add((float)z);

            currentImediateData.color4b.add(ImediateMode.currentColor[0]);
            currentImediateData.color4b.add(ImediateMode.currentColor[1]);
            currentImediateData.color4b.add(ImediateMode.currentColor[2]);
            currentImediateData.color4b.add(ImediateMode.currentColor[3]);
#endif
		}

		public void DeleteTexture(int texture)
		{
#if USE_OPENGL
			if (HardwareAvailable)
			{
				OpenTK.Graphics.OpenGL.GL.DeleteTextures(1, ref texture);
			}
#else
			OpenTK.Graphics.ES11.GL.DeleteTextures(n, ref textures);
#endif
		}

		public string GetString(StringName name)
		{
#if USE_OPENGL
			if (HardwareAvailable)
			{
				return OpenTK.Graphics.OpenGL.GL.GetString((OpenTK.Graphics.OpenGL.StringName)name);
			}
			return "";
#else
			return "";
#endif
		}

		public void BindBuffer(BufferTarget target, int buffer)
		{
#if USE_OPENGL
			if (HardwareAvailable)
			{
				if (GlHasBufferObjects)
				{
					OpenTK.Graphics.OpenGL.GL.BindBuffer((OpenTK.Graphics.OpenGL.BufferTarget)target, buffer);
				}
				else
				{
					switch (target)
					{
						case BufferTarget.ArrayBuffer:
							currentArrayBufferIndex = buffer;
							break;

						case BufferTarget.ElementArrayBuffer:
							currentElementArrayBufferIndex = buffer;
							break;

						default:
							throw new NotImplementedException();
					}
				}
			}
#else
			if (glHasBufferObjects)
			{
				OpenTK.Graphics.ES11.GL.BindBuffer((OpenTK.Graphics.ES11.All)target, buffer);
			}
			else
			{
				switch (target)
				{
					case BufferTarget.ArrayBuffer:
						currentArrayBufferIndex = buffer;
						break;

					case BufferTarget.ElementArrayBuffer:
						currentElementArrayBufferIndex = buffer;
						break;

					default:
						throw new NotImplementedException();
				}
			}
#endif
		}

		public void BufferData(BufferTarget target, int size, IntPtr data, BufferUsageHint usage)
		{
#if USE_OPENGL
			if (HardwareAvailable)
			{
				if (GlHasBufferObjects)
				{
					OpenTK.Graphics.OpenGL.GL.BufferData((OpenTK.Graphics.OpenGL.BufferTarget)target, (IntPtr)size, data, (OpenTK.Graphics.OpenGL.BufferUsageHint)usage);
				}
				else
				{
					byte[] dataCopy = new byte[size];
					unsafe
					{
						for (int i = 0; i < size; i++)
						{
							dataCopy[i] = ((byte*)data)[i];
						}
					}

					switch (target)
					{
						case BufferTarget.ArrayBuffer:
							if (currentArrayBufferIndex == 0)
							{
								throw new Exception("You don't have a ArrayBuffer set.");
							}
							bufferData[currentArrayBufferIndex] = dataCopy;
							break;

						case BufferTarget.ElementArrayBuffer:
							if (currentElementArrayBufferIndex == 0)
							{
								throw new Exception("You don't have an EllementArrayBuffer set.");
							}
							bufferData[currentElementArrayBufferIndex] = dataCopy;
							break;

						default:
							throw new NotImplementedException();
					}
				}
			}
#else
			if (glHasBufferObjects)
			{
				OpenTK.Graphics.ES11.GL.BufferData((OpenTK.Graphics.ES11.All)target, (IntPtr)size, data, (OpenTK.Graphics.ES11.All)usage);
			}
			else
			{
				byte[] dataCopy = new byte[size];
				unsafe
				{
					for (int i = 0; i < size; i++)
					{
						dataCopy[i] = ((byte*)data)[i];
					}
				}

				switch (target)
				{
					case BufferTarget.ArrayBuffer:
						if (currentArrayBufferIndex == 0)
						{
							throw new Exception("You don't have a ArrayBuffer set.");
						}
						bufferData[currentArrayBufferIndex] = dataCopy;
						break;

					case BufferTarget.ElementArrayBuffer:
						if (currentElementArrayBufferIndex == 0)
						{
							throw new Exception("You don't have an EllementArrayBuffer set.");
						}
						bufferData[currentElementArrayBufferIndex] = dataCopy;
						break;

					default:
						throw new NotImplementedException();
				}
			}
#endif
		}

		public void EnableClientState(ArrayCap arrayCap)
		{
#if USE_OPENGL
			if (HardwareAvailable)
			{
				if (GlHasBufferObjects || arrayCap != ArrayCap.IndexArray) // don't set index array if we don't have buffer objects (we will render through DrawElements instead).
				{
					OpenTK.Graphics.OpenGL.GL.EnableClientState((OpenTK.Graphics.OpenGL.ArrayCap)arrayCap);
				}
			}
#else
			if (glHasBufferObjects || arrayCap != ArrayCap.IndexArray) // don't set index array if we don't have buffer objects (we will render through DrawElements instead).
			{
				OpenTK.Graphics.ES11.GL.EnableClientState((OpenTK.Graphics.ES11.All)arrayCap);
			}
#endif
		}

		int currentArrayBufferIndex = 0;
		int currentElementArrayBufferIndex = 0;
		int genBuffersIndex = 1; // start at 1 so we can use 0 as a not initialize tell.
		Dictionary<int, byte[]> bufferData = new Dictionary<int, byte[]>();

		public int GenBuffer()
		{
#if USE_OPENGL
			if (HardwareAvailable)
			{
				if (GlHasBufferObjects)
				{
					OpenTK.Graphics.OpenGL.GL.GenBuffers(1, out int buffers);
					return buffers;
				}
				else
				{
					int buffer = genBuffersIndex++;
					bufferData.Add(buffer, new byte[1]);
					return buffer;
				}
			}

			return 0;
#else
			if (glHasBufferObjects)
			{
				OpenTK.Graphics.ES11.GL.GenBuffers(n, out buffers);
			}
			else
			{
				if (n != 1)
				{
					throw new Exception("Can only handle 1 gen count at the moment.");
				}
				buffers = genBuffersIndex++;
				bufferData.Add(buffers, new byte[1]);
			}
#endif
		}

		public void DeleteBuffer(int buffer)
		{
#if USE_OPENGL
			if (HardwareAvailable)
			{
				if (GlHasBufferObjects)
				{
					OpenTK.Graphics.OpenGL.GL.DeleteBuffers(1, ref buffer);
				}
				else
				{
					bufferData.Remove(buffer);
				}
			}
#else
			if (glHasBufferObjects)
			{
				OpenTK.Graphics.ES11.GL.DeleteBuffers(n, ref buffers);
			}
			else
			{
				if (n != 1)
				{
					throw new Exception("Can only handle 1 delete count at the moment.");
				}
				bufferData.Remove(buffers);
			}
#endif
		}

		public void ColorPointer(int size, ColorPointerType type, int stride, byte[] pointer)
		{
#if USE_OPENGL
			if (HardwareAvailable)
			{
				OpenTK.Graphics.OpenGL.GL.ColorPointer(size, (OpenTK.Graphics.OpenGL.ColorPointerType)type, stride, pointer);
			}
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

		public void ColorPointer(int size, ColorPointerType type, int stride, IntPtr pointer)
		{
#if USE_OPENGL
			if (HardwareAvailable)
			{
				if (GlHasBufferObjects || currentArrayBufferIndex == 0)
				{
					// we are rending from memory so operate normally
					OpenTK.Graphics.OpenGL.GL.ColorPointer(size, (OpenTK.Graphics.OpenGL.ColorPointerType)type, stride, pointer);
				}
				else
				{
					unsafe
					{
						fixed (byte* buffer = bufferData[currentArrayBufferIndex])
						{
							OpenTK.Graphics.OpenGL.GL.ColorPointer(size, (OpenTK.Graphics.OpenGL.ColorPointerType)type, stride, new IntPtr(&buffer[(int)pointer]));
						}
					}
				}
			}
#else
			if (glHasBufferObjects || currentArrayBufferIndex == 0)
			{
				OpenTK.Graphics.ES11.GL.ColorPointer(size, (OpenTK.Graphics.ES11.All)type, stride, pointer);
			}
			else
			{
				unsafe
				{
					fixed (byte* buffer = bufferData[currentArrayBufferIndex])
					{
						OpenTK.Graphics.ES11.GL.ColorPointer(size, (OpenTK.Graphics.ES11.All)type, stride, new IntPtr(&buffer[(int)pointer]));
					}
				}
			}
#endif
		}

		public void NormalPointer(NormalPointerType type, int stride, float[] pointer)
		{
#if USE_OPENGL
			if (HardwareAvailable)
			{
				OpenTK.Graphics.OpenGL.GL.NormalPointer((OpenTK.Graphics.OpenGL.NormalPointerType)type, stride, pointer);
			}
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

		public void NormalPointer(NormalPointerType type, int stride, IntPtr pointer)
		{
#if USE_OPENGL
			if (HardwareAvailable)
			{
				if (GlHasBufferObjects || currentArrayBufferIndex == 0)
				{
					OpenTK.Graphics.OpenGL.GL.NormalPointer((OpenTK.Graphics.OpenGL.NormalPointerType)type, stride, pointer);
				}
				else
				{
					unsafe
					{
						fixed (byte* buffer = bufferData[currentArrayBufferIndex])
						{
							OpenTK.Graphics.OpenGL.GL.NormalPointer((OpenTK.Graphics.OpenGL.NormalPointerType)type, stride, new IntPtr(&buffer[(int)pointer]));
						}
					}
				}
			}
#else
			if (glHasBufferObjects || currentArrayBufferIndex == 0)
			{
				OpenTK.Graphics.ES11.GL.NormalPointer((OpenTK.Graphics.ES11.All)type, stride, pointer);
			}
			else
			{
				unsafe
				{
					fixed (byte* buffer = bufferData[currentArrayBufferIndex])
					{
						OpenTK.Graphics.ES11.GL.NormalPointer((OpenTK.Graphics.ES11.All)type, stride, new IntPtr(&buffer[(int)pointer]));
					}
				}
			}
#endif
		}

		public void IndexPointer(IndexPointerType type, int stride, IntPtr pointer)
		{
#if USE_OPENGL
			if (HardwareAvailable)
			{
				if (GlHasBufferObjects || currentArrayBufferIndex == 0)
				{
					OpenTK.Graphics.OpenGL.GL.IndexPointer((OpenTK.Graphics.OpenGL.IndexPointerType)type, stride, pointer);
				}
				else
				{
					unsafe
					{
						fixed (byte* buffer = bufferData[currentArrayBufferIndex])
						{
							OpenTK.Graphics.OpenGL.GL.IndexPointer((OpenTK.Graphics.OpenGL.IndexPointerType)type, stride, new IntPtr(&buffer[(int)pointer]));
						}
					}
				}
			}
#endif
		}

		public void VertexPointer(int size, VertexPointerType type, int stride, float[] pointer)
		{
			unsafe
			{
				fixed (float* pArray = pointer)
				{
					VertexPointer(size, type, stride, new IntPtr(pArray));
				}
			}
		}

		public void VertexPointer(int size, VertexPointerType type, int stride, IntPtr pointer)
		{
#if USE_OPENGL
			if (HardwareAvailable)
			{
				if (GlHasBufferObjects || currentArrayBufferIndex == 0)
				{
					OpenTK.Graphics.OpenGL.GL.VertexPointer(size, (OpenTK.Graphics.OpenGL.VertexPointerType)type, stride, pointer);
				}
				else
				{
					unsafe
					{
						fixed (byte* buffer = bufferData[currentArrayBufferIndex])
						{
							OpenTK.Graphics.OpenGL.GL.VertexPointer(size, (OpenTK.Graphics.OpenGL.VertexPointerType)type, stride, new IntPtr(&buffer[(int)pointer]));
						}
					}
				}
			}
#else
			if (glHasBufferObjects || currentArrayBufferIndex == 0)
			{
				OpenTK.Graphics.ES11.GL.VertexPointer(size, (OpenTK.Graphics.ES11.All)type, stride, pointer);
			}
			else
			{
				unsafe
				{
					fixed (byte* buffer = bufferData[currentArrayBufferIndex])
					{
						OpenTK.Graphics.ES11.GL.VertexPointer(size, (OpenTK.Graphics.ES11.All)type, stride, new IntPtr(&buffer[(int)pointer]));
					}
				}
			}
#endif
		}

		public void TexCoordPointer(int size, TexCordPointerType type, int stride, IntPtr pointer)
		{
#if USE_OPENGL
			if (HardwareAvailable)
			{
				OpenTK.Graphics.OpenGL.GL.TexCoordPointer(size, (OpenTK.Graphics.OpenGL.TexCoordPointerType)type, stride, pointer);
			}
#else
			OpenTK.Graphics.ES11.GL.TexCoordPointer(size, (OpenTK.Graphics.ES11.All) type, stride, pointer);
#endif
		}

		public void DrawRangeElements(BeginMode mode, int start, int end, int count, DrawElementsType type, IntPtr indices)
		{
#if USE_OPENGL
			if (HardwareAvailable)
			{
				if (GlHasBufferObjects)
				{
					OpenTK.Graphics.OpenGL.GL.DrawRangeElements((OpenTK.Graphics.OpenGL.BeginMode)mode, start, end, count, (OpenTK.Graphics.OpenGL.DrawElementsType)type, indices);
				}
				else
				{
					unsafe
					{
						fixed (byte* buffer = bufferData[currentElementArrayBufferIndex])
						{
							byte* passedBuffer = &buffer[(int)indices];
							OpenTK.Graphics.OpenGL.GL.DrawElements((OpenTK.Graphics.OpenGL.BeginMode)mode, count, (OpenTK.Graphics.OpenGL.DrawElementsType)type, new IntPtr(passedBuffer));
						}
					}
				}
			}
#else
			if (glHasBufferObjects)
			{
				throw new NotImplementedException();
			}
			else
			{
				unsafe
				{
					fixed (byte* buffer = bufferData[currentElementArrayBufferIndex])
					{
						byte* passedBuffer = &buffer[(int)indices];
						OpenTK.Graphics.ES11.GL.DrawElements((OpenTK.Graphics.ES11.All)mode, count, (OpenTK.Graphics.ES11.All)type, new IntPtr(passedBuffer));
					}
				}
			}
#endif
		}

		public void DepthMask(bool flag)
		{
#if USE_OPENGL
			if (HardwareAvailable)
			{
				OpenTK.Graphics.OpenGL.GL.DepthMask(flag);
			}
#else
			OpenTK.Graphics.ES11.GL.DepthMask(flag);
#endif
		}

		public void ClearDepth(double depth)
		{
#if USE_OPENGL
			if (HardwareAvailable)
			{
				OpenTK.Graphics.OpenGL.GL.ClearDepth(depth);
			}
#else
			OpenTK.Graphics.ES11.GL.ClearDepth((float)depth);
#endif
		}

		public void Viewport(int x, int y, int width, int height)
		{
			currentViewport = new ViewPortData(x, y, width, height);
#if USE_OPENGL
			if (HardwareAvailable)
			{
				OpenTK.Graphics.OpenGL.GL.Viewport(x, y, width, height);
			}
#else
			OpenTK.Graphics.ES11.GL.Viewport(x, y, width, height);
#endif
		}

		public void Clear(ClearBufferMask mask)
		{
#if USE_OPENGL
			if (HardwareAvailable)
			{
				OpenTK.Graphics.OpenGL.GL.Clear((OpenTK.Graphics.OpenGL.ClearBufferMask)mask);
			}
#else
			OpenTK.Graphics.ES11.GL.Clear((OpenTK.Graphics.ES11.ClearBufferMask)mask);
#endif
		}

		public void Light(LightName light, LightParameter pname, float[] param)
		{
#if USE_OPENGL
			if (HardwareAvailable)
			{
				OpenTK.Graphics.OpenGL.GL.Light((OpenTK.Graphics.OpenGL.LightName)light, (OpenTK.Graphics.OpenGL.LightParameter)pname, param);
			}
#else
			//throw new NotImplementedException();
#endif
		}

		public void ShadeModel(ShadingModel model)
		{
#if USE_OPENGL
			if (HardwareAvailable)
			{
				OpenTK.Graphics.OpenGL.GL.ShadeModel((OpenTK.Graphics.OpenGL.ShadingModel)model);
			}
#else
			OpenTK.Graphics.ES11.GL.ShadeModel ((OpenTK.Graphics.ES11.All)model);
#endif
		}

		public void FrontFace(FrontFaceDirection mode)
		{
#if USE_OPENGL
			if (HardwareAvailable)
			{
				OpenTK.Graphics.OpenGL.GL.FrontFace((OpenTK.Graphics.OpenGL.FrontFaceDirection)mode);
			}
#else
			OpenTK.Graphics.ES11.GL.FrontFace((OpenTK.Graphics.ES11.All)mode);
#endif
		}

		public void CullFace(CullFaceMode mode)
		{
#if USE_OPENGL
			if (HardwareAvailable)
			{
				OpenTK.Graphics.OpenGL.GL.CullFace((OpenTK.Graphics.OpenGL.CullFaceMode)mode);
			}
#else
			OpenTK.Graphics.ES11.GL.CullFace((OpenTK.Graphics.ES11.All)mode);
#endif
		}

		public void DepthFunc(DepthFunction func)
		{
#if USE_OPENGL
			if (HardwareAvailable)
			{
				OpenTK.Graphics.OpenGL.GL.DepthFunc((OpenTK.Graphics.OpenGL.DepthFunction)func);
			}
#else
			OpenTK.Graphics.ES11.GL.DepthFunc((OpenTK.Graphics.ES11.All)func);
#endif
		}

		public void ColorMaterial(MaterialFace face, ColorMaterialParameter mode)
		{
#if USE_OPENGL
			if (HardwareAvailable)
			{
				OpenTK.Graphics.OpenGL.GL.ColorMaterial((OpenTK.Graphics.OpenGL.MaterialFace)face, (OpenTK.Graphics.OpenGL.ColorMaterialParameter)mode);
			}
#else
			//throw new NotImplementedException();
#endif
		}

		public void DrawArrays(BeginMode mode, int first, int count)
		{
#if USE_OPENGL
			if (HardwareAvailable)
			{
				OpenTK.Graphics.OpenGL.GL.DrawArrays((OpenTK.Graphics.OpenGL.BeginMode)mode, first, count);
			}
#else
			OpenTK.Graphics.ES11.GL.DrawArrays((OpenTK.Graphics.ES11.All)mode, first, count);
#endif
		}

		public void PolygonOffset(float factor, float units)
		{
#if USE_OPENGL
			if (HardwareAvailable)
			{
				OpenTK.Graphics.OpenGL.GL.PolygonOffset(factor, units);
			}
#else
			throw new NotImplementedException();
#endif
		}
	}
}