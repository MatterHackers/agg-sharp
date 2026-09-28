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

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Layout
{
	/// <summary>
	/// The Frame window's live preview (frame_demo/core.rs FramePreview): a thin wrapper outline, and inside it,
	/// the outer margin in from its top-left, the configured frame - drop shadow, fill, stroke, four corner
	/// radii - around a white "Content" label. The preview grows with the content plus both margins and is never
	/// smaller than 160 by 140 design units.
	/// </summary>
	public class FramePreview : GuiWidget
	{
		public const double MinimumDesignWidth = 160;
		public const double MinimumDesignHeight = 140;

		/// <summary>Stacked rounded rectangles approximating the shadow's blur falloff.</summary>
		private const int ShadowSteps = 12;

		private readonly FrameState state;
		private readonly DemoTheme demoTheme;

		public FramePreview(FrameState state, DemoTheme demoTheme, double contentPointSize)
		{
			this.state = state;
			this.demoTheme = demoTheme;
			this.HAnchor = HAnchor.Absolute;
			this.VAnchor = VAnchor.Absolute;
			this.Content = new TextWidget("Content", pointSize: contentPointSize, textColor: Color.White)
			{
				Name = "Frame Preview Content",
				Selectable = false,
				AutoExpandBoundsToText = true,
			};
			this.AddChild(this.Content);
			this.state.Changed += this.State_Changed;
			this.UpdateLayout();
		}

		public TextWidget Content { get; }

		public override void OnClosed(EventArgs e)
		{
			this.state.Changed -= this.State_Changed;
			base.OnClosed(e);
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			double scale = DeviceScale;
			FrameEdges corner = this.state.CornerRadius;
			RectangleDouble frame = this.FrameBounds;

			// The wrapper: the outer margin is the gap between this outline and the frame.
			graphics2D.Render(new Stroke(new RoundedRect(this.LocalBounds.Left + .5, this.LocalBounds.Bottom + .5, this.LocalBounds.Right - .5, this.LocalBounds.Top - .5, 4 * scale), 1), this.demoTheme.Palette.WidgetStroke);

			// egui's shadow offset grows downwards; here y is up.
			double spread = this.state.ShadowSpread * scale;
			double blur = this.state.ShadowBlur * scale;
			var shadowBase = new RectangleDouble(
				frame.Left + this.state.ShadowX * scale - spread,
				frame.Bottom - this.state.ShadowY * scale - spread,
				frame.Right + this.state.ShadowX * scale + spread,
				frame.Top - this.state.ShadowY * scale + spread);
			var shadow = new ColorF(this.state.ShadowColor);
			for (int i = ShadowSteps - 1; i >= 0; i--)
			{
				double t = i / (double)ShadowSteps;
				double inflate = t * blur;
				double falloff = (1 - t) * (1 - t);
				double alpha = Math.Min(1, shadow.alpha * falloff / ShadowSteps * 6);
				var layer = shadowBase;
				layer.Inflate(inflate);
				graphics2D.Render(Corners(layer, corner, scale, inflate), new ColorF(shadow.red, shadow.green, shadow.blue, alpha).ToColor());
			}

			RoundedRect frameShape = Corners(frame, corner, scale, 0);
			graphics2D.Render(frameShape, this.state.Fill);
			if (this.state.StrokeWidth > 0 && this.state.StrokeColor.alpha > 0)
			{
				graphics2D.Render(new Stroke(frameShape, this.state.StrokeWidth * scale), this.state.StrokeColor);
			}

			base.OnDraw(graphics2D);
		}

		/// <summary>The frame's rectangle: the content plus the inner margin, the outer margin in from the top-left.</summary>
		public RectangleDouble FrameBounds
		{
			get
			{
				double scale = DeviceScale;
				FrameEdges inner = this.state.InnerMargin;
				FrameEdges outer = this.state.OuterMargin;
				double width = Math.Max(4 * scale, this.ContentWidth + (inner[0] + inner[1]) * scale);
				double height = Math.Max(4 * scale, this.ContentHeight + (inner[2] + inner[3]) * scale);
				double left = outer[0] * scale;
				double top = this.Height - outer[2] * scale;
				return new RectangleDouble(left, top - height, left + width, top);
			}
		}

		private double ContentWidth => Math.Max(1, this.Content.Width);

		private double ContentHeight => Math.Max(1, this.Content.Height);

		/// <summary>
		/// <paramref name="bounds"/> with the four corner radii (grown by <paramref name="inflate"/> for a shadow
		/// layer), each clamped to half the shorter side so large values do not kink the outline.
		/// </summary>
		private static RoundedRect Corners(RectangleDouble bounds, FrameEdges corner, double scale, double inflate)
		{
			double max = Math.Max(0, Math.Min(bounds.Width, bounds.Height) / 2);
			double Radius(int i) => Math.Clamp(corner[i] * scale + inflate, 0, max);
			var shape = new RoundedRect(bounds.Left, bounds.Bottom, bounds.Right, bounds.Top);

			// FrameEdges holds NW, NE, SW, SE; RoundedRect takes left-bottom, right-bottom, right-top, left-top.
			shape.radius(Radius(2), Radius(3), Radius(1), Radius(0));
			return shape;
		}

		private void State_Changed(object sender, EventArgs e)
		{
			this.UpdateLayout();
			this.Invalidate();
		}

		/// <summary>Sizes the preview from the content and both margins, and seats the content inside the frame.</summary>
		private void UpdateLayout()
		{
			double scale = DeviceScale;
			FrameEdges inner = this.state.InnerMargin;
			FrameEdges outer = this.state.OuterMargin;
			double width = Math.Max(MinimumDesignWidth * scale, this.ContentWidth + (inner[0] + inner[1] + outer[0] + outer[1]) * scale);
			double height = Math.Max(MinimumDesignHeight * scale, this.ContentHeight + (inner[2] + inner[3] + outer[2] + outer[3]) * scale);
			this.Size = new VectorMath.Vector2(width, height);

			RectangleDouble frame = this.FrameBounds;
			this.Content.Position = new VectorMath.Vector2(frame.Left + inner[0] * scale, frame.Bottom + inner[3] * scale);
		}
	}
}
