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
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.Agg
{
	/// <summary>How a filtered image draw reads past the image's edge (C++ AGG's image accessors).</summary>
	public enum ImageFilterEdge
	{
		/// <summary>The nearest edge pixel (image_accessor_clone).</summary>
		Clamp,

		/// <summary><see cref="ImageFilterFill.Background"/> (image_accessor_clip).</summary>
		Clip,

		/// <summary>The image tiles (image_accessor_wrap with wrap_mode_repeat).</summary>
		Repeat,

		/// <summary>The image tiles mirrored on every other tile (image_accessor_wrap with wrap_mode_reflect).</summary>
		Reflect,
	}

	/// <summary>Which of C++ AGG's image span generators a filtered image draw runs.</summary>
	public enum ImageFilterKind
	{
		/// <summary>span_image_filter_rgba_nn: each pixel takes the image pixel under its center.</summary>
		Nearest,

		/// <summary>span_image_filter_rgba_bilinear: the 2x2 block under the center, weighted by distance.</summary>
		Bilinear,

		/// <summary>
		/// span_image_filter_rgba (span_image_filter_rgba_2x2 for a table of diameter 2): the table's weights over
		/// its diameter at the mapped pixel center, whatever the scale.
		/// </summary>
		Filter,

		/// <summary>
		/// span_image_resample_rgba(_affine): the table's weights widened by the mapping's local scale where it
		/// shrinks the image, so a reduction averages every image pixel it covers.
		/// </summary>
		Resample,
	}

	/// <summary>How <see cref="IImageFilterGraphics.FillPathWithFilteredImage"/> maps and filters its image.</summary>
	public sealed class ImageFilterFill
	{
		/// <param name="imageToScreen">Maps image pixels (row 0 at the bottom) to the target's pixels, perspective
		/// included; the current transform does not move the image (as in <see cref="IPatternFillGraphics"/>).</param>
		public ImageFilterFill(Perspective imageToScreen)
		{
			this.ImageToScreen = imageToScreen;
		}

		/// <inheritdoc cref="ImageFilterFill(Perspective)"/>
		public ImageFilterFill(Affine imageToScreen)
			: this(new Perspective(imageToScreen))
		{
		}

		/// <summary>
		/// Maps the target's pixels to image pixels through a bilinear quad transform - as C++ AGG's demos'
		/// "bilinear" mode runs span_interpolator_linear over trans_bilinear - in place of
		/// <see cref="ImageToScreen"/>. Not for <see cref="ImageFilterKind.Resample"/>.
		/// </summary>
		/// <param name="screenToImage">The target's pixels to image pixels.</param>
		public ImageFilterFill(Bilinear screenToImage)
			: this(Affine.NewIdentity())
		{
			this.BilinearScreenToImage = screenToImage;
		}

		/// <summary>Image pixels to the target's pixels.</summary>
		public Perspective ImageToScreen { get; set; }

		/// <summary>When set, the target's pixels to image pixels, used in place of <see cref="ImageToScreen"/>.</summary>
		public Bilinear BilinearScreenToImage { get; set; }

		/// <summary>The span generator to match.</summary>
		public ImageFilterKind Kind { get; set; } = ImageFilterKind.Bilinear;

		/// <summary>The weights <see cref="ImageFilterKind.Filter"/> and <see cref="ImageFilterKind.Resample"/> use.</summary>
		public ImageFilterLookUpTable Filter { get; set; }

		/// <summary>What is read past the image's edge.</summary>
		public ImageFilterEdge Edge { get; set; } = ImageFilterEdge.Clamp;

		/// <summary>What <see cref="ImageFilterEdge.Clip"/> reads outside the image.</summary>
		public Color Background { get; set; }

		/// <summary>
		/// When set, each filtered pixel's alpha is replaced by the entry its brightness picks - the entry
		/// min((r + g + b) * length / (3 * 255), length - 1) - as image_alpha's span_conv_brightness_alpha does
		/// after its filter.
		/// </summary>
		public byte[] BrightnessToAlpha { get; set; }

		/// <summary>
		/// When set, every filtered pixel has full alpha and its colour is clamped to 0..255 only, as C++ AGG's rgb
		/// generators give (span_image_filter_rgb and the rest): a table whose weights don't sum to 1 then changes
		/// the colour's level, not its opacity. Off, alpha is the image's alpha summed through the weights, as the
		/// rgba generators give.
		/// </summary>
		public bool Opaque { get; set; }

		/// <summary>span_image_resample's blur: the resample footprint's multiplier (1 = the local scale).</summary>
		public double Blur { get; set; } = 1;

		/// <summary>Whether <see cref="ImageToScreen"/> has no perspective part.</summary>
		public bool IsAffine => this.BilinearScreenToImage == null && this.ImageToScreen.w0 == 0 && this.ImageToScreen.w1 == 0 && this.ImageToScreen.w2 == 1;

		/// <summary>The target's pixels to image pixels.</summary>
		public Perspective ScreenToImage()
		{
			var screenToImage = new Perspective(this.ImageToScreen);
			screenToImage.invert();
			return screenToImage;
		}
	}

	/// <summary>
	/// A <see cref="Graphics2D"/> that can fill a path with an image through C++ AGG's image filters - its
	/// span_image_filter / span_image_resample generators, weight tables and accessors. <see cref="ImageGraphics2D"/>
	/// runs those generators; the GPU surface runs their integer arithmetic per pixel in a shader.
	/// </summary>
	public interface IImageFilterGraphics
	{
		/// <summary>
		/// Fills <paramref name="path"/> (in the current transform, anti-aliased like any fill) with
		/// <paramref name="image"/> mapped and filtered as <paramref name="fill"/> says.
		/// </summary>
		/// <param name="path">The shape to fill.</param>
		/// <param name="image">A 32 bit image, composited as <see cref="IPatternFillGraphics.FillPathWithImage"/>
		/// composites its image.</param>
		/// <param name="fill">The mapping, filter and edge.</param>
		void FillPathWithFilteredImage(IVertexSource path, IImageByte image, ImageFilterFill fill);

		/// <summary>
		/// <see cref="FillPathWithFilteredImage(IVertexSource, IImageByte, ImageFilterFill)"/> reading the picture
		/// <paramref name="layer"/> holds where it is, so a result can be filtered again without leaving the device -
		/// image_filters' repeated turns on the GPU. The layer's logical pixels are the image's pixels, and it must
		/// not be the layer being painted.
		/// </summary>
		/// <param name="path">The shape to fill.</param>
		/// <param name="layer">A layer this surface made (<see cref="Graphics2D.CreateRetainedLayer()"/>), painted
		/// at least once. Its colour is premultiplied, and the filters run on it as C++ AGG's rgba generators run
		/// over a premultiplied pixel format; <see cref="ImageFilterFill.BrightnessToAlpha"/> is not supported.</param>
		/// <param name="fill">The mapping, filter and edge.</param>
		/// <exception cref="NotSupportedException">The surface has no retained layers to read (the default).</exception>
		void FillPathWithFilteredImage(IVertexSource path, IRetainedLayer layer, ImageFilterFill fill)
			=> throw new NotSupportedException("This surface cannot read a retained layer as a filtered image.");
	}
}
