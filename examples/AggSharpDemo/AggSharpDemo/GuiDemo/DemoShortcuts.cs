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
using System.Linq;
using System.Text;
using MatterHackers.Agg.UI;

namespace MatterHackers.AggSharpDemo.GuiDemo
{
	/// <summary>
	/// The GUI demo's page-wide keys, as agg-gui's app_builder.rs global key handler has them: Ctrl+Shift+O
	/// organizes the windows, Ctrl+Shift+R closes every demo and test window, Ctrl+Shift+D writes a draw
	/// diagnostic report. On a Mac Command works too - the Mac host reports Command as Control.
	/// </summary>
	public static class DemoShortcuts
	{
		/// <summary>Acts on <paramref name="keyEvent"/> if it is one of the shortcuts; returns whether it was.</summary>
		public static bool Handle(GuiDemoShell shell, KeyEventArgs keyEvent)
		{
			if (!keyEvent.Control
				|| !keyEvent.Shift
				|| keyEvent.Alt)
			{
				return false;
			}

			switch (keyEvent.KeyCode)
			{
				case Keys.O:
					shell.Windows.Organize();
					return true;

				case Keys.R:
					// The specs.rs demo and test windows; About and the Inspector are the shell's, as in agg-gui.
					foreach (DemoSpec spec in GuiDemoSpecs.All)
					{
						shell.Windows.SetOpen(spec, false);
					}

					return true;

				case Keys.D:
					string report = BuildDrawReport(shell);
					Console.Error.WriteLine();
					Console.Error.WriteLine("[agg-sharp draw report - manual Ctrl+Shift+D]");
					Console.Error.WriteLine(report);
					shell.OnDrawReport(report);
					return true;
			}

			return false;
		}

		/// <summary>
		/// agg-gui's debug_draw_report for agg-sharp: what keeps the page drawing - the run mode and the UI thread's
		/// scheduled callbacks (the counterpart of agg-gui's draw flag and deadline) - and the open windows.
		/// </summary>
		public static string BuildDrawReport(GuiDemoShell shell)
		{
			var report = new StringBuilder();
			report.AppendLine("== agg-sharp draw report ==");
			report.AppendLine($"run mode: {shell.BackendPanel.RunMode}{(shell.RedrawsContinuously ? " (redraws every frame)" : "")}");
			report.AppendLine($"scheduled UI callbacks: {UiThread.Count} ({UiThread.CountExpired} due)");

			var widgets = shell.Descendants<GuiWidget>().ToList();
			report.AppendLine($"widgets: {widgets.Count} ({widgets.Count(w => w.DoubleBuffer)} double buffered)");

			var open = shell.Windows.ZOrder;
			report.AppendLine($"open windows ({open.Count}, back to front):");
			foreach (DemoSpec spec in open)
			{
				WindowWidget window = shell.Windows.GetWindow(spec);
				report.AppendLine($"  {spec.Title}: {window.Width:0} x {window.Height:0} at ({window.Position.X:0}, {window.Position.Y:0}){(window.Maximized ? " maximized" : "")}{(window.Collapsed ? " collapsed" : "")}");
			}

			return report.ToString();
		}
	}
}
