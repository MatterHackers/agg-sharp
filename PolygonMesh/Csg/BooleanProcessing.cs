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
using System.ComponentModel;
using System.Linq;
using System.Threading;
using ClipperLib;
using DualContouring;
using g3;
using gs;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.VectorMath;

namespace MatterHackers.PolygonMesh.Csg
{
    using Polygon = List<IntPoint>;

    public enum CsgModes
	{
		Union,
		Subtract,
		Intersect
	}

	public enum IplicitSurfaceMethod
	{
		[Description("Faster but less accurate")]
		Grid,
		[Description("Slower but more accurate")]
		Exact
	};

	public enum ProcessingModes
	{
		[Description("Default CSG processing")]
		Polygons,
		[Description("Experimental Marching Cubes")]
		Marching_Cubes,
		[Description("Experimental Dual Contouring")]
		Dual_Contouring,
	}

	public enum ProcessingResolution
	{
		_64 = 6,
		_128 = 7,
		_256 = 8,
		_512 = 9,
	}

	public static class BooleanProcessing
	{
		public static Mesh DoArray(IEnumerable<(Mesh mesh, Matrix4X4 matrix)> items,
			CsgModes operation,
			ProcessingModes processingMode,
			ProcessingResolution inputResolution,
			ProcessingResolution outputResolution,
			Action<double, string> reporter,
			CancellationToken cancellationToken,
            double amountPerOperation = 1,
			double ratioCompleted = 0)
		{
			if (processingMode == ProcessingModes.Polygons)
			{
				var allManifold = items.All(i => i.mesh.IsManifold());

				if (allManifold)
				{
					// Convert meshes to MeshLib format
					var manifolds = new List<ManifoldNET.Manifold>();
					foreach (var (mesh, matrix) in items)
					{
						var meshCopy = mesh.Copy(CancellationToken.None);
						meshCopy.Transform(matrix);

						// Convert to vertex and face arrays
						var vertProperties = new List<float>();
						var triVerts = new List<uint>();

						foreach (var vertex in meshCopy.Vertices)
						{
							vertProperties.Add(vertex.X);
							vertProperties.Add(vertex.Y);
							vertProperties.Add(vertex.Z);
						}

						foreach (var face in meshCopy.Faces)
						{
							triVerts.Add((uint)face.v0);
							triVerts.Add((uint)face.v1);
							triVerts.Add((uint)face.v2);
						}

						var meshGlData = new ManifoldNET.MeshGLData(vertProperties.ToArray(), triVerts.ToArray());
						var meshGl = new ManifoldNET.MeshGL(meshGlData);

						manifolds.Add(ManifoldNET.Manifold.Create(meshGl));
					}

					// Perform boolean operation using MeshLib
					// Convert operation type to MeshLib enum
					var opperationType = ManifoldNET.BoolOperationType.Add;

					if (operation == CsgModes.Subtract)
					{
						opperationType = ManifoldNET.BoolOperationType.Subtract;
					}
					else if (operation == CsgModes.Intersect)
					{
						opperationType = ManifoldNET.BoolOperationType.Intersect;
					}

					ManifoldNET.MeshGL result = null;
					// Perform boolean operation on first two meshes
					if (manifolds.Count >= 2)
					{
						try
						{
							result = ManifoldNET.Manifold.BatchBoolOperation(manifolds, opperationType).MeshGL;
						}
						catch 
						{
                            var csgBySlicing = new CsgBySlicing();
                            csgBySlicing.Setup(items, null, operation, cancellationToken);
                            return csgBySlicing.Calculate((ratio, message) =>
                            {
                                reporter?.Invoke(ratio * amountPerOperation + ratioCompleted, message);
                            },
                            cancellationToken);
                        }
                    }

					// Convert result back to Mesh format
					var resultMesh = new Mesh();

					if (result != null)
					{
						var vertices = result.VerticesProperties;
						for (int i = 0; i < vertices.Length; i += 3)
						{
							resultMesh.Vertices.Add(new Vector3(
								vertices[i],
								vertices[i + 1],
								vertices[i + 2]));
						}

						var indices = result.TriangleVertices;
						for (int i = 0; i < indices.Length; i += 3)
						{
							resultMesh.Faces.Add(new Face(
								indices[i],
								indices[i + 1],
								indices[i + 2],
								resultMesh.Vertices));
						}
					}
                
					return resultMesh;
                }
                else
				{
					var csgBySlicing = new CsgBySlicing();
					csgBySlicing.Setup(items, null, operation, cancellationToken);
					return csgBySlicing.Calculate((ratio, message) =>
					{
						reporter?.Invoke(ratio * amountPerOperation + ratioCompleted, message);
					},
					cancellationToken);
				}
            }
			else
			{
				return AsImplicitMeshes(items, operation, processingMode, inputResolution, outputResolution);
			}
		}

