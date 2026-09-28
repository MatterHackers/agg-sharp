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
using System.IO;
using MatterHackers.Agg;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.AggSharpDemo.Demos
{
	/// <summary>
	/// The flash_rasterizer examples' compound_shape: shapes.txt read one "=======BeginShape" block at a time,
	/// each path an open polyline of lines and quadratic curves with a fill style on its left, one on its right
	/// and a line style (-1 for none). The shape is drawn flattened, then through the viewport transform that
	/// fits it to the window.
	/// </summary>
	public class FlashShape
	{
		private readonly string[] lines;
		private readonly List<VertexStorage> paths = new List<VertexStorage>();
		private readonly List<(int Left, int Right, int Line)> styles = new List<(int, int, int)>();
		private int nextLine;
		private Affine affine = Affine.NewIdentity();

		// C++ m_curve's approximation scale.
		private double curveApproximationScale = 1;

		public FlashShape()
		{
			const string resourceName = "MatterHackers.AggSharpDemo.Art.shapes.txt";
			using var stream = typeof(FlashShape).Assembly.GetManifestResourceStream(resourceName)
				?? throw new InvalidOperationException(
					$"The resource '{resourceName}' is missing; AggSharpDemo.csproj embeds it from Art/shapes.txt.");
			using var reader = new StreamReader(stream);
			this.lines = reader.ReadToEnd().Split('\n');
		}

		/// <summary>C++ <c>paths()</c>: the number of paths in the current shape.</summary>
		public int Paths => this.paths.Count;

		/// <summary>The lowest fill style any path uses (int.MaxValue for none), flash_rasterizer2's min_style().</summary>
		public int MinStyle { get; private set; } = int.MaxValue;

		/// <summary>The highest fill style any path uses (int.MinValue for none), flash_rasterizer2's max_style().</summary>
		public int MaxStyle { get; private set; } = int.MinValue;

		/// <summary>C++ <c>style(i)</c>: path <paramref name="i"/>'s left fill, right fill and line style.</summary>
		public (int Left, int Right, int Line) Style(int i) => this.styles[i];

		/// <summary>C++ <c>read_next()</c>: reads the next shape; false (and no paths) past the last.</summary>
		public bool ReadNext()
		{
			this.paths.Clear();
			this.styles.Clear();
			this.MinStyle = int.MaxValue;
			this.MaxStyle = int.MinValue;

			while (true)
			{
				if (this.nextLine >= this.lines.Length)
				{
					return false;
				}

				if (this.lines[this.nextLine++].StartsWith('='))
				{
					break;
				}
			}

			VertexStorage path = null;
			while (this.nextLine < this.lines.Length)
			{
				string line = this.lines[this.nextLine++];
				if (line.StartsWith('!'))
				{
					break;
				}

				string[] fields = line.Split(new[] { ' ', '\t', '\r' }, StringSplitOptions.RemoveEmptyEntries);
				if (line.StartsWith('P'))
				{
					int left = int.Parse(fields[1], CultureInfo.InvariantCulture);
					int right = int.Parse(fields[2], CultureInfo.InvariantCulture);
					path = new VertexStorage();
					path.MoveTo(Number(fields[4]), Number(fields[5]));
					this.paths.Add(path);
					this.styles.Add((left, right, int.Parse(fields[3], CultureInfo.InvariantCulture)));
					foreach (int fill in new[] { left, right })
					{
						if (fill >= 0)
						{
							this.MinStyle = Math.Min(this.MinStyle, fill);
							this.MaxStyle = Math.Max(this.MaxStyle, fill);
						}
					}
				}
				else if (line.StartsWith('C'))
				{
					path.Curve3(Number(fields[1]), Number(fields[2]), Number(fields[3]), Number(fields[4]));
				}
				else if (line.StartsWith('L'))
				{
					path.LineTo(Number(fields[1]), Number(fields[2]));
				}
			}

			return true;
		}

		/// <summary>
		/// C++ <c>scale(w, h)</c>: fits the shape's bounding box into a w by h window, centered, keeping its aspect
		/// ratio; curves are then flattened at the fit's scale. C++ measures the box on the raw path
		/// (bounding_rect(m_path, *this, ...)), so the curves' control points count, not the flattened curves.
		/// </summary>
		public void Scale(double width, double height)
		{
			this.affine = Affine.NewIdentity();
			double x1 = 0, y1 = 0, x2 = 0, y2 = 0;
			bool first = true;
			for (int i = 0; i < this.paths.Count; i++)
			{
				foreach (VertexData vertex in this.paths[i].Vertices())
				{
					if (vertex.IsStop)
					{
						break;
					}

					if (ShapePath.IsVertex(vertex.Command))
					{
						double x = vertex.Position.X;
						double y = vertex.Position.Y;
						if (first)
						{
							x1 = x2 = x;
							y1 = y2 = y;
							first = false;
						}
						else
						{
							if (x < x1) x1 = x;
							if (y < y1) y1 = y;
							if (x > x2) x2 = x;
							if (y > y2) y2 = y;
						}
					}
				}
			}

			if (x1 < x2 && y1 < y2)
			{
				var viewport = new Viewport();
				viewport.preserve_aspect_ratio(0.5, 0.5, Viewport.aspect_ratio_e.aspect_ratio_meet);
				viewport.world_viewport(x1, y1, x2, y2);
				viewport.device_viewport(0, 0, width, height);
				this.affine = viewport.to_affine();
			}

			this.curveApproximationScale = this.affine.GetScale();
		}

		/// <summary>C++ <c>approximation_scale(s)</c>: flatten curves at the fit's scale times <paramref name="scale"/>.</summary>
		public void ApproximationScale(double scale)
		{
			this.curveApproximationScale = this.affine.GetScale() * scale;
		}

		/// <summary>Path <paramref name="i"/> flattened, fitted to the window, then through <paramref name="view"/>
		/// (C++'s conv_transform of the shape by m_scale).</summary>
		public IVertexSource Path(int i, Affine view)
		{
			var flattened = new FlattenCurves(this.paths[i]) { ResolutionScale = this.curveApproximationScale };
			return new VertexSourceApplyTransform(new VertexSourceApplyTransform(flattened, this.affine), view);
		}

		// C atof on the file's plain decimal numbers.
		private static double Number(string text) => double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
	}
}
