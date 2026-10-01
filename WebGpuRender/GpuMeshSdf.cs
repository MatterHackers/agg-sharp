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
*/

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.RenderCore;
using MatterHackers.WebGpu;

namespace MatterHackers.WebGpuRender
{
	/// <summary>
	/// The unsigned narrow-band distance grid of a triangle soup, computed on the GPU - a
	/// <c>MeshSdfGrid.DistanceGridBuilder</c> (pass <see cref="ComputeDistances"/> as the builder;
	/// the sign stays the CPU crossing vote in MeshSdfGrid). The production distance fill: agg renders
	/// through WebGPU on every native platform and in the browser, so compute is always available.
	/// </summary>
	/// <remarks>
	/// The CPU buckets
	/// triangles into a uniform grid of cubes <see cref="BucketVoxels"/> samples on a side, each
	/// triangle listed in every bucket its bounds grown by the band touch, so a sample needs only its
	/// own bucket (8 grown measured faster than 4 grown, and than 8 searched with neighbours, whose
	/// dispatch cost more than its smaller upload saved). One compute pass, one thread per sample,
	/// takes the exact closest point-triangle distance over that bucket in f32 and writes it, or the
	/// cap past the band. Samples at (i, j, k) * cellSize with triangles relative to the grid origin
	/// keep f32 error at the part's size, not its distance from the world origin.
	/// The sign stays MeshSdfGrid's CPU crossing vote (7-34 ms on a 172k part): a GPU port in f32
	/// differed from it on samples next to lattice-aligned faces, for almost no time saved.
	/// </remarks>
	public sealed class GpuMeshSdf : IDisposable
	{
		private const string ShaderKey = "agg.GpuMeshSdf.distance";

		private const string DistanceWgsl = @"
struct Params
{
	ni : u32, nj : u32, nk : u32, s : u32,
	bi : u32, bj : u32, bk : u32, pad0 : u32,
	dx : f32, band : f32, cap : f32, pad1 : f32,
};

@group(0) @binding(0) var<uniform> P : Params;
@group(0) @binding(1) var<storage, read> tris : array<f32>;
@group(0) @binding(2) var<storage, read> starts : array<u32>;
@group(0) @binding(3) var<storage, read> ids : array<u32>;
@group(0) @binding(4) var<storage, read_write> dist : array<f32>;

// Ericson, Real-Time Collision Detection 5.1.5: closest point on triangle abc to p, by region.
fn closestSq(p : vec3<f32>, a : vec3<f32>, b : vec3<f32>, c : vec3<f32>) -> f32
{
	let ab = b - a;
	let ac = c - a;
	let ap = p - a;
	let d1 = dot(ab, ap);
	let d2 = dot(ac, ap);
	if (d1 <= 0.0 && d2 <= 0.0) { return dot(ap, ap); }
	let bp = p - b;
	let d3 = dot(ab, bp);
	let d4 = dot(ac, bp);
	if (d3 >= 0.0 && d4 <= d3) { return dot(bp, bp); }
	let vc = d1 * d4 - d3 * d2;
	if (vc <= 0.0 && d1 >= 0.0 && d3 <= 0.0)
	{
		let q = a + ab * (d1 / (d1 - d3));
		return dot(p - q, p - q);
	}
	let cp = p - c;
	let d5 = dot(ab, cp);
	let d6 = dot(ac, cp);
	if (d6 >= 0.0 && d5 <= d6) { return dot(cp, cp); }
	let vb = d5 * d2 - d1 * d6;
	if (vb <= 0.0 && d2 >= 0.0 && d6 <= 0.0)
	{
		let q = a + ac * (d2 / (d2 - d6));
		return dot(p - q, p - q);
	}
	let va = d3 * d6 - d5 * d4;
	if (va <= 0.0 && (d4 - d3) >= 0.0 && (d5 - d6) >= 0.0)
	{
		let q = b + (c - b) * ((d4 - d3) / ((d4 - d3) + (d5 - d6)));
		return dot(p - q, p - q);
	}
	// Inside the face: the plane distance, which f32 keeps far better than rebuilding the
	// closest point from barycentrics (a sliver's va + vb + vc cancels to noise).
	let n = cross(ab, ac);
	let h = dot(ap, n);
	return h * h / dot(n, n);
}

@compute @workgroup_size(4, 4, 4)
fn main(@builtin(global_invocation_id) id : vec3<u32>)
{
	if (id.x >= P.ni || id.y >= P.nj || id.z >= P.nk) { return; }
	let bucket = (id.x / P.s) + P.bi * ((id.y / P.s) + P.bj * (id.z / P.s));
	let p = vec3<f32>(f32(id.x), f32(id.y), f32(id.z)) * P.dx;
	let bandSq = P.band * P.band;
	var best = bandSq;
	let end = starts[bucket + 1u];
	for (var e = starts[bucket]; e < end; e = e + 1u)
	{
		let t = ids[e] * 9u;
		let a = vec3<f32>(tris[t], tris[t + 1u], tris[t + 2u]);
		let b = vec3<f32>(tris[t + 3u], tris[t + 4u], tris[t + 5u]);
		let c = vec3<f32>(tris[t + 6u], tris[t + 7u], tris[t + 8u]);
		best = min(best, closestSq(p, a, b, c));
	}
	dist[id.x + P.ni * (id.y + P.nj * id.z)] = select(P.cap, sqrt(best), best < bandSq);
}
";

