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
	/// Drives the host from the browser's <c>requestAnimationFrame</c>.
	/// </summary>
	/// <remarks>
	/// <para>The browser owns the frame clock, so the loop has to be owned by JS: WebGPU in the browser has no
	/// present call, and a frame is whatever was recorded and submitted before the animation frame callback
	/// returned.</para>
	/// <para>The tick is handed over as a marshalled <see cref="Action"/> rather than being looked up from JS
	/// through <c>getAssemblyExports</c>, which is the fragile half of that pattern -
	/// the JS side would have to know an assembly name and a namespace path, and would fail at runtime rather
	/// than at build time when either moved. It is also a <c>[JSExport]</c> so a stuck frame can be single
	/// stepped from devtools.</para>
	/// <para>The class is <c>partial</c> because the <c>[JSImport]</c>/<c>[JSExport]</c> source generator
	/// requires it - a compiler contract, not a code organization choice.</para>
	/// <para>"Once per animation frame" is not the whole story while the page is hidden, where the browser
	/// serves no animation frames at all. The tick is not only what paints - it is what drains
	/// <see cref="UiThread"/>'s idle queue and advances its intervals - so <c>frameLoop.js</c> keeps calling
	/// it from a timer for as long as the page stays hidden. Nothing here has to know that, but a tick can
	/// therefore arrive from somewhere other than <c>requestAnimationFrame</c>, at a much coarser rate.</para>
	/// <para><b>One deviation from the spike:</b> a frame that throws does not stop the loop. The spike stopped
	/// it because a repeating exception buries its own cause in a demo; an application cannot afford it, since
	/// a dead loop leaves a window on screen with nothing pumping it - no input, no idle queue, no way to
	/// close. Containment lives in <see cref="BrowserFrameTick"/> instead, which reports through
	/// <see cref="UiThread.ReportUnhandledException"/> and keeps ticking; this catch is only the backstop for
	/// anything that gets past it.</para>
	/// </remarks>
	[SupportedOSPlatform("browser")]
	public sealed partial class BrowserFrameLoop : IBrowserFrameLoop
	{
		/// <summary>
		/// The name <see cref="BrowserHostBootstrap"/> imports <c>frameLoop.js</c> under. JS module names are
		/// arbitrary handles; this one has to agree between the import and the <c>[JSImport]</c>s below.
		/// </summary>
		public const string ModuleName = "aggFrameLoop";

		private static Action currentTick;

		/// <summary>
		/// Starts calling <paramref name="onFrame"/> once per animation frame. Any loop already running is
		/// replaced - <c>startFrameLoop</c> cancels the outstanding frame first.
		/// </summary>
		public void Start(Action onFrame)
		{
			currentTick = onFrame ?? throw new ArgumentNullException(nameof(onFrame));

			StartFrameLoop(RunFrame);
		}

		/// <summary>Stops the loop, if one is running.</summary>
		public void Stop()
		{
			StopFrameLoop();
			currentTick = null;
		}

		/// <summary>
		/// Runs exactly one tick. Called by <c>requestAnimationFrame</c>, once per frame, and exported so it
		/// can be poked from devtools when a frame is being investigated.
		/// </summary>
		[JSExport]
		internal static void RunFrame()
		{
			Action tick = currentTick;
			if (tick == null)
			{
				return;
			}

			try
			{
				tick();
			}
			catch (Exception frameException)
			{
				// Backstop only; see the class remarks for why the loop keeps running. Letting this reach JS
				// would leave an exception in the console that nothing in agg ever hears about.
				Console.Error.WriteLine($"BrowserFrameLoop tick threw: {frameException}");
				UiThread.ReportUnhandledException(frameException);
			}
		}

		[JSImport("startFrameLoop", ModuleName)]
		private static partial void StartFrameLoop([JSMarshalAs<JSType.Function>] Action onFrame);

		[JSImport("stopFrameLoop", ModuleName)]
		private static partial void StopFrameLoop();
	}
}
