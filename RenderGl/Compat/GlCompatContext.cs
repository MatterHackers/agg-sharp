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
*/

using System;
using System.Collections.Generic;
using MatterHackers.RenderCore;
using MatterHackers.RenderGl.OpenGl;
using MatterHackers.VectorMath;

namespace MatterHackers.RenderGl.Compat
{
	/// <summary>
	/// A fixed function GL context emulated on top of the retained <see cref="IRenderDevice"/> seam.
	/// <para>
	/// This is a port of the working emulation semantics in the classic D3D11 context - immediate mode
	/// accumulation, matrix stacks, display lists, attribute push/pop and state shadowing - onto an
	/// interface that has none of GL's state machine. The classic file stays untouched on disk as the
	/// oracle; what is genuinely new here is everything below the accumulators: instead of setting
	/// device state and drawing, a flush turns the shadowed state into a
	/// <see cref="RenderPipelineDescriptor"/>, looks it up in a cache, and records a draw into a lazily
	/// opened render pass.
	/// </para>
	/// <para>
	/// <b>Shaders.</b> User shaders are not emulated. The port decided on canned pipelines only (there
	/// are no plugin or external consumers), so <c>CreateShader</c> and friends throw
	/// <see cref="NotSupportedException"/> rather than pretending. The canned combos are named in
	/// <see cref="GlShaderKeys"/>.
	/// </para>
	/// <para>
	/// <b>Lifetime.</b> This class is transitional by design: it exists so the whole application renders
	/// on the new backend before its consumers migrate off <see cref="IGpuContext"/>, and it shrinks
	/// toward deletion as they do.
	/// </para>
	/// </summary>
	public class GlCompatContext : IGpuContext, IDisposable
	{
		private readonly IRenderDevice device;
		private readonly GlStateShadow state = new GlStateShadow();
		private readonly GlMatrixStacks matrices = new GlMatrixStacks();
		private readonly GlImmediateModeBuffer immediate = new GlImmediateModeBuffer();
		private readonly GlPipelineCache pipelines;
		private readonly GlTextureStore textures;
		private readonly GlDisplayListStore displayLists;
		private readonly GlRenderPassScope passes;
		private readonly GlDrawSubmitter submitter;

		/// <summary>Creates a compat context over a retained device.</summary>
		/// <param name="device">The device every draw is recorded on.</param>
		public GlCompatContext(IRenderDevice device)
		{
			this.device = device ?? throw new ArgumentNullException(nameof(device));
			this.pipelines = new GlPipelineCache(device);
			this.textures = new GlTextureStore(device);
			this.displayLists = new GlDisplayListStore(device);
			this.passes = new GlRenderPassScope(device, this.ApplyDynamicState);
			this.submitter = new GlDrawSubmitter(device, this.state, this.matrices, this.pipelines, this.textures, this.passes);
		}

		/// <summary>The device this context records onto.</summary>
		public IRenderDevice Device => this.device;

		/// <summary>The pipeline, bind group and shader module caches. Exposed for diagnostics and tests.</summary>
		public GlPipelineCache Pipelines => this.pipelines;

		/// <summary>The render pass this context keeps open across a frame.</summary>
		public GlRenderPassScope Passes => this.passes;

		/// <summary>The shadowed fixed function state.</summary>
		public GlStateShadow State => this.state;

		/// <summary>The matrix stacks.</summary>
		public GlMatrixStacks Matrices => this.matrices;

		/// <summary>
		/// The client array pointers set by <see cref="VertexPointer"/> and friends. Shadowed because
		/// they are state, but nothing consumes them yet: the draw calls that would are the legacy mesh
		/// fallback, which is not implemented here (see <see cref="DrawArrays"/>).
		/// </summary>
		public GlClientArrayPointers ArrayPointers { get; } = new GlClientArrayPointers();

		/// <inheritdoc/>
		public bool GlHasBufferObjects => true;

		/// <summary>
		/// Points subsequent drawing at a color and optional depth texture, ending any pass in progress.
		/// There is no GL call for this - GL has a global current framebuffer - so it is the host's job
		/// to say where a frame goes before the first draw.
		/// </summary>
		/// <param name="colorTarget">The texture to draw into.</param>
		/// <param name="depthTarget">The depth texture, or null for no depth buffer.</param>
		public void SetRenderTarget(IGpuTexture colorTarget, IGpuTexture depthTarget = null)
			=> this.passes.SetTargets(colorTarget, depthTarget);

