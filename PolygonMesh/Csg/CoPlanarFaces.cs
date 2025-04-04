/*
Copyright (c) 2025, Lars Brubaker, John Lewin
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

using System.Collections.Generic;
using System.Linq;
using ClipperLib;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.VectorMath;

namespace MatterHackers.PolygonMesh.Csg
{
    using Polygon = List<IntPoint>;
    using Polygons = List<List<IntPoint>>;

	public class CoPlanarFaces
	{
		private Dictionary<Plane, Dictionary<int, List<(int sourceFaceIndex, int destFaceIndex)>>> coPlanarFaces
			= new Dictionary<Plane, Dictionary<int, List<(int sourceFaceIndex, int destFaceIndex)>>>();

		private SimilarPlaneFinder similarPlaneFinder;

		public CoPlanarFaces(SimilarPlaneFinder planeSorter)
		{
			this.similarPlaneFinder = planeSorter;
		}

        public IEnumerable<Plane> Planes
		{
			get
			{
				foreach (var plane in coPlanarFaces.Keys)
				{
					yield return plane;
				}
			}
		}

		public IEnumerable<int> MeshFaceIndicesForPlane(Plane plane)
		{
			if (coPlanarFaces.ContainsKey(plane))
			{
				foreach (var kvp in coPlanarFaces[plane])
				{
					yield return kvp.Key;
				}
			}
		}

		public IEnumerable<(int sourceFaceIndex, int destFaceIndex)> FacesSetsForPlaneAndMesh(Plane plane, int meshIndex)
		{
            if (!coPlanarFaces.TryGetValue(plane, out var meshIndexMap))
            {
                yield break;
            }
            
            if (coPlanarFaces[plane].ContainsKey(meshIndex))
			{
				foreach (var faceIndices in coPlanarFaces[plane][meshIndex])
				{
					yield return faceIndices;
				}
			}
		}

		public static Polygon GetFacePolygon(Mesh mesh1, int faceIndex, Matrix4X4 meshTo0Plane)
		{
			var facePolygon = new Polygon();
			var vertices = mesh1.Vertices;
			var face = mesh1.Faces[faceIndex];
			var vertIndices = new int[] { face.v0, face.v1, face.v2 };
			var vertsOnPlane = new Vector3[3];
			for (int i = 0; i < 3; i++)
			{
				vertsOnPlane[i] = Vector3Ex.Transform(vertices[vertIndices[i]].AsVector3(), meshTo0Plane);
				var pointOnPlane = new IntPoint(vertsOnPlane[i].X, vertsOnPlane[i].Y);
				facePolygon.Add(pointOnPlane);
			}

			return facePolygon;
		}

        // Optional cache: Key = (mesh, faceIndex, transformPointer)
        private Dictionary<(Mesh mesh, int faceIndex, Matrix4X4 transform), Polygon> facePolygonCache
            = new Dictionary<(Mesh, int, Matrix4X4), Polygon>();

        /// <summary>
        /// Returns a face polygon from the cache if available; otherwise computes, stores, and returns it.
        /// </summary>
        public Polygon GetFacePolygonCached(Mesh mesh, int faceIndex, Matrix4X4 meshTo0Plane)
        {
            var key = (mesh, faceIndex, meshTo0Plane);

            if (!facePolygonCache.TryGetValue(key, out var cachedPolygon))
            {
                // Compute via the existing code
                cachedPolygon = GetFacePolygon(mesh, faceIndex, meshTo0Plane);
                facePolygonCache[key] = cachedPolygon;
            }

            return cachedPolygon;
        }

        public void SubtractFaces(Plane plane, List<Mesh> transformedMeshes, Mesh resultsMesh, Matrix4X4 planeTransformToXy, HashSet<int> faceIndicesToRemove)
        {
            // get all meshes that have faces on this plane
            var meshesFaceIndicesForPlane = MeshFaceIndicesForPlane(plane).ToList();

            // we need more than one mesh and one of them needs to be the source (mesh 0)
            if (meshesFaceIndicesForPlane.Count < 2
                || !meshesFaceIndicesForPlane.Contains(0))
            {
                // no faces to add
                return;
            }

            // sort them so we can process each group into intersections
            meshesFaceIndicesForPlane.Sort();

            // add the faces that we should remove
            foreach (var meshIndex in meshesFaceIndicesForPlane)
            {
                foreach (var faces in FacesSetsForPlaneAndMesh(plane, meshIndex))
                {
                    faceIndicesToRemove.Add(faces.destFaceIndex);
                }
            }

            // subtract every face from the mesh 0 faces
            // teselate and add what is left
            var keepPolygons = new Polygons();
            foreach (var keepFaceSets in FacesSetsForPlaneAndMesh(plane, 0))
            {
                var facePolygon = GetFacePolygonCached(transformedMeshes[0], keepFaceSets.sourceFaceIndex, planeTransformToXy);
                keepPolygons = keepPolygons.Union(facePolygon);
            }

            // iterate all the meshes that need to be subtracted
            var removePoygons = new Polygons();
            for (int removeMeshIndex = 1; removeMeshIndex < meshesFaceIndicesForPlane.Count; removeMeshIndex++)
            {
                foreach (var removeFaceSets in FacesSetsForPlaneAndMesh(plane, removeMeshIndex))
                {
                    removePoygons = removePoygons.Union(GetFacePolygonCached(transformedMeshes[removeMeshIndex], removeFaceSets.sourceFaceIndex, planeTransformToXy));
                }
            }

            var polygonShape = new Polygons();
            var clipper = new Clipper();
            clipper.AddPaths(keepPolygons, PolyType.ptSubject, true);
            clipper.AddPaths(removePoygons, PolyType.ptClip, true);
            clipper.Execute(ClipType.ctDifference, polygonShape);

            // teselate and add all the new polygons
            var countPreAdd = resultsMesh.Faces.Count;
            polygonShape.AsVertices(1).TriangulateFaces(null, resultsMesh, 0, planeTransformToXy.Inverted);
            EnsureFaceNormals(plane, resultsMesh, countPreAdd);
        }

		private static void EnsureFaceNormals(Plane plane, Mesh resultsMesh, int countPreAdd)
		{
			if (countPreAdd >= resultsMesh.Faces.Count)
			{
				return;
			}

			// Check that the new face normals are pointed in the right direction
			for (int i = countPreAdd; i < resultsMesh.Faces.Count; i++)
			{
				if ((new Vector3(resultsMesh.Faces[i].normal) - plane.Normal).LengthSquared > .1)
				{
					resultsMesh.FlipFace(i);
				}
			}
		}

        public void IntersectFaces(Plane plane, List<Mesh> transformedMeshes, Mesh resultsMesh, Matrix4X4 planeTransformToXy, HashSet<int> faceIndicesToRemove)
		{
			// get all meshes that have faces on this plane
			var meshesWithFaces = MeshFaceIndicesForPlane(plane).ToList();

			// we need more than one mesh
			if (meshesWithFaces.Count < 2)
			{
				// no faces to add
				return;
			}

			// add the faces that we should remove
			foreach (var meshIndex in meshesWithFaces)
			{
				foreach (var faces in FacesSetsForPlaneAndMesh(plane, meshIndex))
				{
					faceIndicesToRemove.Add(faces.destFaceIndex);
				}
			}

			var polygonsByMesh = new List<Polygons>();
			// iterate all the meshes that need to be intersected
			foreach (var meshIndex in meshesWithFaces)
			{
				var unionedPoygons = new Polygons();
				foreach (var removeFaceSets in FacesSetsForPlaneAndMesh(plane, meshIndex))
				{
					unionedPoygons = unionedPoygons.Union(GetFacePolygonCached(transformedMeshes[meshIndex], removeFaceSets.sourceFaceIndex, planeTransformToXy));
				}

				polygonsByMesh.Add(unionedPoygons);
			}

			var total = new Polygons(polygonsByMesh[0]);
			for (int i = 1; i < polygonsByMesh.Count; i++)
			{
				var polygonShape = new Polygons();
				var clipper = new Clipper();
				clipper.AddPaths(total, PolyType.ptSubject, true);
				clipper.AddPaths(polygonsByMesh[i], PolyType.ptClip, true);
				clipper.Execute(ClipType.ctIntersection, polygonShape);

				total = polygonShape;
			}

			// teselate and add all the new polygons
			var countPreAdd = resultsMesh.Faces.Count;
			total.AsVertices(1).TriangulateFaces(null, resultsMesh, 0, planeTransformToXy.Inverted);
			EnsureFaceNormals(plane, resultsMesh, countPreAdd);
		}

        public void UnionFaces(Plane positivePlane,
            List<Mesh> transformedMeshes,
            Mesh resultsMesh,
            Matrix4X4 planeTransformToXy,
            HashSet<int> faceIndicesToRemove,
            CsgBySlicing csgData)
        {
            // get all meshes that have faces on this plane
            var meshesWithFaces = MeshFaceIndicesForPlane(positivePlane).ToList();

            // TODO: Consider passing tolerance from CsgBySlicing if needed for consistency
            var negativePlane = similarPlaneFinder.FindPlane(new Plane()
            {
                Normal = -positivePlane.Normal,
                DistanceFromOrigin = -positivePlane.DistanceFromOrigin,
            }, CsgBySlicing.CoplanarPlaneDistanceTolerance); // Use the constant from CsgBySlicing

            if (negativePlane != null)
            {
                // add any negative faces
                meshesWithFaces.AddRange(MeshFaceIndicesForPlane(negativePlane.Value));
            }

            if (meshesWithFaces.Count < 2)
            {
                return;
            }

            // add the faces that we should union
            foreach (var meshIndex in meshesWithFaces)
            {
                foreach (var faces in FacesSetsForPlaneAndMesh(positivePlane, meshIndex))
                {
                    faceIndicesToRemove.Add(faces.destFaceIndex);
                }
            }

            // sort them so we can process each group into intersections
            meshesWithFaces.Sort();

            Polygons firstPositivePolygons = null;
            var unionPolygons = new Polygons();
            var first = true;

            foreach (var meshIndex in meshesWithFaces)
            {
                var meshPolygons = new Polygons();
                var addedFaces = new HashSet<int>();

                foreach (var (sourceFaceIndex, destFaceIndex) in this.FacesSetsForPlaneAndMesh(positivePlane, meshIndex))
                {
                    if (!addedFaces.Contains(sourceFaceIndex))
                    {
                        var facePolygon = GetFacePolygonCached(transformedMeshes[meshIndex], sourceFaceIndex, planeTransformToXy);
                        meshPolygons.Add(facePolygon);
                        addedFaces.Add(sourceFaceIndex);
                    }
                }

                if (first)
                {
                    firstPositivePolygons = meshPolygons;
                    first = false;
                }
                else
                {
                    unionPolygons.AddRange(meshPolygons);
                }
            }

            // now union all the intersections
            // clip against the slice based on the parameters
            var unionClipper = new Clipper();
            unionClipper.AddPaths(firstPositivePolygons, PolyType.ptSubject, true);
            unionClipper.AddPaths(unionPolygons, PolyType.ptClip, true);

            var totalSlices = new Polygons();
            unionClipper.Execute(ClipType.ctUnion, totalSlices, PolyFillType.pftNonZero);

            var subtractPolygons = csgData.GetTotalSlice(-1, new Plane()
            {
                // We are checking if this surface is inside of another object.
                // Offset to test slightly above the surface of the plane.
                // .04 is an amount that was found to be good for the custom spool holder layers to merge well.
                // This bias can cause us to keep surfaces that should be removed.
                DistanceFromOrigin = positivePlane.DistanceFromOrigin + .04,
                Normal = positivePlane.Normal
            },
            planeTransformToXy);

            if (subtractPolygons.Count > 0)
            {
                // subtract them from the other union faces
                var subtractionClipper = new Clipper();
                subtractionClipper.AddPaths(totalSlices, PolyType.ptSubject, true);
                subtractionClipper.AddPaths(subtractPolygons, PolyType.ptClip, true);

                var totalSlices2 = new Polygons();
                subtractionClipper.Execute(ClipType.ctDifference, totalSlices2, PolyFillType.pftNonZero);
                totalSlices = totalSlices2;
            }

            // teselate and add all the new polygons
            var countPreAdd = resultsMesh.Faces.Count;
            totalSlices.AsVertices(1).TriangulateFaces(null, resultsMesh, 0, planeTransformToXy.Inverted);
            EnsureFaceNormals(positivePlane, resultsMesh, countPreAdd);
        }

        public void StoreFaceAdd(Plane facePlane,
			int sourceMeshIndex,
			int sourceFaceIndex,
			int destFaceIndex)
		{
			// look through all the planes that are close to this one
			// Use consistent tolerance for finding similar planes
			// TODO: Consider passing tolerance from CsgBySlicing if needed
			var plane = similarPlaneFinder.FindPlane(facePlane, CsgBySlicing.CoplanarPlaneDistanceTolerance); // Use the constant from CsgBySlicing
			if (plane != null)
			{
				facePlane = plane.Value;
			}

			if (!coPlanarFaces.ContainsKey(facePlane))
			{
				coPlanarFaces[facePlane] = new Dictionary<int, List<(int sourceFace, int destFace)>>();
			}

			if (!coPlanarFaces[facePlane].ContainsKey(sourceMeshIndex))
			{
				coPlanarFaces[facePlane][sourceMeshIndex] = new List<(int sourceFace, int destFace)>();
			}

			coPlanarFaces[facePlane][sourceMeshIndex].Add((sourceFaceIndex, destFaceIndex));
		}
	}

	public static class PolygonsExtra
	{
		public static string GraphData(this List<Polygons> polygonsSets)
		{
			string output = "";
			foreach (var polygons in polygonsSets)
			{
				output += polygons.GraphData();
			}

			return output;
		}

		public static string GraphData(this Polygons polygons)
		{
			string output = "";
			foreach (var polygon in polygons)
			{
				foreach (var point in polygon)
				{
					output += $"{point.X},{point.Y}\n";
				}

				output += "\n";
			}

			return output;
		}
	}
}