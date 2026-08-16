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
using MatterHackers.RenderCore;
using MatterHackers.RenderCore.Testing;
using MatterHackers.RenderGl.Compat;
using MatterHackers.RenderGl.OpenGl;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests
{
	/// <summary>
	/// What one <c>glBegin</c>/<c>glEnd</c> batch turns into on the retained seam: a vertex buffer, a
	/// uniform write, a cached pipeline and bind group, and a draw inside a lazily opened pass. These
	/// assert the command stream rather than pixels, which is the whole point of
	/// <see cref="RecordingRenderDevice"/> - the emulation semantics can be pinned before any GPU code
	/// exists.
	/// </summary>
	public class GlCompatImmediateModeTests
	{
		[Test]
		public async Task AColoredQuadBecomesOneVertexBufferAndOneDraw()
		{
			var harness = GlCompatTestHarness.Create();

			harness.Context.Color4(255, 0, 0, 255);
			harness.Context.Begin(BeginMode.TriangleStrip);
			harness.Context.Vertex2(0, 0);
			harness.Context.Vertex2(10, 0);
			harness.Context.Vertex2(0, 10);
			harness.Context.Vertex2(10, 10);
			harness.Context.End();
			harness.Context.FlushPass();

			var vertexBuffers = harness.VertexBufferCreations();
			await Assert.That(vertexBuffers.Count).IsEqualTo(1);

			// The buffer comes from the pool, so it is written rather than created with its contents and
			// its capacity is rounded up - what has to be exact is the 4 vertices at the position+color
			// stride the canned layout declares.
			int expectedBytes = 4 * (int)GlShaderKeys.ColoredVertexLayout.ArrayStride;
			await Assert.That(vertexBuffers[0].InitialDataLength).IsEqualTo(0);
			await Assert.That(vertexBuffers[0].SizeInBytes >= (ulong)expectedBytes).IsTrue();

			var vertexWrite = harness.VertexWrites().Single();
			await Assert.That(vertexWrite.Data.Length).IsEqualTo(expectedBytes);
			await Assert.That(vertexWrite.Offset).IsEqualTo(0ul);

			var draws = harness.Device.CommandsOf<DrawCommand>();
			await Assert.That(draws.Count).IsEqualTo(1);
			await Assert.That(draws[0].VertexCount).IsEqualTo(4);

			var pipeline = harness.BoundPipelines().Single();
			await Assert.That(pipeline.Descriptor.VertexShader.SourceKey).IsEqualTo(GlShaderKeys.PositionColor);
			await Assert.That(pipeline.Descriptor.VertexEntryPoint).IsEqualTo(GlShaderKeys.VertexEntryPoint);
			await Assert.That(pipeline.Descriptor.FragmentEntryPoint).IsEqualTo(GlShaderKeys.SmoothFragmentEntryPoint);
			await Assert.That(pipeline.Descriptor.Topology).IsEqualTo(PrimitiveTopology.TriangleStrip);

			// The bind group is the uniform block alone when nothing is textured.
			var bindGroup = harness.Device.CommandsOf<CreateBindGroupCommand>().Single();
			await Assert.That(bindGroup.Descriptor.Entries.Length).IsEqualTo(1);
			await Assert.That(bindGroup.Descriptor.Entries[0].Binding).IsEqualTo(GlShaderKeys.UniformBinding);
		}

		[Test]
		public async Task ATexturedQuadPicksTheTexturedModuleAndBindsTextureAndSampler()
		{
			var harness = GlCompatTestHarness.Create();

			int texture = harness.Context.GenTexture();
			harness.Context.Enable((int)EnableCap.Texture2D);
			harness.Context.BindTexture((int)TextureTarget.Texture2D, texture);
			harness.Context.TexImage2D(0, 0, 0, 2, 2, 0, 0x1908, 0, new byte[2 * 2 * 4]);

			harness.Context.Begin(BeginMode.TriangleStrip);
			for (int i = 0; i < 4; i++)
			{
				harness.Context.TexCoord2(i & 1, (i >> 1) & 1);
				harness.Context.Vertex2(i & 1, (i >> 1) & 1);
			}

			harness.Context.End();
			harness.Context.FlushPass();

			var pipeline = harness.BoundPipelines().Single();
			await Assert.That(pipeline.Descriptor.VertexShader.SourceKey).IsEqualTo(GlShaderKeys.PositionTexture);

			var vertexWrite = harness.VertexWrites().Single();
			await Assert.That(vertexWrite.Data.Length).IsEqualTo(4 * (int)GlShaderKeys.TexturedVertexLayout.ArrayStride);

			var bindGroup = harness.Device.CommandsOf<CreateBindGroupCommand>().Single();
			var entries = bindGroup.Descriptor.Entries;
			await Assert.That(entries.Length).IsEqualTo(3);
			await Assert.That(entries[0].Buffer).IsNotNull();
			await Assert.That(entries[1].Texture).IsNotNull();
			await Assert.That(entries[2].Sampler).IsNotNull();
		}

		[Test]
		public async Task ATriangleFanIsConvertedToATriangleList()
		{
			var harness = GlCompatTestHarness.Create();

			harness.Context.Begin(BeginMode.TriangleFan);
			harness.Context.Vertex2(0, 0);
			harness.Context.Vertex2(10, 0);
			harness.Context.Vertex2(10, 10);
			harness.Context.Vertex2(0, 10);
			harness.Context.End();
			harness.Context.FlushPass();

			// Four fan vertices are two triangles, so six list vertices.
			await Assert.That(harness.Device.CommandsOf<DrawCommand>().Single().VertexCount).IsEqualTo(6);
			await Assert.That(harness.BoundPipelines().Single().Descriptor.Topology).IsEqualTo(PrimitiveTopology.TriangleList);
		}

		[Test]
		public async Task AFlatShadedFanKeepsEveryVertexsOwnColorBecauseTheOracleDoes()
		{
			// The conversion rewrites a fan's vertices as a list but leaves the mode saying "fan", which
			// makes ColorIndexForFlatShading the identity - so a flat shaded fan renders smooth. Real GL
			// would give each triangle its provoking vertex's color, so this is a bug; it is the classic
			// D3D11 path's bug, the goldens were captured through it, and parity beats correctness until
			// the cutover. This test exists so fixing it later is a deliberate act with new goldens.
			var immediate = new GlImmediateModeBuffer();
			immediate.Begin(BeginMode.TriangleFan);
			byte[] reds = { 10, 20, 30, 40 };
			foreach (byte red in reds)
			{
				immediate.SetColor(red, 0, 0, 255);
				immediate.AddVertex(red, 0, 0);
			}

			immediate.ConvertTriangleFanToTriangles();

			await Assert.That(immediate.Mode).IsEqualTo(BeginMode.TriangleFan);
			await Assert.That(GlStateShadow.MapTopology(immediate.Mode)).IsEqualTo(PrimitiveTopology.TriangleList);
			await Assert.That(GlImmediateModeBuffer.ColorIndexForFlatShading(BeginMode.TriangleFan, 0, 6, true)).IsEqualTo(0);

			// Vertices 0,1,2 then 0,2,3 - each carrying the color it was given, flat shading or not.
			var flat = GlImmediateModeBuffer.BuildColoredVertices(
				immediate.Mode,
				immediate.Positions,
				immediate.Colors,
				true);

			int[] expected = { 10, 20, 30, 10, 30, 40 };
			for (int i = 0; i < expected.Length; i++)
			{
				await Assert.That(GlCompatTestHarness.ColoredVertexRed(flat, i)).IsEqualTo(expected[i] / 255f).Within(1e-6f);
			}
		}

		[Test]
		public async Task ImmediateModeVertexBuffersAreRecycledPerSubmitNotLeakedPerFlush()
		{
			// Every glEnd used to create a vertex buffer that nothing owned and nothing disposed. Pooling
			// them has one hard constraint: within a submit window each flush must still get a distinct
			// buffer, because a queue write is ordered against the submit rather than against the draws in
			// an open pass - reusing a buffer between two draws of one pass would show the second batch's
			// vertices in the first batch's draw.
			var harness = GlCompatTestHarness.Create();

			harness.DrawTriangle();
			harness.DrawTriangle();
			harness.Context.FlushPass();

			var withinFrame = harness.VertexWrites().Select(write => write.Buffer).ToList();
			await Assert.That(withinFrame.Count).IsEqualTo(2);
			await Assert.That(ReferenceEquals(withinFrame[0], withinFrame[1])).IsFalse();
			await Assert.That(harness.VertexBufferCreations().Count).IsEqualTo(2);

			// After the submit the pool hands the same two buffers back rather than creating more.
			harness.Context.Submit();
			harness.DrawTriangle();
			harness.DrawTriangle();
			harness.Context.FlushPass();

			var afterSubmit = harness.VertexWrites().Select(write => write.Buffer).ToList();
			await Assert.That(afterSubmit.Count).IsEqualTo(4);
			await Assert.That(ReferenceEquals(afterSubmit[2], afterSubmit[0])).IsTrue();
			await Assert.That(ReferenceEquals(afterSubmit[3], afterSubmit[1])).IsTrue();
			await Assert.That(harness.VertexBufferCreations().Count).IsEqualTo(2);
		}

		[Test]
		public async Task AnEmptyBatchDrawsNothingAndOpensNoPass()
		{
			var harness = GlCompatTestHarness.Create();

			harness.Context.Begin(BeginMode.Triangles);
			harness.Context.End();

			await Assert.That(harness.Device.CommandsOf<DrawCommand>().Count).IsEqualTo(0);
			await Assert.That(harness.Context.Passes.PassOpenCount).IsEqualTo(0);
		}

		[Test]
		public async Task FlatShadingTakesTheProvokingVertexColorAndSmoothTakesEachVertexs()
		{
			var positions = new List<float> { 0, 0, 0, 1, 0, 0, 1, 1, 0 };
			var colors = new List<byte> { 10, 10, 10, 255, 20, 20, 20, 255, 30, 30, 30, 255 };

			var smooth = GlImmediateModeBuffer.BuildColoredVertices(BeginMode.Triangles, positions, colors, false);
			var flat = GlImmediateModeBuffer.BuildColoredVertices(BeginMode.Triangles, positions, colors, true);

			// Smooth keeps each vertex's own color.
			await Assert.That(GlCompatTestHarness.ColoredVertexRed(smooth, 0)).IsEqualTo(10f / 255f).Within(1e-6f);
			await Assert.That(GlCompatTestHarness.ColoredVertexRed(smooth, 1)).IsEqualTo(20f / 255f).Within(1e-6f);
			await Assert.That(GlCompatTestHarness.ColoredVertexRed(smooth, 2)).IsEqualTo(30f / 255f).Within(1e-6f);

			// Flat gives all three the last vertex of the triangle - GL's provoking vertex rule, applied
			// on the CPU exactly as the classic D3D11 path applies it.
			for (int i = 0; i < 3; i++)
			{
				await Assert.That(GlCompatTestHarness.ColoredVertexRed(flat, i)).IsEqualTo(30f / 255f).Within(1e-6f);
			}
		}

		[Test]
		public async Task FlatShadingPicksTheProvokingVertexPerPrimitiveKind()
		{
			// Triangles provoke on the last vertex of each triangle, strips on i + 2, lines on the odd
			// vertex, and an index past the end clamps. All four are the classic path's choices.
			await Assert.That(GlImmediateModeBuffer.ColorIndexForFlatShading(BeginMode.Triangles, 3, 6, true)).IsEqualTo(5);
			await Assert.That(GlImmediateModeBuffer.ColorIndexForFlatShading(BeginMode.TriangleStrip, 1, 6, true)).IsEqualTo(3);
			await Assert.That(GlImmediateModeBuffer.ColorIndexForFlatShading(BeginMode.Lines, 2, 6, true)).IsEqualTo(3);
			await Assert.That(GlImmediateModeBuffer.ColorIndexForFlatShading(BeginMode.TriangleStrip, 5, 6, true)).IsEqualTo(5);
			await Assert.That(GlImmediateModeBuffer.ColorIndexForFlatShading(BeginMode.Triangles, 1, 6, false)).IsEqualTo(1);
		}

		[Test]
		public async Task FlatShadingSelectsTheFlatFragmentEntryPoint()
		{
			var harness = GlCompatTestHarness.Create();

			harness.Context.ShadeModel(ShadingModel.Flat);
			harness.DrawTriangle();
			harness.Context.ShadeModel(ShadingModel.Smooth);
			harness.DrawTriangle();
			harness.Context.FlushPass();

			var pipelines = harness.BoundPipelines();
			await Assert.That(pipelines[0].Descriptor.FragmentEntryPoint).IsEqualTo(GlShaderKeys.FlatFragmentEntryPoint);
			await Assert.That(pipelines[1].Descriptor.FragmentEntryPoint).IsEqualTo(GlShaderKeys.SmoothFragmentEntryPoint);

			// Same module, different entry point: the module cache is not fragmented by shading mode.
			await Assert.That(harness.Context.Pipelines.ShaderModuleCount).IsEqualTo(1);
		}

		[Test]
		public async Task UserShaderEntryPointsRefuseRatherThanPretend()
		{
			var harness = GlCompatTestHarness.Create();

			await Assert.That(() => harness.Context.CreateShader(0x8B31)).Throws<NotSupportedException>();
			await Assert.That(() => harness.Context.CreateProgram()).Throws<NotSupportedException>();
			await Assert.That(() => harness.Context.ShaderSource(1, 1, "void main() {}", null)).Throws<NotSupportedException>();
			await Assert.That(() => harness.Context.CompileShader(1)).Throws<NotSupportedException>();
			await Assert.That(() => harness.Context.UseProgram(1)).Throws<NotSupportedException>();
		}

		[Test]
		public async Task TheGlFacadeDrivesTheCompatContextThroughAWholeBatch()
		{
			// Deliverable check: GL takes an IGpuContext, so it wraps this exactly as it wraps the
			// classic backend - no change to GL.cs, no adapter.
			var harness = GlCompatTestHarness.Create();
			var gl = new GL(harness.Context);

			gl.MatrixMode(MatterHackers.RenderGl.OpenGl.MatrixMode.Modelview);
			gl.PushMatrix();
			gl.Translate(3, 4, 0);
			gl.Color4(Color.Red);
			gl.Begin(BeginMode.Triangles);
			gl.Vertex2(0, 0);
			gl.Vertex2(1, 0);
			gl.Vertex2(1, 1);
			gl.End();
			gl.PopMatrix();
			harness.Context.FlushPass();

			await Assert.That(ReferenceEquals(gl.GpuContext, harness.Context)).IsTrue();
			await Assert.That(harness.Device.CommandsOf<DrawCommand>().Single().VertexCount).IsEqualTo(3);
		}
	}
}
