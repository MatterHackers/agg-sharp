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
using System.Runtime.Versioning;
using System.Threading.Tasks;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.Platform.Browser;
using MatterHackers.Agg.UI;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace MatterHackers.Agg.Examples
{
	/// <summary>
	/// The browser twin of a desktop demo's <c>Main</c>: bring the platform up, point agg at its providers,
	/// show a window, and let the host keep the runtime alive.
	/// </summary>
	/// <remarks>
	/// <para>Blazor is only the loader here. There are no Razor components and no root component is
	/// registered: agg owns the canvas and its own <c>requestAnimationFrame</c> loop, and all Blazor
	/// contributes is booting the .NET runtime, serving the app's static assets, and - through
	/// <c>RunAsync</c> - never returning, which is what keeps the runtime resident after
	/// <c>ShowAsSystemWindow</c> has returned (see <see cref="BrowserSystemWindow"/>'s class remarks for
	/// why it returns at all).</para>
	/// <para>The page is dark for the first few frames and then paints: the WebGPU device is created
	/// asynchronously and the window ticks throughout - draining input, laying out, running idle work -
	/// until it exists. What this host proves is the whole browser platform: the JS modules load, the
	/// provider strings resolve to real types, the tick advances, input translates, the idle queue drains,
	/// and the widget tree reaches the canvas. It narrates itself through the page's status line and the
	/// browser console, which is also where a browser with no WebGPU is told it cannot run this.</para>
	/// <para>Painting needs the emdawnwebgpu link: publish with <c>-p:LinkEmdawnWebGpu=true</c> (see the
	/// README). A plain build still boots, ticks and reports - the device creation simply fails, with the
	/// same message a browser that has no WebGPU gets.</para>
	/// </remarks>
	public static class BrowserHostProgram
	{
		/// <summary>How often the status line reports tick counts. Slow on purpose: it is a heartbeat, not a
		/// frame counter, and every update is a JS call and a DOM write.</summary>
		private const double HeartbeatSeconds = 1;

		/// <summary>
		/// The page's <c>aggHostStatus</c> function, or null if the page did not define one.
		/// </summary>
		/// <remarks>
		/// In-process rather than the async <see cref="IJSRuntime"/> so that a widget's event handler - which
		/// is deep inside a synchronous agg call stack - can report without an await. That is only legal
		/// because wasm is single threaded; on a desktop Blazor host this cast returns null and the host
		/// falls back to the console.
		/// </remarks>
		private static IJSInProcessRuntime pageScript;

		/// <summary>The most recent input event, shown by the heartbeat. Written from widget events.</summary>
		private static string lastInput = "no input yet";

		/// <remarks>
		/// Attributed because the project's TFM is plain <c>net10.0</c> (see PlatformBrowser.csproj for why
		/// that assembly is), so nothing else tells the compatibility analyzer that this entry point only
		/// ever runs in a browser - and <see cref="BrowserHostBootstrap.InitializeAsync"/> is declared
		/// browser-only.
		/// </remarks>
		[SupportedOSPlatform("browser")]
		public static async Task Main(string[] args)
		{
			WebAssemblyHost host = WebAssemblyHostBuilder.CreateDefault(args).Build();

			pageScript = host.Services.GetRequiredService<IJSRuntime>() as IJSInProcessRuntime;

			try
			{
				// Before anything touches agg: the window, the clipboard and the file dialogs all call into
				// modules that have to be imported first, and an import is a promise only a head can await.
				await BrowserHostBootstrap.InitializeAsync();

				StartDemo();
			}
			catch (Exception startupException)
			{
				// Reported rather than rethrown: a throw here would take RunAsync with it and leave a page
				// with no runtime and no explanation but Blazor's generic error strip.
				Report("startup failed: " + startupException);
			}

			await host.RunAsync();
		}

		/// <summary>
		/// Writes one line to the page's status element and to the console. The console copy is the one that
		/// survives a page that failed before its script ran.
		/// </summary>
		internal static void Report(string message)
		{
			Console.WriteLine(message);

			pageScript?.InvokeVoid("aggHostStatus", message);
		}

		/// <summary>Records an input event for the next heartbeat, and echoes it to the console.</summary>
		internal static void ReportInput(string message)
		{
			lastInput = message;

			Console.WriteLine(message);
		}

		/// <summary>
		/// Configures agg for the browser and shows the demo window.
		/// </summary>
		private static void StartDemo()
		{
			// Set explicitly even though AggContext's per-OS defaults already resolve to these under wasm: a
			// head is the one place the provider choice is meant to be readable, and a browser head that
			// silently depended on the default would break quietly if the default ever moved.
			AggContext.Config.ProviderTypes.OsInformationProvider = AggContext.ProviderSettings.BrowserOsInformationProvider;
			AggContext.Config.ProviderTypes.DialogProvider = AggContext.ProviderSettings.BrowserDialogProvider;
			AggContext.Config.ProviderTypes.SystemWindowProvider = AggContext.ProviderSettings.BrowserSystemWindowProvider;

			// The window has one thing to say that a user must actually see, and it says it before there is
			// any canvas to draw it on: "this browser cannot run WebGPU". The status line is this host's only
			// place to put it.
			BrowserSystemWindow.ReportStatus = Report;

			// Exactly what a desktop demo's Main does - and here it returns instead of blocking.
			var demoWindow = new BrowserHostDemoWindow();
			demoWindow.ShowAsSystemWindow();

			Report(
				$"agg is up on {AggContext.OperatingSystem}"
				+ $", window provider {SystemWindow.Provider.GetType().Name}"
				+ $", canvas {BrowserSystemWindow.Current.Backing}");

			// Where the button ended up. It is visible now, so this is no longer the only way to find it -
			// but it is still what a script driving the page clicks. Screen space is agg's, so y counts up
			// from the bottom of the canvas; a script has to flip it.
			Console.WriteLine($"button at (agg screen space) {demoWindow.ButtonScreenBounds}");

			Heartbeat();
		}

		/// <summary>
		/// Reports the tick and paint counts, then re-queues itself. Proof that the frame loop is running and
		/// that the idle queue is being drained - the two things a host with no renderer can still show.
		/// </summary>
		private static void Heartbeat()
		{
			BrowserSystemWindow window = BrowserSystemWindow.Current;

			if (window == null)
			{
				return;
			}

			Report(
				$"ticks {window.FrameTick.TickCount}, paints {window.FrameTick.PaintCount}"
				+ $", renderer {(window.RenderLayerReady ? "ready" : "not ready")}, last input: {lastInput}");

			UiThread.RunOnIdle(Heartbeat, HeartbeatSeconds);
		}
	}

	/// <summary>
	/// The demo: a button that counts clicks, a text box, and event handlers that say what arrived.
	/// </summary>
	/// <remarks>
	/// Small on purpose, and chosen for what each piece exercises rather than for what it looks like. The
	/// button proves the pointer path all the way to a widget's <c>Click</c>; the text box proves keyboard
	/// translation and focus, and its I-beam proves the cursor round trip into CSS; the window's own
	/// handlers report the raw events under both.
	/// </remarks>
	public class BrowserHostDemoWindow : SystemWindow
	{
		private readonly TextWidget clickCountText;

		private readonly Button clickButton;

		private int clickCount;

		public BrowserHostDemoWindow()
			: base(800, 600)
		{
			this.Title = "agg browser host";
			this.BackgroundColor = new Color(30, 30, 30);

			// Fit as well as Center: Center on its own leaves the column zero-sized, and a parent whose local
			// bounds are empty fails the hit test that dispatches a click on to its children - the widgets
			// would draw (once there is a renderer) but never be clickable.
			var column = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Center | HAnchor.Fit,
				VAnchor = VAnchor.Center | VAnchor.Fit,
			};

			this.clickCountText = new TextWidget("clicked 0 times", pointSize: 16, textColor: Color.White);
			column.AddChild(this.clickCountText);

			this.clickButton = new Button("Click me");
			this.clickButton.Click += (sender, e) =>
			{
				this.clickCount++;
				this.clickCountText.Text = $"clicked {this.clickCount} times";

				BrowserHostProgram.ReportInput($"button click {this.clickCount}");
			};
			column.AddChild(this.clickButton);

			column.AddChild(new TextEditWidget("type here", pixelWidth: 200));

			this.AddChild(column);

			this.MouseDown += (sender, e) => BrowserHostProgram.ReportInput($"mouse down {e.Button} at {e.X}, {e.Y}");
			this.KeyDown += (sender, e) => BrowserHostProgram.ReportInput($"key down {e.KeyCode}");
		}

		/// <summary>Where the button is, in agg screen space. Only useful because nothing paints yet.</summary>
		public RectangleDouble ButtonScreenBounds
			=> this.clickButton.TransformToScreenSpace(this.clickButton.LocalBounds);
	}
}
