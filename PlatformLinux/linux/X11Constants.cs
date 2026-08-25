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

namespace MatterHackers.Agg.Platform.Linux
{
	/// <summary>
	/// The X11 protocol values PlatformLinux needs, transcribed from <c>X11/X.h</c>,
	/// <c>X11/Xutil.h</c>, <c>X11/keysymdef.h</c> and <c>X11/cursorfont.h</c>. They are plain constants
	/// rather than a binding for the same reason PlatformMac's <c>AppKitConstants</c> are: none of this is
	/// discoverable at runtime, because a C enum carries no metadata across a P/Invoke.
	/// </summary>
	internal static class X11
	{
		// ---- Universal "nothing" values -------------------------------------------------------------
		// X.h spells all three of these `None`/`CurrentTime`; they are separated here only so a call site
		// says which kind of nothing it means.

		/// <summary>X.h's <c>None</c>: the null XID. Never a real window, atom, cursor or pixmap.</summary>
		public const ulong None = 0;

		/// <summary>X.h's <c>CurrentTime</c>: "whatever the server's clock says when it reads this".</summary>
		public const ulong CurrentTime = 0;

		/// <summary>Inherit the parent's visual/depth in <c>XCreateWindow</c>.</summary>
		public const int CopyFromParent = 0;

		/// <summary>X.h's <c>True</c>. Xlib's <c>Bool</c> is an <c>int</c>, not a C99 <c>_Bool</c>.</summary>
		public const int True = 1;

		/// <summary>X.h's <c>False</c>.</summary>
		public const int False = 0;

		/// <summary>Match any property type in <c>XGetWindowProperty</c>.</summary>
		public const ulong AnyPropertyType = 0;

		/// <summary>
		/// X.h's <c>Success</c>: what <c>XGetWindowProperty</c> returns when it worked. It is <em>zero</em>,
		/// so the reflex "non-zero means success" has this exactly backwards.
		/// </summary>
		public const int Success = 0;

		// ---- Predefined atoms (Xatom.h) -------------------------------------------------------------
		// The first few atom ids are fixed by the protocol itself rather than interned, which is why they can
		// be written as constants at all. Only the handful that property reads and writes here name.

		/// <summary>Xatom.h's <c>XA_ATOM</c> - the type of a property holding atoms, e.g. <c>_NET_WM_STATE</c>.</summary>
		public const ulong XA_ATOM = 4;

		/// <summary>Xatom.h's <c>XA_CARDINAL</c> - the type of a property holding unsigned numbers,
		/// e.g. <c>_NET_FRAME_EXTENTS</c>.</summary>
		public const ulong XA_CARDINAL = 6;

		/// <summary>Xatom.h's <c>XA_STRING</c> - Latin-1 text, which is all <c>WM_NAME</c> can carry;
		/// anything outside it needs <c>_NET_WM_NAME</c> as UTF8_STRING instead.</summary>
		public const ulong XA_STRING = 31;

		// ---- Event types (X.h) ----------------------------------------------------------------------
		// The value in XEvent.type. 0 and 1 are not events: the protocol reserves them for error and reply
		// replies, which never reach an application through the event queue.

		public const int KeyPress = 2;
		public const int KeyRelease = 3;
		public const int ButtonPress = 4;
		public const int ButtonRelease = 5;
		public const int MotionNotify = 6;
		public const int EnterNotify = 7;
		public const int LeaveNotify = 8;
		public const int FocusIn = 9;
		public const int FocusOut = 10;
		public const int KeymapNotify = 11;
		public const int Expose = 12;
		public const int GraphicsExpose = 13;
		public const int NoExpose = 14;
		public const int VisibilityNotify = 15;
		public const int CreateNotify = 16;
		public const int DestroyNotify = 17;
		public const int UnmapNotify = 18;
		public const int MapNotify = 19;
		public const int MapRequest = 20;
		public const int ReparentNotify = 21;
		public const int ConfigureNotify = 22;
		public const int ConfigureRequest = 23;
		public const int GravityNotify = 24;
		public const int ResizeRequest = 25;
		public const int CirculateNotify = 26;
		public const int CirculateRequest = 27;
		public const int PropertyNotify = 28;
		public const int SelectionClear = 29;
		public const int SelectionRequest = 30;
		public const int SelectionNotify = 31;
		public const int ColormapNotify = 32;
		public const int ClientMessage = 33;
		public const int MappingNotify = 34;
		public const int GenericEvent = 35;

