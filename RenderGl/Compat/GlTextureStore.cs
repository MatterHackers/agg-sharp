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
using MatterHackers.RenderCore;
using MatterHackers.RenderGl.OpenGl;

namespace MatterHackers.RenderGl.Compat
{
	/// <summary>
	/// GL texture names and the retained texture and sampler behind each one. GL names are small
	/// integers handed out by <c>glGenTextures</c> and everything downstream refers to them, so the
	/// compat layer has to keep the indirection even though WebGPU has real object handles.
	/// </summary>
	public class GlTextureStore : IDisposable
	{
		private readonly IRenderDevice device;
		private readonly Dictionary<int, GlTextureEntry> textures = new Dictionary<int, GlTextureEntry>();
		private readonly Dictionary<SamplerDescriptor, ISampler> samplers = new Dictionary<SamplerDescriptor, ISampler>();
		private int nextTextureName = 1;

		/// <summary>Creates a texture store over a device.</summary>
		/// <param name="device">The device textures and samplers are created on.</param>
		public GlTextureStore(IRenderDevice device)
		{
			this.device = device ?? throw new ArgumentNullException(nameof(device));
		}

		/// <summary>Reserves a texture name, as <c>glGenTextures</c> does. No GPU object exists yet.</summary>
		public int GenerateName()
		{
			int name = this.nextTextureName++;
			this.textures[name] = new GlTextureEntry();
			return name;
		}

		/// <summary>Releases a texture name and everything behind it.</summary>
		/// <param name="name">The texture name.</param>
		public void Delete(int name)
		{
			if (this.textures.TryGetValue(name, out var entry))
			{
				entry.Texture?.Dispose();
				this.textures.Remove(name);
			}
		}

		/// <summary>Looks a texture name up, returning null when it is unknown or empty.</summary>
		/// <param name="name">The texture name.</param>
		public GlTextureEntry Find(int name)
			=> name > 0 && this.textures.TryGetValue(name, out var entry) ? entry : null;

		/// <summary>
		/// Uploads one mip level of a texture. Level 0 (re)creates the texture and every level after it
		/// writes into the chain that level 0 allocated.
		/// <para>
		/// The classic D3D11 path accumulates the levels a caller pushes and only creates the texture
		/// once the whole chain has arrived, because D3D11 takes the level data at creation. WebGPU
		/// separates the two - <c>wgpuDeviceCreateTexture</c> fixes <c>mipLevelCount</c>, then each
		/// level is a separate <c>wgpuQueueWriteTexture</c> - so no accumulation buffer is needed here.
		/// What is needed instead is knowing the level count up front, and that comes from the min
		/// filter: <c>ImageTexturePlugin</c> sets a <c>*_MIPMAP_*</c> min filter before uploading level
		/// 0, so a mipmapped texture gets the full canonical chain allocated and an unmipmapped one gets
		/// exactly one level. A caller that uploads mip levels without ever asking for a mipmapped min
		/// filter gets only level 0, the same as before.
		/// </para>
		/// </summary>
		/// <param name="name">The texture name to upload into.</param>
		/// <param name="level">Mip level; 0 recreates, higher levels write into the existing chain.</param>
		/// <param name="width">Width of this level in pixels.</param>
		/// <param name="height">Height of this level in pixels.</param>
		/// <param name="glFormat">The GL pixel format - 0x80E1 is BGRA, anything else is treated as RGBA.</param>
		/// <param name="pixels">Tightly packed pixels, first row first, or null to allocate without contents.</param>
		public void UploadImage(int name, int level, int width, int height, int glFormat, byte[] pixels)
		{
			var entry = this.Find(name);
			if (entry == null || level < 0 || width <= 0 || height <= 0)
			{
				return;
			}

			var format = glFormat == 0x80E1 ? TextureFormat.Bgra8Unorm : TextureFormat.Rgba8Unorm;
			if (level == 0)
			{
				uint mipLevelCount = entry.MipmapFilterEnabled ? FullMipChainLength(width, height) : 1;
				if (entry.Texture == null
					|| entry.Texture.Descriptor.Width != (uint)width
					|| entry.Texture.Descriptor.Height != (uint)height
					|| entry.Texture.Descriptor.Format != format
					|| entry.Texture.Descriptor.MipLevelCount != mipLevelCount)
				{
					entry.Texture?.Dispose();
					entry.Texture = this.device.CreateTexture(new TextureDescriptor(
						(uint)width,
						(uint)height,
						format,
						TextureUsage.TextureBinding | TextureUsage.CopyDst,
						mipLevelCount,
						1,
						"glTexture" + name));
				}
			}
			else if (entry.Texture == null || (uint)level >= entry.Texture.Descriptor.MipLevelCount)
			{
				// A level the allocated chain cannot hold is dropped rather than thrown on: the 2D path
				// pushes levels unconditionally and refusing would break drawing that works at level 0.
				return;
			}

			if (pixels != null)
			{
				this.device.WriteTexture(entry.Texture, pixels, (uint)(width * 4), (uint)level);
			}
		}

