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
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.UI
{
	public class ScrollBar : GuiWidget
	{
		private readonly GuiWidget background;

		private readonly ScrollableWidget parentScrollWidget;

		private readonly ThumDragWidget thumb;

		private readonly Orientation orientation;

		private bool mouseInBounds = false;

		private ShowState showState = ShowState.WhenRequired;

		private readonly Color constructedTrackColor;

		private bool floating;

		private double? barWidth;

		private double floatingWidth;

		private double handleMinLength;

		private double outerMargin;

		private double innerMargin;

		private Color? trackColor;

		private Color? thumbColor;

		private Color? thumbHoverColor;

		// Only a floating bar that shows when required fades; every other bar is fully opaque.
		private readonly ScrollBarTween visibility = new ScrollBarTween(1, FadeMs);

		// A floating bar's growth from its thin width to its full thickness while hovered or dragged, 0..1.
		private readonly ScrollBarTween hover = new ScrollBarTween(0, GrowMs);

		internal ScrollBar(ScrollableWidget parent, Orientation orientation = Orientation.Vertical)
			: this(parent, DefaultBackgroundColor, DefaultThumbColor, orientation)
		{
		}

		internal ScrollBar(ScrollableWidget parent, Color backgroundColor, Color thumbViewColor, Orientation orientation = Orientation.Vertical)
		{
			parentScrollWidget = parent;
			this.orientation = orientation;

			this.constructedTrackColor = backgroundColor;
			this.background = new GuiWidget()
			{
				BackgroundColor = backgroundColor
			};
			thumb = new ThumDragWidget(orientation)
			{
				BackgroundColor = thumbViewColor
			};
			thumb.SizeChanged += (s, e) =>
			{
				thumb.BackgroundRadius = new RadiusCorners((orientation == Orientation.Horizontal ? thumb.Height : thumb.Width) / 2);
			};

			AddChild(background);
			AddChild(thumb);

			this.Margin = ScrollBar.DefaultMargin;

			parentScrollWidget.BoundsChanged += Bounds_Changed;
			parentScrollWidget.ScrollArea.BoundsChanged += Bounds_Changed;
			parentScrollWidget.ScrollPositionChanged += Bounds_Changed;
			parentScrollWidget.ScrollArea.MarginChanged += Bounds_Changed;

			UpdateScrollBar();
		}

		public enum ShowState
		{
			Never,
			WhenRequired,
			Always
		}

		public static BorderDouble DefaultMargin { get; set; } = 0;

		private static double? explicitGrowThumbBy;

		private static double? explicitScrollBarWidth;

		/// <summary>
		/// Gets or sets the amount to grow each side of the thumb in Y on Hover, in device pixels.
		/// </summary>
		/// <remarks>
		/// The default is read live rather than baked into a static initializer: a static initializer runs
		/// once when the type first loads and would hold whatever <see cref="GuiWidget.DeviceScale"/> was in
		/// force at that moment for the rest of the process. A value assigned here is taken verbatim - the
		/// caller owns the device pixels and is responsible for re-setting it if the scale changes.
		/// </remarks>
		public static double GrowThumbBy
		{
			get => explicitGrowThumbBy ?? 3 * GuiWidget.DeviceScale;
			set => explicitGrowThumbBy = value;
		}

		/// <summary>
		/// Gets or sets the width of a scroll bar, in device pixels. Defaults live off
		/// <see cref="GuiWidget.DeviceScale"/>; see <see cref="GrowThumbBy"/> for why and for what an
		/// explicit assignment means.
		/// </summary>
		public static double ScrollBarWidth
		{
			get => explicitScrollBarWidth ?? 15 * GuiWidget.DeviceScale;
			set => explicitScrollBarWidth = value;
		}

		public ShowState Show
		{
			get
			{
				return showState;
			}

			set
			{
				if (value != showState)
				{
					if (value != ShowState.Never
						&& value != ShowState.WhenRequired
						&& value != ShowState.Always)
					{
						throw new NotImplementedException();
					}

					showState = value;

					// The new state decides visibility right now. Waiting for the next bounds change left a bar
					// that was switched on after its content had already settled (a host pinning its height and
					// then asking for WhenRequired) hidden over content that did not fit.
					UpdateScrollBar();
				}
			}
		}

		/// <summary>
		/// Gets or sets whether the bar floats over the content instead of taking room beside it. A floating bar
		/// reserves no space, is drawn at <see cref="FloatingWidth"/> until hovered, is found by the pointer a
		/// <see cref="GrabMargin"/> short of it, and - when shown <see cref="ShowState.WhenRequired"/> - stays hidden
		/// until the pointer is over it or dragging it, fading in and back out as agg-gui's does. Off by default.
		/// </summary>
		public bool Floating
		{
			get => floating;
			set => SetAppearance(ref floating, value);
		}

		/// <summary>Gets or sets this bar's thickness in device pixels; null (the default) uses <see cref="ScrollBarWidth"/>.</summary>
		public double? BarWidth
		{
			get => barWidth;
			set => SetAppearance(ref barWidth, value);
		}

		/// <summary>Gets or sets the dormant thickness of a <see cref="Floating"/> bar, in device pixels; it grows to
		/// the full thickness on hover. 0 (the default) means no thin state.</summary>
		public double FloatingWidth
		{
			get => floatingWidth;
			set => SetAppearance(ref floatingWidth, value);
		}

		/// <summary>Gets or sets the shortest the thumb may get, in device pixels, so huge content keeps a grabbable
		/// thumb. 0 (the default) lets it shrink with the ratio of view to content.</summary>
		public double HandleMinLength
		{
			get => handleMinLength;
			set => SetAppearance(ref handleMinLength, value);
		}

		/// <summary>Gets or sets the gap, in device pixels, between the bar and the view's outer edge.</summary>
		public double OuterMargin
		{
			get => outerMargin;
			set => SetAppearance(ref outerMargin, value);
		}

		/// <summary>Gets or sets the gap, in device pixels, between the bar and the content.</summary>
		public double InnerMargin
		{
			get => innerMargin;
			set => SetAppearance(ref innerMargin, value);
		}

		/// <summary>Gets or sets the track colour; null (the default) keeps the colour the bar was built with.</summary>
		public Color? TrackColor
		{
			get => trackColor;
			set => SetAppearance(ref trackColor, value);
		}

		/// <summary>Gets or sets the thumb colour; null (the default) uses <see cref="DefaultThumbColor"/>.</summary>
		public Color? ThumbColor
		{
			get => thumbColor;
			set => SetAppearance(ref thumbColor, value);
		}

		/// <summary>Gets or sets the hovered thumb colour; null (the default) uses <see cref="DefaultThumbHoverColor"/>.</summary>
		public Color? ThumbHoverColor
		{
			get => thumbHoverColor;
			set => SetAppearance(ref thumbHoverColor, value);
		}

		/// <summary>The time the fade reads, in milliseconds; tests hold it to step the fade deterministically.</summary>
		internal Func<long> Clock { get; set; } = () => UiThread.CurrentTimerMs;

		/// <summary>The bar's full thickness in device pixels.</summary>
		public double Thickness => barWidth ?? ScrollBarWidth;

		/// <summary>Space the bar takes across the view: the thickness and both margins.</summary>
		internal double StripThickness => outerMargin + Thickness + innerMargin;

		/// <summary>
		/// How far past its strip, towards the content, a floating bar still counts as hovered - agg-gui's
		/// DEFAULT_GRAB_MARGIN of 6. A floating bar is thin or invisible until hovered, so the pointer needs more than
		/// the bar itself to find it. It widens only the hit area: the bar draws, and the corner is measured, as before.
		/// </summary>
		internal double GrabMargin => floating ? 6 * DeviceScale : 0;

		/// <summary>How opaque the bar is drawn, 0..1; below 1 only while a floating bar is hidden or fading.</summary>
		internal double Opacity => visibility.Value;

		/// <summary>Whether this bar takes room beside the content, so the view's scroll area steps in by it.</summary>
		internal bool ReservesSpace => Visible && !floating;

		internal double ThumbHeight => ThumbLength(VerticalTrackLength, parentScrollWidget.RatioOfViewToContents0To1().Y);

		/// <summary>The length a vertical bar runs along: the view's height less the horizontal bar at the bottom,
		/// so the two bars do not overlap in the corner.</summary>
		internal double VerticalTrackLength
		{
			get
			{
				ScrollBar horizontal = parentScrollWidget.HorizontalScrollBar;
				return Math.Max(0, parentScrollWidget.Height - (horizontal != null && horizontal.Visible ? horizontal.StripThickness : 0));
			}
		}

		/// <summary>The length a horizontal bar runs along: the view's width less the vertical bar's corner.</summary>
		private double HorizontalTrackLength => Math.Max(0, parentScrollWidget.Width - (parentScrollWidget.VerticalScrollBar.Visible ? parentScrollWidget.VerticalScrollBar.StripThickness : 0));

		/// <summary>A horizontal bar's thumb length: the track scaled by how much of the content's width is in view.</summary>
		internal double ThumbWidth => ThumbLength(HorizontalTrackLength, parentScrollWidget.RatioOfViewToContents0To1().X);

		/// <summary>The thumb widget, for tests that measure it.</summary>
		internal GuiWidget Thumb => thumb;

		/// <summary>The track widget, for tests that measure it.</summary>
		internal GuiWidget Track => background;

		private bool AutoHides => floating && showState == ShowState.WhenRequired;

		/// <summary>How long a full fade in or out takes, in milliseconds (agg-gui's visibility tween).</summary>
		internal const double FadeMs = 180;

		/// <summary>How long a floating bar takes to grow to full thickness on hover, or shrink back, in milliseconds
		/// (agg-gui's hover tween).</summary>
		internal const double GrowMs = 120;

		/// <summary>The thumb's length along a track: the share of the content in view, held to <see cref="HandleMinLength"/>.</summary>
		private double ThumbLength(double track, double viewRatio) => Math.Max(viewRatio * track, Math.Min(handleMinLength, track));

		/// <summary>The opacity an auto-hiding bar is heading for at <paramref name="nowMs"/>: shown while hovered or
		/// dragged, hidden otherwise. A scroll alone does not bring it up, as in agg-gui.</summary>
		internal double FadeTarget(long nowMs)
		{
			if (!AutoHides)
			{
				return 1;
			}

			return mouseInBounds || thumb.Dragging ? 1 : 0;
		}

		/// <summary>Points the fade and the hover growth at what the bar should be showing at
		/// <paramref name="nowMs"/>. An ease starts from this moment, so it is called as soon as hover or drag
		/// changes, not only when the bar next draws.</summary>
		private void SetTargets(long nowMs)
		{
			visibility.SetTarget(FadeTarget(nowMs), nowMs);
			hover.SetTarget(mouseInBounds || thumb.Dragging ? 1 : 0, nowMs);
		}

		/// <summary>Starts the eases for a change of hover or drag now, and asks for the draw that runs them.</summary>
		internal void Retarget()
		{
			SetTargets(Clock());
			Invalidate();
		}

		/// <summary>Moves the opacity and the hover growth to where their eases are at <paramref name="nowMs"/> and
		/// reports whether the bar needs drawing again.</summary>
		internal bool StepFade(long nowMs)
		{
			SetTargets(nowMs);
			bool changed = visibility.Step(nowMs);
			changed |= hover.Step(nowMs);
			if (changed)
			{
				UpdateScrollBar();
			}

			return visibility.Animating || hover.Animating;
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			if (AutoHides || floating || visibility.Value != 1)
			{
				if (StepFade(Clock()))
				{
					Invalidate();
				}
			}

			base.OnDraw(graphics2D);
		}

		private void SetAppearance<T>(ref T field, T value)
		{
			if (Equals(field, value))
			{
				return;
			}

			field = value;
			visibility.Reset(AutoHides ? 0 : 1);
			hover.Reset(mouseInBounds ? 1 : 0);
			parentScrollWidget.SetScrollAreaMargin();
			UpdateScrollBar();
			Invalidate();
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			// Only the position along the track decides, as agg-gui's page_at: anywhere across the bar level with the
			// thumb (grab margin included) is the thumb's, and the thumb takes that press as a drag.
			RectangleDouble thumbBounds = thumb.BoundsRelativeToParent;
			if (orientation == Orientation.Horizontal)
			{
				if (mouseEvent.X < thumbBounds.Left || mouseEvent.X > thumbBounds.Right)
				{
					// page towards the click, the way the vertical bar does
					MoveThumb(new Vector2(mouseEvent.X < thumbBounds.Left ? -thumb.Width : thumb.Width, 0));
				}
			}
			else if (mouseEvent.Y < thumbBounds.Bottom || mouseEvent.Y > thumbBounds.Top)
			{
				// we did not click on the thumb so we want to move the scroll bar towards the click
				MoveThumb(new Vector2(0, mouseEvent.Y < thumbBounds.Bottom ? -thumb.Height : thumb.Height));
			}

			base.OnMouseDown(mouseEvent);
		}

		public override void OnMouseEnterBounds(MouseEventArgs mouseEvent)
		{
			mouseInBounds = true;
			base.OnMouseEnterBounds(mouseEvent);

			this.UpdateScrollBar();
			Retarget();
		}

		public override void OnMouseLeaveBounds(MouseEventArgs mouseEvent)
		{
			mouseInBounds = false;
			base.OnMouseLeaveBounds(mouseEvent);

			this.UpdateScrollBar();
			Retarget();
		}

		internal void MoveThumb(Vector2 deltaToMove)
		{
			if (orientation == Orientation.Horizontal)
			{
				double travel = HorizontalTrackLength - ThumbWidth;
				if (travel > 0)
				{
					parentScrollWidget.SetScrollOffsetFromLeft(parentScrollWidget.ScrollOffsetFromLeft() + deltaToMove.X / travel * parentScrollWidget.MaxScrollFromLeft());
				}

				return;
			}

			double notThumbHeight = VerticalTrackLength - ThumbHeight;
			double changeRatio = deltaToMove.Y / notThumbHeight;
			parentScrollWidget.ScrollRatioFromTop0To1 += new Vector2(0, changeRatio);
		}

		private void Bounds_Changed(object sender, EventArgs e)
		{
			UpdateScrollBar();
		}

		/// <summary>
		/// Re-decides whether this bar should be showing and, if it is, where its thumb sits.
		/// </summary>
		/// <remarks>
		/// Driven by the events subscribed to in the constructor.
		/// </remarks>
		internal void UpdateScrollBar()
		{
			switch (Show)
			{
				case ShowState.WhenRequired:
					if (orientation == Orientation.Horizontal ? parentScrollWidget.MaxScrollFromLeft() > 0 : parentScrollWidget.HasVerticalOverflow)
					{
						goto case ShowState.Always;
					}
					else
					{
						goto case ShowState.Never;
					}

				case ShowState.Always:
					// make sure we can see it
					Visible = true;
					if (orientation == Orientation.Horizontal)
					{
						UpdateHorizontalBar();
						break;
					}

					// the strip runs from just above the horizontal bar (if any) to the top
					double track = VerticalTrackLength;
					double bottom = parentScrollWidget.Height - track;
					// the grab margin reaches left into the content (negative x), so the bar's own coordinates stay put
					LocalBounds = new RectangleDouble(-GrabMargin, bottom, StripThickness, parentScrollWidget.Height);

					// the bar sits between the inner margin (content side, left) and the outer margin (right edge)
					(double thumbLeft, double thumbRight) = ThumbBand(innerMargin, hugHigh: true);
					background.LocalBounds = new RectangleDouble(innerMargin, bottom, innerMargin + Thickness, parentScrollWidget.Height);
					double thumbHeight = ThumbHeight;
					thumb.LocalBounds = new RectangleDouble(thumbLeft, 0, thumbRight, thumbHeight);
					ApplyColors();

					Vector2 scrollRatioFromTop0To1 = parentScrollWidget.ScrollRatioFromTop0To1;
					thumb.OriginRelativeParent = new Vector2(0, bottom + (track - thumbHeight) * scrollRatioFromTop0To1.Y);
					break;

				case ShowState.Never:
					Visible = false;
					break;
			}

			// HACK: Workaround to fix problems with initial positioning - set padding on ScrollArea to force layout
			this.parentScrollWidget.ScrollArea.Padding = 0;
		}

		/// <summary>Lays a horizontal bar along the view's bottom, its thumb as far along as the content is scrolled.</summary>
		private void UpdateHorizontalBar()
		{
			double track = HorizontalTrackLength;
			// the grab margin reaches up into the content, above the bar
			LocalBounds = new RectangleDouble(0, 0, track, StripThickness + GrabMargin);

			// the bar sits between the outer margin (bottom edge) and the inner margin (content side, top)
			(double thumbBottom, double thumbTop) = ThumbBand(outerMargin, hugHigh: false);
			background.LocalBounds = new RectangleDouble(0, outerMargin, track, outerMargin + Thickness);
			double thumbWidth = ThumbWidth;
			thumb.LocalBounds = new RectangleDouble(0, thumbBottom, thumbWidth, thumbTop);
			ApplyColors();
			double max = parentScrollWidget.MaxScrollFromLeft();
			double along = max > 0 ? parentScrollWidget.ScrollOffsetFromLeft() / max : 0;
			thumb.OriginRelativeParent = new Vector2((track - thumbWidth) * along, 0);
		}

		/// <summary>
		/// Where the thumb sits across the bar that starts at <paramref name="barStart"/>: a floating bar with a thin
		/// width eases between it (against the view's outer edge - the high side when <paramref name="hugHigh"/>) and
		/// the full thickness as hover comes and goes; any other bar is full on hover and otherwise inset by
		/// <see cref="GrowThumbBy"/> on both sides.
		/// </summary>
		private (double Start, double End) ThumbBand(double barStart, bool hugHigh)
		{
			double full = Thickness;
			if (floating && floatingWidth > 0)
			{
				// eased from thin to full, like agg-gui's bar_width_at(hover)
				double thin = Math.Min(floatingWidth, full);
				double width = thin + (full - thin) * hover.Value;
				return hugHigh ? (barStart + full - width, barStart + full) : (barStart, barStart + width);
			}

			if (mouseInBounds)
			{
				return (barStart, barStart + full);
			}

			// a narrow bar keeps at least half its thickness, where the full inset would leave nothing to see
			double inset = Math.Min(GrowThumbBy, full / 4);
			return (barStart + inset, barStart + full - inset);
		}

		/// <summary>Colours the track and thumb for the hover state, faded by <see cref="Opacity"/>. A floating bar
		/// shows its track only while hovered.</summary>
		private void ApplyColors()
		{
			Color track = trackColor ?? constructedTrackColor;
			background.BackgroundColor = floating && !mouseInBounds ? Color.Transparent : Faded(track);
			thumb.BackgroundColor = Faded(mouseInBounds ? thumbHoverColor ?? DefaultThumbHoverColor : thumbColor ?? DefaultThumbColor);
		}

		private Color Faded(Color color) => Opacity >= 1 ? color : color.WithAlpha((int)Math.Round(color.alpha * Opacity));

		// Color is a multi-field struct, so unsynchronized cross-thread writes (e.g. from
		// ThemeConfig.RebuildTheme) could be observed torn or stale by ScrollBar constructors.
		private static readonly object defaultColorLocker = new object();

		private static Color defaultBackgroundColor = Color.LightGray;
		private static Color defaultThumbColor = Color.DarkGray;
		private static Color defaultThumbHoverColor = Color.DarkGray;

		public static Color DefaultBackgroundColor
		{
			get
			{
				lock (defaultColorLocker)
				{
					return defaultBackgroundColor;
				}
			}

			set
			{
				lock (defaultColorLocker)
				{
					defaultBackgroundColor = value;
				}
			}
		}

		public static Color DefaultThumbColor
		{
			get
			{
				lock (defaultColorLocker)
				{
					return defaultThumbColor;
				}
			}

			set
			{
				lock (defaultColorLocker)
				{
					defaultThumbColor = value;
				}
			}
		}

		public static Color DefaultThumbHoverColor
		{
			get
			{
				lock (defaultColorLocker)
				{
					return defaultThumbHoverColor;
				}
			}

			set
			{
				lock (defaultColorLocker)
				{
					defaultThumbHoverColor = value;
				}
			}
		}
	}

	public class ThumDragWidget : GuiWidget
	{
		private readonly Orientation orientation;

		private Vector2 mouseDownPosition;

		public ThumDragWidget(Orientation orientation)
		{
			this.orientation = orientation;
		}

		protected bool MouseDownOnThumb { get; set; }

		/// <summary>Whether the thumb is being dragged.</summary>
		internal bool Dragging => MouseDownOnThumb;

		/// <summary>
		/// Level with the thumb anywhere across its bar's hit area counts as on the thumb - agg-gui's pos_on_thumb,
		/// which includes the grab margin - so a press beside a thin floating thumb grabs it rather than paging.
		/// </summary>
		/// <remarks>The thumb only ever moves along the track, so across it the thumb and the bar share coordinates.</remarks>
		public override bool PositionWithinLocalBounds(double x, double y)
		{
			if (!(Parent is ScrollBar bar))
			{
				return base.PositionWithinLocalBounds(x, y);
			}

			RectangleDouble thumbBounds = LocalBounds;
			RectangleDouble barBounds = bar.LocalBounds;
			return orientation == Orientation.Vertical
				? y >= thumbBounds.Bottom && y <= thumbBounds.Top && x >= barBounds.Left && x <= barBounds.Right
				: x >= thumbBounds.Left && x <= thumbBounds.Right && y >= barBounds.Bottom && y <= barBounds.Top;
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			MouseDownOnThumb = true;
			mouseDownPosition = new Vector2(mouseEvent.X, mouseEvent.Y);
			(Parent as ScrollBar)?.Retarget();

			base.OnMouseDown(mouseEvent);
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			if (MouseDownOnThumb)
			{
				var mousePosition = new Vector2(mouseEvent.X, mouseEvent.Y);

				Vector2 deltaFromDownPosition = mousePosition - mouseDownPosition;

				if (orientation == Orientation.Vertical)
				{
					deltaFromDownPosition.X = 0;
				}
				else
				{
					deltaFromDownPosition.Y = 0;
				}

				var parentScrollBar = (ScrollBar)Parent;
				parentScrollBar.MoveThumb(deltaFromDownPosition);
			}

			base.OnMouseMove(mouseEvent);
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			MouseDownOnThumb = false;

			// a drag held the bar up and full width; released off the bar, it now fades and thins
			(Parent as ScrollBar)?.Retarget();
			base.OnMouseUp(mouseEvent);
		}
	}
}