		/// <summary>Ends the open pass, if any, without submitting.</summary>
		public void FlushPass() => this.passes.FlushPass();

		/// <summary>
		/// Ends the open pass and submits everything recorded. Recycles the per-draw uniform and vertex
		/// buffers, which is safe here and only here: queue writes issued after a submit are ordered
		/// after it.
		/// </summary>
		public void Submit()
		{
			this.passes.FlushPass();
			this.device.Submit();
			this.submitter.ResetPerDrawPools();
		}

		/// <summary>Submits and presents a surface.</summary>
		/// <param name="target">The surface to present.</param>
		public void Present(ISurfaceTarget target)
		{
			this.Submit();
			this.device.Present(target);
		}

		// --- Immediate mode ---

		/// <inheritdoc/>
		public void Begin(BeginMode mode) => this.immediate.Begin(mode);

		/// <inheritdoc/>
		public void End()
		{
			if (this.immediate.Mode == BeginMode.TriangleFan)
			{
				this.immediate.ConvertTriangleFanToTriangles();
			}

			if (this.displayLists.IsRecording)
			{
				this.displayLists.Record(this.immediate);
				return;
			}

			this.FlushImmediateMode();
		}

		/// <inheritdoc/>
		public void Vertex2(double x, double y) => this.immediate.AddVertex(x, y, 0);

		/// <inheritdoc/>
		public void Vertex3(double x, double y, double z) => this.immediate.AddVertex(x, y, z);

		/// <inheritdoc/>
		public void Color4(byte red, byte green, byte blue, byte alpha)
			=> this.immediate.SetColor(red, green, blue, alpha);

		/// <inheritdoc/>
		public void TexCoord2(double x, double y) => this.immediate.AddTexCoord(x, y);

		/// <inheritdoc/>
		public void Normal3(double x, double y, double z) => this.immediate.AddNormal(x, y, z);

		// --- State ---

		/// <inheritdoc/>
		public void Enable(int cap)
		{
			this.state.Enable(cap);
			if (cap == (int)EnableCap.ScissorTest)
			{
				this.ApplyScissorToOpenPass();
			}
		}

		/// <inheritdoc/>
		public void Disable(int cap)
		{
			this.state.Disable(cap);
			if (cap == (int)EnableCap.ScissorTest)
			{
				this.ApplyScissorToOpenPass();
			}
		}

		/// <inheritdoc/>
		public void EnableClientState(ArrayCap arrayCap) => this.state.SetClientState(arrayCap, true);

		/// <inheritdoc/>
		public void DisableClientState(ArrayCap array) => this.state.SetClientState(array, false);

		/// <inheritdoc/>
		public void BlendFunc(int sfactor, int dfactor)
		{
			this.state.BlendSourceFactor = sfactor;
			this.state.BlendDestinationFactor = dfactor;
		}

		/// <inheritdoc/>
		public void DepthFunc(int func) => this.state.DepthCompare = GlStateShadow.MapCompareFunction(func);

		/// <inheritdoc/>
		public void DepthMask(bool flag) => this.state.DepthMask = flag;

		/// <inheritdoc/>
		public void ColorMask(bool red, bool green, bool blue, bool alpha)
		{
			var mask = ColorWriteMask.None;
			if (red)
			{
				mask |= ColorWriteMask.Red;
			}

			if (green)
			{
				mask |= ColorWriteMask.Green;
			}

			if (blue)
			{
				mask |= ColorWriteMask.Blue;
			}

			if (alpha)
			{
				mask |= ColorWriteMask.Alpha;
			}

			this.state.ColorWriteMask = mask;
		}

		/// <inheritdoc/>
		public void ColorMaterial(MaterialFace face, ColorMaterialParameter mode)
		{
			// Deliberately nothing, matching the classic path: the canned shaders take the vertex color
			// as the material already, so there is nothing for this to select.
		}

		/// <inheritdoc/>
		public void CullFace(CullFaceMode mode)
			=> this.state.CullFaceMode = mode == OpenGl.CullFaceMode.Front ? CullMode.Front : CullMode.Back;

		/// <inheritdoc/>
		public void FrontFace(FrontFaceDirection mode)
			=> this.state.FrontFaceCcw = mode == FrontFaceDirection.Ccw;

		/// <inheritdoc/>
		public void ShadeModel(ShadingModel model) => this.state.FlatShading = model == ShadingModel.Flat;

