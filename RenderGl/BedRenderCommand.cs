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

using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;

namespace MatterHackers.RenderGl
{
	public sealed class BedRenderCommand
	{
		/// <summary>
		/// Alpha multiplier applied to the bed when viewing from below,
		/// making it semi-transparent so objects beneath the bed are visible.
		/// </summary>
		public const float BelowBedAlphaMultiplier = 0.3f;

		public RectangleDouble BedBounds { get; set; }

		public Color Color { get; set; } = Color.White;

		public bool LookingDownOnBed { get; set; }

		public Mesh Mesh { get; set; }

		public ImageBuffer TopBaseTexture { get; set; }

		public Matrix4X4 Transform { get; set; } = Matrix4X4.Identity;

		public ImageBuffer UnderBaseTexture { get; set; }

		/// <summary>
		/// Creates the MeshRenderCommand for rendering the bed, applying semi-transparency
		/// when the camera is looking up through the bed from below.
		/// </summary>
		public MeshRenderCommand CreateSceneCommand()
		{
			return new MeshRenderCommand
			{
				Color = Color,
				Mesh = Mesh,
				Transform = Transform,
				RenderType = RenderTypes.Shaded,
				WireFrameColor = Color.Transparent,
				BlendTexture = false,
				ForceCullBackFaces = false,
				AlphaMultiplier = LookingDownOnBed ? 1.0f : BelowBedAlphaMultiplier,
			};
		}
	}
}
