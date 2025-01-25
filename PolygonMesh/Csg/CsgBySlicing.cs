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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ClipperLib;
using MatterHackers.Agg;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.RayTracer;
using MatterHackers.VectorMath;

namespace MatterHackers.PolygonMesh.Csg
{
    using Polygons = List<List<IntPoint>>;

    public class CsgBySlicing
    {
        private int totalOperations;
        private List<List<bool>> isFaceIntersecting;
        private CsgModes operation;
        private List<Mesh> transformedMeshes;
        private List<ITraceable> bvhAccelerators;
        private List<List<Plane>> plansByMeshIndex;
        private SimilarPlaneFinder similarPlaneFinder;
        private Dictionary<Plane, Matrix4X4> planeTransformsToXy;
        private AxisAlignedBoundingBox activeOperationBounds;
        private Dictionary<(int, Plane), Polygons> cachedSlices = new Dictionary<(int, Plane), Polygons>();

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
            CsgModes operation,
            CancellationToken cancellationToken)
        {
            var totalTimeTimer = new QuickTimer("CsgBySlicing_Setup");
            this.operation = operation;
            transformedMeshes = new List<Mesh>();
            bvhAccelerators = new List<ITraceable>();

            // First pass: Transform meshes and build BVH accelerators
            foreach (var (mesh, matrix) in meshAndMatrix)
            {
                if (mesh == null) continue;

                cancellationToken.ThrowIfCancellationRequested();
                var meshCopy = mesh.Copy(cancellationToken);
                meshCopy.Transform(matrix);
                transformedMeshes.Add(meshCopy);
                bvhAccelerators.Add(GetBVH(meshCopy));
            }

            if (transformedMeshes.Count == 0)
            {
                throw new ArgumentException("No valid meshes provided");
            }

            // Calculate operation bounds based on CSG mode
            CalculateOperationBounds();

            // Initialize face intersection tracking
            InitializeFaceIntersectionTracking();

            // Build plane information
            BuildPlaneInformation(cancellationToken);
            totalTimeTimer?.Dispose();
        }

        private void CalculateOperationBounds()
        {
            // Calculate all pairwise intersections for any operation type
            var allIntersections = new List<AxisAlignedBoundingBox>();

            // Calculate all pairwise intersections
            for (int i = 0; i < transformedMeshes.Count - 1; i++)
            {
                var bounds1 = transformedMeshes[i].GetAxisAlignedBoundingBox();

                for (int j = i + 1; j < transformedMeshes.Count; j++)
                {
                    var bounds2 = transformedMeshes[j].GetAxisAlignedBoundingBox();
                    var intersection = bounds1.GetIntersection(bounds2);

                    // Only add valid intersections (those with volume)
                    if (intersection.XSize > 0 && intersection.YSize > 0 && intersection.ZSize > 0)
                    {
                        allIntersections.Add(intersection);
                    }
                }
            }

            // If we found any valid intersections, expand to include all of them
            if (allIntersections.Count > 0)
            {
                activeOperationBounds = allIntersections[0];
                for (int i = 1; i < allIntersections.Count; i++)
                {
                    activeOperationBounds.ExpandToInclude(allIntersections[i]);
                }
            }
            else
            {
                // No intersections found - use empty bounds
                activeOperationBounds = new AxisAlignedBoundingBox(Vector3.Zero, Vector3.Zero);
            }

            // Add a small buffer for numerical stability
            activeOperationBounds.Expand(.1);
        }

        private void InitializeFaceIntersectionTracking()
        {
            totalOperations = 0;
            isFaceIntersecting = new List<List<bool>>();

            // Initialize lists for each mesh
            for (int i = 0; i < transformedMeshes.Count; i++)
            {
                var intersectingFaces = new List<bool>();
                var currentMesh = transformedMeshes[i];

                for (int faceIndex = 0; faceIndex < currentMesh.Faces.Count; faceIndex++)
                {
                    bool isIntersecting = InIntersection(currentMesh, faceIndex);
                    intersectingFaces.Add(isIntersecting);

                    if (isIntersecting)
                    {
                        totalOperations++;
                    }
                }

                isFaceIntersecting.Add(intersectingFaces);
            }
        }

        private void BuildPlaneInformation(CancellationToken cancellationToken)
        {
            plansByMeshIndex = new List<List<Plane>>();
            var uniquePlanes = new HashSet<Plane>();

            foreach (var mesh in transformedMeshes)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var planesForMesh = new List<Plane>();
                for (int faceIndex = 0; faceIndex < mesh.Faces.Count; faceIndex++)
                {
                    var cutPlane = mesh.GetPlane(faceIndex);
                    planesForMesh.Add(cutPlane);
                    uniquePlanes.Add(cutPlane);
                }
                plansByMeshIndex.Add(planesForMesh);
            }

            // Initialize plane sorter and transforms
            similarPlaneFinder = new SimilarPlaneFinder(uniquePlanes);
            planeTransformsToXy = new Dictionary<Plane, Matrix4X4>();

