// Copyright (c) 2026, Lars Brubaker
// This file is licensed under the MIT license.
// See the LICENSE.md file in the project root for more information.

using System;

namespace Markdig.Renderers.Agg
{
	/// <summary>
	/// Pixel snapping shared by the two table grids (the viewer's <see cref="AggTable"/> and the rich editor's table
	/// painter), so both draw lines a whole number of device pixels thick on whole device pixels.
	/// </summary>
	internal static class GridPixels
	{
		/// <summary>
		/// A grid line's thickness in whole device pixels: one pixel at DeviceScale 1, scaled and rounded above it.
		/// </summary>
		public static int LineThickness(double deviceScale) => Math.Max(1, (int)Math.Round(deviceScale));

		/// <summary>
		/// <paramref name="value"/> moved to the nearest whole device pixel once the draw transform's
		/// <paramref name="translation"/> on that axis is applied, returned in drawing coordinates.
		/// </summary>
		/// <remarks>
		/// Floor(v + .5), not Math.Round: banker's rounding sends some .5 edges down and others up (DeviceScale 1.25
		/// puts edges on .5), which made columns differ by a pixel and let a line eat into a cell's padding.
		/// Assumes a translation-only transform (no zoom or scale), as AggTable's grid does.
		/// </remarks>
		public static double Snap(double value, double translation) => Math.Floor(value + translation + 0.5) - translation;
	}
}