		/// <summary>
		/// Records the polygon offset. WebGPU carries depth bias as immutable pipeline state, so this
		/// only shadows the values; <see cref="GlPipelineCache.BuildPipelineDescriptor"/> folds them
		/// into <see cref="DepthStencilState"/> at the next draw, and a change simply lands on a
		/// different cache entry. Coplanar overlays get their z-fight mitigation from there.
		/// </summary>
		/// <param name="factor">Slope scaled bias, becoming <c>depthBiasSlopeScale</c>.</param>
		/// <param name="units">Constant bias, truncated to the integer <c>depthBias</c>.</param>
		public void PolygonOffset(float factor, float units)
		{
			this.state.PolygonOffsetFactor = factor;
			this.state.PolygonOffsetUnits = units;
		}

		/// <inheritdoc/>
		public void Light(LightName light, LightParameter pname, float[] param)
		{
			int index = light == LightName.Light0 ? 0 : 1;
			if (index >= this.state.Lights.Length || param == null)
			{
				return;
			}

			var target = this.state.Lights[index];
			switch (pname)
			{
				case LightParameter.Ambient:
					Array.Copy(param, target.Ambient, Math.Min(param.Length, 4));
					break;

				case LightParameter.Diffuse:
					Array.Copy(param, target.Diffuse, Math.Min(param.Length, 4));
					break;

				case LightParameter.Specular:
					Array.Copy(param, target.Specular, Math.Min(param.Length, 4));
					break;

				case LightParameter.Position:
					// GL transforms a light position by the model-view matrix at the moment it is set,
					// not at draw time. Getting this wrong makes lights swim with the camera.
					var modelView = this.matrices.ModelView;
					float x = param.Length > 0 ? param[0] : 0;
					float y = param.Length > 1 ? param[1] : 0;
					float z = param.Length > 2 ? param[2] : 0;
					float w = param.Length > 3 ? param[3] : 0;

					target.Position[0] = (float)((x * modelView.Row0.X) + (y * modelView.Row1.X) + (z * modelView.Row2.X) + (w * modelView.Row3.X));
					target.Position[1] = (float)((x * modelView.Row0.Y) + (y * modelView.Row1.Y) + (z * modelView.Row2.Y) + (w * modelView.Row3.Y));
					target.Position[2] = (float)((x * modelView.Row0.Z) + (y * modelView.Row1.Z) + (z * modelView.Row2.Z) + (w * modelView.Row3.Z));
					target.Position[3] = (float)((x * modelView.Row0.W) + (y * modelView.Row1.W) + (z * modelView.Row2.W) + (w * modelView.Row3.W));
					break;
			}
		}

		// --- Matrices ---

		/// <inheritdoc/>
		public void MatrixMode(OpenGl.MatrixMode mode) => this.matrices.Mode = mode;

		/// <inheritdoc/>
		public void LoadIdentity() => this.matrices.LoadIdentity();

		/// <inheritdoc/>
		public void LoadMatrix(double[] m)
			=> this.matrices.Load(new Matrix4X4(
				m[0], m[1], m[2], m[3],
				m[4], m[5], m[6], m[7],
				m[8], m[9], m[10], m[11],
				m[12], m[13], m[14], m[15]));

		/// <inheritdoc/>
		public void MultMatrix(float[] m) => this.matrices.Multiply(new Matrix4X4(m));

		/// <inheritdoc/>
		public void PushMatrix() => this.matrices.Push();

		/// <inheritdoc/>
		public void PopMatrix() => this.matrices.Pop();

		/// <inheritdoc/>
		public void Ortho(double left, double right, double bottom, double top, double zNear, double zFar)
			=> this.matrices.Ortho(left, right, bottom, top, zNear, zFar);

		/// <inheritdoc/>
		public void Translate(Vector3 vector) => this.matrices.Translate(vector.X, vector.Y, vector.Z);

		/// <inheritdoc/>
		public void Translate(double x, double y, double z) => this.matrices.Translate(x, y, z);

		/// <inheritdoc/>
		public void Rotate(double angle, double x, double y, double z) => this.matrices.Rotate(angle, x, y, z);

		/// <inheritdoc/>
		public void Scale(double x, double y, double z) => this.matrices.Scale(x, y, z);

		/// <inheritdoc/>
		public void PushAttrib(AttribMask mask) => this.state.PushAttrib(mask);

