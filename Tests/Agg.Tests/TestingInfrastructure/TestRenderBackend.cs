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
using System.Runtime.InteropServices;
using MatterHackers.WebGpu;
using MatterHackers.WebGpuRender;

namespace MatterHackers.Agg.Tests.TestingInfrastructure
{
	/// <summary>
	/// Which wgpu backend the render tests must run on.
	/// </summary>
	public static class TestRenderBackend
	{
		/// <summary>
		/// Gets the one backend this OS ships a window host for.
		/// <para>
		/// The tests name a backend explicitly rather than letting wgpu choose, because a machine that has
		/// silently fallen back to a software or secondary adapter should fail loudly rather than pass with
		/// pixels nobody ships. That makes the choice per-OS: D3D12 is the shipping backend on Windows and
		/// Metal is the shipping backend on macOS, so pinning D3D12 everywhere would simply refuse to start
		/// off Windows.
		/// </para>
		/// </summary>
		public static WGPUBackendType Native
		{
			get
			{
				if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				{
					return WGPUBackendType.D3D12;
				}

				if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
				{
					return WGPUBackendType.Metal;
				}

				// Everywhere else wgpu-native's only real backend is Vulkan.
				return WGPUBackendType.Vulkan;
			}
		}

		/// <summary>Set to 1 to make this run ask wgpu for the software (fallback) adapter.</summary>
		public const string ForceFallbackEnvironmentVariable = "AGG_FORCE_WARP";

		private static readonly object adapterKindLock = new object();

		private static bool? publishedAdapterKind;

		/// <summary>
		/// Gets whether this run should demand wgpu's software adapter rather than the machine's GPU.
		/// <para>
		/// Opt-in, via <c>AGG_FORCE_WARP=1</c>, and meant for debugging the software render path locally -
		/// <b>not</b> for reproducing CI. WARP is a Windows component, so its version tracks the OS: a
		/// Windows 10 workstation and a GitHub runner do not rasterize the same. We measured one scene
		/// differing on 47% of its pixels between the two, and another losing the device outright on
		/// Windows 10. So the authoritative renders for the software golden set are CI's own, downloaded
		/// from the <c>golden-image-failures</c> artifact - never a local <c>AGG_FORCE_WARP=1</c> run.
		/// </para>
		/// </summary>
		public static bool ForceFallbackAdapter
			=> Environment.GetEnvironmentVariable(ForceFallbackEnvironmentVariable) == "1";

		/// <summary>
		/// Tells this class what kind of adapter a device that was actually created landed on, so
		/// <see cref="NativeGoldenFolderName"/> can answer without creating a device of its own.
		/// <para>
		/// The capture device is the preferred source: it is the device whose pixels are being compared, it
		/// already exists whenever a golden test is running, and asking it costs nothing. The alternative -
		/// spinning up a throwaway device to ask the same question - doubles adapter creation and lets a
		/// transient wgpu failure decide the golden folder for the whole run.
		/// </para>
		/// <para>
		/// First publish wins. Every capture in a run asks for the same backend on the same machine, so
		/// they agree; taking the first simply avoids a later, differently-configured device redirecting
		/// comparisons mid-suite.
		/// </para>
		/// </summary>
		/// <param name="isFallbackAdapter">The device's <c>IsFallbackAdapter</c>.</param>
		internal static void PublishAdapterKind(bool isFallbackAdapter)
		{
			lock (adapterKindLock)
			{
				publishedAdapterKind ??= isFallbackAdapter;
			}
		}

		/// <summary>
		/// Whether the adapter this run actually renders on is a software rasterizer.
		/// <para>
		/// Answered from whatever a real capture published, and only probed with a throwaway device when
		/// nothing has captured yet (a golden path that reads the folder name before rendering). A probe
		/// that throws is not cached, so a transient adapter failure costs one retry rather than the run.
		/// When the fallback was demanded outright the answer is known without asking anyone.
		/// </para>
		/// </summary>
		private static bool RunningOnFallbackAdapter
		{
			get
			{
				if (ForceFallbackAdapter)
				{
					return true;
				}

				lock (adapterKindLock)
				{
					if (publishedAdapterKind == null)
					{
						using (var probe = new WebGpuRenderDevice(false, Native, "TestRenderBackend adapter probe"))
						{
							publishedAdapterKind = probe.IsFallbackAdapter;
						}
					}

					return publishedAdapterKind.Value;
				}
			}
		}

		/// <summary>
		/// Gets <see cref="Native"/> as the folder name its golden image set lives under - <c>d3d12</c>,
		/// <c>metal</c> or <c>vulkan</c>, with a <c>-warp</c> suffix when this run is on a software fallback
		/// adapter (<c>d3d12-warp</c>).
		/// </summary>
		/// <remarks>
		/// Spelled from the enum member rather than a lookup table on purpose. A table would be a second
		/// answer to "which backend is this run", free to drift from <see cref="Native"/>, and the way that
		/// drift shows up is a suite comparing Metal pixels against the D3D12 goldens.
		/// <para>
		/// The suffix exists because a software rasterizer is, for golden purposes, a different backend: any
		/// adapter wgpu reports as type CPU breaks rasterization ties differently from hardware, which at
		/// tolerance 0 is a failure on nearly every image. So software adapters get their own golden set
		/// instead of forcing the tolerance up for everyone. <c>warp</c> reads as "software fallback
		/// adapter" here, not as a D3D12-only name - a fallback adapter on macOS would land in
		/// <c>metal-warp</c> - but WARP is the familiar name and the only fallback baselined today, because
		/// GitHub's Windows runners have no GPU and that is what CI renders with.
		/// </para>
		/// </remarks>
		public static string NativeGoldenFolderName
		{
			get
			{
				var name = Native.ToString().ToLowerInvariant();
				return RunningOnFallbackAdapter ? name + "-warp" : name;
			}
		}
	}
}
