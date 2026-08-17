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
using System.Linq;
using MatterHackers.RenderCore;
using MatterHackers.RenderCore.Testing;
using MatterHackers.RenderGl.Compat;
using MatterHackers.RenderGl.OpenGl;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.Tests
{
	/// <summary>
	/// A <see cref="GlCompatContext"/> wired to a <see cref="RecordingRenderDevice"/> and a render
	/// target, plus the readers the compat tests need to pick their assertions out of the command
	/// stream.
	/// </summary>
	public class GlCompatTestHarness
	{
		private GlCompatTestHarness(RecordingRenderDevice device, GlCompatContext context, IGpuTexture target)
		{
			this.Device = device;
			this.Context = context;
			this.Target = target;
		}

		/// <summary>The device every call is recorded on.</summary>
		public RecordingRenderDevice Device { get; }

		/// <summary>The context under test.</summary>
		public GlCompatContext Context { get; }

		/// <summary>The color attachment drawing goes to.</summary>
		public IGpuTexture Target { get; }

		/// <summary>
		/// Creates a context over a fresh recording device with a color target already set, and drops
		/// the recording of that setup so a test measures only what it does itself.
		/// </summary>
		/// <param name="width">Target width in pixels.</param>
		/// <param name="height">Target height in pixels. The GL to device y flip is measured against this.</param>
		/// <param name="withDepth">Whether to attach a depth buffer.</param>
		public static GlCompatTestHarness Create(uint width = 100, uint height = 50, bool withDepth = false)
		{
			var device = new RecordingRenderDevice();
			var target = device.CreateTexture(new TextureDescriptor(
				width,
				height,
				TextureFormat.Bgra8Unorm,
				TextureUsage.RenderAttachment | TextureUsage.CopySrc,
				1,
				1,
				"colorTarget"));

			IGpuTexture depth = null;
			if (withDepth)
			{
				depth = device.CreateTexture(new TextureDescriptor(
					width,
					height,
					TextureFormat.Depth32Float,
					TextureUsage.RenderAttachment,
					1,
					1,
					"depthTarget"));
			}

			var context = new GlCompatContext(device);
			context.SetRenderTarget(target, depth);
			device.ClearRecording();

			return new GlCompatTestHarness(device, context, target);
		}

		/// <summary>Draws one unit triangle through immediate mode.</summary>
		public void DrawTriangle()
		{
			this.Context.Begin(BeginMode.Triangles);
			this.Context.Vertex2(0, 0);
			this.Context.Vertex2(1, 0);
			this.Context.Vertex2(1, 1);
			this.Context.End();
		}

		/// <summary>Every vertex buffer the context created, in order. Uniform buffers are filtered out.</summary>
		public IReadOnlyList<CreateBufferCommand> VertexBufferCreations()
			=> this.Device.CommandsOf<CreateBufferCommand>()
				.Where(command => (command.Usage & BufferUsage.Vertex) != 0)
				.ToList();

		/// <summary>Every uniform buffer the context created, in order.</summary>
		public IReadOnlyList<CreateBufferCommand> UniformBufferCreations()
			=> this.Device.CommandsOf<CreateBufferCommand>()
				.Where(command => (command.Usage & BufferUsage.Uniform) != 0)
				.ToList();

		/// <summary>The pipelines bound by draws, in the order they were bound. Repeats are kept.</summary>
		public IReadOnlyList<IRenderPipeline> BoundPipelines()
			=> this.Device.CommandsOf<SetPipelineCommand>().Select(command => command.Pipeline).ToList();

		/// <summary>The vertex buffers bound by draws, in the order they were bound.</summary>
		public IReadOnlyList<IGpuBuffer> BoundVertexBuffers()
			=> this.Device.CommandsOf<SetVertexBufferCommand>().Select(command => command.Buffer).ToList();

		/// <summary>The model-view matrix as it reached the shader in the uniform write at a draw.</summary>
		/// <param name="drawIndex">Which uniform write to read, zero based.</param>
		public Matrix4X4 UniformModelView(int drawIndex)
			=> ReadMatrix(this.UniformWrite(drawIndex), GlUniformBlock.ModelViewMatrixOffset);

		/// <summary>The projection matrix as it reached the shader in the uniform write at a draw.</summary>
		/// <param name="drawIndex">Which uniform write to read, zero based.</param>
		public Matrix4X4 UniformProjection(int drawIndex)
			=> ReadMatrix(this.UniformWrite(drawIndex), GlUniformBlock.ProjectionMatrixOffset);

		/// <summary>
		/// The buffer writes that filled uniform blocks, in order. Vertex writes are filtered out: the
		/// immediate mode path pools its vertex buffers too, so both kinds interleave in the stream.
		/// </summary>
		public IReadOnlyList<WriteBufferCommand> UniformWrites()
			=> this.Device.CommandsOf<WriteBufferCommand>()
				.Where(command => (command.Buffer.Usage & BufferUsage.Uniform) != 0)
				.ToList();

		/// <summary>
		/// The buffer writes that filled staged vertex buffers, in order. Like the uniform blocks, a write
		/// covers every batch staged since the previous submit, so nothing is readable until a submit.
		/// </summary>
		public IReadOnlyList<WriteBufferCommand> VertexWrites()
			=> this.Device.CommandsOf<WriteBufferCommand>()
				.Where(command => (command.Buffer.Usage & BufferUsage.Vertex) != 0)
				.ToList();

		/// <summary>
		/// The vertex bytes one draw binds, sliced back out of the batched write they were staged into.
		/// </summary>
		/// <param name="drawIndex">Which draw to read, zero based, counting across submits.</param>
		/// <param name="byteCount">How many bytes the batch occupies - vertex count times the layout stride.</param>
		public byte[] VertexBytesForDraw(int drawIndex, int byteCount)
		{
			var binding = this.Device.CommandsOf<SetVertexBufferCommand>()[drawIndex];
			var write = this.VertexWrites().First(command
				=> ReferenceEquals(command.Buffer, binding.Buffer)
					&& command.Offset <= binding.Offset
					&& binding.Offset + (ulong)byteCount <= command.Offset + (ulong)command.Data.Length);

			return write.Data.AsSpan((int)(binding.Offset - write.Offset), byteCount).ToArray();
		}

		/// <summary>
		/// The bytes of one draw's uniform block. Per-draw blocks are batched: a write covers every draw
		/// staged since the previous submit, one block per <see cref="GlDrawSubmitter.UniformStride"/>
		/// bytes, so a draw's block has to be sliced back out of it.
		/// </summary>
		/// <param name="drawIndex">Which draw to read, zero based, counting across submits.</param>
		public byte[] UniformWrite(int drawIndex) => this.UniformBlocks()[drawIndex];

		/// <summary>Every draw's uniform block, in draw order, unpacked from the batched writes.</summary>
		public IReadOnlyList<byte[]> UniformBlocks()
		{
			var blocks = new List<byte[]>();
			foreach (var write in this.UniformWrites())
			{
				for (int offset = 0;
					offset + GlUniformBlock.SizeInBytes <= write.Data.Length;
					offset += GlDrawSubmitter.UniformStride)
				{
					blocks.Add(write.Data.AsSpan(offset, GlUniformBlock.SizeInBytes).ToArray());
				}
			}

			return blocks;
		}

		/// <summary>Reads the red channel of one vertex out of a colored vertex buffer's bytes.</summary>
		/// <param name="vertices">Bytes built by <see cref="GlImmediateModeBuffer.BuildColoredVertices"/>.</param>
		/// <param name="vertexIndex">Which vertex.</param>
		public static float ColoredVertexRed(byte[] vertices, int vertexIndex)
		{
			int stride = (int)GlShaderKeys.ColoredVertexLayout.ArrayStride;
			return BitConverter.ToSingle(vertices, (vertexIndex * stride) + 12);
		}

		/// <summary>Reads a matrix back out of a uniform block's bytes.</summary>
		/// <param name="block">The bytes written.</param>
		/// <param name="offset">Byte offset of the matrix member.</param>
		public static Matrix4X4 ReadMatrix(byte[] block, int offset)
		{
			double Element(int index) => BitConverter.ToSingle(block, offset + (index * 4));

			return new Matrix4X4(
				Element(0), Element(1), Element(2), Element(3),
				Element(4), Element(5), Element(6), Element(7),
				Element(8), Element(9), Element(10), Element(11),
				Element(12), Element(13), Element(14), Element(15));
		}
	}
}
