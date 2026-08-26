/*
Copyright (c) 2014, Lars Brubaker
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
using MIConvexHull;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace MatterHackers.PolygonMesh
{
	public static class MeshConvexHull
	{
		public static string ConvexHullMesh => nameof(ConvexHullMesh);

		public static string CreatingConvexHullMesh => nameof(CreatingConvexHullMesh);

		/// <summary>
		/// Mesh.PropertyBag is a plain Dictionary, and the two hull keys are read and written by
		/// background hull tasks as well as by calling threads, so all access goes through this lock.
		/// </summary>
		private static readonly object hullCacheLock = new object();

		/// <summary>
		/// Gets the convex hull of a mesh, caching it on the mesh (and dropping the cache when the
		/// mesh changes).
		/// </summary>
		/// <param name="mesh">The mesh to build the hull of. Not recursed into children.</param>
		/// <param name="generateAsync">When true, the caller accepts a null return while the hull is
		/// built on a background thread. When false, the hull is built on the calling thread.</param>
		/// <returns>The hull mesh, or null if there is no hull yet (or the mesh is degenerate).</returns>
		/// <remarks>
		/// This call never blocks. A sync caller wants a hull now, so if a background task is already
		/// building one it builds its own instead of waiting: the hull is a pure function of the mesh
		/// vertices and the cache write is last wins, so the duplicated work is harmless - and far
		/// cheaper than parking a thread (fatal on a single threaded host such as the browser).
		/// </remarks>
		public static Mesh GetConvexHull(this Mesh mesh, bool generateAsync)
		{
			// Async generation stays off: a background task walking a mesh the owning thread may still
			// be editing is the original "threading issue" and re-enabling it is a separate decision.
			// The async path below is maintained (it cleans up after itself even when the build throws)
			// but it is unexercised, so treat it as a starting point rather than as proven.
			generateAsync = false;

			if (mesh.Faces.Count < 4)
			{
				return null;
			}

			lock (hullCacheLock)
			{
				if (mesh.PropertyBag.TryGetValue(ConvexHullMesh, out var meshData)
					&& meshData is Mesh convexHullMesh)
				{
					return convexHullMesh;
				}

				if (generateAsync)
				{
					// Store the in flight task rather than a bare marker so a future async caller can
					// join it instead of starting a second identical hull.
					if (!mesh.PropertyBag.ContainsKey(CreatingConvexHullMesh))
					{
						mesh.PropertyBag[CreatingConvexHullMesh] = Task.Run(() =>
						{
							try
							{
								return CreateHullMesh(mesh);
							}
							catch (Exception ex)
							{
								// Building from a mesh the owning thread mutates can throw out of the vertex
								// walk. Nobody awaits this task, so swallow it here (an unobserved fault is
								// worse) and let the finally retire the marker - otherwise the failed build
								// stays "in flight" forever and no later caller can ever hull this mesh.
								Debug.WriteLine($"Convex hull generation failed: {ex}");
								return null;
							}
							finally
							{
								ClearInFlightMarker(mesh);
							}
						});
					}

					return null;
				}
			}

			return CreateHullMesh(mesh);
		}

		private static Mesh CreateHullMesh(Mesh mesh)
		{
			var bounds = AxisAlignedBoundingBox.Empty();
			// Get the convex hull for the mesh
			var cHVertexList = new List<CHVertex>();
			foreach (var position in mesh.Vertices.Distinct().ToArray())
			{
				cHVertexList.Add(new CHVertex(position));
				bounds.ExpandToInclude(position);
			}

			var tollerance = .01;

			if (cHVertexList.Count == 0
				|| bounds.XSize <= tollerance
				|| bounds.YSize <= tollerance
				|| bounds.ZSize <= tollerance
				|| double.IsNaN(cHVertexList.First().Position[0]))
			{
				// degenerate (flat or empty) - the mesh is its own best hull, but don't cache it
				ClearInFlightMarker(mesh);
				return mesh;
			}

			var convexHull = ConvexHull.Create<CHVertex, CHFace>(cHVertexList, tollerance);
			if (convexHull?.Result != null)
			{
				// create the mesh from the hull data
				Mesh hullMesh = new Mesh();
				foreach (var face in convexHull.Result.Faces)
				{
					int vertexCount = hullMesh.Vertices.Count;

					foreach (var vertex in face.Vertices)
					{
						hullMesh.Vertices.Add(new Vector3(vertex.Position[0], vertex.Position[1], vertex.Position[2]));
					}

					hullMesh.Faces.Add(vertexCount, vertexCount + 1, vertexCount + 2, hullMesh.Vertices);
				}

				lock (hullCacheLock)
				{
					// last wins - two threads hulling the same (unchanged) mesh produce equivalent hulls
					mesh.PropertyBag[ConvexHullMesh] = hullMesh;

					// we are done building, whether or not anyone was tracking us
					mesh.PropertyBag.Remove(CreatingConvexHullMesh);

					// subscribed under the same lock the hull was published under, so a change cannot slip
					// between the two and leave this now stale hull cached until the change after it
					mesh.Changed += MeshChanged_RemoveConvexHull;
				}

				return hullMesh;
			}

			ClearInFlightMarker(mesh);
			return null;
		}

		private static void ClearInFlightMarker(Mesh mesh)
		{
			lock (hullCacheLock)
			{
				// cleared even when no hull was produced so a later request is free to try again
				mesh.PropertyBag.Remove(CreatingConvexHullMesh);
			}
		}

		private static void MeshChanged_RemoveConvexHull(object sender, EventArgs e)
		{
			if (sender is Mesh mesh)
			{
				mesh.Changed -= MeshChanged_RemoveConvexHull;

				lock (hullCacheLock)
				{
					// remove any cached hull as it is no longer valid (the mesh changed)
					mesh.PropertyBag.Remove(ConvexHullMesh);

					// and any in flight hull, which is building from the old vertices
					mesh.PropertyBag.Remove(CreatingConvexHullMesh);
				}
			}
		}

		internal class CHFace : ConvexFace<CHVertex, CHFace>
		{
		}

		internal class CHVertex : MIConvexHull.IVertex
		{
			private double[] position;

			internal CHVertex(Vector3 position)
			{
				this.position = position.ToArray();
			}

			internal CHVertex(Vector3Float position)
			{
				this.position = new double[] { position.X, position.Y, position.Z };
			}

			public double[] Position => position;
		}
	}
}