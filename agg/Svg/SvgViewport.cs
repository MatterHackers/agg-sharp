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
using System.Collections.Generic;
using MatterHackers.Agg.Transform;

namespace MatterHackers.Agg.Svg
{
	/// <summary>The viewBox and preserveAspectRatio attributes: how user space is fitted to a viewport.</summary>
	public static class SvgViewport
	{
		/// <summary>viewBox="minX minY width height", or null when missing or when its size is not positive.</summary>
		public static RectangleDouble? ParseViewBox(string text)
		{
			List<double> values = SvgLength.ParseList(text);
			if (values.Count != 4 || values[2] <= 0 || values[3] <= 0)
			{
				return null;
			}

			return new RectangleDouble(values[0], values[1], values[0] + values[2], values[1] + values[3]);
		}

		/// <summary>
		/// The transform that fits <paramref name="viewBox"/> into a <paramref name="width"/> by
		/// <paramref name="height"/> viewport at the origin, per <paramref name="preserveAspectRatio"/>
		/// ("none", or an x/y alignment such as xMidYMid - the default - with "meet" or "slice"). Both are in
		/// SVG's y-down space.
		/// </summary>
		public static Affine ViewBoxTransform(RectangleDouble viewBox, string preserveAspectRatio, double width, double height)
		{
			double scaleX = width / viewBox.Width;
			double scaleY = height / viewBox.Height;
			string[] parts = (preserveAspectRatio ?? "").Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
			string align = parts.Length > 0 ? parts[0] : "xMidYMid";
			if (align == "defer")
			{
				align = parts.Length > 1 ? parts[1] : "xMidYMid";
			}

			double translateX = 0, translateY = 0;
			if (align != "none")
			{
				bool slice = Array.IndexOf(parts, "slice") >= 0;
				double scale = slice ? Math.Max(scaleX, scaleY) : Math.Min(scaleX, scaleY);
				scaleX = scaleY = scale;
				double spareX = width - viewBox.Width * scale;
				double spareY = height - viewBox.Height * scale;
				translateX = align.Contains("xMid") ? spareX / 2 : align.Contains("xMax") ? spareX : 0;
				translateY = align.Contains("YMid") ? spareY / 2 : align.Contains("YMax") ? spareY : 0;
			}

			return Affine.NewTranslation(-viewBox.Left, -viewBox.Bottom)
				* Affine.NewScaling(scaleX, scaleY)
				* Affine.NewTranslation(translateX, translateY);
		}
	}
}
