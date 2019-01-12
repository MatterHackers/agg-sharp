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

using MatterHackers.Agg;
using MatterHackers.VectorMath;
using System;

namespace MatterHackers.RenderOpenGl.OpenGl
{
	public static class GL
	{
		public static IOpenGL Instance { get; set; } = null;

		public static void Begin(BeginMode mode)
		{
			Instance?.Begin(mode);
		}

		public static void BindBuffer(BufferTarget target, int buffer)
		{
			Instance?.BindBuffer(target, buffer);
		}

		public static void BindTexture(TextureTarget target, int texture)
		{
			Instance?.BindTexture(target, texture);
		}

		public static void BlendFunc(BlendingFactorSrc sfactor, BlendingFactorDest dfactor)
		{
			Instance?.BlendFunc(sfactor, dfactor);
		}

		public static void BufferData(BufferTarget target, int size, IntPtr data, BufferUsageHint usage)
		{
			Instance?.BufferData(target, size, data, usage);
		}

		public static void Clear(ClearBufferMask mask)
		{
			Instance?.Clear(mask);
		}

		public static void ClearDepth(double depth)
		{
			Instance?.ClearDepth(depth);
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
		}

		public static void ColorMask(bool red, bool green, bool blue, bool alpha)
		{
			Instance?.ColorMask(red, green, blue, alpha);
		}

		public static void ColorMaterial(MaterialFace face, ColorMaterialParameter mode)
		{
			Instance?.ColorMaterial(face, mode);
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
		}

		public static void CullFace(CullFaceMode mode)
		{
			Instance?.CullFace(mode);
		}

		public static void DeleteBuffers(int n, ref int buffers)
		{
			Instance?.DeleteBuffers(n, ref buffers);
		}

		public static void DeleteTextures(int n, ref int textures)
		{
			Instance?.DeleteTextures(n, ref textures);
		}

		public static void DepthFunc(DepthFunction func)
		{
			Instance?.DepthFunc(func);
		}

		public static void DepthMask(bool flag)
		{
			Instance?.DepthMask(flag);
		}

		public static void Disable(EnableCap cap)
		{
			Instance?.Disable(cap);
		}

		public static void DisableClientState(ArrayCap array)
		{
			Instance?.DisableClientState(array);
		}

		public static void DrawArrays(BeginMode mode, int first, int count)
		{
			Instance?.DrawArrays(mode, first, count);
		}

		public static void DrawRangeElements(BeginMode mode, int start, int end, int count, DrawElementsType type, IntPtr indices)
		{
			Instance?.DrawRangeElements(mode, start, end, count, type, indices);
		}

		public static void Enable(EnableCap cap)
		{
			Instance?.Enable(cap);
		}

		public static void EnableClientState(ArrayCap arrayCap)
		{
			Instance?.EnableClientState(arrayCap);
		}

		public static void End()
		{
			Instance?.End();
		}

		public static void Finish()
		{
			Instance?.Finish();
		}

		public static void FrontFace(FrontFaceDirection mode)
		{
			Instance?.FrontFace(mode);
		}

		public static void GenBuffers(int n, out int buffers)
		{
			buffers = -1;
			Instance?.GenBuffers(n, out buffers);
		}

		public static void GenTextures(int n, out int textureHandle)
		{
			textureHandle = -1;
			Instance?.GenTextures(n, out textureHandle);
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
				return Instance.GetString(name);
			}

			return "";
		}

		public static void Light(LightName light, LightParameter pname, float[] param)
		{
			Instance?.Light(light, pname, param);
		}

		public static void LoadIdentity()
		{
			Instance?.LoadIdentity();
		}

		public static void LoadMatrix(double[] m)
		{
			Instance?.LoadMatrix(m);
		}

		public static void MatrixMode(MatrixMode mode)
		{
			Instance?.MatrixMode(mode);
		}

		public static void MultMatrix(float[] m)
		{
			Instance?.MultMatrix(m);
		}

		public static void Normal3(double x, double y, double z)
		{
			Instance?.Normal3(x, y, z);
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
		}

		public static void Ortho(double left, double right, double bottom, double top, double zNear, double zFar)
		{
			Instance?.Ortho(left, right, bottom, top, zNear, zFar);
		}

		public static void PolygonOffset(float factor, float units)
		{
			Instance?.PolygonOffset(factor, units);
		}

		public static void PopAttrib()
		{
			Instance?.PopAttrib();
		}

		public static void PopMatrix()
		{
			Instance?.PopMatrix();
		}

		public static void PushAttrib(AttribMask mask)
		{
			Instance?.PushAttrib(mask);
		}

		public static void PushMatrix()
		{
			Instance?.PushMatrix();
		}

		public static void Rotate(double angle, double x, double y, double z)
		{
			Instance?.Rotate(angle, x, y, z);
		}

		public static void Scale(double x, double y, double z)
		{
			Instance?.Scale(x, y, z);
		}

		public static void Scissor(int x, int y, int width, int height)
		{
			Instance?.Scissor(x, y, width, height);
		}

		public static void ShadeModel(ShadingModel model)
		{
			Instance?.ShadeModel(model);
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
		}

		public static void TexCoordPointer(int size, TexCordPointerType type, int stride, IntPtr pointer)
		{
			Instance?.TexCoordPointer(size, type, stride, pointer);
		}

		public static void TexImage2D(TextureTarget target, int level,
			PixelInternalFormat internalFormat,
			int width, int height, int border,
			PixelFormat format,
			PixelType type,
			Byte[] pixels)
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
		}

		public static void TexParameter(TextureTarget target, TextureParameterName pname, int param)
		{
			Instance?.TexParameter(target, pname, param);
		}

		public static void Translate(MatterHackers.VectorMath.Vector3 vector)
		{
			Translate(vector.X, vector.Y, vector.Z);
		}

		public static void Translate(double x, double y, double z)
		{
			Instance?.Translate(x, y, z);
		}

		public static void Vertex2(Vector2 position)
		{
			Vertex2(position.X, position.Y);
		}

		public static void Vertex2(double x, double y)
		{
			Instance?.Vertex2(x, y);
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
		}

		public static void Viewport(int x, int y, int width, int height)
		{
			Instance?.Viewport(x, y, width, height);
		}
	}
}