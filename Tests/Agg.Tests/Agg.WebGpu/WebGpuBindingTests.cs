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
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using MatterHackers.WebGpu;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests
{
	/// <summary>
	/// Guards the generated wgpu-native binding. A struct whose C# layout drifts from the C layout does
	/// not fail loudly - it hands the driver garbage pointers and corrupts memory somewhere else - so
	/// every generated struct is measured against the size and alignment the generator computed from
	/// webgpu.yml's member types using standard C layout rules.
	/// </summary>
	public class WebGpuBindingTests
	{
		[Test]
		public async Task EveryGeneratedStructMatchesTheCLayout()
		{
			var mismatches = new List<string>();
			foreach (var layout in WGPUStructLayouts.All)
			{
				var (size, alignment) = MeasureLayout(layout.Type);
				if (size != layout.Size || alignment != layout.Alignment)
				{
					mismatches.Add($"{layout.Name}: C# is {size}/{alignment} (size/align), C is {layout.Size}/{layout.Alignment}");
				}
			}

			await Assert.That(string.Join(Environment.NewLine, mismatches)).IsEqualTo(string.Empty);
			await Assert.That(WGPUStructLayouts.All.Length).IsGreaterThan(100);
		}

		[Test]
		public async Task ChainedStructAndStringViewHaveTheirSpecifiedLayout()
		{
			// These two are hand written rather than generated, so they get their own explicit check:
			// every extensible struct starts with a WGPUChainedStruct pointer and most carry a string
			// view, which makes a mistake in either of them a mistake in nearly every descriptor.
			await Assert.That(Unsafe.SizeOf<WGPUChainedStruct>()).IsEqualTo(16);
			await Assert.That(Unsafe.SizeOf<WGPUStringView>()).IsEqualTo(16);
			await Assert.That(Unsafe.SizeOf<WGPUBool>()).IsEqualTo(4);
		}

		[Test]
		public async Task HandlesArePointerSized()
		{
			var handleTypes = typeof(WGPUDevice).Assembly.GetTypes()
				.Where(type => type.IsValueType && type.Name.StartsWith("WGPU", StringComparison.Ordinal))
				.Where(type => type.GetField("handle", BindingFlags.Instance | BindingFlags.NonPublic) != null)
				.ToList();

			await Assert.That(handleTypes.Count).IsEqualTo(23);
			foreach (var handleType in handleTypes)
			{
				var (size, alignment) = MeasureLayout(handleType);
				await Assert.That(size).IsEqualTo(IntPtr.Size);
				await Assert.That(alignment).IsEqualTo(IntPtr.Size);
			}
		}

		[Test]
		public async Task NullHandleIsTheDefaultValue()
		{
			await Assert.That(default(WGPUInstance).IsNull).IsTrue();
			await Assert.That(new WGPUInstance(new IntPtr(4)).IsNull).IsFalse();
			await Assert.That(new WGPUInstance(new IntPtr(4)) == new WGPUInstance(new IntPtr(4))).IsTrue();
		}

		/// <summary>
		/// The smoke test for the P/Invoke surface itself: it proves the WgpuNative package payload is
		/// beside the test binary, that the library name resolves on this platform, and that the Cdecl
		/// calling convention and struct return handling line up well enough to create and destroy a
		/// real wgpu instance.
		/// </summary>
		[Test]
		public async Task CreateInstanceReturnsALiveInstanceAndReleaseAcceptsIt()
		{
			WGPUInstance instance = CreateDefaultInstance();
			try
			{
				await Assert.That(instance.IsNull).IsFalse();
			}
			finally
			{
				if (!instance.IsNull)
				{
					Wgpu.wgpuInstanceRelease(instance);
				}
			}
		}

		/// <summary>A null descriptor asks wgpu for a default instance, which is all the smoke test needs.</summary>
		private static unsafe WGPUInstance CreateDefaultInstance() => Wgpu.wgpuCreateInstance(null);

		/// <summary>
		/// Hands the native library a pointer to one of our generated structs and lets it write through
		/// it. Unlike the size assertions, which check our own computed layout, this is wgpu itself
		/// filling in the struct - end to end evidence that the pointer convention is right, with no GPU
		/// adapter needed. (The reported count is only asserted to be readable: wgpu-native returns zero
		/// when the timed-wait instance feature is off, which is a valid answer, not a failure.)
		/// </summary>
		[Test]
		public async Task NativeCodeWritesThroughAGeneratedStruct()
		{
			var limits = default(WGPUInstanceLimits);
			WGPUStatus status;
			unsafe
			{
				status = Wgpu.wgpuGetInstanceLimits(&limits);
			}

			await Assert.That(status).IsEqualTo(WGPUStatus.Success);
		}

		[Test]
		public async Task NativeExtensionsResolveAndReportThePinnedVersion()
		{
			// wgpu-native encodes its version as one byte per component; the pinned build is 29.0.1.1.
			uint version = WgpuNative.wgpuGetVersion();
			await Assert.That((version >> 24) & 0xFF).IsEqualTo(29u);
		}

		private static (int Size, int Alignment) MeasureLayout(Type type)
		{
			var measure = typeof(WebGpuBindingTests)
				.GetMethod(nameof(Measure), BindingFlags.Static | BindingFlags.NonPublic)
				.MakeGenericMethod(type);
			return ((int, int))measure.Invoke(null, null);
		}

		/// <summary>
		/// Alignment is not directly observable in C#, so it is measured: putting a byte in front of the
		/// value grows the containing struct by exactly the value's alignment.
		/// </summary>
		private static (int Size, int Alignment) Measure<T>()
			where T : unmanaged
		{
			int size = Unsafe.SizeOf<T>();
			return (size, Unsafe.SizeOf<AlignmentProbe<T>>() - size);
		}

		private struct AlignmentProbe<T>
			where T : unmanaged
		{
			public byte pad;

			public T value;
		}
	}
}