		/// <inheritdoc/>
		public void PopAttrib()
		{
			if (this.state.PopAttrib(out var restored))
			{
				this.Viewport(restored.X, restored.Y, restored.Width, restored.Height);
			}
		}

		// --- Clear, viewport, scissor ---

		/// <inheritdoc/>
		public void Clear(int mask)
			=> this.passes.RequestClear(
				(mask & 0x00004000) != 0,
				(mask & 0x00000100) != 0,
				this.state.ClearValue);

		/// <inheritdoc/>
		public void ClearDepth(double depth)
		{
			// Nothing, as in the classic path: the pass always clears depth to the far plane
			// (DepthAttachment.FarClear, passed by GlRenderPassScope) and nothing in the renderer clears
			// to anything else.
		}

		/// <inheritdoc/>
		public void ClearColor(double r, double g, double b, double a)
			=> this.state.ClearValue = new MatterHackers.RenderCore.ClearColor(r, g, b, a);

		/// <inheritdoc/>
		public void Viewport(int x, int y, int width, int height)
		{
			this.state.SetViewport(new GlViewportRect(x, y, width, height));
			if (this.passes.IsPassOpen)
			{
				this.ApplyViewport(this.passes.EnsurePassOpen());
			}
		}

		/// <inheritdoc/>
		public void Scissor(int x, int y, int width, int height)
		{
			this.state.Scissor = new GlViewportRect(x, y, width, height);
			this.ApplyScissorToOpenPass();
		}

		// --- Textures ---

		/// <inheritdoc/>
		public int GenTexture() => this.textures.GenerateName();

		/// <inheritdoc/>
		public void GenTextures(int n, out int textureName) => textureName = this.textures.GenerateName();

		/// <inheritdoc/>
		public void DeleteTexture(int texture) => this.textures.Delete(texture);

		/// <inheritdoc/>
		public void BindTexture(int target, int texture) => this.state.BindTexture(texture);

		/// <inheritdoc/>
		public void TexImage2D(int target, int level, int internalFormat, int width, int height, int border, int format, int type, byte[] pixels)
		{
			// A queue texture write while a pass is open would be ordered against the submit rather than
			// against the draws around it, so a glyph atlas updated mid-frame could appear in draws that
			// were recorded before the update. End the pass; the next draw re-opens it with LoadOp.Load.
			this.passes.FlushPass();
			this.textures.UploadImage(this.state.BoundTexture(this.state.ActiveTextureUnit), level, width, height, format, pixels);
		}

		/// <inheritdoc/>
		public void TexParameter(TextureTarget target, TextureParameterName pname, int param)
			=> this.textures.SetParameter(this.state.BoundTexture(this.state.ActiveTextureUnit), pname, param);

		/// <inheritdoc/>
		public void TexParameteri(int target, int pname, int param)
			=> this.TexParameter(TextureTarget.Texture2D, (TextureParameterName)pname, param);

		/// <inheritdoc/>
		public void TexEnv(TextureEnvironmentTarget target, TextureEnvParameter pname, float param)
		{
			if (pname == TextureEnvParameter.TextureEnvMode)
			{
				const int GlReplace = 0x1E01;
				this.state.TextureEnvironmentReplace = (int)param == GlReplace;
			}
		}

		/// <inheritdoc/>
		public void ActiveTexture(int texture)
		{
			const int GlTexture0 = 0x84C0;
			if (texture >= GlTexture0 && texture < GlTexture0 + 8)
			{
				this.state.ActiveTextureUnit = texture - GlTexture0;
			}
			else if (texture >= 0 && texture < 8)
			{
				this.state.ActiveTextureUnit = texture;
			}
		}

		// --- Display lists ---

		/// <inheritdoc/>
		public int GenLists(int v) => this.displayLists.GenerateNames(v);

		/// <inheritdoc/>
		public void NewList(int displayListId, object compile) => this.displayLists.BeginRecording(displayListId);

		/// <inheritdoc/>
		public void EndList() => this.displayLists.EndRecording();

		/// <inheritdoc/>
		public void CallList(int displayListId)
		{
			foreach (var entry in this.displayLists.Entries(displayListId))
			{
				if (entry.VertexCount == 0)
				{
					continue;
				}

				bool textured = entry.HasTexCoords && this.HasBoundTexture();
				var buffer = this.displayLists.GetBakedGeometry(entry, textured, this.state.FlatShading);
				this.submitter.Draw(buffer, entry.VertexCount, entry.Mode, textured);
			}
		}

