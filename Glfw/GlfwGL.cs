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
using System.Runtime.InteropServices;
using System.Text;
using GLFW;
using MatterHackers.Agg;
using MatterHackers.RenderOpenGl.OpenGl;
using MatterHackers.VectorMath;
using ErrorCode = MatterHackers.RenderOpenGl.OpenGl.ErrorCode;

namespace MatterHackers.RenderOpenGl
{
	public class GlfwGL : IOpenGL
	{
		private static glBeginHandler glBegin;

		private static glBindBufferHandler glBindBuffer;

		private static glBindTextureHandler glBindTexture;

		private static glBlendFuncHandler glBlendFunc;

		private static glBufferDataHandler glBufferData;

		private static glClearHandler glClear;

		private static glClearDepthHandler glClearDepth;

		private static glColor4fHandler glColor4f;

		private static glColorMaterialHandler glColorMaterial;

		private static glColorPointerHandler glColorPointer;

		private static glCullFaceHandler glCullFace;

		private static glDeleteBuffersHandler glDeleteBuffers;

		private static glDeleteTexturesHandler glDeleteTextures;

		private static glDepthFuncHandler glDepthFunc;

		private static glDisableHandler glDisable;

		private static glDisableClientStateHandler glDisableClientState;

		private static glDrawArraysHandler glDrawArrays;

		private static glDrawRangeElementsHandler glDrawRangeElements;

		private static glEnableHandler glEnable;

		private static glEnableClientStateHandler glEnableClientState;

		private static glEndHandler glEnd;

		private static glFrontFaceHandler glFrontFace;

		private static glGenBuffersHandler glGenBuffers;

		private static glGenTexturesHandler glGenTextures;

		private static glGetErrorHandler glGetError;

		private static glGetStringHandler glGetString;

		private static glIndexPointerHandler glIndexPointer;

		private static glLightfvHandler glLightfv;

		private static glLoadIdentityHandler glLoadIdentity;

		private static glLoadMatrixdHandler glLoadMatrixd;

		private static glMatrixModeHandler glMatrixMode;

		private static glMultMatrixHandler glMultMatrixf;

		private static glNormal3fHandler glNormal3f;

		private static glNormalPointerHandler glNormalPointer;

		private static glOrthoHandler glOrtho;

		private static glPolygonOffsetHandler glPolygonOffset;

		private static glPopAttribHandler glPopAttrib;

		private static glPopMatrixHandler glPopMatrix;

		private static glPushAttribHandler glPushAttrib;

		private static glPushMatrixHandler glPushMatrix;

		private static glRotatefHandler glRotatef;

		private static glScalefHandler glScalef;

		private static glScissorHandler glScissor;

		private static glShadeModelHandler glShadeModel;

		private static glTexCoord2fHandler glTexCoord2f;

		private static glTexCoordPointerHandler glTexCoordPointer;

		private static glTexEnvfPointerHandler glTexEnvf;

		private static glTexImage2DHandler glTexImage2D;

		private static glTexParameteriHandler glTexParameteri;

		private static glTranslatefHandler glTranslatef;

		private static glVertex2fHandler glVertex2f;

		private static glVertex3fHandler glVertex3f;

		private static glVertexPointerHandler glVertexPointer;

		private static glViewportHandler glViewport;

		private static bool initialized = false;

		private readonly Dictionary<int, byte[]> bufferData = new Dictionary<int, byte[]>();

		private int currentArrayBufferIndex = 0;

		private int currentElementArrayBufferIndex = 0;

		private int genBuffersIndex = 1;

