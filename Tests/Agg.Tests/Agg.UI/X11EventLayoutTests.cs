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

using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.Platform.Linux;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// The X11 event structs are an ABI contract with a C header: the server writes the bytes, Xlib hands
	/// them over untouched, and nothing in between would notice a field in the wrong place. A layout that
	/// is off by one <c>int</c> of padding does not crash - it reads a neighbouring field, and shows up
	/// months later as "the mouse position is wrong sometimes". These sizes were taken from
	/// <c>X11/Xlib.h</c> compiled for LP64, which is what every Linux target here is.
	/// </summary>
	public class X11EventLayoutTests
	{
		/// <summary>
		/// The layer's own self-check, which covers every struct in it rather than the handful spelled out
		/// below. It throws with the offending type named, so a failure here reads as a diff against Xlib.h.
		/// </summary>
		[Test]
		public async Task DeclaredLayoutsMatchTheXlibAbi()
		{
			Xlib.VerifyLayouts();

			// Reaching here at all is the assertion; this keeps the test honest about being a test.
			await Assert.That(Unsafe.SizeOf<XEvent>()).IsEqualTo(192);
		}

		/// <summary>
		/// The arm sizes, asserted independently of <c>VerifyLayouts</c> so that a number mistyped into both
		/// places still has one witness. They are what the natural alignment of each field list produces on
		/// LP64, which is the padding rule the System V ABI and C# sequential layout happen to share.
		/// </summary>
		[Test]
		public async Task InputEventArmsMatchXlib()
		{
			await Assert.That(Unsafe.SizeOf<XKeyEvent>()).IsEqualTo(96);
			await Assert.That(Unsafe.SizeOf<XButtonEvent>()).IsEqualTo(96);
			await Assert.That(Unsafe.SizeOf<XMotionEvent>()).IsEqualTo(96);
			await Assert.That(Unsafe.SizeOf<XCrossingEvent>()).IsEqualTo(104);
		}

		[Test]
		public async Task WindowEventArmsMatchXlib()
		{
			await Assert.That(Unsafe.SizeOf<XConfigureEvent>()).IsEqualTo(88);
			await Assert.That(Unsafe.SizeOf<XClientMessageEvent>()).IsEqualTo(96);
			await Assert.That(Unsafe.SizeOf<XFocusChangeEvent>()).IsEqualTo(48);
			await Assert.That(Unsafe.SizeOf<XExposeEvent>()).IsEqualTo(64);
			await Assert.That(Unsafe.SizeOf<XDestroyWindowEvent>()).IsEqualTo(48);
		}

		[Test]
		public async Task SelectionAndPropertyArmsMatchXlib()
		{
			await Assert.That(Unsafe.SizeOf<XSelectionRequestEvent>()).IsEqualTo(80);
			await Assert.That(Unsafe.SizeOf<XSelectionEvent>()).IsEqualTo(72);
			await Assert.That(Unsafe.SizeOf<XPropertyEvent>()).IsEqualTo(64);
			await Assert.That(Unsafe.SizeOf<XMappingEvent>()).IsEqualTo(56);
		}

		/// <summary>
		/// The structs that do not come off the event queue: the ones passed <em>into</em> Xlib, plus
		/// <c>XErrorEvent</c>, which despite its name arrives by callback instead. A wrong size in a request
		/// struct is worse than a wrong read - the server acts on whatever the misaligned fields happened to
		/// contain.
		/// </summary>
		[Test]
		public async Task RequestStructsMatchXlib()
		{
			await Assert.That(Unsafe.SizeOf<XErrorEvent>()).IsEqualTo(40);
			await Assert.That(Unsafe.SizeOf<XSizeHints>()).IsEqualTo(80);
			await Assert.That(Unsafe.SizeOf<XSetWindowAttributes>()).IsEqualTo(112);
			await Assert.That(Unsafe.SizeOf<XWindowAttributes>()).IsEqualTo(136);
			await Assert.That(Unsafe.SizeOf<PollFd>()).IsEqualTo(8);
		}

		/// <summary>
		/// <c>XEvent.As&lt;T&gt;()</c> has to be a view and not a copy, or the host would read one event and
		/// dispatch another. This writes through one arm and reads back through both the union's own
		/// <c>Type</c> and a freshly taken arm.
		/// </summary>
		[Test]
		public async Task ArmsAliasTheUnionsStorage()
		{
			(int type, int width, int height) probe = WriteAndReadBackThroughUnion();

			await Assert.That(probe.type).IsEqualTo(X11.ConfigureNotify);
			await Assert.That(probe.width).IsEqualTo(1234);
			await Assert.That(probe.height).IsEqualTo(768);
		}

		/// <summary>
		/// The provider is constructed from wherever <c>AggContext</c> is first touched, which under a test
		/// runner or in CI is a process with no <c>DISPLAY</c> at all. That path must produce usable numbers
		/// rather than zeros - every caller of <c>DesktopSize</c> is sizing or centring a window. The
		/// assertions below hold on a real desktop too, so this is not a headless-only test.
		/// </summary>
		[Test]
		public async Task InformationProviderIsUsableWithoutADisplay()
		{
			var provider = new LinuxInformationProvider();

			await Assert.That(provider.OperatingSystem).IsEqualTo(OSType.X11);
			await Assert.That(provider.DesktopSize.x).IsGreaterThan(0);
			await Assert.That(provider.DesktopSize.y).IsGreaterThan(0);
			await Assert.That(provider.DisplayScale).IsGreaterThan(0.0);

			// /proc/meminfo exists on every Linux, container included, and this test only compiles there.
			await Assert.That(provider.PhysicalMemory).IsGreaterThan(0L);
		}

		/// <summary>
		/// Kept out of the test method because a <c>ref</c> local cannot live across an <c>await</c>.
		/// </summary>
		private static (int Type, int Width, int Height) WriteAndReadBackThroughUnion()
		{
			XEvent raw = default;

			ref XConfigureEvent configure = ref raw.As<XConfigureEvent>();
			configure.Type = X11.ConfigureNotify;
			configure.Width = 1234;
			configure.Height = 768;

			ref XConfigureEvent readBack = ref raw.As<XConfigureEvent>();
			return (raw.Type, readBack.Width, readBack.Height);
		}
	}
}
