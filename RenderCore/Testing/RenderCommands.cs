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

namespace MatterHackers.RenderCore.Testing
{
	/// <summary>
	/// One call a <see cref="RecordingRenderDevice"/> received. Commands are records so a test can
	/// compare them structurally, and every one has a terse <c>ToString</c> so
	/// <see cref="RecordingRenderDevice.Dump"/> reads like a render trace.
	/// </summary>
	public abstract record RenderCommand
	{
	}

	/// <summary>A buffer was created.</summary>
	/// <param name="Buffer">The stub buffer handed back.</param>
	/// <param name="Usage">Declared usages.</param>
	/// <param name="SizeInBytes">Requested size.</param>
	/// <param name="InitialDataLength">Bytes of initial contents supplied.</param>
	public sealed record CreateBufferCommand(IGpuBuffer Buffer, BufferUsage Usage, ulong SizeInBytes, int InitialDataLength) : RenderCommand
	{
		/// <inheritdoc/>
		public override string ToString()
			=> $"CreateBuffer {this.Buffer.Label} {this.Usage} {this.SizeInBytes} bytes"
			+ (this.InitialDataLength > 0 ? $" (+{this.InitialDataLength} initial)" : string.Empty);
	}

	/// <summary>A texture was created.</summary>
	/// <param name="Texture">The stub texture handed back.</param>
	/// <param name="Descriptor">The descriptor it was created from.</param>
	public sealed record CreateTextureCommand(IGpuTexture Texture, TextureDescriptor Descriptor) : RenderCommand
	{
		/// <inheritdoc/>
		public override string ToString() => $"CreateTexture {this.Texture.Label} {this.Descriptor}";
	}

	/// <summary>A sampler was created.</summary>
	/// <param name="Sampler">The stub sampler handed back.</param>
	/// <param name="Descriptor">The descriptor it was created from.</param>
	public sealed record CreateSamplerCommand(ISampler Sampler, SamplerDescriptor Descriptor) : RenderCommand
	{
		/// <inheritdoc/>
		public override string ToString() => $"CreateSampler {this.Sampler.Label} {this.Descriptor}";
	}

	/// <summary>A shader module was created from a source key.</summary>
	/// <param name="Module">The stub module handed back.</param>
	/// <param name="SourceKey">The key that was resolved.</param>
	public sealed record CreateShaderModuleCommand(IShaderModule Module, string SourceKey) : RenderCommand
	{
		/// <inheritdoc/>
		public override string ToString() => $"CreateShaderModule {this.SourceKey}";
	}

	/// <summary>A render pipeline was created.</summary>
	/// <param name="Pipeline">The stub pipeline handed back.</param>
	/// <param name="Descriptor">The descriptor it was created from.</param>
	public sealed record CreateRenderPipelineCommand(IRenderPipeline Pipeline, RenderPipelineDescriptor Descriptor) : RenderCommand
	{
		/// <inheritdoc/>
		public override string ToString() => $"CreateRenderPipeline {this.Pipeline.Label} {this.Descriptor}";
	}

	/// <summary>A bind group was created.</summary>
	/// <param name="BindGroup">The stub bind group handed back.</param>
	/// <param name="Descriptor">The descriptor it was created from.</param>
	public sealed record CreateBindGroupCommand(IBindGroup BindGroup, BindGroupDescriptor Descriptor) : RenderCommand
	{
		/// <inheritdoc/>
		public override string ToString() => $"CreateBindGroup {this.BindGroup.Label} {this.Descriptor}";
	}

	/// <summary>A render pass was opened.</summary>
	/// <param name="Encoder">The encoder handed back.</param>
	/// <param name="Descriptor">Attachments and load/store ops.</param>
	public sealed record BeginRenderPassCommand(RecordingRenderEncoder Encoder, RenderPassDescriptor Descriptor) : RenderCommand
	{
		/// <inheritdoc/>
		public override string ToString() => $"BeginRenderPass {this.Encoder.Label} {this.Descriptor}";
	}

	/// <summary>A render pass was ended by disposing its encoder.</summary>
	/// <param name="Encoder">The encoder that ended.</param>
	public sealed record EndRenderPassCommand(RecordingRenderEncoder Encoder) : RenderCommand
	{
		/// <inheritdoc/>
		public override string ToString() => $"EndRenderPass {this.Encoder.Label}";
	}

	/// <summary>Bytes were written into a buffer.</summary>
	/// <param name="Buffer">Destination buffer.</param>
	/// <param name="Offset">Byte offset written at.</param>
	/// <param name="Data">A copy of the bytes written - the caller's span may be scratch memory.</param>
	public sealed record WriteBufferCommand(IGpuBuffer Buffer, ulong Offset, byte[] Data) : RenderCommand
	{
		/// <inheritdoc/>
		public override string ToString() => $"WriteBuffer {this.Buffer.Label}+{this.Offset} {this.Data.Length} bytes";
	}

	/// <summary>Pixels were uploaded into a texture.</summary>
	/// <param name="Texture">Destination texture.</param>
	/// <param name="BytesPerRow">Source row stride.</param>
	/// <param name="Data">A copy of the pixels uploaded.</param>
	/// <param name="MipLevel">Destination mip level; 0 is the full resolution image.</param>
	public sealed record WriteTextureCommand(IGpuTexture Texture, uint BytesPerRow, byte[] Data, uint MipLevel = 0) : RenderCommand
	{
		/// <inheritdoc/>
		public override string ToString()
			=> $"WriteTexture {this.Texture.Label} {this.Data.Length} bytes @ {this.BytesPerRow}/row"
			+ (this.MipLevel == 0 ? string.Empty : $" mip {this.MipLevel}");
	}