            foreach (var plane in uniquePlanes)
            {
                var matrix = SliceLayer.GetTransformToXy(plane, 10000);
                planeTransformsToXy[plane] = matrix;
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

        public Mesh Calculate(Action<double, string> progressReporter,
            CancellationToken cancellationToken)
        {
            var totalTimeTimer = new ReportTimer("CsgBySlicing_Calculate", 1);
            double amountPerOperation = 1.0 / totalOperations;
            double ratioCompleted = 0;

            var resultsMesh = new Mesh();
            var coPlanarFaces = new CoPlanarFaces(similarPlaneFinder);

            for (var currentMeshIndex = 0; currentMeshIndex < transformedMeshes.Count; currentMeshIndex++)
            {
                var currentMesh = transformedMeshes[currentMeshIndex];

                if (cancellationToken.IsCancellationRequested)
                {
                    return null;
                }

                for (int faceIndex = 0; faceIndex < currentMesh.Faces.Count; faceIndex++)
                {
                    var cutPlane = plansByMeshIndex[currentMeshIndex][faceIndex];

                    if (double.IsNaN(cutPlane.DistanceFromOrigin))
                    {
                        continue;
                    }

                    if (!isFaceIntersecting[currentMeshIndex][faceIndex])
                    {
                        if (operation == CsgModes.Union
                            || (operation == CsgModes.Subtract && currentMeshIndex == 0))
                        {
                            resultsMesh.AddFaceCopy(currentMesh, faceIndex);
                            coPlanarFaces.StoreFaceAdd(cutPlane, currentMeshIndex, faceIndex, resultsMesh.Faces.Count - 1);
                        }
                        continue;
                    }

                    var face = currentMesh.Faces[faceIndex];
                    var planeTransformToXy = planeTransformsToXy[cutPlane];

                    Polygons totalSlice = GetTotalSlice(currentMeshIndex, cutPlane, planeTransformToXy);
                    var facePolygon = CoPlanarFaces.GetFacePolygon(currentMesh, faceIndex, planeTransformToXy);

                    var polygonShape = new Polygons();
                    var clipper = new Clipper();
                    clipper.AddPath(facePolygon, PolyType.ptSubject, true);
                    clipper.AddPaths(totalSlice, PolyType.ptClip, true);
                    var expectedFaceNormal = face.normal;

                    switch (operation)
                    {
                        case CsgModes.Union:
                            using (new ReportTimer("CsgBySlicing_Calculate_Union", 1))
                            {
                            clipper.Execute(ClipType.ctDifference, polygonShape);
                            }
                            break;

                        case CsgModes.Subtract:
                            if (currentMeshIndex == 0)
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

                    var faceCountPreAdd = resultsMesh.Faces.Count;

                    if (polygonShape.Count == 1
                        && polygonShape[0].Count == 3
                        && facePolygon.Contains(polygonShape[0][0])
                        && facePolygon.Contains(polygonShape[0][1])
                        && facePolygon.Contains(polygonShape[0][2]))
                    {
                        resultsMesh.AddFaceCopy(currentMesh, faceIndex);
                    }
                    else
                    {
                        var vertCountPreAdd = resultsMesh.Vertices.Count;
                        polygonShape.AsVertices(1).TriangulateFaces(null, resultsMesh, 0, planeTransformsToXy[cutPlane].Inverted);
                        var postAddCount = resultsMesh.Vertices.Count;

                        var polygonPlane = currentMesh.GetPlane(faceIndex);

                        for (int addedVertIndex = vertCountPreAdd; addedVertIndex < postAddCount; addedVertIndex++)
                        {
                            for (int meshIndex = 0; meshIndex < transformedMeshes.Count; meshIndex++)
                            {
                                var foundMatch = false;
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
                                                foundMatch = true;
                                                break;
                                            }
                                            else if (deltaSquared < .00001)
                                            {
                                                resultsMesh.Vertices[addedVertIndex] = sourcePosition;
                                                foundMatch = true;
                                                break;
                                            }
                                            else
                                            {
                                                var distanceToPlane = polygonPlane.GetDistanceFromPlane(resultsMesh.Vertices[addedVertIndex]);
                                                resultsMesh.Vertices[addedVertIndex] -= new Vector3Float(polygonPlane.Normal * distanceToPlane);
                                            }
                                        }
                                    }

                                    if (foundMatch)
                                    {
                                        break;
                                    }
                                }
                            }
                        }
                    }

                    if (faceCountPreAdd < resultsMesh.Faces.Count)
                    {
                        for (int i = faceCountPreAdd; i < resultsMesh.Faces.Count; i++)
                        {
                            coPlanarFaces.StoreFaceAdd(cutPlane, currentMeshIndex, faceIndex, i);
                            if (resultsMesh.Faces[i].normal.Dot(expectedFaceNormal) < 0)
                            {
                                resultsMesh.FlipFace(i);
                            }
                        }
                    }
                    else
                    {
                        coPlanarFaces.StoreFaceAdd(cutPlane, currentMeshIndex, faceIndex, -1);
                    }

                    ratioCompleted += amountPerOperation;
                    progressReporter?.Invoke(ratioCompleted * .8, "");

                    if (cancellationToken.IsCancellationRequested)
                    {
                        return null;
                    }
                }
            }

            using (new ReportTimer("CsgBySlicing_Calculate_ProcessCoplanarFaces", 1))
            {
                ProcessCoplanarFaces(operation, resultsMesh, coPlanarFaces, (progress, description) => progressReporter?.Invoke(.8 + progress * .2, description));
            }

            totalTimeTimer?.Dispose();
            ReportTimer.ReportAndRestart(1);
            return resultsMesh;
        }

        public Polygons GetTotalSlice(int currentMeshIndex, Plane cutPlane, Matrix4X4 planeTransformToXy, bool includeBehindThePlane = true)
        {
            //return CalculateTotalSlice(currentMeshIndex, cutPlane, planeTransformToXy, includeBehindThePlane);
            Polygons totalSlice;

            var nullableSimilarPlane = similarPlaneFinder.FindPlane(cutPlane, .0001);
            if (nullableSimilarPlane == null)
            {
                totalSlice = CalculateTotalSlice(currentMeshIndex, cutPlane, planeTransformToXy, includeBehindThePlane);
                similarPlaneFinder.AddPlaneToComparer(cutPlane);
                planeTransformsToXy[cutPlane] = planeTransformToXy;
                return totalSlice;
            }

            var similarPlane = nullableSimilarPlane.Value;
            var similarPlaneTransformToXY = planeTransformsToXy[similarPlane];

            bool useSimilar = true;
            if (!similarPlane.Equals(cutPlane)
                || !similarPlaneTransformToXY.AreTransformationsEquivalent(planeTransformToXy))
            {
                useSimilar = false;
            }

            if (useSimilar)
            {
                if (cachedSlices.ContainsKey((currentMeshIndex, similarPlane)))
                {
                    totalSlice = cachedSlices[(currentMeshIndex, similarPlane)];
                }
                else
                {
                    using (new ReportTimer("CsgBySlicing_Calculate_GetTotalSlice", 1))
                    {
                        totalSlice = CalculateTotalSlice(currentMeshIndex, similarPlane, similarPlaneTransformToXY);
                        cachedSlices[(currentMeshIndex, similarPlane)] = totalSlice;
                    }
                }
            }
            else
            {
                if (cachedSlices.ContainsKey((currentMeshIndex, cutPlane)))
                {
                    totalSlice = cachedSlices[(currentMeshIndex, cutPlane)];
                }
                else
                {
                    using (new ReportTimer("CsgBySlicing_Calculate_GetTotalSlice", 1))
                    {
                        totalSlice = CalculateTotalSlice(currentMeshIndex, cutPlane, planeTransformToXy);
                        cachedSlices[(currentMeshIndex, cutPlane)] = totalSlice;
                    }
                }
            }

            return totalSlice;
        }


        private Polygons CalculateTotalSlice(int meshIndexToIgnore, Plane cutPlane, Matrix4X4 planeTransformToXy, bool includeBehindThePlane = true)
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
                var slice = SliceLayer.CreateSlice(mesh2, cutPlane, planeTransformToXy, bvhAccelerators[sliceMeshIndex], includeBehindThePlane);
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

        private void ProcessCoplanarFaces(CsgModes operation, Mesh resultsMesh, CoPlanarFaces coPlanarFaces, Action<double, string> progressReporter)
        {
            var faceIndicesToRemove = new HashSet<int>();
            var numProcessed = 0;
            var totalOpperations = coPlanarFaces.Planes.Count();
            foreach (var plane in coPlanarFaces.Planes)
            {
                var meshIndices = coPlanarFaces.MeshFaceIndicesForPlane(plane).ToList();

                if (operation == CsgModes.Union)
                {
                    var negativePlane = similarPlaneFinder.FindPlane(new Plane()
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
                    var planeTransformToXy = planeTransformsToXy[plane];

                    // depending on the operation add or remove polygons that are planar
                    switch (operation)
                    {
                        case CsgModes.Union:
                            coPlanarFaces.UnionFaces(plane, transformedMeshes, resultsMesh, planeTransformToXy, faceIndicesToRemove, this);
                            break;

                        case CsgModes.Subtract:
                            coPlanarFaces.SubtractFaces(plane, transformedMeshes, resultsMesh, planeTransformToXy, faceIndicesToRemove);
                            break;

                        case CsgModes.Intersect:
                            coPlanarFaces.IntersectFaces(plane, transformedMeshes, resultsMesh, planeTransformToXy, faceIndicesToRemove);
                            break;
                    }
                }

                // report progress
                progressReporter?.Invoke(numProcessed++ / (double)totalOpperations, "Union Faces");
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