		/// <summary>
		/// Applies a <c>glTexParameter</c>. The sampler is rebuilt lazily by
		/// <see cref="GetSampler"/>, so this only records intent.
		/// </summary>
		/// <param name="name">The texture name.</param>
		/// <param name="parameter">Which parameter.</param>
		/// <param name="value">The raw GL value.</param>
		public void SetParameter(int name, TextureParameterName parameter, int value)
		{
			var entry = this.Find(name);
			if (entry == null)
			{
				return;
			}

			switch (parameter)
			{
				case TextureParameterName.TextureMagFilter:
					entry.MagFilterLinear = value == 9729;
					break;

				case TextureParameterName.TextureMinFilter:
					// Anything except GL_NEAREST counts as filtered, including the mipmapped modes -
					// the same coarse reading the classic path uses.
					entry.MinFilterLinear = value != 9728;

					// GL_NEAREST_MIPMAP_NEAREST(9984) .. GL_LINEAR_MIPMAP_LINEAR(9987) are the four modes
					// that sample the mip chain, and callers set this before uploading level 0, which is
					// what lets UploadImage size the chain at creation.
					entry.MipmapFilterEnabled = value >= 9984 && value <= 9987;

					// Of those four, the two *_MIPMAP_LINEAR modes blend between adjacent levels;
					// *_MIPMAP_NEAREST picks one. GL_NEAREST_MIPMAP_LINEAR is 9986, GL_LINEAR_MIPMAP_LINEAR
					// is 9987.
					entry.MipmapFilterLinear = value >= 9986 && value <= 9987;
					break;

				case TextureParameterName.TextureWrapS:
				case TextureParameterName.TextureWrapT:
					entry.Clamp = value == 33071;
					break;
			}
		}

		/// <summary>Returns the sampler matching a texture's filter and wrap state, cached by descriptor.</summary>
		/// <remarks>
		/// The mip filter follows the GL min filter mode rather than being pinned to nearest, which it was
		/// until the mip chains started uploading for real: a <c>LINEAR_MIPMAP_LINEAR</c> texture sampled
		/// with a nearest mip filter snaps between levels instead of blending them, which reads as a hard
		/// change in sharpness as a textured surface recedes.
		/// </remarks>
		/// <param name="entry">The texture whose sampling state is wanted.</param>
		public ISampler GetSampler(GlTextureEntry entry)
		{
			var address = entry.Clamp ? AddressMode.ClampToEdge : AddressMode.Repeat;
			var descriptor = new SamplerDescriptor(
				address,
				address,
				entry.MagFilterLinear ? FilterMode.Linear : FilterMode.Nearest,
				entry.MinFilterLinear ? FilterMode.Linear : FilterMode.Nearest,
				entry.MipmapFilterLinear ? FilterMode.Linear : FilterMode.Nearest);

			if (!this.samplers.TryGetValue(descriptor, out var sampler))
			{
				sampler = this.device.CreateSampler(descriptor);
				this.samplers[descriptor] = sampler;
			}

			return sampler;
		}

		/// <summary>
		/// How many levels a complete mip chain for one image has, down to and including 1x1. This is
		/// the same <c>1 + floor(log2(max(width, height)))</c> the D3D11 backend computes as its
		/// expected mip count, so both paths allocate identically shaped textures.
		/// </summary>
		/// <param name="width">Level 0 width in pixels.</param>
		/// <param name="height">Level 0 height in pixels.</param>
		public static uint FullMipChainLength(int width, int height)
			=> (uint)(1 + (int)Math.Floor(Math.Log(Math.Max(1, Math.Max(width, height)), 2)));

		/// <summary>Releases every texture and sampler.</summary>
		public void Dispose()
		{
			foreach (var entry in this.textures.Values)
			{
				entry.Texture?.Dispose();
			}

			foreach (var sampler in this.samplers.Values)
			{
				sampler.Dispose();
			}

			this.textures.Clear();
			this.samplers.Clear();
		}
	}

	/// <summary>One GL texture name: the retained texture, if it has been uploaded, and its sampling state.</summary>
	public class GlTextureEntry
	{
		/// <summary>The retained texture, or null when the name has been generated but never uploaded.</summary>
		public IGpuTexture Texture { get; set; }

		/// <summary>Whether coordinates clamp rather than wrap.</summary>
		public bool Clamp { get; set; }

		/// <summary>Whether magnification filters. GL's default is linear, and so is this.</summary>
		public bool MagFilterLinear { get; set; } = true;

		/// <summary>Whether minification filters.</summary>
		public bool MinFilterLinear { get; set; } = true;

		/// <summary>
		/// Whether the min filter is one of GL's mipmapped modes. Read at level 0 upload to decide how
		/// long a mip chain to allocate, because WebGPU cannot grow one afterwards.
		/// </summary>
		public bool MipmapFilterEnabled { get; set; }

		/// <summary>
		/// Whether the min filter blends between mip levels (the <c>*_MIPMAP_LINEAR</c> modes) rather than
		/// picking one. Read by <see cref="GlTextureStore.GetSampler"/>, and so part of the sampler cache key.
		/// </summary>
		public bool MipmapFilterLinear { get; set; }
	}
}
