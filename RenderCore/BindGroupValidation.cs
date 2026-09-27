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
	/// The buffer rules of <see cref="IRenderDevice.CreateBindGroup"/>, in one place so the native device
	/// and the recording double refuse exactly the same calls.
	/// <para>
	/// Why managed code checks what wgpu would also check: a storage binding larger than the device's
	/// <c>maxStorageBufferBindingSize</c> is not reported as a validation error by wgpu-native - the bind
	/// group is created, and the process aborts inside wgpu when the work using it is submitted. A caller
	/// sizing compute work against <see cref="DeviceLimits"/> gets an exception here instead.
	/// </para>
	/// </summary>
	public static class BindGroupValidation
	{
		/// <summary>
		/// Throws if any buffer entry of <paramref name="descriptor"/> binds a range its layout type does
		/// not allow under <paramref name="limits"/>. An entry whose binding the pipeline's layout does not
		/// declare is left to the backend: its type is unknown here.
		/// </summary>
		/// <param name="descriptor">The bind group about to be created.</param>
		/// <param name="limits">The limits of the device creating it.</param>
		/// <exception cref="ArgumentOutOfRangeException">A bound range is larger than the binding type allows.</exception>
		/// <exception cref="ArgumentException">An offset is misaligned or past the end of its buffer.</exception>
		public static void Validate(in BindGroupDescriptor descriptor, DeviceLimits limits)
		{
			BindGroupLayoutEntry[] layout = descriptor.Pipeline != null
				? descriptor.Pipeline.Descriptor.BindGroupLayout
				: descriptor.ComputePipeline?.Descriptor.BindGroupLayout ?? Array.Empty<BindGroupLayoutEntry>();

			foreach (BindGroupEntry entry in descriptor.Entries)
			{
				if (entry.Buffer == null || !TryFindType(layout, descriptor.Group, entry.Binding, out BindingType type))
				{
					continue;
				}

				ulong maxSize;
				uint alignment;
				string limitName;
				switch (type)
				{
					case BindingType.UniformBuffer:
						maxSize = limits.MaxUniformBufferBindingSize;
						alignment = limits.MinUniformBufferOffsetAlignment;
						limitName = "maxUniformBufferBindingSize";
						break;
					case BindingType.StorageBuffer:
					case BindingType.ReadOnlyStorageBuffer:
						maxSize = limits.MaxStorageBufferBindingSize;
						alignment = limits.MinStorageBufferOffsetAlignment;
						limitName = "maxStorageBufferBindingSize";
						break;
					default:
						continue;
				}

				IGpuBuffer buffer = entry.Buffer;
				if (alignment != 0 && entry.Offset % alignment != 0)
				{
					throw new ArgumentException(
						$"@group({descriptor.Group}) @binding({entry.Binding}): a {type} offset must be a multiple of {alignment}, "
						+ $"not {entry.Offset} ('{buffer.Label}').",
						nameof(descriptor));
				}

				if (entry.Offset > buffer.SizeInBytes
					|| (entry.Size != 0 && entry.Size > buffer.SizeInBytes - entry.Offset))
				{
					throw new ArgumentException(
						$"@group({descriptor.Group}) @binding({entry.Binding}): binding {entry.Size} bytes at offset {entry.Offset} "
						+ $"runs past the end of the {buffer.SizeInBytes} byte buffer '{buffer.Label}'.",
						nameof(descriptor));
				}

				// RenderCore spells "to the end of the buffer" as a size of 0.
				ulong boundSize = entry.Size == 0 ? buffer.SizeInBytes - entry.Offset : entry.Size;
				if (boundSize > maxSize)
				{
					throw new ArgumentOutOfRangeException(
						nameof(descriptor),
						$"@group({descriptor.Group}) @binding({entry.Binding}): a {boundSize:N0} byte {type} binding exceeds this device's "
						+ $"{limitName} of {maxSize:N0} ('{buffer.Label}'). Bind a smaller range, or create the device with raised compute limits.");
				}
			}
		}

		private static bool TryFindType(BindGroupLayoutEntry[] layout, uint group, uint binding, out BindingType type)
		{
			foreach (BindGroupLayoutEntry layoutEntry in layout)
			{
				if (layoutEntry.Group == group && layoutEntry.Binding == binding)
				{
					type = layoutEntry.Type;
					return true;
				}
			}

			type = default;
			return false;
		}
	}
}