		// ---- Event masks (X.h) ----------------------------------------------------------------------
		// What XSelectInput is told to deliver. A `long` because that is what XSelectInput takes, and on
		// LP64 that is 64 bits even though only the low 25 are defined.

		public const long NoEventMask = 0L;
		public const long KeyPressMask = 1L << 0;
		public const long KeyReleaseMask = 1L << 1;
		public const long ButtonPressMask = 1L << 2;
		public const long ButtonReleaseMask = 1L << 3;
		public const long EnterWindowMask = 1L << 4;
		public const long LeaveWindowMask = 1L << 5;
		public const long PointerMotionMask = 1L << 6;

		/// <summary>Asks the server to compress motion into one event plus a "there is more" hint. Not used
		/// here: a 3D viewport wants every sample it can get, and compression is what makes a drag stutter.</summary>
		public const long PointerMotionHintMask = 1L << 7;

		public const long Button1MotionMask = 1L << 8;
		public const long Button2MotionMask = 1L << 9;
		public const long Button3MotionMask = 1L << 10;
		public const long Button4MotionMask = 1L << 11;
		public const long Button5MotionMask = 1L << 12;
		public const long ButtonMotionMask = 1L << 13;
		public const long KeymapStateMask = 1L << 14;
		public const long ExposureMask = 1L << 15;
		public const long VisibilityChangeMask = 1L << 16;
		public const long StructureNotifyMask = 1L << 17;
		public const long ResizeRedirectMask = 1L << 18;
		public const long SubstructureNotifyMask = 1L << 19;
		public const long SubstructureRedirectMask = 1L << 20;
		public const long FocusChangeMask = 1L << 21;
		public const long PropertyChangeMask = 1L << 22;
		public const long ColormapChangeMask = 1L << 23;
		public const long OwnerGrabButtonMask = 1L << 24;

		// ---- Pointer buttons (X.h) ------------------------------------------------------------------
		// X11 has no separate wheel event: a detent is a ButtonPress/ButtonRelease pair on a synthetic
		// button, which is why a wheel-only device still reports "buttons".

		public const uint Button1 = 1;   // left
		public const uint Button2 = 2;   // middle
		public const uint Button3 = 3;   // right
		public const uint Button4 = 4;   // wheel up
		public const uint Button5 = 5;   // wheel down
		public const uint Button6 = 6;   // wheel/tilt left
		public const uint Button7 = 7;   // wheel/tilt right

		// ---- Key/button state modifier masks (X.h) --------------------------------------------------
		// The `state` field of a key, button, motion or crossing event. It is the state *before* the event,
		// so a ShiftMask on a KeyPress of Shift itself is absent, and present on the matching KeyRelease.

		public const uint ShiftMask = 1 << 0;
		public const uint LockMask = 1 << 1;      // Caps Lock
		public const uint ControlMask = 1 << 2;
		public const uint Mod1Mask = 1 << 3;      // conventionally Alt
		public const uint Mod2Mask = 1 << 4;      // conventionally Num Lock
		public const uint Mod3Mask = 1 << 5;
		public const uint Mod4Mask = 1 << 6;      // conventionally Super / the Windows key
		public const uint Mod5Mask = 1 << 7;      // conventionally AltGr

		/// <summary>Every modifier bit, and no button bit. What separates the two halves of a state word:
		/// the low eight bits are modifiers, bits 8 and up are the buttons currently held.</summary>
		public const uint AllModifierMask = ShiftMask | LockMask | ControlMask | Mod1Mask | Mod2Mask | Mod3Mask | Mod4Mask | Mod5Mask;

		public const uint Button1Mask = 1 << 8;
		public const uint Button2Mask = 1 << 9;
		public const uint Button3Mask = 1 << 10;
		public const uint Button4Mask = 1 << 11;
		public const uint Button5Mask = 1 << 12;

		// ---- Crossing and focus event modes (X.h) ---------------------------------------------------
		// The `mode` field of an EnterNotify, LeaveNotify, FocusIn or FocusOut. Only NotifyNormal is the
		// pointer (or the focus) actually having moved: a grab and its release each manufacture a crossing
		// pair "as if the pointer warped", so a host that believes every LeaveNotify fires its pointer-gone
		// sentinel every time it grabs for a drag - which is the X11 spelling of the cursor-rect artifact
		// the mac host's IsRealPointerExit exists for.

