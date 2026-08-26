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

namespace MatterHackers.WebGpuRender
{
	/// <summary>
	/// What to do with the result of <c>wgpuSurfaceGetCurrentTexture</c>.
	/// <para>
	/// The same vocabulary as agg-gui-wgpu's <c>SurfaceAcquire</c> (<c>gpu.rs</c>), deliberately named the
	/// same way so the two ports can be compared line for line. The one extra member is
	/// <see cref="Fail"/>: the C binding's status is an open <c>int</c>, so unlike Rust's closed enum there
	/// is a "the header does not define this" case to answer for.
	/// </para>
	/// </summary>
	public enum SurfaceAcquireAction
	{
		/// <summary>The texture is usable - render into it.</summary>
		Present,

		/// <summary>
		/// The swapchain is stale or gone (Outdated/Lost): reconfigure the surface and try once more
		/// this frame. This is what a window resize looks like from the acquire.
		/// </summary>
		Reconfigure,

		/// <summary>
		/// Transient (Timeout): skip the frame without touching the swapchain, but ask for another frame
		/// so a host that only paints on demand does not idle forever.
		/// </summary>
		SkipAndRetry,

		/// <summary>
		/// Skip the frame with no follow-up (Occluded/Error): the window is not visible, or the app has a
		/// validation error to fix, and a self-requested redraw would just burn CPU.
		/// </summary>
		Skip,

		/// <summary>A status the binding does not define - a driver or binding bug, so the acquire throws.</summary>
		Fail,
	}
}