		private static Mesh AsImplicitMeshes(IEnumerable<(Mesh mesh, Matrix4X4 matrix)> items,
			CsgModes operation,
			ProcessingModes processingMode,
			ProcessingResolution inputResolution,
			ProcessingResolution outputResolution)
		{
			Mesh implicitResult = null;

			var implicitMeshs = new List<BoundedImplicitFunction3d>();
			foreach (var (mesh, matrix) in items)
			{
				var meshCopy = mesh.Copy(CancellationToken.None);
				meshCopy.Transform(matrix);

				implicitMeshs.Add(GetImplicitFunction(meshCopy, processingMode == ProcessingModes.Polygons, 1 << (int)inputResolution));
			}

			DMesh3 GenerateMeshF(BoundedImplicitFunction3d root, int numCells)
			{
				var bounds = root.Bounds();

				var c = new MarchingCubesPro()
				{
					Implicit = root,
					RootMode = MarchingCubesPro.RootfindingModes.LerpSteps,      // cube-edge convergence method
					RootModeSteps = 5,                                        // number of iterations
					Bounds = bounds,
					CubeSize = bounds.MaxDim / numCells,
				};

				c.Bounds.Expand(3 * c.CubeSize);                            // leave a buffer of cells
				c.Generate();

				MeshNormals.QuickCompute(c.Mesh);                           // generate normals
				return c.Mesh;
			}

			switch (operation)
			{
				case CsgModes.Union:
					if (processingMode == ProcessingModes.Dual_Contouring)
					{
						var union = new ImplicitNaryUnion3d()
						{
							Children = implicitMeshs
						};
						var bounds = union.Bounds();
						var size = bounds.Max - bounds.Min;
						var root = Octree.BuildOctree((pos) =>
						{
							var pos2 = new Vector3d(pos.X, pos.Y, pos.Z);
							return union.Value(ref pos2);
						}, new Vector3(bounds.Min.x, bounds.Min.y, bounds.Min.z),
						new Vector3(size.x, size.y, size.z),
						(int)outputResolution,
						.001);
						implicitResult = Octree.GenerateMeshFromOctree(root);
					}
					else
					{
						implicitResult = GenerateMeshF(new ImplicitNaryUnion3d()
						{
							Children = implicitMeshs
						}, 1 << (int)outputResolution).ToMesh();
					}
					break;

				case CsgModes.Subtract:
					{
						if (processingMode == ProcessingModes.Dual_Contouring)
						{
							var subtract = new ImplicitNaryIntersection3d()
							{
								Children = implicitMeshs
							};
							var bounds = subtract.Bounds();
							var root = Octree.BuildOctree((pos) =>
							{
								var pos2 = new Vector3d(pos.X, pos.Y, pos.Z);
								return subtract.Value(ref pos2);
							}, new Vector3(bounds.Min.x, bounds.Min.y, bounds.Min.z),
							new Vector3(bounds.Width, bounds.Depth, bounds.Height),
							(int)outputResolution,
							.001);
							implicitResult = Octree.GenerateMeshFromOctree(root);
						}
						else
						{
							implicitResult = GenerateMeshF(new ImplicitNaryDifference3d()
							{
								A = implicitMeshs.First(),
								BSet = implicitMeshs.GetRange(0, implicitMeshs.Count - 1)
							}, 1 << (int)outputResolution).ToMesh();
						}
					}
					break;

				case CsgModes.Intersect:
					if (processingMode == ProcessingModes.Dual_Contouring)
					{
						var intersect = new ImplicitNaryIntersection3d()
						{
							Children = implicitMeshs
						};
						var bounds = intersect.Bounds();
						var root = Octree.BuildOctree((pos) =>
						{
							var pos2 = new Vector3d(pos.X, pos.Y, pos.Z);
							return intersect.Value(ref pos2);
						}, new Vector3(bounds.Min.x, bounds.Min.y, bounds.Min.z),
						new Vector3(bounds.Width, bounds.Depth, bounds.Height),
						(int)outputResolution,
						.001);
						implicitResult = Octree.GenerateMeshFromOctree(root);
					}
					else
					{
						implicitResult = GenerateMeshF(new ImplicitNaryIntersection3d()
						{
							Children = implicitMeshs
						}, 1 << (int)outputResolution).ToMesh();
					}
					break;
			}

			return implicitResult;
		}

