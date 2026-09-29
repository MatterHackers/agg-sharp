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
	/// <summary>
	/// CoreMedia's CMTime: a rational time, value / timescale seconds. 24 bytes, so on arm64 it is passed
	/// and returned through memory rather than registers - which the P/Invoke marshaller does for us as long
	/// as each objc_msgSend declaration names the struct by value (see <see cref="ObjC"/>'s arm64 rule).
	/// </summary>
	[StructLayout(LayoutKind.Sequential)]
	internal struct CMTime
	{
		/// <summary>kCMTimeFlags_Valid.</summary>
		public const uint FlagValid = 1;

		/// <summary>TimeSpan ticks (100 ns) as a CMTime timescale; fits CMTime's int32.</summary>
		public const int TicksTimescale = 10_000_000;

		public long Value;
		public int Timescale;
		public uint Flags;
		public long Epoch;

		public static CMTime Zero => new CMTime { Value = 0, Timescale = 1, Flags = FlagValid };

		public static CMTime FromTimeSpan(TimeSpan time) => new CMTime { Value = time.Ticks, Timescale = TicksTimescale, Flags = FlagValid };

		public bool IsNumeric => (Flags & 0x1D) == FlagValid; // valid and not +/- infinity or indefinite

		public TimeSpan ToTimeSpan()
		{
			// Integer arithmetic where it is exact, so a 3 s file at timescale 600 is exactly 3 s.
			if (!IsNumeric || Timescale <= 0)
			{
				return TimeSpan.Zero;
			}

			return TimeSpan.FromTicks((long)Math.Round((decimal)Value * TimeSpan.TicksPerSecond / Timescale));
		}
	}

	/// <summary>CoreMedia's CMTimeRange: a start and a duration, 48 bytes, returned through memory.</summary>
	[StructLayout(LayoutKind.Sequential)]
	internal struct CMTimeRange
	{
		public CMTime Start;
		public CMTime Duration;
	}

	/// <summary>A Core Graphics affine transform: [a b 0; c d 0; tx ty 1], row-vector convention.</summary>
	[StructLayout(LayoutKind.Sequential)]
	internal struct CGAffineTransform
	{
		public double A;
		public double B;
		public double C;
		public double D;
		public double Tx;
		public double Ty;
	}

	/// <summary>
	/// The AVFoundation, CoreMedia and CoreGraphics calls <see cref="MacVideoSession"/> makes, as raw
	/// P/Invoke in the same style as <see cref="ObjC"/> (one objc_msgSend declaration per signature).
	/// </summary>
	internal static class AVFoundationInterop
	{
		public const string AVFoundation = "/System/Library/Frameworks/AVFoundation.framework/AVFoundation";
		public const string CoreMedia = "/System/Library/Frameworks/CoreMedia.framework/CoreMedia";

		// CGBitmapInfo for BGRX in memory: kCGImageAlphaNoneSkipFirst | kCGBitmapByteOrder32Little. That is
		// agg's BGRA byte order once the X byte is made opaque.
		public const uint BitmapInfoBgrx = 6 | (2 << 12);

		private static readonly object LoadLock = new object();
		private static bool loaded;

		/// <summary>
		/// Maps AVFoundation and CoreMedia into the process. Like AppKit (see
		/// <see cref="ObjC.EnsureFrameworksLoaded"/>) a bare .NET process links neither, so AVURLAsset does
		/// not exist until this has run.
		/// </summary>
		public static void EnsureLoaded()
		{
			lock (LoadLock)
			{
				if (!loaded)
				{
					ObjC.EnsureFrameworksLoaded();
					NativeLibrary.Load(CoreMedia);
					NativeLibrary.Load(AVFoundation);
					loaded = true;
				}
			}
		}

		// Autorelease pools. A .NET pool thread has none, so anything autoreleased on it (the tracks array,
		// NSURL, NSError) would leak; every step that talks to AVFoundation runs inside one.
		[DllImport(ObjC.LibObjC)]
		public static extern IntPtr objc_autoreleasePoolPush();

		[DllImport(ObjC.LibObjC)]
		public static extern void objc_autoreleasePoolPop(IntPtr pool);

		/// <summary>-(id)selector:(id) with:(id) - notably +[AVURLAsset URLAssetWithURL:options:].</summary>
		[DllImport(ObjC.LibObjC, EntryPoint = "objc_msgSend")]
		public static extern IntPtr Send_r_r_r(IntPtr receiver, IntPtr selector, IntPtr arg0, IntPtr arg1);

		/// <summary>-(CMTime)selector - notably -[AVAsset duration]. Returned through x8 (it is 24 bytes).</summary>
		[DllImport(ObjC.LibObjC, EntryPoint = "objc_msgSend")]
		public static extern CMTime Send_T(IntPtr receiver, IntPtr selector);

		/// <summary>-(CMTimeRange)selector - notably -[AVAssetTrack timeRange].</summary>
		[DllImport(ObjC.LibObjC, EntryPoint = "objc_msgSend")]
		public static extern CMTimeRange Send_TR(IntPtr receiver, IntPtr selector);

		/// <summary>-(CGAffineTransform)selector - notably -[AVAssetTrack preferredTransform].</summary>
		[DllImport(ObjC.LibObjC, EntryPoint = "objc_msgSend")]
		public static extern CGAffineTransform Send_A(IntPtr receiver, IntPtr selector);

		/// <summary>-(void)selector:(CMTime) - the image generator's time tolerances.</summary>
		[DllImport(ObjC.LibObjC, EntryPoint = "objc_msgSend")]
		public static extern void Send_v_T(IntPtr receiver, IntPtr selector, CMTime arg0);

		/// <summary>-[AVAssetImageGenerator copyCGImageAtTime:actualTime:error:]; the image is +1 retained.</summary>
		[DllImport(ObjC.LibObjC, EntryPoint = "objc_msgSend")]
		public static extern IntPtr Send_CopyCGImage(IntPtr receiver, IntPtr selector, CMTime requestedTime, out CMTime actualTime, out IntPtr error);

		[DllImport(ObjC.CoreGraphics)]
		public static extern nuint CGImageGetWidth(IntPtr image);

		[DllImport(ObjC.CoreGraphics)]
		public static extern nuint CGImageGetHeight(IntPtr image);

		[DllImport(ObjC.CoreGraphics)]
		public static extern IntPtr CGImageGetColorSpace(IntPtr image);

		[DllImport(ObjC.CoreGraphics)]
		public static extern void CGImageRelease(IntPtr image);

		[DllImport(ObjC.CoreGraphics)]
		public static extern IntPtr CGColorSpaceCreateDeviceRGB();

		[DllImport(ObjC.CoreGraphics)]
		public static extern void CGColorSpaceRelease(IntPtr colorSpace);

		[DllImport(ObjC.CoreGraphics)]
		public static extern IntPtr CGBitmapContextCreate(IntPtr data, nuint width, nuint height, nuint bitsPerComponent, nuint bytesPerRow, IntPtr colorSpace, uint bitmapInfo);

		[DllImport(ObjC.CoreGraphics)]
		public static extern void CGContextDrawImage(IntPtr context, CGRect rect, IntPtr image);

		[DllImport(ObjC.CoreGraphics)]
		public static extern void CGContextSetBlendMode(IntPtr context, int mode);

		[DllImport(ObjC.CoreGraphics)]
		public static extern void CGContextRelease(IntPtr context);

		/// <summary>kCGBlendModeCopy: write the image's pixels rather than composite them over the context.</summary>
		public const int BlendModeCopy = 17;
	}
}