		public GlfwGL()
		{
			if (!initialized)
			{
				initialized = true;
				glBegin = Marshal.GetDelegateForFunctionPointer<glBeginHandler>(Glfw.GetProcAddress("glBegin"));
				glBindBuffer = Marshal.GetDelegateForFunctionPointer<glBindBufferHandler>(Glfw.GetProcAddress("glBindBuffer"));
				glBindTexture = Marshal.GetDelegateForFunctionPointer<glBindTextureHandler>(Glfw.GetProcAddress("glBindTexture"));
				glBlendFunc = Marshal.GetDelegateForFunctionPointer<glBlendFuncHandler>(Glfw.GetProcAddress("glBlendFunc"));
				glBufferData = Marshal.GetDelegateForFunctionPointer<glBufferDataHandler>(Glfw.GetProcAddress("glBufferData"));
				glClear = Marshal.GetDelegateForFunctionPointer<glClearHandler>(Glfw.GetProcAddress("glClear"));
				glClearDepth = Marshal.GetDelegateForFunctionPointer<glClearDepthHandler>(Glfw.GetProcAddress("glClearDepth"));
				glColor4f = Marshal.GetDelegateForFunctionPointer<glColor4fHandler>(Glfw.GetProcAddress("glColor4f"));
				glColorMaterial = Marshal.GetDelegateForFunctionPointer<glColorMaterialHandler>(Glfw.GetProcAddress("glColorMaterial"));
				glColorPointer = Marshal.GetDelegateForFunctionPointer<glColorPointerHandler>(Glfw.GetProcAddress("glColorPointer"));
				glCullFace = Marshal.GetDelegateForFunctionPointer<glCullFaceHandler>(Glfw.GetProcAddress("glCullFace"));
				glDeleteBuffers = Marshal.GetDelegateForFunctionPointer<glDeleteBuffersHandler>(Glfw.GetProcAddress("glDeleteBuffers"));
				glDeleteTextures = Marshal.GetDelegateForFunctionPointer<glDeleteTexturesHandler>(Glfw.GetProcAddress("glDeleteTextures"));
				glDepthFunc = Marshal.GetDelegateForFunctionPointer<glDepthFuncHandler>(Glfw.GetProcAddress("glDepthFunc"));
				glDisable = Marshal.GetDelegateForFunctionPointer<glDisableHandler>(Glfw.GetProcAddress("glDisable"));
				glDisableClientState = Marshal.GetDelegateForFunctionPointer<glDisableClientStateHandler>(Glfw.GetProcAddress("glDisableClientState"));
				glDrawArrays = Marshal.GetDelegateForFunctionPointer<glDrawArraysHandler>(Glfw.GetProcAddress("glDrawArrays"));
				glDrawRangeElements = Marshal.GetDelegateForFunctionPointer<glDrawRangeElementsHandler>(Glfw.GetProcAddress("glDrawRangeElements"));
				glEnable = Marshal.GetDelegateForFunctionPointer<glEnableHandler>(Glfw.GetProcAddress("glEnable"));
				glEnableClientState = Marshal.GetDelegateForFunctionPointer<glEnableClientStateHandler>(Glfw.GetProcAddress("glEnableClientState"));
				glEnd = Marshal.GetDelegateForFunctionPointer<glEndHandler>(Glfw.GetProcAddress("glEnd"));
				glFrontFace = Marshal.GetDelegateForFunctionPointer<glFrontFaceHandler>(Glfw.GetProcAddress("glFrontFace"));
				glGenBuffers = Marshal.GetDelegateForFunctionPointer<glGenBuffersHandler>(Glfw.GetProcAddress("glGenBuffers"));
				glGenTextures = Marshal.GetDelegateForFunctionPointer<glGenTexturesHandler>(Glfw.GetProcAddress("glGenTextures"));
				glGetError = Marshal.GetDelegateForFunctionPointer<glGetErrorHandler>(Glfw.GetProcAddress("glGetError"));
				glGetString = Marshal.GetDelegateForFunctionPointer<glGetStringHandler>(Glfw.GetProcAddress("glGetString"));
				glIndexPointer = Marshal.GetDelegateForFunctionPointer<glIndexPointerHandler>(Glfw.GetProcAddress("glIndexPointer"));
				glLightfv = Marshal.GetDelegateForFunctionPointer<glLightfvHandler>(Glfw.GetProcAddress("glLightfv"));
				glLoadIdentity = Marshal.GetDelegateForFunctionPointer<glLoadIdentityHandler>(Glfw.GetProcAddress("glLoadIdentity"));
				glLoadMatrixd = Marshal.GetDelegateForFunctionPointer<glLoadMatrixdHandler>(Glfw.GetProcAddress("glLoadMatrixd"));
				glMatrixMode = Marshal.GetDelegateForFunctionPointer<glMatrixModeHandler>(Glfw.GetProcAddress("glMatrixMode"));
				glMultMatrixf = Marshal.GetDelegateForFunctionPointer<glMultMatrixHandler>(Glfw.GetProcAddress("glMultMatrixf"));
				glNormal3f = Marshal.GetDelegateForFunctionPointer<glNormal3fHandler>(Glfw.GetProcAddress("glNormal3f"));
				glNormalPointer = Marshal.GetDelegateForFunctionPointer<glNormalPointerHandler>(Glfw.GetProcAddress("glNormalPointer"));
				glOrtho = Marshal.GetDelegateForFunctionPointer<glOrthoHandler>(Glfw.GetProcAddress("glOrtho"));
				glPolygonOffset = Marshal.GetDelegateForFunctionPointer<glPolygonOffsetHandler>(Glfw.GetProcAddress("glPolygonOffset"));
				glPopAttrib = Marshal.GetDelegateForFunctionPointer<glPopAttribHandler>(Glfw.GetProcAddress("glPopAttrib"));
				glPopMatrix = Marshal.GetDelegateForFunctionPointer<glPopMatrixHandler>(Glfw.GetProcAddress("glPopMatrix"));
				glPushAttrib = Marshal.GetDelegateForFunctionPointer<glPushAttribHandler>(Glfw.GetProcAddress("glPushAttrib"));
				glPushMatrix = Marshal.GetDelegateForFunctionPointer<glPushMatrixHandler>(Glfw.GetProcAddress("glPushMatrix"));
				glRotatef = Marshal.GetDelegateForFunctionPointer<glRotatefHandler>(Glfw.GetProcAddress("glRotatef"));
				glScalef = Marshal.GetDelegateForFunctionPointer<glScalefHandler>(Glfw.GetProcAddress("glScalef"));
				glScissor = Marshal.GetDelegateForFunctionPointer<glScissorHandler>(Glfw.GetProcAddress("glScissor"));
				glShadeModel = Marshal.GetDelegateForFunctionPointer<glShadeModelHandler>(Glfw.GetProcAddress("glShadeModel"));
				glTexCoord2f = Marshal.GetDelegateForFunctionPointer<glTexCoord2fHandler>(Glfw.GetProcAddress("glTexCoord2f"));
				glTexCoordPointer = Marshal.GetDelegateForFunctionPointer<glTexCoordPointerHandler>(Glfw.GetProcAddress("glTexCoordPointer"));
				glTexEnvf = Marshal.GetDelegateForFunctionPointer<glTexEnvfPointerHandler>(Glfw.GetProcAddress("glTexEnvf"));
				glTexImage2D = Marshal.GetDelegateForFunctionPointer<glTexImage2DHandler>(Glfw.GetProcAddress("glTexImage2D"));
				glTexParameteri = Marshal.GetDelegateForFunctionPointer<glTexParameteriHandler>(Glfw.GetProcAddress("glTexParameteri"));
				glTranslatef = Marshal.GetDelegateForFunctionPointer<glTranslatefHandler>(Glfw.GetProcAddress("glTranslatef"));
				glVertex2f = Marshal.GetDelegateForFunctionPointer<glVertex2fHandler>(Glfw.GetProcAddress("glVertex2f"));
				glVertex3f = Marshal.GetDelegateForFunctionPointer<glVertex3fHandler>(Glfw.GetProcAddress("glVertex3f"));
				glVertexPointer = Marshal.GetDelegateForFunctionPointer<glVertexPointerHandler>(Glfw.GetProcAddress("glVertexPointer"));
				glViewport = Marshal.GetDelegateForFunctionPointer<glViewportHandler>(Glfw.GetProcAddress("glViewport"));
			}
		}

#pragma warning disable SA1300 // Element should begin with upper-case letter