		// The session's shared instance, made on first use (see GetSharedAsync). CreateAsync is the
		// plain constructor on the desktop and the Promise-driven path in the browser.
		private static readonly RetryingAsyncLazy<GpuMeshSdf> SharedInstance = new RetryingAsyncLazy<GpuMeshSdf>(
			async () => new GpuMeshSdf(await WebGpuRenderDevice.CreateAsync(null, "GpuMeshSdf", raiseComputeLimits: true)),
			gpu => gpu.IsDeviceLost);

		private readonly IRenderDevice device;

		// One build at a time drives the device: wgpu's queue is not safe to submit to from two
		// threads, and two concurrent builds would only share the GPU anyway.
		private readonly SemaphoreSlim deviceGate = new SemaphoreSlim(1, 1);
		private IShaderModule module;
		private IComputePipeline pipeline;

		/// <param name="device">A device the caller owns.</param>
		public GpuMeshSdf(IRenderDevice device)
		{
			this.device = device ?? throw new ArgumentNullException(nameof(device));
			device.RegisterShaderSources(new Sources());
		}

		/// <summary>Gets or sets whether a test has marked this instance's device as lost.</summary>
		internal bool SimulateDeviceLost { get; set; }

		/// <summary>Gets whether the device is gone, so the shared instance must be replaced.</summary>
		internal bool IsDeviceLost => SimulateDeviceLost || (device as WebGpuRenderDevice)?.IsDeviceLost == true;

		/// <summary>Gets or sets the bucket edge in samples.</summary>
		public int BucketVoxels { get; set; } = 8;

		/// <summary>Gets the stage times of the last <see cref="ComputeDistances"/>, in ms.</summary>
		public (double Bucket, double Upload, double Dispatch, double Readback, long Entries) LastTimings { get; private set; }

		/// <summary>
		/// The session's shared offscreen device and pipeline for distance work, created on the first
		/// call and reused after. agg renders through WebGPU on every native platform and in the
		/// browser, so this device always exists and has compute; there is no "no GPU" path by design.
		/// Safe from any thread: builds on it run their device work one at a time.
		/// </summary>
		/// <remarks>
		/// A failed creation is not kept: a GPU always exists, so the failure is a transient bug the
		/// caller reports, and the next call tries again. Nor is a lost device (a driver reset, sleep
		/// and resume, the browser's GPU process restarting): the next call makes a fresh one.
		/// </remarks>
		public static Task<GpuMeshSdf> GetSharedAsync() => SharedInstance.GetAsync();

		/// <summary>Matches <c>MatterHackers.PolygonMesh.Sdf.DistanceGridBuilder</c>.</summary>
		public Task<float[]> ComputeDistances(float[] triangles, int ni, int nj, int nk, float cellSize, float band, float cap)
			=> ComputeDistances(triangles, ni, nj, nk, cellSize, band, cap, null);

