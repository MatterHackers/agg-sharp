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

namespace MatterHackers.RenderCore
{
	/// <summary>
	/// The argument rules of <see cref="IComputeEncoder.Dispatch"/>, in one place so the native device and
	/// the recording double refuse exactly the same calls.
	/// </summary>
	public static class ComputeDispatch
	{
		/// <summary>Throws if any workgroup count exceeds the device's per-dimension limit.</summary>
		/// <param name="limits">The limits of the device recording the dispatch.</param>
		/// <param name="workgroupCountX">Workgroups along X.</param>
		/// <param name="workgroupCountY">Workgroups along Y.</param>
		/// <param name="workgroupCountZ">Workgroups along Z.</param>
		/// <exception cref="ArgumentOutOfRangeException">A count is over
		/// <see cref="DeviceLimits.MaxComputeWorkgroupsPerDimension"/>.</exception>
		public static void Validate(in DeviceLimits limits, uint workgroupCountX, uint workgroupCountY, uint workgroupCountZ)
		{
			// Checked here because wgpu only reports an oversize dispatch out of band: it invalidates the
			// whole command buffer, and a readback after it quietly returns zeros.
			ValidateOne(limits, workgroupCountX, nameof(workgroupCountX));
			ValidateOne(limits, workgroupCountY, nameof(workgroupCountY));
			ValidateOne(limits, workgroupCountZ, nameof(workgroupCountZ));
		}

		private static void ValidateOne(in DeviceLimits limits, uint count, string parameterName)
		{
			if (count > limits.MaxComputeWorkgroupsPerDimension)
			{
				throw new ArgumentOutOfRangeException(
					parameterName,
					count,
					$"A dispatch may have at most {limits.MaxComputeWorkgroupsPerDimension:N0} workgroups along one dimension;"
					+ " split the work into several dispatches or spread it over another dimension.");
			}
		}
	}
}
