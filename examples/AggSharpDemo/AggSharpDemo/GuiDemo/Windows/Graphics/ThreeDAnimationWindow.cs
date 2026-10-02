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
using System.Collections.Generic;
using System.Globalization;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Graphics
{
	/// <summary>
	/// agg-gui's "3D Animation" window (windows.rs cube_content, backend_panel SsaaRow): a status line with the
	/// SSAA setting and the bar grid's backbuffer cost, a row of SSAA buttons, and the animated bar grid. The
	/// buttons are agg-gui's Off / 4x / 9x / 16x samples per pixel, linear factors 1 to 4.
	/// </summary>
	public class ThreeDAnimationWindow : FlowLayoutWidget
	{
		/// <summary>The button labels, index i being linear factor i + 1.</summary>
		public static readonly string[] SsaaLabels = { "Off", "4×", "9×", "16×" };

		/// <summary>The factor the window opens at and "Reset all state" returns to: 1, Off. agg-gui starts the cube
		/// without AA (font_init.rs reads the saved msaa_samples with unwrap_or(0)).</summary>
		public const int DefaultSsaaFactor = 1;

		private readonly DemoTheme demoTheme;
		private readonly TextWidget label;

		public ThreeDAnimationWindow(DemoTheme demoTheme)
			: base(FlowDirection.TopToBottom)
		{
			this.demoTheme = demoTheme;
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Stretch;

			this.BarGrid = new BarGridWidget(demoTheme)
			{
				Name = "3D Animation Bar Grid",
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
				SoftwarePlaceholder = "3D needs the GPU renderer",
				SsaaFactor = DefaultSsaaFactor,
			};

			this.Status = new TextWidget(string.Empty, pointSize: 8, textColor: demoTheme.Palette.TextDim)
			{
				Name = "3D Animation SSAA Status",
				AutoExpandBoundsToText = true,
				Margin = new BorderDouble(12, 0, 0, 4),
			};
			this.AddChild(this.Status);

			var row = new FlowLayoutWidget()
			{
				Name = "3D Animation SSAA",
				HAnchor = HAnchor.Stretch,
				Padding = new BorderDouble(6, 4),
			};
			this.label = new TextWidget("SSAA", pointSize: 9, textColor: demoTheme.Palette.TextColor)
			{
				VAnchor = VAnchor.Center,
				Margin = new BorderDouble(6, 0, 12, 0),
			};
			row.AddChild(this.label);

			// agg-gui's SsaaRow: separate subtle buttons 4px apart, the selected one on the accent fill.
			for (int i = 0; i < SsaaLabels.Length; i++)
			{
				int factor = i + 1;
				var button = new ThemedTextButton(SsaaLabels[i], demoTheme.Theme, demoTheme.Theme.DefaultFontSize * 10 / 12)
				{
					Name = "3D Animation SSAA " + SsaaLabels[i],
					Margin = new BorderDouble(right: 4),
					MinimumSize = new VectorMath.Vector2(40 * DeviceScale, 0),
				};
				button.Click += (s, e) => this.SsaaFactor = factor;
				this.SsaaButtons.Add(button);
				row.AddChild(button);
			}

			this.AddChild(row);
			this.AddChild(this.BarGrid);

			this.BarGrid.BoundsChanged += (s, e) => this.Refresh();
			this.Refresh();
			demoTheme.ThemeChanged += this.OnThemeChanged;
			this.Closed += (s, e) => demoTheme.ThemeChanged -= this.OnThemeChanged;
		}

		/// <summary>Raised when the SSAA factor changes, from a button or the setter.</summary>
		public event EventHandler SsaaFactorChanged;

		/// <summary>The animated bars.</summary>
		public BarGridWidget BarGrid { get; }

		/// <summary>The SSAA buttons: Off, 4x, 9x, 16x.</summary>
		public List<ThemedTextButton> SsaaButtons { get; } = new List<ThemedTextButton>();

		/// <summary>The line above the buttons: the SSAA setting and the bar grid's backbuffer size and memory.</summary>
		public TextWidget Status { get; }

		/// <summary>The bar grid's linear SSAA factor (1 to 4); setting it lights its button and raises
		/// <see cref="SsaaFactorChanged"/> when it changes.</summary>
		public int SsaaFactor
		{
			get => this.BarGrid.SsaaFactor;
			set
			{
				if (value != this.BarGrid.SsaaFactor)
				{
					this.BarGrid.SsaaFactor = value;
					this.Refresh();
					this.SsaaFactorChanged?.Invoke(this, EventArgs.Empty);
				}
			}
		}

		/// <summary>
		/// agg-gui's SsaaRow status caption: "Off" or "&lt;linear&gt;× linear · &lt;raw&gt;× memory", then the
		/// backbuffer's pixel size and its memory at 8 bytes a pixel (RGBA8 colour + Depth32) times the samples.
		/// The size is left off until the grid has one.
		/// </summary>
		public static string StatusCaption(int linearFactor, int width, int height)
		{
			int raw = linearFactor * linearFactor;
			string prefix = raw <= 1 ? "Off" : $"{linearFactor}× linear · {raw}× memory";
			if (width <= 0 || height <= 0)
			{
				return prefix;
			}

			double mb = (double)width * height * 8 * raw / (1024.0 * 1024.0);
			return $"{prefix}  ({width} × {height} = {mb.ToString("0.0", CultureInfo.InvariantCulture)} MB)";
		}

		private void OnThemeChanged(object sender, EventArgs e) => this.Refresh();

		private void Refresh()
		{
			DemoPalette palette = this.demoTheme.Palette;
			this.label.TextColor = palette.TextColor;
			this.Status.TextColor = palette.TextDim;
			this.Status.Text = StatusCaption(this.SsaaFactor, (int)Math.Round(this.BarGrid.Width), (int)Math.Round(this.BarGrid.Height));
			Color accent = DemoTheme.ColorOf(this.demoTheme.Accent);
			for (int i = 0; i < this.SsaaButtons.Count; i++)
			{
				bool on = i + 1 == this.SsaaFactor;
				this.SsaaButtons[i].BackgroundColor = on ? accent : this.demoTheme.Theme.ButtonBackgroundColor;
				this.SsaaButtons[i].TextColor = on ? Color.White : palette.TextColor;
			}
		}
	}
}
