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
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.PolygonMesh;
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
	/// The selection outline's width is a UI size in units, so on a display that maps one unit to two
	/// device pixels (a Retina mac) it has to be twice as many pixels wide to look the same.
	/// </summary>
	public class SelectionOutlineWidthTests
	{
		private const int Width = 80;

		private const int Height = 60;

		[Test]
		[Arguments(1.0)]
		[Arguments(1.5)]
		[Arguments(2.0)]
		public async Task OutlineWidthIsTwoUnitsInDevicePixels(double deviceScale)
		{
			// Outside a full-frame capture the scene renders at the target's own resolution
			// (SupersampleScale 1), so device pixels and target pixels are the same thing here.
			float width = RenderOneSelectedCube(deviceScale);

			await Assert.That(width).IsEqualTo((float)(WebGpuSceneRenderer.SelectionOutlineWidth * deviceScale)).Within(1e-5f);
		}

		/// <summary>
		/// Draws and selects one cube through a <see cref="SceneDrawContext"/> made with
		/// <paramref name="deviceScale"/>, over the real scene renderer and a recording device, and returns the
		/// outline width the composite pass was handed.
		/// </summary>
		private static float RenderOneSelectedCube(double deviceScale)
		{
			var compat = GlCompatTestHarness.Create(Width, Height, withDepth: true);
			var gl = new GL(compat.Context);
			var renderer = new WebGpuSceneRenderer(compat.Context) { OwnerGl = gl };
			compat.Context.SceneRenderer = renderer;

			try
			{
				var world = new WorldView(Width, Height);
				world.Reset();

				var context = new SceneDrawContext(gl, world, deviceScale);
				var cube = PlatonicSolids.CreateCube(10, 10, 10);

				compat.Device.ClearRecording();
				context.BeginFrame(world, new RectangleDouble(0, 0, Width, Height), new LightingData());
				try
				{
					context.DrawMesh(cube, Color.Red, Matrix4X4.Identity);
					context.QueueSelectionOutline(cube, Color.White, Matrix4X4.Identity);
				}
				finally
				{
					context.EndFrame();
				}

				// The composite pass's bind group carries the outline uniform at binding 5; its first float is
				// the width the shader steps its taps out by.
				var commands = compat.Device.Commands;
				var outlineBindGroup = commands.OfType<SetBindGroupCommand>()
					.Single(command => command.Encoder.Descriptor.Label == "SelectionOutline")
					.BindGroup;
				var outlineBuffer = commands.OfType<CreateBindGroupCommand>()
					.Single(command => command.BindGroup == outlineBindGroup)
					.Descriptor.Entries.Single(entry => entry.Binding == 5)
					.Buffer;
				var write = commands.OfType<WriteBufferCommand>()
					.Last(command => command.Buffer == outlineBuffer && command.Offset == 0);

				return BitConverter.ToSingle(write.Data, 0);
			}
			finally
			{
				renderer.Dispose();
				compat.Context.Dispose();
				compat.Device.Dispose();
			}
		}
	}
}
