/*
Copyright (c) 2018, Lars Brubaker, John Lewin
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
using System.IO;
using MatterHackers.Agg;
using MatterHackers.RenderOpenGl.OpenGl;
using MatterHackers.VectorMath;
using OpenGL;
using ErrorCode = MatterHackers.RenderOpenGl.OpenGl.ErrorCode;
using static OpenGL.Gl;
using System.Runtime.InteropServices;
using GLFW;

namespace MatterHackers.RenderOpenGl
{
	public class GlfwGL : IOpenGL
	{
		private bool glHasBufferObjects = true;

		public GlfwGL()
		{
		}

		public bool GlHasBufferObjects { get { return glHasBufferObjects; } }

		private delegate void glBeginHandler(int mode);
		private static glBeginHandler glBegin;
		public void Begin(BeginMode mode)
		{
			if (glBegin == null)
			{
				glBegin = Marshal.GetDelegateForFunctionPointer<glBeginHandler>(Glfw.GetProcAddress("glBegin"));
			}

			glBegin((int)mode);
		}

		public void BindBuffer(BufferTarget target, int buffer)
		{
		}

		public void BindFramebuffer(int renderBuffer)
		{
		}

		public void BindRenderbuffer(int renderBuffer)
		{
		}

		public void BindTexture(TextureTarget target, int texture)
		{
		}

		public void BlendFunc(BlendingFactorSrc sfactor, BlendingFactorDest dfactor)
		{
			glBlendFunc((int)sfactor, (int)dfactor);
		}

		public void BufferData(BufferTarget target, int size, IntPtr data, BufferUsageHint usage)
		{
		}

		public void Clear(ClearBufferMask mask)
		{
		}

		public void ClearDepth(double depth)
		{
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
			ImediateMode.currentColor[0] = (byte)red;
			ImediateMode.currentColor[1] = (byte)green;
			ImediateMode.currentColor[2] = (byte)blue;
			ImediateMode.currentColor[3] = (byte)alpha;
		}

		public void ColorMask(bool red, bool green, bool blue, bool alpha)
		{
		}

		public void ColorMaterial(MaterialFace face, ColorMaterialParameter mode)
		{
		}

		public void ColorPointer(int size, ColorPointerType type, int stride, byte[] pointer)
		{
		}

		public void ColorPointer(int size, ColorPointerType type, int stride, IntPtr pointer)
		{
			throw new NotImplementedException();
		}

		public void CullFace(CullFaceMode mode)
		{
		}

		public void DeleteBuffers(int n, ref int buffers)
		{
		}

		public void DeleteFramebuffers(int n, ref int frameBuffers)
		{
		}

		public void DeleteRenderbuffers(int n, ref int renderBuffers)
		{
		}

		public void DeleteTextures(int n, ref int textures)
		{
		}

		public void DepthFunc(DepthFunction func)
		{
		}

		public void DepthMask(bool flag)
		{
		}

		public void Disable(EnableCap cap)
		{
			glDisable((int)cap);
		}

		private delegate void glDisableClientStateHandler(int state);
		private static glDisableClientStateHandler glDisableClientState;
		public void DisableClientState(ArrayCap state)
		{
			if (glDisableClientState == null)
			{
				glDisableClientState = Marshal.GetDelegateForFunctionPointer<glDisableClientStateHandler>(Glfw.GetProcAddress("glDisableClientState"));
			}

			glDisableClientState((int)state);
		}

		public void DisableGlBuffers()
		{
			glHasBufferObjects = false;
		}

		public void DisposeResources()
		{
		}

		Random rand = new Random();

		public void DrawArrays(BeginMode mode, int first, int count)
		{
			Gl.glDrawArrays((int)mode, first, count);
		}

		public void DrawRangeElements(BeginMode mode, int start, int end, int count, DrawElementsType type, IntPtr indices)
		{
		}

		public void Enable(EnableCap cap)
		{
			glEnable((int)cap);
		}

		private delegate void glEnableClientStateHandler(int arrayCap);
		private static glEnableClientStateHandler glEnableClientState;
		public void EnableClientState(ArrayCap arrayCap)
		{
			if (glEnableClientState == null)
			{
				glEnableClientState = Marshal.GetDelegateForFunctionPointer<glEnableClientStateHandler>(Glfw.GetProcAddress("glEnableClientState"));
			}

			glEnableClientState((int)arrayCap);
		}

		private delegate void glEndHandler();
		private static glEndHandler glEnd;
		public void End()
		{
			if (glEnd == null)
			{
				glEnd = Marshal.GetDelegateForFunctionPointer<glEndHandler>(Glfw.GetProcAddress("glEnd"));
			}

			glEnd();
		}

		public void Finish()
		{
		}

		public void FramebufferRenderbuffer(int renderBuffer)
		{
		}

		public void FrontFace(FrontFaceDirection mode)
		{
		}

		// start at 1 so we can use 0 as a not initialize tell.
		public void GenBuffers(int n, out int buffers)
		{
			buffers = -1;
		}

		public void GenFramebuffers(int n, out int frameBuffers)
		{
			throw new NotImplementedException();
		}

		public void GenRenderbuffers(int n, out int renderBuffers)
		{
			throw new NotImplementedException();
		}

		public void GenTextures(int n, out int textureHandle)
		{
			textureHandle = -1;
		}

		public ErrorCode GetError()
		{
			throw new NotImplementedException();
		}

		public string GetString(StringName name)
		{
			return "";
		}

		public void Light(LightName light, LightParameter pname, float[] param)
		{
		}

		private delegate void glLoadIdentityHandler();
		private static glLoadIdentityHandler glLoadIdentity;
		public void LoadIdentity()
		{
			if (glLoadIdentity == null)
			{
				glLoadIdentity = Marshal.GetDelegateForFunctionPointer<glLoadIdentityHandler>(Glfw.GetProcAddress("glLoadIdentity"));
			}

			glLoadIdentity();
		}

		public void LoadMatrix(double[] m)
		{
		}


		private delegate void glMatrixModeHandler(int mode);
		private static glMatrixModeHandler glMatrixMode;
		public void MatrixMode(MatrixMode mode)
		{
			if (glMatrixMode == null)
			{
				glMatrixMode = Marshal.GetDelegateForFunctionPointer<glMatrixModeHandler>(Glfw.GetProcAddress("glMatrixMode"));
			}

			glMatrixMode((int)mode);
		}

		private delegate void glMultMatrixHandler(float[] m);
		private static glMultMatrixHandler glMultMatrixf;
		public void MultMatrix(float[] m)
		{
			if (glMultMatrixf == null)
			{
				glMultMatrixf = Marshal.GetDelegateForFunctionPointer<glMultMatrixHandler>(Glfw.GetProcAddress("glMultMatrixf"));
			}

			glMultMatrixf(m);
		}

		public void Normal3(double x, double y, double z)
		{
		}

		public void NormalPointer(NormalPointerType type, int stride, float[] pointer)
		{
		}

		public void NormalPointer(NormalPointerType type, int stride, IntPtr pointer)
		{
		}

		private delegate void glOrthoHandler(double left, double right, double bottom, double top, double zNear, double zFar);
		private static glOrthoHandler glOrtho;
		public void Ortho(double left, double right, double bottom, double top, double zNear, double zFar)
		{
			if (glOrtho == null)
			{
				glOrtho = Marshal.GetDelegateForFunctionPointer<glOrthoHandler>(Glfw.GetProcAddress("glOrtho"));
			}

			glOrtho(left, right, bottom, top, zNear, zFar);
		}

		public void PolygonOffset(float factor, float units)
		{
		}

		public void PopAttrib()
		{
		}

		private delegate void glPopMatrixHandler();
		private static glPopMatrixHandler glPopMatrix;
		public void PopMatrix()
		{
			if (glPopMatrix == null)
			{
				glPopMatrix = Marshal.GetDelegateForFunctionPointer<glPopMatrixHandler>(Glfw.GetProcAddress("glPopMatrix"));
			}

			glPopMatrix();
		}

		public void PushAttrib(AttribMask mask)
		{
		}

		private delegate void glPushMatrixHandler();
		private static glPushMatrixHandler glPushMatrix;
		public void PushMatrix()
		{
			if (glPushMatrix == null)
			{
				glPushMatrix = Marshal.GetDelegateForFunctionPointer<glPushMatrixHandler>(Glfw.GetProcAddress("glPushMatrix"));
			}

			glPushMatrix();
		}

		public void ReadBuffer()
		{
		}

		public byte[] ReadEmbeddedAssetBytes(string name)
		{
			using (Stream stream = OpenEmbeddedAssetStream(name))
			{
				byte[] bytes = new byte[stream.Length];
				using (MemoryStream ms = new MemoryStream(bytes))
				{
					stream.CopyTo(ms);
					return bytes;
				}
			}
		}

		public void ReadPixels(int x, int y, int width, int height, OpenGl.PixelFormat pixelFormat, PixelType pixelType, byte[] buffer)
		{
		}

		public void Rotate(double angle, double x, double y, double z)
		{
		}

		public void Scale(double x, double y, double z)
		{
		}

		public void Scissor(int x, int y, int width, int height)
		{
			Gl.glScissor(x, y, width, height);
		}

		public void ShadeModel(ShadingModel model)
		{
		}

		public void TexCoord2(Vector2 uv)
		{
			TexCoord2(uv.X, uv.Y);
		}

		public void TexCoord2(double x, double y)
		{
		}

		private delegate void glTexCoordPointerHandler(int size, int type, int stride, IntPtr pointer);
		private static glTexCoordPointerHandler glTexCoordPointer;
		public void TexCoordPointer(int size, TexCordPointerType type, int stride, IntPtr pointer)
		{
			if (glTexCoordPointer == null)
			{
				glTexCoordPointer = Marshal.GetDelegateForFunctionPointer<glTexCoordPointerHandler>(Glfw.GetProcAddress("glTexCoordPointer"));
			}

			glTexCoordPointer(size, (int)type, stride, pointer);
		}

		public void TexImage2D(TextureTarget target, int level,
			PixelInternalFormat internalFormat,
			int width, int height, int border,
			OpenGl.PixelFormat format,
			PixelType type,
			Byte[] pixels)
		{
		}

		public void TexParameter(TextureTarget target, TextureParameterName pname, int param)
		{
		}

		public void Translate(MatterHackers.VectorMath.Vector3 vector)
		{
			Translate(vector.X, vector.Y, vector.Z);
		}

		public void Translate(double x, double y, double z)
		{
		}

		public void Vertex2(Vector2 position)
		{
			Vertex2(position.X, position.Y);
		}

		private delegate void glVertex2dHandler(double x, double y);
		private static glVertex2dHandler glVertex2d;
		public void Vertex2(double x, double y)
		{
			if (glVertex2d == null)
			{
				glVertex2d = Marshal.GetDelegateForFunctionPointer<glVertex2dHandler>(Glfw.GetProcAddress("glVertex2d"));
			}

			glVertex2d(x, y);
		}

		public void Vertex3(Vector3 position)
		{
			Vertex3(position.X, position.Y, position.Z);
		}

		public void Vertex3(double x, double y, double z)
		{
			throw new NotImplementedException();
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
			throw new NotImplementedException();
		}

		public void Viewport(int x, int y, int width, int height)
		{
			glViewport(x, y, width, height);
		}

		private Stream OpenEmbeddedAssetStream(string name) => GetType().Assembly.GetManifestResourceStream(name);

		public void TexEnv(TextureEnvironmentTarget target, TextureEnvParameter pname, float param)
		{
		}

		public void IndexPointer(IndexPointerType type, int stride, IntPtr pointer)
		{
			throw new NotImplementedException();
		}

		internal struct ViewPortData
		{
			internal int height;
			internal int width;
			internal int x;
			internal int y;

			public ViewPortData(int x, int y, int width, int height)
			{
				// TODO: Complete member initialization
				this.x = x;
				this.y = y;
				this.width = width;
				this.height = height;
			}
		}

		internal class ImediateMode
		{
			public static byte[] currentColor = new byte[4];
			internal VectorPOD<byte> color4b = new VectorPOD<byte>();
			internal VectorPOD<float> positions3f = new VectorPOD<float>();
			internal VectorPOD<float> textureCoords2f = new VectorPOD<float>();
			private BeginMode mode;

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
		}
	}
}