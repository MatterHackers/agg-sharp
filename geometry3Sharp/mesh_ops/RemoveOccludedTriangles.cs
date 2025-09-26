// Copyright (c) 2025 Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Distributed under the Boost Software License, Version 1.0. http://www.boost.org/LICENSE_1_0.txt
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using g3;

namespace gs
{
	/// <summary>
	/// Remove "occluded" triangles, ie triangles on the "inside" of the mesh. 
	/// This is a fuzzy definition, current implementation is basically computing
	/// something akin to ambient occlusion, and if face is fully occluded, then
	/// we classify it as inside and remove it.
	/// </summary>
	public class RemoveOccludedTriangles
	{
		public DMesh3 Mesh;
		public DMeshAABBTree3 Spatial;

		// indices of removed triangles. List will be empty if nothing removed
		public List<int> RemovedT = null;

		// Mesh.RemoveTriange() can return false, if that happens, this will be true
		public bool RemoveFailed = false;

		// if true, then we discard tris if any vertex is occluded.
		// Otherwise we discard based on tri centroids
		public bool PerVertex = false;

		// we nudge points out by this amount to try to counteract numerical issues
		public double NormalOffset = MathUtil.ZeroTolerance;

		// use this as winding isovalue for WindingNumber mode
		public double WindingIsoValue = 0.5;

		public enum CalculationMode
		{
			RayParity = 0,
			AnalyticWindingNumber = 1,
			FastWindingNumber = 2,
			SimpleOcclusionTest = 3
		}
		public CalculationMode InsideMode = CalculationMode.RayParity;


		/// <summary>
		/// Set this to be able to cancel running remesher
		/// </summary>
		public ProgressCancel Progress = null;

		/// <summary>
		/// Progress reporter that takes a ratio (0-1) and a message string
		/// </summary>
		public Action<double, string> ProgressReporter = null;

		/// <summary>
		/// if this returns true, abort computation. 
		/// </summary>
		protected virtual bool Cancelled()
		{
			return (Progress == null) ? false : Progress.Cancelled();
		}



		public RemoveOccludedTriangles(DMesh3 mesh)
		{
			Mesh = mesh;
		}

		public RemoveOccludedTriangles(DMesh3 mesh, DMeshAABBTree3 spatial)
		{
			Mesh = mesh;
			Spatial = spatial;
		}