		/// <inheritdoc/>
		public void DeleteLists(int id, int v) => this.displayLists.Delete(id, v);

		// --- Misc ---

		/// <inheritdoc/>
		public ErrorCode GetError() => ErrorCode.NoError;

		/// <inheritdoc/>
		public string GetString(StringName name)
			=> name == StringName.Extensions
				? "ARB_texture_non_power_of_two"
				: "MatterHackers GlCompatContext";

		/// <inheritdoc/>
		public void Finish() => this.Submit();

		/// <inheritdoc/>
		public void IndexPointer(IndexPointerType type, int stride, IntPtr pointer)
		{
			// Nothing, as in the classic path: nothing in the renderer draws through an index array.
		}

		/// <inheritdoc/>
		public void VertexPointer(int size, VertexPointerType type, int stride, IntPtr pointer)
			=> this.ArrayPointers.Vertex = new GlClientArrayPointer(size, stride, pointer);

		/// <inheritdoc/>
		public void ColorPointer(int size, ColorPointerType type, int stride, IntPtr pointer)
			=> this.ArrayPointers.Color = new GlClientArrayPointer(size, stride, pointer);

		/// <inheritdoc/>
		public void TexCoordPointer(int size, TexCordPointerType type, int stride, IntPtr pointer)
			=> this.ArrayPointers.TexCoord = new GlClientArrayPointer(size, stride, pointer);

		/// <inheritdoc/>
		public void NormalPointer(NormalPointerType type, int stride, IntPtr pointer)
			=> this.ArrayPointers.Normal = new GlClientArrayPointer(3, stride, pointer);

		/// <summary>Releases the caches, stores and any open pass.</summary>
		public void Dispose()
		{
			this.passes.Dispose();
			this.displayLists.Dispose();
			this.textures.Dispose();
			this.pipelines.Dispose();
			this.submitter.Dispose();
		}

		// --- Not supported: user shaders (canned pipelines only) ---

		private const string ShaderMessage =
			"GlCompatContext does not emulate user shaders. The renderer draws through the canned "
			+ "pipelines named in GlShaderKeys; add a canned combo there instead of compiling GLSL.";

		/// <summary>Not supported - see <see cref="GlShaderKeys"/>.</summary>
		public int CreateProgram() => throw new NotSupportedException(ShaderMessage);

		/// <summary>Not supported - see <see cref="GlShaderKeys"/>.</summary>
		public int CreateShader(int shaderType) => throw new NotSupportedException(ShaderMessage);

		/// <summary>Not supported - see <see cref="GlShaderKeys"/>.</summary>
		public void ShaderSource(int id, int count, string src, object p) => throw new NotSupportedException(ShaderMessage);

		/// <summary>Not supported - see <see cref="GlShaderKeys"/>.</summary>
		public void CompileShader(int id) => throw new NotSupportedException(ShaderMessage);

		/// <summary>Not supported - see <see cref="GlShaderKeys"/>.</summary>
		public void AttachShader(int program, int shader) => throw new NotSupportedException(ShaderMessage);

		/// <summary>Not supported - see <see cref="GlShaderKeys"/>.</summary>
		public void LinkProgram(int id) => throw new NotSupportedException(ShaderMessage);

		/// <summary>Not supported - see <see cref="GlShaderKeys"/>.</summary>
		public void DeleteShader(int shader) => throw new NotSupportedException(ShaderMessage);

		/// <summary>Not supported - see <see cref="GlShaderKeys"/>.</summary>
		public void DetachShader(int id, int shader) => throw new NotSupportedException(ShaderMessage);

		/// <summary>Not supported - see <see cref="GlShaderKeys"/>.</summary>
		public void UseProgram(int program) => throw new NotSupportedException(ShaderMessage);

		/// <summary>Not supported - see <see cref="GlShaderKeys"/>.</summary>
		public int GetUniformLocation(int program, string name) => throw new NotSupportedException(ShaderMessage);

		/// <summary>Not supported - see <see cref="GlShaderKeys"/>.</summary>
		public void Uniform1i(int location, int v0) => throw new NotSupportedException(ShaderMessage);

		/// <summary>Not supported - see <see cref="GlShaderKeys"/>.</summary>
		public void Uniform1f(int location, float v0) => throw new NotSupportedException(ShaderMessage);