		/// <summary>
		/// <see cref="ComputeDistances(float[], int, int, int, float, float, float)"/>, calling
		/// <paramref name="stageDone"/> with 1 when the triangles are bucketed, 2 when the dispatch has
		/// finished and 3 when the distances are read back, so a caller can pace a progress bar across
		/// a pass it cannot otherwise watch. Safe to call from several threads; the device work runs
		/// one call at a time. The read-backs are awaited: in the browser they finish on a later turn of the JS event loop.
		/// </summary>
		public async Task<float[]> ComputeDistances(float[] triangles, int ni, int nj, int nk, float cellSize, float band, float cap, Action<int> stageDone)
		{
			var clock = Stopwatch.StartNew();
			int s = Math.Max(1, BucketVoxels);
			int bi = (ni + s - 1) / s, bj = (nj + s - 1) / s, bk = (nk + s - 1) / s;
			int bucketCount = bi * bj * bk;
			int triangleCount = triangles.Length / 9;
			double reach = band / cellSize;
			var counts = new int[bucketCount + 1];

			// Two passes over the triangles (count, then fill) so the lists land in one flat array.
			void Range(int t, out int i0, out int i1, out int j0, out int j1, out int k0, out int k1)
			{
				int o = t * 9;
				float minX = Math.Min(triangles[o], Math.Min(triangles[o + 3], triangles[o + 6]));
				float maxX = Math.Max(triangles[o], Math.Max(triangles[o + 3], triangles[o + 6]));
				float minY = Math.Min(triangles[o + 1], Math.Min(triangles[o + 4], triangles[o + 7]));
				float maxY = Math.Max(triangles[o + 1], Math.Max(triangles[o + 4], triangles[o + 7]));
				float minZ = Math.Min(triangles[o + 2], Math.Min(triangles[o + 5], triangles[o + 8]));
				float maxZ = Math.Max(triangles[o + 2], Math.Max(triangles[o + 5], triangles[o + 8]));
				i0 = Clamp((int)Math.Floor(((minX / cellSize) - reach) / s), bi);
				i1 = Clamp((int)Math.Floor(((maxX / cellSize) + reach) / s), bi);
				j0 = Clamp((int)Math.Floor(((minY / cellSize) - reach) / s), bj);
				j1 = Clamp((int)Math.Floor(((maxY / cellSize) + reach) / s), bj);
				k0 = Clamp((int)Math.Floor(((minZ / cellSize) - reach) / s), bk);
				k1 = Clamp((int)Math.Floor(((maxZ / cellSize) + reach) / s), bk);
			}

			Parallel.For(0, triangleCount, t =>
			{
				Range(t, out int i0, out int i1, out int j0, out int j1, out int k0, out int k1);
				for (int k = k0; k <= k1; k++)
				{
					for (int j = j0; j <= j1; j++)
					{
						for (int i = i0; i <= i1; i++)
						{
							Interlocked.Increment(ref counts[i + (bi * (j + (bj * k)))]);
						}
					}
				}
			});

			var starts = new uint[bucketCount + 1];
			long total = 0;
			for (int b = 0; b < bucketCount; b++)
			{
				starts[b] = (uint)total;
				total += counts[b];
				counts[b] = (int)starts[b];
			}

			starts[bucketCount] = (uint)total;
			if (total * 4 > (long)device.Limits.MaxStorageBufferBindingSize)
			{
				throw new InvalidOperationException($"{total:N0} bucket entries exceed the device's storage binding; raise BucketVoxels.");
			}

			var ids = new uint[Math.Max(1, total)];
			Parallel.For(0, triangleCount, t =>
			{
				Range(t, out int i0, out int i1, out int j0, out int j1, out int k0, out int k1);
				for (int k = k0; k <= k1; k++)
				{
					for (int j = j0; j <= j1; j++)
					{
						for (int i = i0; i <= i1; i++)
						{
							ids[Interlocked.Increment(ref counts[i + (bi * (j + (bj * k)))]) - 1] = (uint)t;
						}
					}
				}
			});

			double bucketMs = clock.Elapsed.TotalMilliseconds;
			stageDone?.Invoke(1);
			await deviceGate.WaitAsync();
			try
			{
				return await Dispatch(triangles, ni, nj, nk, cellSize, band, cap, s, bi, bj, bk, starts, ids, total, bucketMs, stageDone);
			}
			finally
			{
				deviceGate.Release();
			}
		}

