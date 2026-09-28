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
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.AggSharpDemo.GuiDemo;
using MatterHackers.AggSharpDemo.GuiDemo.Windows.Graphics;
using MatterHackers.GuiAutomation;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	// The GUI demo's Screenshot window (agg-gui's screenshot_demo.rs).
	[NotInParallel(new[] { nameof(AutomationRunner.ShowWindowAndExecuteTests), nameof(MatterHackers.Agg.UI.ThemeConfig.Current) })]
	public class ScreenshotWindowTests
	{
		private static DemoSpec Spec => GuiDemoSpecs.All.First(s => s.Title == "Screenshot");

		[Test]
		public async Task BuildsTheControlsWithNothingCaptured()
		{
			var demoTheme = new DemoTheme(ThemePreference.Light);
			var window = (ScreenshotWindow)GuiDemoSpecs.CreateContent(Spec, demoTheme);
			var host = new GuiWidget(Spec.DefaultWidth, Spec.DefaultHeight);
			host.AddChild(window);
			host.PerformLayout();

			await Assert.That(window.Name).IsEqualTo("Screenshot Content");
			await Assert.That(window.FindDescendant("Screenshot Take")).IsSameReferenceAs(window.TakeButton);
			await Assert.That(window.FindDescendant("Screenshot Continuous")).IsSameReferenceAs(window.ContinuousCheckBox);
			await Assert.That(window.FindDescendant("Screenshot Save")).IsSameReferenceAs(window.SaveButton);
			await Assert.That(window.FindDescendant("Screenshot Copy")).IsSameReferenceAs(window.CopyButton);
			await Assert.That(window.FindDescendant("Screenshot Preview")).IsSameReferenceAs(window.Preview);
			await Assert.That(window.Preview.Image).IsNull();
			await Assert.That(window.SaveButton.Enabled).IsFalse();
			await Assert.That(window.CopyButton.Enabled).IsFalse();
			await Assert.That(window.Preview.Height).IsGreaterThan(0);

			var image = new ImageBuffer((int)window.Width, (int)window.Height);
			window.OnDraw(image.NewGraphics2D());
			demoTheme.SetPreference(ThemePreference.Dark);
			await Assert.That(window.BackgroundColor).IsEqualTo(demoTheme.Palette.PanelFill);
			window.OnDraw(image.NewGraphics2D());
		}

		[Test]
		public async Task TheExportButtonIsADownloadInABrowser()
		{
			var browser = new ScreenshotWindow(new DemoTheme(ThemePreference.Light), isBrowser: true);
			var desktop = new ScreenshotWindow(new DemoTheme(ThemePreference.Light), isBrowser: false);

			await Assert.That(browser.SaveButton.Text).IsEqualTo("Download");
			await Assert.That(desktop.SaveButton.Text).IsEqualTo("Save...");
		}

		[Test]
		public async Task ThePreviewFitsTheCaptureCentredWithItsAspectKept()
		{
			RectangleDouble fit = ScreenshotPreview.FitRect(200, 100, new RectangleDouble(0, 0, 100, 100));

			await Assert.That(fit).IsEqualTo(new RectangleDouble(0, 25, 100, 75));
		}

		[Test]
		public async Task ACaptureIsTheWholeAppAndEnablesTheExports()
		{
			var (window, host) = HostedWindow();
			window.Capture();

			ImageBuffer capture = window.Preview.Image;
			await Assert.That(capture).IsNotNull();
			await Assert.That(capture.Width).IsEqualTo((int)host.Width);
			await Assert.That(capture.Height).IsEqualTo((int)host.Height);
			// The sibling beside the window is in the capture, so it is the app, not just this window.
			await Assert.That(capture.GetPixel(10, 10)).IsEqualTo(Color.Red);
			await Assert.That(window.SaveButton.Enabled).IsTrue();
			await Assert.That(window.CopyButton.Enabled).IsTrue();

			// The next capture draws the preview, which shows the last capture: it must go into another image,
			// or it reads the pixels it is overwriting. The one after reuses the first rather than making the GPU
			// upload a new texture per capture.
			window.Capture();
			ImageBuffer second = window.Preview.Image;
			await Assert.That(second).IsNotSameReferenceAs(capture);
			window.Capture();
			await Assert.That(window.Preview.Image).IsSameReferenceAs(capture);
		}

		[Test]
		public async Task SaveWritesThePngWhereTheDialogSays()
		{
			string chosen = Path.Combine(Path.GetTempPath(), "ScreenshotWindowTests-" + Guid.NewGuid().ToString("N"));
			var dialogs = new ChoosingFileDialogs(chosen);
			var (window, host) = HostedWindow(dialogs);
			window.Capture();
			try
			{
				window.Save();

				await Assert.That(dialogs.AskedFilter).IsEqualTo("PNG Image|*.png");
				await Assert.That(window.LastSavedPath).IsEqualTo(chosen + ".png");
				ImageBuffer saved = ImageIO.LoadImage(chosen + ".png");
				await Assert.That(saved.Width).IsEqualTo((int)host.Width);
				await Assert.That(saved.Height).IsEqualTo((int)host.Height);
			}
			finally
			{
				File.Delete(chosen + ".png");
			}
		}

		[Test]
		public async Task TakeScreenshotAndCaptureContinuouslyCapture()
		{
			var window = (ScreenshotWindow)GuiDemoSpecs.CreateContent(Spec, new DemoTheme(ThemePreference.Light));
			var systemWindow = new SystemWindow(420, 360) { Name = "Screenshot Test Window" };
			systemWindow.AddChild(window);

			await AutomationRunner.ShowWindowAndExecuteTests(systemWindow, async testRunner =>
			{
				testRunner.ClickByName("Screenshot Take");
				testRunner.WaitFor(() => window.CaptureCount == 1);
				await Assert.That(window.Preview.Image.Width).IsEqualTo((int)window.Width);

				// Continuous capture keeps capturing on its own, off the preview's draws.
				testRunner.ClickByName("Screenshot Continuous");
				testRunner.WaitFor(() => window.CaptureCount >= 4);
				testRunner.ClickByName("Screenshot Continuous");
				testRunner.WaitFor(() => !window.ContinuousCheckBox.Checked);
				testRunner.MarkTestComplete();
			});
		}

		/// <summary>The window beside a red block in one host, as the shell holds it beside its sidebar.</summary>
		private static (ScreenshotWindow Window, GuiWidget Host) HostedWindow(IFileDialogProvider dialogs = null)
		{
			var window = new ScreenshotWindow(new DemoTheme(ThemePreference.Light), dialogs)
			{
				HAnchor = HAnchor.Absolute,
				VAnchor = VAnchor.Absolute,
				Width = 300,
				Height = 260,
				OriginRelativeParent = new VectorMath.Vector2(100, 0),
			};
			var host = new GuiWidget(400, 260);
			host.AddChild(new GuiWidget(40, 40) { BackgroundColor = Color.Red });
			host.AddChild(window);
			host.PerformLayout();
			return (window, host);
		}

		/// <summary>A save dialog in which the user picks <c>chosen</c> (no extension) at once.</summary>
		private sealed class ChoosingFileDialogs : IFileDialogProvider
		{
			private readonly string chosen;

			public ChoosingFileDialogs(string chosen)
			{
				this.chosen = chosen;
			}

			public string AskedFilter { get; private set; }

			public string LastDirectoryUsed => Path.GetDirectoryName(this.chosen);

			public string ResolveFilePath(string path) => path;

			public bool OpenFileDialog(OpenFileDialogParams openParams, Action<OpenFileDialogParams> callback) => false;

			public bool SelectFolderDialog(SelectFolderDialogParams folderParams, Action<SelectFolderDialogParams> callback) => false;

			public bool SaveFileDialog(SaveFileDialogParams saveParams, Action<SaveFileDialogParams> callback)
			{
				this.AskedFilter = saveParams.Filter;
				saveParams.FileName = this.chosen;
				callback(saveParams);
				return true;
			}

			public void ShowFileInFolder(string fileName)
			{
			}
		}
	}
}
