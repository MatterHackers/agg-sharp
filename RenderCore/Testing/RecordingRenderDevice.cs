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
using System.Threading.Tasks;

namespace MatterHackers.RenderCore.Testing
{
	/// <summary>
	/// An <see cref="IRenderDevice"/> that talks to no GPU and records every call it receives as a
	/// structured command list. This is how rendering logic - the compat layer's accumulators, the
	/// scene renderer's pass plans - is tested headlessly: the assertion is on the command stream, not
	/// on pixels.
	/// <para>
	/// It is deliberately strict about the rules the real backend enforces, because a test that never
	/// sees the rule broken is worthless: passes do not nest, and readback, submit and present all
	/// throw while a pass is open.
	/// </para>
	/// </summary>
	public class RecordingRenderDevice : IRenderDevice
	{
		private readonly List<RenderCommand> commands = new List<RenderCommand>();
		private readonly List<IShaderSourceProvider> shaderSources = new List<IShaderSourceProvider>();
		private readonly Dictionary<string, int> handleCounts = new Dictionary<string, int>();
		private RecordingRenderEncoder openEncoder;

		/// <summary>Every call received, oldest first.</summary>
		public IReadOnlyList<RenderCommand> Commands => this.commands;

		/// <summary>
		/// The limits this double reports and enforces. Settable so a test can pin a small
		/// <see cref="DeviceLimits.MaxBufferSize"/> and prove the callers that split their data actually
		/// split it - a real device's 256 MiB would need a several hundred megabyte mesh to reach.
		/// </summary>
		public DeviceLimits Limits { get; set; } = new DeviceLimits(DeviceLimits.DefaultMaxBufferSize);

		/// <summary>True once <see cref="Dispose"/> has been called.</summary>
		public bool IsDisposed { get; private set; }

		/// <summary>The pass currently open, or null. Tests read this to prove a pass was flushed.</summary>
		public RecordingRenderEncoder OpenPass => this.openEncoder;

		/// <summary>Drops the recorded commands so a test can measure only what follows.</summary>
		public void ClearRecording() => this.commands.Clear();

		/// <summary>Every recorded command of one kind, in order.</summary>
		/// <typeparam name="T">The command type wanted.</typeparam>
		public IReadOnlyList<T> CommandsOf<T>()
			where T : RenderCommand
			=> this.commands.OfType<T>().ToList();

		/// <summary>
		/// The whole command stream as one newline separated string, draws indented under their pass.
		/// Intended as the failure message of a sequence assertion - a diff of this reads like a render
		/// trace.
		/// </summary>
		public string Dump() => string.Join(Environment.NewLine, this.commands.Select(command => command.ToString()));

		/// <inheritdoc/>
		public IGpuBuffer CreateBuffer(BufferUsage usage, ulong sizeInBytes, ReadOnlySpan<byte> initialData = default)
		{
			if (initialData.Length > (int)Math.Min(sizeInBytes, int.MaxValue))
			{
				throw new ArgumentException("Initial data is larger than the buffer.", nameof(initialData));
			}

			// Refused exactly as the native device refuses it: a test that never sees the limit enforced
			// would not prove that what a caller split really fits.
			if (sizeInBytes > this.Limits.MaxBufferSize)
			{
				throw new InvalidOperationException(
					$"A {sizeInBytes:N0} byte buffer exceeds this device's maxBufferSize of"
					+ $" {this.Limits.MaxBufferSize:N0} bytes.");
			}

			var buffer = new StubBuffer(this.NextLabel("buffer"), usage, sizeInBytes);
			this.Record(new CreateBufferCommand(buffer, usage, sizeInBytes, initialData.Length));
			return buffer;
		}

		/// <inheritdoc/>
		public IGpuTexture CreateTexture(in TextureDescriptor descriptor)
		{
			var texture = new StubTexture(this.NextLabel("texture"), descriptor);
			this.Record(new CreateTextureCommand(texture, descriptor));
			return texture;
		}

		/// <inheritdoc/>
		public ISampler CreateSampler(in SamplerDescriptor descriptor)
		{
			var sampler = new StubSampler(this.NextLabel("sampler"), descriptor);
			this.Record(new CreateSamplerCommand(sampler, descriptor));
			return sampler;
		}

		/// <inheritdoc/>
		public IShaderModule CreateShaderModule(string sourceKey)
		{
			if (string.IsNullOrEmpty(sourceKey))
			{
				throw new ArgumentException("A shader source key is required.", nameof(sourceKey));
			}

			// With no providers registered the device is permissive: a test exercising draw logic
			// should not have to carry WGSL around. Once a test does register sources, an unknown key
			// is a real error and is reported the way the native device reports it.
			string source = null;
			foreach (var provider in this.shaderSources)
			{
				source = provider.TryGetSource(sourceKey);
				if (source != null)
				{
					break;
				}
			}

			if (source == null && this.shaderSources.Count > 0)
			{
				throw new ArgumentException($"No registered shader source for key '{sourceKey}'.", nameof(sourceKey));
			}

			var module = new StubShaderModule(this.NextLabel("shader"), sourceKey, source);
			this.Record(new CreateShaderModuleCommand(module, sourceKey));
			return module;
		}