		public const int NotifyNormal = 0;
		public const int NotifyGrab = 1;
		public const int NotifyUngrab = 2;
		public const int NotifyWhileGrabbed = 3;

		// ---- Crossing and focus event details (X.h) -------------------------------------------------
		// The `detail` field, which says where the other end of the transition sat in the window hierarchy.
		// The first five describe a focus that moved between real windows. The last three do not, and that
		// is what makes them worth naming: Pointer and PointerRoot are what a focus-follows-mouse desktop
		// sends as the pointer crosses a window while the focus is PointerRoot - they mean "the keyboard
		// goes wherever the pointer is", not "you have lost it" - and DetailNone is the focus becoming None.
		// Acting on those releases the held modifiers in the middle of a drag whose pointer merely passed
		// over another window, which is the latched-modifier bug the focus handlers exist to prevent,
		// arriving by the opposite route.

		public const int NotifyAncestor = 0;
		public const int NotifyVirtual = 1;
		public const int NotifyInferior = 2;
		public const int NotifyNonlinear = 3;
		public const int NotifyNonlinearVirtual = 4;
		public const int NotifyPointer = 5;
		public const int NotifyPointerRoot = 6;
		public const int NotifyDetailNone = 7;

		// ---- Keysyms (keysymdef.h) ------------------------------------------------------------------
		// A keysym is the *symbol* the key produces under the active layout, which is what agg's Keys maps
		// onto - the raw keycode is a hardware position and differs per keyboard.
		//
		// Latin-1 needs no table: keysyms 0x0020-0x00FF are exactly their ISO 8859-1 code points, so
		// XK_space is 0x20, XK_0-XK_9 are 0x30-0x39, XK_A-XK_Z are 0x41-0x5A and XK_a-XK_z are 0x61-0x7A.
		// The anchors below exist so a range check can be written without a magic number.

		public const ulong XK_space = 0x0020;
		public const ulong XK_0 = 0x0030;
		public const ulong XK_9 = 0x0039;
		public const ulong XK_A = 0x0041;
		public const ulong XK_Z = 0x005A;
		public const ulong XK_a = 0x0061;
		public const ulong XK_z = 0x007A;

		// Function and editing keys. Most live in the 0xFF00 "keyboard function" page, but not all of them:
		// the ISO keysyms sit one page below in 0xFE00, and XK_ISO_Left_Tab in particular is not an obscure
		// corner - it is what an ordinary Shift+Tab produces. What the two pages do share is that neither
		// can collide with the Latin-1 range above.
		public const ulong XK_ISO_Left_Tab = 0xFE20;

		public const ulong XK_BackSpace = 0xFF08;
		public const ulong XK_Tab = 0xFF09;
		public const ulong XK_Return = 0xFF0D;
		public const ulong XK_Pause = 0xFF13;
		public const ulong XK_Scroll_Lock = 0xFF14;
		public const ulong XK_Escape = 0xFF1B;
		public const ulong XK_Home = 0xFF50;
		public const ulong XK_Left = 0xFF51;
		public const ulong XK_Up = 0xFF52;
		public const ulong XK_Right = 0xFF53;
		public const ulong XK_Down = 0xFF54;
		public const ulong XK_Page_Up = 0xFF55;     // XK_Prior in older headers
		public const ulong XK_Page_Down = 0xFF56;   // XK_Next in older headers
		public const ulong XK_End = 0xFF57;
		public const ulong XK_Begin = 0xFF58;
		public const ulong XK_Print = 0xFF61;
		public const ulong XK_Insert = 0xFF63;
		public const ulong XK_Menu = 0xFF67;
		public const ulong XK_Num_Lock = 0xFF7F;
		public const ulong XK_Delete = 0xFFFF;

