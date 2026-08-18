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

namespace MatterHackers.Agg.Platform.Mac
{
	/// <summary>
	/// The AppKit enumeration values PlatformMac needs, transcribed from the framework headers. They are
	/// plain constants rather than a binding because nothing here is discoverable at runtime: an
	/// Objective-C enum has no metadata on the other side of <c>objc_msgSend</c>.
	/// </summary>
	internal static class AppKitConstants
	{
		// NSWindowStyleMask
		public const ulong NSWindowStyleMaskBorderless = 0;
		public const ulong NSWindowStyleMaskTitled = 1 << 0;
		public const ulong NSWindowStyleMaskClosable = 1 << 1;
		public const ulong NSWindowStyleMaskMiniaturizable = 1 << 2;
		public const ulong NSWindowStyleMaskResizable = 1 << 3;

		// NSBackingStoreType
		public const ulong NSBackingStoreBuffered = 2;

		// NSApplicationActivationPolicy
		public const long NSApplicationActivationPolicyRegular = 0;

		// NSEventMask
		public const ulong NSEventMaskAny = ulong.MaxValue;

		// NSEventType
		public const long NSEventTypeLeftMouseDown = 1;
		public const long NSEventTypeLeftMouseUp = 2;
		public const long NSEventTypeRightMouseDown = 3;
		public const long NSEventTypeRightMouseUp = 4;
		public const long NSEventTypeMouseMoved = 5;
		public const long NSEventTypeLeftMouseDragged = 6;
		public const long NSEventTypeRightMouseDragged = 7;
		public const long NSEventTypeMouseEntered = 8;
		public const long NSEventTypeMouseExited = 9;
		public const long NSEventTypeKeyDown = 10;
		public const long NSEventTypeKeyUp = 11;
		public const long NSEventTypeFlagsChanged = 12;
		public const long NSEventTypeScrollWheel = 22;
		public const long NSEventTypeOtherMouseDown = 25;
		public const long NSEventTypeOtherMouseUp = 26;
		public const long NSEventTypeOtherMouseDragged = 27;
		public const long NSEventTypeMagnify = 30;

		// NSEventPhase. A continuous gesture - a trackpad scroll, a pinch - reports where in its life each
		// event falls, and it is a bitmask rather than an enum because a single event can carry more than one
		// (an Ended that is also Cancelled). Two streams use it: -[NSEvent phase] is the fingers-on-glass part
		// of the gesture, and -[NSEvent momentumPhase] is the inertia AppKit keeps sending after they lift.
		// Both read as zero (None) for a device that has no phases at all, such as a real mouse wheel.
		public const ulong NSEventPhaseNone = 0;
		public const ulong NSEventPhaseBegan = 1 << 0;
		public const ulong NSEventPhaseStationary = 1 << 1;
		public const ulong NSEventPhaseChanged = 1 << 2;
		public const ulong NSEventPhaseEnded = 1 << 3;
		public const ulong NSEventPhaseCancelled = 1 << 4;
		public const ulong NSEventPhaseMayBegin = 1 << 5;

		// NSEventModifierFlags
		public const ulong NSEventModifierFlagCapsLock = 1 << 16;
		public const ulong NSEventModifierFlagShift = 1 << 17;
		public const ulong NSEventModifierFlagControl = 1 << 18;
		public const ulong NSEventModifierFlagOption = 1 << 19;
		public const ulong NSEventModifierFlagCommand = 1 << 20;

		// NSBitmapImageFileType
		public const ulong NSBitmapImageFileTypePNG = 4;

		// Virtual key codes (Carbon "kVK_" constants). Hardware positions, not characters, which is why
		// they are the same on every keyboard layout - the typed character comes from -[NSEvent characters].
		public const ushort VkReturn = 0x24;
		public const ushort VkTab = 0x30;
		public const ushort VkSpace = 0x31;
		public const ushort VkDelete = 0x33;      // Backspace
		public const ushort VkEscape = 0x35;
		public const ushort VkCommand = 0x37;
		public const ushort VkShift = 0x38;
		public const ushort VkCapsLock = 0x39;
		public const ushort VkOption = 0x3A;
		public const ushort VkControl = 0x3B;
		public const ushort VkRightShift = 0x3C;
		public const ushort VkRightOption = 0x3D;
		public const ushort VkRightControl = 0x3E;
		public const ushort VkFunction = 0x3F;
		public const ushort VkF1 = 0x7A;
		public const ushort VkF2 = 0x78;
		public const ushort VkF3 = 0x63;
		public const ushort VkF4 = 0x76;
		public const ushort VkF5 = 0x60;
		public const ushort VkF6 = 0x61;
		public const ushort VkF7 = 0x62;
		public const ushort VkF8 = 0x64;
		public const ushort VkF9 = 0x65;
		public const ushort VkF10 = 0x6D;
		public const ushort VkF11 = 0x67;
		public const ushort VkF12 = 0x6F;
		public const ushort VkHome = 0x73;
		public const ushort VkPageUp = 0x74;
		public const ushort VkForwardDelete = 0x75;
		public const ushort VkEnd = 0x77;
		public const ushort VkPageDown = 0x79;
		public const ushort VkLeftArrow = 0x7B;
		public const ushort VkRightArrow = 0x7C;
		public const ushort VkDownArrow = 0x7D;
		public const ushort VkUpArrow = 0x7E;
	}
}
