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
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.UI
{
	/// <summary>Where <see cref="ScrollOffsets.TargetForSpan"/> puts a span (a row, an item) in the view.</summary>
	public enum ScrollAlignment
	{
		/// <summary>The span's top at the view's top.</summary>
		Top,

		/// <summary>The span centred in the view.</summary>
		Center,

		/// <summary>The span's bottom at the view's bottom.</summary>
		Bottom,

		/// <summary>The least scroll that shows the whole span - none at all when it is already visible.</summary>
		BringIntoView,
	}

	/// <summary>
	/// A <see cref="ScrollableWidget"/>'s vertical scroll as one number, the distance the content has been scrolled
	/// down from its top, plus the maths for scrolling to a span and for drawing only the rows on screen.
	/// </summary>
	/// <remarks>
	/// ScrollPosition and TopLeftOffset are the scroll area's origin, whose meaning depends on how tall the content
	/// is; "how far down from the top, out of how far it can go" is what a scroll-to or a readout wants. Every value
	/// here is in device pixels, like the bounds it is measured from.
	/// </remarks>
	public static class ScrollOffsets
	{
		/// <summary>How far the content can scroll down: its height (margin included, as the scroll clamp counts it)
		/// less the view's, never negative.</summary>
		public static double MaxScrollFromTop(this ScrollableWidget scrollable)
		{
			double content = scrollable.ScrollArea.LocalBounds.Height + scrollable.ScrollArea.DeviceMargin.Height;
			return Math.Max(0, content - scrollable.LocalBounds.Height);
		}

		/// <summary>How far the content is scrolled down from its top: 0 at the top, <see cref="MaxScrollFromTop"/>
		/// at the bottom.</summary>
		public static double ScrollOffsetFromTop(this ScrollableWidget scrollable)
		{
			return Math.Max(0, Math.Min(scrollable.MaxScrollFromTop(), scrollable.TopLeftOffset.Y));
		}

		/// <summary>Scrolls the content <paramref name="offset"/> down from its top, clamped to the travel there is.
		/// The horizontal scroll is left where it is.</summary>
		public static void SetScrollOffsetFromTop(this ScrollableWidget scrollable, double offset)
		{
			double clamped = Math.Max(0, Math.Min(scrollable.MaxScrollFromTop(), offset));
			scrollable.TopLeftOffset = new Vector2(scrollable.TopLeftOffset.X, clamped);
		}

		/// <summary>How far the content can scroll right: its width (margin included) less the view's, never negative.</summary>
		public static double MaxScrollFromLeft(this ScrollableWidget scrollable)
		{
			double content = scrollable.ScrollArea.LocalBounds.Width + scrollable.ScrollArea.DeviceMargin.Width;
			return Math.Max(0, content - scrollable.LocalBounds.Width);
		}

		/// <summary>How far the content is scrolled right from its left edge: 0 at the left, <see cref="MaxScrollFromLeft"/>
		/// at the right. TopLeftOffset.X goes negative as the content moves left, hence the sign.</summary>
		public static double ScrollOffsetFromLeft(this ScrollableWidget scrollable)
		{
			return Math.Max(0, Math.Min(scrollable.MaxScrollFromLeft(), -scrollable.TopLeftOffset.X));
		}

		/// <summary>Scrolls the content <paramref name="offset"/> right from its left edge, clamped to the travel there
		/// is. The vertical scroll is left where it is.</summary>
		public static void SetScrollOffsetFromLeft(this ScrollableWidget scrollable, double offset)
		{
			double clamped = Math.Max(0, Math.Min(scrollable.MaxScrollFromLeft(), offset));
			scrollable.TopLeftOffset = new Vector2(-clamped, scrollable.TopLeftOffset.Y);
		}

		/// <summary>
		/// The scroll offset that puts the span <paramref name="spanTop"/>..<paramref name="spanTop"/> +
		/// <paramref name="spanHeight"/> (measured down from the content's top) where <paramref name="alignment"/>
		/// asks, in a view <paramref name="viewportHeight"/> tall currently scrolled <paramref name="currentOffset"/>,
		/// clamped to 0..<paramref name="maxScroll"/>.
		/// </summary>
		public static double TargetForSpan(double spanTop, double spanHeight, double viewportHeight, double currentOffset, double maxScroll, ScrollAlignment alignment)
		{
			double spanBottom = spanTop + spanHeight;
			double target = alignment switch
			{
				ScrollAlignment.Top => spanTop,
				ScrollAlignment.Center => spanTop - (viewportHeight - spanHeight) * 0.5,
				ScrollAlignment.Bottom => spanBottom - viewportHeight,
				_ => spanTop < currentOffset ? spanTop
					: spanBottom > currentOffset + viewportHeight ? spanBottom - viewportHeight
					: currentOffset,
			};
			return Math.Max(0, Math.Min(maxScroll, target));
		}

		/// <summary>
		/// The part of the widget being drawn that <paramref name="graphics2D"/> will actually paint, in that widget's
		/// own coordinates - what a scrolled virtual list draws its rows for.
		/// </summary>
		/// <remarks>
		/// GetClippingRect is in the destination surface's coordinates (the screen, or a double-buffered ancestor's
		/// backbuffer), not the widget's, so it is mapped back through the current transform.
		/// </remarks>
		public static RectangleDouble LocalClippingRect(Graphics2D graphics2D)
		{
			RectangleDouble clip = graphics2D.GetClippingRect();
			Transform.Affine transform = graphics2D.GetTransform();
			double left = clip.Left, bottom = clip.Bottom, right = clip.Right, top = clip.Top;
			transform.inverse_transform(ref left, ref bottom);
			transform.inverse_transform(ref right, ref top);
			return new RectangleDouble(Math.Min(left, right), Math.Min(bottom, top), Math.Max(left, right), Math.Max(bottom, top));
		}

		/// <summary>
		/// The rows, of <paramref name="rowCount"/> rows <paramref name="rowHeight"/> tall stacked down from
		/// <paramref name="contentTop"/> (y grows up, as agg's does), that touch the band
		/// <paramref name="visibleBottom"/>..<paramref name="visibleTop"/> - usually a draw's clipping rect - as
		/// [First, End). Drawing only these keeps a draw's cost to what is on screen however many rows there are.
		/// </summary>
		public static (int First, int End) VisibleRows(double contentTop, double visibleBottom, double visibleTop, double rowHeight, int rowCount)
		{
			if (rowCount <= 0 || rowHeight <= 0 || visibleTop <= visibleBottom)
			{
				return (0, 0);
			}

			// Clamped in doubles first: a clip far outside the content would overflow the int cast.
			double firstRow = Math.Floor((contentTop - visibleTop) / rowHeight);
			double endRow = Math.Ceiling((contentTop - visibleBottom) / rowHeight);
			int first = (int)Math.Max(0, Math.Min(rowCount, firstRow));
			int end = (int)Math.Max(first, Math.Min(rowCount, endRow));
			return (first, end);
		}
	}
}
