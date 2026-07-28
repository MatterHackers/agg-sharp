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
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.VertexSource;
using MatterHackers.PolygonMesh;
using MatterHackers.RenderGl;
using MatterHackers.RenderGl.OpenGl;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests
{
	/// <summary>
	/// MatterCAD renders thumbnails on background worker threads that own their own GL context while
	/// the UI thread paints. Anything cached process wide that holds a captured <see cref="GL"/> will,
	/// when handed to the other context, pump immediate mode vertices into the wrong context's vertex
	/// buffer and corrupt an in-flight flush. These tests pin the GL caches to the context they were
	/// created for.
	/// </summary>
	[NotInParallel]
	public class GlContextIsolationTests
	{
		[Test]
		public async Task ImageTexturePluginIsPerGlContext()
		{
			// The plugin cache is keyed off the image's pixel buffer, so every test needs its own image.
			var image = new ImageBuffer(16, 16, 32, new BlenderBGRA());
			image.SetPixel(4, 4, Color.Red);

			var fakeA = new RecordingGpuContext(idBase: 1000);
			var fakeB = new RecordingGpuContext(idBase: 2000);
			var glA = new GL(fakeA);
			var glB = new GL(fakeB);

			var pluginA = ImageTexturePlugin.GetImageTexturePlugin(glA, image, false);
			var pluginB = ImageTexturePlugin.GetImageTexturePlugin(glB, image, false);

			// The texture handle handed back for glB has to have been minted by glB - a handle from
			// another context names a completely different (or non existent) texture.
			await Assert.That(fakeB.GeneratedTextures.Contains(pluginB.GLTextureHandle))
				.IsTrue()
				.Because("the plugin returned for glB must own a texture created on glB");
			await Assert.That(pluginA.GLTextureHandle).IsNotEqualTo(pluginB.GLTextureHandle);

			fakeA.ResetCallRecording();
			fakeB.ResetCallRecording();

			pluginB.DrawToGL();

			await Assert.That(fakeB.BeginCount).IsEqualTo(1);
			await Assert.That(fakeB.EndCount).IsEqualTo(1);
			await Assert.That(fakeB.Vertex2Count).IsEqualTo(4);
			await Assert.That(fakeB.BoundTextures.Contains(pluginB.GLTextureHandle)).IsTrue();

			await Assert.That(fakeA.GotImmediateModeCalls)
				.IsFalse()
				.Because("drawing a plugin fetched for glB must not push vertices into glA's context");
		}

		[Test]
		public async Task ImageTexturePluginReusesPluginForSameContext()
		{
			var image = new ImageBuffer(16, 16, 32, new BlenderBGRA());
			image.SetPixel(2, 2, Color.Blue);

			var fake = new RecordingGpuContext(idBase: 3000);
			var gl = new GL(fake);

			var first = ImageTexturePlugin.GetImageTexturePlugin(gl, image, false);
			var second = ImageTexturePlugin.GetImageTexturePlugin(gl, image, false);

			await Assert.That(ReferenceEquals(first, second))
				.IsTrue()
				.Because("asking the same context for the same image twice must not build a second texture");
			await Assert.That(fake.GeneratedTextures.Count).IsEqualTo(1);
		}

		[Test]
		public async Task ChangedImageOnlyRebuildsTheTextureForTheFetchingContext()
		{
			var image = new ImageBuffer(16, 16, 32, new BlenderBGRA());
			image.SetPixel(8, 8, Color.Green);

			var fakeA = new RecordingGpuContext(idBase: 11000);
			var fakeB = new RecordingGpuContext(idBase: 12000);
			var glA = new GL(fakeA);
			var glB = new GL(fakeB);

			var firstA = ImageTexturePlugin.GetImageTexturePlugin(glA, image, false);
			var pluginB = ImageTexturePlugin.GetImageTexturePlugin(glB, image, false);
			var handleBBeforeChange = pluginB.GLTextureHandle;

			// Only the context that comes back for the image can upload the new pixels - the other
			// context's texture has to be left alone, because only its own thread may touch it.
			image.MarkImageChanged();

			var secondA = ImageTexturePlugin.GetImageTexturePlugin(glA, image, false);

			await Assert.That(ReferenceEquals(firstA, secondA))
				.IsFalse()
				.Because("the changed image must be re-uploaded for glA");
			await Assert.That(fakeA.GeneratedTextures.Count)
				.IsEqualTo(2)
				.Because("glA should have minted a second texture for the changed image");
			await Assert.That(fakeA.GeneratedTextures.Contains(secondA.GLTextureHandle)).IsTrue();

			await Assert.That(fakeB.GeneratedTextures.Count)
				.IsEqualTo(1)
				.Because("glB was never asked for the image again, so it must not have created anything");
			await Assert.That(pluginB.GLTextureHandle)
				.IsEqualTo(handleBBeforeChange)
				.Because("glA refetching must not have disturbed glB's existing plugin");

			// glB picks the change up the next time it asks, on its own thread.
			var secondB = ImageTexturePlugin.GetImageTexturePlugin(glB, image, false);
			await Assert.That(ReferenceEquals(pluginB, secondB)).IsFalse();
			await Assert.That(fakeB.GeneratedTextures.Contains(secondB.GLTextureHandle)).IsTrue();
		}

		[Test]
		public async Task InvalidateGlCachesRecompilesDisplayListsOnTheOwningContext()
		{
			Graphics2DGpu.InvalidateGlCaches();

			var fake = new RecordingGpuContext(idBase: 13000);
			var graphics = new Graphics2DGpu(new GL(fake), 100, 100, 1);

			graphics.Render(new Ellipse(50, 50, 20, 20), Color.Red);

			var listsFromFirstRender = fake.CompiledLists.ToList();
			await Assert.That(listsFromFirstRender.Count)
				.IsGreaterThan(0)
				.Because("the first render has to compile the shape into a display list");

			// Nothing was invalidated, so a repeat of the same shape replays the cached list.
			graphics.Render(new Ellipse(50, 50, 20, 20), Color.Red);
			await Assert.That(fake.CompiledLists.Count)
				.IsEqualTo(listsFromFirstRender.Count)
				.Because("an unchanged cache should replay, not recompile");

			Graphics2DGpu.InvalidateGlCaches();
			fake.ResetCallRecording();

			graphics.Render(new Ellipse(50, 50, 20, 20), Color.Red);

			await Assert.That(fake.CompiledLists.Count)
				.IsGreaterThan(listsFromFirstRender.Count)
				.Because("after invalidation the shape must be recompiled rather than replayed from a stale list id");
			await Assert.That(fake.CalledLists.Any(id => listsFromFirstRender.Contains(id)))
				.IsFalse()
				.Because("the display list ids from before the invalidation no longer name anything");
			await Assert.That(listsFromFirstRender.All(id => fake.DeletedLists.Contains(id)))
				.IsTrue()
				.Because("the stale lists have to be freed by the context that minted them, on its own thread");
		}

		[Test]
		public async Task Graphics2DGpuTesselatesIntoItsOwnContext()
		{
			// Start from a known empty cache state so the first Graphics2DGpu below is the one that
			// seeds the tesselator pool - that is the situation the crash comes from.
			Graphics2DGpu.InvalidateGlCaches();

			var fakeA = new RecordingGpuContext(idBase: 4000);
			var fakeB = new RecordingGpuContext(idBase: 5000);
			var graphicsA = new Graphics2DGpu(new GL(fakeA), 100, 100, 1);
			var graphicsB = new Graphics2DGpu(new GL(fakeB), 100, 100, 1);

			fakeA.ResetCallRecording();
			fakeB.ResetCallRecording();

			graphicsB.Render(new Ellipse(50, 50, 20, 20), Color.Red);

			await Assert.That(fakeB.GotImmediateModeCalls)
				.IsTrue()
				.Because("the shape has to actually be tesselated into glB");

			await Assert.That(fakeA.GotImmediateModeCalls)
				.IsFalse()
				.Because("rendering through glB's Graphics2DGpu must not emit geometry into glA's context");
		}

		[Test]
		public async Task Graphics2DGpuDisplayListsAreNotSharedAcrossContexts()
		{
			Graphics2DGpu.InvalidateGlCaches();

			var fakeA = new RecordingGpuContext(idBase: 6000);
			var fakeB = new RecordingGpuContext(idBase: 7000);
			var graphicsA = new Graphics2DGpu(new GL(fakeA), 100, 100, 1);
			var graphicsB = new Graphics2DGpu(new GL(fakeB), 100, 100, 1);

			// Same geometry and color on both contexts - the display list cache key is identical.
			graphicsA.Render(new Ellipse(50, 50, 20, 20), Color.Red);

			fakeA.ResetCallRecording();
			fakeB.ResetCallRecording();

			graphicsB.Render(new Ellipse(50, 50, 20, 20), Color.Red);

			await Assert.That(fakeB.CalledLists.Count)
				.IsGreaterThan(0)
				.Because("glB should be replaying a display list of its own");

			await Assert.That(fakeB.CalledLists.All(id => fakeB.CompiledLists.Contains(id)))
				.IsTrue()
				.Because("a display list id compiled on glA names nothing (or the wrong geometry) on glB");
		}

		[Test]
		public async Task MeshTrianglePluginIsPerGlContext()
		{
			var mesh = MakeTexturedCube(out var texture);

			var fakeA = new RecordingGpuContext(idBase: 20000);
			var fakeB = new RecordingGpuContext(idBase: 21000);
			var glA = new GL(fakeA);
			var glB = new GL(fakeB);

			var pluginA = MeshTrianglePlugin.Get(glA, mesh);
			var pluginB = MeshTrianglePlugin.Get(glB, mesh);

			// The submeshes carry renderer minted gpu buffers (CachedGpuBuffer), which name a device
			// resource on exactly one context.
			await Assert.That(ReferenceEquals(pluginA, pluginB))
				.IsFalse()
				.Because("each context has to get its own plugin, not the one the other context built");

			await Assert.That(ReferenceEquals(pluginA, MeshTrianglePlugin.Get(glA, mesh)))
				.IsTrue()
				.Because("asking the same context for the same unchanged mesh twice must not re-tesselate it");

			// Building the render data for glB has to bind the face texture through glB, so the handle
			// the draw will use must have been minted by glB.
			var textureForB = ImageTexturePlugin.GetImageTexturePlugin(glB, texture, true);
			await Assert.That(fakeB.GeneratedTextures.Contains(textureForB.GLTextureHandle))
				.IsTrue()
				.Because("the face texture glB draws with must have been created on glB");
			await Assert.That(pluginB.subMeshs.Any(subMesh => ReferenceEquals(subMesh.texture, texture)))
				.IsTrue()
				.Because("the textured face has to end up in a submesh that references the texture");
		}

		[Test]
		public async Task ChangedMeshOnlyRebuildsForTheFetchingContext()
		{
			var mesh = MakeTexturedCube(out _);

			var fakeA = new RecordingGpuContext(idBase: 22000);
			var fakeB = new RecordingGpuContext(idBase: 23000);
			var glA = new GL(fakeA);
			var glB = new GL(fakeB);

			var firstA = MeshTrianglePlugin.Get(glA, mesh);
			var pluginB = MeshTrianglePlugin.Get(glB, mesh);
			var subMeshCountBBeforeChange = pluginB.subMeshs.Count;

			// Only the context that comes back for the mesh may rebuild - the other context's plugin
			// owns gpu buffers that only its own thread may touch.
			mesh.MarkAsChanged();

			var secondA = MeshTrianglePlugin.Get(glA, mesh);

			await Assert.That(ReferenceEquals(firstA, secondA))
				.IsFalse()
				.Because("the changed mesh must be re-tesselated for glA");
			await Assert.That(pluginB.subMeshs)
				.IsNotNull()
				.Because("glA rebuilding must not have torn down glB's plugin");
			await Assert.That(pluginB.subMeshs.Count)
				.IsEqualTo(subMeshCountBBeforeChange)
				.Because("glB is still rendering from this plugin until it refetches on its own thread");

			// glB picks the change up the next time it asks, and gets render data of its own.
			var secondB = MeshTrianglePlugin.Get(glB, mesh);
			await Assert.That(ReferenceEquals(pluginB, secondB))
				.IsFalse()
				.Because("glB has to notice the mesh change on its own next fetch");
			await Assert.That(ReferenceEquals(secondA, secondB))
				.IsFalse()
				.Because("glA's rebuild must not have become glB's plugin as well");
		}

		[Test]
		public async Task ConcurrentMeshTrianglePluginFetchesStayConsistent()
		{
			// Untextured so this stays pure cpu work and runs in milliseconds.
			var mesh = PlatonicSolids.CreateCube(10, 10, 10);

			var glA = new GL(new RecordingGpuContext(idBase: 24000));
			var glB = new GL(new RecordingGpuContext(idBase: 25000));

			var failures = new List<Exception>();

			void FetchRepeatedly(GL gl, bool alsoChangeTheMesh)
			{
				try
				{
					for (int i = 0; i < 200; i++)
					{
						var plugin = MeshTrianglePlugin.Get(gl, mesh);

						// A plugin must never be published before its render data is complete.
						if (plugin.subMeshs == null || plugin.subMeshs.Count == 0)
						{
							throw new Exception("a plugin was handed out before its render data was built");
						}

						foreach (var subMesh in plugin.subMeshs)
						{
							if (subMesh.interleavedData == null)
							{
								throw new Exception("a submesh was handed out before it was interleaved");
							}
						}

						if (alsoChangeTheMesh && (i % 10) == 0)
						{
							mesh.MarkAsChanged();
						}
					}
				}
				catch (Exception e)
				{
					lock (failures)
					{
						failures.Add(e);
					}
				}
			}

			var threadA = new Thread(() => FetchRepeatedly(glA, true));
			var threadB = new Thread(() => FetchRepeatedly(glB, false));

			threadA.Start();
			threadB.Start();
			threadA.Join();
			threadB.Join();

			await Assert.That(failures.Count)
				.IsEqualTo(0)
				.Because("two contexts fetching the same changing mesh must not corrupt the cache: "
					+ string.Join(", ", failures.Select(e => e.Message)));
		}

		[Test]
		public async Task RenderPathTesselatesIntoTheRenderingContext()
		{
			var world = new WorldView(100, 100);

			var fakeA = new RecordingGpuContext(idBase: 26000);
			var fakeB = new RecordingGpuContext(idBase: 27000);
			var glA = new GL(fakeA);
			var glB = new GL(fakeB);

			world.RenderPath(glA, new Ellipse(50, 50, 20, 20), Color.Red, false);

			fakeA.ResetCallRecording();
			fakeB.ResetCallRecording();

			world.RenderPath(glB, new Ellipse(50, 50, 20, 20), Color.Red, false);

			await Assert.That(fakeB.GotImmediateModeCalls)
				.IsTrue()
				.Because("a tesselator cached for this world must emit into the context currently rendering it");
			await Assert.That(fakeA.GotImmediateModeCalls)
				.IsFalse()
				.Because("the tesselator must not emit into the context that first populated the cache");
		}

		/// <summary>
		/// A cube with one textured face, so building its render data also exercises the per face
		/// texture path of CreateRenderData.
		/// </summary>
		private static Mesh MakeTexturedCube(out ImageBuffer texture)
		{
			var mesh = PlatonicSolids.CreateCube(10, 10, 10);

			// The image texture cache is keyed off the pixel buffer, so every test needs its own image.
			texture = new ImageBuffer(16, 16, 32, new BlenderBGRA());
			texture.SetPixel(1, 1, Color.Yellow);

			mesh.PlaceTextureOnFace(0, texture);

			return mesh;
		}
	}
}
