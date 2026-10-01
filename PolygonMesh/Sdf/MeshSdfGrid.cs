/*
Copyright (c) 2026, Lars Brubaker
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
using System.Threading;
using System.Threading.Tasks;
using g3;
using MatterHackers.PolygonMesh.Csg;
using MatterHackers.PolygonMesh.Processors;

namespace MatterHackers.PolygonMesh.Sdf
{
	/// <summary>
	/// Computes the unsigned narrow-band distance grid for <see cref="MeshSdfGrid"/> somewhere other
	/// than g3's CPU flood - a GPU driver (agg's WebGpuRender GpuMeshSdf) plugs in here. Primitive
	/// arrays only, so PolygonMesh takes no dependency on the renderer that implements it.
	/// </summary>
	/// <param name="triangles">Nine floats per triangle, its corners relative to the grid origin.</param>
	/// <param name="ni">Samples along x; x is the fastest-varying index of the result.</param>
	/// <param name="nj">Samples along y.</param>
	/// <param name="nk">Samples along z.</param>
	/// <param name="cellSize">Sample spacing; sample (i, j, k) sits at (i, j, k) * cellSize.</param>
	/// <param name="band">Samples nearer than this to a triangle get the exact distance ...</param>
	/// <param name="cap">... the rest get this value.</param>
	/// <returns>ni * nj * nk unsigned distances, index i + ni * (j + nj * k).</returns>
	public delegate float[] DistanceGridBuilder(float[] triangles, int ni, int nj, int nk, float cellSize, float band, float cap);


	/// <summary>
	/// A signed distance field of a solid, sampled once on a grid, from which the offset surface at
	/// any radius up to <see cref="MaxRadius"/> can be pulled by marching cubes - the preview a
	/// Dilate / Erode radius drag shows before the exact kernel result replaces it.
	/// </summary>
	/// <remarks>
	/// The field is negative inside. Distances come from geometry3Sharp's
	/// <see cref="MeshSignedDistanceGrid"/> in its narrow-band flood-fill mode, which is exact out to
	/// the band and never visits voxels beyond it; the band is <see cref="MaxRadius"/> plus two
	/// cells, so every iso-surface up to that radius crosses only exact samples. Voxels past the band
	/// keep a large magnitude, which is all marching cubes needs of them.
	/// The sign is ours, not g3's: g3 signs by a winding count along X alone, which inverted faces
	/// break. Here every grid line along each of the three axes counts triangle crossings (orientation
	/// free), and a voxel is inside when at least two of its three parities say so - one axis grazing
	/// an edge or a small hole can not flip a voxel on its own.
	/// </remarks>
	public class MeshSdfGrid
	{
		/// <summary>The most voxels along the longest axis: 256^3 floats is 64 MB, and the step
		/// time grows with the cube of it.</summary>
		public const int MaxResolution = 256;

		private readonly Mesh source;
		private readonly object buildLock = new object();
		private volatile DenseGrid3f grid;
		private Vector3d gridOrigin;
		private double cellSize;
		private int gridBuilds;
		private int orientRepairs;
		private readonly DistanceGridBuilder distanceBuilder;

		/// <param name="solid">The part; its shells are oriented as Dilate and Erode read them.</param>
		/// <param name="maxRadius">
		/// The largest |iso| the caller will ask <see cref="Extract"/> for: the largest Dilate
		/// radius of the drag, or the largest Erode / Hollow Out depth - the band holds exact
		/// distances this far on both sides of the surface, and the grid is padded by it outside.
		/// </param>
		/// <param name="resolution">Voxels along the grid's longest axis, padding included;
		/// clamped to <see cref="MaxResolution"/>.</param>
		/// <param name="distanceBuilder">Computes the unsigned band distances instead of g3's CPU
		/// flood (null = the flood); the sign is still the crossing vote here.</param>
		public MeshSdfGrid(Mesh solid, double maxRadius, int resolution = 128, DistanceGridBuilder distanceBuilder = null)
		{
			ArgumentNullException.ThrowIfNull(solid);
			if (!(maxRadius > 0))
			{
				throw new ArgumentOutOfRangeException(nameof(maxRadius), maxRadius, "The offset range must be positive.");
			}

			if (resolution < 16)
			{
				throw new ArgumentOutOfRangeException(nameof(resolution), resolution, "A grid needs at least 16 voxels along its longest axis.");
			}

			source = solid;
			MaxRadius = maxRadius;
			Resolution = Math.Min(resolution, MaxResolution);
			this.distanceBuilder = distanceBuilder;
		}

		/// <summary>Gets how many times the distance field has been computed; one per instance.</summary>
		public int GridBuilds => gridBuilds;

		/// <summary>Gets the largest |iso| the band holds exact distances for.</summary>
		public double MaxRadius { get; }

		/// <summary>Gets the voxel count along the longest padded axis.</summary>
		public int Resolution { get; }

		/// <summary>Gets the voxel edge length, available once the field is built.</summary>
		public double CellSize => cellSize;

		/// <summary>Gets how many builds sent the part through the kernel's shell orientation
		/// rather than taking it as is; for tests.</summary>
		internal int OrientRepairs => orientRepairs;

		/// <summary>Gets the voxels the band flood processed in the last build, finished or
		/// cancelled - the build's dominant work, counted rather than timed.</summary>
		internal long FloodWork { get; private set; }

		/// <summary>
		/// Called with the flood's running work count - the voxels processed so far, compared with
		/// <see cref="FloodWork"/> of an earlier build or an estimate of the band's size - so a long
		/// build can move a progress bar. Called once per voxel from the flood's worker threads, so it
		/// must be cheap and thread safe.
		/// </summary>
		public Action<long> FloodProgress { get; set; }

		/// <summary>
		/// Whether the build orients the part's shells first. Leave it true unless the caller has
		/// already run <see cref="MinkowskiProcessing.OrientShellsAsSolid"/> on the mesh it passes
		/// (Hollow Out does, for its outer shell), which makes a second pass pure cost.
		/// </summary>
		public bool OrientShells { get; init; } = true;

		/// <summary>Gets the sampled field (negative inside), for tests; null until built.</summary>
		internal DenseGrid3f Field => grid;

		/// <summary>Gets the position of the field's sample (0, 0, 0), for tests.</summary>
		internal Vector3d FieldOrigin => gridOrigin;

		/// <summary>
		/// Computes the field if it has not been computed yet. <see cref="Extract"/> calls this, so a
		/// caller only needs it to pay the cost up front (the first frame of a drag). Safe to call
		/// from several threads: one builds, the others wait for it. A cancelled build throws
		/// <see cref="OperationCanceledException"/> and leaves the field unbuilt, so a later call
		/// starts over.
		/// </summary>
		public void Build(CancellationToken cancellationToken = default)
		{
			if (grid != null)
			{
				return;
			}

			lock (buildLock)
			{
				if (grid == null)
				{
					BuildField(cancellationToken);
				}
			}
		}

		private void BuildField(CancellationToken cancellationToken)
		{
			// Orient first, as the kernel does, so the preview is of the same solid the exact
			// Dilate / Erode will build - its repair also joins split seams the crossing count
			// would otherwise see as doubled walls.
			Mesh oriented = source;
			try
			{
				// A closed, consistently wound part with no inverted shell (most parts) comes back
				// from the kernel unchanged, so skip its import and read-back (~200 ms at 172k).
				if (OrientShells && MinkowskiShellOrientation.MeshMayNeedRepair(source))
				{
					Interlocked.Increment(ref orientRepairs);
					oriented = MinkowskiProcessing.OrientShellsAsSolid(source, cancellationToken);
				}
			}
			catch (MeshImportRejectedException)
			{
				// The kernel refuses an open part (a missing triangle is enough), but the crossing
				// count needs no orientation and the vote outruns a small hole, so the preview
				// still shows the part as the solid it nearly is.
				oriented = source;
			}

			var dmesh = oriented.ToDMesh3();
			var bounds = dmesh.CachedBounds;
			double longest = Math.Max(bounds.Width, Math.Max(bounds.Height, bounds.Depth));

			// The band - MaxRadius plus two cells, so every iso crossing up to MaxRadius sits
			// between exact samples - is also the padding each side, the reach of the largest
			// Dilate. This cell size makes the padded longest axis Resolution voxels.
			cellSize = (longest + (2 * MaxRadius)) / (Resolution - 4);
			DenseGrid3f field;
			if (distanceBuilder != null)
			{
				field = BuildWithInjectedDistances(dmesh, bounds, cancellationToken);
			}
			else
			{
				var spatial = new DMeshAABBTree3(dmesh, autoBuild: true);
				var distances = new MeshSignedDistanceGrid(dmesh, cellSize, spatial)
				{
					ComputeMode = MeshSignedDistanceGrid.ComputeModes.NarrowBand_SpatialFloodFill,
					NarrowBandMaxDistance = MaxRadius + (2 * cellSize),
					PadWidth = MaxRadius + (2 * cellSize),
					ComputeSigns = false,
					CancelF = () => cancellationToken.IsCancellationRequested,
					FloodProgress = FloodProgress,
				};
				distances.Compute();
				FloodWork = distances.FloodVoxelsProcessed;
				cancellationToken.ThrowIfCancellationRequested();

				field = distances.Grid;
				var origin = distances.GridOrigin;
				gridOrigin = new Vector3d(origin.x, origin.y, origin.z);
			}

			ApplyCrossingSigns(dmesh, field, gridOrigin, cellSize, cancellationToken);

			grid = field;
			Interlocked.Increment(ref gridBuilds);
		}

		/// <summary>
		/// The same grid layout g3's <see cref="MeshSignedDistanceGrid.Compute"/> lays out with
		/// PadWidth set (float origin, float cell size, the same truncating counts), so the injected
		/// distances land on exactly the samples the CPU flood would have written.
		/// </summary>
		private DenseGrid3f BuildWithInjectedDistances(DMesh3 dmesh, AxisAlignedBox3d bounds, CancellationToken cancellationToken)
		{
			float dx = (float)cellSize;
			float pad = (float)(MaxRadius + (2 * cellSize));
			Vector3f origin = (Vector3f)bounds.Min - (pad * Vector3f.One);
			Vector3f max = (Vector3f)bounds.Max + (pad * Vector3f.One);
			int ni = (int)((max.x - origin.x) / dx) + 1;
			int nj = (int)((max.y - origin.y) / dx) + 1;
			int nk = (int)((max.z - origin.z) / dx) + 1;
			gridOrigin = new Vector3d(origin.x, origin.y, origin.z);

			var triangles = new float[dmesh.TriangleCount * 9];
			int t = 0;
			foreach (int tid in dmesh.TriangleIndices())
			{
				Vector3d p0 = Vector3d.Zero, p1 = Vector3d.Zero, p2 = Vector3d.Zero;
				dmesh.GetTriVertices(tid, ref p0, ref p1, ref p2);
				foreach (var p in new[] { p0, p1, p2 })
				{
					triangles[t++] = (float)(p.x - gridOrigin.x);
					triangles[t++] = (float)(p.y - gridOrigin.y);
					triangles[t++] = (float)(p.z - gridOrigin.z);
				}
			}

			cancellationToken.ThrowIfCancellationRequested();

			// g3's upper bound for samples the band never reached.
			float cap = (ni + nj + nk) * dx;
			float[] distances = distanceBuilder(triangles, ni, nj, nk, dx, (float)(MaxRadius + (2 * cellSize)), cap);
			var field = new DenseGrid3f(ni, nj, nk, 0);
			Array.Copy(distances, field.Buffer, field.size);
			return field;
		}

		/// <summary>
		/// The surface at signed distance <paramref name="iso"/> from the part: positive grows it
		/// (Dilate), negative shrinks it (Erode). Reads only the cached field.
		/// </summary>
		public Mesh Extract(double iso, CancellationToken cancellationToken = default)
		{
			if (Math.Abs(iso) > MaxRadius)
			{
				throw new ArgumentOutOfRangeException(nameof(iso), iso, $"This field holds offsets up to {MaxRadius} either way; build it with maxRadius at least |iso| (the largest Dilate radius or Erode / Hollow depth of the drag).");
			}

			Build(cancellationToken);

			return GridMarchingCubes.Extract(grid, gridOrigin, cellSize, iso, cancellationToken);
		}

		/// <summary>
		/// Negates every voxel that at least two of the three axis crossing parities put inside.
		/// </summary>
		private static void ApplyCrossingSigns(DMesh3 mesh, DenseGrid3f field, Vector3d origin, double dx, CancellationToken cancellationToken)
		{
			var options = new ParallelOptions { CancellationToken = cancellationToken };
			int[] size = { field.ni, field.nj, field.nk };
			int[] stride = { 1, field.ni, field.ni * field.nj };
			var crossings = new int[field.size];
			var insideVotes = new byte[field.size];
			int[] triangles = new int[mesh.TriangleCount];
			int count = 0;
			foreach (int tid in mesh.TriangleIndices())
			{
				triangles[count++] = tid;
			}

			for (int axis = 0; axis < 3; axis++)
			{
				cancellationToken.ThrowIfCancellationRequested();
				int b = (axis + 1) % 3;
				int c = (axis + 2) % 3;
				Array.Clear(crossings);

				// A crossing at line coordinate t lands in the first voxel at or past it, so a
				// running parity along the line is odd exactly for the voxels inside.
				Parallel.For(0, count, options, t =>
				{
					Vector3d p0 = Vector3d.Zero, p1 = Vector3d.Zero, p2 = Vector3d.Zero;
					mesh.GetTriVertices(triangles[t], ref p0, ref p1, ref p2);
					double a0 = (p0[axis] - origin[axis]) / dx, a1 = (p1[axis] - origin[axis]) / dx, a2 = (p2[axis] - origin[axis]) / dx;
					double b0 = (p0[b] - origin[b]) / dx, b1 = (p1[b] - origin[b]) / dx, b2 = (p2[b] - origin[b]) / dx;
					double c0 = (p0[c] - origin[c]) / dx, c1 = (p1[c] - origin[c]) / dx, c2 = (p2[c] - origin[c]) / dx;
					int bMin = Math.Max(0, (int)Math.Ceiling(Math.Min(b0, Math.Min(b1, b2))));
					int bMax = Math.Min(size[b] - 1, (int)Math.Floor(Math.Max(b0, Math.Max(b1, b2))));
					int cMin = Math.Max(0, (int)Math.Ceiling(Math.Min(c0, Math.Min(c1, c2))));
					int cMax = Math.Min(size[c] - 1, (int)Math.Floor(Math.Max(c0, Math.Max(c1, c2))));
					for (int jc = cMin; jc <= cMax; jc++)
					{
						for (int jb = bMin; jb <= bMax; jb++)
						{
							// Batty's exact orientation test with its tie-break: a line through a
							// shared edge or vertex is counted for exactly one of the triangles.
							if (!MeshSignedDistanceGrid.point_in_triangle_2d(jb, jc, b0, c0, b1, c1, b2, c2, out double w0, out double w1, out double w2))
							{
								continue;
							}

							double hit = (w0 * a0 + w1 * a1 + w2 * a2) / (w0 + w1 + w2);
							int ia = (int)Math.Ceiling(hit);
							if (ia < 0)
							{
								ia = 0;
							}
							else if (ia >= size[axis])
							{
								continue;
							}

							Interlocked.Increment(ref crossings[(ia * stride[axis]) + (jb * stride[b]) + (jc * stride[c])]);
						}
					}
				});

				Parallel.For(0, size[b] * size[c], options, line =>
				{
					int jb = line % size[b];
					int jc = line / size[b];
					int lineStart = (jb * stride[b]) + (jc * stride[c]);
					int parity = 0;
					for (int ia = 0; ia < size[axis]; ia++)
					{
						int index = lineStart + (ia * stride[axis]);
						parity ^= crossings[index] & 1;
						insideVotes[index] += (byte)parity;
					}
				});
			}

			Parallel.For(0, field.size, options, index =>
			{
				if (insideVotes[index] >= 2)
				{
					field.Buffer[index] = -field.Buffer[index];
				}
			});
		}
	}
}
