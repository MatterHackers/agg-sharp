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
using MatterHackers.Agg.UI;
using MatterHackers.AggSharpDemo.GuiDemo.Windows.Widgets;

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Tests
{
	/// <summary>
	/// The "Clipboard Test" window, a port of agg-gui's clipboard_test (demo-ui/src/windows/tests/basic/controls.rs,
	/// itself egui's clipboard_test.rs): an editable text field with a copy button that puts its live text on the
	/// system clipboard, a gradient image with a copy button of its own, and the text field's shortcut legend.
	/// The mac and browser clipboards take text only, so there the image button copies nothing.
	/// </summary>
	public class ClipboardTestWindow : FlowLayoutWidget
	{
		public const string InitialText = "Example text you can copy-and-paste";

		/// <summary>The side of the square gradient image, as agg-gui's IMG_W / IMG_H.</summary>
		public const int ImageSize = 48;

		private readonly DemoTheme demoTheme;
		private readonly MiscDemoKit kit;
		private readonly GuiWidget separator;
		private readonly GuiWidget imageFrame;

		public ClipboardTestWindow(DemoTheme demoTheme)
			: base(FlowDirection.TopToBottom)
		{
			this.demoTheme = demoTheme;
			this.kit = new MiscDemoKit(demoTheme);
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Stretch;
			this.Padding = new BorderDouble(16);

			// agg-gui's column gap.
			double gap = 14;
			this.AddChild(this.kit.Wrapped("agg-sharp integrates with the system clipboard."));
			this.AddChild(this.kit.Wrapped("Try copy-cut-pasting text in the text edit below."));

			var textRow = this.kit.Row();
			textRow.HAnchor = HAnchor.Stretch;
			textRow.Margin = new BorderDouble(0, gap / 2);
			this.TextField = new ThemedTextEditWidget(InitialText, demoTheme.Theme, pixelWidth: 200)
			{
				Name = "Clipboard Text",
				HAnchor = HAnchor.Stretch,
				Margin = new BorderDouble(right: 10),
			};
			textRow.AddChild(this.TextField);
			this.CopyTextButton = this.kit.GlyphButton("Clipboard Copy Text", IconFont.Copy, 13);
			this.CopyTextButton.Click += (s, e) => this.ClipboardTarget?.SetText(this.TextField.Text);
			textRow.AddChild(this.CopyTextButton);
			this.AddChild(textRow);

			this.AddChild(this.kit.Wrapped("You can also copy images:"));

			// What is shown is exactly what is copied.
			this.Image = GradientImage(ImageSize, ImageSize);
			var imageRow = this.kit.Row();
			imageRow.Margin = new BorderDouble(0, gap / 2);
			this.imageFrame = new ImageWidget(this.Image)
			{
				Name = "Clipboard Image",
				Border = new BorderDouble(1),
				Margin = new BorderDouble(right: 10),
			};
			imageRow.AddChild(this.imageFrame);
			this.CopyImageButton = this.kit.GlyphButton("Clipboard Copy Image", IconFont.Copy, 13);
			this.CopyImageButton.Click += (s, e) => this.ClipboardTarget?.SetImage(this.Image);
			imageRow.AddChild(this.CopyImageButton);
			this.AddChild(imageRow);

			this.separator = new GuiWidget
			{
				HAnchor = HAnchor.Stretch,
				Height = Math.Max(1, Math.Round(DeviceScale)),
				Margin = new BorderDouble(0, gap / 2),
			};
			this.AddChild(this.separator);

			this.AddChild(this.kit.Wrapped("Ctrl+C / Ctrl+X — copy or cut selected text\nCtrl+V — paste from clipboard\nCtrl+A — select all", 11.5));

			this.Recolor();
			demoTheme.ThemeChanged += this.OnThemeChanged;
		}

		/// <summary>Where the copy buttons write: the platform's clipboard unless a test swaps in its own.</summary>
		public ISystemClipboard ClipboardTarget { get; set; } = Clipboard.Instance;

		public ThemedTextEditWidget TextField { get; }

		public ThemedIconButton CopyTextButton { get; }

		public ThemedIconButton CopyImageButton { get; }

		/// <summary>The gradient the image row shows and copies.</summary>
		public ImageBuffer Image { get; }

		/// <summary>agg-gui's generate_test_image: red rising left to right, green top to bottom, blue fixed, opaque.</summary>
		public static ImageBuffer GradientImage(int width, int height)
		{
			var image = new ImageBuffer(width, height);
			for (int y = 0; y < height; y++)
			{
				for (int x = 0; x < width; x++)
				{
					// agg-gui's row 0 is the top; ImageBuffer's is the bottom.
					image.SetPixel(x, height - 1 - y, new Color(255 * x / width, 255 * y / height, 160, 255));
				}
			}

			return image;
		}

		public override void OnClosed(EventArgs e)
		{
			// The theme outlives the window.
			this.demoTheme.ThemeChanged -= this.OnThemeChanged;
			base.OnClosed(e);
		}

		private void OnThemeChanged(object sender, EventArgs e)
		{
			this.Recolor();
			this.Invalidate();
		}

		/// <summary>Pushes the current theme into the widgets that copied their colours when they were built.</summary>
		private void Recolor()
		{
			this.kit.Recolor();
			this.BackgroundColor = this.demoTheme.Palette.PanelFill;
			this.separator.BackgroundColor = this.demoTheme.Palette.Separator;
			this.imageFrame.BorderColor = this.demoTheme.Palette.WidgetStroke;
			this.TextField.ActualTextEditWidget.TextColor = this.demoTheme.Theme.TextColor;
		}
	}
}
