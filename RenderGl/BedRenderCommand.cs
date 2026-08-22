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
		public RectangleDouble BedBounds { get; set; }

		public Color Color { get; set; } = Color.White;

		/// <summary>
		/// True when any scene object extends more than 1mm below the bed surface,
		/// causing the bed to become semi-transparent even when viewed from above
		/// so the user can see what's underneath.
		/// </summary>
		public bool ObjectsBelowBed { get; set; }

		public Mesh Mesh { get; set; }

		public Color ShadowColor { get; set; } = Color.Black;

		public ImageBuffer TopBaseTexture { get; set; }

		/// <summary>
		/// Edge length in pixels of the shadow mask, blur and composite intermediates the renderer
		/// allocates for the bed. Deliberately independent of <see cref="TopBaseTexture"/>'s size:
		/// the base texture only supplies the bed's flat fill colour (the grid is analytic), so it
		/// can be a handful of texels while the shadow still needs real resolution.
		/// </summary>
		public int ShadowMapSize { get; set; } = 2048;

		public Matrix4X4 Transform { get; set; } = Matrix4X4.Identity;

		/// <summary>
		/// Distance in mm between bed grid lines. The grid is drawn analytically by the
		/// pixel shader rather than baked into <see cref="TopBaseTexture"/>: a texture-space
		/// line is magnified and bilinearly smeared under perspective, so it can never stay
		/// one screen pixel wide no matter how the texture is authored.
		/// </summary>
		public double GridSpacing { get; set; } = 50;

		public Color GridLineColor { get; set; } = Color.Transparent;

		/// <summary>Color of the world X axis, the horizontal line at world Y == 0.</summary>
		public Color AxisXColor { get; set; } = Color.Transparent;

		/// <summary>Color of the world Y axis, the vertical line at world X == 0.</summary>
		public Color AxisYColor { get; set; } = Color.Transparent;

		/// <summary>Color of the short Z axis stub drawn at the origin.</summary>
		public Color AxisZColor { get; set; } = Color.Transparent;

		/// <summary>Half length in mm of the Z axis stub drawn at the origin.</summary>
		public double AxisHeight { get; set; } = 10;

		/// <summary>
		/// Grid line thickness in screen pixels. 1 pixel matches the weight of the baked
		/// grid (2 texels of a 2048 texture across a 1200mm bed).
		/// </summary>
		public float GridLineWidthPixels { get; set; } = 1.0f;

		/// <summary>Axis line thickness in screen pixels (the baked axes were 3 texels).</summary>
		public float AxisLineWidthPixels { get; set; } = 1.5f;

		/// <summary>
		/// Creates the MeshRenderCommand for rendering the bed. Bed translucency is
		/// encoded in the texture itself so the command alpha remains stable as the
		/// camera moves above or below the bed.
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
				AlphaMultiplier = 1.0f,
			};
		}
	}
}
