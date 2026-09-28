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
using System.Threading.Tasks;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.Platform.Browser;
using MatterHackers.Agg.UI;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace MatterHackers.AggSharpDemo
{
	/// <summary>
	/// The browser twin of the mac head's <c>Main</c>. Blazor is only the loader: it boots the runtime,
	/// serves the static assets and, through <c>RunAsync</c>, keeps the runtime resident after
	/// <c>ShowAsSystemWindow</c> returns. agg owns the canvas and its frame loop.
	/// </summary>
	public static partial class AggSharpDemoBrowserProgram
	{
		/// <summary>The page's status line writer; null outside a browser (see examples/BrowserHost).</summary>
		private static IJSInProcessRuntime pageScript;

		[SupportedOSPlatform("browser")]
		public static async Task Main(string[] args)
		{
			WebAssemblyHost host = WebAssemblyHostBuilder.CreateDefault(args).Build();

			pageScript = host.Services.GetRequiredService<IJSRuntime>() as IJSInProcessRuntime;

			try
			{
				// The window, clipboard and dialogs call into JS modules that must be imported first, and an
				// import is a promise only a head can await.
				await BrowserHostBootstrap.InitializeAsync();

				// The GUI demo's saved layout and settings (localStorage), read synchronously while the page is built.
				await JSHost.ImportAsync(BrowserDemoStateStore.ModuleName, BrowserDemoStateStore.ModulePath);

				AggContext.Config.ProviderTypes.OsInformationProvider = AggContext.ProviderSettings.BrowserOsInformationProvider;
				AggContext.Config.ProviderTypes.DialogProvider = AggContext.ProviderSettings.BrowserDialogProvider;
				AggContext.Config.ProviderTypes.SystemWindowProvider = AggContext.ProviderSettings.BrowserSystemWindowProvider;

				// "This browser cannot run WebGPU" arrives before there is a canvas to draw it on.
				BrowserSystemWindow.ReportStatus = Report;

				var systemWindow = new SystemWindow(1200, 800)
				{
					Title = "agg-sharp demo",
				};
				systemWindow.AddChild(new AggSharpDemoApp(InitialDemoFromUrl(), new BrowserDemoStateStore(), followDisplayScale: true));
				systemWindow.ShowAsSystemWindow();

				Report(string.Empty);
			}
			catch (Exception startupException)
			{
				// Reported rather than rethrown: a throw here would take RunAsync with it and leave a page with
				// no runtime and no explanation.
				Report("startup failed: " + startupException);
			}

			await host.RunAsync();
		}

		/// <summary>
		/// Whether the page has really drawn, for a script driving it (scripts/check-demo-site.py): the
		/// painted frame count and whether the WebGPU device is up, as "paints N, renderer ready|not ready",
		/// or "no window" before there is one. Pulled by the script rather than written to the status line,
		/// which a visitor sees. A page screenshot alone cannot answer this - "loading..." on a dark page is
		/// already a picture.
		/// </summary>
		[SupportedOSPlatform("browser")]
		[JSExport]
		internal static string PaintState()
		{
			BrowserSystemWindow window = BrowserSystemWindow.Current;
			if (window == null)
			{
				return "no window";
			}

			return $"paints {window.FrameTick.PaintCount}, renderer {(window.RenderLayerReady ? "ready" : "not ready")}";
		}

		/// <summary>
		/// The page's URL fragment names the demo to open on, the browser twin of the mac head's AGG_DEMO:
		/// ".../#GUI%20Demo" opens the GUI demo. Null (the first AGG demo) when there is none.
		/// </summary>
		[SupportedOSPlatform("browser")]
		private static string InitialDemoFromUrl()
		{
			using JSObject location = JSHost.GlobalThis.GetPropertyAsJSObject("location");
			string hash = location?.GetPropertyAsString("hash");
			return string.IsNullOrEmpty(hash) || hash.Length < 2 ? null : Uri.UnescapeDataString(hash.Substring(1));
		}

		private static void Report(string message)
		{
			if (!string.IsNullOrEmpty(message))
			{
				Console.WriteLine(message);
			}

			pageScript?.InvokeVoid("aggHostStatus", message);
		}
	}
}
