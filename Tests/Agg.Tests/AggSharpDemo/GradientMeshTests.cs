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
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Tests.GoldenImages;
using MatterHackers.Agg.UI;
using MatterHackers.AggSharpDemo;
using MatterHackers.AggSharpDemo.Demos;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using MatterHackers.Agg.Transform;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	// The gradient demos' GPU path colors mesh vertices with SpanSampler; a vertex on a pixel center must get
	// exactly the color the software path's span gives that pixel.
	public class GradientMeshTests
	{
		[Test]
		public async Task SamplerMatchesTheSpanAtAPixelCenter()
		{
			Affine demoToGradient = Affine.NewScaling(0.75, 1.2) * Affine.NewRotation(0.4) * Affine.NewTranslation(200, 150);
			demoToGradient.invert();
			var colors = new gradient_lut(256, fastInterpolator: false);
			colors.add_color(0.0, new Color(255, 0, 0, 255));
			colors.add_color(1.0, new Color(0, 0, 255, 128));
			colors.build_lut();

			var spanInterpolator = new span_interpolator_linear(demoToGradient);
			var span = new Color[4];
			new span_gradient(spanInterpolator, new gradient_reflect_adaptor(new gradient_radial()), colors, 0, 60).generate(span, 0, 230, 170, 4);

			var sampleInterpolator = new span_interpolator_linear(demoToGradient);
			var sampler = new SpanSampler(new span_gradient(sampleInterpolator, new gradient_reflect_adaptor(new gradient_radial()), colors, 0, 60), (sampleInterpolator, demoToGradient));
			for (int i = 0; i < 4; i++)
			{
				await Assert.That(sampler.Sample(230 + i + 0.5, 170.5)).IsEqualTo(span[i]);
			}
		}

		[Test]
		public async Task DiscHasAOnePixelHaloThatFadesTheRimColorsToTransparent()
		{
			Affine demoToGradient = Affine.NewIdentity();
			var colors = new gradient_lut(256, fastInterpolator: false);
			colors.add_color(0.0, new Color(255, 0, 0, 255));
			colors.add_color(1.0, new Color(0, 0, 255, 200));
			colors.build_lut();
			var interpolator = new span_interpolator_linear(demoToGradient);
			var sampler = new SpanSampler(new span_gradient(interpolator, new gradient_x(), colors, 0, 100), (interpolator, demoToGradient));

			// Scale 2: the halo must be 1 device pixel, so half a demo pixel.
			Affine transform = Affine.NewScaling(2) * Affine.NewTranslation(10, 20);
			const double cx = 50, cy = 40, radius = 30;
			const int rings = 4, sectors = 16;
			PosColorVertex[] vertices = GradientMesh.BuildDisc(transform, sampler, cx, cy, radius, rings, sectors);

			int cellVertices = rings * sectors * 6;
			await Assert.That(vertices.Length).IsEqualTo(cellVertices + (sectors * 6));

			Vector2 deviceCenter = new Vector2(cx * 2 + 10, cy * 2 + 20);
			double deviceRim = radius * 2;
			for (int v = cellVertices; v < vertices.Length; v++)
			{
				var position = new Vector2(vertices[v].Position.X, vertices[v].Position.Y);
				double distance = (position - deviceCenter).Length;
				Color color = vertices[v].Color;
				if (Math.Abs(distance - deviceRim) < 1e-6)
				{
					// Inner edge: the rim's own color, the same as the cells' outer ring there.
					Vector2 demo = (position - new Vector2(10, 20)) / 2;
					await Assert.That(color).IsEqualTo(sampler.Sample(demo.X, demo.Y));
				}
				else
				{
					await Assert.That(distance).IsEqualTo(deviceRim + GradientMesh.HaloWidth).Within(1e-6);
					await Assert.That(color.alpha).IsEqualTo((byte)0);

					// Same hue as the rim point it fades from, so the edge does not tint toward black.
					Vector2 demo = (deviceCenter + (position - deviceCenter) * (deviceRim / distance) - new Vector2(10, 20)) / 2;
					Color rim = sampler.Sample(demo.X, demo.Y);
					await Assert.That((color.red, color.green, color.blue)).IsEqualTo((rim.red, rim.green, rim.blue));
				}
			}
		}
	}
}
