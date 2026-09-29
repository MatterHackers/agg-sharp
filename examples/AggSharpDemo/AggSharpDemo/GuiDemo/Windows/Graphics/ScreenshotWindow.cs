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
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.AggSharpDemo.GuiDemo.Windows.Widgets;

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Graphics
{
	/// <summary>
	/// agg-gui's "Screenshot" window (screenshot_demo.rs, after egui's ScreenshotDemo): a note, Take Screenshot
	/// with a Capture continuously toggle, Save... (Download in a browser) and Copy for the latest capture, a
	/// separator, and a preview of that capture. See <see cref="ScreenshotCapture"/> for how a capture is taken.
	/// </summary>
	public class ScreenshotWindow : FlowLayoutWidget
	{
		private const double Gap = 10;

		private readonly Func<IFileDialogProvider> fileDialogs;
		private ImageBuffer spareImage;
		private bool capturePending;
		private bool capturing;
		private bool closed;

		/// <param name="fileDialogs">Where Save asks for a file; null for the platform's (<see cref="AggContext.FileDialogs"/>),
		/// which on a browser hands back a staging path and downloads what is written there.</param>
		/// <param name="isBrowser">Whether the export button reads Download rather than Save...; null to ask the runtime.</param>
		public ScreenshotWindow(DemoTheme demoTheme, IFileDialogProvider fileDialogs = null, bool? isBrowser = null)
			: base(FlowDirection.TopToBottom)
		{
			this.fileDialogs = () => fileDialogs ?? AggContext.FileDialogs;
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Stretch;
			this.Padding = new BorderDouble(12);
			this.BackgroundColor = demoTheme.Palette.PanelFill;

			var kit = new MiscDemoKit(demoTheme);
			GuiWidget Spaced(GuiWidget widget)
			{
				widget.Margin = new BorderDouble(bottom: Gap);
				return widget;
			}

			WrappedTextWidget note = kit.Wrapped("This demo showcases how to take screenshots: the app is drawn into an ImageBuffer.", 12);
			note.HAnchor = HAnchor.Stretch;
			this.AddChild(Spaced(note));

			FlowLayoutWidget captureRow = kit.Row(Gap);
			this.TakeButton = kit.Button("Screenshot Take", "Take Screenshot");
			this.TakeButton.Margin = new BorderDouble(right: Gap);
			this.TakeButton.Click += (s, e) => this.RequestCapture();
			captureRow.AddChild(this.TakeButton);
			this.ContinuousCheckBox = kit.CheckBox("Screenshot Continuous", "Capture continuously", false, 12);
			this.ContinuousCheckBox.CheckedStateChanged += (s, e) =>
			{
				if (this.ContinuousCheckBox.Checked)
				{
					this.RequestCapture();
				}
			};
			captureRow.AddChild(this.ContinuousCheckBox);
			this.AddChild(Spaced(captureRow));

			FlowLayoutWidget exportRow = kit.Row(Gap);
			this.SaveButton = kit.Button("Screenshot Save", ExportLabel(isBrowser ?? OperatingSystem.IsBrowser()));
			this.SaveButton.Margin = new BorderDouble(right: Gap);
			this.SaveButton.Click += (s, e) => UiThread.RunOnIdle(this.Save);
			exportRow.AddChild(this.SaveButton);
			this.CopyButton = kit.Button("Screenshot Copy", "Copy");
			this.CopyButton.Click += (s, e) => UiThread.RunOnIdle(this.Copy);
			exportRow.AddChild(this.CopyButton);
			this.AddChild(Spaced(exportRow));

			var separator = new GuiWidget
			{
				HAnchor = HAnchor.Stretch,
				Height = Math.Max(1, Math.Round(DeviceScale)),
			};
			this.AddChild(Spaced(separator));

			this.Preview = new ScreenshotPreview(demoTheme) { Name = "Screenshot Preview" };
			// Re-armed from the preview's own draw, as agg-gui does, so continuous capture runs only while the
			// window is on screen and stops once it is closed.
			this.Preview.AfterDraw += (s, e) =>
			{
				if (this.ContinuousCheckBox.Checked && !this.capturing)
				{
					this.RequestCapture();
				}
			};
			this.AddChild(this.Preview);

			void Recolor(object sender, EventArgs e)
			{
				DemoPalette palette = demoTheme.Palette;
				this.BackgroundColor = palette.PanelFill;
				kit.Recolor();
				separator.BackgroundColor = palette.Separator;
				foreach (ThemedTextButton button in new[] { this.TakeButton, this.SaveButton, this.CopyButton })
				{
					demoTheme.StyleButton(button);
				}

				this.Preview.Invalidate();
			}

			Recolor(null, null);
			demoTheme.ThemeChanged += Recolor;
			this.Closed += (s, e) =>
			{
				this.closed = true;
				demoTheme.ThemeChanged -= Recolor;
			};

			this.UpdateButtons();
		}

		public ThemedTextButton TakeButton { get; }

		public CheckBox ContinuousCheckBox { get; }

		public ThemedTextButton SaveButton { get; }

		public ThemedTextButton CopyButton { get; }

		public ScreenshotPreview Preview { get; }

		/// <summary>How many captures have been taken, for tests to wait on.</summary>
		public int CaptureCount { get; private set; }

		/// <summary>The file the last save wrote, or null if none has.</summary>
		public string LastSavedPath { get; private set; }

		/// <summary>The export button's text: a browser's save is a download (agg-gui's EXPORT_BUTTON_LABEL).</summary>
		public static string ExportLabel(bool isBrowser) => isBrowser ? "Download" : "Save...";

		/// <summary>Captures the app into <see cref="ScreenshotPreview.Image"/> now.</summary>
		public void Capture()
		{
			if (this.closed)
			{
				return;
			}

			// Drawing the app draws this window too; its preview must not re-arm from inside the capture.
			this.capturing = true;
			try
			{
				// Into the image not on show: the capture draws the preview, which would otherwise read the
				// pixels being overwritten. Two images alternate, so continuous capture makes no new textures.
				ImageBuffer next = ScreenshotCapture.Capture(ScreenshotCapture.CaptureRoot(this), this.spareImage);
				this.spareImage = this.Preview.Image;
				this.Preview.Image = next;
			}
			finally
			{
				this.capturing = false;
			}

			this.CaptureCount++;
			this.UpdateButtons();
			this.Preview.Invalidate();
		}

		/// <summary>Asks for the latest capture as a PNG file and writes it there.</summary>
		public void Save()
		{
			if (this.Preview.Image == null)
			{
				return;
			}

			// A copy: the answer can come later (a browser's always does), after continuous capture has drawn
			// over this image.
			var image = new ImageBuffer(this.Preview.Image);

			var saveParams = new SaveFileDialogParams("PNG Image|*.png") { FileName = "screenshot.png" };
			this.fileDialogs()?.SaveFileDialog(saveParams, chosen =>
			{
				if (!string.IsNullOrEmpty(chosen.FileName))
				{
					string path = ScreenshotCapture.PngPath(chosen.FileName);
					if (ScreenshotCapture.WritePng(path, image))
					{
						this.LastSavedPath = path;
					}
				}
			});
		}

		/// <summary>Puts the latest capture on the system clipboard.</summary>
		public void Copy()
		{
			if (this.Preview.Image != null)
			{
				Clipboard.Instance?.SetImage(this.Preview.Image);
			}
		}

		// Off the draw, on the next idle: the capture redraws the whole app, which cannot happen mid-draw.
		private void RequestCapture()
		{
			if (this.capturePending || this.closed)
			{
				return;
			}

			this.capturePending = true;
			UiThread.RunOnIdle(() =>
			{
				this.capturePending = false;
				this.Capture();
			});
		}

		private void UpdateButtons()
		{
			this.SaveButton.Enabled = this.Preview.Image != null;
			this.CopyButton.Enabled = this.Preview.Image != null;
		}
	}
}
