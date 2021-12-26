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
using MatterHackers.VectorMath;

namespace MatterHackers.RenderOpenGl.OpenGl
{
	public interface IOpenGL
	{
		bool GlHasBufferObjects { get; }

		void Begin(BeginMode mode);

		void BindBuffer(int target, int buffer);
		
		void BindFramebuffer(int target, int buffer);

		void BindTexture(int target, int texture);

		void BlendFunc(int sfactor, int dfactor);

		void BufferData(int target, int size, IntPtr data, int usage);

		void Clear(int mask);

		void ClearDepth(double depth);

		void Color4(byte red, byte green, byte blue, byte alpha);

		void ColorMask(bool red, bool green, bool blue, bool alpha);

		void ColorMaterial(MaterialFace face, ColorMaterialParameter mode);

		void ColorPointer(int size, ColorPointerType type, int stride, IntPtr pointer);

		void CullFace(CullFaceMode mode);

		void DeleteBuffer(int buffer);

		void DeleteTexture(int texture);

		void DepthFunc(int func);

		void DepthMask(bool flag);

		void Disable(int cap);

		void DisableClientState(ArrayCap array);

		void DrawArrays(BeginMode mode, int first, int count);

		void DrawRangeElements(BeginMode mode, int start, int end, int count, DrawElementsType type, IntPtr indices);

		void Enable(int cap);

		void EnableClientState(ArrayCap arrayCap);

		void End();

		void Finish();

		void FrontFace(FrontFaceDirection mode);

		int GenBuffer();

		int GenTexture();

		ErrorCode GetError();

		string GetString(StringName name);

		void Light(LightName light, LightParameter pname, float[] param);

		void LoadIdentity();

		void LoadMatrix(double[] m);

		void MatrixMode(MatrixMode mode);

		void MultMatrix(float[] m);

		void Normal3(double x, double y, double z);

		void NormalPointer(NormalPointerType type, int stride, IntPtr pointer);

		void IndexPointer(IndexPointerType type, int stride, IntPtr pointer);

		void Ortho(double left, double right, double bottom, double top, double zNear, double zFar);

		void PolygonOffset(float factor, float units);

		void PopAttrib();

		void PopMatrix();

		void PushAttrib(AttribMask mask);

		void PushMatrix();

		void Rotate(double angle, double x, double y, double z);

		void Scale(double x, double y, double z);

		void Scissor(int x, int y, int width, int height);

		void ShadeModel(ShadingModel model);

		void TexCoord2(double x, double y);

		void TexCoordPointer(int size, TexCordPointerType type, int stride, IntPtr pointer);

		void TexEnv(TextureEnvironmentTarget target, TextureEnvParameter pname, float param);

		void TexImage2D(int target, int level, int internalFormat, int width, int height, int border, int format, int type, byte[] pixels);

		void TexParameter(TextureTarget target, TextureParameterName pname, int param);

		void Translate(Vector3 vector);

		void Translate(double x, double y, double z);

		void Vertex2(double x, double y);

		void Vertex3(double x, double y, double z);

		void VertexPointer(int size, VertexPointerType type, int stride, IntPtr pointer);

		void Viewport(int x, int y, int width, int height);

        int CreateProgram();

        int CreateShader(int shaderType);
        void BindVertexArray(int vertexArray);
        void ShaderSource(int id, int count, string src, object p);
        void CompileShader(int id);
        void AttachShader(int program, int shader);
        void LinkProgram(int id);
        void DeleteShader(int shader);
        void DetachShader(int id, int shader);
        void GenVertexArrays(int n, out int arrays);
        void GenBuffers(int n, out int buffer);
        void TexParameteri(int target, int pname, int param);
        void GenTextures(int n, out int textures);
        void GenFramebuffers(int v, out int fbo);
        void FramebufferTexture2D(int target, int attachment, int textarget, int texture, int level);
        void UniformMatrix4fv(int location, int count, int transpose, float[] value);
        void VertexAttribPointer(int index, int size, int type, int normalized, int stride, IntPtr pointer);
        void EnableVertexAttribArray(int index);
        void UseProgram(int program);
        int GetUniformLocation(int program, string name);
        void Uniform1i(int location, int v0);
        void ActiveTexture(int texture);
        void DrawElements(int mode, int count, int elementType, IntPtr indices);

		void Uniform1f(int location, float v0);
        void ClearColor(double r, double g, double b, double a);
        int GenFramebuffer();
    }
}