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
	/// One color attachment of a render pass (<c>WGPURenderPassColorAttachment</c>). A pass that is
	/// being re-opened after an interruption uses <see cref="LoadOp.Load"/> so the pixels already
	/// drawn survive - that is the whole mechanism behind the consumer-side FlushPass pattern.
	/// </summary>
	public readonly struct ColorAttachment : IEquatable<ColorAttachment>
	{
		/// <summary>Creates a color attachment.</summary>
		/// <param name="target">The texture written to.</param>
		/// <param name="loadOp">What happens to the existing contents when the pass opens.</param>
		/// <param name="clearValue">The value written when <paramref name="loadOp"/> is Clear.</param>
		/// <param name="storeOp">What happens to the results when the pass ends.</param>
		public ColorAttachment(
			IGpuTexture target,
			LoadOp loadOp = LoadOp.Load,
			ClearColor clearValue = default,
			StoreOp storeOp = StoreOp.Store)
		{
			this.Target = target;
			this.LoadOp = loadOp;
			this.ClearValue = clearValue;
			this.StoreOp = storeOp;
		}

		/// <summary>The texture written to.</summary>
		public IGpuTexture Target { get; }

		/// <summary>What happens to the existing contents when the pass opens.</summary>
		public LoadOp LoadOp { get; }

		/// <summary>The value written when <see cref="LoadOp"/> is <see cref="RenderCore.LoadOp.Clear"/>.</summary>
		public ClearColor ClearValue { get; }

		/// <summary>What happens to the results when the pass ends.</summary>
		public StoreOp StoreOp { get; }

		/// <inheritdoc/>
		public bool Equals(ColorAttachment other)
			=> ReferenceEquals(this.Target, other.Target)
			&& this.LoadOp == other.LoadOp
			&& this.ClearValue.Equals(other.ClearValue)
			&& this.StoreOp == other.StoreOp;

		/// <inheritdoc/>
		public override bool Equals(object obj) => obj is ColorAttachment other && this.Equals(other);

		/// <inheritdoc/>
		public override int GetHashCode() => HashCode.Combine(this.Target, this.LoadOp, this.ClearValue, this.StoreOp);

		/// <inheritdoc/>
		public override string ToString()
			=> $"color {this.Target?.Label} {this.LoadOp}"
			+ (this.LoadOp == LoadOp.Clear ? $" {this.ClearValue}" : string.Empty)
			+ $" {this.StoreOp}";
	}

	/// <summary>
	/// The depth attachment of a render pass (the depth half of
	/// <c>WGPURenderPassDepthStencilAttachment</c>). A <see cref="Target"/> of null means the pass has
	/// no depth buffer.
	/// <para>
	/// <see cref="ClearValue"/> defaults to zero, not to the far plane, so that
	/// <c>default(DepthAttachment)</c> and <c>new DepthAttachment(target)</c> describe the same
	/// attachment - the same rule <c>SamplerDescriptor</c> follows, and the same choice
	/// <c>WGPURenderPassDepthStencilAttachment</c> itself makes. A pass that clears depth wants
	/// <see cref="FarClear"/>, and says so at the call site rather than inheriting it from an optional
	/// argument nobody can see.
	/// </para>
	/// </summary>
	public readonly struct DepthAttachment : IEquatable<DepthAttachment>
	{
		/// <summary>The far plane, which is what clearing a depth buffer normally means.</summary>
		public const float FarClear = 1.0f;

		/// <summary>Creates a depth attachment.</summary>
		/// <param name="target">The depth texture written to.</param>
		/// <param name="loadOp">What happens to the existing depth when the pass opens.</param>
		/// <param name="clearValue">The depth written when <paramref name="loadOp"/> is Clear; pass
		/// <see cref="FarClear"/> for the usual full clear.</param>
		/// <param name="storeOp">What happens to the depth when the pass ends.</param>
		public DepthAttachment(
			IGpuTexture target,
			LoadOp loadOp = LoadOp.Load,
			float clearValue = 0,
			StoreOp storeOp = StoreOp.Store)
		{
			this.Target = target;
			this.LoadOp = loadOp;
			this.ClearValue = clearValue;
			this.StoreOp = storeOp;
		}

		/// <summary>No depth attachment.</summary>
		public static DepthAttachment None => default;

		/// <summary>The depth texture written to, or null for no depth attachment.</summary>
		public IGpuTexture Target { get; }

		/// <summary>What happens to the existing depth when the pass opens.</summary>
		public LoadOp LoadOp { get; }

		/// <summary>The depth written when <see cref="LoadOp"/> is <see cref="RenderCore.LoadOp.Clear"/>.</summary>
		public float ClearValue { get; }

		/// <summary>What happens to the depth when the pass ends.</summary>
		public StoreOp StoreOp { get; }

		/// <inheritdoc/>
		public bool Equals(DepthAttachment other)
			=> ReferenceEquals(this.Target, other.Target)
			&& this.LoadOp == other.LoadOp
			&& this.ClearValue.Equals(other.ClearValue)
			&& this.StoreOp == other.StoreOp;

		/// <inheritdoc/>
		public override bool Equals(object obj) => obj is DepthAttachment other && this.Equals(other);

		/// <inheritdoc/>
		public override int GetHashCode() => HashCode.Combine(this.Target, this.LoadOp, this.ClearValue, this.StoreOp);

		/// <inheritdoc/>
		public override string ToString()
			=> this.Target == null
				? "no depth"
				: $"depth {this.Target.Label} {this.LoadOp}"
					+ (this.LoadOp == LoadOp.Clear ? $" {this.ClearValue}" : string.Empty)
					+ $" {this.StoreOp}";
	}

	/// <summary>
	/// Everything needed to open a render pass (<c>WGPURenderPassDescriptor</c>): which textures are
	/// written and what happens to their existing contents.
	/// </summary>
	public readonly struct RenderPassDescriptor : IEquatable<RenderPassDescriptor>
	{
		private readonly ColorAttachment[] colorAttachments;

		/// <summary>Creates a render pass descriptor.</summary>
		/// <param name="colorAttachments">The color attachments, in the order the pipeline writes them.</param>
		/// <param name="depth">The depth attachment, or <see cref="DepthAttachment.None"/>.</param>
		/// <param name="label">Optional debug name.</param>
		public RenderPassDescriptor(ColorAttachment[] colorAttachments, DepthAttachment depth = default, string label = null)
		{
			this.colorAttachments = colorAttachments ?? Array.Empty<ColorAttachment>();
			this.Depth = depth;
			this.Label = label ?? string.Empty;
		}

		/// <summary>Creates a single color attachment pass - by far the common case.</summary>
		/// <param name="target">The texture written to.</param>
		/// <param name="loadOp">What happens to the existing contents when the pass opens.</param>
		/// <param name="clearValue">The value written when <paramref name="loadOp"/> is Clear.</param>
		/// <param name="label">Optional debug name.</param>
		public RenderPassDescriptor(IGpuTexture target, LoadOp loadOp = LoadOp.Load, ClearColor clearValue = default, string label = null)
			: this(new[] { new ColorAttachment(target, loadOp, clearValue) }, default, label)
		{
		}

		/// <summary>The color attachments. Never null.</summary>
		public ColorAttachment[] ColorAttachments => this.colorAttachments ?? Array.Empty<ColorAttachment>();

		/// <summary>The depth attachment; its Target is null when the pass has no depth buffer.</summary>
		public DepthAttachment Depth { get; }

		/// <summary>Debug name.</summary>
		public string Label { get; }

		/// <inheritdoc/>
		public bool Equals(RenderPassDescriptor other)
			=> this.Depth.Equals(other.Depth)
			&& DescriptorEquality.ArrayEquals(this.ColorAttachments, other.ColorAttachments);

		/// <inheritdoc/>
		public override bool Equals(object obj) => obj is RenderPassDescriptor other && this.Equals(other);

		/// <inheritdoc/>
		public override int GetHashCode()
			=> HashCode.Combine(this.Depth, DescriptorEquality.ArrayHash(this.ColorAttachments));

		/// <inheritdoc/>
		public override string ToString()
			=> $"Pass [{string.Join(", ", this.ColorAttachments)}] {this.Depth}"
			+ (string.IsNullOrEmpty(this.Label) ? string.Empty : $" '{this.Label}'");
	}
}
