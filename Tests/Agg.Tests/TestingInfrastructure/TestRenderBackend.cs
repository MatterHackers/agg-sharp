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

using System.Runtime.InteropServices;
using MatterHackers.WebGpu;

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

		/// <summary>
		/// Gets <see cref="Native"/> as the folder name its golden image set lives under - <c>d3d12</c>,
		/// <c>metal</c> or <c>vulkan</c>.
		/// </summary>
		/// <remarks>
		/// Spelled from the enum member rather than a lookup table on purpose. A table would be a second
		/// answer to "which backend is this run", free to drift from <see cref="Native"/>, and the way that
		/// drift shows up is a suite comparing Metal pixels against the D3D12 goldens.
		/// </remarks>
		public static string NativeGoldenFolderName => Native.ToString().ToLowerInvariant();
	}
}
