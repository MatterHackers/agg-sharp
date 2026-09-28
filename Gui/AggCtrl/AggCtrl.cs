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
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// C++ AGG's <c>ctrl</c>: the base of the in-canvas controls the AGG examples draw into their own image
	/// (slider, cbox, rbox, ...). A control is a set of numbered paths, each filled in its own color, in the
	/// demo's coordinates; <see cref="Render"/> does what C++ <c>render_ctrl</c> does, through
	/// <see cref="Graphics2D"/>, so the same control draws on the GPU and in the software reference.
	/// </summary>
	/// <remarks>
	/// Every path is optionally flipped (<c>y1 + y2 - y</c>, for examples that work y down) and then run
	/// through <see cref="Transform"/>, as C++ <c>transform_xy</c>; mouse input goes through the inverse.
	/// </remarks>
	public abstract class AggCtrl
	{
		protected AggCtrl(double x1, double y1, double x2, double y2, bool flipY)
		{
			this.X1 = x1;
			this.Y1 = y1;
			this.X2 = x2;
			this.Y2 = y2;
			this.FlipY = flipY;
		}

		public double X1 { get; }

		public double Y1 { get; }

		public double X2 { get; }

		public double Y2 { get; }

		public bool FlipY { get; }

		/// <summary>C++ <c>ctrl::transform</c>; null is C++ <c>no_transform()</c>, the default.</summary>
		public Affine? Transform { get; set; }

		/// <summary>How many paths <see cref="Render"/> fills, C++ <c>num_paths</c>.</summary>
		public abstract int NumPaths { get; }

		/// <summary>The color path <paramref name="index"/> is filled with, C++ <c>color(i)</c>.</summary>
		public abstract Color PathColor(int index);

		public abstract bool InRect(double x, double y);

		public abstract bool OnMouseButtonDown(double x, double y);

		public abstract bool OnMouseButtonUp(double x, double y);

		public abstract bool OnMouseMove(double x, double y, bool buttonFlag);

		public abstract bool OnArrowKeys(bool left, bool right, bool down, bool up);

		/// <summary>C++ <c>render_ctrl</c>: fills every path in turn with its color.</summary>
		public void Render(Graphics2D graphics)
		{
			for (int i = 0; i < this.NumPaths; i++)
			{
				graphics.Render(this.TransformedPath(i), this.PathColor(i));
			}
		}

		/// <summary>
		/// C++ <c>rewind(index)</c> + <c>vertex</c>: path <paramref name="index"/> as <see cref="Render"/> fills it,
		/// flipped and transformed. For callers that draw the paths themselves, such as the GuiWidget adapters
		/// that pick some of a control's paths.
		/// </summary>
		public IVertexSource PathSource(int index) => this.TransformedPath(index);

		/// <summary>
		/// C++ <c>conv_stroke&lt;gsv_text&gt;</c> as every ctrl draws its text: the gsv_text outline started at
		/// (<paramref name="x"/>, <paramref name="y"/>), stroked <paramref name="thickness"/> wide with round
		/// joins and caps. The outline is captured into a path first so what the rasterizer sees does not
		/// depend on how the GPU path walks a vertex source. Empty text draws nothing.
		/// </summary>
		protected static IVertexSource StrokedText(string text, double x, double y, double height, double width, double thickness)
		{
			var outline = new VertexStorage();
			if (!string.IsNullOrEmpty(text))
			{
#pragma warning disable CS0618 // gsv_text is obsolete for UI text; C++ controls draw with exactly this font.
				var gsvText = new gsv_text();
#pragma warning restore CS0618
				gsvText.text(text);
				gsvText.start_point(x, y);
				gsvText.size(height, width);
				foreach (VertexData vertex in gsvText.Vertices())
				{
					if (vertex.IsStop)
					{
						break;
					}

					outline.Add(vertex.Position.X, vertex.Position.Y, vertex.Command);
				}
			}

			return new Stroke(outline, thickness)
			{
				LineJoin = LineJoin.Round,
				LineCap = LineCap.Round,
			};
		}

		/// <summary>Path <paramref name="index"/> in the control's own coordinates, before flip and transform.</summary>
		protected abstract IVertexSource Path(int index);

		/// <summary>C++ <c>ctrl::inverse_transform_xy</c>: demo to control coordinates.</summary>
		protected void InverseTransformXY(ref double x, ref double y)
		{
			if (this.Transform is Affine transform)
			{
				transform.inverse_transform(ref x, ref y);
			}

			if (this.FlipY)
			{
				y = this.Y1 + this.Y2 - y;
			}
		}

		/// <summary>C++ <c>ctrl::transform_xy</c> applied to a whole path; the path itself when there is
		/// nothing to apply, so an untransformed control's vertices reach the rasterizer untouched.</summary>
		private IVertexSource TransformedPath(int index)
		{
			IVertexSource path = this.Path(index);
			if (!this.FlipY && this.Transform == null)
			{
				return path;
			}

			var storage = new VertexStorage();
			foreach (VertexData vertex in path.Vertices())
			{
				if (vertex.IsStop)
				{
					break;
				}

				// Like C++ every vertex but the stop is transformed - curve control points included.
				double x = vertex.Position.X;
				double y = vertex.Position.Y;
				if (this.FlipY)
				{
					y = this.Y1 + this.Y2 - y;
				}

				if (this.Transform is Affine transform)
				{
					transform.Transform(ref x, ref y);
				}

				storage.Add(x, y, vertex.Command);
			}

			return storage;
		}
	}
}
