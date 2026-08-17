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
using System.Runtime.InteropServices;

namespace MatterHackers.Agg.Platform.Mac
{
	/// <summary>A Core Graphics point. Two <c>CGFloat</c>s, which are doubles on 64-bit.</summary>
	[StructLayout(LayoutKind.Sequential)]
	public struct CGPoint
	{
		public double X;
		public double Y;

		public CGPoint(double x, double y)
		{
			this.X = x;
			this.Y = y;
		}

		public override string ToString() => $"({this.X}, {this.Y})";
	}

	/// <summary>A Core Graphics size.</summary>
	[StructLayout(LayoutKind.Sequential)]
	public struct CGSize
	{
		public double Width;
		public double Height;

		public CGSize(double width, double height)
		{
			this.Width = width;
			this.Height = height;
		}

		public override string ToString() => $"{this.Width}x{this.Height}";
	}

	/// <summary>
	/// Cocoa's NSRect. Four doubles, so on arm64 this is a homogeneous float aggregate: the ABI passes it
	/// in v0-v3 and returns it in v0-v3 rather than through a hidden sret pointer. HFA classification is
	/// recursive, so the nested <see cref="CGPoint"/>/<see cref="CGSize"/> layout classifies identically to
	/// a flat four-double struct - which is why declaring it as a plain blittable struct is enough.
	/// </summary>
	[StructLayout(LayoutKind.Sequential)]
	public struct CGRect
	{
		public CGPoint Origin;
		public CGSize Size;

		public CGRect(double x, double y, double width, double height)
		{
			this.Origin = new CGPoint(x, y);
			this.Size = new CGSize(width, height);
		}

		public override string ToString() => $"[{this.Origin} {this.Size}]";
	}

	/// <summary>
	/// The whole Objective-C runtime surface PlatformMac needs, as raw P/Invoke. No NuGet package, no
	/// MonoMac/Xamarin binding, no SDL or GLFW: AppKit is reachable directly through
	/// <c>objc_msgSend</c> and that is all this uses.
	/// <para>
	/// <b>The central arm64 rule.</b> Every <c>objc_msgSend</c> call must go through a declaration whose
	/// parameter list exactly matches the selector's real signature. Apple's AAPCS64 variant passes
	/// variadic arguments differently from non-variadic ones and there is no integer/float promotion to
	/// hide behind, so reusing one catch-all <c>(IntPtr, IntPtr, IntPtr)</c> overload for a call that
	/// really takes a <c>double</c> or a <c>CGRect</c> reads the wrong registers and silently returns
	/// garbage. Hence one <c>[DllImport(EntryPoint = "objc_msgSend")]</c> per distinct signature; the C#
	/// overload set gives that for free.
	/// </para>
	/// <para>
	/// <b>Two things that do not exist on arm64:</b> <c>objc_msgSend_stret</c> and
	/// <c>objc_msgSend_fpret</c>. Struct returns and floating point returns both go through plain
	/// <c>objc_msgSend</c>.
	/// </para>
	/// <para>
	/// <b>BOOL is a signed char.</b> C#'s default <c>bool</c> marshalling in a <c>DllImport</c> is a
	/// four-byte Win32 BOOL, which is wrong here, so every Objective-C BOOL is a <see cref="byte"/>.
	/// </para>
	/// </summary>
	public static class ObjC
	{
		public const string LibObjC = "/usr/lib/libobjc.A.dylib";

		// Framework binaries. On macOS 11+ these do not exist as files on disk - they live in the dyld
		// shared cache - but dlopen still resolves the paths, so NativeLibrary.Load and DllImport both work.
		public const string AppKit = "/System/Library/Frameworks/AppKit.framework/AppKit";
		public const string QuartzCore = "/System/Library/Frameworks/QuartzCore.framework/QuartzCore";
		public const string CoreGraphics = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";
		public const string Metal = "/System/Library/Frameworks/Metal.framework/Metal";
		public const string Foundation = "/System/Library/Frameworks/Foundation.framework/Foundation";

		public const byte YES = 1;
		public const byte NO = 0;

		private static readonly object FrameworkLoadLock = new object();
		private static bool frameworksLoaded;

