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
using System.Threading;
using ClipperLib;
using DualContouring;
using g3;
using gs;
using MatterHackers.Agg;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.VectorMath;

// Same aliasing convention as the kernel itself (ManifoldKernel.cs): types that come
// from the native kernel are spelled with a Rust prefix so a use site says which
// library it means.
using RustWindingRule = ManifoldRust.WindingRule;

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

	/// <summary>
	/// Entry points for constructive solid geometry over <see cref="Mesh"/>.
	/// </summary>
	/// <remarks>
	/// Polygon mode runs on ManifoldRust, the one and only boolean kernel
	/// (see <see cref="ManifoldKernel"/>, which this type is the public face of). There is
	/// no second engine behind it: an input the kernel cannot take is an exception the caller
	/// sees, not geometry built by different rules. The other processing modes do not use a
	/// kernel at all - they resample the operands as implicit surfaces.
	/// </remarks>
	public static class BooleanProcessing
	{
		/// <summary>
		/// Combines every item into one mesh with a single n-ary boolean.
		/// </summary>
		/// <param name="windingRule">
		/// Which winding numbers the native kernel counts as solid.
		/// <see cref="RustWindingRule.Nonzero"/> keeps inside-out shells as material
		/// rather than letting them cancel, at the cost of forcing the slower robust
		/// engine. Ignored by the implicit-surface modes, which have no such concept.
		/// </param>
		/// <param name="repairOrientation">
		/// Rewind each operand's inside-out shells before combining - the alternative
		/// to <see cref="RustWindingRule.Nonzero"/>, fixing the data once instead of
		/// redefining "solid". Ignored on the same paths.
		/// </param>
		public static Mesh DoArray(IEnumerable<(Mesh mesh, Matrix4X4 matrix)> items,
			CsgModes operation,
			ProcessingModes processingMode,
			ProcessingResolution inputResolution,
			ProcessingResolution outputResolution,
			Action<double, string> reporter,
			CancellationToken cancellationToken,
            double amountPerOperation = 1,
			double ratioCompleted = 0,
			Color[] meshColors = null,
			RustWindingRule windingRule = RustWindingRule.Positive,
			bool repairOrientation = false)
		{
			if (processingMode == ProcessingModes.Polygons)
			{
				// Every input goes to the kernel, and whatever it says is the answer. Both of
				// the gates that used to stand here are gone: the IsManifold pre-gate, because
				// the robust import plus the Auto engine handle closed non-manifold geometry
				// directly, and the catch-all fallback, because a mesh the kernel refused is
				// better reported than silently replaced with one built by other rules.
				return DoArrayViaManifoldRust(items, operation, cancellationToken, reporter, amountPerOperation, ratioCompleted, meshColors, windingRule, repairOrientation);
            }
			else
			{
				return AsImplicitMeshes(items, operation, processingMode, inputResolution, outputResolution);
			}
		}

		/// <summary>
		/// The kernel path <see cref="DoArray"/> takes in <see cref="ProcessingModes.Polygons"/>,
		/// named here so the tests can call it directly and watch the kernel reject an input at
		/// the point it is rejected.
		/// </summary>
		/// <inheritdoc cref="ManifoldKernel.RunBoolean"/>
		internal static Mesh DoArrayViaManifoldRust(
			IEnumerable<(Mesh mesh, Matrix4X4 matrix)> items,
			CsgModes operation,
			CancellationToken cancellationToken,
			Action<double, string> reporter,
			double amountPerOperation,
			double ratioCompleted,
			Color[] meshColors,
			RustWindingRule windingRule = RustWindingRule.Positive,
			bool repairOrientation = false)
		{
			return ManifoldKernel.RunBoolean(
				items, operation, cancellationToken, reporter, amountPerOperation, ratioCompleted, meshColors, windingRule, repairOrientation);
		}

		/// <summary>
		/// What the kernel makes of a mesh offered to it as a boolean operand: whether it would
		/// take it at all, and if so whether the solid intersects itself.
		/// </summary>
		/// <remarks>
		/// Both halves of the answer come from one import, because both are properties of the same
		/// upload and a caller that asked them separately would pay for the mesh twice.
		/// <para>
		/// Exposed because self-intersecting operands are the difference between a union that
		/// finishes and one that does not: they force the kernel off its exact pipeline onto the
		/// robust one, and on a set of large hole-filled meshes that has been measured in tens of
		/// minutes with no end. A caller that has a choice about which operands to hand over needs
		/// to be able to ask first.
		/// </para>
		/// <para>
		/// The import is the same one <see cref="DoArray"/> performs - transform first, weld retry
		/// included - so the verdict is about the manifold the boolean would actually see and not
		/// about some other reading of the same triangles.
		/// </para>
		/// </remarks>
		/// <param name="matrix">
		/// The operand's transform, applied before the import exactly as the boolean applies it. It
		/// can change the answer on its own: a mirroring matrix turns every triangle inside out.
		/// </param>
		/// <param name="repairOrientation">
		/// Import with the same shell-orientation repair the boolean would use, since that changes
		/// which triangles the kernel ends up scanning.
		/// </param>
		public static BooleanOperandVerdict ClassifyBooleanOperand(Mesh mesh, Matrix4X4 matrix, bool repairOrientation = false)
		{
			if (mesh == null
				|| mesh.Faces.Count == 0
				|| mesh.Vertices.Count == 0)
			{
				return BooleanOperandVerdict.Refused;
			}

			try
			{
				var meshCopy = mesh.Copy(CancellationToken.None);
				meshCopy.Transform(matrix);

				using (var imported = ManifoldKernel.Import(meshCopy, repairOrientation))
				{
					return imported.HasSelfIntersections
						? BooleanOperandVerdict.SelfIntersecting
						: BooleanOperandVerdict.Clean;
				}
			}
			catch (Exception)
			{
				// Including the native failures: a mesh the kernel cannot be made to hold is one it
				// would refuse as an operand too, and that is the only thing the caller is asking.
				return BooleanOperandVerdict.Refused;
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

		/// <summary>
		/// The two-operand spelling of <see cref="DoArray"/>.
		/// </summary>
		/// <inheritdoc cref="DoArray"/>
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
			Color[] meshColors = null,
			RustWindingRule windingRule = RustWindingRule.Positive,
			bool repairOrientation = false)
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
					meshColors,
					windingRule,
					repairOrientation);
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
