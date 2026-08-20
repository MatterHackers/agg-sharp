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
using MatterHackers.RenderCore;

namespace MatterHackers.RenderGl.Compat
{
	/// <summary>
	/// Owns the one render pass the compat layer keeps open across a frame.
	/// <para>
	/// GL has no notion of a pass: widget paint interleaves draws, texture uploads, clears and readback
	/// in whatever order it likes. WebGPU passes are explicit, non-nestable, and forbid readback, submit
	/// and present while one is open. The reconciliation is this class: a pass is opened lazily on the
	/// first draw (<see cref="EnsurePassOpen"/>) and ended whenever something a pass forbids has to
	/// happen (<see cref="FlushPass"/>); the next draw re-opens it with <see cref="LoadOp.Load"/> so the
	/// pixels already drawn survive. Nothing above this class has to think about passes at all.
	/// </para>
	/// </summary>
	public class GlRenderPassScope : IDisposable
	{
		private readonly IRenderDevice device;
		private readonly Action<IRenderEncoder> applyDynamicState;
		private IRenderEncoder encoder;
		private bool hasEverHadTarget;
		private bool clearColorPending;
		private bool clearDepthPending;
		private MatterHackers.RenderCore.ClearColor pendingClearValue;

		/// <summary>Creates a pass scope.</summary>
		/// <param name="device">The device passes are opened on.</param>
		/// <param name="applyDynamicState">
		/// Called every time a pass opens, to re-apply the viewport and scissor. Those are pass-scoped
		/// in WebGPU: a re-opened pass starts at the full attachment again, so without this a mid-frame
		/// flush would silently drop the clip.
		/// </param>
		public GlRenderPassScope(IRenderDevice device, Action<IRenderEncoder> applyDynamicState)
		{
			this.device = device ?? throw new ArgumentNullException(nameof(device));
			this.applyDynamicState = applyDynamicState;
		}

		/// <summary>The color attachment drawn into. Setting it ends any open pass.</summary>
		public IGpuTexture ColorTarget { get; private set; }

		/// <summary>The depth attachment, or null when there is none.</summary>
		public IGpuTexture DepthTarget { get; private set; }

		/// <summary>True while a pass is open.</summary>
		public bool IsPassOpen => this.encoder != null;

		/// <summary>
		/// True when a target was set once and has since been taken away - by the present that ended the
		/// frame, by a resize freeing the swapchain textures, by a device loss rebuilding everything, or
		/// by this scope being disposed.
		/// <para>
		/// It is the difference between the two ways there can be no target, and the widget tree is why
		/// it has to be told apart. A paint that is half way through the widget tree cannot be stopped
		/// when the frame's destination disappears underneath it, so the draws it goes on issuing are
		/// dropped here and the next frame - which sets a target again - renders normally. A context that
		/// has *never* had a target is the other thing entirely: a host that never wired itself up, which
		/// still throws out of <see cref="EnsurePassOpen"/>.
		/// </para>
		/// </summary>
		public bool TargetReleased => this.ColorTarget == null && this.hasEverHadTarget;

		/// <summary>
		/// How many times a pass has been opened. A test reads this to prove that an interruption
		/// really did end and re-open the pass rather than quietly draw into a stale one.
		/// </summary>
		public int PassOpenCount { get; private set; }

		/// <summary>The color attachment's format, or Undefined when no target is set.</summary>
		public TextureFormat ColorFormat
			=> this.ColorTarget?.Descriptor.Format ?? TextureFormat.Undefined;

		/// <summary>The depth attachment's format, or Undefined when there is none.</summary>
		public TextureFormat DepthFormat
			=> this.DepthTarget?.Descriptor.Format ?? TextureFormat.Undefined;

		/// <summary>The color attachment's height in pixels, used to flip GL's y-up coordinates.</summary>
		public int TargetHeight => (int)(this.ColorTarget?.Descriptor.Height ?? 0);