		private delegate void glBeginHandler(int mode);

		private delegate void glBindBufferHandler(int target, uint buffer);

		private delegate void glBindTextureHandler(int target, uint texture);

		private delegate void glBlendFuncHandler(int sfactor, int dfactor);

		private delegate void glBufferDataHandler(int target, int size, IntPtr data, int usage);

		private delegate void glClearDepthHandler(double depth);

		private delegate void glClearHandler(int mask);

		private delegate void glColor4fHandler(float red, float green, float blue, float alpha);

		private delegate void glColorMaterialHandler(int face, int mode);

		private delegate void glColorPointerHandler(int size, int type, int stride, IntPtr pointer);

		private delegate void glCullFaceHandler(int mode);

		private delegate void glDeleteBuffersHandler(int n, IntPtr buffers);

		private delegate void glDeleteTexturesHandler(int n, IntPtr textures);

		private delegate void glDepthFuncHandler(int func);

		private delegate void glDisableClientStateHandler(int state);

		private delegate void glDisableHandler(int cap);

		private delegate void glDrawArraysHandler(int mode, int first, int count);

		private delegate void glDrawRangeElementsHandler(int mode, uint start, uint end, int count, int type, IntPtr indices);

		private delegate void glEnableClientStateHandler(int arrayCap);

