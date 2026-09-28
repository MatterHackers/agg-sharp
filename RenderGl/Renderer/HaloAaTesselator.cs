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

using System.Collections.Generic;
using MatterHackers.Agg;
using MatterHackers.DataConverters2D;
using MatterHackers.RenderGl.OpenGl;
using MatterHackers.VectorMath;

namespace MatterHackers.RenderGl
{
	/// <summary>
	/// One vertex of a halo anti-aliased fill: a position and the coverage (0 or 1) the rasterizer
	/// interpolates across the triangle.
	/// </summary>
	public readonly record struct HaloAaVertex(Vector2 Position, float Alpha);

	/// <summary>
	/// Anti-aliased fills by halo strips, ported from agg-gui's <c>tessellate_path_aa</c>
	/// (<c>agg-gui/src/gl_renderer/tess2_bridge.rs</c>). The path is tessellated once; every triangle
	/// is drawn fully opaque, and every original outline edge (libtess's edge flag) adds a quad that
	/// runs from the edge at coverage <see cref="HaloWidth"/> to the edge pushed <see cref="HaloWidth"/>
	/// outward at coverage 0. Coverage is then plain interpolation across that quad.
	/// </summary>
	/// <remarks>
	/// This replaces <see cref="AARenderTesselator"/>, which faded each triangle only against its own
	/// outline edge: near an outline vertex the interior diagonals then carried part of the ramp, so
	/// sub-ULP geometry changes that moved a diagonal flipped a pixel between ~50% and 100%. Here the
	/// interior is always exactly opaque and the fade lives entirely outside the outline, so the
	/// tessellation's choice of diagonals can not change a pixel's coverage.
	/// <para>
	/// <b>Known limitation, shared with agg-gui:</b> the mesh is drawn in one pass with ordinary source-over
	/// blending, so wherever two pieces overlap a pixel is blended twice. At a concave outline vertex the
	/// halo quads of the two edges overlap each other (and at any vertex a halo can overlap the interior),
	/// so a translucent fill shows slightly darker specks there. Opaque fills are unaffected.
	/// </para>
	/// </remarks>
	public class HaloAaTesselator : CachedTesselator
	{
		/// <summary>
		/// How far outside the outline the coverage ramp reaches, in the tessellated units (screen pixels).
		/// </summary>
		/// <remarks>
		/// The ramp falls one coverage level per pixel, so the edge itself carries coverage
		/// <see cref="HaloWidth"/>. At the default half pixel a pixel whose centre is d outside the edge
		/// gets 0.5 - d: an edge on a pixel boundary leaves the outside pixel untouched and an edge through
		/// a pixel centre half covers it, as software AGG's area coverage does. agg-gui's halo (and this
		/// one before it) ran from coverage 1 at the edge to 0 a whole pixel out, which half covered the
		/// pixel outside every aligned edge and drew each shape about a pixel fatter than software.
		/// Pixels whose centre is inside the outline stay opaque, so within half a pixel inside an edge the
		/// fill still over-covers slightly (an eighth of a pixel per unit of edge on average); fading the
		/// interior instead would bring back the diagonal-dependent coverage described above.
		/// </remarks>
		public double HaloWidth { get; set; } = .5;

		/// <summary>The halo mesh <see cref="BuildHaloMesh"/> made: interior vertices first, then four per outline edge.</summary>
		public List<HaloAaVertex> HaloVertices { get; } = new List<HaloAaVertex>();

		/// <summary>Triangle indices into <see cref="HaloVertices"/>, three per triangle.</summary>
		public List<int> HaloIndices { get; } = new List<int>();

		/// <summary>
		/// Turns the last tessellation (<see cref="CachedTesselator.VerticesCache"/> and its edge flagged
		/// <see cref="CachedTesselator.IndicesCache"/>) into the halo mesh.
		/// </summary>
		public void BuildHaloMesh()
		{
			HaloVertices.Clear();
			HaloIndices.Clear();

			// Interior triangles - coverage 1 everywhere.
			foreach (var vertex in VerticesCache)
			{
				HaloVertices.Add(new HaloAaVertex(vertex.Position, 1));
			}

			foreach (var index in IndicesCache)
			{
				HaloIndices.Add(index.Index);
			}

			// One quad per outline edge. libtess flags the edge that starts at a vertex, so edge k runs
			// from vertex k to vertex k + 1 of its triangle.
			for (int i = 0; i + 2 < IndicesCache.Count; i += 3)
			{
				for (int k = 0; k < 3; k++)
				{
					if (!IndicesCache[i + k].IsEdge)
					{
						continue;
					}

					var a = VerticesCache[IndicesCache[i + k].Index].Position;
					var b = VerticesCache[IndicesCache[i + (k + 1) % 3].Index].Position;
					var c = VerticesCache[IndicesCache[i + (k + 2) % 3].Index].Position;
					var edge = b - a;
					double length = edge.Length;
					if (length < 1e-6)
					{
						continue;
					}

					// The right-hand perpendicular, flipped if it points toward the triangle's third
					// vertex. A winding assumption would not do: libtess emits clockwise triangles for
					// clockwise input and inside self-intersections, and their halos would point inward.
					var normal = new Vector2(edge.Y, -edge.X) * (HaloWidth / length);
					if (Vector2.Dot(normal, c - a) > 0)
					{
						normal = -normal;
					}

					int start = HaloVertices.Count;
					float edgeCoverage = (float)System.Math.Min(1, HaloWidth);
					HaloVertices.Add(new HaloAaVertex(a, edgeCoverage));
					HaloVertices.Add(new HaloAaVertex(b, edgeCoverage));
					HaloVertices.Add(new HaloAaVertex(a + normal, 0));
					HaloVertices.Add(new HaloAaVertex(b + normal, 0));
					HaloIndices.Add(start);
					HaloIndices.Add(start + 1);
					HaloIndices.Add(start + 2);
					HaloIndices.Add(start + 1);
					HaloIndices.Add(start + 3);
					HaloIndices.Add(start + 2);
				}
			}
		}

		/// <summary>
		/// Draws the halo mesh as immediate mode triangles in <paramref name="color"/>, premultiplied by
		/// each vertex's coverage, for a <c>One, OneMinusSrcAlpha</c> blend with texturing off.
		/// </summary>
		/// <param name="gl">The context to draw through.</param>
		/// <param name="color">The fill color, not premultiplied.</param>
		public void Render(GL gl, Color color)
		{
			gl.Begin(BeginMode.Triangles);
			foreach (int index in HaloIndices)
			{
				var vertex = HaloVertices[index];
				int alpha = (int)(color.alpha * vertex.Alpha + .5f);
				gl.Color4(
					(byte)((color.red * alpha + 127) / 255),
					(byte)((color.green * alpha + 127) / 255),
					(byte)((color.blue * alpha + 127) / 255),
					(byte)alpha);
				gl.Vertex2(vertex.Position.X, vertex.Position.Y);
			}

			gl.End();
		}
	}
}
