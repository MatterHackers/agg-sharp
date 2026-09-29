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
using MatterHackers.Agg.UI;
using MatterHackers.AggSharpDemo.GuiDemo.Windows.Widgets;

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Interaction
{
	/// <summary>
	/// agg-gui's "Scene" window (scene_demo.rs scene_demo): two lines of help, a Reset view button beside a live
	/// scene_rect readout, a separator, and a <see cref="ScenePanZoom"/> (zoom 0.1..2) over a <see cref="SceneCanvas"/>.
	/// </summary>
	public class SceneWindow : FlowLayoutWidget
	{
		private const double Gap = 8;

		public SceneWindow(DemoTheme demoTheme)
			: base(FlowDirection.TopToBottom)
		{
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Stretch;
			this.Padding = new BorderDouble(8);
			this.BackgroundColor = demoTheme.Palette.PanelFill;

			var kit = new MiscDemoKit(demoTheme);
			GuiWidget Spaced(GuiWidget widget)
			{
				widget.Margin = new BorderDouble(bottom: Gap);
				return widget;
			}

			this.AddChild(Spaced(kit.Label("Pan: drag background or middle-drag.  Zoom: scroll wheel (anchored on cursor).", 11.5)));
			this.AddChild(Spaced(kit.Label("Double-click the background to reset the view.  Buttons stay interactive at any zoom.", 11)));

			FlowLayoutWidget toolbar = Spaced(kit.Row(10)) as FlowLayoutWidget;
			this.ResetButton = kit.Button("Scene Reset View", "Reset view");
			this.ResetButton.Margin = new BorderDouble(right: 10);
			toolbar.AddChild(this.ResetButton);
			this.Readout = kit.Label("scene_rect:", 11.5);
			this.Readout.Name = "Scene Rect Readout";
			toolbar.AddChild(this.Readout);
			this.AddChild(toolbar);

			var separator = new GuiWidget
			{
				HAnchor = HAnchor.Stretch,
				Height = Math.Max(1, Math.Round(DeviceScale)),
				Margin = new BorderDouble(bottom: Gap),
			};
			this.AddChild(separator);

			this.Canvas = new SceneCanvas(demoTheme, kit);
			this.Scene = new ScenePanZoom(this.Canvas)
			{
				Name = "Scene Pan Zoom",
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
				MinZoom = 0.1,
				MaxZoom = 2,
			};
			this.Scene.SceneRectChanged += (s, e) => this.UpdateReadout();
			this.ResetButton.Click += (s, e) => this.Scene.ResetView();
			this.AddChild(this.Scene);
			this.UpdateReadout();

			void Recolor(object sender, EventArgs e)
			{
				DemoPalette palette = demoTheme.Palette;
				this.BackgroundColor = palette.PanelFill;
				// agg-gui fills the Scene with the window background (Visuals::bg_color) around the canvas panel.
				this.Scene.BackgroundColor = palette.BackgroundColor;
				separator.BackgroundColor = palette.Separator;
				kit.Recolor();
				this.Canvas.Recolor();
				kit.DemoTheme.StyleButton(this.ResetButton);
			}

			Recolor(null, null);
			demoTheme.ThemeChanged += Recolor;
			this.Closed += (s, e) => demoTheme.ThemeChanged -= Recolor;
		}

		public ThemedTextButton ResetButton { get; }

		public TextWidget Readout { get; }

		public ScenePanZoom Scene { get; }

		public SceneCanvas Canvas { get; }

		private void UpdateReadout()
		{
			// Scene space is DeviceScale'd pixels; the readout shows agg-gui's logical units.
			RectangleDouble r = this.Scene.SceneRect;
			double s = DeviceScale;
			this.Readout.Text = $"scene_rect: [x {r.Left / s:0}, y {r.Bottom / s:0}, w {r.Width / s:0}, h {r.Height / s:0}]";
		}
	}
}