		private delegate void glEnableHandler(int cap);

		private delegate void glEndHandler();

		private delegate void glFrontFaceHandler(int mode);

		private delegate void glGenBuffersHandler(int n, IntPtr buffers);

		private delegate void glGenTexturesHandler(int n, IntPtr textures);

		private delegate string glGetErrorHandler();

		private delegate IntPtr glGetStringHandler(int name);

		private delegate void glIndexPointerHandler(int type, int stride, IntPtr pointer);

		private delegate void glLightfvHandler(int light, int pname, float[] param);

		private delegate void glLoadIdentityHandler();

		private delegate void glLoadMatrixdHandler(double[] m);

		private delegate void glMatrixModeHandler(int mode);

		private delegate void glMultMatrixHandler(float[] m);

		private delegate void glNormal3fHandler(float x, float y, float z);

		private delegate void glNormalPointerHandler(int type, int stride, IntPtr pointer);

		private delegate void glOrthoHandler(double left, double right, double bottom, double top, double zNear, double zFar);

		private delegate void glPolygonOffsetHandler(float factor, float units);

		private delegate void glPopAttribHandler();

		private delegate void glPopMatrixHandler();

		private delegate void glPushAttribHandler();

		private delegate void glPushMatrixHandler();

		private delegate void glRotatefHandler(float angle, float x, float y, float z);

		private delegate void glScalefHandler(float x, float y, float z);

		private delegate void glScissorHandler(int x, int y, int width, int height);

		private delegate void glShadeModelHandler(int model);

		private delegate void glTexCoord2fHandler(float x, float y);

		private delegate void glTexCoordPointerHandler(int size, int type, int stride, IntPtr pointer);

		private delegate void glTexEnvfPointerHandler(int target, int pname, float param);

		private delegate void glTexImage2DHandler(int target, int level, int internalformat, int width, int height, int border, int format, int type, IntPtr pixels);

		private delegate void glTexParameteriHandler(int target, int pname, int param);

		private delegate void glTranslatefHandler(float x, float y, float z);

		private delegate void glVertex2fHandler(float x, float y);

		private delegate void glVertex3fHandler(float x, float y, float z);

		private delegate void glVertexPointerHandler(int size, int type, int stride, IntPtr pointer);

		private delegate void glViewportHandler(int x, int y, int width, int height);

#pragma warning restore SA1300 // Element should begin with upper-case letter

		public bool GlHasBufferObjects { get; private set; } = true;

		public void Begin(BeginMode mode)
		{
			glBegin((int)mode);
		}

		public void BindBuffer(BufferTarget target, int buffer)
		{
			if (GlHasBufferObjects)
			{
				glBindBuffer((int)target, (uint)buffer);
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
			glBindTexture((int)target, (uint)texture);
		}

		public void BlendFunc(BlendingFactorSrc sfactor, BlendingFactorDest dfactor)
		{
			glBlendFunc((int)sfactor, (int)dfactor);
		}

		public void BufferData(BufferTarget target, int size, IntPtr data, BufferUsageHint usage)
		{
			if (GlHasBufferObjects)
			{
				glBufferData((int)target, size, data, (int)usage);
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
							throw new System.Exception("You don't have a ArrayBuffer set.");
						}

						bufferData[currentArrayBufferIndex] = dataCopy;
						break;

					case BufferTarget.ElementArrayBuffer:
						if (currentElementArrayBufferIndex == 0)
						{
							throw new System.Exception("You don't have an EllementArrayBuffer set.");
						}

						bufferData[currentElementArrayBufferIndex] = dataCopy;
						break;

					default:
						throw new NotImplementedException();
				}
			}
		}

		public void Clear(ClearBufferMask mask)
		{
			glClear((int)mask);
		}

		public void ClearDepth(double depth)
		{
			glClearDepth(depth);
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
			glColor4f(red / 255.0F, green / 255.0F, blue / 255.0F, alpha / 255.0F);
		}

		public void ColorMask(bool red, bool green, bool blue, bool alpha)
		{
			throw new NotImplementedException();
		}

