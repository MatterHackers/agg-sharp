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
using System.Threading;
using MatterHackers.Agg;
using MatterHackers.VectorMath;

namespace MatterHackers.RenderOpenGl.OpenGl
{
	public static class GL
	{
		private static readonly Dictionary<EnableCap, bool> IsEnabled = new Dictionary<EnableCap, bool>();
		private static IOpenGL _instance = null;
		private static bool inBegin;
		private static int pushAttribCount = 0;
		private static int pushMatrixCount = 0;
		private static int threadId = -1;

		public static IOpenGL Instance
		{
			get
			{
				if (threadId == -1)
				{
					threadId = Thread.CurrentThread.ManagedThreadId;
				}

				if (Thread.CurrentThread.ManagedThreadId != threadId)
				{
					throw new Exception("You mush only cal GL on the main thread.");
				}

				CheckForError();
				return _instance;
			}

			set
			{
				_instance = value;
			}
		}

		public static void Begin(BeginMode mode)
		{
			inBegin = true;
			Instance?.Begin(mode);
			CheckForError();
		}

		public static void BindBuffer(BufferTarget target, int buffer)
		{
			Instance?.BindBuffer(target, buffer);
			CheckForError();
		}

		public static void BindTexture(TextureTarget target, int texture)
		{
			Instance?.BindTexture(target, texture);
			CheckForError();
		}

		public static void BlendFunc(BlendingFactorSrc sfactor, BlendingFactorDest dfactor)
		{
			Instance?.BlendFunc(sfactor, dfactor);
			CheckForError();
		}

		public static void BufferData(BufferTarget target, int size, IntPtr data, BufferUsageHint usage)
		{
			Instance?.BufferData(target, size, data, usage);
			CheckForError();
		}

		public static void CheckForError()
		{
#if DEBUG
			if (!inBegin)
			{
				var code = _instance.GetError();
				if (code != ErrorCode.NoError)
				{
					throw new Exception($"OpenGL Error: {code}");
				}
			}
#endif
		}

		public static void Clear(ClearBufferMask mask)
		{
			Instance?.Clear(mask);
			CheckForError();
		}

		public static void ClearDepth(double depth)
		{
			Instance?.ClearDepth(depth);
			CheckForError();
		}

		public static void Color4(Color color)
		{
			Color4(color.red, color.green, color.blue, color.alpha);
		}

		public static void Color4(int red, int green, int blue, int alpha)
		{
			Color4((byte)red, (byte)green, (byte)blue, (byte)alpha);
		}

		public static void Color4(byte red, byte green, byte blue, byte alpha)
		{
			Instance?.Color4(red, green, blue, alpha);
			CheckForError();
		}

		public static void ColorMask(bool red, bool green, bool blue, bool alpha)
		{
			Instance?.ColorMask(red, green, blue, alpha);
			CheckForError();
		}

		public static void ColorMaterial(MaterialFace face, ColorMaterialParameter mode)
		{
			Instance?.ColorMaterial(face, mode);
			CheckForError();
		}

		public static void ColorPointer(int size, ColorPointerType type, int stride, byte[] pointer)
		{
			unsafe
			{
				fixed (byte* intPointer = pointer)
				{
					ColorPointer(size, type, stride, (IntPtr)intPointer);
				}
			}
		}

		public static void ColorPointer(int size, ColorPointerType type, int stride, IntPtr pointer)
		{
			Instance?.ColorPointer(size, type, stride, pointer);
			CheckForError();
		}

		public static void CullFace(CullFaceMode mode)
		{
			Instance?.CullFace(mode);
			CheckForError();
		}

		public static void DeleteBuffers(int n, ref int buffers)
		{
			Instance?.DeleteBuffers(n, ref buffers);
			CheckForError();
		}

		public static void DeleteTextures(int n, ref int textures)
		{
			Instance?.DeleteTextures(n, ref textures);
			CheckForError();
		}