		/// <summary>The color attachment's width in pixels.</summary>
		public int TargetWidth => (int)(this.ColorTarget?.Descriptor.Width ?? 0);

		/// <summary>Points subsequent drawing at new attachments, ending any pass in progress.</summary>
		/// <param name="colorTarget">The texture to draw into.</param>
		/// <param name="depthTarget">The depth texture, or null.</param>
		public void SetTargets(IGpuTexture colorTarget, IGpuTexture depthTarget)
		{
			this.FlushPass();
			this.ColorTarget = colorTarget;
			this.DepthTarget = depthTarget;
			this.hasEverHadTarget |= colorTarget != null;
		}

		/// <summary>
		/// Queues a clear for the next pass. WebGPU clears through a pass's load op rather than with a
		/// command, so a clear requested mid-pass has to end that pass and re-open it - which is why
		/// this flushes rather than recording anything.
		/// </summary>
		/// <param name="color">Clear the color attachment.</param>
		/// <param name="depth">Clear the depth attachment.</param>
		/// <param name="clearValue">The color to clear to.</param>
		public void RequestClear(bool color, bool depth, MatterHackers.RenderCore.ClearColor clearValue)
		{
			if (!color && !depth)
			{
				return;
			}

			this.FlushPass();
			this.clearColorPending |= color;
			this.clearDepthPending |= depth;
			this.pendingClearValue = clearValue;
		}

		/// <summary>
		/// Returns the open pass, opening one if needed. Any queued clear is consumed as the load op of
		/// the pass this opens.
		/// </summary>
		/// <returns>
		/// The open pass, or null when the frame's target has been released (see
		/// <see cref="TargetReleased"/>) and there is therefore nothing left to draw into. Callers that
		/// record work must treat null as "drop this draw".
		/// </returns>
		/// <exception cref="InvalidOperationException">
		/// No render target has ever been set, so the host never said where its frames go.
		/// </exception>
		public IRenderEncoder EnsurePassOpen()
		{
			if (this.encoder != null)
			{
				return this.encoder;
			}

			if (this.ColorTarget == null)
			{
				if (this.hasEverHadTarget)
				{
					FrameProfiler.Count("DrawAfterTargetReleased");
					return null;
				}

				throw new InvalidOperationException(
					"No render target is set on the compat context. Call SetRenderTarget before drawing.");
			}

			var color = new ColorAttachment(
				this.ColorTarget,
				this.clearColorPending ? LoadOp.Clear : LoadOp.Load,
				this.pendingClearValue);

			var depth = this.DepthTarget == null
				? DepthAttachment.None
				: new DepthAttachment(
					this.DepthTarget,
					this.clearDepthPending ? LoadOp.Clear : LoadOp.Load,
					DepthAttachment.FarClear);

			this.clearColorPending = false;
			this.clearDepthPending = false;

			FrameProfiler.Count("PassOpen");
			using (FrameProfiler.Time("PassOpen"))
			{
				this.encoder = this.device.BeginRenderPass(
					new RenderPassDescriptor(new[] { color }, depth, "GlCompat"));
			}

			this.PassOpenCount++;

			this.applyDynamicState?.Invoke(this.encoder);
			return this.encoder;
		}

		/// <summary>
		/// Ends the pass if one is open. Safe and cheap to call before anything a pass forbids, which is
		/// exactly how callers should use it - guess wrong in this direction and the cost is a pass
		/// boundary, guess wrong in the other and the device throws.
		/// </summary>
		public void FlushPass()
		{
			if (this.encoder == null)
			{
				return;
			}

			var ending = this.encoder;
			this.encoder = null;
			using (FrameProfiler.Time("PassEnd"))
			{
				ending.Dispose();
			}
		}

		/// <summary>Ends any open pass and forgets the targets.</summary>
		public void Dispose()
		{
			this.FlushPass();
			this.ColorTarget = null;
			this.DepthTarget = null;
		}
	}
}
