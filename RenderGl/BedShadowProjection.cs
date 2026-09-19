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
using MatterHackers.Agg;
using MatterHackers.VectorMath;

namespace MatterHackers.RenderGl
{
	/// <summary>
	/// The camera the bed shadow is rasterized from, straight down over one rectangle.
	/// </summary>
	/// <remarks>
	/// The whole bed shadow rests on ONE rectangle - <see cref="BedRenderCommand.BedBounds"/> - being used
	/// for the floor quad, for this camera, for the uv the bed shader samples the map with, and for
	/// <see cref="RenderHelper.ShouldRenderInBedShadow"/>'s culling. When there were two the shadows came
	/// out in the wrong place: the volume was written in world coordinates while the view had already
	/// recentred on the rectangle, so the map was offset by the rectangle's centre. A 1200mm bed sits on
	/// the world origin and that offset is zero, which is why it stayed invisible until a floor grew to
	/// room size and stopped being centred on it.
	/// </remarks>
	public static class BedShadowProjection
	{
		/// <summary>
		/// How far above the bed the camera sits for anything short. Not a look-at distance that matters
		/// optically - the projection is orthographic - but it is what fixes the near and far planes, and
		/// it is the application's historical value, so every scene that fits the old bed renders exactly
		/// as it did.
		/// </summary>
		public const double MinimumViewDistance = 1000;

		/// <summary>How much of the world below the bed the volume still reaches, so a part sunk into the bed casts.</summary>
		public const double DistanceBelowBed = 1000;

		/// <summary>Clearance kept between the camera and the tallest caster, comfortably clear of the near plane.</summary>
		public const double CasterHeadroom = 100;

		/// <summary>The near plane, in mm in front of the camera.</summary>
		public const double NearPlane = 1;

		/// <summary>
		/// How high the shadow camera has to be for everything in the scene to be in front of it. A wall is
		/// metres tall and the historical camera was one metre up, so anything taller than that was behind
		/// the camera and cast nothing at all.
		/// </summary>
		/// <param name="tallestCasterZ">Top of the tallest shadow caster in world mm, or 0 when there is none.</param>
		public static double ViewDistance(double tallestCasterZ)
		{
			if (double.IsNaN(tallestCasterZ) || double.IsInfinity(tallestCasterZ))
			{
				return MinimumViewDistance;
			}

			return Math.Max(MinimumViewDistance, tallestCasterZ + CasterHeadroom);
		}

		/// <summary>The shadow camera's view matrix: straight down at the middle of the floor rectangle.</summary>
		/// <param name="bedBounds">The floor rectangle, in world mm.</param>
		/// <param name="viewDistance">From <see cref="ViewDistance"/>.</param>
		public static Matrix4X4 CreateView(RectangleDouble bedBounds, double viewDistance)
		{
			var center = new Vector3((bedBounds.Left + bedBounds.Right) * .5, (bedBounds.Bottom + bedBounds.Top) * .5, 0);
			return Matrix4X4.LookAt(center + new Vector3(0, 0, viewDistance), center, Vector3.UnitY);
		}

		/// <summary>
		/// The shadow camera's orthographic volume, exactly covering the floor rectangle - so the map's
		/// edges are the rectangle's edges and a floor point's uv is simply where it sits in the rectangle.
		/// </summary>
		/// <remarks>
		/// The volume is written about the camera, not about the world origin, because
		/// <see cref="CreateView"/> has already moved the rectangle's centre to the eye. Naming the
		/// rectangle's world edges here instead (which is what the renderer used to do) counts that centre
		/// twice: every shadow then lands one floor-centre away from what cast it, which on a retail floor
		/// reaching 9m in X and 7m in Y put each slab's shadow about 3m left and 2.5m down of the slab.
		/// It went unseen for as long as the floor was the 1200mm bed on the origin, where the offset is 0.
		/// <para>
		/// The volume follows the rectangle's aspect rather than being squared off to it: the map is square
		/// but the shader samples it with the floor quad's own 0..1 uvs, so a non-square floor is simply a
		/// non-square texel and both ends still agree. Squaring the volume would waste half the map on a
		/// long room.
		/// </para>
		/// </remarks>
		/// <param name="bedBounds">The floor rectangle, in world mm.</param>
		/// <param name="viewDistance">From <see cref="ViewDistance"/>.</param>
		public static Matrix4X4 CreateProjection(RectangleDouble bedBounds, double viewDistance)
		{
			return Matrix4X4.CreateOrthographicOffCenter(
				-bedBounds.Width / 2,
				bedBounds.Width / 2,
				-bedBounds.Height / 2,
				bedBounds.Height / 2,
				NearPlane,
				viewDistance + DistanceBelowBed);
		}
	}
}
