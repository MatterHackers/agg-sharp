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
            return BoundingVolumeHierarchy.CreateNewHierachy(tracePrimitives);
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
            if (activeOperationBounds.Intersects(faceAabb))
            {
                return true;
            }

            return false;
        }

        public Mesh Calculate(CsgModes operation,
			Action<double, string> progressReporter,
			CancellationToken cancellationToken)
        {
            double amountPerOperation = 1.0 / totalOperations;
            double ratioCompleted = 0;

            var resultsMesh = new Mesh();

            // keep track of all the faces added by their plane
            var coPlanarFaces = new CoPlanarFaces(planeSorter);

            for (var mesh1Index = 0; mesh1Index < transformedMeshes.Count; mesh1Index++)
            {
                var mesh1 = transformedMeshes[mesh1Index];

                if (cancellationToken.IsCancellationRequested)
                {
                    return null;
                }

                var slicePolygons = new Dictionary<Plane, Polygons>();

                for (int faceIndex = 0; faceIndex < mesh1.Faces.Count; faceIndex++)
                {
                    var cutPlane = plansByMesh[mesh1Index][faceIndex];
                    if (double.IsNaN(cutPlane.DistanceFromOrigin))
                    {
                        continue;
                    }

                    if (!InIntersection(mesh1, faceIndex))
                    {
                        if (operation == CsgModes.Union
                            || (operation == CsgModes.Subtract && mesh1Index == 0))
                        {
                            resultsMesh.AddFaceCopy(mesh1, faceIndex);
                            coPlanarFaces.StoreFaceAdd(cutPlane, mesh1Index, faceIndex, resultsMesh.Faces.Count - 1);
                        }
                        continue;
                    }

                    var face = mesh1.Faces[faceIndex];

                    var transformTo0Plane = transformTo0Planes[cutPlane].matrix;

                    Polygons totalSlice;
                    
                    // check if we have already calculated this exact plane
                    if (slicePolygons.ContainsKey(cutPlane))
                    {
                        totalSlice = slicePolygons[cutPlane];
                    }
                    else
                    {
                        totalSlice = GetTotalSlice(mesh1Index, cutPlane, transformTo0Plane);
                        slicePolygons[cutPlane] = totalSlice;
                    }
                    

                    // now we have the total loops that this polygon can intersect from the other meshes
                    // make a polygon for this face
                    var facePolygon = CoPlanarFaces.GetFacePolygon(mesh1, faceIndex, transformTo0Plane);

                    var polygonShape = new Polygons();
                    // clip against the slice based on the parameters
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

                    var faceCountPreAdd = resultsMesh.Faces.Count;

                    if (polygonShape.Count == 1
                        && polygonShape[0].Count == 3
                        && facePolygon.Contains(polygonShape[0][0])
                        && facePolygon.Contains(polygonShape[0][1])
                        && facePolygon.Contains(polygonShape[0][2]))
                    {
                        resultsMesh.AddFaceCopy(mesh1, faceIndex);
                    }
                    else
                    {
                        var vertCountPreAdd = resultsMesh.Vertices.Count;
                        // mesh the new polygon and add it to the resultsMesh
                        polygonShape.AsVertices(1).TriangulateFaces(null, resultsMesh, 0, transformTo0Planes[cutPlane].inverted);
                        var postAddCount = resultsMesh.Vertices.Count;

                        var polygonPlane = mesh1.GetPlane(faceIndex);

                        // for every vertex that we just added
                        for (int addedVertIndex = vertCountPreAdd; addedVertIndex < postAddCount; addedVertIndex++)
                        {
                            // TODO: map all the added vertices that can be back to the original polygon positions
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
                                                var a = 0;
                                            }
                                            else if (deltaSquared < .00001)
                                            {
                                                // we found a vertex that this is equivalent to
                                                // make it exactly the same
                                                resultsMesh.Vertices[addedVertIndex] = sourcePosition;
                                            }
                                            else
                                            {
                                                // we did not find a matching vertex but we can still make sure
                                                // the new vertex is on the right plane
                                                var distanceToPlane = polygonPlane.GetDistanceFromPlane(resultsMesh.Vertices[addedVertIndex]);
                                                resultsMesh.Vertices[addedVertIndex] -= new Vector3Float(polygonPlane.Normal * distanceToPlane);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }

                    if (resultsMesh.Faces.Count - faceCountPreAdd > 0)
                    {
                        // keep track of the adds so we can process the coplanar faces after
                        for (int i = faceCountPreAdd; i < resultsMesh.Faces.Count; i++)
                        {
                            coPlanarFaces.StoreFaceAdd(cutPlane, mesh1Index, faceIndex, i);
                            // make sure our added faces are the right direction
                            if (resultsMesh.Faces[i].normal.Dot(expectedFaceNormal) < 0)
                            {
                                resultsMesh.FlipFace(i);
                            }
                        }
                    }
                    else // we did not add any faces but we will still keep track of this polygons plane
                    {
                        coPlanarFaces.StoreFaceAdd(cutPlane, mesh1Index, faceIndex, -1);
                    }

                    ratioCompleted += amountPerOperation;
                    progressReporter?.Invoke(ratioCompleted, "");

                    if (cancellationToken.IsCancellationRequested)
                    {
                        return null;
                    }
                }
            }

            // handle the co-planar faces
            ProcessCoplanarFaces(operation, resultsMesh, coPlanarFaces);

            resultsMesh.MergeVertices(.01);
            resultsMesh.CleanAndMerge();
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

        private void ProcessCoplanarFaces(CsgModes operation, Mesh resultsMesh, CoPlanarFaces coPlanarFaces)
        {
            var faceIndicesToRemove = new HashSet<int>();
            foreach (var plane in coPlanarFaces.Planes)
            {
                var meshIndices = coPlanarFaces.MeshIndicesForPlane(plane).ToList();

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
                        meshIndices.AddRange(coPlanarFaces.MeshIndicesForPlane(negativePlane.Value));
                    }
                }

                if (meshIndices.Count() > 1)
                {
                    // check if more than one mesh has this polygons on this plane
                    var transformTo0Plane = transformTo0Planes[plane].matrix;

                    // depending on the operation add or remove polygons that are planar
                    switch (operation)
                    {
                        case CsgModes.Union:
                            coPlanarFaces.UnionFaces(plane, transformedMeshes, resultsMesh, transformTo0Plane, faceIndicesToRemove, this);
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