		// Keypad. Reported as the *_KP_ symbols only when Num Lock is off or the layout says so; with Num
		// Lock on the digits arrive as XK_KP_0..XK_KP_9 instead of the navigation names.
		public const ulong XK_KP_Space = 0xFF80;
		public const ulong XK_KP_Tab = 0xFF89;
		public const ulong XK_KP_Enter = 0xFF8D;
		public const ulong XK_KP_Home = 0xFF95;
		public const ulong XK_KP_Left = 0xFF96;
		public const ulong XK_KP_Up = 0xFF97;
		public const ulong XK_KP_Right = 0xFF98;
		public const ulong XK_KP_Down = 0xFF99;
		public const ulong XK_KP_Page_Up = 0xFF9A;
		public const ulong XK_KP_Page_Down = 0xFF9B;
		public const ulong XK_KP_End = 0xFF9C;
		public const ulong XK_KP_Begin = 0xFF9D;
		public const ulong XK_KP_Insert = 0xFF9E;
		public const ulong XK_KP_Delete = 0xFF9F;
		public const ulong XK_KP_Multiply = 0xFFAA;
		public const ulong XK_KP_Add = 0xFFAB;
		public const ulong XK_KP_Separator = 0xFFAC;
		public const ulong XK_KP_Subtract = 0xFFAD;
		public const ulong XK_KP_Decimal = 0xFFAE;
		public const ulong XK_KP_Divide = 0xFFAF;
		public const ulong XK_KP_0 = 0xFFB0;
		public const ulong XK_KP_1 = 0xFFB1;
		public const ulong XK_KP_2 = 0xFFB2;
		public const ulong XK_KP_3 = 0xFFB3;
		public const ulong XK_KP_4 = 0xFFB4;
		public const ulong XK_KP_5 = 0xFFB5;
		public const ulong XK_KP_6 = 0xFFB6;
		public const ulong XK_KP_7 = 0xFFB7;
		public const ulong XK_KP_8 = 0xFFB8;
		public const ulong XK_KP_9 = 0xFFB9;

		/// <summary>Out of numeric order with the operators above because keysymdef.h puts it here, past the
		/// digits and immediately before F1 - it was added to the keypad block long after the rest.</summary>
		public const ulong XK_KP_Equal = 0xFFBD;

		// F1..F12 are contiguous from 0xFFBE, so a loop can walk them.
		public const ulong XK_F1 = 0xFFBE;
		public const ulong XK_F2 = 0xFFBF;
		public const ulong XK_F3 = 0xFFC0;
		public const ulong XK_F4 = 0xFFC1;
		public const ulong XK_F5 = 0xFFC2;
		public const ulong XK_F6 = 0xFFC3;
		public const ulong XK_F7 = 0xFFC4;
		public const ulong XK_F8 = 0xFFC5;
		public const ulong XK_F9 = 0xFFC6;
		public const ulong XK_F10 = 0xFFC7;
		public const ulong XK_F11 = 0xFFC8;
		public const ulong XK_F12 = 0xFFC9;

		// Modifier keys themselves. A bare modifier still produces a KeyPress/KeyRelease pair on X11 (unlike
		// AppKit's separate FlagsChanged), so these are how Keyboard's down-state is kept honest.
		public const ulong XK_Shift_L = 0xFFE1;
		public const ulong XK_Shift_R = 0xFFE2;
		public const ulong XK_Control_L = 0xFFE3;
		public const ulong XK_Control_R = 0xFFE4;
		public const ulong XK_Caps_Lock = 0xFFE5;
		public const ulong XK_Meta_L = 0xFFE7;
		public const ulong XK_Meta_R = 0xFFE8;
		public const ulong XK_Alt_L = 0xFFE9;
		public const ulong XK_Alt_R = 0xFFEA;
		public const ulong XK_Super_L = 0xFFEB;
		public const ulong XK_Super_R = 0xFFEC;

		// ---- Font cursor shapes (cursorfont.h) ------------------------------------------------------
		// XC_ ids index the "cursor" font. They are always even, because each glyph is a source/mask pair.

		public const uint XC_arrow = 2;
		public const uint XC_bottom_left_corner = 12;
		public const uint XC_bottom_right_corner = 14;
		public const uint XC_crosshair = 34;
		public const uint XC_fleur = 52;
		public const uint XC_hand2 = 60;
		public const uint XC_question_arrow = 92;
		public const uint XC_sb_h_double_arrow = 108;
		public const uint XC_sb_v_double_arrow = 116;
		public const uint XC_top_left_corner = 134;
		public const uint XC_top_right_corner = 136;
		public const uint XC_watch = 150;
		public const uint XC_xterm = 152;