		/// <inheritdoc/>
		public void RegisterShaderSources(IShaderSourceProvider provider)
		{
			if (provider == null)
			{
				throw new ArgumentNullException(nameof(provider));
			}

			this.shaderSources.Add(provider);
		}

		/// <inheritdoc/>
		public IRenderPipeline CreateRenderPipeline(in RenderPipelineDescriptor descriptor)
		{
			var pipeline = new StubRenderPipeline(this.NextLabel("pipeline"), descriptor);
			this.Record(new CreateRenderPipelineCommand(pipeline, descriptor));
			return pipeline;
		}

		/// <inheritdoc/>
		public IBindGroup CreateBindGroup(in BindGroupDescriptor descriptor)
		{
			var bindGroup = new StubBindGroup(this.NextLabel("bindGroup"), descriptor);
			this.Record(new CreateBindGroupCommand(bindGroup, descriptor));
			return bindGroup;
		}

		/// <inheritdoc/>
		public IRenderEncoder BeginRenderPass(in RenderPassDescriptor descriptor)
		{
			this.ThrowIfPassOpen("begin a render pass");

			var encoder = new RecordingRenderEncoder(this, this.NextLabel("pass"), descriptor);
			this.openEncoder = encoder;
			this.Record(new BeginRenderPassCommand(encoder, descriptor));
			return encoder;
		}

		/// <inheritdoc/>
		public void WriteBuffer(IGpuBuffer buffer, ulong offset, ReadOnlySpan<byte> data)
		{
			if (buffer == null)
			{
				throw new ArgumentNullException(nameof(buffer));
			}

			this.Record(new WriteBufferCommand(buffer, offset, data.ToArray()));
		}

		/// <inheritdoc/>
		public void WriteTexture(IGpuTexture texture, ReadOnlySpan<byte> data, uint bytesPerRow, uint mipLevel = 0)
		{
			if (texture == null)
			{
				throw new ArgumentNullException(nameof(texture));
			}

			// The native device would fault on an out of range level, so the double does too - a test
			// that never sees the rule broken would not prove the mip accumulation picked real levels.
			if (mipLevel >= texture.Descriptor.MipLevelCount)
			{
				throw new ArgumentOutOfRangeException(
					nameof(mipLevel),
					$"Texture '{texture.Label}' has {texture.Descriptor.MipLevelCount} mip level(s); level {mipLevel} does not exist.");
			}

			this.Record(new WriteTextureCommand(texture, bytesPerRow, data.ToArray(), mipLevel));
		}

		/// <inheritdoc/>
		public ValueTask<TextureReadResult> ReadTextureAsync(IGpuTexture source, Memory<byte> destination)
		{
			if (source == null)
			{
				throw new ArgumentNullException(nameof(source));
			}

			this.ThrowIfPassOpen("read a texture back");

			var descriptor = source.Descriptor;
			uint rowStride = TextureFormatInfo.AlignedRowStride(descriptor.Format, descriptor.Width);
			var result = new TextureReadResult(descriptor.Width, descriptor.Height, rowStride);
			if ((ulong)destination.Length < result.TotalBytes)
			{
				throw new ArgumentException(
					$"Destination holds {destination.Length} bytes; the padded read needs {result.TotalBytes}.",
					nameof(destination));
			}

			// Zeroed, not left alone: a caller that forgets to honor RowStride then reads deterministic
			// zeros rather than whatever the buffer happened to contain.
			destination.Span.Slice(0, (int)result.TotalBytes).Clear();
			this.Record(new ReadTextureCommand(source, result));

			// Completed synchronously, matching the native desktop fast path (wgpuDevicePoll with
			// wait: true resolves the map before this method returns), so awaiting costs nothing.
			return new ValueTask<TextureReadResult>(result);
		}

		/// <inheritdoc/>
		public void Submit()
		{
			this.ThrowIfPassOpen("submit");
			this.Record(new SubmitCommand());
		}

		/// <inheritdoc/>
		public void Present(ISurfaceTarget target)
		{
			if (target == null)
			{
				throw new ArgumentNullException(nameof(target));
			}

			this.ThrowIfPassOpen("present");
			this.Record(new PresentCommand(target));
		}

		/// <inheritdoc/>
		public void Dispose()
		{
			this.IsDisposed = true;
		}

		/// <summary>Appends a command to the recording. Used by the encoder this device handed out.</summary>
		/// <param name="command">The command to record.</param>
		internal void Record(RenderCommand command) => this.commands.Add(command);

		/// <summary>Called by the encoder when it is disposed, so the device knows the pass closed.</summary>
		/// <param name="encoder">The encoder that ended.</param>
		internal void EndPass(RecordingRenderEncoder encoder)
		{
			if (ReferenceEquals(this.openEncoder, encoder))
			{
				this.openEncoder = null;
			}

			this.Record(new EndRenderPassCommand(encoder));
		}

		private void ThrowIfPassOpen(string action)
		{
			if (this.openEncoder != null)
			{
				throw new InvalidOperationException(
					$"Cannot {action} while render pass '{this.openEncoder.Label}' is open. "
					+ "End the pass first and re-open it with LoadOp.Load.");
			}
		}

		private string NextLabel(string kind)
		{
			this.handleCounts.TryGetValue(kind, out int count);
			count++;
			this.handleCounts[kind] = count;
			return kind + count;
		}
	}
}
