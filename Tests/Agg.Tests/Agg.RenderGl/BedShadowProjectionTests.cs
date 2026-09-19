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
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.RenderGl;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.RenderGl
{
	/// <summary>
	/// The bed shadow is rasterized through one camera and sampled back through the floor's uvs, and the
	/// two have to describe the same rectangle. They did not: the shadow pass looked at the floor's centre
	/// while its orthographic volume was still written in world coordinates, so every mm the floor's
	/// centre sat away from the world origin moved the whole shadow map by that much.
	/// </summary>
	/// <remarks>
	/// Cause (b) of the four the bug report listed: the grown floor is no longer centred on the world
	/// origin (it is the scene footprint x1.5 unioned with the bed and rounded out to grid cells), and the
	/// shadow camera assumed that symmetry. A 1200mm bed centred on the origin hid it completely, which is
	/// why it only showed up at room scale.
	/// </remarks>
	public class BedShadowProjectionTests
	{
		/// <summary>The owner's retail floor: slabs spanning roughly -3000..9000 in X and -2000..7000 in Y.</summary>
		private static readonly RectangleDouble RoomFloor = new RectangleDouble(-3000, -2000, 9000, 7000);

		/// <summary>The bed the application has always drawn, centred on the world origin.</summary>
		private static readonly RectangleDouble DefaultBed = new RectangleDouble(-600, -600, 600, 600);

		/// <summary>
		/// Where a world point on the floor lands in the shadow map, as the pass actually rasterizes it:
		/// through the shadow camera to clip space and then to the target's uv (y down, because the
		/// framebuffer's rows run the opposite way to NDC y - which is the flip the bed shader undoes).
		/// </summary>
		private static Vector2 RasterizedUv(RectangleDouble floor, double viewDistance, Vector3 world)
		{
			var clip = ToClip(world, floor, viewDistance);
			return new Vector2((clip.X / clip.W + 1) / 2, (1 - (clip.Y / clip.W)) / 2);
		}

		/// <summary>
		/// A world point in the shadow camera's clip space. Vector4 by hand because Vector3's
		/// TransformPerspective builds its Vector4 with w = 0, which is a direction, not a point.
		/// </summary>
		private static Vector4 ToClip(Vector3 world, RectangleDouble floor, double viewDistance)
			=> Vector4.Transform(
				new Vector4(world, 1),
				BedShadowProjection.CreateView(floor, viewDistance) * BedShadowProjection.CreateProjection(floor, viewDistance));

		/// <summary>
		/// The uv the bed shader samples the shadow with for a point on the floor: NodeDesignerScene.wgsl's
		/// applyBedGrid takes the quad's own 0..1 uvs (which span the floor rectangle, placed geometrically
		/// by PlaceTextureOnFaces) and flips v.
		/// </summary>
		private static Vector2 SampledUv(RectangleDouble floor, Vector3 world)
			=> new Vector2((world.X - floor.Left) / floor.Width, (floor.Top - world.Y) / floor.Height);

		[Test]
		public async Task ShadowOfASlabOnAGrownFloorLandsUnderTheSlab()
		{
			var viewDistance = BedShadowProjection.ViewDistance(3);

			// A corner of a 6ft x 18ft slab lying on the floor, well away from the floor's centre.
			foreach (var corner in new[]
			{
				new Vector3(1829, 914, 3),
				new Vector3(7315, 914, 3),
				new Vector3(1829, 6096, 3),
				new Vector3(RoomFloor.Center.X, RoomFloor.Center.Y, 3),
			})
			{
				var rasterized = RasterizedUv(RoomFloor, viewDistance, corner);
				var sampled = SampledUv(RoomFloor, corner);

				// Half a texel of a 2048 map is 1/4096; anything larger is a visible offset, and the bug
				// was a quarter of the whole map.
				await Assert.That(Math.Abs(rasterized.X - sampled.X)).IsLessThan(1.0 / 8192);
				await Assert.That(Math.Abs(rasterized.Y - sampled.Y)).IsLessThan(1.0 / 8192);
			}
		}

		[Test]
		public async Task ADefaultBedRendersFromExactlyTheCameraItAlwaysDid()
		{
			// The historical camera: 1000mm above the bed centre, a 1200mm square volume, near 1 far 2000.
			var viewDistance = BedShadowProjection.ViewDistance(60);
			await Assert.That(viewDistance).IsEqualTo(1000);

			var expectedView = Matrix4X4.LookAt(new Vector3(0, 0, 1000), Vector3.Zero, Vector3.UnitY);
			var expectedProjection = Matrix4X4.CreateOrthographicOffCenter(-600, 600, -600, 600, 1, 2000);

			// Exact, not near: a scene that fits the old bed has to render the same image it always did.
			await Assert.That(BedShadowProjection.CreateView(DefaultBed, viewDistance).Equals(expectedView)).IsTrue();
			await Assert.That(BedShadowProjection.CreateProjection(DefaultBed, viewDistance).Equals(expectedProjection)).IsTrue();
		}

		[Test]
		public async Task AWallTallerThanTheOldCameraStillCastsAShadow()
		{
			// Three metres of wall used to be 2m behind a camera fixed 1000mm up, so it cast nothing.
			var wallTop = 3000.0;
			var viewDistance = BedShadowProjection.ViewDistance(wallTop);

			foreach (var z in new[] { 0.0, wallTop })
			{
				var clip = ToClip(new Vector3(RoomFloor.Center.X, RoomFloor.Center.Y, z), RoomFloor, viewDistance);
				var depth = clip.Z / clip.W;

				// The GL clip volume the renderer remaps to 0..1 depth: inside means it is rasterized.
				await Assert.That(depth).IsGreaterThanOrEqualTo(-1.0);
				await Assert.That(depth).IsLessThanOrEqualTo(1.0);
			}
		}
	}
}
