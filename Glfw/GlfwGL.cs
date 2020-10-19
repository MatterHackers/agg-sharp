﻿/*
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
using System.IO;
using System.Runtime.InteropServices;
using GLFW;
using MatterHackers.Agg;
using MatterHackers.RenderOpenGl.OpenGl;
using MatterHackers.VectorMath;
using OpenGL;
using static OpenGL.Gl;
using ErrorCode = MatterHackers.RenderOpenGl.OpenGl.ErrorCode;

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
			Gl.glBindBuffer((int)target, (uint)buffer);
		}

		public void BindFramebuffer(int renderBuffer)
		{
			throw new NotImplementedException();
		}

		public void BindRenderbuffer(int renderBuffer)
		{
			throw new NotImplementedException();
		}

		public void BindTexture(TextureTarget target, int texture)
		{
			Gl.glBindTexture((int)target, (uint)texture);
		}

		public void BlendFunc(BlendingFactorSrc sfactor, BlendingFactorDest dfactor)
		{
			glBlendFunc((int)sfactor, (int)dfactor);
		}

		public void BufferData(BufferTarget target, int size, IntPtr data, BufferUsageHint usage)
		{
			throw new NotImplementedException();
		}

		private delegate void glClearHandler(int mask);
		private static glClearHandler glClear;
		public void Clear(ClearBufferMask mask)
		{
			if (glClear == null)
			{
				glClear = Marshal.GetDelegateForFunctionPointer<glClearHandler>(Glfw.GetProcAddress("glClear"));
			}

			glClear((int)mask);
		}

		public void ClearDepth(double depth)
		{
			Gl.glClearDepth(depth);
		}

		public void Color4(Color color)
		{
			Color4(color.red, color.green, color.blue, color.alpha);
		}

		public void Color4(int red, int green, int blue, int alpha)
		{
			Color4((byte)red, (byte)green, (byte)blue, (byte)alpha);
		}

		private delegate void glColor4fHandler(float red, float green, float blue, float alpha);
		private static glColor4fHandler glColor4f;
		public void Color4(byte red, byte green, byte blue, byte alpha)
		{
			if (glColor4f == null)
			{
				glColor4f = Marshal.GetDelegateForFunctionPointer<glColor4fHandler>(Glfw.GetProcAddress("glColor4f"));
			}

			glColor4f(red / 255.0F, green / 255.0F, blue / 255.0F, alpha / 255.0F);
		}

		public void ColorMask(bool red, bool green, bool blue, bool alpha)
		{
			throw new NotImplementedException();
		}

		private delegate void glColorMaterialHandler(int face, int mode);
		private static glColorMaterialHandler glColorMaterial;
		public void ColorMaterial(MaterialFace face, ColorMaterialParameter mode)
		{
			if (glColorMaterial == null)
			{
				glColorMaterial = Marshal.GetDelegateForFunctionPointer<glColorMaterialHandler>(Glfw.GetProcAddress("glColorMaterial"));
			}

			glColorMaterial((int)face, (int)mode);
		}

		public void ColorPointer(int size, ColorPointerType type, int stride, byte[] pointer)
		{
			unsafe
			{
				fixed (byte* pArray = pointer)
				{
					ColorPointer(size, type, stride, new IntPtr(pArray));
				}
			}
		}

		private delegate void glColorPointerHandler(int size, int type, int stride, IntPtr pointer);
		private static glColorPointerHandler glColorPointer;
		public void ColorPointer(int size, ColorPointerType type, int stride, IntPtr pointer)
		{
			if (glColorPointer == null)
			{
				glColorPointer = Marshal.GetDelegateForFunctionPointer<glColorPointerHandler>(Glfw.GetProcAddress("glColorPointer"));
			}

			glColorPointer(size, (int)type, stride, pointer);
		}

		public void CullFace(CullFaceMode mode)
		{
			Gl.glCullFace((int)mode);
		}

		public void DeleteBuffers(int n, ref int buffers)
		{
			throw new NotImplementedException();
		}

		public void DeleteFramebuffers(int n, ref int frameBuffers)
		{
			throw new NotImplementedException();
		}

		public void DeleteRenderbuffers(int n, ref int renderBuffers)
		{
			throw new NotImplementedException();
		}

		public void DeleteTextures(int n, ref int textures)
		{
			if (n != 1)
			{
				throw new NotImplementedException();
			}

			Gl.glDeleteTexture((uint)textures);
		}

		public void DepthFunc(DepthFunction func)
		{
			Gl.glDepthFunc((int)func);
		}

		public void DepthMask(bool flag)
		{
			throw new NotImplementedException();
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
			throw new NotImplementedException();
		}

		public void DrawArrays(BeginMode mode, int first, int count)
		{
			Gl.glDrawArrays((int)mode, first, count);
		}

		public void DrawRangeElements(BeginMode mode, int start, int end, int count, DrawElementsType type, IntPtr indices)
		{
			throw new NotImplementedException();
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
			throw new NotImplementedException();
		}

		public void FramebufferRenderbuffer(int renderBuffer)
		{
			throw new NotImplementedException();
		}

		public void FrontFace(FrontFaceDirection mode)
		{
			Gl.glFrontFace((int)mode);
		}

		public void GenBuffers(int n, out int buffers)
		{
			throw new NotImplementedException();
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
			if (n != 1)
			{
				throw new NotImplementedException();
			}

			textureHandle = (int)Gl.glGenTexture();
		}

		public ErrorCode GetError()
		{
			return (ErrorCode)Enum.Parse(typeof(ErrorCode), Gl.GetError().ToString());
		}

		public string GetString(StringName name)
		{
			return Gl.glGetString((int)name);
		}

		private delegate void glLightfHandler(int light, int pname, float[] param);
		private static glLightfHandler glLightf;
		public void Light(LightName light, LightParameter pname, float[] param)
		{
			if (glLightf == null)
			{
				glLightf = Marshal.GetDelegateForFunctionPointer<glLightfHandler>(Glfw.GetProcAddress("glLightf"));
			}

			glLightf((int)light, (int)pname, param);
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

		private delegate void glLoadMatrixdHandler(double[] m);
		private static glLoadMatrixdHandler glLoadMatrixd;
		public void LoadMatrix(double[] m)
		{
			if (glLoadMatrixd == null)
			{
				glLoadMatrixd = Marshal.GetDelegateForFunctionPointer<glLoadMatrixdHandler>(Glfw.GetProcAddress("glLoadMatrixd"));
			}

			glLoadMatrixd(m);
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

		private delegate void glNormal3fHandler(float x, float y, float z);
		private static glNormal3fHandler glNormal3f;
		public void Normal3(double x, double y, double z)
		{
			if (glNormal3f == null)
			{
				glNormal3f = Marshal.GetDelegateForFunctionPointer<glNormal3fHandler>(Glfw.GetProcAddress("glNormal3f"));
			}

			glNormal3f((float)x, (float)y, (float)z);
		}

		public void NormalPointer(NormalPointerType type, int stride, float[] pointer)
		{
			unsafe
			{
				fixed (float* pArray = pointer)
				{
					NormalPointer(type, stride, new IntPtr(pArray));
				}
			}
		}

		private delegate void glNormalPointerHandler(int type, int stride, IntPtr pointer);
		private static glNormalPointerHandler glNormalPointer;
		public void NormalPointer(NormalPointerType type, int stride, IntPtr pointer)
		{
			if (glNormalPointer == null)
			{
				glNormalPointer = Marshal.GetDelegateForFunctionPointer<glNormalPointerHandler>(Glfw.GetProcAddress("glNormalPointer"));
			}

			glNormalPointer((int)type, stride, pointer);
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
			Gl.glPolygonOffset(factor, units);
		}

		private delegate void glPopAttribHandler();
		private static glPopAttribHandler glPopAttrib;
		public void PopAttrib()
		{
			if (glPopAttrib == null)
			{
				glPopAttrib = Marshal.GetDelegateForFunctionPointer<glPopAttribHandler>(Glfw.GetProcAddress("glPopAttrib"));
			}

			glPopAttrib();
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

		private delegate void glPushAttribHandler();
		private static glPushAttribHandler glPushAttrib;
		public void PushAttrib(AttribMask mask)
		{
			if (glPushAttrib == null)
			{
				glPushAttrib = Marshal.GetDelegateForFunctionPointer<glPushAttribHandler>(Glfw.GetProcAddress("glPushAttrib"));
			}

			glPushAttrib();
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
			throw new NotImplementedException();
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
			throw new NotImplementedException();
		}

		private delegate void glRotatefHandler(float angle, float x, float y, float z);
		private static glRotatefHandler glRotatef;
		public void Rotate(double angle, double x, double y, double z)
		{
			if (glRotatef == null)
			{
				glRotatef = Marshal.GetDelegateForFunctionPointer<glRotatefHandler>(Glfw.GetProcAddress("glRotatef"));
			}

			glRotatef((float)angle, (float)x, (float)y, (float)z);
		}

		private delegate void glScalefHandler(float x, float y, float z);
		private static glScalefHandler glScalef;
		public void Scale(double x, double y, double z)
		{
			if (glScalef == null)
			{
				glScalef = Marshal.GetDelegateForFunctionPointer<glScalefHandler>(Glfw.GetProcAddress("glScalef"));
			}

			glScalef((float)x, (float)y, (float)z);
		}

		public void Scissor(int x, int y, int width, int height)
		{
			Gl.glScissor(x, y, width, height);
		}

		private delegate void glShadeModelHandler(int model);
		private static glShadeModelHandler glShadeModel;
		public void ShadeModel(ShadingModel model)
		{
			if (glShadeModel == null)
			{
				glShadeModel = Marshal.GetDelegateForFunctionPointer<glShadeModelHandler>(Glfw.GetProcAddress("glShadeModel"));
			}

			glShadeModel((int)model);
		}

		public void TexCoord2(Vector2 uv)
		{
			TexCoord2(uv.X, uv.Y);
		}

		private delegate void glTexCoord2fHandler(float x, float y);
		private static glTexCoord2fHandler glTexCoord2f;
		public void TexCoord2(double x, double y)
		{
			if (glTexCoord2f == null)
			{
				glTexCoord2f = Marshal.GetDelegateForFunctionPointer<glTexCoord2fHandler>(Glfw.GetProcAddress("glTexCoord2f"));
			}

			glTexCoord2f((float)x, (float)y);
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
			unsafe
			{
				fixed (byte* pArray = pixels)
				{
					Gl.glTexImage2D((int)target, level, (int)internalFormat, width, height, border, (int)format, (int)type, new IntPtr(pArray));
				}
			}
		}

		private delegate void glTexParameteriHandler(int target, int pname, int param);
		private static glTexParameteriHandler glTexParameteri;
		public void TexParameter(TextureTarget target, TextureParameterName pname, int param)
		{
			if (glTexParameteri == null)
			{
				glTexParameteri = Marshal.GetDelegateForFunctionPointer<glTexParameteriHandler>(Glfw.GetProcAddress("glTexParameteri"));
			}

			glTexParameteri((int)target, (int)pname, param);
		}

		public void Translate(MatterHackers.VectorMath.Vector3 vector)
		{
			Translate(vector.X, vector.Y, vector.Z);
		}

		private delegate void glTranslatefHandler(float x, float y, float z);
		private static glTranslatefHandler glTranslatef;
		public void Translate(double x, double y, double z)
		{
			if (glTranslatef == null)
			{
				glTranslatef = Marshal.GetDelegateForFunctionPointer<glTranslatefHandler>(Glfw.GetProcAddress("glTranslatef"));
			}

			glTranslatef((float)x, (float)y, (float)z);
		}

		public void Vertex2(Vector2 position)
		{
			Vertex2(position.X, position.Y);
		}

		private delegate void glVertex2fHandler(float x, float y);
		private static glVertex2fHandler glVertex2f;
		public void Vertex2(double x, double y)
		{
			if (glVertex2f == null)
			{
				glVertex2f = Marshal.GetDelegateForFunctionPointer<glVertex2fHandler>(Glfw.GetProcAddress("glVertex2f"));
			}

			glVertex2f((float)x, (float)y);
		}

		public void Vertex3(Vector3 position)
		{
			Vertex3(position.X, position.Y, position.Z);
		}

		private delegate void glVertex3fHandler(float x, float y, float z);
		private static glVertex3fHandler glVertex3f;
		public void Vertex3(double x, double y, double z)
		{
			if (glVertex3f == null)
			{
				glVertex3f = Marshal.GetDelegateForFunctionPointer<glVertex3fHandler>(Glfw.GetProcAddress("glVertex3f"));
			}

			glVertex3f((float)x, (float)y, (float)z);
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

		private delegate void glVertexPointerHandler(int size, int type, int stride, IntPtr pointer);
		private static glVertexPointerHandler glVertexPointer;
		public void VertexPointer(int size, VertexPointerType type, int stride, IntPtr pointer)
		{
			if (glVertexPointer == null)
			{
				glVertexPointer = Marshal.GetDelegateForFunctionPointer<glVertexPointerHandler>(Glfw.GetProcAddress("glVertexPointer"));
			}

			glVertexPointer(size, (int)type, stride, pointer);
		}

		public void Viewport(int x, int y, int width, int height)
		{
			glViewport(x, y, width, height);
		}

		private Stream OpenEmbeddedAssetStream(string name) => GetType().Assembly.GetManifestResourceStream(name);

		private delegate void glTexEnvfPointerHandler(int target, int pname, float param);
		private static glTexEnvfPointerHandler glTexEnvf;
		public void TexEnv(TextureEnvironmentTarget target, TextureEnvParameter pname, float param)
		{
			if (glTexEnvf == null)
			{
				glTexEnvf = Marshal.GetDelegateForFunctionPointer<glTexEnvfPointerHandler>(Glfw.GetProcAddress("glTexEnvf"));
			}

			glTexEnvf((int)target, (int)pname, param);
		}

		private delegate void glIndexPointerHandler(int type, int stride, IntPtr pointer);
		private static glIndexPointerHandler glIndexPointer;
		public void IndexPointer(IndexPointerType type, int stride, IntPtr pointer)
		{
			if (glIndexPointer == null)
			{
				glIndexPointer = Marshal.GetDelegateForFunctionPointer<glIndexPointerHandler>(Glfw.GetProcAddress("glIndexPointer"));
			}

			glIndexPointer((int)type, stride, pointer);
		}
	}
}