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
using MatterHackers.Agg;
using MatterHackers.VectorMath;
using Veldrid;

namespace MatterHackers.RenderOpenGl.OpenGl
{
	public class VeldridGLProvider : IOpenGL
	{
		bool glHasBufferObjects = true;

		public bool HardwareAvailable { get; set; } = true;

		public bool GlHasBufferObjects { get { return glHasBufferObjects; } }

		public void DisableGlBuffers()
		{
			glHasBufferObjects = false;
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

		//private ImediateMode currentImediateData = new ImediateMode();

		public void BlendFunc(BlendingFactorSrc sfactor, BlendingFactorDest dfactor)
		{
		}

		public void Scissor(int x, int y, int width, int height)
		{
		}

		public void Enable(EnableCap cap)
		{
		}

		public void ColorMask(bool red, bool green, bool blue, bool alpha)
		{
		}

		public void Disable(EnableCap cap)
		{
		}

		public void DisableClientState(ArrayCap array)
		{
		}

		public void LoadMatrix(double[] m)
		{
		}

		public void MatrixMode(MatrixMode mode)
		{
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
		}

		public void Rotate(double angle, double x, double y, double z)
		{
		}

		public void Scale(double x, double y, double z)
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
			//ImediateMode.currentColor[0] = (byte)red;
			//ImediateMode.currentColor[1] = (byte)green;
			//ImediateMode.currentColor[2] = (byte)blue;
			//ImediateMode.currentColor[3] = (byte)alpha;
		}

		public void LoadIdentity()
		{
		}

		public void PushMatrix()
		{
		}

		public void MultMatrix(float[] m)
		{
			//VeldridGL.commandList.UpdateBuffer(VeldridGL.projectionBuffer, 0, System.Numerics.Matrix4x4.CreatePerspectiveFieldOfView(
			//				1.0f,
			//(float)Window.Width / Window.Height,
			//0.5f,
			//100f));
		}

		public void PopMatrix()
		{
		}

		public void Ortho(double left, double right, double bottom, double top, double zNear, double zFar)
		{
		}

		public void PushAttrib(AttribMask mask)
		{
		}

		public void PopAttrib()
		{

		}

		public void GenTextures(int n, out int textureHandle)
		{
			textureHandle = -1;
		}

		public void BindTexture(TextureTarget target, int texture)
		{
		}

		public void Finish()
		{
		}

		public void TexParameter(TextureTarget target, TextureParameterName pname, int param)
		{
		}

		public void TexImage2D(TextureTarget target, int level,
			PixelInternalFormat internalFormat,
			int width, int height, int border,
			PixelFormat format,
			PixelType type,
			Byte[] pixels)
		{
		}

		public void GenFramebuffers(int n, out int frameBuffers)
		{
			throw new NotImplementedException();
		}

		public void GenRenderbuffers(int n, out int renderBuffers)
		{
			throw new NotImplementedException();
		}

		public void DeleteFramebuffers(int n, ref int frameBuffers)
		{
		}

		public void DeleteRenderbuffers(int n, ref int renderBuffers)
		{
		}

		public void BindRenderbuffer(int renderBuffer)
		{
		}

		//public void RenderbufferStorage(RenderbufferStorage storage, int width, int height)
		//{
		//}

		public void BindFramebuffer(int renderBuffer)
		{
		}

		public void ReadPixels(int x, int y, int width, int height, PixelFormat pixelFormat, PixelType pixelType, byte[] buffer)
		{

		}

		public void ReadBuffer()
		{
		}

		public void FramebufferRenderbuffer(int renderBuffer)
		{
		}

		public ErrorCode GetError()
		{
			throw new NotImplementedException();
		}

		public void Begin(BeginMode mode)
		{
			//currentImediateData.Mode = mode;
		}