		private async Task<float[]> Dispatch(float[] triangles, int ni, int nj, int nk, float cellSize, float band, float cap, int s, int bi, int bj, int bk, uint[] starts, uint[] ids, long total, double bucketMs, Action<int> stageDone)
		{
			var clock = Stopwatch.StartNew();
			EnsurePipeline();
			var parameters = new byte[48];
			var words = MemoryMarshal.Cast<byte, uint>(parameters.AsSpan());
			words[0] = (uint)ni;
			words[1] = (uint)nj;
			words[2] = (uint)nk;
			words[3] = (uint)s;
			words[4] = (uint)bi;
			words[5] = (uint)bj;
			words[6] = (uint)bk;
			var floats = MemoryMarshal.Cast<byte, float>(parameters.AsSpan());
			floats[8] = cellSize;
			floats[9] = band;
			floats[10] = cap;

			ulong outputBytes = (ulong)ni * (ulong)nj * (ulong)nk * sizeof(float);
			using var uniforms = device.CreateBuffer(BufferUsage.Uniform | BufferUsage.CopyDst, 48, parameters);
			using var triangleBuffer = device.CreateBuffer(BufferUsage.Storage, (ulong)Math.Max(36, triangles.Length * 4), MemoryMarshal.AsBytes(triangles.AsSpan()));
			using var startBuffer = device.CreateBuffer(BufferUsage.Storage, (ulong)starts.Length * 4, MemoryMarshal.AsBytes(starts.AsSpan()));
			using var idBuffer = device.CreateBuffer(BufferUsage.Storage, (ulong)ids.Length * 4, MemoryMarshal.AsBytes(ids.AsSpan()));
			using var output = device.CreateBuffer(BufferUsage.Storage | BufferUsage.CopySrc, outputBytes);
			using var bindGroup = device.CreateBindGroup(new BindGroupDescriptor(
				pipeline,
				0,
				new[]
				{
					BindGroupEntry.ForBuffer(0, uniforms),
					BindGroupEntry.ForBuffer(1, triangleBuffer),
					BindGroupEntry.ForBuffer(2, startBuffer),
					BindGroupEntry.ForBuffer(3, idBuffer),
					BindGroupEntry.ForBuffer(4, output),
				}));
			double uploadMs = clock.Elapsed.TotalMilliseconds;
			clock.Restart();

			using (var pass = device.BeginComputePass("GpuMeshSdf"))
			{
				pass.SetPipeline(pipeline);
				pass.SetBindGroup(0, bindGroup);
				pass.Dispatch((uint)((ni + 3) / 4), (uint)((nj + 3) / 4), (uint)((nk + 3) / 4));
			}

			// A 4-byte read waits for the dispatch, so the full read below times only the copy.
			await device.ReadBufferAsync(output, 0, new byte[4]);
			double dispatchMs = clock.Elapsed.TotalMilliseconds;
			stageDone?.Invoke(2);
			clock.Restart();

			var bytes = new byte[outputBytes];
			await device.ReadBufferAsync(output, 0, bytes);
			var result = new float[ni * nj * nk];
			Buffer.BlockCopy(bytes, 0, result, 0, bytes.Length);
			LastTimings = (bucketMs, uploadMs, dispatchMs, clock.Elapsed.TotalMilliseconds, total);
			stageDone?.Invoke(3);
			return result;
		}

		/// <inheritdoc/>
		public void Dispose()
		{
			pipeline?.Dispose();
			module?.Dispose();
		}

		private static int Clamp(int value, int count) => value < 0 ? 0 : (value >= count ? count - 1 : value);

		private void EnsurePipeline()
		{
			if (pipeline != null)
			{
				return;
			}

			module = device.CreateShaderModule(ShaderKey);
			pipeline = device.CreateComputePipeline(new ComputePipelineDescriptor(
				module,
				"main",
				new[]
				{
					new BindGroupLayoutEntry(0, 0, ShaderStage.Compute, BindingType.UniformBuffer),
					new BindGroupLayoutEntry(0, 1, ShaderStage.Compute, BindingType.ReadOnlyStorageBuffer),
					new BindGroupLayoutEntry(0, 2, ShaderStage.Compute, BindingType.ReadOnlyStorageBuffer),
					new BindGroupLayoutEntry(0, 3, ShaderStage.Compute, BindingType.ReadOnlyStorageBuffer),
					new BindGroupLayoutEntry(0, 4, ShaderStage.Compute, BindingType.StorageBuffer),
				},
				"GpuMeshSdf"));
		}

		private sealed class Sources : IShaderSourceProvider
		{
			public string TryGetSource(string sourceKey) => sourceKey == ShaderKey ? DistanceWgsl : null;
		}
	
		/// <summary>
		/// Creates a value once and shares it, like <see cref="Lazy{T}"/>, except that a failed or
		/// cancelled creation, or a value <c>isStale</c> says has gone bad, is dropped so the next
		/// <see cref="GetAsync"/> starts a new one. Callers that arrive while a creation is running share it.
		/// </summary>
		internal sealed class RetryingAsyncLazy<T>
			where T : class
		{
			private readonly Func<Task<T>> create;
			private readonly Func<T, bool> isStale;
			private readonly object gate = new object();
			private Task<T> current;

			// The created value, kept beside its task so it can be checked without reading the task's result.
			private T created;

			public RetryingAsyncLazy(Func<Task<T>> create, Func<T, bool> isStale = null)
			{
				this.create = create;
				this.isStale = isStale;
			}

			public Task<T> GetAsync()
			{
				lock (gate)
				{
					var value = Volatile.Read(ref created);
					if (current == null || current.IsFaulted || current.IsCanceled
						|| (value != null && isStale?.Invoke(value) == true))
					{
						created = null;
						current = Start();
					}

					return current;
				}
			}

			// Async so a factory that throws synchronously still yields a faulted task, not a throw under the lock.
			private async Task<T> Start()
			{
				var value = await create();
				Volatile.Write(ref created, value);
				return value;
			}
		}
	}
}
