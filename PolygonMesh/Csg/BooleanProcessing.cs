/*
Copyright (c) 2019, 2026, Lars Brubaker, John Lewin
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
using System.Runtime.InteropServices;
using System.Threading;
using ClipperLib;
using DualContouring;
using g3;
using gs;
using MatterHackers.Agg;
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
			double ratioCompleted = 0,
			Color[] meshColors = null)
		{
			if (processingMode == ProcessingModes.Polygons)
			{
				var allManifold = items.All(i => i.mesh.IsManifold());

				if (allManifold)
				{
					try
					{
						return DoArrayViaManifold(items, operation, cancellationToken, reporter, amountPerOperation, ratioCompleted, meshColors);
					}
					catch
					{
						// Manifold native library failed — fall back to managed CsgBySlicing
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

		/// <summary>
		/// Perform a boolean operation via the Manifold native library.
		/// All native P/Invoke calls are contained here so the caller can catch
		/// any managed exception and fall back to CsgBySlicing.
		/// </summary>
		private static Mesh DoArrayViaManifold(
			IEnumerable<(Mesh mesh, Matrix4X4 matrix)> items,
			CsgModes operation,
			CancellationToken cancellationToken,
			Action<double, string> reporter,
			double amountPerOperation,
			double ratioCompleted,
			Color[] meshColors)
		{
			bool trackColors = meshColors != null;

			var manifolds = new List<ManifoldNET.Manifold>();
			var originalIdToColor = new Dictionary<int, Color>();
			var originalIdToSpatialColors = new Dictionary<int, List<(Vector3, Color)>>();
			int meshIndex = 0;
			foreach (var (mesh, matrix) in items)
			{
				if (mesh.Vertices.Count == 0 || mesh.Faces.Count == 0)
				{
					if (operation == CsgModes.Intersect)
					{
						return new Mesh();
					}

					if (meshIndex == 0 && operation == CsgModes.Subtract)
					{
						return new Mesh();
					}

					meshIndex++;
					continue;
				}

				var meshCopy = mesh.Copy(CancellationToken.None);
				meshCopy.Transform(matrix);

				ManifoldNET.Manifold manifold = null;

				if (trackColors && meshCopy.FaceColors != null)
				{
					manifold = TrySplitByFaceColors(meshCopy, originalIdToColor);
				}

				if (manifold == null)
				{
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

					var meshGlData = new ManifoldNET.MeshGLData(vertProperties.ToArray(), triVerts.ToArray(), 3);
					var meshGl = new ManifoldNET.MeshGL(meshGlData);
					manifold = ManifoldNET.Manifold.Create(meshGl);

					if (trackColors)
					{
						manifold = manifold.AsOriginal();
						if (meshCopy.FaceColors != null)
						{
							originalIdToSpatialColors[manifold.OriginalID] = meshCopy.SaveFaceCentroidColors();
						}
						else
						{
							var color = (meshColors != null && meshIndex < meshColors.Length)
								? meshColors[meshIndex]
								: new Color(200, 200, 200, 255);
							originalIdToColor[manifold.OriginalID] = color;
						}
					}
				}

				manifolds.Add(manifold);
				meshIndex++;
			}

			var opperationType = ManifoldNET.BoolOperationType.Add;

			if (operation == CsgModes.Subtract)
			{
				opperationType = ManifoldNET.BoolOperationType.Subtract;
			}
			else if (operation == CsgModes.Intersect)
			{
				opperationType = ManifoldNET.BoolOperationType.Intersect;
			}

			ManifoldNET.Manifold boolResult = null;
			if (manifolds.Count == 1)
			{
				boolResult = manifolds[0];
			}
			else if (manifolds.Count >= 2)
			{
				boolResult = ManifoldNET.Manifold.BatchBoolOperation(manifolds, opperationType);
			}

			var resultMesh = new Mesh();

			if (boolResult != null)
			{
				// Guard against calling MeshGL on an invalid manifold — manifold_get_meshgl can
				// crash the CLR (ExecutionEngineException) when the result is in InvalidConstruction
				// state. Throwing a managed exception here lets the caller's catch block fall back
				// to CsgBySlicing instead of killing the process.
				if (boolResult.Status != ManifoldNET.ManifoldError.NoError)
				{
					throw new InvalidOperationException($"Manifold boolean result has error status: {boolResult.Status}");
				}

				var result = boolResult.MeshGL;
				var resultNumProp = result.PropertiesNumber;
				var vertices = result.VerticesProperties;
				var indices = result.TriangleVertices;

				for (int i = 0; i < vertices.Length; i += resultNumProp)
				{
					resultMesh.Vertices.Add(new Vector3(
						vertices[i],
						vertices[i + 1],
						vertices[i + 2]));
				}

				for (int i = 0; i < indices.Length; i += 3)
				{
					resultMesh.Faces.Add(new Face(
						indices[i],
						indices[i + 1],
						indices[i + 2],
						resultMesh.Vertices));
				}

				if (trackColors && resultMesh.Faces.Count > 0)
				{
					var faceColors = ManifoldRunHelper.ExtractFaceColors(
						result, resultMesh, originalIdToColor, originalIdToSpatialColors);
					if (faceColors != null)
					{
						resultMesh.FaceColors = faceColors;
					}
				}
			}

			return resultMesh;
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
			CancellationToken cancellationToken = default,
			Color[] meshColors = null)
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
					ratioCompleted,
					meshColors);
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

		/// <summary>
		/// Try to split a mesh with FaceColors into sub-manifolds by color group.
		/// Returns a single manifold (union of sub-manifolds) on success, or null if
		/// any color group doesn't form a valid manifold (e.g., from boolean results
		/// where color groups share boundaries).
		/// </summary>
		private static ManifoldNET.Manifold TrySplitByFaceColors(
			Mesh meshCopy,
			Dictionary<int, Color> originalIdToColor)
		{
			try
			{
				var colorGroups = new Dictionary<Color, List<int>>();
				for (int faceIdx = 0; faceIdx < meshCopy.Faces.Count; faceIdx++)
				{
					var faceColor = faceIdx < meshCopy.FaceColors.Length
						? meshCopy.FaceColors[faceIdx]
						: new Color(200, 200, 200, 255);
					if (!colorGroups.TryGetValue(faceColor, out var faceList))
					{
						faceList = new List<int>();
						colorGroups[faceColor] = faceList;
					}

					faceList.Add(faceIdx);
				}

				var subManifolds = new List<ManifoldNET.Manifold>();
				foreach (var (color, faceIndices) in colorGroups)
				{
					var subMesh = new Mesh();
					var vertexMap = new Dictionary<int, int>();

					foreach (var faceIdx in faceIndices)
					{
						var face = meshCopy.Faces[faceIdx];
						int GetOrAddVertex(int origIdx)
						{
							if (!vertexMap.TryGetValue(origIdx, out int newIdx))
							{
								newIdx = subMesh.Vertices.Count;
								subMesh.Vertices.Add(meshCopy.Vertices[origIdx]);
								vertexMap[origIdx] = newIdx;
							}

							return newIdx;
						}

						subMesh.Faces.Add(new Face(
							GetOrAddVertex(face.v0),
							GetOrAddVertex(face.v1),
							GetOrAddVertex(face.v2),
							subMesh.Vertices));
					}

					// Check if sub-mesh is manifold before trying to create a Manifold
					if (!subMesh.IsManifold())
					{
						return null;
					}

					var subVertProps = new List<float>();
					var subTriVerts = new List<uint>();
					foreach (var v in subMesh.Vertices)
					{
						subVertProps.Add(v.X);
						subVertProps.Add(v.Y);
						subVertProps.Add(v.Z);
					}

					foreach (var f in subMesh.Faces)
					{
						subTriVerts.Add((uint)f.v0);
						subTriVerts.Add((uint)f.v1);
						subTriVerts.Add((uint)f.v2);
					}

					var subGlData = new ManifoldNET.MeshGLData(subVertProps.ToArray(), subTriVerts.ToArray(), 3);
					var subGl = new ManifoldNET.MeshGL(subGlData);
					var subManifold = ManifoldNET.Manifold.Create(subGl);
					subManifold = subManifold.AsOriginal();
					originalIdToColor[subManifold.OriginalID] = color;
					subManifolds.Add(subManifold);
				}

				if (subManifolds.Count == 1)
				{
					return subManifolds[0];
				}

				return ManifoldNET.Manifold.BatchBoolOperation(subManifolds, ManifoldNET.BoolOperationType.Add);
			}
			catch
			{
				return null;
			}
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

	/// <summary>
	/// Helper to read run data from Manifold's MeshGL via P/Invoke.
	/// ManifoldNET 1.0.7-alpha exposes RunIndexLength/RunOriginalIdLength
	/// but not the actual data arrays — we access those through the native C API directly.
	/// </summary>
	internal static class ManifoldRunHelper
	{
		[DllImport("manifoldc", CallingConvention = CallingConvention.Cdecl)]
		private static extern ulong manifold_meshgl_run_index_length(IntPtr m);

		[DllImport("manifoldc", CallingConvention = CallingConvention.Cdecl)]
		private static extern ulong manifold_meshgl_run_original_id_length(IntPtr m);

		[DllImport("manifoldc", CallingConvention = CallingConvention.Cdecl)]
		private static extern IntPtr manifold_meshgl_run_index(IntPtr mem, IntPtr m);

		[DllImport("manifoldc", CallingConvention = CallingConvention.Cdecl)]
		private static extern IntPtr manifold_meshgl_run_original_id(IntPtr mem, IntPtr m);

		private static IntPtr GetMeshGlPointer(ManifoldNET.MeshGL meshGl)
		{
			var pointerField = typeof(ManifoldNET.MeshGL).BaseType?.GetField("_pointer",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
			if (pointerField == null)
			{
				return IntPtr.Zero;
			}

			return (IntPtr)pointerField.GetValue(meshGl);
		}

		/// <summary>
		/// Extract per-face colors from a boolean result MeshGL using run data.
		/// Uses run data to find which source mesh each result face came from,
		/// then uses spatial centroid matching for per-face color lookup when
		/// the source had multiple face colors.
		/// </summary>
		public static Color[] ExtractFaceColors(
			ManifoldNET.MeshGL resultMeshGl,
			Mesh resultMesh,
			Dictionary<int, Color> originalIdToColor,
			Dictionary<int, List<(Vector3 centroid, Color color)>> originalIdToSpatialColors = null)
		{
			var faceCount = resultMesh.Faces.Count;
			var meshGlPtr = GetMeshGlPointer(resultMeshGl);
			if (meshGlPtr == IntPtr.Zero)
			{
				return null;
			}

			var runIndexLen = (int)manifold_meshgl_run_index_length(meshGlPtr);
			var runOriginalIdLen = (int)manifold_meshgl_run_original_id_length(meshGlPtr);

			if (runIndexLen < 2 || runOriginalIdLen < 1)
			{
				return null;
			}

			var runIndexMem = Marshal.AllocHGlobal(runIndexLen * sizeof(int));
			var runOriginalIdMem = Marshal.AllocHGlobal(runOriginalIdLen * sizeof(int));

			try
			{
				var runIndexDataPtr = manifold_meshgl_run_index(runIndexMem, meshGlPtr);
				var runOriginalIdDataPtr = manifold_meshgl_run_original_id(runOriginalIdMem, meshGlPtr);

				var runIndex = new int[runIndexLen];
				var runOriginalId = new int[runOriginalIdLen];
				Marshal.Copy(runIndexDataPtr, runIndex, 0, runIndexLen);
				Marshal.Copy(runOriginalIdDataPtr, runOriginalId, 0, runOriginalIdLen);

				var faceColors = new Color[faceCount];
				var defaultColor = new Color(200, 200, 200, 255);

				for (int runIdx = 0; runIdx < runOriginalIdLen; runIdx++)
				{
					int startTri = runIndex[runIdx] / 3;
					int endTri = (runIdx + 1 < runIndexLen) ? runIndex[runIdx + 1] / 3 : faceCount;
					var origId = runOriginalId[runIdx];

					// Check if this OriginalID has spatial face colors
					List<(Vector3 centroid, Color color)> spatialColors = null;
					if (originalIdToSpatialColors != null)
					{
						originalIdToSpatialColors.TryGetValue(origId, out spatialColors);
					}

					if (spatialColors != null)
					{
						// Match each result face to the nearest source face by centroid
						for (int tri = startTri; tri < endTri && tri < faceCount; tri++)
						{
							var face = resultMesh.Faces[tri];
							var centroid = new Vector3(
								(resultMesh.Vertices[face.v0]
								+ resultMesh.Vertices[face.v1]
								+ resultMesh.Vertices[face.v2]) / 3f);
							faceColors[tri] = Mesh.FindNearestCentroidColor(centroid, spatialColors);
						}
					}
					else
					{
						// Single color for this OriginalID
						var color = originalIdToColor.TryGetValue(origId, out var c) ? c : defaultColor;
						for (int tri = startTri; tri < endTri && tri < faceCount; tri++)
						{
							faceColors[tri] = color;
						}
					}
				}

				return faceColors;
			}
			finally
			{
				Marshal.FreeHGlobal(runIndexMem);
				Marshal.FreeHGlobal(runOriginalIdMem);
			}
		}
	}
}