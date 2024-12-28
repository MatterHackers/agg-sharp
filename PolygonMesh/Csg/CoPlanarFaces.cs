/*
Copyright (c) 2019, Lars Brubaker, John Lewin
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
using System.Threading;
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

		private SimilarPlaneFinder planeSorter;

		public CoPlanarFaces(SimilarPlaneFinder planeSorter)
		{
			this.planeSorter = planeSorter;
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

		public void SubtractFaces(Plane plane, List<Mesh> transformedMeshes, Mesh resultsMesh, Matrix4X4 transformTo0Plane, HashSet<int> faceIndicesToRemove)
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
                var facePolygon = GetFacePolygon(transformedMeshes[0], keepFaceSets.sourceFaceIndex, transformTo0Plane);
                keepPolygons = keepPolygons.Union(facePolygon);
            }

            // iterate all the meshes that need to be subtracted
            var removePoygons = new Polygons();
            for (int removeMeshIndex = 1; removeMeshIndex < meshesFaceIndicesForPlane.Count; removeMeshIndex++)
            {
                foreach (var removeFaceSets in FacesSetsForPlaneAndMesh(plane, removeMeshIndex))
                {
                    removePoygons = removePoygons.Union(GetFacePolygon(transformedMeshes[removeMeshIndex], removeFaceSets.sourceFaceIndex, transformTo0Plane));
                }
            }

            var polygonShape = new Polygons();
            var clipper = new Clipper();
            clipper.AddPaths(keepPolygons, PolyType.ptSubject, true);
            clipper.AddPaths(removePoygons, PolyType.ptClip, true);
            clipper.Execute(ClipType.ctDifference, polygonShape);

            // teselate and add all the new polygons
            var countPreAdd = resultsMesh.Faces.Count;
            polygonShape.AsVertices(1).TriangulateFaces(null, resultsMesh, 0, transformTo0Plane.Inverted);
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

        public void IntersectFaces(Plane plane, List<Mesh> transformedMeshes, Mesh resultsMesh, Matrix4X4 transformTo0Plane, HashSet<int> faceIndicesToRemove)
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
					unionedPoygons = unionedPoygons.Union(GetFacePolygon(transformedMeshes[meshIndex], removeFaceSets.sourceFaceIndex, transformTo0Plane));
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
			total.AsVertices(1).TriangulateFaces(null, resultsMesh, 0, transformTo0Plane.Inverted);
			EnsureFaceNormals(plane, resultsMesh, countPreAdd);
		}

        public void UnionFaces(Plane positivePlane,
            List<Mesh> transformedMeshes,
            Mesh resultsMesh,
            Matrix4X4 transformTo0Plane,
            HashSet<int> faceIndicesToRemove,
            CsgBySlicing csgData,
            CsgDebugger debugger)
        {
            var debugState = debugger?.CsgDebugState;

            // Debug Point 1: Starting union faces
            if (debugger != null)
            {
                debugState.CoplanarProcessingPhase = "Starting UnionFaces";
                debugState.CurrentPlane = positivePlane;
                debugState.CurrentResultMesh = resultsMesh.Copy(CancellationToken.None);
                debugger.OnFaceProcessed?.Invoke();
                if (debugger.WaitForStep) { debugger.StepEvent.Reset(); debugger.StepEvent.WaitOne(); }
            }

            // get all meshes that have faces on this plane
            var meshesWithFaces = MeshFaceIndicesForPlane(positivePlane).ToList();

            var negativePlane = planeSorter.FindPlane(new Plane()
            {
                Normal = -positivePlane.Normal,
                DistanceFromOrigin = -positivePlane.DistanceFromOrigin,
            }, .02);

            if (negativePlane != null)
            {
                // Debug Point 2: Found negative plane
                if (debugger != null)
                {
                    debugState.CoplanarProcessingPhase = "Adding negative plane faces";
                    debugState.CurrentResultMesh = resultsMesh.Copy(CancellationToken.None);
                    debugger.OnFaceProcessed?.Invoke();
                    if (debugger.WaitForStep) { debugger.StepEvent.Reset(); debugger.StepEvent.WaitOne(); }
                }

                // add any negative faces
                meshesWithFaces.AddRange(MeshFaceIndicesForPlane(negativePlane.Value));
            }

            if (meshesWithFaces.Count < 2)
            {
                // Debug Point 3: Not enough faces to process
                if (debugger != null)
                {
                    debugState.CoplanarProcessingPhase = $"Insufficient faces to process: {meshesWithFaces.Count}";
                    debugState.SkipReason = "Less than 2 faces to process";
                    debugState.CurrentResultMesh = resultsMesh.Copy(CancellationToken.None);
                    debugger.OnFaceProcessed?.Invoke();
                    if (debugger.WaitForStep) { debugger.StepEvent.Reset(); debugger.StepEvent.WaitOne(); }
                }
                return;
            }

            // add the faces that we should union
            foreach (var meshIndex in meshesWithFaces)
            {
                foreach (var faces in FacesSetsForPlaneAndMesh(positivePlane, meshIndex))
                {
                    faceIndicesToRemove.Add(faces.destFaceIndex);

                    // Debug Point 4: Adding face to remove list
                    if (debugger != null)
                    {
                        debugState.CoplanarProcessingPhase = $"Adding face {faces.destFaceIndex} to remove list";
                        debugState.FacesToRemove = new HashSet<int>(faceIndicesToRemove);
                        debugger.OnFaceProcessed?.Invoke();
                        if (debugger.WaitForStep) { debugger.StepEvent.Reset(); debugger.StepEvent.WaitOne(); }
                    }
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
                        var facePolygon = GetFacePolygon(transformedMeshes[meshIndex], sourceFaceIndex, transformTo0Plane);
                        meshPolygons.Add(facePolygon);
                        addedFaces.Add(sourceFaceIndex);

                        // Debug Point 5: Added face polygon
                        if (debugger != null)
                        {
                            debugState.CoplanarProcessingPhase = $"Added polygon for face {sourceFaceIndex} from mesh {meshIndex}";
                            debugState.OriginalFacePolygons[sourceFaceIndex] = facePolygon;
                            debugState.CurrentProcessingFaceIndex = sourceFaceIndex;
                            debugger.OnFaceProcessed?.Invoke();
                            if (debugger.WaitForStep) { debugger.StepEvent.Reset(); debugger.StepEvent.WaitOne(); }
                        }
                    }
                }

                if (first)
                {
                    firstPositivePolygons = meshPolygons;
                    if (debugger != null)
                    {
                        debugState.KeepPolygons = firstPositivePolygons;
                    }
                    first = false;
                }
                else
                {
                    unionPolygons.AddRange(meshPolygons);
                    if (debugger != null)
                    {
                        debugState.RemovePolygons = unionPolygons;
                    }
                }

                // Debug Point 6: Updated polygon collections
                if (debugger != null)
                {
                    debugState.CoplanarProcessingPhase = "Updated keep/union polygons";
                    debugState.KeepPolygons = firstPositivePolygons;
                    debugState.RemovePolygons = unionPolygons;
                    debugger.OnFaceProcessed?.Invoke();
                    if (debugger.WaitForStep) { debugger.StepEvent.Reset(); debugger.StepEvent.WaitOne(); }
                }
            }

            // now union all the intersections
            // clip against the slice based on the parameters
            var unionClipper = new Clipper();
            unionClipper.AddPaths(firstPositivePolygons, PolyType.ptSubject, true);
            unionClipper.AddPaths(unionPolygons, PolyType.ptClip, true);

            var totalSlices = new Polygons();
            unionClipper.Execute(ClipType.ctUnion, totalSlices, PolyFillType.pftNonZero);

            // Debug Point 7: After union operation
            if (debugger != null)
            {
                debugState.CoplanarProcessingPhase = $"Completed union operation: {totalSlices.Count} resulting polygons";
                debugState.ClippingResult = totalSlices;
                debugState.CurrentClipOperation = ClipType.ctUnion;
                debugger.OnFaceProcessed?.Invoke();
                if (debugger.WaitForStep) { debugger.StepEvent.Reset(); debugger.StepEvent.WaitOne(); }
            }

            var subtractPolygons = csgData.GetTotalSlice(-1, new Plane()
            {
                DistanceFromOrigin = positivePlane.DistanceFromOrigin + .001,
                Normal = positivePlane.Normal
            },
            transformTo0Plane);

            if (subtractPolygons.Count > 0)
            {
                // Debug Point 8: Before subtraction
                if (debugger != null)
                {
                    debugState.CoplanarProcessingPhase = "Starting subtraction operation";
                    debugState.RemovePolygons = subtractPolygons;
                    debugger.OnFaceProcessed?.Invoke();
                    if (debugger.WaitForStep) { debugger.StepEvent.Reset(); debugger.StepEvent.WaitOne(); }
                }

                // subtract them from the other union faces
                var subtractionClipper = new Clipper();
                subtractionClipper.AddPaths(totalSlices, PolyType.ptSubject, true);
                subtractionClipper.AddPaths(subtractPolygons, PolyType.ptClip, true);

                var totalSlices2 = new Polygons();
                subtractionClipper.Execute(ClipType.ctDifference, totalSlices2, PolyFillType.pftNonZero);
                totalSlices = totalSlices2;

                // Debug Point 9: After subtraction
                if (debugger != null)
                {
                    debugState.CoplanarProcessingPhase = $"Completed subtraction: {totalSlices.Count} final polygons";
                    debugState.ClippingResult = totalSlices;
                    debugState.CurrentClipOperation = ClipType.ctDifference;
                    debugger.OnFaceProcessed?.Invoke();
                    if (debugger.WaitForStep) { debugger.StepEvent.Reset(); debugger.StepEvent.WaitOne(); }
                }
            }

            // teselate and add all the new polygons
            var countPreAdd = resultsMesh.Faces.Count;
            totalSlices.AsVertices(1).TriangulateFaces(null, resultsMesh, 0, transformTo0Plane.Inverted);
            EnsureFaceNormals(positivePlane, resultsMesh, countPreAdd);

            // Debug Point 10: Final result
            if (debugger != null)
            {
                debugState.CoplanarProcessingPhase = "Completed face processing";
                debugState.CurrentResultMesh = resultsMesh.Copy(CancellationToken.None);
                debugger.OnFaceProcessed?.Invoke();
                if (debugger.WaitForStep) { debugger.StepEvent.Reset(); debugger.StepEvent.WaitOne(); }
            }
        }

        public void StoreFaceAdd(Plane facePlane,
			int sourceMeshIndex,
			int sourceFaceIndex,
			int destFaceIndex)
		{
			// look through all the planes that are close to this one
			var plane = planeSorter.FindPlane(facePlane, .02);
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