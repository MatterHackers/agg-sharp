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

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using MatterHackers.VectorMath;

namespace MatterHackers.PolygonMesh
{
	/// <summary>
	/// Whole-mesh measurements: enclosed volume and total surface area, in the mesh's own units.
	/// </summary>
	public static class MeshMeasure
	{
		/// <summary>
		/// The volume a closed, consistently wound mesh encloses, by the divergence theorem: the sum over
		/// every triangle of the signed volume of the tetrahedron it makes with the origin, a . (b x c) / 6.
		/// </summary>
		/// <remarks>
		/// The sign follows the winding: outward-facing (counter-clockwise seen from outside, as
		/// <see cref="Face.normal"/> is computed) gives a positive volume, an inside-out mesh the same volume
		/// negated. Several closed shells add, so a shell wound inward subtracts (a cavity).
		/// On an open mesh (a hole, a missing face) or one whose winding is inconsistent the sum is still
		/// returned but it is not a volume: it changes as the mesh moves relative to the origin. Check the
		/// mesh is closed first if the answer has to mean something. Sums in double from the float vertices.
		/// </remarks>
		public static double GetVolume(this Mesh mesh)
		{
			return GetVolume(mesh, Matrix4X4.Identity);
		}

		/// <summary>
		/// <see cref="GetVolume(Mesh)"/> of the mesh after <paramref name="matrix"/> is applied to every
		/// vertex, without copying the mesh - the world-space volume of an object is its mesh's volume under
		/// its world matrix.
		/// </summary>
		/// <remarks>
		/// For an affine matrix this is the untransformed volume times the determinant of its 3x3 part, so a
		/// mirror negates it; the vertices are transformed rather than multiplying by the determinant so a
		/// caller's matrix never has to be affine for the result to be the transformed mesh's volume.
		/// </remarks>
		public static double GetVolume(this Mesh mesh, Matrix4X4 matrix)
		{
			var sixTimesVolume = 0.0;
			var vertices = mesh.Vertices;

			foreach (var face in mesh.Faces)
			{
				var a = new Vector3(vertices[face.v0]).Transform(matrix);
				var b = new Vector3(vertices[face.v1]).Transform(matrix);
				var c = new Vector3(vertices[face.v2]).Transform(matrix);

				sixTimesVolume += a.Dot(b.Cross(c));
			}

			return sixTimesVolume / 6;
		}

		/// <summary>
		/// The total area of every triangle in the mesh, |(b - a) x (c - a)| / 2 each. Winding and closure
		/// do not matter; zero-area faces add nothing.
		/// </summary>
		public static double GetSurfaceArea(this Mesh mesh)
		{
			return GetSurfaceArea(mesh, Matrix4X4.Identity);
		}

		/// <summary>
		/// <see cref="GetSurfaceArea(Mesh)"/> of the mesh after <paramref name="matrix"/> is applied to every
		/// vertex. Area has no single scale factor under a non-uniform scale, so the vertices are transformed.
		/// </summary>
		public static double GetSurfaceArea(this Mesh mesh, Matrix4X4 matrix)
		{
			var twiceArea = 0.0;
			var vertices = mesh.Vertices;

			foreach (var face in mesh.Faces)
			{
				var a = new Vector3(vertices[face.v0]).Transform(matrix);
				var b = new Vector3(vertices[face.v1]).Transform(matrix);
				var c = new Vector3(vertices[face.v2]).Transform(matrix);

				twiceArea += (b - a).Cross(c - a).Length;
			}

			return twiceArea / 2;
		}
	}
}
