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

using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.AggSharpDemo.Demos
{
	/// <summary>
	/// The quad demos' (image_perspective, image_resample, pattern_perspective, pattern_resample) GPU path: the
	/// rectangle (x1, y1) .. (x2, y2) of an image's pixel space mapped onto a quad the way C++ AGG's parallelogram,
	/// trans_bilinear and trans_perspective map it, for <see cref="IImageFilterGraphics.FillPathWithFilteredImage"/>.
	/// </summary>
	internal static class QuadImageFill
	{
		/// <summary>The quad's corners (x, y pairs) in the Graphics2D's pixels: the fill's mapping is in those.</summary>
		public static double[] ToFrame(double[] quad, Affine demoToFrame)
		{
			var frame = (double[])quad.Clone();
			for (int i = 0; i < 8; i += 2)
			{
				demoToFrame.Transform(ref frame[i], ref frame[i + 1]);
			}

			return frame;
		}

		/// <summary>The quad as a closed path.</summary>
		public static IVertexSource Outline(double[] quad)
		{
			var outline = new VertexStorage();
			outline.MoveTo(quad[0], quad[1]);
			outline.LineTo(quad[2], quad[3]);
			outline.LineTo(quad[4], quad[5]);
			outline.LineTo(quad[6], quad[7]);
			outline.ClosePolygon();
			return outline;
		}

		/// <summary>The parallelogram of the quad's first three corners (C++ trans_affine(rect, parl)).</summary>
		public static ImageFilterFill Parallelogram(double[] frameQuad, double x1, double y1, double x2, double y2)
		{
			Affine imageToFrame = ImagePerspectiveDemo.ParallelogramToRectangle(frameQuad, x1, y1, x2, y2);
			imageToFrame.invert();
			return new ImageFilterFill(imageToFrame);
		}

		/// <summary>C++ trans_bilinear(quad, rect) under span_interpolator_linear, or null while it is singular.</summary>
		public static ImageFilterFill Bilinear(double[] frameQuad, double x1, double y1, double x2, double y2)
		{
			var frameToImage = new Bilinear(frameQuad, x1, y1, x2, y2);
			return frameToImage.is_valid() ? new ImageFilterFill(frameToImage) : null;
		}

		/// <summary>C++ trans_perspective(rect, quad), or null while it is singular.</summary>
		public static ImageFilterFill Perspective(double[] frameQuad, double x1, double y1, double x2, double y2)
		{
			var imageToFrame = new Perspective(x1, y1, x2, y2, frameQuad);
			return imageToFrame.is_valid() ? new ImageFilterFill(imageToFrame) : null;
		}

		/// <summary>A 32 bit copy of a (24 bit) picture: the filtered image fill reads 32 bit images.</summary>
		public static ImageBuffer To32Bit(ImageBuffer source)
		{
			var copy = new ImageBuffer(source.Width, source.Height);
			for (int y = 0; y < source.Height; y++)
			{
				for (int x = 0; x < source.Width; x++)
				{
					copy.SetPixel(x, y, source.GetPixel(x, y));
				}
			}

			return copy;
		}
	}
}
