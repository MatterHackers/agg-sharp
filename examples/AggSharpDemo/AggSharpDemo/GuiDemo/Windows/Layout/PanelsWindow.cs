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
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.AggSharpDemo.GuiDemo.Windows.Widgets;
using MatterHackers.VectorMath;

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Layout
{
	/// <summary>
	/// The "Panels" window, a port of agg-gui's panels_demo (demo-ui/src/windows/interaction.rs, egui's panels
	/// demo): an expandable upper panel, left and right panels, a bottom panel with the source link and a central
	/// panel, each titled and scrolling two paragraphs of lorem ipsum. Dragging the separator under the top panel
	/// or beside either side panel resizes it. Panels are named "Panels &lt;title&gt;", separators
	/// "Panels &lt;Top|Left|Right&gt; Separator".
	/// </summary>
	public class PanelsWindow : GuiWidget
	{
		public const string SourceUrl = "https://github.com/larsbrubaker/agg-gui/blob/main/demo-ui/src/windows/interaction.rs";

		public const string LoremIpsum = "Lorem ipsum dolor sit amet, consectetur adipiscing elit. "
			+ "Curabitur et mauris auctor, cursus leo ut, viverra erat. "
			+ "Nulla facilisi. Vivamus tempus ligula a lectus condimentum aliquam. "
			+ "Sed sit amet magna et arcu efficitur porttitor. Suspendisse potenti. "
			+ "Praesent consequat, lacus in sollicitudin tempor, ex purus commodo urna.";

		/// <summary>How far past the 4-unit gap a separator can still be grabbed, on each side.</summary>
		private const double GrabSlop = 2;

		private readonly DemoTheme demoTheme;
		private readonly MiscDemoKit kit;
		private readonly GuiWidget[] panels = new GuiWidget[5];
		private readonly List<GuiWidget> rules = new List<GuiWidget>();
		private readonly Dictionary<PanelDrag, PanelSeparator> separators = new Dictionary<PanelDrag, PanelSeparator>();

		public PanelsWindow(DemoTheme demoTheme)
		{
			this.demoTheme = demoTheme;
			this.kit = new MiscDemoKit(demoTheme);
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Stretch;

			for (int i = 0; i < 5; i++)
			{
				string title = PanelsLayout.Titles[i];
				this.panels[i] = i == 3 ? this.BottomPanel() : this.ScrollingPanel(title);
				this.panels[i].Name = $"Panels {title}";
				this.panels[i].HAnchor = HAnchor.Absolute;
				this.panels[i].VAnchor = VAnchor.Absolute;
				this.AddChild(this.panels[i]);
			}

			// After the panels, so the separators are on top where their grab zones overlap a panel's edge.
			foreach (PanelDrag target in new[] { PanelDrag.Top, PanelDrag.Left, PanelDrag.Right })
			{
				var separator = new PanelSeparator(this, target) { Name = $"Panels {target} Separator" };
				this.separators[target] = separator;
				this.AddChild(separator);
			}

			this.Recolor();
			demoTheme.ThemeChanged += this.OnThemeChanged;
		}

		/// <summary>The top height and side widths, in design units.</summary>
		public PanelsLayout PanelLayout { get; } = new PanelsLayout();

		/// <summary>The panel titled <paramref name="title"/> (one of <see cref="PanelsLayout.Titles"/>).</summary>
		public GuiWidget Panel(string title) => this.panels[Array.IndexOf(PanelsLayout.Titles, title)];

		public GuiWidget Separator(PanelDrag target) => this.separators[target];

		public override void OnBoundsChanged(EventArgs e)
		{
			base.OnBoundsChanged(e);
			this.ArrangePanels();
		}

		public override void OnClosed(EventArgs e)
		{
			// The theme outlives the window.
			this.demoTheme.ThemeChanged -= this.OnThemeChanged;
			base.OnClosed(e);
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			base.OnDraw(graphics2D);

			// agg-gui outlines every panel over its content.
			foreach (GuiWidget panel in this.panels)
			{
				RectangleDouble r = panel.BoundsRelativeToParent;
				graphics2D.Render(new Stroke(new RoundedRect(r.Left + .5, r.Bottom + .5, r.Right - .5, r.Top - .5, 0), 1), this.demoTheme.Palette.Separator);
			}
		}

		/// <summary>Moves <paramref name="target"/>'s separator to <paramref name="position"/> (this widget's pixels).</summary>
		public void DragSeparator(PanelDrag target, Vector2 position)
		{
			double scale = DeviceScale;
			this.PanelLayout.Resize(target, position.X / scale, position.Y / scale, this.Width / scale, this.Height / scale);
			this.ArrangePanels();
			this.Invalidate();
		}

		/// <summary>Seats the panels and separators for the current size and <see cref="PanelLayout"/>.</summary>
		private void ArrangePanels()
		{
			double scale = DeviceScale;
			double width = this.Width / scale;
			double height = this.Height / scale;
			if (width <= 0 || height <= 0)
			{
				return;
			}

			this.PanelLayout.Clamp(width, height);
			RectangleDouble[] rects = this.PanelLayout.Rects(width, height);
			for (int i = 0; i < 5; i++)
			{
				Place(this.panels[i], rects[i], scale);
			}

			foreach (KeyValuePair<PanelDrag, PanelSeparator> pair in this.separators)
			{
				RectangleDouble gap = this.PanelLayout.Separator(pair.Key, width, height);
				if (pair.Key == PanelDrag.Top)
				{
					gap.Inflate(new BorderDouble(0, GrabSlop));
				}
				else
				{
					gap.Inflate(new BorderDouble(GrabSlop, 0));
				}

				Place(pair.Value, gap, scale);
			}
		}

		private static void Place(GuiWidget widget, RectangleDouble designRect, double scale)
		{
			widget.Position = new Vector2(designRect.Left * scale, designRect.Bottom * scale);
			widget.Size = new Vector2(Math.Max(0, designRect.Width) * scale, Math.Max(0, designRect.Height) * scale);
		}

		/// <summary>A titled panel scrolling lorem ipsum, a rule, and lorem ipsum again.</summary>
		private GuiWidget ScrollingPanel(string title)
		{
			var scroll = new ScrollableWidget(autoScroll: true);
			scroll.ScrollArea.HAnchor = HAnchor.Stretch;
			FlowLayoutWidget column = this.kit.Column();
			column.Padding = new BorderDouble(10, 8, 10, 10);
			TextWidget heading = this.kit.Label(title, 17);
			heading.HAnchor = HAnchor.Center;
			heading.Margin = new BorderDouble(0, 4);
			column.AddChild(heading);
			column.AddChild(this.kit.Wrapped(LoremIpsum));
			var rule = new GuiWidget(1, 1 * DeviceScale)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Absolute,
				Margin = new BorderDouble(0, 4),
			};
			this.rules.Add(rule);
			column.AddChild(rule);
			column.AddChild(this.kit.Wrapped(LoremIpsum));
			scroll.AddChild(column);
			return scroll;
		}

		/// <summary>"Bottom Panel" over the source link, both centred.</summary>
		private GuiWidget BottomPanel()
		{
			var column = new FlowLayoutWidget(FlowDirection.TopToBottom) { Padding = new BorderDouble(8, 5) };
			TextWidget heading = this.kit.Label("Bottom Panel", 17);
			heading.HAnchor = HAnchor.Center;
			column.AddChild(heading);
			column.AddChild(new Hyperlink("(source code)", this.kit.Theme, SourceUrl)
			{
				Name = "Panels Source Link",
				HAnchor = HAnchor.Center,
				Margin = new BorderDouble(0, 0, 0, 4),
			});
			return column;
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
			this.BackgroundColor = this.demoTheme.Palette.WindowFill;
			foreach (GuiWidget panel in this.panels)
			{
				panel.BackgroundColor = this.demoTheme.Palette.PanelFill;
			}

			foreach (GuiWidget rule in this.rules)
			{
				rule.BackgroundColor = this.demoTheme.Palette.Separator;
			}
		}

		/// <summary>
		/// The grab zone over one gap between panels: the gap plus a little either side. It paints the gap in
		/// the separator colour, or the accent while hovered or dragged, and drags the panel edge it borders.
		/// </summary>
		private sealed class PanelSeparator : GuiWidget
		{
			private readonly PanelsWindow owner;
			private readonly PanelDrag target;
			private bool dragging;
			private bool hovered;

			public PanelSeparator(PanelsWindow owner, PanelDrag target)
			{
				this.owner = owner;
				this.target = target;
				this.HAnchor = HAnchor.Absolute;
				this.VAnchor = VAnchor.Absolute;
				this.Cursor = target == PanelDrag.Top ? Cursors.SizeNS : Cursors.SizeWE;
			}

			public override void OnDraw(Graphics2D graphics2D)
			{
				double slop = GrabSlop * DeviceScale;
				RectangleDouble gap = this.LocalBounds;
				if (this.target == PanelDrag.Top)
				{
					gap.Inflate(new BorderDouble(0, -slop));
				}
				else
				{
					gap.Inflate(new BorderDouble(-slop, 0));
				}

				Color color = this.hovered || this.dragging
					? this.owner.demoTheme.Theme.PrimaryAccentColor.WithAlpha(140)
					: this.owner.demoTheme.Palette.Separator;
				graphics2D.FillRectangle(gap, color);
				base.OnDraw(graphics2D);
			}

			public override void OnMouseDown(MouseEventArgs mouseEvent)
			{
				if (mouseEvent.Button == MouseButtons.Left)
				{
					this.dragging = true;
					this.Invalidate();
				}

				base.OnMouseDown(mouseEvent);
			}

			public override void OnMouseMove(MouseEventArgs mouseEvent)
			{
				if (this.dragging)
				{
					this.owner.DragSeparator(this.target, mouseEvent.Position + this.Position);
				}

				base.OnMouseMove(mouseEvent);
			}

			public override void OnMouseUp(MouseEventArgs mouseEvent)
			{
				this.dragging = false;
				this.Invalidate();
				base.OnMouseUp(mouseEvent);
			}

			public override void OnMouseEnterBounds(MouseEventArgs mouseEvent)
			{
				this.hovered = true;
				this.Invalidate();
				base.OnMouseEnterBounds(mouseEvent);
			}

			public override void OnMouseLeaveBounds(MouseEventArgs mouseEvent)
			{
				this.hovered = false;
				this.Invalidate();
				base.OnMouseLeaveBounds(mouseEvent);
			}
		}
	}
}
