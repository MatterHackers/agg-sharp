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

using MatterHackers.Agg;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.AggSharpDemo.Demos
{
	/// <summary>
	/// C++ AGG's perspective.cpp: the lion and an ellipse mapped from the lion's bounding rectangle onto a
	/// quadrilateral whose corners you drag, by a bilinear or a perspective transform (the radio box).
	/// </summary>
	public class PerspectiveDemo : AggDemo
	{
		private readonly LionShape lion = new LionShape();

		private readonly AggCtrlContainer ctrls = new AggCtrlContainer();

		public PerspectiveDemo()
		{
			// perspective.cpp's parse_lion mirrors the lion in both axes after taking its bounds.
			RectangleDouble bounds = this.lion.Bounds;
			foreach (var shape in this.lion.Shapes)
			{
				shape.VertexStorage.FlipX(bounds.Left, bounds.Right);
				shape.VertexStorage.FlipY(bounds.Bottom, bounds.Top);
			}

			// perspective.cpp runs with flip_y = true and gives its rbox !flip_y.
			this.TransTypeRbox = new RboxCtrl(420, 5.0, 420 + 130.0, 55.0, false);
			this.TransTypeRbox.AddItem("Bilinear");
			this.TransTypeRbox.AddItem("Perspective");
			this.TransTypeRbox.CurrentItem = 0;
			this.ctrls.Add(this.TransTypeRbox);
			this.ctrls.Changed += (s, e) => this.Invalidate();

			// The constructor puts the corners on the lion's bounds and on_init moves them by half the window
			// less half the lion - so the lion's own offset from the origin stays in, as C++ has it.
			double dx = (this.Width / 2.0) - ((bounds.Right - bounds.Left) / 2.0);
			double dy = (this.Height / 2.0) - ((bounds.Top - bounds.Bottom) / 2.0);
			this.Quad.SetPoint(0, bounds.Left + dx, bounds.Bottom + dy);
			this.Quad.SetPoint(1, bounds.Right + dx, bounds.Bottom + dy);
			this.Quad.SetPoint(2, bounds.Right + dx, bounds.Top + dy);
			this.Quad.SetPoint(3, bounds.Left + dx, bounds.Top + dy);
		}

		/// <summary>C++ <c>m_trans_type</c>: item 0 is bilinear, item 1 perspective.</summary>
		public RboxCtrl TransTypeRbox { get; }

		/// <summary>C++ <c>m_quad</c>: the four corners the lion's bounding rectangle maps onto.</summary>
		public InteractivePolygon Quad { get; } = new InteractivePolygon(4, 5.0);

		public override string Name => "perspective";

		public override string Category => "Vector Graphics";

		public override string Description => "The lion mapped onto a quadrilateral. Drag a corner, an edge or the whole shape; choose a bilinear or perspective mapping.";

		public override int Width => 600;

		public override int Height => 600;

		public override void Draw(Graphics2D graphics)
		{
			graphics.FillRectangle(0, 0, this.Width, this.Height, Color.White);

			RectangleDouble bounds = this.lion.Bounds;
			var quad = new double[8];
			for (int i = 0; i < 4; i++)
			{
				quad[i * 2] = this.Quad.GetPoint(i).X;
				quad[(i * 2) + 1] = this.Quad.GetPoint(i).Y;
			}

			ITransform transform = null;
			if (this.TransTypeRbox.CurrentItem == 0)
			{
				var bilinear = new Bilinear(bounds.Left, bounds.Bottom, bounds.Right, bounds.Top, quad);
				transform = bilinear.is_valid() ? bilinear : null;
			}
			else
			{
				var perspective = new Perspective(bounds.Left, bounds.Bottom, bounds.Right, bounds.Top, quad);
				transform = perspective.is_valid() ? perspective : null;
			}

			// C++ draws nothing but the quad and the rbox while the corners make the mapping singular.
			if (transform != null)
			{
				foreach (var shape in this.lion.Shapes)
				{
					graphics.Render(new VertexSourceApplyTransform(shape.VertexStorage, transform), shape.Color);
				}

				var ellipse = new Ellipse(
					(bounds.Left + bounds.Right) * 0.5,
					(bounds.Bottom + bounds.Top) * 0.5,
					(bounds.Right - bounds.Left) * 0.5,
					(bounds.Top - bounds.Bottom) * 0.5,
					200);
				graphics.Render(new VertexSourceApplyTransform(ellipse, transform), Rgba8.FromRgba(0.5, 0.3, 0.0, 0.3));
				graphics.Render(new VertexSourceApplyTransform(new Stroke(ellipse, 3.0), transform), Rgba8.FromRgba(0.0, 0.3, 0.2, 1.0));
			}

			graphics.Render(this.Quad, Rgba8.FromRgba(0, 0.3, 0.5, 0.6));

			this.ctrls.Render(graphics);
		}

		public override void OnMouseDown(int x, int y, AggInputFlags button, AggInputFlags flags)
		{
			if (this.ctrls.OnMouseDown(x, y, button) || !button.HasFlag(AggInputFlags.MouseLeft))
			{
				return;
			}

			if (this.Quad.OnMouseButtonDown(x, y))
			{
				this.Invalidate();
			}
		}

		public override void OnMouseMove(int x, int y, AggInputFlags flags)
		{
			if (this.ctrls.OnMouseMove(x, y, flags))
			{
				return;
			}

			if (flags.HasFlag(AggInputFlags.MouseLeft))
			{
				if (this.Quad.OnMouseMove(x, y))
				{
					this.Invalidate();
				}
			}
			else if (this.Quad.OnMouseButtonUp(x, y))
			{
				this.Invalidate();
			}
		}

		public override void OnMouseUp(int x, int y, AggInputFlags button, AggInputFlags flags)
		{
			this.ctrls.OnMouseUp(x, y, button);
			if (this.Quad.OnMouseButtonUp(x, y))
			{
				this.Invalidate();
			}
		}

		public override void OnKeyDown(Keys key, AggInputFlags flags)
		{
			this.ctrls.OnKeyDown(key);
		}
	}
}
