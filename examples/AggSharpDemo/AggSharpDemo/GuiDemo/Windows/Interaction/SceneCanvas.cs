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
using MatterHackers.Agg.VertexSource;
using MatterHackers.AggSharpDemo.GuiDemo.Windows.Widgets;
using MatterHackers.VectorMath;

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Interaction
{
	/// <summary>
	/// The scene-space content of the Scene window (scene_demo.rs SceneCanvas): a fixed 420 x 300 panel with a
	/// title, Increment / Zero buttons and their click count, a text field and a hint, each at scene_demo.rs's
	/// position, over two rounded rectangles and three accent circles. Geometry is agg-gui's logical pixels scaled
	/// by DeviceScale, Y-up from the bottom.
	/// </summary>
	public class SceneCanvas : GuiWidget
	{
		public const double ContentWidth = 420;
		public const double ContentHeight = 300;

		private static readonly (double X, double Y, double W, double H)[] Rects = { (250, 150, 90, 60), (300, 60, 70, 40) };
		private static readonly (double X, double Y, double R)[] Circles = { (300, 200, 34), (360, 120, 22), (250, 90, 16) };

		private readonly DemoTheme demoTheme;
		private readonly MiscDemoKit kit;
		private readonly TextWidget clicksLabel;
		private int clicks;

		internal SceneCanvas(DemoTheme demoTheme, MiscDemoKit kit)
			: base(ContentWidth * DeviceScale, ContentHeight * DeviceScale)
		{
			this.demoTheme = demoTheme;
			this.kit = kit;
			this.Name = "Scene Canvas";

			this.Place(kit.Label("Scene content", 16), 20, ContentHeight - 34);

			this.IncrementButton = kit.Button("Scene Increment", "Increment");
			this.IncrementButton.Click += (s, e) => this.Clicks++;
			this.Place(this.IncrementButton, 20, ContentHeight - 80);

			this.ZeroButton = kit.Button("Scene Zero", "Zero");
			this.ZeroButton.Click += (s, e) => this.Clicks = 0;
			this.Place(this.ZeroButton, 165, ContentHeight - 80);

			this.clicksLabel = kit.Label("Clicks: 0", 13);
			this.clicksLabel.Name = "Scene Clicks";
			this.Place(this.clicksLabel, 20, ContentHeight - 120);

			this.TextField = new ThemedTextEditWidget("", kit.Theme, pixelWidth: 200 * GuiWidget.DeviceScale, messageWhenEmptyAndNotSelected: "Type here (focus works!)")
			{
				Name = "Scene Text Field",
			};
			this.Place(this.TextField, 20, ContentHeight - 160);

			this.Place(kit.Label("Drag empty space to pan", 11), 20, 24);
		}

		public ThemedTextButton IncrementButton { get; }

		public ThemedTextButton ZeroButton { get; }

		public ThemedTextEditWidget TextField { get; }

		/// <summary>The shared counter the Increment and Zero buttons drive.</summary>
		public int Clicks
		{
			get => this.clicks;
			set
			{
				this.clicks = value;
				this.clicksLabel.Text = $"Clicks: {value}";
			}
		}

		private static double S => DeviceScale;

		public override void OnDraw(Graphics2D graphics2D)
		{
			DemoPalette palette = this.demoTheme.Palette;

			// The panel and its border show the scene's extent while panning.
			graphics2D.FillRectangle(this.LocalBounds, palette.PanelFill);
			graphics2D.Rectangle(this.LocalBounds, palette.Separator, S);

			foreach (var (x, y, w, h) in Rects)
			{
				var rect = new RoundedRect(x * S, y * S, (x + w) * S, (y + h) * S, 4 * S);
				graphics2D.Render(rect, palette.WidgetBackground);
				graphics2D.Render(new Stroke(rect, S), palette.WidgetStroke);
			}

			Color accent = DemoTheme.ColorOf(this.demoTheme.Accent).WithAlpha(140);
			foreach (var (x, y, r) in Circles)
			{
				graphics2D.Render(new Ellipse(x * S, y * S, r * S, r * S), accent);
			}

			base.OnDraw(graphics2D);
		}

		/// <summary>Pushes the theme into the widgets that copied a colour when built (the kit does its own).</summary>
		public void Recolor()
		{
			this.TextField.ActualTextEditWidget.TextColor = this.kit.Theme.TextColor;
			this.TextField.BackgroundColor = this.demoTheme.Palette.WidgetBackground;
			foreach (ThemedTextButton button in new[] { this.IncrementButton, this.ZeroButton })
			{
				this.kit.DemoTheme.StyleButton(button);
			}

			this.Invalidate();
		}

		/// <summary>Puts <paramref name="child"/>'s bottom-left at scene_demo.rs's logical (<paramref name="x"/>, <paramref name="y"/>).</summary>
		private void Place(GuiWidget child, double x, double y)
		{
			child.HAnchor = HAnchor.Absolute;
			child.VAnchor = VAnchor.Absolute;
			child.Margin = new BorderDouble(0);
			child.OriginRelativeParent = new Vector2(x * S, y * S);
			this.AddChild(child);
		}
	}
}