		public static Mesh Do(Mesh inMeshA,
			Matrix4X4 matrixA,
			// mesh B
			Mesh inMeshB,
			Matrix4X4 matrixB,
			// operation
			CsgModes operation,
            ProcessingModes processingMode = ProcessingModes.Polygons,
			ProcessingResolution inputResolution = ProcessingResolution._64,
			ProcessingResolution outputResolution = ProcessingResolution._64,
            // reporting
            Action<double, string> reporter = null,
			double amountPerOperation = 1,
			double ratioCompleted = 0,
			CancellationToken cancellationToken = default)
		{
			if (processingMode == ProcessingModes.Polygons)
			{
				return BooleanProcessing.DoArray(new (Mesh, Matrix4X4)[] { (inMeshA, matrixA), (inMeshB, matrixB) },
					operation,
					processingMode,
					inputResolution,
					outputResolution,
					reporter,
                    cancellationToken,
                    amountPerOperation,
					ratioCompleted);
			}
			else
			{
				var meshA = inMeshA.Copy(CancellationToken.None);
				meshA.Transform(matrixA);

				var meshB = inMeshB.Copy(CancellationToken.None);
				meshB.Transform(matrixB);

				if (meshA.Faces.Count < 4)
				{
					return meshB;
				}
				else if (meshB.Faces.Count < 4)
				{
					return meshA;
				}

				var implicitA = GetImplicitFunction(meshA, processingMode == ProcessingModes.Polygons, (int)inputResolution);
				var implicitB = GetImplicitFunction(meshB, processingMode == ProcessingModes.Polygons, (int)inputResolution);

				DMesh3 GenerateMeshF(BoundedImplicitFunction3d root, int numCells)
				{
					var bounds = root.Bounds();

					var c = new MarchingCubes()
					{
						Implicit = root,
						RootMode = MarchingCubes.RootfindingModes.LerpSteps,      // cube-edge convergence method
						RootModeSteps = 5,                                        // number of iterations
						Bounds = bounds,
						CubeSize = bounds.MaxDim / numCells,
					};

					c.Bounds.Expand(3 * c.CubeSize);                            // leave a buffer of cells
					c.Generate();

					MeshNormals.QuickCompute(c.Mesh);                           // generate normals
					return c.Mesh;
				}

				var marchingCells = 1 << (int)outputResolution;
				switch (operation)
				{
					case CsgModes.Union:
						return GenerateMeshF(new ImplicitUnion3d()
						{
							A = implicitA,
							B = implicitB
						}, marchingCells).ToMesh();

					case CsgModes.Subtract:
						return GenerateMeshF(new ImplicitDifference3d()
						{
							A = implicitA,
							B = implicitB
						}, marchingCells).ToMesh();

					case CsgModes.Intersect:
						return GenerateMeshF(new ImplicitIntersection3d()
						{
							A = implicitA,
							B = implicitB
						}, marchingCells).ToMesh();
				}
			}

			return null;
		}

		class MWNImplicit : BoundedImplicitFunction3d
		{
			public DMeshAABBTree3 MeshAABBTree3;
			public AxisAlignedBox3d Bounds() { return MeshAABBTree3.Bounds; }
			public double Value(ref Vector3d pt)
			{
				return -(MeshAABBTree3.FastWindingNumber(pt) - 0.5);
			}
		}


		public static BoundedImplicitFunction3d GetImplicitFunction(Mesh mesh, bool exact, int numCells)
		{
			var meshA3 = mesh.ToDMesh3();

			// Interesting experiment, this produces an extremely accurate surface representation but is quite slow (even though fast) compared to voxel lookups.
			if (exact)
			{
				DMeshAABBTree3 meshAABBTree3 = new DMeshAABBTree3(meshA3, true);
				meshAABBTree3.FastWindingNumber(Vector3d.Zero);   // build approximation
				return new MWNImplicit()
				{
					MeshAABBTree3 = meshAABBTree3
				};
			}
			else
			{
				double meshCellsize = meshA3.CachedBounds.MaxDim / numCells;
				var signedDistance = new MeshSignedDistanceGrid(meshA3, meshCellsize);
				signedDistance.Compute();
				return new DenseGridTrilinearImplicit(signedDistance.Grid, signedDistance.GridOrigin, signedDistance.CellSize);
			}
		}
    }
}