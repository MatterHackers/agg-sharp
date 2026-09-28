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
using System.Collections.Specialized;
using System.Runtime.InteropServices;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform.Mac;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// The macOS <see cref="ISystemClipboard"/>, backed by <c>[NSPasteboard generalPasteboard]</c>.
	/// The peer of PlatformWin32's <c>WindowsFormsClipboard</c>; an app installs it with
	/// <c>Clipboard.SetSystemClipboard(new MacClipboard())</c>.
	/// <para>
	/// Text, HTML and images round trip. Images go out as <c>public.png</c> and come back from PNG or,
	/// failing that, <c>public.tiff</c> - which is what Preview and the screenshot tool put there alongside
	/// or instead of PNG. File drop lists report "not present" rather than pretending: nothing on macOS
	/// asks for them yet, and faking a capability is worse than declining it.
	/// </para>
	/// <para>
	/// Every pasteboard call goes through <see cref="MainThreadDispatcher"/>: NSPasteboard is not thread
	/// safe, and a widget's copy/paste can reach here from a test's thread rather than the UI thread.
	/// </para>
	/// </summary>
	public class MacClipboard : ISystemClipboard
	{
		// Uniform Type Identifiers, which are what NSPasteboard has spoken since 10.6. The older
		// NSStringPboardType style constants are exported symbols we would have to dlsym; the UTI
		// strings are stable API and need no lookup.
		private const string TypeUtf8PlainText = "public.utf8-plain-text";
		private const string TypeHtml = "public.html";
		private const string TypePng = "public.png";
		private const string TypeTiff = "public.tiff";

		private static readonly IntPtr SelGeneralPasteboard = ObjC.Sel("generalPasteboard");
		private static readonly IntPtr SelStringForType = ObjC.Sel("stringForType:");
		private static readonly IntPtr SelSetStringForType = ObjC.Sel("setString:forType:");
		private static readonly IntPtr SelClearContents = ObjC.Sel("clearContents");
		private static readonly IntPtr SelDataForType = ObjC.Sel("dataForType:");
		private static readonly IntPtr SelSetDataForType = ObjC.Sel("setData:forType:");
		private static readonly IntPtr SelTypes = ObjC.Sel("types");
		private static readonly IntPtr SelContainsObject = ObjC.Sel("containsObject:");
		private static readonly IntPtr SelDataWithBytesLength = ObjC.Sel("dataWithBytes:length:");
		private static readonly IntPtr SelBytes = ObjC.Sel("bytes");
		private static readonly IntPtr SelLength = ObjC.Sel("length");

		public bool ContainsText => this.GetString(TypeUtf8PlainText) != null;

		public bool ContainsHtml => this.GetString(TypeHtml) != null;

		public bool ContainsImage => MainThreadDispatcher.Invoke(() => HasType(TypePng) || HasType(TypeTiff));

		// See the class remarks: declined rather than faked.
		public bool ContainsFileDropList => false;

		public string GetText() => this.GetString(TypeUtf8PlainText) ?? string.Empty;

		public string GetHtml() => this.GetString(TypeHtml) ?? string.Empty;

		/// <summary>The pasteboard's image, or null when it holds none this can decode.</summary>
		public ImageBuffer GetImage()
			=> ClipboardImageCodec.Decode(MainThreadDispatcher.Invoke(() => GetData(TypePng) ?? GetData(TypeTiff)));

		public StringCollection GetFileDropList() => new StringCollection();

		public void SetText(string text) => MainThreadDispatcher.Invoke(() => SetTextOnMainThread(text));

		private static void SetTextOnMainThread(string text)
		{
			IntPtr pasteboard = GeneralPasteboard();

			// clearContents is mandatory before writing: without it the old flavors stay on the pasteboard
			// and a stale HTML representation would win over the plain text we just wrote.
			ObjC.Send_q(pasteboard, SelClearContents);
			ObjC.Send_B_r_r(pasteboard, SelSetStringForType, ObjC.NSString(text ?? string.Empty), ObjC.NSString(TypeUtf8PlainText));
		}

		public void SetTextAndHtml(string text, string html)
			=> MainThreadDispatcher.Invoke(() => SetTextAndHtmlOnMainThread(text, html));

		private static void SetTextAndHtmlOnMainThread(string text, string html)
		{
			IntPtr pasteboard = GeneralPasteboard();

			ObjC.Send_q(pasteboard, SelClearContents);
			ObjC.Send_B_r_r(pasteboard, SelSetStringForType, ObjC.NSString(text ?? string.Empty), ObjC.NSString(TypeUtf8PlainText));
			ObjC.Send_B_r_r(pasteboard, SelSetStringForType, ObjC.NSString(html ?? string.Empty), ObjC.NSString(TypeHtml));
		}

		/// <summary>Replaces the pasteboard's contents with the image as PNG. A null or empty image clears it.</summary>
		public void SetImage(ImageBuffer imageBuffer)
		{
			// Encoded off the main thread: it is the slow half, and it touches nothing AppKit owns.
			byte[] png = ClipboardImageCodec.EncodePng(imageBuffer);

			MainThreadDispatcher.Invoke(() =>
			{
				IntPtr pasteboard = GeneralPasteboard();
				ObjC.Send_q(pasteboard, SelClearContents);

				if (png != null)
				{
					ObjC.Send_B_r_r(pasteboard, SelSetDataForType, NSData(png), ObjC.NSString(TypePng));
				}
			});
		}

		/// <summary>An autoreleased NSData holding a copy of <paramref name="bytes"/>.</summary>
		private static unsafe IntPtr NSData(byte[] bytes)
		{
			fixed (byte* pointer = bytes)
			{
				return ObjC.Send_r_r_Q(ObjC.Class("NSData"), SelDataWithBytesLength, (IntPtr)pointer, (ulong)bytes.Length);
			}
		}

		/// <summary>Whether the general pasteboard advertises <paramref name="type"/>. Main thread only.</summary>
		private static bool HasType(string type)
		{
			IntPtr types = ObjC.Send_r(GeneralPasteboard(), SelTypes);
			return types != IntPtr.Zero && ObjC.Send_B_r(types, SelContainsObject, ObjC.NSString(type)) != 0;
		}

		/// <summary>One flavor's bytes off the general pasteboard, or null when absent. Main thread only.</summary>
		private static byte[] GetData(string type)
		{
			IntPtr data = ObjC.Send_r_r(GeneralPasteboard(), SelDataForType, ObjC.NSString(type));
			if (data == IntPtr.Zero)
			{
				return null;
			}

			ulong length = ObjC.Send_Q(data, SelLength);
			IntPtr bytes = ObjC.Send_r(data, SelBytes);
			if (length == 0 || bytes == IntPtr.Zero)
			{
				return null;
			}

			var copy = new byte[length];
			Marshal.Copy(bytes, copy, 0, (int)length);
			return copy;
		}

		private static IntPtr GeneralPasteboard() => ObjC.Send_r(ObjC.Class("NSPasteboard"), SelGeneralPasteboard);

		/// <summary>Reads one flavor off the general pasteboard, or null when it is not present.</summary>
		private string GetString(string type)
			=> MainThreadDispatcher.Invoke(
				() => ObjC.FromNSString(ObjC.Send_r_r(GeneralPasteboard(), SelStringForType, ObjC.NSString(type))));
	}
}