		[DllImport(LibObjC)]
		public static extern IntPtr objc_getClass([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

		[DllImport(LibObjC)]
		public static extern IntPtr sel_registerName([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

		[DllImport(LibObjC)]
		public static extern IntPtr class_getName(IntPtr cls);

		[DllImport(LibObjC)]
		public static extern IntPtr object_getClass(IntPtr obj);

		/// <summary>Begins defining a new class at runtime. Pair with <see cref="objc_registerClassPair"/>.</summary>
		[DllImport(LibObjC)]
		public static extern IntPtr objc_allocateClassPair(IntPtr superclass, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, nuint extraBytes);

		/// <summary>
		/// Adds a method implementation to a class being defined. <paramref name="imp"/> must be an
		/// unmanaged function pointer whose signature is <c>(id self, SEL _cmd, ...)</c>, and
		/// <paramref name="types"/> is the Objective-C type encoding of that signature (for example
		/// <c>"v@:@"</c> for a void method taking one object).
		/// </summary>
		[DllImport(LibObjC)]
		public static extern byte class_addMethod(IntPtr cls, IntPtr name, IntPtr imp, [MarshalAs(UnmanagedType.LPUTF8Str)] string types);

		[DllImport(LibObjC)]
		public static extern void objc_registerClassPair(IntPtr cls);

		// ---------------------------------------------------------------------
		// objc_msgSend, one declaration per call signature.
		// Naming convention: Send_<ret>_<args>. r = id/IntPtr, v = void, d = double, f = float,
		// B = BOOL(byte), q = NSInteger(long), Q = NSUInteger(ulong), u = unsigned short,
		// R = CGRect, P = CGPoint, S = CGSize, str = const char*.
		// ---------------------------------------------------------------------

		/// <summary>-(id)selector</summary>
		[DllImport(LibObjC, EntryPoint = "objc_msgSend")]
		public static extern IntPtr Send_r(IntPtr receiver, IntPtr selector);

		/// <summary>-(void)selector</summary>
		[DllImport(LibObjC, EntryPoint = "objc_msgSend")]
		public static extern void Send_v(IntPtr receiver, IntPtr selector);

		/// <summary>-(CGFloat)selector - returns in d0. There is no objc_msgSend_fpret on arm64.</summary>
		[DllImport(LibObjC, EntryPoint = "objc_msgSend")]
		public static extern double Send_d(IntPtr receiver, IntPtr selector);

		/// <summary>-(float)selector - the handful of AppKit APIs that return a genuine 32-bit float.</summary>
		[DllImport(LibObjC, EntryPoint = "objc_msgSend")]
		public static extern float Send_f(IntPtr receiver, IntPtr selector);

		/// <summary>-(BOOL)selector - BOOL is a signed char, so byte and never C# bool.</summary>
		[DllImport(LibObjC, EntryPoint = "objc_msgSend")]
		public static extern byte Send_B(IntPtr receiver, IntPtr selector);

		/// <summary>-(NSRect)selector - HFA return in v0-v3. There is no objc_msgSend_stret on arm64.</summary>
		[DllImport(LibObjC, EntryPoint = "objc_msgSend")]
		public static extern CGRect Send_R(IntPtr receiver, IntPtr selector);

		/// <summary>-(NSPoint)selector - 2-double HFA return in v0-v1.</summary>
		[DllImport(LibObjC, EntryPoint = "objc_msgSend")]
		public static extern CGPoint Send_P(IntPtr receiver, IntPtr selector);

		/// <summary>-(NSSize)selector - 2-double HFA return in v0-v1.</summary>
		[DllImport(LibObjC, EntryPoint = "objc_msgSend")]
		public static extern CGSize Send_S(IntPtr receiver, IntPtr selector);

		/// <summary>-(NSUInteger)selector</summary>
		[DllImport(LibObjC, EntryPoint = "objc_msgSend")]
		public static extern ulong Send_Q(IntPtr receiver, IntPtr selector);

		/// <summary>-(NSInteger)selector</summary>
		[DllImport(LibObjC, EntryPoint = "objc_msgSend")]
		public static extern long Send_q(IntPtr receiver, IntPtr selector);

		/// <summary>-(unsigned short)selector - notably -[NSEvent keyCode].</summary>
		[DllImport(LibObjC, EntryPoint = "objc_msgSend")]
		public static extern ushort Send_u(IntPtr receiver, IntPtr selector);

		/// <summary>-(void)selector:(id)</summary>
		[DllImport(LibObjC, EntryPoint = "objc_msgSend")]
		public static extern void Send_v_r(IntPtr receiver, IntPtr selector, IntPtr arg0);

		/// <summary>-(id)selector:(id)</summary>
		[DllImport(LibObjC, EntryPoint = "objc_msgSend")]
		public static extern IntPtr Send_r_r(IntPtr receiver, IntPtr selector, IntPtr arg0);

		/// <summary>-(void)selector:(id) with:(id)</summary>
		[DllImport(LibObjC, EntryPoint = "objc_msgSend")]
		public static extern void Send_v_r_r(IntPtr receiver, IntPtr selector, IntPtr arg0, IntPtr arg1);

		/// <summary>-(id)selector:(NSUInteger) - notably -[NSArray objectAtIndex:].</summary>
		[DllImport(LibObjC, EntryPoint = "objc_msgSend")]
		public static extern IntPtr Send_r_Q(IntPtr receiver, IntPtr selector, ulong arg0);

		/// <summary>-(id)selector:(const char *)</summary>
		[DllImport(LibObjC, EntryPoint = "objc_msgSend")]
		public static extern IntPtr Send_r_str(IntPtr receiver, IntPtr selector, [MarshalAs(UnmanagedType.LPUTF8Str)] string arg0);

		/// <summary>-(void)selector:(BOOL)</summary>
		[DllImport(LibObjC, EntryPoint = "objc_msgSend")]
		public static extern void Send_v_B(IntPtr receiver, IntPtr selector, byte arg0);

		/// <summary>-(BOOL)selector:(NSInteger)</summary>
		[DllImport(LibObjC, EntryPoint = "objc_msgSend")]
		public static extern byte Send_B_q(IntPtr receiver, IntPtr selector, long arg0);

		/// <summary>-(void)selector:(CGFloat)</summary>
		[DllImport(LibObjC, EntryPoint = "objc_msgSend")]
		public static extern void Send_v_d(IntPtr receiver, IntPtr selector, double arg0);

		/// <summary>-(id)selector:(NSTimeInterval)</summary>
		[DllImport(LibObjC, EntryPoint = "objc_msgSend")]
		public static extern IntPtr Send_r_d(IntPtr receiver, IntPtr selector, double arg0);

		/// <summary>-(void)selector:(NSPoint) - 2-double HFA in v0-v1. Notably -[NSWindow setFrameOrigin:].</summary>
		[DllImport(LibObjC, EntryPoint = "objc_msgSend")]
		public static extern void Send_v_P(IntPtr receiver, IntPtr selector, CGPoint arg0);

		/// <summary>-(void)selector:(NSSize)</summary>
		[DllImport(LibObjC, EntryPoint = "objc_msgSend")]
		public static extern void Send_v_S(IntPtr receiver, IntPtr selector, CGSize arg0);

		/// <summary>-(id)selector:(NSRect) - initWithFrame: and friends.</summary>
		[DllImport(LibObjC, EntryPoint = "objc_msgSend")]
		public static extern IntPtr Send_r_R(IntPtr receiver, IntPtr selector, CGRect arg0);

		/// <summary>-(void)selector:(NSRect)</summary>
		[DllImport(LibObjC, EntryPoint = "objc_msgSend")]
		public static extern void Send_v_R(IntPtr receiver, IntPtr selector, CGRect arg0);

		/// <summary>-(NSRect)selector:(NSRect) - convertRectToBacking: and friends.</summary>
		[DllImport(LibObjC, EntryPoint = "objc_msgSend")]
		public static extern CGRect Send_R_R(IntPtr receiver, IntPtr selector, CGRect arg0);

		/// <summary>-(NSPoint)selector:(NSPoint) fromView:(id) - the window-to-view coordinate conversion.</summary>
		[DllImport(LibObjC, EntryPoint = "objc_msgSend")]
		public static extern CGPoint Send_P_P_r(IntPtr receiver, IntPtr selector, CGPoint arg0, IntPtr arg1);

		/// <summary>
		/// -[NSWindow initWithContentRect:styleMask:backing:defer:]. The CGRect eats v0-v3 and the three
		/// integer arguments land in x2/x3/x4 after self/_cmd, which is exactly why the signature has to be
		/// spelled out in full.
		/// </summary>
		[DllImport(LibObjC, EntryPoint = "objc_msgSend")]
		public static extern IntPtr Send_r_R_Q_Q_B(IntPtr receiver, IntPtr selector, CGRect rect, ulong styleMask, ulong backing, byte defer);

		/// <summary>-[NSApplication nextEventMatchingMask:untilDate:inMode:dequeue:]</summary>
		[DllImport(LibObjC, EntryPoint = "objc_msgSend")]
		public static extern IntPtr Send_r_Q_r_r_B(IntPtr receiver, IntPtr selector, ulong mask, IntPtr untilDate, IntPtr mode, byte dequeue);

		/// <summary>-[NSBitmapImageRep representationUsingType:properties:]</summary>
		[DllImport(LibObjC, EntryPoint = "objc_msgSend")]
		public static extern IntPtr Send_r_Q_r(IntPtr receiver, IntPtr selector, ulong arg0, IntPtr arg1);

		/// <summary>-[NSView cacheDisplayInRect:toBitmapImageRep:]</summary>
		[DllImport(LibObjC, EntryPoint = "objc_msgSend")]
		public static extern IntPtr Send_r_R_r(IntPtr receiver, IntPtr selector, CGRect arg0, IntPtr arg1);

		/// <summary>-(BOOL)selector:(id) with:(id) - notably -[NSWorkspace selectFile:inFileViewerRootedAtPath:].</summary>
		[DllImport(LibObjC, EntryPoint = "objc_msgSend")]
		public static extern byte Send_B_r_r(IntPtr receiver, IntPtr selector, IntPtr arg0, IntPtr arg1);

		/// <summary>-[NSData writeToFile:atomically:]</summary>
		[DllImport(LibObjC, EntryPoint = "objc_msgSend")]
		public static extern byte Send_B_r_B(IntPtr receiver, IntPtr selector, IntPtr arg0, byte arg1);

		/// <summary>+[NSTimer scheduledTimerWithTimeInterval:target:selector:userInfo:repeats:]</summary>
		[DllImport(LibObjC, EntryPoint = "objc_msgSend")]
		public static extern IntPtr Send_r_d_r_r_r_B(IntPtr receiver, IntPtr selector, double interval, IntPtr target, IntPtr sel, IntPtr userInfo, byte repeats);

		// ---------------------------------------------------------------------
		// Convenience helpers
		// ---------------------------------------------------------------------

		public static IntPtr Sel(string name) => sel_registerName(name);

		/// <summary>
		/// Looks a class up by name, having first made sure the frameworks that own the classes this
		/// assembly uses are mapped into the process.
		/// </summary>
		/// <exception cref="InvalidOperationException">No such class, even after loading the frameworks.</exception>
		public static IntPtr Class(string name)
		{
			EnsureFrameworksLoaded();

			IntPtr cls = objc_getClass(name);
			if (cls == IntPtr.Zero)
			{
				throw new InvalidOperationException($"objc_getClass(\"{name}\") returned nil - is the owning framework loaded?");
			}

			return cls;
		}

		/// <summary>
		/// dlopens the frameworks whose Objective-C classes this assembly reaches for.
		/// <para>
		/// This is not optional and it is easy to think it is: a bare .NET console process links none of
		/// these, so <c>objc_getClass("NSWindow")</c> returns nil until AppKit is mapped. QuartzCore
		/// happens to be pulled in as a dependency of something else and resolves without help, which makes
		/// <c>CAMetalLayer</c> work while <c>NSWindow</c> mysteriously does not - the exact failure this
		/// prevents.
		/// </para>
		/// </summary>
		public static void EnsureFrameworksLoaded()
		{
			if (frameworksLoaded)
			{
				return;
			}

			lock (FrameworkLoadLock)
			{
				if (frameworksLoaded)
				{
					return;
				}

				foreach (string framework in new[] { Foundation, AppKit, QuartzCore, CoreGraphics, Metal })
				{
					NativeLibrary.Load(framework);
				}

				frameworksLoaded = true;
			}
		}

		private static readonly IntPtr SelAlloc = Sel("alloc");
		private static readonly IntPtr SelInit = Sel("init");
		private static readonly IntPtr SelRetain = Sel("retain");
		private static readonly IntPtr SelRelease = Sel("release");
		private static readonly IntPtr SelStringWithUTF8String = Sel("stringWithUTF8String:");
		private static readonly IntPtr SelUTF8String = Sel("UTF8String");

		public static IntPtr Alloc(IntPtr cls) => Send_r(cls, SelAlloc);

		public static IntPtr Init(IntPtr obj) => Send_r(obj, SelInit);

		public static IntPtr New(string className) => Init(Alloc(Class(className)));

		public static IntPtr Retain(IntPtr obj) => obj == IntPtr.Zero ? IntPtr.Zero : Send_r(obj, SelRetain);

		public static void Release(IntPtr obj)
		{
			if (obj != IntPtr.Zero)
			{
				Send_v(obj, SelRelease);
			}
		}

		/// <summary>Creates an autoreleased NSString from a managed string.</summary>
		public static IntPtr NSString(string value)
			=> Send_r_str(Class("NSString"), SelStringWithUTF8String, value ?? string.Empty);

		/// <summary>Reads an NSString back out as a managed string.</summary>
		public static string FromNSString(IntPtr nsString)
		{
			if (nsString == IntPtr.Zero)
			{
				return null;
			}

			IntPtr utf8 = Send_r(nsString, SelUTF8String);
			return utf8 == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(utf8);
		}

		/// <summary>The runtime class name of an object. Diagnostics only.</summary>
		public static string ClassNameOf(IntPtr obj)
		{
			if (obj == IntPtr.Zero)
			{
				return "(nil)";
			}

			IntPtr namePtr = class_getName(object_getClass(obj));
			return Marshal.PtrToStringUTF8(namePtr) ?? "(?)";
		}

		// ---------------------------------------------------------------------
		// Non-objc entry points
		// ---------------------------------------------------------------------

		[DllImport(Metal)]
		public static extern IntPtr MTLCreateSystemDefaultDevice();
	}
}