		public static void DepthFunc(DepthFunction func)
		{
			Instance?.DepthFunc(func);
			CheckForError();
		}

		public static void DepthMask(bool flag)
		{
			Instance?.DepthMask(flag);
			CheckForError();
		}

		public static void Disable(EnableCap cap)
		{
			IsEnabled[cap] = false;

			Instance?.Disable(cap);
			CheckForError();
		}

		public static void DisableClientState(ArrayCap array)
		{
			Instance?.DisableClientState(array);
			CheckForError();
		}

		public static void DrawArrays(BeginMode mode, int first, int count)
		{
			Instance?.DrawArrays(mode, first, count);
			CheckForError();
		}

		public static void DrawRangeElements(BeginMode mode, int start, int end, int count, DrawElementsType type, IntPtr indices)
		{
			Instance?.DrawRangeElements(mode, start, end, count, type, indices);
			CheckForError();
		}

		public static void Enable(EnableCap cap)
		{
			IsEnabled[cap] = true;
			Instance?.Enable(cap);
			CheckForError();
		}

		public static void EnableClientState(ArrayCap arrayCap)
		{
			Instance?.EnableClientState(arrayCap);
			CheckForError();
		}

		public static bool EnableState(EnableCap cap)
		{
			if (IsEnabled.ContainsKey(cap))
			{
				return IsEnabled[cap];
			}

			return false;
		}

		public static void End()
		{
			Instance?.End();
			inBegin = false;

			CheckForError();
		}

		public static void Finish()
		{
			Instance?.Finish();
			CheckForError();
		}

		public static void FrontFace(FrontFaceDirection mode)
		{
			Instance?.FrontFace(mode);
			CheckForError();
		}

		public static int GenBuffer()
		{
			if (Instance != null)
			{
				var buffer = Instance.GenBuffer();
				CheckForError();
				return buffer;
			}

			return 0;
		}

		public static void GenTextures(int n, out int textureHandle)
		{
			textureHandle = -1;
			Instance?.GenTextures(n, out textureHandle);
			CheckForError();
		}

		public static ErrorCode GetError()
		{
			if (Instance != null)
			{
				return Instance.GetError();
			}

			return ErrorCode.NoError;
		}

		public static string GetString(StringName name)
		{
			if (Instance != null)
			{
				CheckForError();
				return Instance.GetString(name);
			}

			return "";
		}

		public static void IndexPointer(IndexPointerType type, int stride, IntPtr pointer)
		{
			Instance?.IndexPointer(type, stride, pointer);
			CheckForError();
		}

		public static void Light(LightName light, LightParameter pname, float[] param)
		{
			Instance?.Light(light, pname, param);
			CheckForError();
		}

		public static void LoadIdentity()
		{
			Instance?.LoadIdentity();
			CheckForError();
		}

		public static void LoadMatrix(double[] m)
		{
			Instance?.LoadMatrix(m);
			CheckForError();
		}

		public static void MatrixMode(MatrixMode mode)
		{
			Instance?.MatrixMode(mode);
			CheckForError();
		}

		public static void MultMatrix(float[] m)
		{
			Instance?.MultMatrix(m);
			CheckForError();
		}

		public static void Normal3(double x, double y, double z)
		{
			Instance?.Normal3(x, y, z);
			CheckForError();
		}

		public static void NormalPointer(NormalPointerType type, int stride, float[] pointer)
		{
			unsafe
			{
				fixed (float* floatPointer = pointer)
				{
					NormalPointer(type, stride, (IntPtr)floatPointer);
				}
			}
		}

		public static void NormalPointer(NormalPointerType type, int stride, IntPtr pointer)
		{
			Instance?.NormalPointer(type, stride, pointer);
			CheckForError();
		}

		public static void Ortho(double left, double right, double bottom, double top, double zNear, double zFar)
		{
			Instance?.Ortho(left, right, bottom, top, zNear, zFar);
			CheckForError();
		}

