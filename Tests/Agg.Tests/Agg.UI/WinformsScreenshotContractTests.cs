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

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// The WinForms half of <see cref="ScreenshotContractTests"/>: that the async capture actually dispatches
	/// to <see cref="WebGpuSystemWindow"/>'s override. Interface mapping is fixed at the class that declares
	/// <see cref="IPlatformWindow"/>, so if <see cref="WinformsSystemWindow"/> stopped declaring a virtual
	/// <c>CaptureScreenshotAsync</c>, the interface would silently bind to its own default implementation -
	/// the synchronous blit - and every wgpu window's async capture would go back to blocking. Nothing about
	/// that failure is visible at a call site, which is why it is pinned here.
	/// The project drops this file when WindowsBuild is false.
	/// </summary>
	public class WinformsScreenshotContractTests
	{
		[Test]
		public async Task AsyncCaptureMapsToTheWinformsVirtualNotTheInterfaceDefault()
		{
			var map = typeof(WinformsSystemWindow).GetInterfaceMap(typeof(IPlatformWindow));

			int slot = Array.FindIndex(
				map.InterfaceMethods,
				method => method.Name == nameof(IPlatformWindow.CaptureScreenshotAsync));

			await Assert.That(slot).IsGreaterThanOrEqualTo(0);

			MethodInfo target = map.TargetMethods[slot];

			// A default interface implementation would show up here as declared on IPlatformWindow.
			await Assert.That(target.DeclaringType).IsEqualTo(typeof(WinformsSystemWindow));
			await Assert.That(target.IsVirtual).IsTrue();
			await Assert.That(target.IsFinal).IsFalse();
		}

		[Test]
		public async Task WebGpuWindowOverridesTheAsyncCapture()
		{
			MethodInfo captureAsync = typeof(WebGpuSystemWindow)
				.GetMethods(BindingFlags.Public | BindingFlags.Instance)
				.Single(method => method.Name == nameof(IPlatformWindow.CaptureScreenshotAsync));

			// The override is what the interface slot above resolves to at run time; without it the wgpu
			// window would inherit the synchronous-blit base.
			await Assert.That(captureAsync.DeclaringType).IsEqualTo(typeof(WebGpuSystemWindow));
		}
	}
}