		public virtual bool Apply()
		{
			ProgressReporter?.Invoke(0.0, "Building spatial tree");

			DMesh3 testAgainstMesh = Mesh;
			if (InsideMode == CalculationMode.RayParity)
			{
				var loops = new MeshBoundaryLoops(testAgainstMesh);
				if (loops.Count > 0)
				{
					ProgressReporter?.Invoke(0.05, "Filling holes for ray parity test");
					testAgainstMesh = new DMesh3(Mesh);
					foreach (var loop in loops)
					{
						if (Cancelled())
						{
							return false;
						}

						var filler = new SimpleHoleFiller(testAgainstMesh, loop);
						filler.Fill();
					}
				}
			}

			DMeshAABBTree3 spatial = (Spatial != null && testAgainstMesh == Mesh) ?
				Spatial : new DMeshAABBTree3(testAgainstMesh, true);
			if (InsideMode == CalculationMode.AnalyticWindingNumber)
			{
				ProgressReporter?.Invoke(0.1, "Initializing analytic winding number");
				spatial.WindingNumber(Vector3d.Zero);
			}
			else if (InsideMode == CalculationMode.FastWindingNumber)
			{
				ProgressReporter?.Invoke(0.1, "Initializing fast winding number");
				spatial.FastWindingNumber(Vector3d.Zero);
			}

			if (Cancelled())
			{
				return false;
			}

			// ray directions
			List<Vector3d> ray_dirs = null; int NR = 0;
			if (InsideMode == CalculationMode.SimpleOcclusionTest)
			{
				ray_dirs = new List<Vector3d>();
				ray_dirs.Add(Vector3d.AxisX); ray_dirs.Add(-Vector3d.AxisX);
				ray_dirs.Add(Vector3d.AxisY); ray_dirs.Add(-Vector3d.AxisY);
				ray_dirs.Add(Vector3d.AxisZ); ray_dirs.Add(-Vector3d.AxisZ);
				NR = ray_dirs.Count;
			}

			Func<Vector3d, bool> isOccludedF = (pt) =>
			{

				if (InsideMode == CalculationMode.RayParity)
				{
					return spatial.IsInside(pt);
				}
				else if (InsideMode == CalculationMode.AnalyticWindingNumber)
				{
					return spatial.WindingNumber(pt) > WindingIsoValue;
				}
				else if (InsideMode == CalculationMode.FastWindingNumber)
				{
					return spatial.FastWindingNumber(pt) > WindingIsoValue;
				}
				else
				{
					for (int k = 0; k < NR; ++k)
					{
						int hit_tid = spatial.FindNearestHitTriangle(new Ray3d(pt, ray_dirs[k]));
						if (hit_tid == DMesh3.InvalidID)
						{
							return false;
						}
					}
					return true;
				}
			};

			bool cancel = false;

			BitArray vertices = null;
			if (PerVertex)
			{
				ProgressReporter?.Invoke(0.15, "Testing vertices for occlusion");
				
				vertices = new BitArray(Mesh.MaxVertexID);

				MeshNormals normals = null;
				if (Mesh.HasVertexNormals == false)
				{
					normals = new MeshNormals(Mesh);
					normals.Compute();
				}

				var vertexIndices = Mesh.VertexIndices().ToArray();
				int totalVertices = vertexIndices.Length;
				int processedVertices = 0;
				int reportSpan = Math.Max(1, totalVertices / 150); // Limit to ~150 reports for vertex phase
				object progressLock = new object();

				gParallel.ForEach(vertexIndices, (vid) =>
				{
					if (cancel)
					{
						return;
					}

					if (vid % 10 == 0)
					{
						cancel = Cancelled();
					}

					Vector3d c = Mesh.GetVertex(vid);
					Vector3d n = (normals == null) ? Mesh.GetVertexNormal(vid) : normals[vid];
					c += n * NormalOffset;
					vertices[vid] = isOccludedF(c);

					// Report progress with limited frequency
					int currentProcessed = Interlocked.Increment(ref processedVertices);
					if (currentProcessed % reportSpan == 0 || currentProcessed == totalVertices)
					{
						double ratio = 0.15 + (double)currentProcessed / totalVertices * 0.35; // Vertex phase is ~35% of total
						ProgressReporter?.Invoke(ratio, $"Testing vertices)");
					}
				});
			}
			if (Cancelled())
			{
				return false;
			}

			ProgressReporter?.Invoke(0.5, "Testing triangles for occlusion");

			RemovedT = new List<int>();
			var removeLock = new SpinLock();

			var triangleIndices = Mesh.TriangleIndices().ToArray();
			int totalTriangles = triangleIndices.Length;
			int processedTriangles = 0;
			int triangleReportSpan = Math.Max(1, totalTriangles / 150); // Limit to ~150 reports for triangle phase

			gParallel.ForEach(triangleIndices, (tid) =>
			{
				if (cancel)
				{
					return;
				}

				if (tid % 10 == 0)
				{
					cancel = Cancelled();
				}

				bool inside = false;
				if (PerVertex)
				{
					Index3i tri = Mesh.GetTriangle(tid);
					inside = vertices[tri.a] || vertices[tri.b] || vertices[tri.c];

				}
				else
				{
					Vector3d c = Mesh.GetTriCentroid(tid);
					Vector3d n = Mesh.GetTriNormal(tid);
					c += n * NormalOffset;
					inside = isOccludedF(c);
				}

				if (inside)
				{
					bool taken = false;
					removeLock.Enter(ref taken);
					RemovedT.Add(tid);
					removeLock.Exit();
				}

				// Report progress with limited frequency
				int currentProcessed = Interlocked.Increment(ref processedTriangles);
				if (currentProcessed % triangleReportSpan == 0 || currentProcessed == totalTriangles)
				{
					double ratio = 0.5 + (double)currentProcessed / totalTriangles * 0.4; // Triangle phase is ~40% of total
					ProgressReporter?.Invoke(ratio, $"Testing triangles)");
				}
			});

			if (Cancelled())
			{
				return false;
			}

			if (RemovedT.Count > 0)
			{
				ProgressReporter?.Invoke(0.9, $"Removing {RemovedT.Count} occluded triangles");
				var editor = new MeshEditor(Mesh);
				bool bOK = editor.RemoveTriangles(RemovedT, true);
				RemoveFailed = (bOK == false);
			}

			ProgressReporter?.Invoke(1.0, "Occlusion removal complete");
			return true;
		}



	}
}