		public static void PolygonOffset(float factor, float units)
		{
			Instance?.PolygonOffset(factor, units);
			CheckForError();
		}

		public static void PopAttrib()
		{
			pushAttribCount--;
			Instance?.PopAttrib();
			CheckForError();
		}

		public static void PopMatrix()
		{
			pushMatrixCount--;
			Instance?.PopMatrix();
			CheckForError();
		}

		public static void PushAttrib(AttribMask mask)
		{
			pushAttribCount++;
			if (pushAttribCount > 100)
			{
				throw new Exception("pushAttrib being called without matching PopAttrib");
			}

			Instance?.PushAttrib(mask);
			CheckForError();
		}

		public static void PushMatrix()
		{
			pushMatrixCount++;
			if (pushMatrixCount > 100)
			{
				throw new Exception("PushMatrix being called without matching PopMatrix");
			}

			Instance?.PushMatrix();
			CheckForError();
		}

		public static void Rotate(double angle, double x, double y, double z)
		{
			Instance?.Rotate(angle, x, y, z);
			CheckForError();
		}

		public static void Scale(double x, double y, double z)
		{
			Instance?.Scale(x, y, z);
			CheckForError();
		}

		public static void Scissor(int x, int y, int width, int height)
		{
			Instance?.Scissor(x, y, width, height);
			CheckForError();
		}

		public static void ShadeModel(ShadingModel model)
		{
			Instance?.ShadeModel(model);
			CheckForError();
		}

		public static void TexCoord2(Vector2 uv)
		{
			TexCoord2(uv.X, uv.Y);
		}

		public static void TexCoord2(Vector2Float uv)
		{
			TexCoord2(uv.X, uv.Y);
		}

		public static void TexCoord2(double x, double y)
		{
			Instance?.TexCoord2(x, y);
			CheckForError();
		}

		public static void TexCoordPointer(int size, TexCordPointerType type, int stride, IntPtr pointer)
		{
			Instance?.TexCoordPointer(size, type, stride, pointer);
			CheckForError();
		}

		public static void TexEnv(TextureEnvironmentTarget target, TextureEnvParameter pname, float param)
		{
			Instance?.TexEnv(target, pname, param);
			CheckForError();
		}

		public static void TexImage2D(TextureTarget target,
			int level,
			PixelInternalFormat internalFormat,
			int width,
			int height,
			int border,
			PixelFormat format,
			PixelType type,
			byte[] pixels)
		{
			Instance?.TexImage2D(target,
				level,
				internalFormat,
				width,
				height,
				border,
				format,
				type,
				pixels);
			CheckForError();
		}

		public static void TexParameter(TextureTarget target, TextureParameterName pname, int param)
		{
			Instance?.TexParameter(target, pname, param);
			CheckForError();
		}

		public static void Translate(MatterHackers.VectorMath.Vector3 vector)
		{
			Translate(vector.X, vector.Y, vector.Z);
		}

		public static void Translate(double x, double y, double z)
		{
			Instance?.Translate(x, y, z);
			CheckForError();
		}

		public static void Vertex2(Vector2 position)
		{
			Vertex2(position.X, position.Y);
		}

		public static void Vertex2(double x, double y)
		{
			Instance?.Vertex2(x, y);
			CheckForError();
		}

		public static void Vertex3(Vector3 position)
		{
			Vertex3(position.X, position.Y, position.Z);
		}

		public static void Vertex3(Vector3Float position)
		{
			Vertex3(position.X, position.Y, position.Z);
		}

		public static void Vertex3(double x, double y, double z)
		{
			Instance?.Vertex3(x, y, z);
			CheckForError();
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
			Instance?.VertexPointer(size, type, stride, pointer);
			CheckForError();
		}

		public static void Viewport(int x, int y, int width, int height)
		{
			Instance?.Viewport(x, y, width, height);
			CheckForError();
		}
	}
}