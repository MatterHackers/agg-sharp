using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters2D;
using MatterHackers.RenderGl;
using MatterHackers.VectorMath;
using Tesselate;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests
{
	public class HaloAaTesselatorTests
	{
		private static HaloAaTesselator Tessellate(IVertexSource path, Tesselator.WindingRuleType windingRule = Tesselator.WindingRuleType.NonZero)
		{
			var tesselator = new HaloAaTesselator { WindingRule = windingRule };
			VertexSourceToTesselator.SendShapeToTesselator(tesselator, path);
			tesselator.BuildHaloMesh();
			return tesselator;
		}

		private static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
		{
			double Cross(Vector2 o, Vector2 u, Vector2 v) => (u.X - o.X) * (v.Y - o.Y) - (u.Y - o.Y) * (v.X - o.X);
			double d0 = Cross(a, b, p), d1 = Cross(b, c, p), d2 = Cross(c, a, p);
			return (d0 >= 0 && d1 >= 0 && d2 >= 0) || (d0 <= 0 && d1 <= 0 && d2 <= 0);
		}

		/// <summary>Whether a point lies in an interior (fully opaque) triangle - the ones libtess made.</summary>
		private static bool CoveredByInterior(HaloAaTesselator mesh, Vector2 point)
		{
			for (int i = 0; i + 2 < mesh.IndicesCache.Count; i += 3)
			{
				if (InTriangle(point,
					mesh.VerticesCache[mesh.IndicesCache[i].Index].Position,
					mesh.VerticesCache[mesh.IndicesCache[i + 1].Index].Position,
					mesh.VerticesCache[mesh.IndicesCache[i + 2].Index].Position))
				{
					return true;
				}
			}

			return false;
		}

		[Test]
		public async Task InteriorIsOpaqueAndEveryOutlineEdgeFadesHalfAPixelOutward()
		{
			// Clockwise on purpose: the halo direction must not depend on winding.
			var square = new VertexStorage();
			square.MoveTo(10, 10);
			square.LineTo(10, 20);
			square.LineTo(20, 20);
			square.LineTo(20, 10);
			square.ClosePolygon();

			var mesh = Tessellate(square);

			// Only outline edges get a halo: four quads, whatever diagonal libtess chose.
			int interiorVertices = mesh.VerticesCache.Count;
			await Assert.That(mesh.HaloVertices.Count - interiorVertices).IsEqualTo(16);
			await Assert.That(mesh.HaloVertices.Take(interiorVertices).All(v => v.Alpha == 1)).IsTrue();

			foreach (var outer in mesh.HaloVertices.Skip(interiorVertices).Where(v => v.Alpha == 0))
			{
				// Half a pixel outside the square, never inside it.
				var p = outer.Position;
				double outside = System.Math.Max(System.Math.Max(10 - p.X, p.X - 20), System.Math.Max(10 - p.Y, p.Y - 20));
				await Assert.That(outside).IsEqualTo(.5).Within(1e-12);
			}

			// The ramp falls one coverage level per pixel, so the edge itself is half covered.
			await Assert.That(mesh.HaloVertices.Skip(interiorVertices).Count(v => v.Alpha == .5f)).IsEqualTo(8);
		}

		/// <summary>
		/// An end_poly without the close flag (what SmoothPolygon ends an open path with) ends the contour and adds
		/// no point; its position is (0, 0), which as a vertex drew a wedge out to the origin (conv_dash_marker).
		/// </summary>
		[Test]
		public async Task OpenEndPolyAddsNoVertex()
		{
			var triangle = new VertexStorage();
			triangle.MoveTo(40, 40);
			triangle.LineTo(80, 40);
			triangle.LineTo(60, 80);
			triangle.Add(0, 0, FlagsAndCommand.EndPoly);

			var mesh = Tessellate(triangle);

			await Assert.That(CoveredByInterior(mesh, new Vector2(20, 20))).IsFalse();
			await Assert.That(CoveredByInterior(mesh, new Vector2(60, 50))).IsTrue();
		}

		[Test]
		public async Task EvenOddLeavesThePentagramCenterOpen()
		{
			var star = new VertexStorage();
			for (int i = 0; i < 5; i++)
			{
				double angle = System.Math.PI / 2 + i * 4 * System.Math.PI / 5;
				var point = new Vector2(50 + 40 * System.Math.Cos(angle), 50 + 40 * System.Math.Sin(angle));
				if (i == 0)
				{
					star.MoveTo(point);
				}
				else
				{
					star.LineTo(point);
				}
			}

			star.ClosePolygon();

			var center = new Vector2(50, 50);
			var arm = new Vector2(50, 85);
			var nonZero = Tessellate(star);
			var evenOdd = Tessellate(star, Tesselator.WindingRuleType.Odd);

			await Assert.That(CoveredByInterior(nonZero, center)).IsTrue();
			await Assert.That(CoveredByInterior(evenOdd, center)).IsFalse();
			await Assert.That(CoveredByInterior(evenOdd, arm)).IsTrue();
		}
	}
}
