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
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace MatterHackers.Agg.Platform.Linux
{
	// ---------------------------------------------------------------------------------------------
	// The event structs.
	//
	// Every one of these mirrors a struct in X11/Xlib.h field for field, and the field order IS the
	// ABI - Xlib hands back raw memory the server wrote and there is no marshalling layer to catch a
	// mistake. The types below are the LP64 (Linux x86-64 / aarch64) mapping:
	//
	//   int, Bool, Status  -> int    (4 bytes; Bool is an int, not a C99 _Bool and not a byte)
	//   unsigned long      -> ulong  (8 bytes on LP64 - NOT uint, which is the usual way to get this wrong)
	//   Window, Atom, Time,
	//   Colormap, Cursor,
	//   Drawable, XID      -> ulong  (all are `unsigned long` typedefs)
	//   Display*, Visual*,
	//   Screen*            -> IntPtr
	//
	// C#'s sequential layout uses the same natural alignment the System V ABI does, so the padding
	// falls out on its own - VerifyLayouts() is what proves that claim rather than assuming it.
	// ---------------------------------------------------------------------------------------------

	/// <summary>
	/// Xlib's <c>XEvent</c> union. The union's largest member is <c>long pad[24]</c>, so the whole thing
	/// is 24 longs wide no matter which arm is live, and every arm begins with the same <c>int type</c>.
	/// Read <see cref="Type"/> first, then take the matching arm with <see cref="As{T}"/>.
	/// </summary>
	[StructLayout(LayoutKind.Sequential)]
	internal unsafe struct XEvent
	{
		/// <summary>The union's storage, sized by Xlib's own <c>long pad[24]</c>.</summary>
		public fixed long Payload[24];

		/// <summary>
		/// The event type - one of the <c>X11.KeyPress</c> family. It aliases the first four bytes of the
		/// union because every arm starts with <c>int type</c>. Read through <see cref="Unsafe"/> rather
		/// than off <see cref="Payload"/>[0] so it does not quietly depend on byte order.
		/// </summary>
		public int Type => Unsafe.As<XEvent, int>(ref this);

		/// <summary>
		/// Reinterprets the union as one of its arms. No copy is made, so writing through the returned
		/// reference writes the event - which is what <c>XSendEvent</c> round-trips need.
		/// </summary>
		/// <typeparam name="T">The arm to view, e.g. <see cref="XKeyEvent"/>.</typeparam>
		[UnscopedRef]
		public ref T As<T>()
			where T : unmanaged
		{
			return ref Unsafe.As<XEvent, T>(ref this);
		}
	}

	/// <summary>Xlib's <c>XKeyEvent</c> (<c>KeyPress</c> / <c>KeyRelease</c>).</summary>
	[StructLayout(LayoutKind.Sequential)]
	internal struct XKeyEvent
	{
		public int Type;
		public ulong Serial;

		/// <summary>Bool: non-zero when this arrived through <c>XSendEvent</c> rather than real hardware.</summary>
		public int SendEvent;

		public IntPtr Display;
		public ulong Window;
		public ulong Root;
		public ulong Subwindow;
		public ulong Time;

		/// <summary>Pointer position in the event window, X11's top-left origin (agg's is bottom-left).</summary>
		public int X;
		public int Y;

		public int XRoot;
		public int YRoot;

		/// <summary>Modifier and button mask as it was <em>before</em> this event.</summary>
		public uint State;

		/// <summary>The hardware key position. Meaningless on its own - resolve it to a keysym.</summary>
		public uint Keycode;

		/// <summary>Bool.</summary>
		public int SameScreen;
	}

	/// <summary>Xlib's <c>XButtonEvent</c> (<c>ButtonPress</c> / <c>ButtonRelease</c>).</summary>
	[StructLayout(LayoutKind.Sequential)]
	internal struct XButtonEvent
	{
		public int Type;
		public ulong Serial;
		public int SendEvent;
		public IntPtr Display;
		public ulong Window;
		public ulong Root;
		public ulong Subwindow;
		public ulong Time;
		public int X;
		public int Y;
		public int XRoot;
		public int YRoot;
		public uint State;

		/// <summary>One of <c>X11.Button1</c>..<c>Button7</c>; 4-7 are the wheel.</summary>
		public uint Button;

		public int SameScreen;
	}

	/// <summary>Xlib's <c>XMotionEvent</c> (<c>MotionNotify</c>).</summary>
	[StructLayout(LayoutKind.Sequential)]
	internal struct XMotionEvent
	{
		public int Type;
		public ulong Serial;
		public int SendEvent;
		public IntPtr Display;
		public ulong Window;
		public ulong Root;
		public ulong Subwindow;
		public ulong Time;
		public int X;
		public int Y;
		public int XRoot;
		public int YRoot;
		public uint State;

		/// <summary>A <c>char</c>, not a Bool: set only when PointerMotionHintMask compressed the stream.</summary>
		public byte IsHint;

		public int SameScreen;
	}

	/// <summary>Xlib's <c>XCrossingEvent</c> (<c>EnterNotify</c> / <c>LeaveNotify</c>).</summary>
	[StructLayout(LayoutKind.Sequential)]
	internal struct XCrossingEvent
	{
		public int Type;
		public ulong Serial;
		public int SendEvent;
		public IntPtr Display;
		public ulong Window;
		public ulong Root;
		public ulong Subwindow;
		public ulong Time;
		public int X;
		public int Y;
		public int XRoot;
		public int YRoot;

		/// <summary>NotifyNormal / NotifyGrab / NotifyUngrab. A grab produces crossings the pointer never made.</summary>
		public int Mode;

		public int Detail;
		public int SameScreen;
		public int Focus;
		public uint State;
	}

	/// <summary>Xlib's <c>XFocusChangeEvent</c> (<c>FocusIn</c> / <c>FocusOut</c>).</summary>
	[StructLayout(LayoutKind.Sequential)]
	internal struct XFocusChangeEvent
	{
		public int Type;
		public ulong Serial;
		public int SendEvent;
		public IntPtr Display;
		public ulong Window;
		public int Mode;
		public int Detail;
	}

	/// <summary>Xlib's <c>XExposeEvent</c> (<c>Expose</c>).</summary>
	[StructLayout(LayoutKind.Sequential)]
	internal struct XExposeEvent
	{
		public int Type;
		public ulong Serial;
		public int SendEvent;
		public IntPtr Display;
		public ulong Window;
		public int X;
		public int Y;
		public int Width;
		public int Height;

		/// <summary>How many more Expose events for this same damage are still queued behind this one.</summary>
		public int Count;
	}

	/// <summary>Xlib's <c>XConfigureEvent</c> (<c>ConfigureNotify</c>) - move, resize and restack.</summary>
	[StructLayout(LayoutKind.Sequential)]
	internal struct XConfigureEvent
	{
		public int Type;
		public ulong Serial;
		public int SendEvent;
		public IntPtr Display;

		/// <summary>The window the event was selected on, which for StructureNotify is the window itself.</summary>
		public ulong Event;

		public ulong Window;
		public int X;
		public int Y;
		public int Width;
		public int Height;
		public int BorderWidth;
		public ulong Above;

		/// <summary>Bool.</summary>
		public int OverrideRedirect;
	}

	/// <summary>
	/// Xlib's <c>XClientMessageEvent</c> (<c>ClientMessage</c>). This is how <c>WM_DELETE_WINDOW</c>
	/// arrives, so it is the close button.
	/// </summary>
	[StructLayout(LayoutKind.Sequential)]
	internal unsafe struct XClientMessageEvent
	{
		public int Type;
		public ulong Serial;
		public int SendEvent;
		public IntPtr Display;
		public ulong Window;
		public ulong MessageType;

		/// <summary>8, 16 or 32 - the element width the sender used. WM protocol messages are always 32.</summary>
		public int Format;

		/// <summary>
		/// The payload. Xlib's union is <c>char b[20] / short s[10] / long l[5]</c>; the long arm is the
		/// widest, so it is the one that fixes the size. Note that a "format 32" message travels in
		/// <em>five longs</em> on LP64 even though the protocol only carries 32 bits per slot.
		/// </summary>
		public fixed long Data[5];
	}

	/// <summary>Xlib's <c>XSelectionRequestEvent</c> - another client asking for our clipboard.</summary>
	[StructLayout(LayoutKind.Sequential)]
	internal struct XSelectionRequestEvent
	{
		public int Type;
		public ulong Serial;
		public int SendEvent;
		public IntPtr Display;
		public ulong Owner;
		public ulong Requestor;
		public ulong Selection;
		public ulong Target;

		/// <summary>Where to put the answer on the requestor's window. <c>None</c> means an obsolete client.</summary>
		public ulong Property;

		public ulong Time;
	}

	/// <summary>Xlib's <c>XSelectionEvent</c> - the answer to our own <c>XConvertSelection</c>.</summary>
	[StructLayout(LayoutKind.Sequential)]
	internal struct XSelectionEvent
	{
		public int Type;
		public ulong Serial;
		public int SendEvent;
		public IntPtr Display;
		public ulong Requestor;
		public ulong Selection;
		public ulong Target;

		/// <summary><c>None</c> when the owner refused the conversion.</summary>
		public ulong Property;

		public ulong Time;
	}

	/// <summary>
	/// Xlib's <c>XSelectionClearEvent</c> - somebody else claimed a selection we owned. This is the only
	/// notice an owner gets, and the moment it must stop answering for that selection.
	/// </summary>
	[StructLayout(LayoutKind.Sequential)]
	internal struct XSelectionClearEvent
	{
		public int Type;
		public ulong Serial;
		public int SendEvent;
		public IntPtr Display;
		public ulong Window;
		public ulong Selection;
		public ulong Time;
	}

	/// <summary>Xlib's <c>XPropertyEvent</c> (<c>PropertyNotify</c>).</summary>
	[StructLayout(LayoutKind.Sequential)]
	internal struct XPropertyEvent
	{
		public int Type;
		public ulong Serial;
		public int SendEvent;
		public IntPtr Display;
		public ulong Window;
		public ulong Atom;
		public ulong Time;

		/// <summary>PropertyNewValue (0) or PropertyDelete (1).</summary>
		public int State;
	}

	/// <summary>Xlib's <c>XDestroyWindowEvent</c> (<c>DestroyNotify</c>).</summary>
	[StructLayout(LayoutKind.Sequential)]
	internal struct XDestroyWindowEvent
	{
		public int Type;
		public ulong Serial;
		public int SendEvent;
		public IntPtr Display;
		public ulong Event;
		public ulong Window;
	}

	/// <summary>
	/// Xlib's <c>XMappingEvent</c> (<c>MappingNotify</c>) - the keyboard layout changed under us. Xlib's
	/// cached keymap is stale until <c>XRefreshKeyboardMapping</c> is called with this.
	/// </summary>
	[StructLayout(LayoutKind.Sequential)]
	internal struct XMappingEvent
	{
		public int Type;
		public ulong Serial;
		public int SendEvent;
		public IntPtr Display;
		public ulong Window;

		/// <summary>MappingModifier (0), MappingKeyboard (1) or MappingPointer (2).</summary>
		public int Request;

		public int FirstKeycode;
		public int Count;
	}

	/// <summary>
	/// Xlib's <c>XSizeHints</c> (Xutil.h) - what the window manager is told about acceptable geometry.
	/// Only the fields named in <see cref="Flags"/> are read; the rest are ignored no matter what they hold.
	/// </summary>
	[StructLayout(LayoutKind.Sequential)]
	internal struct XSizeHints
	{
		/// <summary>A bitwise OR of <c>X11.PMinSize</c> and friends.</summary>
		public long Flags;

		public int X;
		public int Y;
		public int Width;
		public int Height;
		public int MinWidth;
		public int MinHeight;
		public int MaxWidth;
		public int MaxHeight;
		public int WidthInc;
		public int HeightInc;
		public int MinAspectX;
		public int MinAspectY;
		public int MaxAspectX;
		public int MaxAspectY;
		public int BaseWidth;
		public int BaseHeight;
		public int WinGravity;
	}

	/// <summary>Xlib's <c>XSetWindowAttributes</c> - the creation-time and change-time window fields.</summary>
	[StructLayout(LayoutKind.Sequential)]
	internal struct XSetWindowAttributes
	{
		public ulong BackgroundPixmap;
		public ulong BackgroundPixel;
		public ulong BorderPixmap;
		public ulong BorderPixel;
		public int BitGravity;
		public int WinGravity;
		public int BackingStore;
		public ulong BackingPlanes;
		public ulong BackingPixel;

		/// <summary>Bool.</summary>
		public int SaveUnder;

		public long EventMask;
		public long DoNotPropagateMask;

		/// <summary>Bool: true asks the window manager to leave this window entirely alone.</summary>
		public int OverrideRedirect;

		public ulong Colormap;
		public ulong Cursor;
	}

	/// <summary>Xlib's <c>XWindowAttributes</c> - what <c>XGetWindowAttributes</c> fills in.</summary>
	[StructLayout(LayoutKind.Sequential)]
	internal struct XWindowAttributes
	{
		/// <summary>Position relative to the <em>parent</em>, which under a reparenting window manager is
		/// the frame and not the root. Use <c>XTranslateCoordinates</c> for a screen position.</summary>
		public int X;
		public int Y;

		public int Width;
		public int Height;
		public int BorderWidth;
		public int Depth;
		public IntPtr Visual;
		public ulong Root;

		/// <summary>Xlib calls this <c>class</c>: InputOutput or InputOnly.</summary>
		public int Class;

		public int BitGravity;
		public int WinGravity;
		public int BackingStore;
		public ulong BackingPlanes;
		public ulong BackingPixel;
		public int SaveUnder;
		public ulong Colormap;
		public int MapInstalled;

		/// <summary>IsUnmapped (0), IsUnviewable (1) or IsViewable (2).</summary>
		public int MapState;

		public long AllEventMasks;
		public long YourEventMask;
		public long DoNotPropagateMask;
		public int OverrideRedirect;
		public IntPtr Screen;
	}

	/// <summary>
	/// Xlib's <c>XErrorEvent</c>. Despite the name it is not an arm of <see cref="XEvent"/> and never
	/// reaches the event queue: a protocol error is delivered by <em>calling</em> the installed error
	/// handler. That matters because the default handler prints to stderr and then calls <c>exit</c>, so an
	/// X11 host that installs nothing dies on the first BadWindow instead of reporting it - see
	/// <see cref="Xlib.XSetErrorHandler"/>.
	/// </summary>
	[StructLayout(LayoutKind.Sequential)]
	internal struct XErrorEvent
	{
		public int Type;
		public IntPtr Display;

		/// <summary>The XID the failed request named, when the error carries one.</summary>
		public ulong ResourceId;

		/// <summary>Serial of the failed request, which is how it is matched back to the call that made it.</summary>
		public ulong Serial;

		/// <summary>BadWindow, BadMatch and friends. A <c>byte</c>, not an <c>int</c>.</summary>
		public byte ErrorCode;

		/// <summary>Major opcode of the failed request.</summary>
		public byte RequestCode;

		/// <summary>Minor opcode, which is only meaningful for an extension's request.</summary>
		public byte MinorCode;
	}

	/// <summary>libc's <c>struct pollfd</c>, for waiting on the X connection without spinning.</summary>
	[StructLayout(LayoutKind.Sequential)]
	internal struct PollFd
	{
		public int Fd;
		public short Events;
		public short Revents;
	}

	/// <summary>
	/// The raw Xlib entry points PlatformLinux is built on, plus the one libc call needed to sleep on the
	/// X connection. This is the Linux counterpart of PlatformMac's <c>ObjC</c>: a flat P/Invoke surface
	/// with no policy in it, so that the host above can be read as X11 protocol rather than as marshalling.
	/// <para>
	/// <b>Why the versioned soname.</b> The import is <c>libX11.so.6</c>, not <c>libX11</c>: the unversioned
	/// <c>libX11.so</c> is a linker symlink that ships in the <c>-dev</c> package, and a machine that can run
	/// an X client is not a machine that has development headers installed. .NET's probing would find
	/// neither and the first call would throw <see cref="DllNotFoundException"/> on an otherwise perfectly
	/// good desktop.
	/// </para>
	/// <para>
	/// <b>Threading.</b> Xlib is only thread-safe after <c>XInitThreads</c>, which is not called here.
	/// Everything in this class must therefore be reached from the one thread that owns the display - which
	/// costs nothing, because the host pumps the connection from a single loop anyway. Note this is a
	/// different rule from AppKit's: it is "one thread", not "the main thread" (see
	/// <c>MainThreadDispatcher.MainThreadRequired</c>, which is false off macOS for exactly this reason).
	/// </para>
	/// </summary>
	internal static unsafe class Xlib
	{
		/// <summary>See the class remarks: the versioned soname is the one that exists at runtime.</summary>
		private const string X11Lib = "libX11.so.6";

		private const string LibC = "libc";

		/// <summary>poll(2)'s "there is data to read" bit.</summary>
		public const short POLLIN = 0x001;

		/// <summary>
		/// <c>setlocale</c>'s character-classification category - the one that decides what encoding
		/// <c>XLookupString</c> writes. These two numbers are glibc's; the C standard fixes the names but
		/// not the values, so they would need checking against another libc.
		/// </summary>
		public const int LC_CTYPE = 0;

		/// <summary>Every category at once. What an application normally sets.</summary>
		public const int LC_ALL = 6;

		// ---- Display ---------------------------------------------------------------------------------

		/// <summary>Opens a connection. <paramref name="displayName"/> null means "$DISPLAY".</summary>
		/// <returns>The <c>Display*</c>, or <see cref="IntPtr.Zero"/> when there is no X server to talk to.</returns>
		[DllImport(X11Lib)]
		public static extern IntPtr XOpenDisplay([MarshalAs(UnmanagedType.LPUTF8Str)] string displayName);

		[DllImport(X11Lib)]
		public static extern int XCloseDisplay(IntPtr display);

		[DllImport(X11Lib)]
		public static extern int XDefaultScreen(IntPtr display);

		[DllImport(X11Lib)]
		public static extern ulong XRootWindow(IntPtr display, int screenNumber);

		[DllImport(X11Lib)]
		public static extern IntPtr XDefaultVisual(IntPtr display, int screenNumber);

		[DllImport(X11Lib)]
		public static extern int XDefaultDepth(IntPtr display, int screenNumber);

		[DllImport(X11Lib)]
		public static extern ulong XBlackPixel(IntPtr display, int screenNumber);

		/// <summary>Screen size in pixels. This is the whole screen, not the work area - X11 has no notion
		/// of a work area at all; that is a window-manager convention carried in <c>_NET_WORKAREA</c>.</summary>
		[DllImport(X11Lib)]
		public static extern int XDisplayWidth(IntPtr display, int screenNumber);

		[DllImport(X11Lib)]
		public static extern int XDisplayHeight(IntPtr display, int screenNumber);

		/// <summary>Screen size in millimetres, as reported by the server. Frequently a fiction (many
		/// drivers report a made-up 1024x768-ish default), so it is a last-resort DPI source at best.</summary>
		[DllImport(X11Lib)]
		public static extern int XDisplayWidthMM(IntPtr display, int screenNumber);

		[DllImport(X11Lib)]
		public static extern int XDisplayHeightMM(IntPtr display, int screenNumber);

		/// <summary>The socket behind the display, for <c>poll</c>/<c>select</c>. See <see cref="Poll"/>.</summary>
		[DllImport(X11Lib)]
		public static extern int XConnectionNumber(IntPtr display);

		// ---- Error handling --------------------------------------------------------------------------
		// Xlib does not return protocol errors from the call that caused them - requests are asynchronous,
		// so by the time the server objects the call has long since returned. Errors arrive by callback
		// instead, and the two defaults are both fatal in practice: the protocol-error default prints and
		// exits for some codes, and the I/O-error default always exits. Installing replacements is step 3's
		// job; these are the bindings it needs.

		/// <summary>
		/// A protocol error handler. Must not throw: it is called from native code, and an exception
		/// crossing that boundary tears the process down with no diagnostic - the same rule the mac host's
		/// <c>[UnmanagedCallersOnly]</c> IMPs follow.
		/// </summary>
		/// <returns>Ignored by Xlib; return 0.</returns>
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		public delegate int XErrorHandler(IntPtr display, XErrorEvent* error);

		/// <summary>
		/// A fatal I/O error handler - the connection to the server is gone and no further request can be
		/// made on it. Xlib requires this one <b>not to return</b>; if it does, Xlib calls <c>exit</c>
		/// itself. The same no-throw rule as <see cref="XErrorHandler"/> applies.
		/// </summary>
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		public delegate int XIOErrorHandler(IntPtr display);

		/// <summary>
		/// Installs a protocol-error handler. The caller must keep the delegate alive for as long as the
		/// handler is installed - nothing on the native side roots it, and a collected delegate turns the
		/// next BadWindow into a jump to freed memory.
		/// </summary>
		/// <returns>
		/// The previous handler as a raw function pointer, not a delegate: what is usually there is Xlib's
		/// own C default, which has no managed identity to marshal back into. Keep it if the new handler
		/// wants to chain, and treat it as opaque otherwise.
		/// </returns>
		[DllImport(X11Lib)]
		public static extern IntPtr XSetErrorHandler(XErrorHandler handler);

		/// <summary>Installs a fatal I/O error handler. Same lifetime and return-value notes as
		/// <see cref="XSetErrorHandler"/>.</summary>
		[DllImport(X11Lib)]
		public static extern IntPtr XSetIOErrorHandler(XIOErrorHandler handler);

		// ---- Locale ----------------------------------------------------------------------------------
		// XLookupString hands back bytes in the *current locale's* encoding, and a C program starts in the
		// "C" locale, where that encoding is ASCII - so without the three calls below every non-ASCII key
		// silently produces nothing. .NET setting its own managed encoding does not help: this is libc's
		// per-process locale, which Xlib reads directly.

		/// <summary>
		/// libc's <c>setlocale</c>. Pass <see cref="LC_ALL"/> and <c>""</c> to adopt the environment's
		/// locale, which is what makes <c>XLookupString</c> produce UTF-8 on any modern desktop.
		/// </summary>
		/// <returns>The locale now in effect, in static storage libc owns - do not free it.</returns>
		[DllImport(LibC, EntryPoint = "setlocale")]
		public static extern IntPtr SetLocale(int category, [MarshalAs(UnmanagedType.LPUTF8Str)] string locale);

		/// <summary>
		/// Whether Xlib can work in the locale <see cref="SetLocale"/> just established. False means the X
		/// locale database has nothing for it, and the caller should fall back to the "C" locale rather
		/// than proceed - Xlib's behaviour in an unsupported locale is undefined.
		/// </summary>
		/// <returns>Bool.</returns>
		[DllImport(X11Lib)]
		public static extern int XSupportsLocale();

		/// <summary>
		/// Sets the X locale modifiers, which is how an input method is selected. Pass <c>""</c> to take the
		/// value of <c>XMODIFIERS</c> from the environment - the usual choice, since that is where a running
		/// IME advertises itself.
		/// </summary>
		/// <returns>The modifier string now in effect, or null when it could not be set. Xlib owns it.</returns>
		[DllImport(X11Lib)]
		public static extern IntPtr XSetLocaleModifiers([MarshalAs(UnmanagedType.LPUTF8Str)] string modifierList);

		// ---- Windows ---------------------------------------------------------------------------------

		[DllImport(X11Lib)]
		public static extern ulong XCreateSimpleWindow(
			IntPtr display,
			ulong parent,
			int x,
			int y,
			uint width,
			uint height,
			uint borderWidth,
			ulong border,
			ulong background);

		/// <summary>
		/// The full creation call. Only the fields of <paramref name="attributes"/> named in
		/// <paramref name="valueMask"/> are read, so a field set without its <c>CW*</c> bit is silently lost.
		/// </summary>
		[DllImport(X11Lib)]
		public static extern ulong XCreateWindow(
			IntPtr display,
			ulong parent,
			int x,
			int y,
			uint width,
			uint height,
			uint borderWidth,
			int depth,
			uint windowClass,
			IntPtr visual,
			ulong valueMask,
			XSetWindowAttributes* attributes);

		[DllImport(X11Lib)]
		public static extern int XDestroyWindow(IntPtr display, ulong window);

		[DllImport(X11Lib)]
		public static extern int XMapWindow(IntPtr display, ulong window);

		[DllImport(X11Lib)]
		public static extern int XUnmapWindow(IntPtr display, ulong window);

		[DllImport(X11Lib)]
		public static extern int XRaiseWindow(IntPtr display, ulong window);

		[DllImport(X11Lib)]
		public static extern int XSetInputFocus(IntPtr display, ulong focus, int revertTo, ulong time);

		[DllImport(X11Lib)]
		public static extern int XMoveWindow(IntPtr display, ulong window, int x, int y);

		[DllImport(X11Lib)]
		public static extern int XResizeWindow(IntPtr display, ulong window, uint width, uint height);

		[DllImport(X11Lib)]
		public static extern int XMoveResizeWindow(IntPtr display, ulong window, int x, int y, uint width, uint height);

		/// <summary>Reads geometry and state. The position it returns is parent-relative - see
		/// <see cref="XTranslateCoordinates"/> for the screen position a desktop position needs.</summary>
		[DllImport(X11Lib)]
		public static extern int XGetWindowAttributes(IntPtr display, ulong window, out XWindowAttributes attributes);

		/// <summary>
		/// Maps a point from one window's coordinates to another's. Passing the root as
		/// <paramref name="destWindow"/> is how a window's true screen position is found under a reparenting
		/// window manager, where the frame - not the window - is what the root actually contains.
		/// </summary>
		[DllImport(X11Lib)]
		public static extern int XTranslateCoordinates(
			IntPtr display,
			ulong srcWindow,
			ulong destWindow,
			int srcX,
			int srcY,
			out int destX,
			out int destY,
			out ulong child);

		[DllImport(X11Lib)]
		public static extern int XStoreName(IntPtr display, ulong window, [MarshalAs(UnmanagedType.LPUTF8Str)] string windowName);

		/// <summary>Allocates a zeroed <see cref="XSizeHints"/> the Xlib way. Free with <see cref="XFree"/>.
		/// A stack <see cref="XSizeHints"/> works just as well - this exists for the paths that would rather
		/// let Xlib own the memory than trust a hand-written layout.</summary>
		[DllImport(X11Lib)]
		public static extern IntPtr XAllocSizeHints();

		[DllImport(X11Lib)]
		public static extern void XSetWMNormalHints(IntPtr display, ulong window, XSizeHints* hints);

		// ---- Events ----------------------------------------------------------------------------------

		[DllImport(X11Lib)]
		public static extern int XSelectInput(IntPtr display, ulong window, long eventMask);

		/// <summary>How many events are already decoded and waiting. Zero does not mean the socket is
		/// empty - it means Xlib's queue is; the flush inside it is why a pump can call this and then poll.</summary>
		[DllImport(X11Lib)]
		public static extern int XPending(IntPtr display);

		/// <summary>Removes the next event, <b>blocking</b> until there is one. Only safe after
		/// <see cref="XPending"/> reports a non-zero count, or the pump stalls.</summary>
		[DllImport(X11Lib)]
		public static extern int XNextEvent(IntPtr display, out XEvent eventReturn);

		/// <summary>Reads the next event without removing it. Used to collapse a burst of
		/// ConfigureNotify or MotionNotify into just the last one.</summary>
		[DllImport(X11Lib)]
		public static extern int XPeekEvent(IntPtr display, out XEvent eventReturn);

		[DllImport(X11Lib)]
		public static extern int XSendEvent(IntPtr display, ulong window, int propagate, long eventMask, ref XEvent eventSend);

		/// <summary>Pushes buffered requests to the server without waiting for them.</summary>
		[DllImport(X11Lib)]
		public static extern int XFlush(IntPtr display);

		/// <summary>Flushes and then waits for the server to finish. <paramref name="discard"/> non-zero
		/// throws away everything queued, which is only ever right during teardown.</summary>
		[DllImport(X11Lib)]
		public static extern int XSync(IntPtr display, int discard);

		// ---- Atoms and properties --------------------------------------------------------------------

		/// <summary>Interns a name. <paramref name="onlyIfExists"/> non-zero returns <c>None</c> rather
		/// than creating an atom nobody else knows about.</summary>
		[DllImport(X11Lib)]
		public static extern ulong XInternAtom(
			IntPtr display,
			[MarshalAs(UnmanagedType.LPUTF8Str)] string atomName,
			int onlyIfExists);

		/// <summary>Declares which WM protocols this window handles - <c>WM_DELETE_WINDOW</c> above all,
		/// without which the window manager kills the connection instead of asking to close.</summary>
		[DllImport(X11Lib)]
		public static extern int XSetWMProtocols(IntPtr display, ulong window, ulong[] protocols, int count);

		[DllImport(X11Lib)]
		public static extern int XChangeProperty(
			IntPtr display,
			ulong window,
			ulong property,
			ulong type,
			int format,
			int mode,
			byte* data,
			int elementCount);

		/// <summary>
		/// Reads a property. <paramref name="prop"/> comes back as memory Xlib owns and the caller must
		/// release with <see cref="XFree"/> - including when <paramref name="itemCount"/> is zero, which is
		/// the leak everyone writes at least once.
		/// </summary>
		/// <remarks>
		/// The offset and length are in <em>32-bit units</em>, and a "format 32" property is unpacked into
		/// C <c>long</c>s, so on LP64 each item in <paramref name="prop"/> is 8 bytes wide even though the
		/// wire carried 4.
		/// </remarks>
		[DllImport(X11Lib)]
		public static extern int XGetWindowProperty(
			IntPtr display,
			ulong window,
			ulong property,
			long longOffset,
			long longLength,
			int delete,
			ulong requestedType,
			out ulong actualType,
			out int actualFormat,
			out ulong itemCount,
			out ulong bytesAfter,
			out IntPtr prop);

		/// <summary>
		/// Removes a property from a window. On the receiving side of an INCR transfer this is not a
		/// tidy-up but the protocol's flow control: deleting the property is how the reader tells the
		/// sender it has taken the chunk and is ready for the next one.
		/// </summary>
		[DllImport(X11Lib)]
		public static extern int XDeleteProperty(IntPtr display, ulong window, ulong property);

		[DllImport(X11Lib)]
		public static extern int XFree(IntPtr data);

		/// <summary>
		/// The largest request this connection may send, in 4-byte units. Bounds how much property data a
		/// single <c>XChangeProperty</c> can carry - Xlib does not split one for you.
		/// </summary>
		[DllImport(X11Lib)]
		public static extern long XMaxRequestSize(IntPtr display);

		/// <summary>
		/// The same limit raised by the BIG-REQUESTS extension, in 4-byte units, or 0 when the server does
		/// not offer it - in which case <see cref="XMaxRequestSize"/> is the real ceiling.
		/// </summary>
		[DllImport(X11Lib)]
		public static extern long XExtendedMaxRequestSize(IntPtr display);

		/// <summary>
		/// The server-wide X resource database as one string, or null when nothing has been loaded into
		/// <c>RESOURCE_MANAGER</c>. The returned pointer belongs to Xlib and must <b>not</b> be freed.
		/// See <see cref="TryReadXftDpi"/> for the only thing this is used for here.
		/// </summary>
		[DllImport(X11Lib)]
		public static extern IntPtr XResourceManagerString(IntPtr display);

		// ---- Keyboard --------------------------------------------------------------------------------

		/// <summary>
		/// Translates a key event into both the typed text and the keysym. This is the layout-aware call -
		/// it honours Shift, Caps Lock and the group - so it is what a character-producing key goes through.
		/// </summary>
		/// <param name="keyEvent">The event; taken by reference because Xlib's prototype is non-const.</param>
		/// <param name="buffer">Where the typed bytes go, in the current locale's encoding.</param>
		/// <param name="bytesBuffer">Capacity of <paramref name="buffer"/>.</param>
		/// <param name="keysym">The resolved keysym, or <c>None</c>.</param>
		/// <param name="status">An <c>XComposeStatus*</c>; pass <see cref="IntPtr.Zero"/> - the compose
		/// state this would carry is dead weight, since real compose handling needs an XIM instead.</param>
		/// <returns>How many bytes were written to <paramref name="buffer"/>.</returns>
		[DllImport(X11Lib)]
		public static extern int XLookupString(ref XKeyEvent keyEvent, byte* buffer, int bytesBuffer, out ulong keysym, IntPtr status);

		/// <summary>
		/// The keysym at a shift level of the event's keycode, ignoring the event's own modifier state.
		/// Index 0 is the unshifted symbol, which is what a shortcut should be matched on so that Ctrl+Shift+S
		/// still resolves to S rather than to whatever S produces when shifted.
		/// </summary>
		[DllImport(X11Lib)]
		public static extern ulong XLookupKeysym(ref XKeyEvent keyEvent, int index);

		/// <summary>
		/// The XKB form of the same lookup, taking a keycode with no event around it. Exported by libX11
		/// itself (XKB is not a separate library), so it needs no extra import.
		/// </summary>
		/// <param name="keycode">An X keycode - a <c>KeyCode</c>, which is a single byte.</param>
		/// <param name="group">The keyboard group (layout); 0 is the active one for most setups.</param>
		/// <param name="level">The shift level; 0 is unshifted.</param>
		[DllImport(X11Lib)]
		public static extern ulong XkbKeycodeToKeysym(IntPtr display, byte keycode, int group, int level);

		/// <summary>
		/// Re-reads the server's keyboard mapping into Xlib's cache. Must be called with the
		/// <c>MappingNotify</c> that reported the change: until it is, <see cref="XLookupString"/> and
		/// <see cref="XLookupKeysym"/> keep answering from the layout that was in effect before the user
		/// switched it, so every keystroke resolves to the wrong symbol.
		/// </summary>
		/// <param name="mappingEvent">The event; taken by reference because Xlib's prototype is non-const.</param>
		[DllImport(X11Lib)]
		public static extern int XRefreshKeyboardMapping(ref XMappingEvent mappingEvent);

		// ---- Cursors and pointer grabs ---------------------------------------------------------------

		/// <summary>Makes a cursor from the standard "cursor" font. <paramref name="shape"/> is an
		/// <c>X11.XC_*</c> id. The result must be freed with <see cref="XFreeCursor"/>.</summary>
		[DllImport(X11Lib)]
		public static extern ulong XCreateFontCursor(IntPtr display, uint shape);

		[DllImport(X11Lib)]
		public static extern int XDefineCursor(IntPtr display, ulong window, ulong cursor);

		/// <summary>Drops the window's cursor override, so it inherits the parent's again.</summary>
		[DllImport(X11Lib)]
		public static extern int XUndefineCursor(IntPtr display, ulong window);

		[DllImport(X11Lib)]
		public static extern int XFreeCursor(IntPtr display, ulong cursor);

		/// <summary>
		/// Redirects all pointer events to one window until <see cref="XUngrabPointer"/>. This is how X11
		/// spells the implicit capture WinForms gives a button press for free: without it a drag that leaves
		/// the window stops being delivered mid-gesture.
		/// </summary>
		/// <returns><c>X11.GrabSuccess</c>, or a refusal code when another client already holds the pointer.</returns>
		[DllImport(X11Lib)]
		public static extern int XGrabPointer(
			IntPtr display,
			ulong grabWindow,
			int ownerEvents,
			uint eventMask,
			int pointerMode,
			int keyboardMode,
			ulong confineTo,
			ulong cursor,
			ulong time);

		[DllImport(X11Lib)]
		public static extern int XUngrabPointer(IntPtr display, ulong time);

		/// <summary>
		/// Asks the server where the pointer is and which modifiers and buttons are held right now. The
		/// point of it here is <paramref name="mask"/>: it is the live equivalent of the <c>state</c> word
		/// every input event carries, and is the only way to learn what is held when no event said so -
		/// which is exactly the case on regaining the focus, where every modifier change that happened
		/// while another window had the keyboard was delivered somewhere else. The mac host reads
		/// <c>+[NSEvent modifierFlags]</c> at the same moment and for the same reason.
		/// </summary>
		/// <returns>Bool: false when the pointer is on another screen, in which case the window-relative
		/// coordinates are meaningless (the mask still is not).</returns>
		[DllImport(X11Lib)]
		public static extern int XQueryPointer(
			IntPtr display,
			ulong window,
			out ulong root,
			out ulong child,
			out int rootX,
			out int rootY,
			out int windowX,
			out int windowY,
			out uint mask);

		// ---- Selections (the clipboard) --------------------------------------------------------------

		/// <summary>
		/// Claims a selection. X11 has no clipboard daemon in the protocol: the owner <em>is</em> the
		/// clipboard, and must answer SelectionRequest events for as long as it holds the claim.
		/// </summary>
		[DllImport(X11Lib)]
		public static extern int XSetSelectionOwner(IntPtr display, ulong selection, ulong owner, ulong time);

		[DllImport(X11Lib)]
		public static extern ulong XGetSelectionOwner(IntPtr display, ulong selection);

		/// <summary>Asks the current owner to convert a selection into a target type. The answer arrives
		/// later as a SelectionNotify event, so a paste is asynchronous by construction.</summary>
		[DllImport(X11Lib)]
		public static extern int XConvertSelection(
			IntPtr display,
			ulong selection,
			ulong target,
			ulong property,
			ulong requestor,
			ulong time);

		// ---- libc ------------------------------------------------------------------------------------

		/// <summary>
		/// Waits for readability on the X connection with a timeout, so an idle pump can sleep instead of
		/// spinning. <paramref name="timeout"/> is milliseconds; -1 blocks forever and 0 returns at once.
		/// </summary>
		[DllImport(LibC, EntryPoint = "poll", SetLastError = true)]
		public static extern int Poll(PollFd* fds, nuint fdCount, int timeout);

		// ---- Helpers ---------------------------------------------------------------------------------

		/// <summary>
		/// Reads <c>Xft.dpi</c> out of the X resource database, which is where every desktop environment
		/// records the user's chosen scaling. There is no Xrm parse here on purpose: the database is a
		/// newline-separated list of <c>Name:\tvalue</c> lines, and pulling one known key out of it in C# is
		/// less code than binding <c>XrmGetStringDatabase</c>/<c>XrmGetResource</c> and freeing the database
		/// afterwards.
		/// </summary>
		/// <param name="display">An open display.</param>
		/// <param name="dpi">The value found, in dots per inch.</param>
		/// <returns>False when the resource database is empty or has no <c>Xft.dpi</c> line.</returns>
		public static bool TryReadXftDpi(IntPtr display, out double dpi)
		{
			dpi = 0;

			if (display == IntPtr.Zero)
			{
				return false;
			}

			IntPtr resources = XResourceManagerString(display);
			if (resources == IntPtr.Zero)
			{
				return false;
			}

			// Xlib owns this string, so it is read and never freed.
			string database = Marshal.PtrToStringUTF8(resources);
			if (string.IsNullOrEmpty(database))
			{
				return false;
			}

			foreach (string line in database.Split('\n'))
			{
				int separator = line.IndexOf(':');
				if (separator < 0)
				{
					continue;
				}

				if (!line.AsSpan(0, separator).Trim().Equals("Xft.dpi", StringComparison.Ordinal))
				{
					continue;
				}

				// The value is conventionally separated by a tab, and is an integer in every writer seen in
				// the wild - but it is parsed as a double and with the invariant culture anyway, because a
				// decimal comma from the ambient culture would otherwise silently reject "96.0".
				ReadOnlySpan<char> value = line.AsSpan(separator + 1).Trim();
				if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
					&& parsed > 0)
				{
					dpi = parsed;
					return true;
				}

				return false;
			}

			return false;
		}

		/// <summary>
		/// Checks the hand-written struct layouts above against the sizes <c>X11/Xlib.h</c> produces on
		/// LP64. Every one of these is memory the X server wrote and Xlib handed over untouched, so a field
		/// in the wrong place does not fail - it silently reads a neighbouring field, which is the kind of
		/// bug that looks like "the mouse is off by a bit sometimes". A test calls this.
		/// </summary>
		/// <exception cref="InvalidOperationException">A struct does not have the size the C ABI gives it.</exception>
		public static void VerifyLayouts()
		{
			Expect(sizeof(XEvent), 192, nameof(XEvent));
			Expect(sizeof(XKeyEvent), 96, nameof(XKeyEvent));
			Expect(sizeof(XButtonEvent), 96, nameof(XButtonEvent));
			Expect(sizeof(XMotionEvent), 96, nameof(XMotionEvent));
			Expect(sizeof(XCrossingEvent), 104, nameof(XCrossingEvent));
			Expect(sizeof(XFocusChangeEvent), 48, nameof(XFocusChangeEvent));
			Expect(sizeof(XExposeEvent), 64, nameof(XExposeEvent));
			Expect(sizeof(XConfigureEvent), 88, nameof(XConfigureEvent));
			Expect(sizeof(XClientMessageEvent), 96, nameof(XClientMessageEvent));
			Expect(sizeof(XSelectionRequestEvent), 80, nameof(XSelectionRequestEvent));
			Expect(sizeof(XSelectionEvent), 72, nameof(XSelectionEvent));
			Expect(sizeof(XSelectionClearEvent), 56, nameof(XSelectionClearEvent));
			Expect(sizeof(XPropertyEvent), 64, nameof(XPropertyEvent));
			Expect(sizeof(XDestroyWindowEvent), 48, nameof(XDestroyWindowEvent));
			Expect(sizeof(XMappingEvent), 56, nameof(XMappingEvent));
			Expect(sizeof(XErrorEvent), 40, nameof(XErrorEvent));
			Expect(sizeof(XSizeHints), 80, nameof(XSizeHints));
			Expect(sizeof(XSetWindowAttributes), 112, nameof(XSetWindowAttributes));
			Expect(sizeof(XWindowAttributes), 136, nameof(XWindowAttributes));
			Expect(sizeof(PollFd), 8, nameof(PollFd));

			static void Expect(int actual, int expected, string name)
			{
				if (actual != expected)
				{
					throw new InvalidOperationException(
						$"{name} marshals to {actual} bytes but the X11 ABI says {expected}. "
						+ "A field type or its order does not match X11/Xlib.h.");
				}
			}
		}
	}
}