		public void ColorMaterial(MaterialFace face, ColorMaterialParameter mode)
		{
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

		public void ColorPointer(int size, ColorPointerType type, int stride, IntPtr pointer)
		{
			if (GlHasBufferObjects || currentArrayBufferIndex == 0)
			{
				// we are rending from memory so operate normally
				glColorPointer(size, (int)type, stride, pointer);
			}
			else
			{
				unsafe
				{
					fixed (byte* buffer = bufferData[currentArrayBufferIndex])
					{
						glColorPointer(size, (int)type, stride, new IntPtr(&buffer[(int)pointer]));
					}
				}
			}
		}

		public void CullFace(CullFaceMode mode)
		{
			glCullFace((int)mode);
		}

		public void DeleteBuffer(int buffer)
		{
			if (GlHasBufferObjects)
			{
				var buffers = new int[] { buffer };
				unsafe
				{
					fixed (int* pArray = buffers)
					{
						glDeleteBuffers(1, new IntPtr(pArray));
					}
				}
			}
			else
			{
				bufferData.Remove(buffer);
			}
		}

		public void DeleteFramebuffers(int n, ref int frameBuffers)
		{
			throw new NotImplementedException();
		}

		public void DeleteRenderbuffers(int n, ref int renderBuffers)
		{
			throw new NotImplementedException();
		}

		public void DeleteTexture(int texture)
		{
			var textures = new int[] { texture };
			unsafe
			{
				fixed (int* pArray = textures)
				{
					glDeleteTextures(1, new IntPtr(pArray));
				}
			}
		}

		public void DepthFunc(DepthFunction func)
		{
			glDepthFunc((int)func);
		}

		public void DepthMask(bool flag)
		{
			throw new NotImplementedException();
		}

		public void Disable(EnableCap cap)
		{
			glDisable((int)cap);
		}

		public void DisableClientState(ArrayCap state)
		{
			glDisableClientState((int)state);
		}

		public void DisableGlBuffers()
		{
			GlHasBufferObjects = false;
		}

		public void DisposeResources()
		{
			throw new NotImplementedException();
		}

		public void DrawArrays(BeginMode mode, int first, int count)
		{
			glDrawArrays((int)mode, first, count);
		}

		public void DrawRangeElements(BeginMode mode, int start, int end, int count, DrawElementsType type, IntPtr indices)
		{
			unsafe
			{
				glDrawRangeElements((int)mode, (uint)start, (uint)end, count, (int)type, indices);
			}
		}

		public void Enable(EnableCap cap)
		{
			glEnable((int)cap);
		}

		public void EnableClientState(ArrayCap arrayCap)
		{
			if (GlHasBufferObjects || arrayCap != ArrayCap.IndexArray) // don't set index array if we don't have buffer objects (we will render through DrawElements instead).
			{
				glEnableClientState((int)arrayCap);
			}
		}

		public void End()
		{
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
			glFrontFace((int)mode);
		}

		// start at 1 so we can use 0 as a not initialize tell.
		public int GenBuffer()
		{
			if (GlHasBufferObjects)
			{
				var buffers = new int[1];
				unsafe
				{
					fixed (int* pArray = buffers)
					{
						glGenBuffers(1, new IntPtr(pArray));
					}
				}

				return buffers[0];
			}
			else
			{
				int buffer = genBuffersIndex++;
				bufferData.Add(buffer, new byte[1]);
				return buffer;
			}
		}

		public void GenFramebuffers(int n, out int frameBuffers)
		{
			throw new NotImplementedException();
		}

		public void GenRenderbuffers(int n, out int renderBuffers)
		{
			throw new NotImplementedException();
		}

		public int GenTexture()
		{
			var textures = new int[1];
			unsafe
			{
				fixed (int* pArray = textures)
				{
					glGenTextures(1, new IntPtr(pArray));
				}
			}

			return textures[0];
		}

		public ErrorCode GetError()
		{
			var error = glGetError();
			if (!string.IsNullOrEmpty(error))
			{
				return (ErrorCode)Enum.Parse(typeof(ErrorCode), error.ToString());
			}

			return ErrorCode.NoError;
		}

		public string GetString(StringName name)
		{
			var buffer = glGetString((int)name);
			return PtrToStringUtf8(buffer);
		}

		public void IndexPointer(IndexPointerType type, int stride, IntPtr pointer)
		{
			glIndexPointer((int)type, stride, pointer);
		}

		public void Light(LightName light, LightParameter pname, float[] param)
		{
			glLightfv((int)light, (int)pname, param);
		}

		public void LoadIdentity()
		{
			glLoadIdentity();
		}

		public void LoadMatrix(double[] m)
		{
			glLoadMatrixd(m);
		}

		public void MatrixMode(MatrixMode mode)
		{
			glMatrixMode((int)mode);
		}

		public void MultMatrix(float[] m)
		{
			glMultMatrixf(m);
		}

		public void Normal3(double x, double y, double z)
		{
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

		public void NormalPointer(NormalPointerType type, int stride, IntPtr pointer)
		{
			glNormalPointer((int)type, stride, pointer);
		}

		public void Ortho(double left, double right, double bottom, double top, double zNear, double zFar)
		{
			glOrtho(left, right, bottom, top, zNear, zFar);
		}

		public void PolygonOffset(float factor, float units)
		{
			glPolygonOffset(factor, units);
		}

		public void PopAttrib()
		{
			glPopAttrib();
		}

		public void PopMatrix()
		{
			glPopMatrix();
		}

		public void PushAttrib(AttribMask mask)
		{
			glPushAttrib();
		}

		public void PushMatrix()
		{
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
				using (var ms = new MemoryStream(bytes))
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

		public void Rotate(double angle, double x, double y, double z)
		{
			glRotatef((float)angle, (float)x, (float)y, (float)z);
		}

		public void Scale(double x, double y, double z)
		{
			glScalef((float)x, (float)y, (float)z);
		}

		public void Scissor(int x, int y, int width, int height)
		{
			glScissor(x, y, width, height);
		}

		public void ShadeModel(ShadingModel model)
		{
			glShadeModel((int)model);
		}

		public void TexCoord2(Vector2 uv)
		{
			TexCoord2(uv.X, uv.Y);
		}

		public void TexCoord2(double x, double y)
		{
			glTexCoord2f((float)x, (float)y);
		}

		public void TexCoordPointer(int size, TexCordPointerType type, int stride, IntPtr pointer)
		{
			glTexCoordPointer(size, (int)type, stride, pointer);
		}

		public void TexEnv(TextureEnvironmentTarget target, TextureEnvParameter pname, float param)
		{
			glTexEnvf((int)target, (int)pname, param);
		}

		public void TexImage2D(TextureTarget target,
			int level,
			PixelInternalFormat internalFormat,
			int width,
			int height,
			int border,
			PixelFormat format,
			PixelType type,
			byte[] pixels)
		{
			unsafe
			{
				fixed (byte* pArray = pixels)
				{
					glTexImage2D((int)target, level, (int)internalFormat, width, height, border, (int)format, (int)type, new IntPtr(pArray));
				}
			}
		}

		public void TexParameter(TextureTarget target, TextureParameterName pname, int param)
		{
			glTexParameteri((int)target, (int)pname, param);
		}

		public void Translate(MatterHackers.VectorMath.Vector3 vector)
		{
			Translate(vector.X, vector.Y, vector.Z);
		}

		public void Translate(double x, double y, double z)
		{
			glTranslatef((float)x, (float)y, (float)z);
		}

		public void Vertex2(Vector2 position)
		{
			Vertex2(position.X, position.Y);
		}

		public void Vertex2(double x, double y)
		{
			glVertex2f((float)x, (float)y);
		}

		public void Vertex3(Vector3 position)
		{
			Vertex3(position.X, position.Y, position.Z);
		}

		public void Vertex3(double x, double y, double z)
		{
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

		public void VertexPointer(int size, VertexPointerType type, int stride, IntPtr pointer)
		{
			glVertexPointer(size, (int)type, stride, pointer);
		}

		public void Viewport(int x, int y, int width, int height)
		{
			glViewport(x, y, width, height);
		}

		private static string PtrToStringUtf8(IntPtr ptr)
		{
			var length = 0;
			while (Marshal.ReadByte(ptr, length) != 0)
			{
				length++;
			}

			var buffer = new byte[length];
			Marshal.Copy(ptr, buffer, 0, length);
			return Encoding.UTF8.GetString(buffer);
		}

		private Stream OpenEmbeddedAssetStream(string name) => GetType().Assembly.GetManifestResourceStream(name);
	}
}