	/// <summary>A texture was read back.</summary>
	/// <param name="Texture">Texture that was read.</param>
	/// <param name="Result">The layout reported to the caller.</param>
	public sealed record ReadTextureCommand(IGpuTexture Texture, TextureReadResult Result) : RenderCommand
	{
		/// <inheritdoc/>
		public override string ToString() => $"ReadTexture {this.Texture.Label} {this.Result}";
	}

	/// <summary>Recorded work was submitted to the queue.</summary>
	public sealed record SubmitCommand : RenderCommand
	{
		/// <inheritdoc/>
		public override string ToString() => "Submit";
	}

	/// <summary>A surface was presented.</summary>
	/// <param name="Target">The surface presented.</param>
	public sealed record PresentCommand(ISurfaceTarget Target) : RenderCommand
	{
		/// <inheritdoc/>
		public override string ToString() => $"Present {this.Target.Label}";
	}

	/// <summary>A pipeline was bound inside a pass.</summary>
	/// <param name="Encoder">The pass it happened in.</param>
	/// <param name="Pipeline">The pipeline bound.</param>
	public sealed record SetPipelineCommand(RecordingRenderEncoder Encoder, IRenderPipeline Pipeline) : RenderCommand
	{
		/// <inheritdoc/>
		public override string ToString() => $"  SetPipeline {this.Pipeline.Label}";
	}

	/// <summary>A bind group was bound inside a pass.</summary>
	/// <param name="Encoder">The pass it happened in.</param>
	/// <param name="Index">The group index.</param>
	/// <param name="BindGroup">The group bound.</param>
	public sealed record SetBindGroupCommand(RecordingRenderEncoder Encoder, int Index, IBindGroup BindGroup) : RenderCommand
	{
		/// <inheritdoc/>
		public override string ToString() => $"  SetBindGroup {this.Index} {this.BindGroup.Label}";
	}

	/// <summary>A vertex buffer was bound inside a pass.</summary>
	/// <param name="Encoder">The pass it happened in.</param>
	/// <param name="Slot">The slot bound.</param>
	/// <param name="Buffer">The buffer bound.</param>
	/// <param name="Offset">Byte offset of the first vertex.</param>
	public sealed record SetVertexBufferCommand(RecordingRenderEncoder Encoder, int Slot, IGpuBuffer Buffer, ulong Offset) : RenderCommand
	{
		/// <inheritdoc/>
		public override string ToString() => $"  SetVertexBuffer {this.Slot} {this.Buffer.Label}+{this.Offset}";
	}

	/// <summary>An index buffer was bound inside a pass.</summary>
	/// <param name="Encoder">The pass it happened in.</param>
	/// <param name="Buffer">The buffer bound.</param>
	/// <param name="Format">Index element width.</param>
	/// <param name="Offset">Byte offset of the first index.</param>
	public sealed record SetIndexBufferCommand(RecordingRenderEncoder Encoder, IGpuBuffer Buffer, IndexFormat Format, ulong Offset) : RenderCommand
	{
		/// <inheritdoc/>
		public override string ToString() => $"  SetIndexBuffer {this.Buffer.Label}+{this.Offset} {this.Format}";
	}

	/// <summary>The viewport was set inside a pass.</summary>
	/// <param name="Encoder">The pass it happened in.</param>
	/// <param name="X">Left edge.</param>
	/// <param name="Y">Top edge.</param>
	/// <param name="Width">Width.</param>
	/// <param name="Height">Height.</param>
	/// <param name="MinDepth">Near depth bound.</param>
	/// <param name="MaxDepth">Far depth bound.</param>
	public sealed record SetViewportCommand(
		RecordingRenderEncoder Encoder,
		float X,
		float Y,
		float Width,
		float Height,
		float MinDepth,
		float MaxDepth) : RenderCommand
	{
		/// <inheritdoc/>
		public override string ToString()
			=> $"  SetViewport {this.X},{this.Y} {this.Width}x{this.Height} depth {this.MinDepth}..{this.MaxDepth}";
	}

	/// <summary>The scissor rectangle was set inside a pass.</summary>
	/// <param name="Encoder">The pass it happened in.</param>
	/// <param name="X">Left edge.</param>
	/// <param name="Y">Top edge.</param>
	/// <param name="Width">Width.</param>
	/// <param name="Height">Height.</param>
	public sealed record SetScissorCommand(RecordingRenderEncoder Encoder, int X, int Y, int Width, int Height) : RenderCommand
	{
		/// <inheritdoc/>
		public override string ToString() => $"  SetScissor {this.X},{this.Y} {this.Width}x{this.Height}";
	}

	/// <summary>A non-indexed draw.</summary>
	/// <param name="Encoder">The pass it happened in.</param>
	/// <param name="VertexCount">Number of vertices.</param>
	/// <param name="FirstVertex">Index of the first vertex.</param>
	public sealed record DrawCommand(RecordingRenderEncoder Encoder, int VertexCount, int FirstVertex) : RenderCommand
	{
		/// <inheritdoc/>
		public override string ToString() => $"  Draw {this.VertexCount} from {this.FirstVertex}";
	}

	/// <summary>An indexed draw.</summary>
	/// <param name="Encoder">The pass it happened in.</param>
	/// <param name="IndexCount">Number of indices.</param>
	/// <param name="FirstIndex">Index of the first index.</param>
	/// <param name="BaseVertex">Value added to every index.</param>
	public sealed record DrawIndexedCommand(RecordingRenderEncoder Encoder, int IndexCount, int FirstIndex, int BaseVertex) : RenderCommand
	{
		/// <inheritdoc/>
		public override string ToString()
			=> $"  DrawIndexed {this.IndexCount} from {this.FirstIndex} base {this.BaseVertex}";
	}
}
