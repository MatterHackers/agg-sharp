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
using MatterHackers.WebGpu;

namespace MatterHackers.WebGpuRender
{
	/// <summary>
	/// Where a window's swapchain present mode comes from when nobody sets one explicitly.
	/// </summary>
	/// <remarks>
	/// An interactive window wants Fifo: vsync, no tearing, and no reason to render faster than the
	/// display. Automation wants Immediate for the opposite reason - every vsync wait is wall time the
	/// suite pays for nothing, and the classic D3D11 path presents unthrottled, so Fifo would make the
	/// wgpu provider look slower than it is and change test timing. Hence the override.
	/// </remarks>
	public static class PresentModeSettings
	{
		/// <summary>The environment variable read by <see cref="FromEnvironment"/>.</summary>
		public const string EnvironmentVariable = "AGG_PRESENT_MODE";

		/// <summary>
		/// The present mode <c>AGG_PRESENT_MODE</c> asks for - <c>fifo</c>, <c>immediate</c> or
		/// <c>mailbox</c>, case-insensitive - or Fifo when it is unset or unrecognised. A surface that does
		/// not support the requested mode falls back to Fifo as well.
		/// </summary>
		public static WGPUPresentMode FromEnvironment()
		{
			return Parse(Environment.GetEnvironmentVariable(EnvironmentVariable));
		}

		/// <summary>Parses a present mode name; anything unrecognised (null included) is Fifo.</summary>
		/// <param name="value">The name to parse.</param>
		public static WGPUPresentMode Parse(string value)
		{
			if (string.IsNullOrWhiteSpace(value))
			{
				return WGPUPresentMode.Fifo;
			}

			switch (value.Trim().ToLowerInvariant())
			{
				case "immediate":
					return WGPUPresentMode.Immediate;

				case "mailbox":
					return WGPUPresentMode.Mailbox;

				default:
					return WGPUPresentMode.Fifo;
			}
		}
	}
}
