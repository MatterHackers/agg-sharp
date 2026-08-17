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
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform.Mac;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// The macOS <see cref="ISystemClipboard"/>, backed by <c>[NSPasteboard generalPasteboard]</c>.
	/// The peer of PlatformWin32's <c>WindowsFormsClipboard</c>; an app installs it with
	/// <c>Clipboard.SetSystemClipboard(new MacClipboard())</c>.
	/// <para>
	/// Text and HTML round trip. Images and file drop lists report "not present" rather than pretending:
	/// nothing on macOS asks for them yet, and faking a capability is worse than declining it. When a
	/// caller does need them, NSPasteboard's <c>NSPasteboardTypePNG</c> and <c>NSFilenamesPboardType</c>
	/// are the hooks to fill in here.
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

		private static readonly IntPtr SelGeneralPasteboard = ObjC.Sel("generalPasteboard");
		private static readonly IntPtr SelStringForType = ObjC.Sel("stringForType:");
		private static readonly IntPtr SelSetStringForType = ObjC.Sel("setString:forType:");
		private static readonly IntPtr SelClearContents = ObjC.Sel("clearContents");

		public bool ContainsText => this.GetString(TypeUtf8PlainText) != null;

		public bool ContainsHtml => this.GetString(TypeHtml) != null;

		// See the class remarks: declined rather than faked.
		public bool ContainsImage => false;

		public bool ContainsFileDropList => false;

		public string GetText() => this.GetString(TypeUtf8PlainText) ?? string.Empty;

		public string GetHtml() => this.GetString(TypeHtml) ?? string.Empty;

		public ImageBuffer GetImage() => null;

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

		public void SetImage(ImageBuffer imageBuffer)
		{
		}

		private static IntPtr GeneralPasteboard() => ObjC.Send_r(ObjC.Class("NSPasteboard"), SelGeneralPasteboard);

		/// <summary>Reads one flavor off the general pasteboard, or null when it is not present.</summary>
		private string GetString(string type)
			=> MainThreadDispatcher.Invoke(
				() => ObjC.FromNSString(ObjC.Send_r_r(GeneralPasteboard(), SelStringForType, ObjC.NSString(type))));
	}
}
