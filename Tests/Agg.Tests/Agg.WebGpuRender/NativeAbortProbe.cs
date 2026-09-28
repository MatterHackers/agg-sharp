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
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using MatterHackers.Agg.Tests.TestingInfrastructure;
using MatterHackers.RenderCore;
using MatterHackers.RenderCore.Testing;
using MatterHackers.WebGpuRender;

namespace MatterHackers.Agg.Tests
{
	/// <summary>
	/// Runs a piece of real device code in a child copy of the test process, for the bugs whose symptom is
	/// wgpu-native aborting the process. Handing wgpu-native a null handle is a Rust panic across the C
	/// boundary, which kills the whole host - run in-process, the reproducing test would take every other
	/// test in the run down with it instead of failing on its own. In a child the abort is just a non-zero
	/// exit code the parent asserts on.
	/// <para>
	/// The child is this same executable: <c>Program.Main</c> hands its arguments to <see cref="TryRun"/>
	/// before the test platform starts, so a probe costs one process start and one device, not a second
	/// test discovery.
	/// </para>
	/// </summary>
	internal static class NativeAbortProbe
	{
		/// <summary>The first argument that marks a process as a probe child rather than a test run.</summary>
		private const string ProbeArgument = "--native-abort-probe";

		private static readonly Dictionary<string, Action> Probes = new Dictionary<string, Action>
		{
			[nameof(DisposeRenderPipelineWithNoBindings)] = DisposeRenderPipelineWithNoBindings,
			[nameof(ComputeCallsOnDisposedResourcesThrow)] = ComputeCallsOnDisposedResourcesThrow,
			[nameof(OversizedStorageBindingIsRefused)] = OversizedStorageBindingIsRefused,
			[nameof(SubmittingAnInvalidRecordingThrows)] = SubmittingAnInvalidRecordingThrows,
		};

		/// <summary>
		/// Runs the named probe if <paramref name="args"/> asks for one. Exit code 0 means the probe
		/// completed; anything else (an exception is 2, an abort is whatever the OS reports) means it did not.
		/// </summary>
		/// <param name="args">The process arguments.</param>
		/// <param name="exitCode">The code the process should exit with.</param>
		/// <returns>True if this process was a probe child and has run it.</returns>
		public static bool TryRun(string[] args, out int exitCode)
		{
			exitCode = 0;
			if (args.Length != 2 || args[0] != ProbeArgument)
			{
				return false;
			}

			try
			{
				Probes[args[1]]();
			}
			catch (Exception exception)
			{
				Console.Error.WriteLine(exception);
				exitCode = 2;
			}

			return true;
		}

