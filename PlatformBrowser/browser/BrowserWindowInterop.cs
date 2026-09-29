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

namespace MatterHackers.Agg.Platform.Browser
{
	/// <summary>
	/// The DOM half of <see cref="BrowserSystemWindow"/>: the <c>[JSImport]</c>s into <c>input.js</c>.
	/// </summary>
	/// <remarks>
	/// <para>Everything browser-only lives here so that the window itself carries no platform attribute and
	/// can be constructed, ticked and resized by the desktop test suite. The class is <c>partial</c> because
	/// the interop source generator requires it.</para>
	/// <para><c>input.js</c> owns the canvas element as a whole - listeners, focus, cursor and the page title -
	/// rather than being split across a third module, because every one of those is a write to the element (or
	/// its document) that the listeners already hold a reference to, and two modules is the shortest import
	/// list a host page has to await.</para>
	/// </remarks>
	[SupportedOSPlatform("browser")]
	public sealed partial class BrowserWindowInterop : IBrowserWindowInterop
	{
		/// <summary>
		/// The name <see cref="BrowserHostBootstrap"/> imports <c>input.js</c> under; see
		/// <see cref="BrowserFrameLoop.ModuleName"/>.
		/// </summary>
		public const string ModuleName = "aggCanvasHost";

		/// <inheritdoc/>
		public BrowserBackingSize BindCanvas(string canvasSelector)
		{
			double[] metrics = BindCanvasCore(canvasSelector);

			if (metrics == null || metrics.Length < 5)
			{
				throw new InvalidOperationException(
					$"input.js bindCanvas('{canvasSelector}') did not report a device-pixel box, a CSS box and a "
					+ "devicePixelRatio. The module and this binding are out of step.");
			}

			return SizeCanvas(canvasSelector, metrics[0], metrics[1], metrics[2], metrics[3], metrics[4]);
		}

		/// <inheritdoc/>
		/// <remarks>
		/// File drag-and-drop is attached here too, after the input listeners whose state it joins, so
		/// <see cref="DetachInput"/> removes both and the window interface needs no drop method of its own.
		/// </remarks>
		public void AttachInput(string canvasSelector)
		{
			AttachInputCore(
				canvasSelector,
				BrowserInputEvents.DispatchInputEvent,
				canvasMetrics => BrowserInputEvents.DispatchResize(canvasSelector, canvasMetrics));

			AttachFileDropCore(canvasSelector, BrowserFileDropEvents.DispatchFileDragEvent);
		}

		/// <inheritdoc/>
		public void DetachInput(string canvasSelector) => DetachInputCore(canvasSelector);

		/// <inheritdoc/>
		public void SetCursor(string canvasSelector, string cssCursor) => SetCanvasCursor(canvasSelector, cssCursor);

		/// <inheritdoc/>
		public void SetDocumentTitle(string title) => SetDocumentTitleCore(title);

		/// <inheritdoc/>
		public void Focus(string canvasSelector) => FocusCanvas(canvasSelector);

		/// <summary>
		/// Decides the canvas's backing size from what the browser measured (see
		/// <see cref="BrowserBacking.FromDeviceMetrics(double, double, double, double, double)"/>) and sizes the
		/// canvas's backing store to it, so the canvas and what agg is told are the same number.
		/// </summary>
		internal static BrowserBackingSize SizeCanvas(
			string canvasSelector,
			double devicePixelWidth,
			double devicePixelHeight,
			double cssWidth,
			double cssHeight,
			double devicePixelRatio)
		{
			BrowserBackingSize backing = BrowserBacking.FromDeviceMetrics(
				devicePixelWidth, devicePixelHeight, cssWidth, cssHeight, devicePixelRatio);
			SetCanvasBackingSize(canvasSelector, backing.PixelWidth, backing.PixelHeight);
			return backing;
		}

		/// <summary>
		/// Prepares the canvas and reports <c>[devicePixelWidth, devicePixelHeight, cssWidth, cssHeight,
		/// devicePixelRatio]</c> - the device-pixel box is NaN, since only a resize observer entry carries one.
		/// An array rather than three calls because it is one measurement of one element at one moment - three
		/// round trips could straddle a resize.
		/// </summary>
		[JSImport("bindCanvas", ModuleName)]
		[return: JSMarshalAs<JSType.Array<JSType.Number>>]
		private static partial double[] BindCanvasCore(string canvasSelector);

		/// <summary>
		/// Subscribes the listeners, handing JS the two managed entry points as marshalled callbacks.
		/// </summary>
		/// <remarks>
		/// One callback for every input event, carrying a plain JS object of the fields agg needs, because a
		/// marshalled delegate takes at most three arguments and a pointer event has ten. Resize gets its own,
		/// also a plain object (two boxes and a ratio are five numbers), because it is not an input event at
		/// all; it is wrapped to carry the selector the canvas is resized through. See
		/// <see cref="BrowserFrameLoop.RunFrame"/> for why the callbacks are handed over rather than looked up.
		/// </remarks>
		[JSImport("attachInput", ModuleName)]
		private static partial void AttachInputCore(
			string canvasSelector,
			[JSMarshalAs<JSType.Function<JSType.Object>>] Action<JSObject> onInputEvent,
			[JSMarshalAs<JSType.Function<JSType.Object>>] Action<JSObject> onResize);

		/// <summary>
		/// Subscribes the canvas's drag-and-drop listeners. The callback answers each dragover with whether
		/// to show a copy; see <see cref="BrowserFileDrop"/>.
		/// </summary>
		[JSImport("attachFileDrop", ModuleName)]
		private static partial void AttachFileDropCore(
			string canvasSelector,
			[JSMarshalAs<JSType.Function<JSType.Object, JSType.Boolean>>] Func<JSObject, bool> onFileDrag);

		/// <summary>Sets the canvas's <c>width</c>/<c>height</c> attributes - its backing store.</summary>
		[JSImport("setCanvasBackingSize", ModuleName)]
		private static partial void SetCanvasBackingSize(string canvasSelector, double pixelWidth, double pixelHeight);

		[JSImport("detachInput", ModuleName)]
		private static partial void DetachInputCore(string canvasSelector);

		[JSImport("setCanvasCursor", ModuleName)]
		private static partial void SetCanvasCursor(string canvasSelector, string cssCursor);

		[JSImport("setDocumentTitle", ModuleName)]
		private static partial void SetDocumentTitleCore(string title);

		[JSImport("focusCanvas", ModuleName)]
		private static partial void FocusCanvas(string canvasSelector);
	}
}
