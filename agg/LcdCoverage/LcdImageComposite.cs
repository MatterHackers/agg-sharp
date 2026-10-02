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
using System.Runtime.CompilerServices;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Transform;

namespace MatterHackers.Agg.LcdCoverage
{
	/// <summary>
	/// The image-draw twin of the LCD vector path: an image carrying an <see cref="LcdCoverageSidecar"/> (an
	/// SVG icon) composites per channel when it lands 1:1 on whole pixels, as text does. Every backend's
	/// <c>Render(IImageByte, ...)</c> asks <see cref="TryRender"/> first and takes its ordinary blit when the
	/// answer is no, so nothing changes for any other draw.
	/// </summary>
	public static class LcdImageComposite
	{
		/// <summary>
		/// The finished buffer per image, so an icon drawn every frame is converted once rather than per draw.
		/// Weak on the image, so the cache never keeps an icon alive.
		/// </summary>
		private static readonly ConditionalWeakTable<ImageBuffer, CachedBuffer> Cache = new ConditionalWeakTable<ImageBuffer, CachedBuffer>();

		/// <summary>
		/// Composites <paramref name="imageSource"/> through <see cref="Graphics2D.CompositeLcdBuffer"/> and
		/// returns true when every gate allows it; returns false, having drawn nothing, otherwise.
		/// </summary>
		/// <remarks>
		/// The gates, any one of which sends the draw back to the ordinary blit unchanged:
		/// <list type="bullet">
		/// <item><description>the image carries a sidecar made for its current size;</description></item>
		/// <item><description>the destination takes a whole LCD buffer
		/// (<see cref="Graphics2D.CanCompositeLcdBuffer"/>, which already refuses transparent compositing
		/// layers) and LCD rendering is on;</description></item>
		/// <item><description>the whole placement - the image's own scale and turn and the graphics transform -
		/// is a pure translation landing on whole destination pixels. The buffer is finished pixels, and
		/// <see cref="Graphics2D.CompositeLcdBuffer"/> places it untransformed; resampling it would smear each
		/// channel's phase into its neighbours, so anything else draws as before.</description></item>
		/// <item><description><paramref name="destinationAccepts"/>, when given, accepts the destination pixels
		/// the image will cover (left, bottom, right, top; half-open) - the backend's own validity check;</description></item>
		/// <item><description>the image has not gained ink since its coverage was made (see
		/// <see cref="LcdCoverageSidecar.BuildLcdBuffer"/>).</description></item>
		/// </list>
		/// </remarks>
		public static bool TryRender(
			Graphics2D graphics,
			IImageByte imageSource,
			double x,
			double y,
			double angleRadians,
			double scaleX,
			double scaleY,
			Func<RectangleInt, bool> destinationAccepts = null)
		{
			if (!(imageSource is ImageBuffer image)
				|| !(image.LcdCoverage is LcdCoverageSidecar sidecar)
				|| !sidecar.Fits(image)
				|| angleRadians != 0
				|| scaleX != 1
				|| scaleY != 1
				|| !graphics.CanCompositeLcdBuffer)
			{
				return false;
			}

			Affine transform = graphics.GetTransform();
			if (transform.sx != 1 || transform.sy != 1 || transform.shx != 0 || transform.shy != 0
				|| !LcdRenderSettings.IsEnabledAtScale(1))
			{
				return false;
			}

			// The same placement the ordinary blit computes: position through the transform, less the hotspot.
			double placedX = x + transform.tx - image.OriginOffset.X;
			double placedY = y + transform.ty - image.OriginOffset.Y;
			if (placedX != (int)placedX || placedY != (int)placedY)
			{
				return false;
			}

			var footprint = new RectangleInt((int)placedX, (int)placedY, (int)placedX + image.Width, (int)placedY + image.Height);
			if (destinationAccepts != null && !destinationAccepts(footprint))
			{
				return false;
			}

			LcdBuffer buffer = GetBuffer(image, sidecar);
			if (buffer == null)
			{
				return false;
			}

			graphics.CompositeLcdBuffer(buffer, footprint.Left, footprint.Bottom);
			return true;
		}

		/// <summary>
		/// The cached buffer for <paramref name="image"/>, or null when its coverage is unusable, rebuilt when
		/// the image's pixels or its sidecar have changed since it was made. The null verdict is cached too, so
		/// an image that has gained ink is scanned once per change rather than on every frame.
		/// </summary>
		private static LcdBuffer GetBuffer(ImageBuffer image, LcdCoverageSidecar sidecar)
		{
			CachedBuffer cached = Cache.GetValue(image, _ => new CachedBuffer());
			lock (cached)
			{
				if (!cached.Built
					|| cached.Sidecar != sidecar
					|| cached.ChangedCount != image.ChangedCount)
				{
					// Read before the build, not after: a change that lands while the buffer is being built must
					// leave the stamp behind the image, so the next draw rebuilds instead of keeping a buffer made
					// from half-changed pixels.
					int changedCount = image.ChangedCount;
					cached.Buffer = sidecar.BuildLcdBuffer(image);
					cached.Sidecar = sidecar;
					cached.ChangedCount = changedCount;
					cached.Built = true;
				}

				return cached.Buffer;
			}
		}

		private sealed class CachedBuffer
		{
			public bool Built;

			public LcdBuffer Buffer;

			public LcdCoverageSidecar Sidecar;

			public int ChangedCount;
		}
	}
}