		// ---- XCreateWindow attribute mask bits (X.h) ------------------------------------------------
		// Which fields of XSetWindowAttributes the server should read. A field left out of the mask keeps
		// its default no matter what the struct holds, which makes a forgotten mask bit silent.

		public const ulong CWBackPixmap = 1UL << 0;
		public const ulong CWBackPixel = 1UL << 1;
		public const ulong CWBorderPixmap = 1UL << 2;
		public const ulong CWBorderPixel = 1UL << 3;
		public const ulong CWBitGravity = 1UL << 4;
		public const ulong CWWinGravity = 1UL << 5;
		public const ulong CWBackingStore = 1UL << 6;
		public const ulong CWBackingPlanes = 1UL << 7;
		public const ulong CWBackingPixel = 1UL << 8;
		public const ulong CWOverrideRedirect = 1UL << 9;
		public const ulong CWSaveUnder = 1UL << 10;
		public const ulong CWEventMask = 1UL << 11;
		public const ulong CWDontPropagate = 1UL << 12;
		public const ulong CWColormap = 1UL << 13;
		public const ulong CWCursor = 1UL << 14;

		// ---- Window classes (X.h) -------------------------------------------------------------------

		public const uint InputOutput = 1;
		public const uint InputOnly = 2;

		// ---- XChangeProperty modes (X.h) ------------------------------------------------------------

		public const int PropModeReplace = 0;
		public const int PropModePrepend = 1;
		public const int PropModeAppend = 2;

		// ---- PropertyNotify state (X.h) -------------------------------------------------------------
		// Which half of a property's life the event is reporting. An INCR reader cares only about
		// PropertyNewValue: the deletes it sees are the echoes of its own XDeleteProperty acknowledgements,
		// and acting on those would read every chunk twice.

		public const int PropertyNewValue = 0;
		public const int PropertyDelete = 1;

		// ---- Grab modes and results (X.h) -----------------------------------------------------------

		/// <summary>
		/// The grabbed device is frozen after every event until the client calls <c>XAllowEvents</c> to let
		/// the next one through. That per-event hand-back is a deadlock waiting to happen in a
		/// single-threaded pump, so a pointer grab here wants <see cref="GrabModeAsync"/>.
		/// </summary>
		public const int GrabModeSync = 0;

		/// <summary>The device keeps delivering events for the whole grab, with no hand-back. What a drag
		/// capture needs.</summary>
		public const int GrabModeAsync = 1;

		/// <summary>XGrabPointer's success return; every other value is a refusal.</summary>
		public const int GrabSuccess = 0;

		// ---- XSetInputFocus revert-to (X.h) ---------------------------------------------------------

		public const int RevertToNone = 0;
		public const int RevertToPointerRoot = 1;
		public const int RevertToParent = 2;

		// ---- EWMH (_NET_WM_STATE client messages) ---------------------------------------------------
		// Not X11 at all: the freedesktop.org window-manager conventions, which is how a client asks to be
		// maximized. There is no Xlib call for it, and which mechanism applies is decided by map state:
		// before the window is mapped the _NET_WM_STATE property itself is the request, and afterwards the
		// manager owns that property and the request has to be a ClientMessage to the root window, where
		// the manager is the one listening. The values below belong to that second, post-map form.

		public const long NetWmStateRemove = 0;
		public const long NetWmStateAdd = 1;
		public const long NetWmStateToggle = 2;

		/// <summary>The <c>source indication</c> every EWMH message carries: 1 means "a normal application
		/// asked", which is what a window manager honours. 0 is the legacy "unknown" and some managers
		/// ignore it outright.</summary>
		public const long NetWmSourceApplication = 1;

		// ---- XSizeHints flags (Xutil.h) -------------------------------------------------------------
		// The window manager reads these; without the matching flag bit the corresponding field is ignored,
		// which is how a minimum size silently fails to be honoured.

		public const long USPosition = 1L << 0;
		public const long USSize = 1L << 1;
		public const long PPosition = 1L << 2;
		public const long PSize = 1L << 3;
		public const long PMinSize = 1L << 4;
		public const long PMaxSize = 1L << 5;
		public const long PResizeInc = 1L << 6;
		public const long PAspect = 1L << 7;
		public const long PBaseSize = 1L << 8;
		public const long PWinGravity = 1L << 9;
	}
}