		/// <summary>Runs a probe in a child process and returns its exit code and output.</summary>
		/// <param name="probeName">A key of <see cref="Probes"/>.</param>
		public static async Task<(int ExitCode, string Output)> RunInChildAsync(string probeName)
		{
			var startInfo = new ProcessStartInfo
			{
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
			};

			// Launched as `dotnet Agg.Tests.dll` the host is dotnet and needs the assembly named; launched
			// through the apphost the host is the test executable itself.
			string host = Environment.ProcessPath;
			startInfo.FileName = host;
			if (Path.GetFileNameWithoutExtension(host).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
			{
				startInfo.ArgumentList.Add(typeof(NativeAbortProbe).Assembly.Location);
			}

			startInfo.ArgumentList.Add(ProbeArgument);
			startInfo.ArgumentList.Add(probeName);

			using (var process = Process.Start(startInfo))
			{
				Task<string> output = process.StandardOutput.ReadToEndAsync();
				Task<string> error = process.StandardError.ReadToEndAsync();
				await process.WaitForExitAsync();
				return (process.ExitCode, await output + await error);
			}
		}

		private static void DisposeRenderPipelineWithNoBindings()
		{
			using (GpuTestGate.Acquire(nameof(DisposeRenderPipelineWithNoBindings)))
			using (var device = new WebGpuRenderDevice(false, TestRenderBackend.Native, nameof(NativeAbortProbe)))
			{
				device.RegisterShaderSources(new ProbeShaders());
				using var module = device.CreateShaderModule(ProbeShaders.Key);

				// No bind group layout at all: the pipeline gets the null pipeline layout that asks wgpu to
				// derive one, and disposing it must not hand that null back to wgpuPipelineLayoutRelease.
				var pipeline = device.CreateRenderPipeline(new RenderPipelineDescriptor(
					module,
					"vs_main",
					module,
					"fs_main",
					Array.Empty<VertexBufferLayout>(),
					new[] { new ColorTargetState(TextureFormat.Rgba8Unorm) },
					label: "noBindings"));
				pipeline.Dispose();

				if (device.LastUncapturedError != null)
				{
					throw new InvalidOperationException(device.LastUncapturedError);
				}
			}
		}

		private static void ComputeCallsOnDisposedResourcesThrow()
		{
			using (GpuTestGate.Acquire(nameof(ComputeCallsOnDisposedResourcesThrow)))
			using (var device = new WebGpuRenderDevice(false, TestRenderBackend.Native, nameof(NativeAbortProbe)))
			{
				device.RegisterShaderSources(new ProbeShaders());
				using var module = device.CreateShaderModule(ProbeShaders.ComputeKey);
				var pipeline = device.CreateComputePipeline(new ComputePipelineDescriptor(
					module,
					"main",
					new[] { new BindGroupLayoutEntry(0, 0, ShaderStage.Compute, BindingType.StorageBuffer) },
					"disposed"));
				var buffer = device.CreateBuffer(BufferUsage.Storage | BufferUsage.CopySrc, 16);
				var bindGroup = device.CreateBindGroup(new BindGroupDescriptor(pipeline, 0, new[] { BindGroupEntry.ForBuffer(0, buffer) }));
				bindGroup.Dispose();
				pipeline.Dispose();
				buffer.Dispose();

				using (var pass = device.BeginComputePass())
				{
					ExpectObjectDisposed("SetPipeline", () => pass.SetPipeline(pipeline));
					ExpectObjectDisposed("SetBindGroup", () => pass.SetBindGroup(0, bindGroup));
				}

				ExpectObjectDisposed("ReadBufferAsync", () => device.ReadBufferAsync(buffer, 0, new byte[16]));
			}
		}

		private static void OversizedStorageBindingIsRefused()
		{
			// A device at the default limits (no raiseComputeLimits), so a whole-buffer binding of 160 MiB is
			// over the 128 MiB maxStorageBufferBindingSize. Handed to wgpu-native it aborts the process.
			const ulong BufferBytes = 160UL * 1024 * 1024;
			using (GpuTestGate.Acquire(nameof(OversizedStorageBindingIsRefused)))
			using (var device = new WebGpuRenderDevice(false, TestRenderBackend.Native, nameof(NativeAbortProbe)))
			{
				if (device.Limits.MaxStorageBufferBindingSize >= BufferBytes)
				{
					throw new InvalidOperationException(
						$"The default device already grants a {device.Limits.MaxStorageBufferBindingSize:N0} byte storage binding; the probe needs one smaller than {BufferBytes:N0}.");
				}

				device.RegisterShaderSources(new ProbeShaders());
				using var module = device.CreateShaderModule(ProbeShaders.ComputeKey);
				using var pipeline = device.CreateComputePipeline(new ComputePipelineDescriptor(
					module,
					"main",
					new[] { new BindGroupLayoutEntry(0, 0, ShaderStage.Compute, BindingType.StorageBuffer) },
					"oversized"));
				using var buffer = device.CreateBuffer(BufferUsage.Storage | BufferUsage.CopySrc, BufferBytes);

				try
				{
					using var bindGroup = device.CreateBindGroup(new BindGroupDescriptor(pipeline, 0, new[] { BindGroupEntry.ForBuffer(0, buffer) }));
				}
				catch (ArgumentException)
				{
					if (device.LastUncapturedError != null)
					{
						throw new InvalidOperationException(device.LastUncapturedError);
					}

					return;
				}

				throw new InvalidOperationException("A storage binding over maxStorageBufferBindingSize did not throw ArgumentException.");
			}
		}

		private static void SubmittingAnInvalidRecordingThrows()
		{
			using (GpuTestGate.Acquire(nameof(SubmittingAnInvalidRecordingThrows)))
			using (var device = new WebGpuRenderDevice(false, TestRenderBackend.Native, nameof(NativeAbortProbe)))
			{
				// 20 mip levels on a 4x4 texture is invalid, so wgpu hands back an error texture - as it hands
				// back an error buffer when an allocation fails. Copying from it records fine; submitting the
				// recording is what wgpu-native treats as fatal.
				using var invalid = device.CreateTexture(new TextureDescriptor(4, 4, TextureFormat.Rgba8Unorm, TextureUsage.CopySrc | TextureUsage.TextureBinding, 20, 1, "invalid"));
				using var destination = device.CreateTexture(new TextureDescriptor(4, 4, TextureFormat.Rgba8Unorm, TextureUsage.CopyDst | TextureUsage.TextureBinding, 1, 1, "destination"));
				device.CopyTextureToTexture(invalid, destination, 0, 0, 4, 4);

				try
				{
					device.Submit();
				}
				catch (InvalidOperationException)
				{
					return;
				}

				throw new InvalidOperationException("Submitting a recording that uses an invalid texture did not throw.");
			}
		}

		private static void ExpectObjectDisposed(string call, Action action)
		{
			try
			{
				action();
			}
			catch (ObjectDisposedException)
			{
				return;
			}

			throw new InvalidOperationException($"{call} on a disposed resource did not throw ObjectDisposedException.");
		}

		private sealed class ProbeShaders : IShaderSourceProvider
		{
			public const string Key = "test.probe.noBindings";

			private const string Wgsl = @"
@vertex
fn vs_main(@builtin(vertex_index) index : u32) -> @builtin(position) vec4<f32>
{
	return vec4<f32>(f32(index), 0.0, 0.0, 1.0);
}

@fragment
fn fs_main() -> @location(0) vec4<f32>
{
	return vec4<f32>(1.0, 0.0, 0.0, 1.0);
}
";

			public const string ComputeKey = "test.probe.oneStorageBuffer";

			private const string ComputeWgsl = @"
@group(0) @binding(0) var<storage, read_write> data : array<u32>;

@compute @workgroup_size(1)
fn main(@builtin(global_invocation_id) id : vec3<u32>)
{
	data[id.x] = id.x;
}
";

			public string TryGetSource(string sourceKey)
				=> sourceKey switch
				{
					Key => Wgsl,
					ComputeKey => ComputeWgsl,
					_ => null,
				};
		}
	}
}
