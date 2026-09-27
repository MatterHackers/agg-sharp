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
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using MatterHackers.Agg.Tests.TestingInfrastructure;
using MatterHackers.RenderCore;
using MatterHackers.RenderCore.Testing;
using MatterHackers.WebGpuRender;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests
{
	/// <summary>
	/// GPU compute through <see cref="IRenderDevice"/> on a real device: compute pipelines, storage and
	/// uniform buffers, compute passes with several ordered dispatches, and
	/// <see cref="IRenderDevice.ReadBufferAsync"/>.
	/// <para>
	/// The WGSL is registered from this assembly through its own <see cref="IShaderSourceProvider"/>, which
	/// is the route an external consumer (colmap-sharp's PatchMatch stereo) ships compute shaders by. Every
	/// value compared is exactly representable, so every assertion is exact.
	/// </para>
	/// </summary>
	[NotInParallel]
	public class WebGpuComputeTests
	{
		private const string AddKey = "test.compute.add";
		private const string ReverseKey = "test.compute.reverse";
		private const string ScaleKey = "test.compute.scale";
		private const string TailKey = "test.compute.tail";

		// Elements the tail shader touches at the very end of its binding.
		private const uint TailCount = 1024;
		private const uint WorkgroupSize = 64;

		private const string AddWgsl = @"
@group(0) @binding(0) var<storage, read> a : array<f32>;
@group(0) @binding(1) var<storage, read> b : array<f32>;
@group(0) @binding(2) var<storage, read_write> c : array<f32>;

@compute @workgroup_size(64)
fn main(@builtin(global_invocation_id) id : vec3<u32>)
{
	let i = id.x;
	if (i < arrayLength(&c))
	{
		c[i] = a[i] + b[i];
	}
}
";

		// dst[i] = src[n - 1 - i] + 1: nearly every element reads one that a different workgroup of the
		// previous dispatch wrote (for n = 300 only 128..171 read within their own workgroup), so
		// dispatches that were not ordered against each other give wrong values.
		private const string ReverseWgsl = @"
@group(0) @binding(0) var<storage, read> src : array<u32>;
@group(0) @binding(1) var<storage, read_write> dst : array<u32>;

@compute @workgroup_size(64)
fn main(@builtin(global_invocation_id) id : vec3<u32>)
{
	let n = arrayLength(&dst);
	let i = id.x;
	if (i < n)
	{
		dst[i] = src[n - 1u - i] + 1u;
	}
}
";

		private const string ScaleWgsl = @"
struct Params
{
	scale : f32,
	offset : f32,
	count : u32,
	pad : u32,
};

@group(0) @binding(0) var<uniform> params : Params;
@group(0) @binding(1) var<storage, read> input : array<f32>;
@group(0) @binding(2) var<storage, read_write> output : array<f32>;

@compute @workgroup_size(64)
fn main(@builtin(global_invocation_id) id : vec3<u32>)
{
	let i = id.x;
	if (i < params.count)
	{
		output[i] = input[i] * params.scale + params.offset;
	}
}
";

		// data[n - TailCount + i] = data[n - TailCount + i] * 3 + 1 for the last TailCount elements of the
		// binding: those elements are only reachable if the binding really spans the whole buffer.
		private const string TailWgsl = @"
@group(0) @binding(0) var<storage, read_write> data : array<u32>;

@compute @workgroup_size(64)
fn main(@builtin(global_invocation_id) id : vec3<u32>)
{
	let n = arrayLength(&data);
	if (id.x < 1024u)
	{
		let i = n - 1024u + id.x;
		data[i] = data[i] * 3u + 1u;
	}
}
";

		[Test]
		public async Task WithoutTheComputeOptInTheDeviceKeepsTheWebGpuDefaults()
		{
			// Render-only hosts must be unchanged: a raised maxBufferSize would move the mesh chunk size.
			using (GpuTestGate.Acquire(nameof(WebGpuComputeTests)))
			using (var device = CreateDevice())
			{
				DeviceLimits limits = device.Limits;
				await Assert.That(limits.MaxBufferSize).IsEqualTo(DeviceLimits.DefaultMaxBufferSize);
				await Assert.That(limits.MaxStorageBufferBindingSize).IsEqualTo(DeviceLimits.DefaultMaxStorageBufferBindingSize);
				await Assert.That(limits.MaxStorageBuffersPerShaderStage).IsEqualTo(DeviceLimits.DefaultMaxStorageBuffersPerShaderStage);
				await Assert.That(limits.MaxUniformBufferBindingSize).IsEqualTo(DeviceLimits.DefaultMaxUniformBufferBindingSize);
				await Assert.That(limits.MinStorageBufferOffsetAlignment).IsEqualTo(DeviceLimits.DefaultMinStorageBufferOffsetAlignment);
				await Assert.That(limits.MinUniformBufferOffsetAlignment).IsEqualTo(DeviceLimits.DefaultMinUniformBufferOffsetAlignment);
				await Assert.That(limits.MaxComputeInvocationsPerWorkgroup).IsEqualTo(DeviceLimits.DefaultMaxComputeInvocationsPerWorkgroup);
				await Assert.That(limits.MaxComputeWorkgroupsPerDimension).IsEqualTo(DeviceLimits.DefaultMaxComputeWorkgroupsPerDimension);
			}
		}

		[Test]
		public async Task TheComputeOptInGrantsTheAdaptersMaxima()
		{
			using (GpuTestGate.Acquire(nameof(WebGpuComputeTests)))
			using (var device = CreateDevice(raiseComputeLimits: true))
			{
				DeviceLimits granted = device.Limits;
				DeviceLimits adapter = device.AdapterLimits;
				Console.WriteLine($"Adapter '{device.AdapterName}' ({device.AdapterBackend}): {adapter}");
				Console.WriteLine($"Granted: {granted}");

				await Assert.That(granted.MaxBufferSize).IsEqualTo(adapter.MaxBufferSize);
				await Assert.That(granted.MaxStorageBufferBindingSize).IsEqualTo(adapter.MaxStorageBufferBindingSize);
				await Assert.That(granted.MaxStorageBuffersPerShaderStage).IsEqualTo(adapter.MaxStorageBuffersPerShaderStage);
				await Assert.That(granted.MaxComputeInvocationsPerWorkgroup).IsEqualTo(adapter.MaxComputeInvocationsPerWorkgroup);

				await Assert.That(granted.MaxBufferSize).IsGreaterThanOrEqualTo(DeviceLimits.DefaultMaxBufferSize);
				await Assert.That(granted.MaxStorageBufferBindingSize).IsGreaterThanOrEqualTo(DeviceLimits.DefaultMaxStorageBufferBindingSize);
				await Assert.That(granted.MaxStorageBuffersPerShaderStage).IsGreaterThanOrEqualTo(DeviceLimits.DefaultMaxStorageBuffersPerShaderStage);
				await Assert.That(granted.MaxComputeInvocationsPerWorkgroup).IsGreaterThanOrEqualTo(DeviceLimits.DefaultMaxComputeInvocationsPerWorkgroup);

				// The texture limit the device always raises is still raised alongside.
				await Assert.That(granted.MaxTextureDimension2D).IsEqualTo(adapter.MaxTextureDimension2D);
			}
		}

		[Test]
		public async Task AStorageBindingLargerThanTheDefaultReachesItsTail()
		{
			// 160 MiB: past the 128 MiB default binding limit, under the 256 MiB default buffer limit, so the
			// binding size is the only limit this exercises.
			const ulong BufferBytes = 160UL * 1024 * 1024;
			const ulong TailOffset = BufferBytes - (TailCount * sizeof(uint));

			using (GpuTestGate.Acquire(nameof(WebGpuComputeTests)))
			using (var device = CreateDevice(raiseComputeLimits: true))
			{
				if (device.Limits.MaxStorageBufferBindingSize < BufferBytes)
				{
					Skip.Test(
						$"Adapter '{device.AdapterName}' grants a storage binding of only "
						+ $"{device.Limits.MaxStorageBufferBindingSize:N0} bytes; this needs {BufferBytes:N0}.");
				}

				using var module = device.CreateShaderModule(TailKey);
				using var pipeline = device.CreateComputePipeline(new ComputePipelineDescriptor(
					module,
					"main",
					new[] { new BindGroupLayoutEntry(0, 0, ShaderStage.Compute, BindingType.StorageBuffer) },
					"tail"));

				var pattern = new uint[TailCount];
				for (uint i = 0; i < TailCount; i++)
				{
					pattern[i] = (i * 2654435761u) >> 8;
				}

				using var buffer = device.CreateBuffer(BufferUsage.Storage | BufferUsage.CopyDst | BufferUsage.CopySrc, BufferBytes);
				device.WriteBuffer(buffer, TailOffset, MemoryMarshal.AsBytes(pattern.AsSpan()));

				// The whole buffer in one binding (size 0 = to the end).
				using var bindGroup = device.CreateBindGroup(new BindGroupDescriptor(
					pipeline,
					0,
					new[] { BindGroupEntry.ForBuffer(0, buffer) }));

				using (var pass = device.BeginComputePass("tailPass"))
				{
					pass.SetPipeline(pipeline);
					pass.SetBindGroup(0, bindGroup);
					pass.Dispatch(WorkgroupsFor((int)TailCount));
				}

				var bytes = new byte[TailCount * sizeof(uint)];
				await device.ReadBufferAsync(buffer, TailOffset, bytes);
				uint[] result = MemoryMarshal.Cast<byte, uint>(bytes).ToArray();

				await Assert.That(device.LastUncapturedError).IsNull();
				for (int i = 0; i < TailCount; i++)
				{
					uint expected = unchecked((pattern[i] * 3u) + 1u);
					if (result[i] != expected)
					{
						await Assert.That(result[i]).IsEqualTo(expected);
					}
				}
			}
		}

		[Test]
		public async Task AStorageBindingOverTheGrantedLimitIsRefusedNotAborted()
		{
			// In a child process: before the check, wgpu-native aborted the whole process on this binding.
			var (exitCode, output) = await NativeAbortProbe.RunInChildAsync("OversizedStorageBindingIsRefused");
			await Assert.That(exitCode).IsEqualTo(0).Because(output);
		}

		[Test]
		public async Task AComputeShaderAddsTwoStorageArraysIntoAThird()
		{
			// Not a multiple of the workgroup size, so the shader's bounds check is exercised too.
			const int Count = 1000;
			var a = new float[Count];
			var b = new float[Count];
			for (int i = 0; i < Count; i++)
			{
				a[i] = i * 0.5f;
				b[i] = i * 0.25f;
			}

			using (GpuTestGate.Acquire(nameof(WebGpuComputeTests)))
			using (var device = CreateDevice())
			{
				using var module = device.CreateShaderModule(AddKey);
				using var pipeline = device.CreateComputePipeline(new ComputePipelineDescriptor(
					module,
					"main",
					new[]
					{
						new BindGroupLayoutEntry(0, 0, ShaderStage.Compute, BindingType.ReadOnlyStorageBuffer),
						new BindGroupLayoutEntry(0, 1, ShaderStage.Compute, BindingType.ReadOnlyStorageBuffer),
						new BindGroupLayoutEntry(0, 2, ShaderStage.Compute, BindingType.StorageBuffer),
					},
					"add"));

				// One input filled at creation, one through WriteBuffer: both upload routes feed a shader.
				using var bufferA = device.CreateBuffer(BufferUsage.Storage, Count * sizeof(float), MemoryMarshal.AsBytes(a.AsSpan()));
				using var bufferB = device.CreateBuffer(BufferUsage.Storage | BufferUsage.CopyDst, Count * sizeof(float));
				device.WriteBuffer(bufferB, 0, MemoryMarshal.AsBytes(b.AsSpan()));
				using var bufferC = device.CreateBuffer(BufferUsage.Storage | BufferUsage.CopySrc, Count * sizeof(float));

				using var bindGroup = device.CreateBindGroup(new BindGroupDescriptor(
					pipeline,
					0,
					new[]
					{
						BindGroupEntry.ForBuffer(0, bufferA),
						BindGroupEntry.ForBuffer(1, bufferB),
						BindGroupEntry.ForBuffer(2, bufferC),
					}));

				using (var pass = device.BeginComputePass("addPass"))
				{
					pass.SetPipeline(pipeline);
					pass.SetBindGroup(0, bindGroup);
					pass.Dispatch(WorkgroupsFor(Count));
				}

				float[] sums = await ReadFloatsAsync(device, bufferC, 0, Count);

				await Assert.That(device.LastUncapturedError).IsNull();
				for (int i = 0; i < Count; i++)
				{
					if (sums[i] != a[i] + b[i])
					{
						await Assert.That(sums[i]).IsEqualTo(a[i] + b[i]);
					}
				}
			}
		}

		[Test]
		public async Task DispatchesInOnePassSeeTheWritesOfTheOnesBeforeThem()
		{
			// Ping-pong between two buffers for many dispatches in one pass - the loop shape an iterative
			// solver uses. Each step reverses and adds one, so after an even number of steps element i is
			// its own start value plus the step count, and any step that raced its predecessor breaks that.
			const int Count = 300;
			const int Steps = 10;
			var start = new uint[Count];
			for (int i = 0; i < Count; i++)
			{
				start[i] = (uint)(i * 7);
			}

			using (GpuTestGate.Acquire(nameof(WebGpuComputeTests)))
			using (var device = CreateDevice())
			{
				using var module = device.CreateShaderModule(ReverseKey);
				using var pipeline = device.CreateComputePipeline(new ComputePipelineDescriptor(
					module,
					"main",
					new[]
					{
						new BindGroupLayoutEntry(0, 0, ShaderStage.Compute, BindingType.ReadOnlyStorageBuffer),
						new BindGroupLayoutEntry(0, 1, ShaderStage.Compute, BindingType.StorageBuffer),
					},
					"reverse"));

				ulong size = Count * sizeof(uint);
				using var ping = device.CreateBuffer(BufferUsage.Storage | BufferUsage.CopySrc, size, MemoryMarshal.AsBytes(start.AsSpan()));
				using var pong = device.CreateBuffer(BufferUsage.Storage | BufferUsage.CopySrc, size);

				using var pingToPong = device.CreateBindGroup(new BindGroupDescriptor(
					pipeline,
					0,
					new[] { BindGroupEntry.ForBuffer(0, ping), BindGroupEntry.ForBuffer(1, pong) }));
				using var pongToPing = device.CreateBindGroup(new BindGroupDescriptor(
					pipeline,
					0,
					new[] { BindGroupEntry.ForBuffer(0, pong), BindGroupEntry.ForBuffer(1, ping) }));

				using (var pass = device.BeginComputePass("reversePass"))
				{
					pass.SetPipeline(pipeline);
					for (int step = 0; step < Steps; step++)
					{
						pass.SetBindGroup(0, step % 2 == 0 ? pingToPong : pongToPing);
						pass.Dispatch(WorkgroupsFor(Count));
					}
				}

				var bytes = new byte[size];
				await device.ReadBufferAsync(ping, 0, bytes);
				uint[] result = MemoryMarshal.Cast<byte, uint>(bytes).ToArray();

				await Assert.That(device.LastUncapturedError).IsNull();
				for (int i = 0; i < Count; i++)
				{
					if (result[i] != start[i] + Steps)
					{
						await Assert.That(result[i]).IsEqualTo(start[i] + Steps);
					}
				}
			}
		}

		[Test]
		public async Task AUniformBufferParameterizesAComputeShader()
		{
			const int Count = 200;
			const uint ActiveCount = 150;
			var input = new float[Count];
			for (int i = 0; i < Count; i++)
			{
				input[i] = i;
			}

			using (GpuTestGate.Acquire(nameof(WebGpuComputeTests)))
			using (var device = CreateDevice())
			{
				using var module = device.CreateShaderModule(ScaleKey);
				using var pipeline = device.CreateComputePipeline(new ComputePipelineDescriptor(
					module,
					"main",
					new[]
					{
						new BindGroupLayoutEntry(0, 0, ShaderStage.Compute, BindingType.UniformBuffer),
						new BindGroupLayoutEntry(0, 1, ShaderStage.Compute, BindingType.ReadOnlyStorageBuffer),
						new BindGroupLayoutEntry(0, 2, ShaderStage.Compute, BindingType.StorageBuffer),
					},
					"scale"));

				// Params { scale, offset, count, pad }: 16 bytes, the std140-safe size of the struct.
				var parameters = new byte[16];
				BitConverter.TryWriteBytes(parameters.AsSpan(0), 2.0f);
				BitConverter.TryWriteBytes(parameters.AsSpan(4), 0.5f);
				BitConverter.TryWriteBytes(parameters.AsSpan(8), ActiveCount);

				using var uniforms = device.CreateBuffer(BufferUsage.Uniform | BufferUsage.CopyDst, (ulong)parameters.Length);
				device.WriteBuffer(uniforms, 0, parameters);
				using var inputBuffer = device.CreateBuffer(BufferUsage.Storage, Count * sizeof(float), MemoryMarshal.AsBytes(input.AsSpan()));
				using var outputBuffer = device.CreateBuffer(BufferUsage.Storage | BufferUsage.CopySrc, Count * sizeof(float));

				using var bindGroup = device.CreateBindGroup(new BindGroupDescriptor(
					pipeline,
					0,
					new[]
					{
						BindGroupEntry.ForBuffer(0, uniforms),
						BindGroupEntry.ForBuffer(1, inputBuffer),
						BindGroupEntry.ForBuffer(2, outputBuffer),
					}));

				using (var pass = device.BeginComputePass("scalePass"))
				{
					pass.SetPipeline(pipeline);
					pass.SetBindGroup(0, bindGroup);
					pass.Dispatch(WorkgroupsFor(Count));
				}

				float[] output = await ReadFloatsAsync(device, outputBuffer, 0, Count);

				await Assert.That(device.LastUncapturedError).IsNull();
				for (int i = 0; i < Count; i++)
				{
					// Elements past the uniform's count are never written, and a new buffer is zeroed.
					float expected = i < ActiveCount ? (i * 2.0f) + 0.5f : 0.0f;
					if (output[i] != expected)
					{
						await Assert.That(output[i]).IsEqualTo(expected);
					}
				}
			}
		}

		[Test]
		public async Task ReadBufferAsyncReadsTheRangeAtItsOffset()
		{
			var values = new uint[64];
			for (int i = 0; i < values.Length; i++)
			{
				values[i] = (uint)(1000 + i);
			}

			using (GpuTestGate.Acquire(nameof(WebGpuComputeTests)))
			using (var device = CreateDevice())
			{
				using var buffer = device.CreateBuffer(
					BufferUsage.Storage | BufferUsage.CopySrc,
					(ulong)(values.Length * sizeof(uint)),
					MemoryMarshal.AsBytes(values.AsSpan()));

				// 36 bytes in is element 9: aligned to the copy rule of 4 but not to 8 or 16, so an
				// implementation that silently rounded the offset down would return the wrong elements.
				var bytes = new byte[5 * sizeof(uint)];
				await device.ReadBufferAsync(buffer, 36, bytes);
				uint[] read = MemoryMarshal.Cast<byte, uint>(bytes).ToArray();

				await Assert.That(device.LastUncapturedError).IsNull();
				await Assert.That(string.Join(",", read)).IsEqualTo("1009,1010,1011,1012,1013");

				// The copy rules are refused up front rather than left to wgpu's out-of-band validation.
				await Assert.That(() => device.ReadBufferAsync(buffer, 2, new byte[4]).AsTask()).Throws<ArgumentException>();
				await Assert.That(() => device.ReadBufferAsync(buffer, 252, new byte[8]).AsTask()).Throws<ArgumentException>();
			}
		}

		[Test]
		public async Task AComputePassIsUnderTheSamePassRulesAsARenderPass()
		{
			// Recording double, no GPU: the rules are the seam's contract, so both devices enforce them.
			var device = new RecordingRenderDevice();
			var buffer = device.CreateBuffer(BufferUsage.Storage | BufferUsage.CopySrc, 16);

			using (device.BeginComputePass("open"))
			{
				await Assert.That(() => device.Submit()).Throws<InvalidOperationException>();
				await Assert.That(() => device.BeginComputePass()).Throws<InvalidOperationException>();
				await Assert.That(() => device.BeginRenderPass(default)).Throws<InvalidOperationException>();
				await Assert.That(() => device.ReadBufferAsync(buffer, 0, new byte[16]).AsTask()).Throws<InvalidOperationException>();
			}

			device.Submit();
			await Assert.That(device.OpenComputePass).IsNull();
		}

		[Test]
		public async Task TheNativeDeviceEnforcesThePassRulesWhileAComputePassIsOpen()
		{
			using (GpuTestGate.Acquire(nameof(WebGpuComputeTests)))
			using (var device = CreateDevice())
			{
				using var buffer = device.CreateBuffer(BufferUsage.Storage | BufferUsage.CopySrc, 16);

				using (device.BeginComputePass("open"))
				{
					await Assert.That(() => device.Submit()).Throws<InvalidOperationException>();
					await Assert.That(() => device.BeginComputePass()).Throws<InvalidOperationException>();
					await Assert.That(() => device.BeginRenderPass(default)).Throws<InvalidOperationException>();
					await Assert.That(() => device.ReadBufferAsync(buffer, 0, new byte[16]).AsTask()).Throws<InvalidOperationException>();
				}

				// The refused calls left nothing half-recorded: the pass ends, submits and reads back cleanly.
				device.Submit();
				await device.ReadBufferAsync(buffer, 0, new byte[16]);
				await Assert.That(device.LastUncapturedError).IsNull();
			}
		}

		[Test]
		public async Task DisposingADeviceEndsItsOpenComputePass()
		{
			// The recording double first, since it is what the rest of the seam's tests stand on.
			var recording = new RecordingRenderDevice();
			var recordedPass = (RecordingComputeEncoder)recording.BeginComputePass("left open");
			recording.Dispose();
			await Assert.That(recordedPass.IsEnded).IsTrue();
			await Assert.That(recording.OpenComputePass).IsNull();

			using (GpuTestGate.Acquire(nameof(WebGpuComputeTests)))
			{
				var device = CreateDevice();
				var pass = (WebGpuComputeEncoder)device.BeginComputePass("left open");
				device.Dispose();
				await Assert.That(pass.IsEnded).IsTrue();

				// Disposing the pass after its device is a no-op, not a call on a released encoder.
				pass.Dispose();
			}
		}

		[Test]
		public async Task ADispatchOverTheWorkgroupLimitIsRefused()
		{
			// Left to wgpu, an oversize dispatch invalidates the command buffer and a later readback quietly
			// returns zeros; both devices refuse it at the call instead.
			var recording = new RecordingRenderDevice { Limits = new DeviceLimits(DeviceLimits.DefaultMaxBufferSize, maxComputeWorkgroupsPerDimension: 4) };
			using (var pass = recording.BeginComputePass())
			{
				pass.Dispatch(4, 4, 4);
				await Assert.That(() => pass.Dispatch(5)).Throws<ArgumentOutOfRangeException>();
				await Assert.That(() => pass.Dispatch(1, 5)).Throws<ArgumentOutOfRangeException>();
				await Assert.That(() => pass.Dispatch(1, 1, 5)).Throws<ArgumentOutOfRangeException>();
			}

			await Assert.That(recording.CommandsOf<DispatchCommand>().Count).IsEqualTo(1);

			using (GpuTestGate.Acquire(nameof(WebGpuComputeTests)))
			using (var device = CreateDevice())
			{
				uint tooMany = device.Limits.MaxComputeWorkgroupsPerDimension + 1;
				using (var pass = device.BeginComputePass())
				{
					await Assert.That(() => pass.Dispatch(tooMany)).Throws<ArgumentOutOfRangeException>();
					await Assert.That(() => pass.Dispatch(1, tooMany)).Throws<ArgumentOutOfRangeException>();
					await Assert.That(() => pass.Dispatch(1, 1, tooMany)).Throws<ArgumentOutOfRangeException>();
				}

				device.Submit();
				await Assert.That(device.LastUncapturedError).IsNull();
			}
		}

		[Test]
		public async Task ComputeCallsOnDisposedResourcesThrowObjectDisposed()
		{
			var device = new RecordingRenderDevice();
			var pipeline = device.CreateComputePipeline(new ComputePipelineDescriptor(null, "main", null, "disposed"));
			var buffer = device.CreateBuffer(BufferUsage.Storage | BufferUsage.CopySrc, 16);
			var bindGroup = device.CreateBindGroup(new BindGroupDescriptor(pipeline, 0, new[] { BindGroupEntry.ForBuffer(0, buffer) }));
			bindGroup.Dispose();
			pipeline.Dispose();
			buffer.Dispose();

			using (var pass = device.BeginComputePass())
			{
				await Assert.That(() => pass.SetPipeline(pipeline)).Throws<ObjectDisposedException>();
				await Assert.That(() => pass.SetBindGroup(0, bindGroup)).Throws<ObjectDisposedException>();
			}

			await Assert.That(() => device.ReadBufferAsync(buffer, 0, new byte[16]).AsTask()).Throws<ObjectDisposedException>();

			// The native device, in a child process: before this was checked it handed wgpu-native a null
			// handle, which aborts the process rather than throwing.
			var (exitCode, output) = await NativeAbortProbe.RunInChildAsync("ComputeCallsOnDisposedResourcesThrow");
			await Assert.That(exitCode).IsEqualTo(0).Because(output);
		}

		private static uint WorkgroupsFor(int count) => ((uint)count + WorkgroupSize - 1) / WorkgroupSize;

		private static async Task<float[]> ReadFloatsAsync(IRenderDevice device, IGpuBuffer buffer, ulong offset, int count)
		{
			var bytes = new byte[count * sizeof(float)];
			await device.ReadBufferAsync(buffer, offset, bytes);
			return MemoryMarshal.Cast<byte, float>(bytes).ToArray();
		}

		private static WebGpuRenderDevice CreateDevice(bool raiseComputeLimits = false)
		{
			var device = new WebGpuRenderDevice(
				false,
				TestRenderBackend.Native,
				nameof(WebGpuComputeTests),
				raiseComputeLimits: raiseComputeLimits);
			device.RegisterShaderSources(new ComputeTestShaders());
			return device;
		}

		/// <summary>
		/// WGSL shipped as strings by an assembly other than the backend's - the shape a compute consumer
		/// outside agg-sharp registers its shaders in.
		/// </summary>
		private sealed class ComputeTestShaders : IShaderSourceProvider
		{
			public string TryGetSource(string sourceKey)
				=> sourceKey switch
				{
					AddKey => AddWgsl,
					ReverseKey => ReverseWgsl,
					ScaleKey => ScaleWgsl,
					TailKey => TailWgsl,
					_ => null,
				};
		}
	}
}
