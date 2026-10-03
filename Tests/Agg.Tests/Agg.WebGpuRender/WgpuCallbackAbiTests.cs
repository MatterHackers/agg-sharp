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
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using MatterHackers.Agg.Tests.TestingInfrastructure;
using MatterHackers.RenderCore;
using MatterHackers.RenderCore.Testing;
using MatterHackers.WebGpu;
using MatterHackers.WebGpuRender;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests
{
	/// <summary>
	/// Pins the browser shape of the wgpu callbacks. Under the wasm32 C ABI a multi-field struct
	/// argument (every callback's <c>WGPUStringView message</c>) arrives as a pointer, and mono-wasm's
	/// reverse thunk hands the managed method the address of that pointer - so a browser callback that
	/// declares the view by value reads garbage (it made every browser GPU error print as
	/// "Validation: " plus noise). The desktop cannot run the wasm ABI, so these tests pin the declared
	/// signatures, the platform selector and the pointer decode, and reads a real validation error through
	/// the desktop shape; only a browser run proves the browser message reads end to end.
	/// </summary>
	[NotInParallel]
	public class WgpuCallbackAbiTests
	{
		/// <summary>
		/// The only callbacks allowed to take a multi-field struct by value, keyed "Type.Method". Each one
		/// is desktop-only, so the wasm32 ABI never reaches it. Anything not on this list fails the test,
		/// whatever it is named: a new callback has to take its struct by pointer or be argued onto the list.
		/// </summary>
		private static readonly string[] DesktopOnlyByValueCallbacks =
		{
			// Registered only by the synchronous constructor, which throws PlatformNotSupportedException
			// when OperatingSystem.IsBrowser(); the browser goes through CreateAsync and
			// OnAdapterRequestedSpontaneous.
			"WebGpuRenderDevice.OnAdapterRequested",

			// Same constructor fence; the browser's twin is OnDeviceRequestedSpontaneous.
			"WebGpuRenderDevice.OnDeviceRequested",

			// Registered only by MapAndCopy, whose callers route the browser to MapAndCopyBrowserAsync
			// (and OnBufferMappedSpontaneous) before it is reached.
			"WebGpuRenderDevice.OnBufferMapped",

			// Picked by WgpuDeviceCallbacks.UncapturedError(isBrowser) only when isBrowser is false;
			// SelectorsHandTheBrowserThePointerCallbacks pins that choice.
			"WgpuDeviceCallbacks.OnUncapturedError",

			// Picked by WgpuDeviceCallbacks.DeviceLost(isBrowser) only when isBrowser is false; pinned the same way.
			"WgpuDeviceCallbacks.OnDeviceLost",
		};

		[Test]
		public async Task OnlyDesktopCallbacksTakeMultiFieldStructsByValue()
		{
			var assemblies = new[] { typeof(WebGpuRenderDevice).Assembly, typeof(WGPUStringView).Assembly };
			var callbacks = assemblies
				.SelectMany(assembly => assembly.GetTypes())
				.SelectMany(type => type.GetMethods(BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
				.Where(method => method.GetCustomAttribute<UnmanagedCallersOnlyAttribute>() != null)
				.ToList();

			// Adapter, device and buffer-map requests plus uncaptured error and device lost, in both shapes.
			await Assert.That(callbacks.Count).IsGreaterThanOrEqualTo(10);

			var byValue = callbacks
				.Where(method => method.GetParameters().Any(parameter => ScalarLeafCount(parameter.ParameterType) > 1))
				.Select(method => $"{method.DeclaringType.Name}.{method.Name}")
				.ToList();

			// Both directions: a new by-value callback fails, and so does a stale allow-list entry, so the
			// list cannot outlive the fix that should have shrunk it.
			string unexpected = string.Join(", ", byValue.Except(DesktopOnlyByValueCallbacks));
			string missing = string.Join(", ", DesktopOnlyByValueCallbacks.Except(byValue));
			await Assert.That(unexpected)
				.IsEqualTo(string.Empty)
				.Because("these callbacks take a multi-field struct by value but are not on the desktop-only list; take it by pointer");
			await Assert.That(missing)
				.IsEqualTo(string.Empty)
				.Because("these desktop-only entries no longer take a multi-field struct by value; remove them from the list");
		}

		[Test]
		public async Task SelectorsHandTheBrowserThePointerCallbacks()
		{
			Type callbacks = typeof(WebGpuRenderDevice).Assembly.GetType("MatterHackers.WebGpuRender.WgpuDeviceCallbacks", throwOnError: true);

			foreach (var (selector, desktopName, browserName) in new[]
			{
				("UncapturedError", "OnUncapturedError", "OnUncapturedErrorBrowser"),
				("DeviceLost", "OnDeviceLost", "OnDeviceLostBrowser"),
			})
			{
				MethodInfo select = callbacks.GetMethod(selector, BindingFlags.Static | BindingFlags.Public);
				nint browser = SelectedPointer(select, true);
				nint desktop = SelectedPointer(select, false);

				await Assert.That(browser).IsNotEqualTo(desktop).Because(selector);
				await Assert.That(browser).IsEqualTo(EntryPoint(callbacks, browserName)).Because(selector);
				await Assert.That(desktop).IsEqualTo(EntryPoint(callbacks, desktopName)).Because(selector);

				MethodInfo browserMethod = callbacks.GetMethod(browserName, BindingFlags.Static | BindingFlags.NonPublic);
				await Assert.That(browserMethod.GetParameters().Any(parameter => parameter.ParameterType == typeof(WGPUStringView).MakePointerType()))
					.IsTrue()
					.Because($"{browserName} reads its message through a pointer");
			}
		}

		[Test]
		public async Task ADesktopValidationErrorArrivesReadable()
		{
			// End to end through the by-value desktop callback: a real validation error, decoded from the
			// view wgpu-native passes. 20 mip levels on a 4x4 texture is rejected at creation and reported
			// out of band - which CreateTexture now notices and throws over (its message is the same text,
			// and is not what this test is about); the error the callback recorded is what is read.
			using (GpuTestGate.Acquire(nameof(WgpuCallbackAbiTests)))
			using (var device = new WebGpuRenderDevice(false, TestRenderBackend.Native, nameof(WgpuCallbackAbiTests)))
			{
				await Assert.That(device.LastUncapturedError).IsNull();

				await Assert.That(() => device.CreateTexture(new TextureDescriptor(4, 4, TextureFormat.Rgba8Unorm, TextureUsage.TextureBinding, 20, 1, "tooManyMips")))
					.Throws<InvalidOperationException>();

				string error = device.LastUncapturedError;
				await Assert.That(error).IsNotNull();
				await Assert.That(error).StartsWith("Validation: ");
				await Assert.That(error).Contains("tooManyMips");
				await Assert.That(error).Contains("mip", StringComparison.OrdinalIgnoreCase);
				await Assert.That(error.Contains('\uFFFD') || error.Any(c => char.IsControl(c) && c != '\n' && c != '\t'))
					.IsFalse()
					.Because(error);
			}
		}

		/// <summary>
		/// How many scalars a parameter of this type occupies, counted through nested structs, so a
		/// one-field wrapper around a multi-field struct still counts as multi-field. Pointers, primitives
		/// and enums are one scalar each.
		/// </summary>
		private static int ScalarLeafCount(Type type)
		{
			if (!type.IsValueType || type.IsPrimitive || type.IsEnum || type.IsPointer || type.IsFunctionPointer)
			{
				return 1;
			}

			return type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
				.Sum(field => ScalarLeafCount(field.FieldType));
		}

		private static nint SelectedPointer(MethodInfo selector, bool isBrowser)
		{
			object result = selector.Invoke(null, new object[] { isBrowser });
			return result switch
			{
				IntPtr pointer => pointer,
				Pointer boxed => BoxedAddress(boxed),
				_ => throw new InvalidOperationException($"{selector.Name} returned {result?.GetType().Name ?? "null"}"),
			};
		}

		private static unsafe nint BoxedAddress(Pointer boxed) => (nint)Pointer.Unbox(boxed);

		/// <summary>The native-callable entry of an [UnmanagedCallersOnly] method, which is what &amp;Method yields.</summary>
		private static nint EntryPoint(Type type, string name)
			=> type.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic).MethodHandle.GetFunctionPointer();

		[Test]
		public async Task StringViewPointerDecodesEveryForm()
		{
			// C# forbids await in an unsafe context, so the pointer work happens in DecodeEveryForm.
			string[] decoded = DecodeEveryForm("Validation: binding size");

			await Assert.That(decoded[0]).IsEqualTo(string.Empty);
			await Assert.That(decoded[1]).IsEqualTo(string.Empty);
			await Assert.That(decoded[2]).IsEqualTo("Validation: binding size");
			await Assert.That(decoded[3]).IsEqualTo("Validation: binding size");
		}

		/// <summary>Null pointer, the "no string" view, a counted view and a null-terminated view.</summary>
		private static unsafe string[] DecodeEveryForm(string text)
		{
			var results = new string[4];
			results[0] = WgpuStrings.ToManaged((WGPUStringView*)null);

			WGPUStringView noString = WgpuStrings.Null;
			results[1] = WgpuStrings.ToManaged(&noString);

			// Trailing bytes past the terminator prove the counted form stops at its length.
			byte[] utf8 = Encoding.UTF8.GetBytes(text + "\0trailing");
			fixed (byte* data = utf8)
			{
				var counted = new WGPUStringView { data = data, length = (nuint)Encoding.UTF8.GetByteCount(text) };
				results[2] = WgpuStrings.ToManaged(&counted);

				var terminated = new WGPUStringView { data = data, length = WGPUConstants.WGPU_STRLEN };
				results[3] = WgpuStrings.ToManaged(&terminated);
			}

			return results;
		}
	}
}
