/*
Copyright (c) 2026, Lars Brubaker
All rights reserved.
*/

using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.LcdCoverage;
using MatterHackers.Agg.UI;
using MatterHackers.AggSharpDemo.GuiDemo;
using MatterHackers.AggSharpDemo.GuiDemo.Windows.Tools;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	// DemoState's System window settings and tab, and the OS window size (agg-gui state.rs font/lcd/hinting/gamma,
	// system_tab, window_w/window_h). The settings are process-wide statics every text test reads, so these are
	// keyless [NotInParallel] and put every setting back in a finally block.
	public class DemoStateSystemSettingsTests
	{
		private static DemoSpec SystemSpec => GuiDemoSpecs.All.First(s => s.Title == "System");

		private static GuiDemoShell LaidOutShell(IDemoStateStore store, GuiWidget page)
		{
			var shell = new GuiDemoShell(new DemoTheme(), store);
			page.AddChild(shell);
			page.PerformLayout();
			return shell;
		}

		private static SystemTypographyWindow OpenSystem(GuiDemoShell shell)
		{
			shell.Windows.SetOpen(SystemSpec, true);
			return (SystemTypographyWindow)shell.Windows.GetWindow(SystemSpec).FindDescendant(SystemSpec.ContentName);
		}

		[Test]
		[NotInParallel]
		public async Task SystemSettingsAndTabComeBackOnTheNextRun()
		{
			bool wasLcd = LcdRenderSettings.Enabled;
			bool wasHinting = TypeFacePrinter.SnapBaselinesToWholePixels;
			double wasGamma = LcdRenderSettings.Gamma;
			double wasWeight = LcdRenderSettings.PrimaryWeight;
			try
			{
				var store = new MemoryStore();
				GuiDemoShell first = LaidOutShell(store, new GuiWidget(1400, 1000));
				SystemTypographyWindow system = OpenSystem(first);
				system.FontTab.Lcd.Checked = !wasLcd;
				await Assert.That(first.Persistence.SavePending).IsTrue();
				system.FontTab.Hinting.Checked = !wasHinting;
				system.FontTab.Gamma.Value = 1.7;
				system.FontTab.PrimaryWeight.Value = .4;
				system.Tabs.SelectedIndex = 1;
				first.Close();

				// Another run starts from the defaults the process had.
				LcdRenderSettings.Enabled = wasLcd;
				TypeFacePrinter.SnapBaselinesToWholePixels = wasHinting;
				LcdRenderSettings.Gamma = wasGamma;
				LcdRenderSettings.PrimaryWeight = wasWeight;

				GuiDemoShell second = LaidOutShell(store, new GuiWidget(1400, 1000));
				await Assert.That(LcdRenderSettings.Enabled).IsEqualTo(!wasLcd);
				await Assert.That(TypeFacePrinter.SnapBaselinesToWholePixels).IsEqualTo(!wasHinting);
				await Assert.That(LcdRenderSettings.Gamma).IsEqualTo(1.7).Within(1e-9);
				await Assert.That(LcdRenderSettings.PrimaryWeight).IsEqualTo(.4).Within(1e-9);
				await Assert.That(OpenSystem(second).Tabs.SelectedIndex).IsEqualTo(1);
				second.Close();
			}
			finally
			{
				LcdRenderSettings.Enabled = wasLcd;
				TypeFacePrinter.SnapBaselinesToWholePixels = wasHinting;
				LcdRenderSettings.Gamma = wasGamma;
				LcdRenderSettings.PrimaryWeight = wasWeight;
			}
		}

		[Test]
		[NotInParallel]
		public async Task TheOsWindowSizeIsSavedAndADesktopHeadOpensAtIt()
		{
			var store = new MemoryStore();
			var osWindow = new SystemWindow(900, 650);
			GuiDemoShell shell = LaidOutShell(store, osWindow);
			shell.Persistence.SaveNow();
			DemoState saved = DemoState.Parse(store.Json);
			await Assert.That((saved.OsWindowWidth, saved.OsWindowHeight)).IsEqualTo((900.0, 650.0));
			await Assert.That(DemoState.InitialOsWindowSize(saved, 1200, 800)).IsEqualTo((900.0, 650.0));

			// Nothing saved, or a size too small to use, opens at the head's default.
			await Assert.That(DemoState.InitialOsWindowSize(new DemoState(), 1200, 800)).IsEqualTo((1200.0, 800.0));
			await Assert.That(DemoState.InitialOsWindowSize(new DemoState { OsWindowWidth = 10, OsWindowHeight = 10 }, 1200, 800)).IsEqualTo((1200.0, 800.0));
			osWindow.Close();
		}

		private class MemoryStore : IDemoStateStore
		{
			public string Json { get; private set; }

			public string Load() => this.Json;

			public void Save(string json) => this.Json = json;

			public void Clear() => this.Json = null;
		}
	}
}
