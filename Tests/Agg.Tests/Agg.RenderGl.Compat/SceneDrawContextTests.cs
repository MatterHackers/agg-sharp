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
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.PolygonMesh;
using MatterHackers.RenderCore;
using MatterHackers.RenderCore.Testing;
using MatterHackers.RenderGl;
using MatterHackers.RenderGl.OpenGl;
using MatterHackers.RenderGl.Scene;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests
{
	/// <summary>
	/// What <see cref="ISceneDrawContext"/> members turn into: mesh draws become
	/// <see cref="MeshRenderCommand"/>s the <see cref="NativeSceneRenderPlanner"/> sorts, the escape hatch
	/// becomes one immediate-mode draw of the asked-for topology, and the frame members open and close
	/// exactly one scene pass.
	/// </summary>
	/// <remarks>
	/// Asserted against the command stream rather than pixels: the picture is already pinned by the
	/// golden suite (see <c>GoldenSceneTests.GizmoOverlayThroughDrawContext</c>), and what these need to
	/// prove is the shape of what the seam queues.
	/// </remarks>
	public class SceneDrawContextTests
	{
		[Test]
		public async Task MeshDrawsBecomePlannerCommands()
		{
			var harness = Harness.Create();

			var opaque = PlatonicSolids.CreateCube(10, 10, 10);
			var transparent = PlatonicSolids.CreateCube(4, 4, 4);
			var opaqueTransform = Matrix4X4.CreateTranslation(3, 4, 5);

			harness.Context.BeginFrame(harness.World, harness.Viewport, new LightingData());
			harness.Context.DrawMesh(opaque, Color.Red, opaqueTransform, RenderTypes.Outlines);
			harness.Context.DrawMesh(transparent, new Color(Color.Blue, 120), Matrix4X4.Identity);
			harness.Context.EndFrame();

			await Assert.That(harness.Renderer.MeshCommands.Count).IsEqualTo(2);

			var plan = new NativeSceneRenderPlanner().Build(harness.Renderer.MeshCommands);

			await Assert.That(plan.OpaqueCommands.Count).IsEqualTo(1);
			await Assert.That(plan.TransparentCommands.Count).IsEqualTo(1);

			var opaqueCommand = plan.OpaqueCommands.Single();
			await Assert.That(opaqueCommand.Mesh).IsSameReferenceAs(opaque);
			await Assert.That(opaqueCommand.Color).IsEqualTo(Color.Red);
			await Assert.That(opaqueCommand.RenderType).IsEqualTo(RenderTypes.Outlines);
			await Assert.That(opaqueCommand.Transform).IsEqualTo(opaqueTransform);

			await Assert.That(plan.TransparentCommands.Single().Mesh).IsSameReferenceAs(transparent);
		}

		/// <summary>
		/// A screen-width line is a mesh once a scene pass is open - it is baked into world space and
		/// queued like any other geometry rather than drawn as immediate-mode triangles.
		/// </summary>
		[Test]
		public async Task Render3DLineQueuesABakedLineMesh()
		{
			var harness = Harness.Create();

			harness.Context.BeginFrame(harness.World, harness.Viewport, new LightingData());
			harness.Context.Render3DLine(new Vector3(0, 0, 0), new Vector3(0, 0, 20), Color.Green, doDepthTest: true, width: 2);
			harness.Context.EndFrame();

			var command = harness.Renderer.MeshCommands.Single();

			await Assert.That(command.Color).IsEqualTo(Color.Green);
			await Assert.That(command.RenderType).IsEqualTo(RenderTypes.Shaded);

			// Baked into world space, so the command carries no transform of its own.
			await Assert.That(command.Transform).IsEqualTo(Matrix4X4.Identity);

			// A box with no arrow heads: the eight corners of the cube the line is built from.
			await Assert.That(command.Mesh.Vertices.Count).IsEqualTo(8);
		}

		[Test]
		public async Task DrawPrimitivesEmitsOneStripDraw()
		{
			var harness = Harness.Create();

			var vertices = new[]
			{
				new PosColorVertex(new Vector2(0, 0), Color.Red),
				new PosColorVertex(new Vector2(10, 0), Color.Green),
				new PosColorVertex(new Vector2(0, 10), Color.Blue),
				new PosColorVertex(new Vector2(10, 10), Color.White),
			};

			harness.Context.DrawPrimitives(DrawTopology.TriangleStrip, vertices, Matrix4X4.Identity, depthTest: false);
			harness.Compat.Context.Submit();

			await Assert.That(harness.Compat.Device.CommandsOf<DrawCommand>().Single().VertexCount).IsEqualTo(4);
			await Assert.That(harness.Compat.BoundPipelines().Single().Descriptor.Topology)
				.IsEqualTo(PrimitiveTopology.TriangleStrip);
		}

		[Test]
		public async Task DrawPrimitivesEmitsOneLineListDraw()
		{
			var harness = Harness.Create();

			var vertices = new[]
			{
				new PosColorVertex(new Vector3(0, 0, 1), Color.Red),
				new PosColorVertex(new Vector3(10, 0, 1), Color.Red),
				new PosColorVertex(new Vector3(10, 0, 1), Color.Red),
				new PosColorVertex(new Vector3(10, 10, 1), Color.Red),
			};

			harness.Context.DrawPrimitives(DrawTopology.LineList, vertices, Matrix4X4.CreateTranslation(1, 2, 3), depthTest: true);
			harness.Compat.Context.Submit();

			await Assert.That(harness.Compat.Device.CommandsOf<DrawCommand>().Single().VertexCount).IsEqualTo(4);
			await Assert.That(harness.Compat.BoundPipelines().Single().Descriptor.Topology)
				.IsEqualTo(PrimitiveTopology.LineList);
		}

		/// <summary>An empty run is not a draw of nothing, it is no draw at all - and no state change.</summary>
		[Test]
		public async Task DrawPrimitivesWithNoVerticesDrawsNothing()
		{
			var harness = Harness.Create();

			harness.Context.DrawPrimitives(DrawTopology.LineList, ReadOnlySpan<PosColorVertex>.Empty, Matrix4X4.Identity, depthTest: true);
			harness.Compat.Context.Submit();

			await Assert.That(harness.Compat.Device.CommandsOf<DrawCommand>().Count).IsEqualTo(0);
		}

		/// <summary>
		/// The discipline the frame members exist to enforce: one scene pass per frame, closable early for
		/// the overlays that have to land after the composite, and closed by <c>EndFrame</c> either way.
		/// </summary>
		[Test]
		public async Task AFrameOpensAndClosesExactlyOneScenePass()
		{
			var harness = Harness.Create();

			await Assert.That(harness.Context.IsFrameOpen).IsFalse();

			harness.Context.BeginFrame(harness.World, harness.Viewport, new LightingData());

			await Assert.That(harness.Context.IsFrameOpen).IsTrue();
			await Assert.That(harness.Context.IsSceneRenderingActive).IsTrue();
			await Assert.That(harness.Renderer.BeginCount).IsEqualTo(1);

			harness.Context.EndScenePass();

			// The frame is still open - its camera and lighting are installed - but queued geometry has
			// been flushed, which is what lets a path overlay draw over the composited scene.
			await Assert.That(harness.Context.IsFrameOpen).IsTrue();
			await Assert.That(harness.Context.IsSceneRenderingActive).IsFalse();
			await Assert.That(harness.Renderer.EndCount).IsEqualTo(1);

			harness.Context.EndScenePass();
			await Assert.That(harness.Renderer.EndCount).IsEqualTo(1);

			harness.Context.EndFrame();

			await Assert.That(harness.Context.IsFrameOpen).IsFalse();
			await Assert.That(harness.Renderer.EndCount).IsEqualTo(1);
			await Assert.That(harness.Renderer.BeginCount).IsEqualTo(1);

			// Idempotent, so a finally block can call it without knowing whether the frame ever opened.
			harness.Context.EndFrame();
			await Assert.That(harness.Renderer.EndCount).IsEqualTo(1);
		}

		[Test]
		public async Task EndFrameClosesAScenePassLeftOpen()
		{
			var harness = Harness.Create();

			harness.Context.BeginFrame(harness.World, harness.Viewport, new LightingData());
			harness.Context.EndFrame();

			await Assert.That(harness.Renderer.EndCount).IsEqualTo(1);
			await Assert.That(harness.Context.IsSceneRenderingActive).IsFalse();
		}

		/// <summary>
		/// A widget that draws its own sub-view (the tumble cube, the logo spinner) opens a frame with its
		/// own camera; the world the context had before has to come back, or the enclosing view's helpers
		/// would carry on with the wrong one.
		/// </summary>
		[Test]
		public async Task EndFrameRestoresTheWorldFromBeforeIt()
		{
			var harness = Harness.Create();

			var outerWorld = harness.World;
			var context = new SceneDrawContext(harness.Gl, outerWorld);

			var innerWorld = new WorldView(64, 64);
			innerWorld.Reset();

			context.BeginFrame(innerWorld, new RectangleDouble(0, 0, 64, 64), new LightingData());
			await Assert.That(context.World).IsSameReferenceAs(innerWorld);

			context.EndFrame();
			await Assert.That(context.World).IsSameReferenceAs(outerWorld);
		}

		[Test]
		public async Task ANestedFrameIsRejected()
		{
			var harness = Harness.Create();

			harness.Context.BeginFrame(harness.World, harness.Viewport, new LightingData());

			await Assert.That(() => harness.Context.BeginFrame(harness.World, harness.Viewport, new LightingData()))
				.Throws<InvalidOperationException>();

			harness.Context.EndFrame();
		}

		/// <summary>
		/// The ghost pass, end to end: geometry drawn inside <see cref="SceneDrawContext.SuppressDepthTest"/>
		/// is routed to the always-visible overlay pass rather than into the scene, which is how a 3D control
		/// handle shows through the part it is attached to.
		/// </summary>
		/// <remarks>
		/// Driven through the real <c>WebGpuSceneRenderer</c> because the routing decision lives in its
		/// <c>TryRender</c>, reading the compat layer's depth enable bit - the only thing connecting the
		/// context member to the queue. Known hole, inherited from the GL pair and not fixed here: the
		/// helpers that call <c>PrepareFor3DLineRender(doDepthTest: true)</c> re-enable the depth test inside
		/// a suppression scope, so a line helper called in there lands in the scene queue after all.
		/// </remarks>
		[Test]
		public async Task SuppressDepthTestRoutesAMeshToTheOverlayPass()
		{
			using var harness = GhostPassHarness.Create();

			harness.DrawOneMesh(suppressDepthTest: true);

			await Assert.That(harness.MeshDrawPassLabels().Distinct().ToList())
				.IsEquivalentTo(new[] { "SceneOverlay" });
		}

		[Test]
		public async Task AMeshDrawnOutsideSuppressDepthTestGoesToTheScenePasses()
		{
			using var harness = GhostPassHarness.Create();

			harness.DrawOneMesh(suppressDepthTest: false);

			var labels = harness.MeshDrawPassLabels().Distinct().ToList();

			await Assert.That(labels).Contains("SceneOpaque");
			await Assert.That(labels).DoesNotContain("SceneOverlay");
		}

		/// <summary>
		/// A <see cref="SceneDrawContext"/> over a <see cref="GlCompatTestHarness"/>, with a scene renderer
		/// that records what it is handed instead of drawing it.
		/// </summary>
		private sealed class Harness
		{
			private Harness(GlCompatTestHarness compat, GL gl, RecordingSceneRenderer renderer, SceneDrawContext context)
			{
				this.Compat = compat;
				this.Gl = gl;
				this.Renderer = renderer;
				this.Context = context;
			}

			public GlCompatTestHarness Compat { get; }

			public GL Gl { get; }

			public RecordingSceneRenderer Renderer { get; }

			public SceneDrawContext Context { get; }

			public RectangleDouble Viewport => new RectangleDouble(0, 0, 100, 50);

			public WorldView World { get; private set; }

			public static Harness Create()
			{
				var compat = GlCompatTestHarness.Create(withDepth: true);
				var renderer = new RecordingSceneRenderer();
				compat.Context.SceneRenderer = renderer;

				var gl = new GL(compat.Context);

				var world = new WorldView(100, 50);
				world.Reset();

				var harness = new Harness(compat, gl, renderer, new SceneDrawContext(gl, world))
				{
					World = world,
				};

				compat.Device.ClearRecording();

				return harness;
			}
		}

		/// <summary>
		/// A <see cref="SceneDrawContext"/> over the real <c>WebGpuSceneRenderer</c> and a recording device,
		/// so a draw can be followed all the way to the pass it was encoded into.
		/// </summary>
		private sealed class GhostPassHarness : IDisposable
		{
			private const int Width = 80;

			private const int Height = 60;

			private GhostPassHarness(GlCompatTestHarness compat, WebGpuSceneRenderer renderer, WorldView world, SceneDrawContext context)
			{
				this.compat = compat;
				this.renderer = renderer;
				this.world = world;
				this.context = context;
			}

			private readonly GlCompatTestHarness compat;

			private readonly WebGpuSceneRenderer renderer;

			private readonly WorldView world;

			private readonly SceneDrawContext context;

			public static GhostPassHarness Create()
			{
				var compat = GlCompatTestHarness.Create(Width, Height, withDepth: true);
				var gl = new GL(compat.Context);
				var renderer = new WebGpuSceneRenderer(compat.Context) { OwnerGl = gl };
				compat.Context.SceneRenderer = renderer;

				var world = new WorldView(Width, Height);
				world.Reset();

				compat.Device.ClearRecording();

				return new GhostPassHarness(compat, renderer, world, new SceneDrawContext(gl, world));
			}

			/// <summary>Draws one opaque cube through a whole frame, optionally inside a suppression scope.</summary>
			/// <param name="suppressDepthTest">Whether to draw it as a ghost.</param>
			public void DrawOneMesh(bool suppressDepthTest)
			{
				this.context.BeginFrame(this.world, new RectangleDouble(0, 0, Width, Height), new LightingData());

				try
				{
					if (suppressDepthTest)
					{
						using (this.context.SuppressDepthTest())
						{
							this.context.DrawMesh(PlatonicSolids.CreateCube(10, 10, 10), Color.Red, Matrix4X4.Identity);
						}
					}
					else
					{
						this.context.DrawMesh(PlatonicSolids.CreateCube(10, 10, 10), Color.Red, Matrix4X4.Identity);
					}
				}
				finally
				{
					this.context.EndFrame();
				}
			}

			/// <summary>
			/// The label of the pass each mesh draw was encoded into, in order. Mesh geometry is told apart
			/// from uniform and immediate-mode buffers by usage: only the scene's own vertex buffers are
			/// created write-once as <see cref="BufferUsage.Vertex"/> alone.
			/// </summary>
			public IEnumerable<string> MeshDrawPassLabels()
			{
				var meshBuffers = new HashSet<IGpuBuffer>(
					this.compat.Device.CommandsOf<CreateBufferCommand>()
						.Where(command => command.Usage == BufferUsage.Vertex)
						.Select(command => command.Buffer));

				var bound = new Dictionary<RecordingRenderEncoder, IGpuBuffer>();
				var labels = new List<string>();

				foreach (var command in this.compat.Device.Commands)
				{
					switch (command)
					{
						case SetVertexBufferCommand setVertexBuffer:
							bound[setVertexBuffer.Encoder] = setVertexBuffer.Buffer;
							break;

						case DrawCommand draw
							when bound.TryGetValue(draw.Encoder, out var buffer) && meshBuffers.Contains(buffer):
							labels.Add(draw.Encoder.Descriptor.Label);
							break;
					}
				}

				return labels;
			}

			public void Dispose()
			{
				this.renderer.Dispose();
				this.compat.Context.Dispose();
				this.compat.Device.Dispose();
			}
		}

		/// <summary>
		/// An <see cref="INativeSceneRenderer"/> that accepts everything and records it. Standing in for
		/// <c>WebGpuSceneRenderer</c> keeps these tests about what the seam queues rather than about how
		/// the compositor draws it, which the golden suite already covers.
		/// </summary>
		private sealed class RecordingSceneRenderer : INativeSceneRenderer
		{
			public List<MeshRenderCommand> MeshCommands { get; } = new List<MeshRenderCommand>();

			public int BeginCount { get; private set; }

			public int EndCount { get; private set; }

			public bool IsSceneRenderingActive { get; private set; }

			public void BeginSceneRendering(SceneRenderContext context)
			{
				this.BeginCount++;
				this.IsSceneRenderingActive = true;
			}

			public void EndSceneRendering()
			{
				this.EndCount++;
				this.IsSceneRenderingActive = false;
			}

			public bool CanRender(MeshRenderCommand command) => this.IsSceneRenderingActive && command?.Mesh != null;

			public bool TryRender(MeshRenderCommand command)
			{
				if (!this.CanRender(command))
				{
					return false;
				}

				this.MeshCommands.Add(command);
				return true;
			}

			public bool TryRender(BedRenderCommand command) => false;

			public void QueueSelectionOutline(Mesh mesh, Color color, Matrix4X4 transform)
			{
			}

			public void BeginFullFrameCapture(RectangleDouble viewport)
			{
			}

			public void EndFullFrameCapture()
			{
			}

			public void DownsampleAndBlitFullFrame()
			{
			}
		}
	}
}
