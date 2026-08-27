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
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using MatterHackers.Agg.UI;

namespace MatterHackers.Agg.Platform.Browser
{
	/// <summary>
	/// The page half of the three peripheral providers: the <c>[JSImport]</c>s into <c>peripherals.js</c>.
	/// </summary>
	/// <remarks>
	/// <para>One class implementing all three seams because they are one module's worth of page - the
	/// clipboard, the file picker and the screen - in the same way <see cref="BrowserWindowInterop"/> is one
	/// class for the whole of the canvas. Nothing here holds state, so a provider may construct its own.</para>
	/// <para><b>Why a third JS module rather than more of <c>input.js</c>.</b> The split is by owner, not by
	/// feature count: <c>input.js</c> is everything the <em>window</em> needs the DOM for, and its listeners
	/// all hold the one canvas element. None of what is here touches the canvas - the clipboard is
	/// <c>navigator</c>, the picker is a detached <c>&lt;input&gt;</c>, the screen is <c>window.screen</c> -
	/// and the clipboard's focus watch is a <c>window</c> listener that would otherwise have to be threaded
	/// through the canvas host that has no business knowing about it. Three modules, one per owner
	/// (frame loop, window, providers), is where this stops; a fourth would be a feature split and is what
	/// the two-module note in <c>input.js</c> is warning against.</para>
	/// </remarks>
	[SupportedOSPlatform("browser")]
	public sealed partial class BrowserPeripherals : IBrowserClipboardInterop, IBrowserFileDialogInterop, IBrowserScreenInterop
	{
		/// <summary>
		/// The name <see cref="BrowserHostBootstrap"/> imports <c>peripherals.js</c> under; see
		/// <see cref="BrowserFrameLoop.ModuleName"/>.
		/// </summary>
		public const string ModuleName = "aggPeripherals";

		/// <inheritdoc/>
		public void StartWatchingSystemText(Action<string> onText)
		{
			if (onText == null)
			{
				throw new ArgumentNullException(nameof(onText));
			}

			StartClipboardWatchCore(onText);
		}

		/// <inheritdoc/>
		public void WriteText(string text) => WriteClipboardTextCore(text ?? string.Empty);

		/// <inheritdoc/>
		public void PickFiles(string accept, bool multiple, Action<BrowserPickedFile> onFile, Action onComplete)
		{
			if (onFile == null)
			{
				throw new ArgumentNullException(nameof(onFile));
			}

			if (onComplete == null)
			{
				throw new ArgumentNullException(nameof(onComplete));
			}

			PickFilesCore(
				accept ?? string.Empty,
				multiple,
				picked => onFile(UnpackPickedFile(picked)),
				onComplete);
		}

		/// <inheritdoc/>
		public void DownloadFile(string fileName, byte[] bytes) => DownloadFileCore(fileName, bytes);

		/// <inheritdoc/>
		public double[] ReadScreenMetrics()
		{
			double[] metrics = ReadScreenMetricsCore();

			if (metrics == null || metrics.Length < 4)
			{
				throw new InvalidOperationException(
					"peripherals.js readScreenMetrics() did not report a width, height, devicePixelRatio and "
					+ "memory figure. The module and this binding are out of step.");
			}

			return metrics;
		}

		/// <summary>
		/// Reads the <c>{ name, bytes }</c> bag <c>peripherals.js</c> hands over for each chosen file.
		/// </summary>
		/// <remarks>
		/// A bag rather than two callback arguments for the reason <c>input.js</c> packs its events into
		/// one: it keeps the marshalling of a <c>Uint8Array</c> and a name in a single place that can be read
		/// against the JS side. The bytes arrive as a copy - the wasm heap owns them from here on, and the
		/// browser's <c>ArrayBuffer</c> is free to be collected.
		/// </remarks>
		private static BrowserPickedFile UnpackPickedFile(JSObject picked)
			=> new BrowserPickedFile(
				picked.GetPropertyAsString("name") ?? string.Empty,
				picked.GetPropertyAsByteArray("bytes") ?? Array.Empty<byte>());

		[JSImport("startClipboardWatch", ModuleName)]
		private static partial void StartClipboardWatchCore(
			[JSMarshalAs<JSType.Function<JSType.String>>] Action<string> onText);

		[JSImport("writeClipboardText", ModuleName)]
		private static partial void WriteClipboardTextCore(string text);

		[JSImport("pickFiles", ModuleName)]
		private static partial void PickFilesCore(
			string accept,
			bool multiple,
			[JSMarshalAs<JSType.Function<JSType.Object>>] Action<JSObject> onFile,
			[JSMarshalAs<JSType.Function>] Action onComplete);

		[JSImport("downloadFile", ModuleName)]
		private static partial void DownloadFileCore(string fileName, byte[] bytes);

		[JSImport("readScreenMetrics", ModuleName)]
		[return: JSMarshalAs<JSType.Array<JSType.Number>>]
		private static partial double[] ReadScreenMetricsCore();

		// -----------------------------------------------------------------------------------------
		// Faults raised by the module itself
		// -----------------------------------------------------------------------------------------

		/// <summary>
		/// Gives <c>peripherals.js</c> somewhere to report its own failures. Called once by
		/// <see cref="BrowserHostBootstrap"/> after the module is imported.
		/// </summary>
		/// <remarks>
		/// <para>The module's work happens inside promise continuations and DOM listeners, where a throw is
		/// reported to the browser console and to nobody that agg can see. A clipboard write that the browser
		/// refuses, a download the page blocked, a file that could not be read - all of them would otherwise
		/// be a menu item that silently does nothing.</para>
		/// <para>Handed over as a marshalled callback rather than exposed as a <c>[JSExport]</c> for the
		/// reason <c>attachInput</c>'s callbacks are: reaching an export from inside a JS module means going
		/// back through the runtime's <c>getAssemblyExports</c>, which is asynchronous and would have to
		/// happen before the first failure rather than after it.</para>
		/// </remarks>
		public static void InstallFaultReporter() => SetFaultReporterCore(ReportFault);

		/// <summary>
		/// Routes a JS-side failure to the channel the crash reporter and the automation tests both watch -
		/// the same one <see cref="BrowserInputEvents"/> uses.
		/// </summary>
		private static void ReportFault(string what, string message)
		{
			var failure = new InvalidOperationException($"Browser {what} failed: {message}");

			Console.Error.WriteLine(failure.Message);
			UiThread.ReportUnhandledException(failure);
		}

		[JSImport("setFaultReporter", ModuleName)]
		private static partial void SetFaultReporterCore(
			[JSMarshalAs<JSType.Function<JSType.String, JSType.String>>] Action<string, string> onFault);
	}
}
