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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ClipperLib;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.RayTracer;
using MatterHackers.VectorMath;

namespace MatterHackers.PolygonMesh.Csg
{
    using Polygons = List<List<IntPoint>>;

    public class CsgBySlicing
    {
        private int totalOperations;
        private List<Mesh> transformedMeshes;
        private List<ITraceable> bvhAccelerators;
        private List<List<Plane>> plansByMesh;
        private SimilarPlaneFinder planeSorter;
        private Dictionary<Plane, (Matrix4X4 matrix, Matrix4X4 inverted)> transformTo0Planes;
        private AxisAlignedBoundingBox activeOperationBounds;

        public CsgBySlicing()
        {
        }

        public ITraceable GetBVH(Mesh mesh)
        {
            var tracePrimitives = new List<ITraceable>();

            for (int faceIndex = 0; faceIndex < mesh.Faces.Count; faceIndex++)
            {
                var face = mesh.Faces[faceIndex];

                ITraceable triangle;
                triangle = new MinimalTriangle((fi, vi) =>
                {
                    switch (vi)
                    {
                        case 0:
                            return mesh.Vertices[mesh.Faces[fi].v0];
                        case 1:
                            return mesh.Vertices[mesh.Faces[fi].v1];
                        default:
                            return mesh.Vertices[mesh.Faces[fi].v2];
                    }
                }, faceIndex);

                tracePrimitives.Add(triangle);
            }

            // return an empty collection
            return BoundingVolumeHierarchy.CreateNewHierarchy(tracePrimitives);
        }

        public void Setup(IEnumerable<(Mesh mesh, Matrix4X4 matrix)> meshAndMatrix,
			Action<double, string> progressReporter,
			CancellationToken cancellationToken)
		{
			transformedMeshes = new List<Mesh>();
			bvhAccelerators = new List<ITraceable>();
			foreach (var (mesh, matrix) in meshAndMatrix)
			{
                if (mesh != null)
                {
                    var meshCopy = mesh.Copy(cancellationToken);
                    transformedMeshes.Add(meshCopy);
                    meshCopy.Transform(matrix);
                    bvhAccelerators.Add(GetBVH(meshCopy));
                }
			}

            activeOperationBounds = transformedMeshes[0].GetAxisAlignedBoundingBox().GetIntersection(transformedMeshes[1].GetAxisAlignedBoundingBox());
            for (var meshIndex = 1; meshIndex < transformedMeshes.Count; meshIndex++)
            {
                for (var meshIndex2 = meshIndex + 1; meshIndex2 <= transformedMeshes.Count; meshIndex2++)
                {
                    var nextMeshIndex = meshIndex2 % transformedMeshes.Count;
                    var nextIntersectionBounds = transformedMeshes[meshIndex].GetAxisAlignedBoundingBox()
                        .GetIntersection(transformedMeshes[nextMeshIndex].GetAxisAlignedBoundingBox());
                    activeOperationBounds.ExpandToInclude(nextIntersectionBounds);
                }
            }

            activeOperationBounds.Expand(.1);

            // figure out how many faces we will process
            totalOperations = 0;
            foreach (var mesh in transformedMeshes)
            {
                if (mesh != null)
                {
                    for (int faceIndex = 0; faceIndex < mesh.Faces.Count; faceIndex++)
                    {
                        if (InIntersection(mesh, faceIndex))
                        {
                            totalOperations++;
                        }
                    }
                }
            }
            
            plansByMesh = new List<List<Plane>>();
			var uniquePlanes = new HashSet<Plane>();
			for (int i = 0; i < transformedMeshes.Count; i++)
			{
				var mesh = transformedMeshes[i];
				plansByMesh.Add(new List<Plane>());
				for (int j = 0; j < transformedMeshes[i].Faces.Count; j++)
				{
                    var cutPlane = mesh.GetPlane(j);
					plansByMesh[i].Add(cutPlane);
					uniquePlanes.Add(cutPlane);
				}

				if (cancellationToken.IsCancellationRequested)
                {
					return;
                }
			}

			planeSorter = new SimilarPlaneFinder(uniquePlanes);
			transformTo0Planes = new Dictionary<Plane, (Matrix4X4 matrix, Matrix4X4 inverted)>();
			foreach (var plane in uniquePlanes)
			{
				var matrix = SliceLayer.GetTransformTo0Plane(plane, 10000);
				transformTo0Planes[plane] = (matrix, matrix.Inverted);
			}
        }

