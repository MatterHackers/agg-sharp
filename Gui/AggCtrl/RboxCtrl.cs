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
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// C++ AGG's <c>rbox_ctrl</c>, drawn and driven exactly as C++ does: a background, a border, one label
	/// per item (gsv_text stroked with round joins and caps), a ring per item and a dot in the current one.
	/// Items stack upward from the bottom of the box; a press on a ring selects it and the arrow keys cycle.
	/// </summary>
	public class RboxCtrl : AggCtrl
	{
		/// <summary>C++ keeps at most 32 items and ignores the rest.</summary>
		private const int MaxItems = 32;

		private readonly List<string> items = new List<string>();

		private double borderWidth = 1.0;

		private double borderExtra;

		public RboxCtrl(double x1, double y1, double x2, double y2, bool flipY = false)
			: base(x1, y1, x2, y2, flipY)
		{
		}

		public IReadOnlyList<string> Items => this.items;

		/// <summary>C++ <c>cur_item</c>: the selected item, -1 for none.</summary>
		public int CurrentItem { get; set; } = -1;

		public double TextThickness { get; set; } = 1.5;

		public double TextHeight { get; private set; } = 9.0;

		/// <summary>0 lets gsv_text pick the width from the height.</summary>
		public double TextWidth { get; private set; }

		public Color BackgroundColor { get; set; } = Rgba8.FromRgba(1.0, 1.0, 0.9);

		public Color BorderColor { get; set; } = Rgba8.FromRgba(0.0, 0.0, 0.0);

		public Color TextColor { get; set; } = Rgba8.FromRgba(0.0, 0.0, 0.0);

		public Color InactiveColor { get; set; } = Rgba8.FromRgba(0.0, 0.0, 0.0);

		public Color ActiveColor { get; set; } = Rgba8.FromRgba(0.4, 0.0, 0.0);

		public override int NumPaths => 5;

		/// <summary>The inside of the border, C++ m_xs1..m_ys2.</summary>
		private double Xs1 => this.X1 + this.borderWidth;

		private double Ys1 => this.Y1 + this.borderWidth;

		/// <summary>
		/// C++ <c>m_dy</c>, the distance between items. C++ sets it in <c>rewind</c> and reads it on mouse
		/// down, so a press before the first draw reads garbage there; here it is always current.
		/// </summary>
		private double Dy => this.TextHeight * 2.0;

		public void AddItem(string text)
		{
			if (this.items.Count < MaxItems)
			{
				this.items.Add(text);
			}
		}

		/// <summary>C++ <c>border_width</c>: the border's width and how far the background reaches past it.</summary>
		public void SetBorderWidth(double width, double extra = 0.0)
		{
			this.borderWidth = width;
			this.borderExtra = extra;
		}

		/// <summary>C++ <c>text_size</c>.</summary>
		public void SetTextSize(double height, double width = 0.0)
		{
			this.TextHeight = height;
			this.TextWidth = width;
		}

		public override Color PathColor(int index)
		{
			switch (index)
			{
				case 0: return this.BackgroundColor;
				case 1: return this.BorderColor;
				case 2: return this.TextColor;
				case 3: return this.InactiveColor;
				default: return this.ActiveColor;
			}
		}

		public override bool InRect(double x, double y)
		{
			this.InverseTransformXY(ref x, ref y);
			return x >= this.X1 && x <= this.X2 && y >= this.Y1 && y <= this.Y2;
		}

		public override bool OnMouseButtonDown(double x, double y)
		{
			this.InverseTransformXY(ref x, ref y);
			for (int i = 0; i < this.items.Count; i++)
			{
				double xp = this.Xs1 + (this.Dy / 1.3);
				double yp = this.Ys1 + (this.Dy * i) + (this.Dy / 1.3);
				double dx = x - xp;
				double dy = y - yp;
				if (Math.Sqrt((dx * dx) + (dy * dy)) <= this.TextHeight / 1.5)
				{
					this.CurrentItem = i;
					return true;
				}
			}

			return false;
		}

		public override bool OnMouseMove(double x, double y, bool buttonFlag) => false;

		public override bool OnMouseButtonUp(double x, double y) => false;

		public override bool OnArrowKeys(bool left, bool right, bool down, bool up)
		{
			if (this.CurrentItem >= 0)
			{
				if (up || right)
				{
					this.CurrentItem++;
					if (this.CurrentItem >= this.items.Count)
					{
						this.CurrentItem = 0;
					}

					return true;
				}

				if (down || left)
				{
					this.CurrentItem--;
					if (this.CurrentItem < 0)
					{
						this.CurrentItem = this.items.Count - 1;
					}

					return true;
				}
			}

			return false;
		}

		/// <summary>C++ <c>rbox_ctrl_impl::rewind</c> + <c>vertex</c> for one path.</summary>
		protected override IVertexSource Path(int index)
		{
			var storage = new VertexStorage();
			double dy = this.Dy;
			switch (index)
			{
				case 1: // border: the outer rectangle and, wound the other way, the inner one
					storage.MoveTo(this.X1, this.Y1);
					storage.LineTo(this.X2, this.Y1);
					storage.LineTo(this.X2, this.Y2);
					storage.LineTo(this.X1, this.Y2);
					storage.MoveTo(this.X1 + this.borderWidth, this.Y1 + this.borderWidth);
					storage.LineTo(this.X1 + this.borderWidth, this.Y2 - this.borderWidth);
					storage.LineTo(this.X2 - this.borderWidth, this.Y2 - this.borderWidth);
					storage.LineTo(this.X2 - this.borderWidth, this.Y1 + this.borderWidth);
					return storage;

				case 2: // text: every item's label, one after another in one path
					for (int i = 0; i < this.items.Count; i++)
					{
						// C++ starts item 0 at ys1 + dy / 2 and item i at ys1 + dy * (i + 1) - dy / 2.
						double y = i == 0 ? this.Ys1 + (dy / 2.0) : this.Ys1 + (dy * (i + 1)) - (dy / 2.0);
						Append(storage, StrokedText(this.items[i], this.Xs1 + (dy * 1.5), y, this.TextHeight, this.TextWidth, this.TextThickness));
					}

					return storage;

				case 3: // inactive items: a ring per item
					for (int i = 0; i < this.items.Count; i++)
					{
						var ring = new Ellipse(this.Xs1 + (dy / 1.3), this.Ys1 + (dy * i) + (dy / 1.3), this.TextHeight / 1.5, this.TextHeight / 1.5, 32);
						Append(storage, new Stroke(ring, this.TextThickness));
					}

					return storage;

				case 4: // active item
					if (this.CurrentItem >= 0)
					{
						return new Ellipse(this.Xs1 + (dy / 1.3), this.Ys1 + (dy * this.CurrentItem) + (dy / 1.3), this.TextHeight / 2.0, this.TextHeight / 2.0, 32);
					}

					return storage;

				default: // background
					storage.MoveTo(this.X1 - this.borderExtra, this.Y1 - this.borderExtra);
					storage.LineTo(this.X2 + this.borderExtra, this.Y1 - this.borderExtra);
					storage.LineTo(this.X2 + this.borderExtra, this.Y2 + this.borderExtra);
					storage.LineTo(this.X1 - this.borderExtra, this.Y2 + this.borderExtra);
					return storage;
			}
		}

		/// <summary>Copies <paramref name="source"/>'s vertices up to its stop, as C++ chains one source after another.</summary>
		private static void Append(VertexStorage storage, IVertexSource source)
		{
			foreach (VertexData vertex in source.Vertices())
			{
				if (vertex.IsStop)
				{
					break;
				}

				storage.Add(vertex.Position.X, vertex.Position.Y, vertex.Command);
			}
		}
	}
}