		/// <summary>Not supported - see <see cref="GlShaderKeys"/>.</summary>
		public void UniformMatrix4fv(int location, int count, int transpose, float[] value)
			=> throw new NotSupportedException(ShaderMessage);

		/// <summary>Not supported - see <see cref="GlShaderKeys"/>.</summary>
		public string GetShaderInfoLog(int shader) => throw new NotSupportedException(ShaderMessage);

		/// <summary>Not supported - see <see cref="GlShaderKeys"/>.</summary>
		public void VertexAttribPointer(int index, int size, int type, int normalized, int stride, IntPtr pointer)
			=> throw new NotSupportedException(ShaderMessage);

		/// <summary>Not supported - see <see cref="GlShaderKeys"/>.</summary>
		public void EnableVertexAttribArray(int index) => throw new NotSupportedException(ShaderMessage);

		// --- Not implemented yet ---

		private const string MeshFallbackMessage =
			"TODO (port plan, Phase 3): the client-array draw path is the legacy GL mesh fallback that "
			+ "runs when INativeSceneRenderer.CanRender returns false. The plan closes those gaps in the "
			+ "native renderer rather than teaching the compat layer lit and textured mesh drawing.";

		private const string FramebufferMessage =
			"TODO (port plan, Phase 2/3): render-to-texture goes through GlCompatContext.SetRenderTarget "
			+ "rather than GL framebuffer objects. Wire the remaining callers to that.";

		private const string BufferObjectMessage =
			"TODO (port plan, Phase 3): GL buffer objects have no consumer in the renderer today; "
			+ "retained vertex data is owned by the scene renderer, not by GL names.";

		/// <summary>Not implemented - see the message on the thrown exception.</summary>
		public void DrawArrays(BeginMode mode, int first, int count) => throw new NotImplementedException(MeshFallbackMessage);

		/// <summary>Not implemented - see the message on the thrown exception.</summary>
		public void DrawRangeElements(BeginMode mode, int start, int end, int count, DrawElementsType type, IntPtr indices)
			=> throw new NotImplementedException(MeshFallbackMessage);

		/// <summary>Not implemented - see the message on the thrown exception.</summary>
		public void DrawElements(int mode, int count, int elementType, IntPtr indices)
			=> throw new NotImplementedException(MeshFallbackMessage);

		/// <summary>Not implemented - see the message on the thrown exception.</summary>
		public int GenBuffer() => throw new NotImplementedException(BufferObjectMessage);

		/// <summary>Not implemented - see the message on the thrown exception.</summary>
		public void GenBuffers(int n, out int buffer) => throw new NotImplementedException(BufferObjectMessage);

		/// <summary>Not implemented - see the message on the thrown exception.</summary>
		public void BindBuffer(int target, int buffer) => throw new NotImplementedException(BufferObjectMessage);

		/// <summary>Not implemented - see the message on the thrown exception.</summary>
		public void BufferData(int target, int size, IntPtr data, int usage) => throw new NotImplementedException(BufferObjectMessage);

		/// <summary>Not implemented - see the message on the thrown exception.</summary>
		public void DeleteBuffer(int buffer) => throw new NotImplementedException(BufferObjectMessage);

		/// <summary>
		/// Vertex array objects have no meaning on this seam - vertex layout is baked into the pipeline -
		/// so this is accepted and ignored rather than throwing, because callers bind array 0 to reset.
		/// </summary>
		public void BindVertexArray(int vertexArray)
		{
		}

		/// <summary>Not implemented - vertex layout is pipeline state on this seam.</summary>
		public void GenVertexArrays(int n, out int arrays) => throw new NotImplementedException(BufferObjectMessage);

		/// <summary>Not implemented - see the message on the thrown exception.</summary>
		public void BindFramebuffer(int target, int buffer) => throw new NotImplementedException(FramebufferMessage);

		/// <summary>Not implemented - see the message on the thrown exception.</summary>
		public int GenFramebuffer() => throw new NotImplementedException(FramebufferMessage);

		/// <summary>Not implemented - see the message on the thrown exception.</summary>
		public void GenFramebuffers(int v, out int fbo) => throw new NotImplementedException(FramebufferMessage);

		/// <summary>Not implemented - see the message on the thrown exception.</summary>
		public void FramebufferTexture2D(int target, int attachment, int textarget, int texture, int level)
			=> throw new NotImplementedException(FramebufferMessage);

		// --- The draw path ---

