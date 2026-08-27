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
using System.Collections.Generic;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace MatterHackers.Agg.Platform.Browser
{
	/// <summary>
	/// The IndexedDB half of <see cref="BrowserStorageMirror"/>: <c>storageMirror.js</c>, behind
	/// <see cref="IBrowserStorageBackend"/>.
	/// </summary>
	/// <remarks>
	/// <para>Everything browser-only about persistence is in this file and that module. The mirror engine
	/// above it is plain <c>System.IO</c> and is exercised by the desktop suite against a dictionary; what
	/// cannot be tested off a browser is exactly these six calls, and they are each one line.</para>
	/// <para>The class is <c>partial</c> because the <c>[JSImport]</c> source generator requires it.</para>
	/// </remarks>
	[SupportedOSPlatform("browser")]
	public sealed partial class BrowserStorageMirrorInterop : IBrowserStorageBackend
	{
		/// <summary>
		/// The name <see cref="BrowserHostBootstrap"/> imports <c>storageMirror.js</c> under. Arbitrary, but it
		/// has to agree between the import and the <c>[JSImport]</c>s below.
		/// </summary>
		public const string ModuleName = "aggStorageMirror";

		/// <summary>
		/// The unload callback, held so the delegate marshalled into JS is not collected while the page still
		/// holds the listener. Same reason <see cref="BrowserFrameLoop"/> holds its tick.
		/// </summary>
		private static Action unloadFlush;

		/// <summary>
		/// Opens the named IndexedDB database. Returns immediately: the open is a promise every operation
		/// below queues behind, so nothing has to be awaited here (and a constructor could not await it
		/// anyway).
		/// </summary>
		/// <remarks>
		/// <see cref="BrowserHostBootstrap.InitializeAsync"/> must have completed - this calls into the module
		/// the moment it is constructed.
		/// </remarks>
		/// <param name="databaseName">Which database. See <see cref="MirrorPolicy.DatabaseName"/> for why an
		/// application may have more than one.</param>
		public BrowserStorageMirrorInterop(string databaseName)
		{
			OpenStore(databaseName);
		}

		/// <summary>
		/// Asks the page to call <paramref name="onUnload"/> when it is going away, so a mirror can make one
		/// last push. Best effort; see <c>storageMirror.js</c>.
		/// </summary>
		public static void InstallUnloadFlush(Action onUnload)
		{
			unloadFlush = onUnload ?? throw new ArgumentNullException(nameof(onUnload));

			InstallUnloadFlushCore(unloadFlush);
		}

		/// <remarks>
		/// The keys arrive one at a time through a callback, and the bytes below arrive wrapped in an object,
		/// because the interop generator marshals neither an array as a promise's result nor an array as a
		/// callback argument (SYSLIB1072). Both shapes are already load bearing in <c>peripherals.js</c> - the
		/// clipboard watch is a string callback and the file picker is an object callback carrying a
		/// Uint8Array - so this is the module pattern, not a workaround invented here. The callbacks are
		/// invoked before the promise settles, so the list is complete when the await returns.
		/// </remarks>
		public async Task<string[]> ListKeysAsync()
		{
			var keys = new List<string>();

			await ListKeys(key => keys.Add(key));

			return keys.ToArray();
		}

		/// <inheritdoc/>
		public async Task<byte[]> ReadAsync(string key)
		{
			byte[] bytes = null;

			// Left null when there is no such key: the module simply does not call back.
			await ReadEntry(key, entry => bytes = entry.GetPropertyAsByteArray("bytes"));

			return bytes;
		}

		public Task WriteAsync(string key, byte[] bytes) => WriteEntry(key, bytes);

		public Task DeleteAsync(string key) => DeleteEntry(key);

		[JSImport("openStore", ModuleName)]
		private static partial void OpenStore(string databaseName);

		[JSImport("listKeys", ModuleName)]
		[return: JSMarshalAs<JSType.Promise<JSType.Void>>]
		private static partial Task ListKeys(
			[JSMarshalAs<JSType.Function<JSType.String>>] Action<string> onKey);

		[JSImport("readEntry", ModuleName)]
		[return: JSMarshalAs<JSType.Promise<JSType.Void>>]
		private static partial Task ReadEntry(
			string key,
			[JSMarshalAs<JSType.Function<JSType.Object>>] Action<JSObject> onBytes);

		[JSImport("writeEntry", ModuleName)]
		[return: JSMarshalAs<JSType.Promise<JSType.Void>>]
		private static partial Task WriteEntry(string key, byte[] bytes);

		[JSImport("deleteEntry", ModuleName)]
		[return: JSMarshalAs<JSType.Promise<JSType.Void>>]
		private static partial Task DeleteEntry(string key);

		[JSImport("installUnloadFlush", ModuleName)]
		private static partial void InstallUnloadFlushCore([JSMarshalAs<JSType.Function>] Action onUnload);
	}
}