        private bool InIntersection(Mesh mesh, int faceIndex)
        {
            var faceAabb = mesh.Faces[faceIndex].GetAxisAlignedBoundingBox(mesh);
            faceAabb.Expand(.1);
            if (activeOperationBounds.Intersects(faceAabb))
            {
                return true;
            }

            return false;
        }

        public Mesh Calculate(CsgModes operation,
            Action<double, string> progressReporter,
            CsgDebugger csgDebugger,
            CancellationToken cancellationToken)
        {
            double amountPerOperation = 1.0 / totalOperations;
            double ratioCompleted = 0;

            var resultsMesh = new Mesh();
            var coPlanarFaces = new CoPlanarFaces(planeSorter);

            CsgDebugState debugState = null;
            if (csgDebugger != null)
            {
                debugState = csgDebugger?.CsgDebugState;
            }
            else
            {
                debugState = new CsgDebugState();
            }

            debugState.Operation = operation;

            for (var mesh1Index = 0; mesh1Index < transformedMeshes.Count; mesh1Index++)
            {
                debugState.CurrentMeshIndex = mesh1Index;
                var mesh1 = transformedMeshes[mesh1Index];

                if (cancellationToken.IsCancellationRequested)
                {
                    return null;
                }

                var slicePolygons = new Dictionary<Plane, Polygons>();

                for (int faceIndex = 0; faceIndex < mesh1.Faces.Count; faceIndex++)
                {
                    debugState.CurrentFaceIndex = faceIndex;
                    debugState.WasProcessed = false;
                    debugState.SkipReason = null;
                    debugState.ProcessingAction = null;
                    var cutPlane = plansByMesh[mesh1Index][faceIndex];

                    // Debug Point 1: Before face processing decision
                    if (csgDebugger != null)
                    {
                        debugState.CurrentResultMesh = resultsMesh.Copy(cancellationToken);
                        debugState.CurrentPlane = cutPlane;
                        debugState.CurrentSlicePolygons = null;
                        debugState.ProcessingAction = "Starting face processing";
                        csgDebugger.OnFaceProcessed?.Invoke();
                        if (csgDebugger.WaitForStep) { csgDebugger.StepEvent.Reset(); csgDebugger.StepEvent.WaitOne(); }
                    }

                    if (double.IsNaN(cutPlane.DistanceFromOrigin))
                    {
                        if (csgDebugger != null)
                        {
                            debugState.SkipReason = "Invalid plane - NaN distance";
                        }
                        continue;
                    }

                    if (!InIntersection(mesh1, faceIndex))
                    {
                        if (operation == CsgModes.Union
                            || (operation == CsgModes.Subtract && mesh1Index == 0))
                        {
                            if (csgDebugger != null)
                            {
                                debugState.ProcessingAction = "Adding non-intersecting face";
                                debugState.WasProcessed = true;
                            }

                            resultsMesh.AddFaceCopy(mesh1, faceIndex);
                            coPlanarFaces.StoreFaceAdd(cutPlane, mesh1Index, faceIndex, resultsMesh.Faces.Count - 1);

                            // Debug Point 2: After non-intersection face added
                            if (csgDebugger != null)
                            {
                                debugState.CurrentResultMesh = resultsMesh.Copy(cancellationToken);
                                lock (debugState)
                                {
                                    debugState.ProcessedFaces.Add((mesh1Index, faceIndex));
                                }
                                csgDebugger.OnFaceProcessed?.Invoke();
                                if (csgDebugger.WaitForStep) { csgDebugger.StepEvent.Reset(); csgDebugger.StepEvent.WaitOne(); }
                            }
                        }
                        else if (csgDebugger != null)
                        {
                            debugState.SkipReason = "Face not in intersection area and operation doesn't require it";
                        }
                        continue;
                    }

                    var face = mesh1.Faces[faceIndex];
                    var transformTo0Plane = transformTo0Planes[cutPlane].matrix;

                    Polygons totalSlice;
                    if (slicePolygons.ContainsKey(cutPlane))
                    {
                        totalSlice = slicePolygons[cutPlane];
                        if (csgDebugger != null)
                        {
                            debugState.ProcessingAction = "Using cached slice polygons";
                        }
                    }
                    else
                    {
                        if (csgDebugger != null)
                        {
                            debugState.ProcessingAction = "Calculating new slice polygons";
                        }
                        totalSlice = GetTotalSlice(mesh1Index, cutPlane, transformTo0Plane);
                        slicePolygons[cutPlane] = totalSlice;
                    }

                    // Debug Point 3: After slice calculation
                    if (csgDebugger != null)
                    {
                        debugState.CurrentResultMesh = resultsMesh.Copy(cancellationToken);
                        debugState.CurrentSlicePolygons = totalSlice;
                        csgDebugger.OnFaceProcessed?.Invoke();
                        if (csgDebugger.WaitForStep) { csgDebugger.StepEvent.Reset(); csgDebugger.StepEvent.WaitOne(); }
                    }

                    var facePolygon = CoPlanarFaces.GetFacePolygon(mesh1, faceIndex, transformTo0Plane);
                    debugState.CurrentFacePolygon = facePolygon;

                    var polygonShape = new Polygons();
                    var clipper = new Clipper();
                    clipper.AddPath(facePolygon, PolyType.ptSubject, true);
                    clipper.AddPaths(totalSlice, PolyType.ptClip, true);
                    var expectedFaceNormal = face.normal;

                    switch (operation)
                    {
                        case CsgModes.Union:
                            clipper.Execute(ClipType.ctDifference, polygonShape);
                            break;

                        case CsgModes.Subtract:
                            if (mesh1Index == 0)
                            {
                                clipper.Execute(ClipType.ctDifference, polygonShape);
                            }
                            else
                            {
                                expectedFaceNormal *= -1;
                                clipper.Execute(ClipType.ctIntersection, polygonShape);
                            }
                            break;

                        case CsgModes.Intersect:
                            clipper.Execute(ClipType.ctIntersection, polygonShape);
                            break;
                    }

                    // Debug Point 4: After clipping operation
                    if (csgDebugger != null)
                    {
                        debugState.CurrentResultMesh = resultsMesh.Copy(cancellationToken);
                        debugState.ClippingResult = polygonShape;
                        debugState.ProcessingAction = "Completed clipping operation";
                        csgDebugger.OnFaceProcessed?.Invoke();
                        if (csgDebugger.WaitForStep) { csgDebugger.StepEvent.Reset(); csgDebugger.StepEvent.WaitOne(); }
                    }

                    var faceCountPreAdd = resultsMesh.Faces.Count;

                    if (polygonShape.Count == 1
                        && polygonShape[0].Count == 3
                        && facePolygon.Contains(polygonShape[0][0])
                        && facePolygon.Contains(polygonShape[0][1])
                        && facePolygon.Contains(polygonShape[0][2]))
                    {
                        if (csgDebugger != null)
                        {
                            debugState.ProcessingAction = "Adding original face - triangle unchanged";
                            debugState.WasProcessed = true;
                        }
                        resultsMesh.AddFaceCopy(mesh1, faceIndex);
                    }
                    else
                    {
                        if (csgDebugger != null)
                        {
                            debugState.ProcessingAction = "Adding clipped face geometry";
                            debugState.WasProcessed = true;
                        }

                        var vertCountPreAdd = resultsMesh.Vertices.Count;
                        polygonShape.AsVertices(1).TriangulateFaces(null, resultsMesh, 0, transformTo0Planes[cutPlane].inverted);
                        var postAddCount = resultsMesh.Vertices.Count;

                        var polygonPlane = mesh1.GetPlane(faceIndex);

                        for (int addedVertIndex = vertCountPreAdd; addedVertIndex < postAddCount; addedVertIndex++)
                        {
                            for (int meshIndex = 0; meshIndex < transformedMeshes.Count; meshIndex++)
                            {
                                var bvhAccelerator = bvhAccelerators[meshIndex];
                                var mesh = transformedMeshes[meshIndex];
                                var touchingBvhItems = bvhAccelerator.GetTouching(new Vector3(resultsMesh.Vertices[addedVertIndex]), .0001);
                                foreach (var touchingBvhItem in touchingBvhItems)
                                {
                                    if (touchingBvhItem is MinimalTriangle triangleShape)
                                    {
                                        var sourceFaceIndex = triangleShape.FaceIndex;
                                        var sourceFace = mesh.Faces[sourceFaceIndex];
                                        var sourceVertexIndices = new int[] { sourceFace.v0, sourceFace.v1, sourceFace.v2 };
                                        foreach (var sourceVertexIndex in sourceVertexIndices)
                                        {
                                            var sourcePosition = mesh.Vertices[sourceVertexIndex];
                                            var deltaSquared = (resultsMesh.Vertices[addedVertIndex] - sourcePosition).LengthSquared;
                                            if (deltaSquared == 0)
                                            {
                                                // do nothing it already matches
                                            }
                                            else if (deltaSquared < .00001)
                                            {
                                                resultsMesh.Vertices[addedVertIndex] = sourcePosition;
                                            }
                                            else
                                            {
                                                var distanceToPlane = polygonPlane.GetDistanceFromPlane(resultsMesh.Vertices[addedVertIndex]);
                                                resultsMesh.Vertices[addedVertIndex] -= new Vector3Float(polygonPlane.Normal * distanceToPlane);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }

                    if (faceCountPreAdd < resultsMesh.Faces.Count)
                    {
                        for (int i = faceCountPreAdd; i < resultsMesh.Faces.Count; i++)
                        {
                            coPlanarFaces.StoreFaceAdd(cutPlane, mesh1Index, faceIndex, i);
                            if (resultsMesh.Faces[i].normal.Dot(expectedFaceNormal) < 0)
                            {
                                resultsMesh.FlipFace(i);
                            }
                        }
                    }
                    else
                    {
                        coPlanarFaces.StoreFaceAdd(cutPlane, mesh1Index, faceIndex, -1);
                    }

                    // Debug Point 5: After face addition
                    if (csgDebugger != null)
                    {
                        if (!debugState.WasProcessed)
                        {
                            debugState.SkipReason = "Face did not meet processing criteria";
                        }
                        debugState.CurrentResultMesh = resultsMesh.Copy(cancellationToken);
                        lock (debugState)
                        {
                            debugState.ProcessedFaces.Add((mesh1Index, faceIndex));
                        }
                        debugState.ProcessingAction = "Completed face processing";
                        csgDebugger.OnFaceProcessed?.Invoke();
                        if (csgDebugger.WaitForStep) { csgDebugger.StepEvent.Reset(); csgDebugger.StepEvent.WaitOne(); }
                    }

                    ratioCompleted += amountPerOperation;
                    progressReporter?.Invoke(ratioCompleted, "");

                    if (cancellationToken.IsCancellationRequested)
                    {
                        return null;
                    }
                }
            }

            // Debug Point 6: Before co-planar face processing
            if (csgDebugger != null)
            {
                debugState.CurrentResultMesh = resultsMesh.Copy(cancellationToken);
                debugState.ProcessingAction = "Starting co-planar face processing";
                csgDebugger.OnFaceProcessed?.Invoke();
                if (csgDebugger.WaitForStep) { csgDebugger.StepEvent.Reset(); csgDebugger.StepEvent.WaitOne(); }
            }

            ProcessCoplanarFaces(operation, resultsMesh, coPlanarFaces, csgDebugger);

            // Debug Point 7: Final result
            if (csgDebugger != null)
            {
                debugState.CurrentResultMesh = resultsMesh.Copy(cancellationToken);
                debugState.ProcessingAction = "Operation complete";
                csgDebugger.OnFaceProcessed?.Invoke();
                if (csgDebugger.WaitForStep) { csgDebugger.StepEvent.Reset(); csgDebugger.StepEvent.WaitOne(); }
                csgDebugger.Done = true;
            }

            //resultsMesh.MergeVertices(.01, .001);
            //resultsMesh.CleanAndMerge();
            return resultsMesh;
        }

        public Polygons GetTotalSlice(int meshIndexToIgnore, Plane cutPlane, Matrix4X4 transformTo0Plane, bool includeBehindThePlane = true)
        {
            var totalSlice = new Polygons();

            var firstSlice = true;
            for (var sliceMeshIndex = 0; sliceMeshIndex < transformedMeshes.Count; sliceMeshIndex++)
            {
                if (meshIndexToIgnore == sliceMeshIndex)
                {
                    continue;
                }

                var mesh2 = transformedMeshes[sliceMeshIndex];
                // calculate and add the PWN face from the loops
                var slice = SliceLayer.CreateSlice(mesh2, cutPlane, transformTo0Plane, bvhAccelerators[sliceMeshIndex], includeBehindThePlane);
                if (firstSlice)
                {
                    totalSlice = slice;
                    firstSlice = false;
                }
                else
                {
                    totalSlice = totalSlice.Union(slice);
                }
            }

            return totalSlice;
        }

        private void ProcessCoplanarFaces(CsgModes operation, Mesh resultsMesh, CoPlanarFaces coPlanarFaces, CsgDebugger csgDebugger)
        {
            var faceIndicesToRemove = new HashSet<int>();
            foreach (var plane in coPlanarFaces.Planes)
            {
                var meshIndices = coPlanarFaces.MeshFaceIndicesForPlane(plane).ToList();

                if (operation == CsgModes.Union)
                {
                    var negativePlane = planeSorter.FindPlane(new Plane()
                    {
                        Normal = -plane.Normal,
                        DistanceFromOrigin = -plane.DistanceFromOrigin,
                    }, .02);

                    if (negativePlane != null)
                    {
                        // add any negative faces
                        meshIndices.AddRange(coPlanarFaces.MeshFaceIndicesForPlane(negativePlane.Value));
                    }
                }

                if (meshIndices.Count() > 1)
                {
                    // check if more than one mesh has polygons on this plane
                    var transformTo0Plane = transformTo0Planes[plane].matrix;

                    // depending on the operation add or remove polygons that are planar
                    switch (operation)
                    {
                        case CsgModes.Union:
                            coPlanarFaces.UnionFaces(plane, transformedMeshes, resultsMesh, transformTo0Plane, faceIndicesToRemove, this, csgDebugger);
                            break;

                        case CsgModes.Subtract:
                            coPlanarFaces.SubtractFaces(plane, transformedMeshes, resultsMesh, transformTo0Plane, faceIndicesToRemove);
                            break;

                        case CsgModes.Intersect:
                            coPlanarFaces.IntersectFaces(plane, transformedMeshes, resultsMesh, transformTo0Plane, faceIndicesToRemove);
                            break;
                    }

                }
            }

            // now rebuild the face list without the remove polygons
            RemoveUnsudeFaces(resultsMesh, faceIndicesToRemove);
        }

        public static void RemoveUnsudeFaces(Mesh resultsMesh, HashSet<int> faceIndicesToRemove)
        {
            if (faceIndicesToRemove.Count > 0)
            {
                var newFaces = new FaceList();
                for (int i = 0; i < resultsMesh.Faces.Count; i++)
                {
                    // if the face is NOT in the remove faces
                    if (!faceIndicesToRemove.Contains(i))
                    {
                        var face = resultsMesh.Faces[i];
                        newFaces.Add(face);
                    }
                }

                resultsMesh.Faces = newFaces;
            }
        }
    }
}