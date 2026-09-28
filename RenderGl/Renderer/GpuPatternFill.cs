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
using System.Runtime.CompilerServices;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters2D;
using MatterHackers.RenderGl.OpenGl;

namespace MatterHackers.RenderGl
{
	/// <summary>
	/// <see cref="Graphics2DGpu"/>'s <see cref="IPatternFillGraphics"/>: the path's halo mesh (the same anti-aliased
	/// tessellation as a solid fill), each vertex given the image coordinate the inverse image transform puts under
	/// it, sampled nearest with the sampler's repeat or mirror-repeat addressing doing the tiling.
	/// </summary>
	internal static class GpuPatternFill
	{
		// A source that is not an ImageBuffer keeps one ImageBuffer copy, so its texture is made once and re-uploaded
		// only when the source's pixels change.
		private static readonly ConditionalWeakTable<IImageByte, ImageBuffer> copies = new ConditionalWeakTable<IImageByte, ImageBuffer>();

		public static void Fill(Graphics2DGpu graphics, GL gl, IVertexSource path, IImageByte image, Affine imageToScreen, ImageWrapMode wrapX, ImageWrapMode wrapY)
		{
			var imageBuffer = image as ImageBuffer ?? CopyOf(image);

			var mesh = new HaloAaTesselator();
			VertexSourceToTesselator.SendShapeToTesselator(mesh, new VertexSourceApplyTransform(path, graphics.GetTransform()));
			mesh.BuildHaloMesh();

			Affine screenToImage = imageToScreen;
			screenToImage.invert();

			// The texture Render(IImageByte) draws this image from; its sampling state is read when each draw is
			// encoded, so it is switched to the pattern's here and put back after. The UVs are in the texture's own
			// size; a texture padded to a power of two would tile its padding, but the WebGPU context never pads.
			var texture = ImageTexturePlugin.GetImageTexturePlugin(gl, imageBuffer, false);

			graphics.PushOrthoProjection();
			gl.Disable(EnableCap.Lighting);
			gl.Disable(EnableCap.DepthTest);
			gl.Enable(EnableCap.Texture2D);
			gl.Enable(EnableCap.Blend);

			// Straight-alpha texels, as Render(IImageByte) reads them; the halo's coverage rides on the vertex alpha.
			// GpuCompOp's coverage pass wants the coverage alone: untextured, premultiplied white.
			bool coverageOnly = graphics.CoverageOnly;
			if (coverageOnly)
			{
				gl.Disable(EnableCap.Texture2D);
			}

			gl.BlendFunc(coverageOnly ? BlendingFactorSrc.One : BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);
			gl.BindTexture(TextureTarget.Texture2D, texture.GLTextureHandle);
			SetSampling(gl, TextureMagFilter.Nearest, TextureMinFilter.Nearest, ToGl(wrapX), ToGl(wrapY));

			gl.Begin(BeginMode.Triangles);
			foreach (int index in mesh.HaloIndices)
			{
				var vertex = mesh.HaloVertices[index];
				double u = vertex.Position.X;
				double v = vertex.Position.Y;
				screenToImage.Transform(ref u, ref v);
				byte alpha = (byte)((255 * vertex.Alpha) + .5f);
				gl.Color4(coverageOnly ? alpha : (byte)255, coverageOnly ? alpha : (byte)255, coverageOnly ? alpha : (byte)255, alpha);
				gl.TexCoord2(u / texture.HardwareWidth, v / texture.HardwareHeight);
				gl.Vertex2(vertex.Position.X, vertex.Position.Y);
			}

			gl.End();

			texture.RestoreSampling();
			graphics.PopOrthoProjection();
		}

		private static ImageBuffer CopyOf(IImageByte image)
		{
			ImageBuffer copy = copies.GetValue(image, source => new ImageBuffer(source.Width, source.Height, 32, source.GetRecieveBlender()));
			if (image.BitDepth != 32 || copy.Width != image.Width || copy.Height != image.Height)
			{
				throw new NotSupportedException("A pattern fill's image is expected to be 32 bit, and not to change size.");
			}

			byte[] from = image.GetBuffer();
			byte[] to = copy.GetBuffer();
			int rowBytes = image.Width * 4;
			bool changed = false;
			for (int y = 0; y < image.Height; y++)
			{
				var sourceRow = new ReadOnlySpan<byte>(from, image.GetBufferOffsetY(y), rowBytes);
				var copyRow = new Span<byte>(to, copy.GetBufferOffsetY(y), rowBytes);
				if (!sourceRow.SequenceEqual(copyRow))
				{
					sourceRow.CopyTo(copyRow);
					changed = true;
				}
			}

			if (changed)
			{
				copy.MarkImageChanged();
			}

			return copy;
		}

		private static TextureWrapMode ToGl(ImageWrapMode mode)
			=> mode == ImageWrapMode.Reflect ? TextureWrapMode.MirroredRepeat : TextureWrapMode.Repeat;

		private static void SetSampling(GL gl, TextureMagFilter mag, TextureMinFilter min, TextureWrapMode wrapS, TextureWrapMode wrapT)
		{
			gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)mag);
			gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)min);
			gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)wrapS);
			gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)wrapT);
		}
	}
}