		private bool HasBoundTexture()
		{
			if (!this.state.Texture2DEnabled)
			{
				return false;
			}

			var entry = this.textures.Find(this.state.BoundTexture(0));
			return entry?.Texture != null;
		}

		private void FlushImmediateMode()
		{
			int vertexCount = this.immediate.VertexCount;
			if (vertexCount == 0)
			{
				return;
			}

			bool textured = this.immediate.TexCoords.Count > 0 && this.HasBoundTexture();

			byte[] bytes = textured
				? GlImmediateModeBuffer.BuildTexturedVertices(
					this.immediate.Mode,
					this.immediate.Positions,
					this.immediate.Colors,
					this.immediate.TexCoords,
					this.state.FlatShading)
				: GlImmediateModeBuffer.BuildColoredVertices(
					this.immediate.Mode,
					this.immediate.Positions,
					this.immediate.Colors,
					this.state.FlatShading);

			// Pooled rather than created per flush: a batch-sized allocation every glEnd leaked one GPU
			// buffer per batch per frame with nothing owning it. Each batch still gets a buffer of its
			// own within a submit window - a queue write into a shared buffer is not ordered against the
			// draws in an open pass - which is exactly the guarantee the pool makes.
			var vertexBuffer = this.submitter.AcquireVertexBuffer(bytes);
			this.submitter.Draw(vertexBuffer, vertexCount, this.immediate.Mode, textured);
		}

		/// <summary>
		/// Re-applies the viewport and scissor into a freshly opened pass. Both are pass-scoped state in
		/// WebGPU and reset to the full attachment every time a pass opens, so a mid-frame flush would
		/// otherwise silently widen the clip.
		/// </summary>
		/// <param name="encoder">The pass that just opened.</param>
		private void ApplyDynamicState(IRenderEncoder encoder)
		{
			this.ApplyViewport(encoder);
			this.ApplyScissor(encoder);
		}

		private void ApplyViewport(IRenderEncoder encoder)
		{
			if (!this.state.ViewportSet)
			{
				return;
			}

			var (x, y, width, height) = this.ToDeviceRect(this.state.Viewport);
			encoder.SetViewport(x, y, width, height);
		}

		private void ApplyScissor(IRenderEncoder encoder)
		{
			if (this.state.ScissorEnabled)
			{
				var (x, y, width, height) = this.ToDeviceRect(this.state.Scissor);
				encoder.SetScissor(x, y, width, height);
			}
			else
			{
				// WebGPU has no "scissor off", so disabling means restoring the full attachment.
				encoder.SetScissor(0, 0, this.passes.TargetWidth, this.passes.TargetHeight);
			}
		}

		private void ApplyScissorToOpenPass()
		{
			if (this.passes.IsPassOpen)
			{
				this.ApplyScissor(this.passes.EnsurePassOpen());
			}
		}

		/// <summary>
		/// Converts a GL rectangle, whose y is measured up from the bottom of the target, into the
		/// top-left origin both D3D and WebGPU use, clipped to the attachment.
		/// <para>
		/// The clip is not cosmetic. GL silently ignores the parts of a viewport or scissor that fall
		/// outside the framebuffer and D3D11 forgave the same thing, so callers do push rectangles that
		/// hang off the edge - a widget scrolled above the top of its window flips into a negative y
		/// here. WebGPU validates instead: a scissor that is not wholly inside the attachment is an
		/// error that kills the pass.
		/// </para>
		/// </summary>
		/// <param name="rect">The rectangle in GL coordinates.</param>
		private (int X, int Y, int Width, int Height) ToDeviceRect(GlViewportRect rect)
		{
			// TODO (port plan, Phase 3): the classic path multiplies both rectangles by its
			// ActiveCoordinateScale before handing them to the device, which is how supersampled
			// offscreen captures keep their clip in step with the oversized attachment. Nothing sets a
			// scale on this seam yet; when render-to-texture supersampling lands, it applies here.
			int width = Math.Max(0, rect.Width);
			int height = Math.Max(0, rect.Height);

			int targetWidth = this.passes.TargetWidth;
			int targetHeight = this.passes.TargetHeight;

			int left = Math.Clamp(rect.X, 0, targetWidth);
			int right = Math.Clamp(rect.X + width, 0, targetWidth);
			int top = Math.Clamp(targetHeight - (rect.Y + height), 0, targetHeight);
			int bottom = Math.Clamp(targetHeight - rect.Y, 0, targetHeight);

			return (left, top, right - left, bottom - top);
		}
	}
}
