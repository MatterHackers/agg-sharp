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
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;

namespace MatterHackers.AggSharpDemo.Demos
{
	/// <summary>
	/// The GPU path of the span-generator demos that <see cref="IGradientFillGraphics"/> cannot express (distortions'
	/// warped spans, graph_test's gradient_radial_d dots). A GPU surface has no scanline rasterizer to take a span generator, so
	/// the shape is tessellated into a mesh whose vertices are coloured by the demo's own span generator - each
	/// vertex gets exactly the color the software path gives a pixel centered on it - and drawn with
	/// <see cref="Graphics2D.DrawColoredPrimitives"/>.
	/// </summary>
	/// <remarks>
	/// The gap to the software path: between vertices the GPU interpolates the colors linearly, where the span
	/// generator looks every pixel up in its color table, so a band narrower than a mesh cell (a reflect seam, a
	/// conic's seam, a thin ring of the color table) is smoothed over.
	/// </remarks>
	public static class GradientMesh
	{
		/// <summary>
		/// A disc of <paramref name="rings"/> x <paramref name="sectors"/> cells around (<paramref name="cx"/>,
		/// <paramref name="cy"/>), in demo coordinates, drawn through <paramref name="transform"/>.
		/// </summary>
		public static void DrawDisc(Graphics2D graphics, Affine transform, SpanSampler sampler, double cx, double cy, double radius, int rings, int sectors)
		{
			graphics.DrawColoredPrimitives(DrawTopology.TriangleList, BuildDisc(transform, sampler, cx, cy, radius, rings, sectors));
		}

		/// <summary>
		/// The triangle list <see cref="DrawDisc"/> draws: the disc's cells, then a halo ring of two triangles per
		/// sector just outside the rim.
		/// </summary>
		/// <remarks>
		/// The halo is the anti-aliasing, done the way <c>HaloAaTesselator</c> does it for the other GPU fills: its
		/// inner edge is the rim with the rim's colors, its outer edge is <see cref="HaloWidth"/> device pixels
		/// further out with the same colors at alpha 0, so the edge fades out instead of stair-stepping. The
		/// width is divided by the transform's scale so it stays one pixel on screen at any view zoom.
		/// </remarks>
		public static PosColorVertex[] BuildDisc(Affine transform, SpanSampler sampler, double cx, double cy, double radius, int rings, int sectors)
		{
			Vector2 DiscPoint(int i, double r)
			{
				double angle = i * 2 * Math.PI / sectors;
				return new Vector2(cx + (r * Math.Cos(angle)), cy + (r * Math.Sin(angle)));
			}

			PosColorVertex[] cells = BuildGrid(transform, sampler, sectors, rings, (i, j) => DiscPoint(i, radius * j / rings));

			double haloRadius = radius + (HaloWidth / transform.GetScale());
			var rim = new PosColorVertex[sectors + 1];
			var halo = new PosColorVertex[sectors + 1];
			for (int i = 0; i <= sectors; i++)
			{
				Vector2 rimPoint = DiscPoint(i, radius);
				Color color = sampler.Sample(rimPoint.X, rimPoint.Y);
				rim[i] = new PosColorVertex(Transformed(transform, rimPoint), color);
				halo[i] = new PosColorVertex(Transformed(transform, DiscPoint(i, haloRadius)), new Color(color.red, color.green, color.blue, 0));
			}

			var vertices = new PosColorVertex[cells.Length + (sectors * 6)];
			cells.CopyTo(vertices, 0);
			int v = cells.Length;
			for (int i = 0; i < sectors; i++)
			{
				vertices[v++] = rim[i];
				vertices[v++] = halo[i];
				vertices[v++] = halo[i + 1];
				vertices[v++] = rim[i];
				vertices[v++] = halo[i + 1];
				vertices[v++] = rim[i + 1];
			}

			return vertices;
		}

		/// <summary>How far outside the disc's rim its anti-aliasing halo reaches, in device pixels.</summary>
		public const double HaloWidth = 1;

		/// <summary>A <paramref name="columns"/> x <paramref name="rows"/> grid of quads, two triangles each, placed by <paramref name="position"/>.</summary>
		private static PosColorVertex[] BuildGrid(Affine transform, SpanSampler sampler, int columns, int rows, Func<int, int, Vector2> position)
		{
			var points = new Vector2[columns + 1, rows + 1];
			var colors = new Color[columns + 1, rows + 1];
			for (int i = 0; i <= columns; i++)
			{
				for (int j = 0; j <= rows; j++)
				{
					Vector2 p = position(i, j);
					colors[i, j] = sampler.Sample(p.X, p.Y);
					points[i, j] = Transformed(transform, p);
				}
			}

			var vertices = new PosColorVertex[columns * rows * 6];
			int v = 0;
			for (int i = 0; i < columns; i++)
			{
				for (int j = 0; j < rows; j++)
				{
					vertices[v++] = new PosColorVertex(points[i, j], colors[i, j]);
					vertices[v++] = new PosColorVertex(points[i + 1, j], colors[i + 1, j]);
					vertices[v++] = new PosColorVertex(points[i + 1, j + 1], colors[i + 1, j + 1]);
					vertices[v++] = new PosColorVertex(points[i, j], colors[i, j]);
					vertices[v++] = new PosColorVertex(points[i + 1, j + 1], colors[i + 1, j + 1]);
					vertices[v++] = new PosColorVertex(points[i, j + 1], colors[i, j + 1]);
				}
			}

			return vertices;
		}

		private static Vector2 Transformed(Affine transform, Vector2 point)
		{
			double x = point.X;
			double y = point.Y;
			transform.Transform(ref x, ref y);
			return new Vector2(x, y);
		}
	}

	/// <summary>
	/// A span generator asked for single points: the color it would give the pixel centered on a demo-space
	/// point. Each of its interpolators is re-aimed so its one-pixel span at (0, 0) lands on that point.
	/// </summary>
	public sealed class SpanSampler
	{
		private readonly ISpanGenerator generator;
		private readonly (span_interpolator_linear Interpolator, Affine DemoToSpace)[] spaces;
		private readonly Color[] span = new Color[1];

		/// <param name="generator">The span generator, built on the interpolators in <paramref name="spaces"/>.</param>
		/// <param name="spaces">Each interpolator with the demo-to-gradient matrix the software path gives it.</param>
		public SpanSampler(ISpanGenerator generator, params (span_interpolator_linear Interpolator, Affine DemoToSpace)[] spaces)
		{
			this.generator = generator;
			this.spaces = spaces;
		}

		public Color Sample(double x, double y)
		{
			foreach (var space in this.spaces)
			{
				// generate() starts at pixel (0, 0)'s center, (0.5, 0.5); move that onto (x, y).
				space.Interpolator.transformer(Affine.NewTranslation(x - 0.5, y - 0.5) * space.DemoToSpace);
			}

			this.generator.generate(this.span, 0, 0, 0, 1);
			return this.span[0];
		}
	}
}
