/*
Copyright (c) 2026, Lars Brubaker
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
using MatterHackers.RenderGl.OpenGl;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.Tests
{
	/// <summary>
	/// A do-nothing <see cref="IGpuContext"/> that records the immediate mode calls it receives.
	/// Used to prove that GL-context-bound caches (textures, tesselators, display lists) route
	/// their draw calls to the context they were asked for rather than to some other context that
	/// happened to populate the cache first.
	/// </summary>
	public class RecordingGpuContext : IGpuContext
	{
		private int nextGeneratedId;

		/// <param name="idBase">
		/// Starting value for generated texture/list/buffer ids. Give each context a distinct base so
		/// a test can tell which context a handle came from.
		/// </param>
		public RecordingGpuContext(int idBase = 1)
		{
			this.IdBase = idBase;
			this.nextGeneratedId = idBase;
		}

		public int IdBase { get; }

		public int BeginCount { get; private set; }

		public int EndCount { get; private set; }

		public int Vertex2Count { get; private set; }

		public int TexCoord2Count { get; private set; }

		public List<int> BoundTextures { get; } = new List<int>();

		public List<int> GeneratedTextures { get; } = new List<int>();

		public List<int> CompiledLists { get; } = new List<int>();

		public List<int> CalledLists { get; } = new List<int>();

		public List<int> DeletedLists { get; } = new List<int>();

		/// <summary>
		/// Zeros the recorded counters so a test can measure only the calls made after this point.
		/// Generated handle history is intentionally kept.
		/// </summary>
		public void ResetCallRecording()
		{
			BeginCount = 0;
			EndCount = 0;
			Vertex2Count = 0;
			TexCoord2Count = 0;
			BoundTextures.Clear();
			CalledLists.Clear();
		}

		/// <summary>
		/// True if any immediate mode geometry was submitted to this context since the last reset.
		/// </summary>
		public bool GotImmediateModeCalls => BeginCount > 0 || EndCount > 0 || Vertex2Count > 0 || TexCoord2Count > 0;

		private int NextId() => nextGeneratedId++;

		public bool GlHasBufferObjects => true;

		public void Begin(BeginMode mode) => BeginCount++;

		public void End() => EndCount++;

		public void Vertex2(double x, double y) => Vertex2Count++;

		public void TexCoord2(double x, double y) => TexCoord2Count++;

		public void BindTexture(int target, int texture) => BoundTextures.Add(texture);

		public int GenTexture()
		{
			var id = NextId();
			GeneratedTextures.Add(id);
			return id;
		}

		public void GenTextures(int n, out int textures)
		{
			textures = GenTexture();
		}

		// The extension string keeps ImageTexturePlugin from rounding texture sizes up to powers of two,
		// which keeps the recorded geometry easy to reason about.
		public string GetString(StringName name) => "GL_ARB_texture_non_power_of_two";

		public ErrorCode GetError() => ErrorCode.NoError;

		public int GenBuffer() => NextId();

		public void GenBuffers(int n, out int buffer) => buffer = NextId();

		public int GenFramebuffer() => NextId();

		public void GenFramebuffers(int v, out int fbo) => fbo = NextId();

		public void GenVertexArrays(int n, out int arrays) => arrays = NextId();

		public int GenLists(int v) => NextId();

		public int CreateProgram() => NextId();

		public int CreateShader(int shaderType) => NextId();

		public int GetUniformLocation(int program, string name) => 0;

		public string GetShaderInfoLog(int shader) => string.Empty;

		#region no-op members
		public void ActiveTexture(int texture) { }

		public void AttachShader(int program, int shader) { }

		public void BindBuffer(int target, int buffer) { }

		public void BindFramebuffer(int target, int buffer) { }

		public void BindVertexArray(int vertexArray) { }

		public void BlendFunc(int sfactor, int dfactor) { }

		public void BufferData(int target, int size, IntPtr data, int usage) { }

		public void CallList(int displayListId) => CalledLists.Add(displayListId);

		public void Clear(int mask) { }

		public void ClearColor(double r, double g, double b, double a) { }

		public void ClearDepth(double depth) { }

		public void Color4(byte red, byte green, byte blue, byte alpha) { }

		public void ColorMask(bool red, bool green, bool blue, bool alpha) { }

		public void ColorMaterial(MaterialFace face, ColorMaterialParameter mode) { }

		public void ColorPointer(int size, ColorPointerType type, int stride, IntPtr pointer) { }

		public void CompileShader(int id) { }

		public void CullFace(CullFaceMode mode) { }

		public void DeleteBuffer(int buffer) { }

		public void DeleteLists(int id, int v) => DeletedLists.Add(id);

		public void DeleteShader(int shader) { }

		public void DeleteTexture(int texture) { }

		public void DepthFunc(int func) { }

		public void DepthMask(bool flag) { }

		public void DetachShader(int id, int shader) { }

		public void Disable(int cap) { }

		public void DisableClientState(ArrayCap array) { }

		public void DrawArrays(BeginMode mode, int first, int count) { }

		public void DrawElements(int mode, int count, int elementType, IntPtr indices) { }

		public void DrawRangeElements(BeginMode mode, int start, int end, int count, DrawElementsType type, IntPtr indices) { }

		public void Enable(int cap) { }

		public void EnableClientState(ArrayCap arrayCap) { }

		public void EnableVertexAttribArray(int index) { }

		public void EndList() { }

		public void Finish() { }

		public void FramebufferTexture2D(int target, int attachment, int textarget, int texture, int level) { }

		public void FrontFace(FrontFaceDirection mode) { }

		public void IndexPointer(IndexPointerType type, int stride, IntPtr pointer) { }

		public void Light(LightName light, LightParameter pname, float[] param) { }

		public void LinkProgram(int id) { }

		public void LoadIdentity() { }

		public void LoadMatrix(double[] m) { }

		public void MatrixMode(MatterHackers.RenderGl.OpenGl.MatrixMode mode) { }

		public void MultMatrix(float[] m) { }

		public void NewList(int displayListId, object compile) => CompiledLists.Add(displayListId);

		public void Normal3(double x, double y, double z) { }

		public void NormalPointer(NormalPointerType type, int stride, IntPtr pointer) { }

		public void Ortho(double left, double right, double bottom, double top, double zNear, double zFar) { }

		public void PolygonOffset(float factor, float units) { }

		public void PopAttrib() { }

		public void PopMatrix() { }

		public void PushAttrib(AttribMask mask) { }

		public void PushMatrix() { }

		public void Rotate(double angle, double x, double y, double z) { }

		public void Scale(double x, double y, double z) { }

		public void Scissor(int x, int y, int width, int height) { }

		public void ShadeModel(ShadingModel model) { }

		public void ShaderSource(int id, int count, string src, object p) { }

		public void TexCoordPointer(int size, TexCordPointerType type, int stride, IntPtr pointer) { }

		public void TexEnv(TextureEnvironmentTarget target, TextureEnvParameter pname, float param) { }

		public void TexImage2D(int target, int level, int internalFormat, int width, int height, int border, int format, int type, byte[] pixels) { }

		public void TexParameter(TextureTarget target, TextureParameterName pname, int param) { }

		public void TexParameteri(int target, int pname, int param) { }

		public void Translate(Vector3 vector) { }

		public void Translate(double x, double y, double z) { }

		public void Uniform1f(int location, float v0) { }

		public void Uniform1i(int location, int v0) { }

		public void UniformMatrix4fv(int location, int count, int transpose, float[] value) { }

		public void UseProgram(int program) { }

		public void Vertex3(double x, double y, double z) { }

		public void VertexAttribPointer(int index, int size, int type, int normalized, int stride, IntPtr pointer) { }

		public void VertexPointer(int size, VertexPointerType type, int stride, IntPtr pointer) { }

		public void Viewport(int x, int y, int width, int height) { }
		#endregion no-op members
	}
}
