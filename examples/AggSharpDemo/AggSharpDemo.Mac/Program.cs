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

using MatterHackers.Agg.UI;
using MatterHackers.AggSharpDemo.GuiDemo;

namespace MatterHackers.AggSharpDemo
{
	public static class AggSharpDemoMacProgram
	{
		public static void Main(string[] args)
		{
			// No provider setup: AggContext's per-OS defaults pick the AppKit host on a Mac, and this project
			// references PlatformMac so that host is in the output folder to be found.
			// The GUI demo remembers its windows and settings in ~/Library/Application Support/AggSharpDemo;
			// AGG_DEMO_STATE=<path> uses that file instead, so a smoke run can start from a prepared state
			// without touching the user's.
			string statePath = System.Environment.GetEnvironmentVariable("AGG_DEMO_STATE");
			var store = new FileDemoStateStore(string.IsNullOrEmpty(statePath) ? null : statePath);

			// The window opens at the size it had when the demo last closed (agg-gui state.rs window_w / window_h).
			(double width, double height) = DemoState.InitialOsWindowSize(DemoState.Parse(store.Load()), 1200, 800);
			var systemWindow = new SystemWindow(width, height)
			{
				Title = "agg-sharp demo",
			};
			// AGG_DEMO=<name> opens on that demo ("GUI Demo" for the GUI demo page), so a smoke screenshot (AGG_SMOKE_FRAMES/AGG_SMOKE_SCREENSHOT)
			// can show any of them.
			systemWindow.AddChild(new AggSharpDemoApp(System.Environment.GetEnvironmentVariable("AGG_DEMO"), store));
			systemWindow.ShowAsSystemWindow();
		}
	}
}
