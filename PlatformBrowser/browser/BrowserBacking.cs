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

namespace MatterHackers.Agg.Platform.Browser
{
	/// <summary>
	/// A canvas's backing store as agg cares about it: its size in device pixels, and the scale those pixels
	/// are at.
	/// </summary>
	/// <remarks>
	/// The equivalent of what <c>MacSystemWindow.SyncSizeFromBacking</c> reads off the window - drawable size
	/// plus <c>backingScaleFactor</c> - and it is a value rather than three fields so a resize can be compared
	/// against the last one in one place. The two travel together because they change together: dragging a
	/// window between a Retina display and a standard one changes the ratio without necessarily changing the
	/// CSS size, and changes the pixel size without necessarily changing the ratio when a pane is dragged.
	/// </remarks>
	public readonly struct BrowserBackingSize : IEquatable<BrowserBackingSize>
	{
		public BrowserBackingSize(uint pixelWidth, uint pixelHeight, double devicePixelRatio)
		{
			this.PixelWidth = pixelWidth;
			this.PixelHeight = pixelHeight;
			this.DevicePixelRatio = devicePixelRatio;
		}

		/// <summary>The backing store's width in device pixels - agg's coordinate space.</summary>
		public uint PixelWidth { get; }

		/// <summary>The backing store's height in device pixels - agg's coordinate space.</summary>
		public uint PixelHeight { get; }

		/// <summary>Device pixels per CSS pixel: the browser's <c>devicePixelRatio</c>.</summary>
		public double DevicePixelRatio { get; }

		public bool Equals(BrowserBackingSize other)
			=> this.PixelWidth == other.PixelWidth
				&& this.PixelHeight == other.PixelHeight
				&& this.DevicePixelRatio.Equals(other.DevicePixelRatio);

		public override bool Equals(object obj) => obj is BrowserBackingSize other && this.Equals(other);

		public override int GetHashCode() => HashCode.Combine(this.PixelWidth, this.PixelHeight, this.DevicePixelRatio);

		public override string ToString() => $"{this.PixelWidth}x{this.PixelHeight} @ {this.DevicePixelRatio}x";
	}

	/// <summary>
	/// Turns what the browser reports about a canvas into a <see cref="BrowserBackingSize"/> agg can be sized
	/// from.
	/// </summary>
	/// <remarks>
	/// The rounding lives in JS, not here: a <c>ResizeObserver</c> reading
	/// <c>devicePixelContentBoxSize</c> is handed exact integer device pixels by the browser itself, and the
	/// one fallback path (CSS pixels times <c>devicePixelRatio</c>) is rounded there so that one place decides
	/// what integer a fractional layout means - the canvas's own <c>width</c>/<c>height</c> attributes are set
	/// from that same number, and a second rounding here could disagree with the backing store JS just sized.
	/// What is left for this side is refusing the values that would break something downstream: a zero-sized
	/// canvas (display:none, or a pane collapsed to nothing) cannot be a swapchain, and a ratio of zero would
	/// divide through every coordinate conversion.
	/// <para/>
	/// Pure - no JS interop, no state - so it runs in the desktop test suite.
	/// </remarks>
	public static class BrowserBacking
	{
		/// <summary>
		/// The smallest backing extent that is reported. A hidden or collapsed canvas measures zero, and a
		/// swapchain of zero width is invalid on every backend; one pixel is the same floor
		/// <c>MacSystemWindow.CreateNativeWindow</c> puts under its content size.
		/// </summary>
		public const uint MinimumPixelExtent = 1;

		/// <summary>
		/// The backing size for a canvas the browser reports as <paramref name="devicePixelWidth"/> by
		/// <paramref name="devicePixelHeight"/> device pixels at <paramref name="devicePixelRatio"/>.
		/// </summary>
		public static BrowserBackingSize FromDeviceMetrics(
			double devicePixelWidth,
			double devicePixelHeight,
			double devicePixelRatio)
			=> new BrowserBackingSize(
				ClampPixelExtent(devicePixelWidth),
				ClampPixelExtent(devicePixelHeight),
				ClampDevicePixelRatio(devicePixelRatio));

		/// <summary>
		/// One axis of a backing size: at least <see cref="MinimumPixelExtent"/>, and a whole number of pixels.
		/// </summary>
		/// <remarks>
		/// NaN reaches here from an element with no layout box at all, which is what a canvas inside a
		/// <c>display:none</c> ancestor measures as in some engines; it means "no size", which is the floor.
		/// </remarks>
		public static uint ClampPixelExtent(double devicePixels)
		{
			if (double.IsNaN(devicePixels) || devicePixels < MinimumPixelExtent)
			{
				return MinimumPixelExtent;
			}

			if (devicePixels > uint.MaxValue)
			{
				return uint.MaxValue;
			}

			return (uint)Math.Round(devicePixels, MidpointRounding.AwayFromZero);
		}

		/// <summary>
		/// A usable <c>devicePixelRatio</c>: anything non-positive or NaN becomes 1, which is "one device
		/// pixel per CSS pixel" - the answer an unscaled display gives and the only safe default, since the
		/// ratio divides into every CSS-pixel coordinate the DOM reports.
		/// </summary>
		public static double ClampDevicePixelRatio(double devicePixelRatio)
			=> double.IsNaN(devicePixelRatio) || devicePixelRatio <= 0 ? 1 : devicePixelRatio;
	}
}