		public void End()
		{
			//switch (currentImediateData.Mode)
			//{
			//	case BeginMode.Lines:
			//		{
			//			GL.EnableClientState(ArrayCap.ColorArray);
			//			GL.EnableClientState(ArrayCap.VertexArray);

			//			float[] v = currentImediateData.positions3f.Array;
			//			byte[] c = currentImediateData.color4b.Array;
			//			// pin the data, so that GC doesn't move them, while used
			//			// by native code
			//			unsafe
			//			{
			//				fixed (float* pv = v)
			//				{
			//					fixed (byte* pc = c)
			//					{
			//						GL.ColorPointer(4, ColorPointerType.UnsignedByte, 0, new IntPtr(pc));
			//						GL.VertexPointer(currentImediateData.vertexCount, VertexPointerType.Float, 0, new IntPtr(pv));
			//						GL.DrawArrays(currentImediateData.Mode, 0, currentImediateData.positions3f.Count / currentImediateData.vertexCount);
			//					}
			//				}
			//			}
			//			GL.DisableClientState(ArrayCap.VertexArray);
			//			GL.DisableClientState(ArrayCap.ColorArray);
			//		}
			//		break;

			//	case BeginMode.TriangleFan:
			//	case BeginMode.Triangles:
			//	case BeginMode.TriangleStrip:
			//		{
			//			GL.EnableClientState(ArrayCap.ColorArray);
			//			GL.EnableClientState(ArrayCap.VertexArray);
			//			GL.EnableClientState(ArrayCap.TextureCoordArray);

			//			float[] v = currentImediateData.positions3f.Array;
			//			byte[] c = currentImediateData.color4b.Array;
			//			float[] t = currentImediateData.textureCoords2f.Array;
			//			// pin the data, so that GC doesn't move them, while used
			//			// by native code
			//			unsafe
			//			{
			//				fixed (float* pv = v, pt = t)
			//				{
			//					fixed (byte* pc = c)
			//					{
			//						GL.ColorPointer(4, ColorPointerType.UnsignedByte, 0, new IntPtr(pc));
			//						GL.VertexPointer(currentImediateData.vertexCount, VertexPointerType.Float, 0, new IntPtr(pv));
			//						GL.TexCoordPointer(2, TexCordPointerType.Float, 0, new IntPtr(pt));
			//						GL.DrawArrays(currentImediateData.Mode, 0, currentImediateData.positions3f.Count / currentImediateData.vertexCount);
			//					}
			//				}
			//			}
			//			GL.DisableClientState(ArrayCap.VertexArray);
			//			GL.DisableClientState(ArrayCap.TextureCoordArray);
			//			GL.DisableClientState(ArrayCap.ColorArray);
			//		}
			//		break;

			//	default:
			//		throw new NotImplementedException();
			//}
		}

		public void TexCoord2(Vector2 uv)
		{
			TexCoord2(uv.X, uv.Y);
		}

		public void TexCoord2(double x, double y)
		{
		}

		public void Vertex2(Vector2 position)
		{
			Vertex2(position.X, position.Y);
		}

		public void Vertex2(double x, double y)
		{
			//currentImediateData.vertexCount = 2;
			//currentImediateData.positions3f.Add((float)x);
			//currentImediateData.positions3f.Add((float)y);

			//currentImediateData.color4b.add(ImediateMode.currentColor[0]);
			//currentImediateData.color4b.add(ImediateMode.currentColor[1]);
			//currentImediateData.color4b.add(ImediateMode.currentColor[2]);
			//currentImediateData.color4b.add(ImediateMode.currentColor[3]);
		}

		public void Normal3(double x, double y, double z)
		{
		}

		public void Vertex3(Vector3 position)
		{
			Vertex3(position.X, position.Y, position.Z);
		}

		public void Vertex3(double x, double y, double z)
		{
		}

		public void DeleteTextures(int n, ref int textures)
		{
		}

		public string GetString(StringName name)
		{
			return "";
		}

		public void BindBuffer(BufferTarget target, int buffer)
		{
		}

		public void BufferData(BufferTarget target, int size, IntPtr data, BufferUsageHint usage)
		{
		}

		public void EnableClientState(ArrayCap arrayCap)
		{
		}

		int currentArrayBufferIndex = 0;
		int currentElementArrayBufferIndex = 0;
		int genBuffersIndex = 1; // start at 1 so we can use 0 as a not initialize tell.
		Dictionary<int, byte[]> bufferData = new Dictionary<int, byte[]>();

		public void GenBuffers(int n, out int buffers)
		{
			buffers = -1;
		}

		public void DeleteBuffers(int n, ref int buffers)
		{

		}

		public void ColorPointer(int size, ColorPointerType type, int stride, byte[] pointer)
		{
		}

		public void ColorPointer(int size, ColorPointerType type, int stride, IntPtr pointer)
		{
		}

		public void NormalPointer(NormalPointerType type, int stride, float[] pointer)
		{
		}

		public void NormalPointer(NormalPointerType type, int stride, IntPtr pointer)
		{
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
		}

		public void TexCoordPointer(int size, TexCordPointerType type, int stride, IntPtr pointer)
		{

		}

		public void DrawRangeElements(BeginMode mode, int start, int end, int count, DrawElementsType type, IntPtr indices)
		{
		}

		public void DepthMask(bool flag)
		{
		}

		public void ClearDepth(double depth)
		{
		}

		public void Viewport(int x, int y, int width, int height)
		{
			currentViewport = new ViewPortData(x, y, width, height);
		}

		public void Clear(ClearBufferMask mask)
		{
		}

		public void Light(LightName light, LightParameter pname, float[] param)
		{
		}

		public void ShadeModel(ShadingModel model)
		{
		}

		public void FrontFace(FrontFaceDirection mode)
		{
		}

		public void CullFace(CullFaceMode mode)
		{
		}

		public void DepthFunc(DepthFunction func)
		{
		}

		public void ColorMaterial(MaterialFace face, ColorMaterialParameter mode)
		{
		}

		public void DrawArrays(BeginMode mode, int first, int count)
		{
		}

		public void PolygonOffset(float factor, float units)
		{
		}
	}
}
