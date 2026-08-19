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
	/// An RGBA clear value, unclamped and in linear component order (<c>WGPUColor</c>). Doubles, not
	/// floats, because that is what webgpu.h uses.
	/// </summary>
	public readonly struct ClearColor : IEquatable<ClearColor>
	{
		/// <summary>Creates a clear value.</summary>
		/// <param name="red">Red component, normally 0..1.</param>
		/// <param name="green">Green component, normally 0..1.</param>
		/// <param name="blue">Blue component, normally 0..1.</param>
		/// <param name="alpha">Alpha component, normally 0..1.</param>
		public ClearColor(double red, double green, double blue, double alpha)
		{
			this.Red = red;
			this.Green = green;
			this.Blue = blue;
			this.Alpha = alpha;
		}

		/// <summary>Fully transparent black - the default clear.</summary>
		public static ClearColor Transparent => new ClearColor(0, 0, 0, 0);

		/// <summary>Opaque black.</summary>
		public static ClearColor Black => new ClearColor(0, 0, 0, 1);

		/// <summary>Red component.</summary>
		public double Red { get; }

		/// <summary>Green component.</summary>
		public double Green { get; }

		/// <summary>Blue component.</summary>
		public double Blue { get; }

		/// <summary>Alpha component.</summary>
		public double Alpha { get; }

		/// <inheritdoc/>
		public bool Equals(ClearColor other)
			=> this.Red.Equals(other.Red)
			&& this.Green.Equals(other.Green)
			&& this.Blue.Equals(other.Blue)
			&& this.Alpha.Equals(other.Alpha);

		/// <inheritdoc/>
		public override bool Equals(object obj) => obj is ClearColor other && this.Equals(other);

		/// <inheritdoc/>
		public override int GetHashCode() => HashCode.Combine(this.Red, this.Green, this.Blue, this.Alpha);

		/// <inheritdoc/>
		public override string ToString() => $"({this.Red}, {this.Green}, {this.Blue}, {this.Alpha})";
	}

	/// <summary>
	/// Everything needed to create a texture (<c>WGPUTextureDescriptor</c>, restricted to 2D). Value
	/// equality ignores <see cref="Label"/> so two identically shaped textures with different debug
	/// names still hit the same cache entry.
	/// </summary>
	public readonly struct TextureDescriptor : IEquatable<TextureDescriptor>
	{
		/// <summary>Creates a 2D texture descriptor.</summary>
		/// <param name="width">Width in pixels.</param>
		/// <param name="height">Height in pixels.</param>
		/// <param name="format">Pixel format.</param>
		/// <param name="usage">Every use the texture will be put to - WebGPU rejects undeclared uses.</param>
		/// <param name="mipLevelCount">Number of mip levels; 1 for no mips.</param>
		/// <param name="sampleCount">MSAA sample count; 1 for no multisampling.</param>
		/// <param name="label">Optional debug name. Not part of equality.</param>
		public TextureDescriptor(
			uint width,
			uint height,
			TextureFormat format,
			TextureUsage usage,
			uint mipLevelCount = 1,
			uint sampleCount = 1,
			string label = null)
		{
			this.Width = width;
			this.Height = height;
			this.Format = format;
			this.Usage = usage;
			this.MipLevelCount = mipLevelCount;
			this.SampleCount = sampleCount;
			this.Label = label ?? string.Empty;
		}

		/// <summary>Width in pixels.</summary>
		public uint Width { get; }

		/// <summary>Height in pixels.</summary>
		public uint Height { get; }

		/// <summary>Pixel format.</summary>
		public TextureFormat Format { get; }

		/// <summary>The uses declared at creation.</summary>
		public TextureUsage Usage { get; }

		/// <summary>Number of mip levels.</summary>
		public uint MipLevelCount { get; }

		/// <summary>MSAA sample count.</summary>
		public uint SampleCount { get; }

		/// <summary>Debug name. Not part of equality.</summary>
		public string Label { get; }

		/// <inheritdoc/>
		public bool Equals(TextureDescriptor other)
			=> this.Width == other.Width
			&& this.Height == other.Height
			&& this.Format == other.Format
			&& this.Usage == other.Usage
			&& this.MipLevelCount == other.MipLevelCount
			&& this.SampleCount == other.SampleCount;

		/// <inheritdoc/>
		public override bool Equals(object obj) => obj is TextureDescriptor other && this.Equals(other);

		/// <inheritdoc/>
		public override int GetHashCode()
			=> HashCode.Combine(this.Width, this.Height, this.Format, this.Usage, this.MipLevelCount, this.SampleCount);

		/// <inheritdoc/>
		public override string ToString()
			=> $"Texture {this.Width}x{this.Height} {this.Format} {this.Usage}"
			+ (this.MipLevelCount == 1 ? string.Empty : $" mips {this.MipLevelCount}")
			+ (this.SampleCount == 1 ? string.Empty : $" samples {this.SampleCount}")
			+ (string.IsNullOrEmpty(this.Label) ? string.Empty : $" '{this.Label}'");
	}

	/// <summary>
	/// Everything needed to create a sampler (<c>WGPUSamplerDescriptor</c>). Samplers are few and
	/// heavily shared, so this is a cache key too; <see cref="Label"/> is excluded from equality.
	/// <para>
	/// Every argument is optional, so the defaults are deliberately chosen to be the zero values:
	/// <c>default(SamplerDescriptor)</c> and <c>new SamplerDescriptor()</c> describe the same sampler
	/// (clamped, nearest) rather than two different ones. Use <see cref="LinearClamp"/> for the
	/// filtered case instead of relying on an optional argument.
	/// </para>
	/// </summary>
	public readonly struct SamplerDescriptor : IEquatable<SamplerDescriptor>
	{
		/// <summary>Creates a sampler descriptor.</summary>
		/// <param name="addressModeU">Wrap behavior in U.</param>
		/// <param name="addressModeV">Wrap behavior in V.</param>
		/// <param name="magFilter">Filter when magnifying.</param>
		/// <param name="minFilter">Filter when minifying.</param>
		/// <param name="mipmapFilter">Filter between mip levels.</param>
		/// <param name="label">Optional debug name. Not part of equality.</param>
		public SamplerDescriptor(
			AddressMode addressModeU = AddressMode.ClampToEdge,
			AddressMode addressModeV = AddressMode.ClampToEdge,
			FilterMode magFilter = FilterMode.Nearest,
			FilterMode minFilter = FilterMode.Nearest,
			FilterMode mipmapFilter = FilterMode.Nearest,
			string label = null)
		{
			this.AddressModeU = addressModeU;
			this.AddressModeV = addressModeV;
			this.MagFilter = magFilter;
			this.MinFilter = minFilter;
			this.MipmapFilter = mipmapFilter;
			this.Label = label ?? string.Empty;
		}

		/// <summary>Clamped, unfiltered - the same sampler as <c>default(SamplerDescriptor)</c>.</summary>
		public static SamplerDescriptor NearestClamp => default;

		/// <summary>Clamped, bilinear - what image and glyph drawing uses.</summary>
		public static SamplerDescriptor LinearClamp
			=> new SamplerDescriptor(AddressMode.ClampToEdge, AddressMode.ClampToEdge, FilterMode.Linear, FilterMode.Linear);

		/// <summary>Wrap behavior in U.</summary>
		public AddressMode AddressModeU { get; }

		/// <summary>Wrap behavior in V.</summary>
		public AddressMode AddressModeV { get; }

		/// <summary>Filter when magnifying.</summary>
		public FilterMode MagFilter { get; }

		/// <summary>Filter when minifying.</summary>
		public FilterMode MinFilter { get; }

		/// <summary>Filter between mip levels.</summary>
		public FilterMode MipmapFilter { get; }

		/// <summary>Debug name. Not part of equality.</summary>
		public string Label { get; }

		/// <inheritdoc/>
		public bool Equals(SamplerDescriptor other)
			=> this.AddressModeU == other.AddressModeU
			&& this.AddressModeV == other.AddressModeV
			&& this.MagFilter == other.MagFilter
			&& this.MinFilter == other.MinFilter
			&& this.MipmapFilter == other.MipmapFilter;

		/// <inheritdoc/>
		public override bool Equals(object obj) => obj is SamplerDescriptor other && this.Equals(other);

		/// <inheritdoc/>
		public override int GetHashCode()
			=> HashCode.Combine(
				this.AddressModeU,
				this.AddressModeV,
				this.MagFilter,
				this.MinFilter,
				this.MipmapFilter);

		/// <inheritdoc/>
		public override string ToString()
			=> $"Sampler {this.AddressModeU}/{this.AddressModeV} mag {this.MagFilter} min {this.MinFilter} mip {this.MipmapFilter}"
			+ (string.IsNullOrEmpty(this.Label) ? string.Empty : $" '{this.Label}'");
	}

	/// <summary>
	/// The limits a device was created with (<c>wgpuDeviceGetLimits</c>). Only the ones application data
	/// can actually reach are carried: a limit nothing sizes against is a limit nobody can honor.
	/// </summary>
	public readonly struct DeviceLimits
	{
		/// <summary>The WebGPU default, 256 MiB. What a device reports when it grants no more.</summary>
		public const ulong DefaultMaxBufferSize = 268435456;

		/// <summary>
		/// The WebGPU default, 8192 pixels. Desktop adapters support 16384, but a device only gets that by
		/// asking for it - see <c>WebGpuRenderDevice.RequestDevice</c>.
		/// </summary>
		public const uint DefaultMaxTextureDimension2D = 8192;

		/// <summary>Creates a limit set.</summary>
		/// <param name="maxBufferSize">The largest buffer the device will create, in bytes.</param>
		/// <param name="maxTextureDimension2D">The largest 2D texture edge the device will create, in pixels.</param>
		public DeviceLimits(ulong maxBufferSize, uint maxTextureDimension2D = DefaultMaxTextureDimension2D)
		{
			this.MaxBufferSize = maxBufferSize;
			this.MaxTextureDimension2D = maxTextureDimension2D;
		}

		/// <summary>
		/// The largest buffer <see cref="IRenderDevice.CreateBuffer"/> will create
		/// (<c>WGPULimits.maxBufferSize</c>). Callers whose data can exceed it - mesh vertex data is the
		/// only one that does - split it into several buffers rather than asking for one that is refused.
		/// </summary>
		public ulong MaxBufferSize { get; }

		/// <summary>
		/// The largest width or height <see cref="IRenderDevice.CreateTexture"/> will create
		/// (<c>WGPULimits.maxTextureDimension2D</c>). The full-frame supersample capture is what reaches it:
		/// a 3x capture of a fullscreen retina window asks for 9072 pixels against a default of 8192.
		/// </summary>
		public uint MaxTextureDimension2D { get; }

		/// <inheritdoc/>
		public override string ToString()
			=> $"DeviceLimits maxBufferSize {this.MaxBufferSize:N0} maxTextureDimension2D {this.MaxTextureDimension2D:N0}";
	}
}
