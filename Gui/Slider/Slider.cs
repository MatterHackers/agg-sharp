using MatterHackers.Agg.Font;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;

//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.4
// Copyright (C) 2002-2005 Maxim Shemanarev (http://www.antigrain.com)
//
// C# port by: Lars Brubaker
//                  larsbrubaker@gmail.com
// Copyright (C) 2007
//
// Permission to copy, use, modify, sell and distribute this software
// is granted provided this copyright notice appears in all copies.
// This software is provided "as is" without express or implied
// warranty, and with no claim as to its suitability for any purpose.
//
//----------------------------------------------------------------------------
// Contact: mcseem@antigrain.com
//          mcseemagg@yahoo.com
//          http://www.antigrain.com
//----------------------------------------------------------------------------
//
// classes slider_ctrl_impl, slider_ctrl
//
//----------------------------------------------------------------------------
using System;

namespace MatterHackers.Agg.UI
{
	public enum Orientation { Horizontal, Vertical };

	public enum TickPlacement { None, BottomLeft, TopRight, Both };

	public class SlideView
	{
		private Slider sliderAttachedTo;

		public Color BackgroundColor { get; set; }

		private Color? trackColor;

		/// <summary>
		/// The unfilled part of the track. Unless set, agg-gui's track_bg for <see cref="ThemeConfig.Current"/>,
		/// read at draw time so a theme swap reaches it.
		/// </summary>
		public Color TrackColor
		{
			get => trackColor ?? SelectionControlStyle.TrackBackground(ThemeConfig.Current);
			set => trackColor = value;
		}

		public double TrackHeight { get; set; }

		public TickPlacement TextPlacement { get; set; }

		public Color TextColor { get; set; }

		public StyledTypeFace TextStyle { get; set; }

		private Color? thumbColor;

		/// <summary>
		/// The accent: the thumb's ring and the track filled in left of (below) it. Unless set,
		/// <see cref="ThemeConfig.Current"/>'s primary accent, read at draw time.
		/// </summary>
		public Color ThumbColor
		{
			get => thumbColor ?? ThemeConfig.Current.PrimaryAccentColor;
			set => thumbColor = value;
		}

		public TickPlacement TickPlacement { get; set; }

		/// <summary>Fill the track in the accent up to the thumb. On by default.</summary>
		public bool TrailingFill { get; set; } = true;

		/// <summary>The thumb's shape. A ring by default.</summary>
		public SliderHandleShape HandleShape { get; set; }

		public Color TickColor { get; set; }

		public SlideView(Slider sliderWidget)
		{
			sliderAttachedTo = sliderWidget;

			TrackHeight = 3 * GuiWidget.DeviceScale;

			TextColor = Color.Black;

			sliderWidget.ValueChanged += new EventHandler(sliderWidget_ValueChanged);
			sliderWidget.TextChanged += new EventHandler(sliderWidget_TextChanged);

			SetFormatStringForText();
		}

		private void SetFormatStringForText()
		{
			if (sliderAttachedTo.Text != "")
			{
				string stringWithValue = string.Format(sliderAttachedTo.Text, sliderAttachedTo.Value);
				sliderAttachedTo.sliderTextWidget.Text = stringWithValue;
				Vector2 textPosition = GetTextPosition();
				sliderAttachedTo.sliderTextWidget.OriginRelativeParent = textPosition;
			}
		}

		private void sliderWidget_TextChanged(object sender, EventArgs e)
		{
			SetFormatStringForText();
		}

		private void sliderWidget_ValueChanged(object sender, EventArgs e)
		{
			SetFormatStringForText();
		}

		private RectangleDouble GetTrackBounds()
		{
			RectangleDouble trackBounds;
			if (sliderAttachedTo.Orientation == Orientation.Horizontal)
			{
				trackBounds = new RectangleDouble(0, -TrackHeight / 2, sliderAttachedTo.TotalWidthInPixels, TrackHeight / 2);
			}
			else
			{
				trackBounds = new RectangleDouble(-TrackHeight / 2, 0, TrackHeight / 2, sliderAttachedTo.TotalWidthInPixels);
			}
			return trackBounds;
		}

		private RectangleDouble GetThumbBounds()
		{
			RectangleDouble thumbBounds = sliderAttachedTo.GetThumbHitBounds();
			return thumbBounds;
		}

		private Vector2 GetTextPosition()
		{
			Vector2 textPosition;
			if (sliderAttachedTo.Orientation == Orientation.Horizontal)
			{
				double textHeight = 0;
				if (sliderAttachedTo.sliderTextWidget.Text != "")
				{
					textHeight = sliderAttachedTo.sliderTextWidget.Printer.TypeFaceStyle.EmSizeInPixels;
				}
				textPosition = new Vector2(sliderAttachedTo.TotalWidthInPixels / 2, GetThumbBounds().Bottom - textHeight);
			}
			else
			{
				textPosition = new Vector2(0, -24 * GuiWidget.DeviceScale);
			}

			return textPosition;
		}

		public RectangleDouble GetTotalBounds()
		{
			RectangleDouble totalBounds = GetTrackBounds();
			totalBounds.ExpandToInclude(GetThumbBounds());
			if (sliderAttachedTo.sliderTextWidget.Text != "")
			{
				totalBounds.ExpandToInclude(sliderAttachedTo.sliderTextWidget.BoundsRelativeToParent);
			}

			return totalBounds;
		}

		public void DoDrawBeforeChildren(Graphics2D graphics2D)
		{
			// erase to the background color
			graphics2D.FillRectangle(GetTotalBounds(), BackgroundColor);
		}

		/// <summary>
		/// Draws agg-gui's slider: a rounded track whose part before the thumb is filled in the accent, and a
		/// round thumb - an accent ring around the widget background - that lightens under the mouse and
		/// darkens while dragged. Everything stays inside the thumb's hit bounds, so the derived LocalBounds
		/// (and every layout built on them) are what they were before the restyle.
		/// </summary>
		public void DoDrawAfterChildren(Graphics2D graphics2D)
		{
			Color accent = ThumbColor;
			Color track = TrackColor;
			if (!sliderAttachedTo.Enabled)
			{
				// agg-gui fades a disabled widget; the accent going grey says "not now" in both themes.
				accent = TrackColor.WithAlpha(Math.Min(255, TrackColor.Alpha0To255 * 2));
				track = TrackColor.WithAlpha(TrackColor.Alpha0To255 / 2);
			}

			RectangleDouble trackBounds = GetTrackBounds();
			graphics2D.Render(new RoundedRect(trackBounds, TrackHeight / 2), track);

			// The filled part runs from the start of the track to the thumb's center.
			RectangleDouble filled = trackBounds;
			if (sliderAttachedTo.Orientation == Orientation.Horizontal)
			{
				filled.Right = sliderAttachedTo.PositionPixelsFromFirstValue;
			}
			else
			{
				filled.Top = sliderAttachedTo.PositionPixelsFromFirstValue;
			}

			if (TrailingFill && filled.Width > 0 && filled.Height > 0)
			{
				graphics2D.Render(new RoundedRect(filled, TrackHeight / 2), accent);
			}

			Color ring = accent;
			if (sliderAttachedTo.Enabled)
			{
				if (sliderAttachedTo.IsDraggingThumb)
				{
					ring = ColorF.GetTweenColor(accent.ToColorF(), ColorF.Black.ToColorF(), .18).ToColor();
				}
				else if (sliderAttachedTo.UnderMouseState != UnderMouseState.NotUnderMouse)
				{
					ring = ColorF.GetTweenColor(accent.ToColorF(), ColorF.White.ToColorF(), .18).ToColor();
				}
			}

			// A circle as wide as the thumb's narrow side, so it never pokes out of the hit bounds (which
			// are the widget's bounds at either end of the track).
			RectangleDouble thumbBounds = sliderAttachedTo.GetThumbHitBounds();
			Vector2 center = thumbBounds.Center;
			double radius = Math.Min(thumbBounds.Width, thumbBounds.Height) / 2;
			if (HandleShape == SliderHandleShape.Rectangle)
			{
				// agg-gui's rectangle handle: 0.9 of the radius across the track, half of it along.
				bool horizontal = sliderAttachedTo.Orientation == Orientation.Horizontal;
				double along = radius * .5;
				double across = radius * .9;
				var handle = horizontal
					? new RectangleDouble(center.X - along, center.Y - across, center.X + along, center.Y + across)
					: new RectangleDouble(center.X - across, center.Y - along, center.X + across, center.Y + along);
				graphics2D.Render(new RoundedRect(handle, 2 * GuiWidget.DeviceScale), ring);
				return;
			}

			graphics2D.Render(new Ellipse(center, radius), ring);

			// agg-gui's ring is 2.5 of its 7 radius; the same proportion keeps it a ring at any thumb size.
			Color thumbCenter = BackgroundColor.Alpha0To255 > 0 ? BackgroundColor : ThemeConfig.Current.BackgroundColor;
			graphics2D.Render(new Ellipse(center, radius * 4.5 / 7), thumbCenter);
		}
	}

	public class Slider : GuiWidget
	{
		internal TextWidget sliderTextWidget; // this will print the 'Text' object for this widget.

		public event EventHandler ValueChanged;

		public event EventHandler SliderReleased;
		
		public event EventHandler SliderGrabed;

		public SlideView View { get; set; }

		private double mouseDownOffsetFromThumbCenter;
		private bool downOnThumb = false;

		/// <summary>
		/// True while the thumb is held by the mouse; the view darkens it for the drag.
		/// </summary>
		public bool IsDraggingThumb => downOnThumb;

		private double position0To1;
		private double thumbHeight;
		private int numTicks = 0;

		public double Position0To1
		{
			get
			{
				return position0To1;
			}

			set
			{
				position0To1 = Math.Max(0, Math.Min(value, 1));
				exactValue = null;

				// LocalBounds here is derived (track, thumb and value text), so moving the thumb changes the
				// bounds without anything writing them. Nothing else will mark the clipping stale.
				InvalidateScreenClipping();
			}
		}

		public double Value
		{
			get
			{
				if (!UsesValueRules)
				{
					return Minimum + (Maximum - Minimum) * Position0To1;
				}

				// The committed value, not the one read back off the position: a log mapping or a value outside
				// the range (Clamping.Never) does not survive the round trip.
				return exactValue ?? ValueAtPosition(Position0To1);
			}
			set
			{
				if (!UsesValueRules)
				{
					double newPosition0To1 = Math.Max(0, Math.Min((value - Minimum) / (Maximum - Minimum), 1));
					if (newPosition0To1 != Position0To1)
					{
						Position0To1 = newPosition0To1;
						ValueChanged?.Invoke(this, null);
						Invalidate();
					}

					return;
				}

				if (SetCommittedValue(value, fromUser: false))
				{
					ValueChanged?.Invoke(this, null);
					Invalidate();
				}
			}
		}

		// Set only while value rules are on; any move of the position that did not come from a value clears it.
		private double? exactValue;

		/// <summary>
		/// Map values logarithmically, for a range spanning orders of magnitude (it may include zero and
		/// infinity). Off by default.
		/// </summary>
		public bool Logarithmic { get => logarithmic; set => ChangeRule(() => logarithmic = value); }

		private bool logarithmic;

		/// <summary>On a logarithmic slider reaching zero, the smallest positive value it can pick. 1 when <see cref="Integer"/>.</summary>
		public double SmallestPositive { get => smallestPositive; set => ChangeRule(() => smallestPositive = value); }

		private double smallestPositive = 1e-6;

		/// <summary>On a logarithmic slider reaching infinity, the largest finite value before it jumps to infinity.</summary>
		public double LargestFinite { get => largestFinite; set => ChangeRule(() => largestFinite = value); }

		private double largestFinite = double.PositiveInfinity;

		/// <summary>When the value is clamped to Minimum..Maximum. Always by default, as a Slider always has.</summary>
		public SliderClamping Clamping { get => clamping; set => ChangeRule(() => clamping = value); }

		private SliderClamping clamping = SliderClamping.Always;

		/// <summary>Values snap to multiples of this, counted from Minimum. 0 (the default) is no step.</summary>
		public double Step { get => step; set => ChangeRule(() => step = value); }

		private double step;

		/// <summary>Values round to whole numbers.</summary>
		public bool Integer { get => integer; set => ChangeRule(() => integer = value); }

		private bool integer;

		/// <summary>Dragging snaps to the roundest value under the pointer (250 rather than 247.23). Off by default.</summary>
		public bool SmartAim { get => smartAim; set => ChangeRule(() => smartAim = value); }

		private bool smartAim;

		/// <summary>
		/// The arrow keys along the slider's axis move a focused slider by <see cref="Step"/> (or one pixel when
		/// there is no step). Off by default so an app's own arrow-key handling keeps its keys.
		/// </summary>
		public bool KeyboardStepping { get; set; }

		/// <summary>
		/// Any of the value options is on. Off, the slider is exactly the linear, always clamped one it has always
		/// been; on, values go through <see cref="SliderMath"/>.
		/// </summary>
		private bool UsesValueRules => Logarithmic || Step > 0 || Integer || SmartAim || Clamping != SliderClamping.Always;

		private double EffectiveSmallestPositive => Integer ? Math.Max(1, SmallestPositive) : SmallestPositive;

		private double ValueAtPosition(double position0To1) => SliderMath.ValueFromNormalized(position0To1, Minimum, Maximum, Logarithmic, EffectiveSmallestPositive, LargestFinite);

		/// <summary>The 0..1 position a value is drawn at (pinned to the ends when outside the range).</summary>
		public double PositionOfValue(double value)
		{
			return UsesValueRules
				? SliderMath.NormalizedFromValue(value, Minimum, Maximum, Logarithmic, EffectiveSmallestPositive, LargestFinite)
				: Math.Max(0, Math.Min((value - Minimum) / (Maximum - Minimum), 1));
		}

		/// <summary>
		/// Changes a range or value option. With value rules on (before or after), the value from before the
		/// change is committed again under the new rules - re-clamped and the thumb moved to it - rather than the
		/// old position being read back through the new mapping. With them off throughout, nothing but the field
		/// changes, as before these options existed.
		/// </summary>
		private void ChangeRule(Action change)
		{
			bool hadRules = UsesValueRules;
			double oldValue = Value;
			change();
			if (!hadRules && !UsesValueRules)
			{
				return;
			}

			bool changed;
			if (UsesValueRules)
			{
				changed = SetCommittedValue(oldValue, fromUser: false);
			}
			else
			{
				// The last option went off: back to a position-held value, placed where the value was.
				double position = Position0To1;
				Position0To1 = PositionOfValue(oldValue);
				changed = Value != oldValue;
				if (position != Position0To1)
				{
					Invalidate();
				}
			}

			if (changed)
			{
				ValueChanged?.Invoke(this, null);
			}
		}

		/// <summary>Commits a value through the rules without raising events. Returns whether it changed.</summary>
		private bool SetCommittedValue(double value, bool fromUser)
		{
			bool clamp = Clamping == SliderClamping.Always || (fromUser && Clamping == SliderClamping.Edits);
			double committed = SliderMath.Commit(value, Minimum, Maximum, clamp, Step, Integer);
			if (double.IsNaN(committed))
			{
				// One bad write must not poison the value.
				return false;
			}

			double oldValue = Value;
			double oldPosition = Position0To1;
			Position0To1 = PositionOfValue(committed);
			exactValue = committed;

			// A re-commit (a range or option change) can move the thumb while the value stays, and callers only
			// repaint for a value change.
			if (Position0To1 != oldPosition)
			{
				Invalidate();
			}

			return committed != oldValue;
		}

		/// <summary>The value the pointer at <paramref name="pixels"/> along the track picks, smart aim included.</summary>
		private double ValueAtPixels(double pixels)
		{
			double PositionAt(double px) => TrackWidth > 0 ? Math.Max(0, Math.Min((px - ThumbWidth / 2) / TrackWidth, 1)) : 0;
			if (!SmartAim)
			{
				return ValueAtPosition(PositionAt(pixels));
			}

			// agg-gui's aim radius: the roundest value within a pixel and a half either side of the pointer.
			double aim = 1.5 * GuiWidget.DeviceScale;
			return SliderMath.BestInRange(ValueAtPosition(PositionAt(pixels - aim)), ValueAtPosition(PositionAt(pixels + aim)));
		}

		/// <summary>Moves the thumb to a pointer position, through the value rules when they are on.</summary>
		private void MoveThumbTo(double pixels)
		{
			if (UsesValueRules)
			{
				SetCommittedValue(ValueAtPixels(pixels), fromUser: true);
			}
			else
			{
				PositionPixelsFromFirstValue = pixels;
			}
		}

		/// <summary>
		/// The value one arrow-key press moves to: a <see cref="Step"/> on, or with no step the value one pixel
		/// along the track (smart aimed when <see cref="SmartAim"/>). <paramref name="direction"/> is +1 or -1.
		/// </summary>
		public double NudgedValue(int direction)
		{
			if (Step > 0)
			{
				return Value + direction * Step;
			}

			double pixels = PositionPixelsFromFirstValue + direction * GuiWidget.DeviceScale;
			if (!SmartAim)
			{
				return ValueAtPixels(pixels);
			}

			double PositionAt(double px) => TrackWidth > 0 ? Math.Max(0, Math.Min((px - ThumbWidth / 2) / TrackWidth, 1)) : 0;
			return SliderMath.BestInRange(ValueAtPosition(PositionAt(pixels - .49)), ValueAtPosition(PositionAt(pixels + .49)));
		}

		public override void OnKeyDown(KeyEventArgs keyEvent)
		{
			if (KeyboardStepping && Enabled)
			{
				int direction = (keyEvent.KeyCode, Orientation) switch
				{
					(Keys.Left, Orientation.Horizontal) => -1,
					(Keys.Right, Orientation.Horizontal) => 1,
					(Keys.Down, Orientation.Vertical) => -1,
					(Keys.Up, Orientation.Vertical) => 1,
					_ => 0,
				};

				if (direction != 0)
				{
					// A key step is a whole edit: grabbed, moved and released, so apps that commit on release
					// (SliderReleased) take it.
					SliderGrabed?.Invoke(this, keyEvent);
					double oldValue = Value;
					if (UsesValueRules)
					{
						SetCommittedValue(NudgedValue(direction), fromUser: true);
					}
					else
					{
						Position0To1 = PositionOfValue(NudgedValue(direction));
					}

					if (oldValue != Value)
					{
						ValueChanged?.Invoke(this, keyEvent);
						Invalidate();
					}

					SliderReleased?.Invoke(this, keyEvent);

					keyEvent.Handled = true;
					keyEvent.SuppressKeyPress = true;
				}
			}

			base.OnKeyDown(keyEvent);
		}

		public override string Text
		{
			get
			{
				return base.Text;
			}
			set
			{
				sliderTextWidget.Text = value;
				base.Text = value;

				// The value readout joins the derived bounds only once it has text in it, so going from no
				// text to text grows this widget without writing its bounds.
				InvalidateScreenClipping();
			}
		}

		public double PositionPixelsFromFirstValue
		{
			get
			{
				return ThumbWidth / 2 + TrackWidth * Position0To1;
			}
			set
			{
				Position0To1 = (value - ThumbWidth / 2) / TrackWidth;
			}
		}

		public Orientation Orientation { get; set; }

		public double ThumbWidth { get; set; }

		public double ThumbHeight
		{
			get
			{
				return Math.Max(thumbHeight, ThumbWidth);
			}
			set
			{
				thumbHeight = value;
			}
		}

		public double TotalWidthInPixels
		{
			get => totalWidthInPixels;

			set
			{
				totalWidthInPixels = value;

				// The track is as long as this, and the track is most of the derived LocalBounds.
				InvalidateScreenClipping();
			}
		}

		private double totalWidthInPixels;

		public double TrackWidth
		{
			get
			{
				return TotalWidthInPixels - ThumbWidth;
			}
		}

		/// <summary>
		/// There will always be 0 or at least two ticks, one at the start and one at the end.
		/// </summary>
		public int NumTicks
		{
			get
			{
				return numTicks;
			}

			set
			{
				numTicks = value;
				if (numTicks == 1)
				{
					numTicks = 2;
				}
			}
		}

		public bool SnapToTicks { get; set; }

		/// <summary>
		/// The value at the start of the track. With no value option on, changing the range keeps the thumb where
		/// it is (so the value moves), as a Slider always has; with one on, it keeps the value and moves the thumb.
		/// </summary>
		public double Minimum { get => minimum; set => ChangeRule(() => minimum = value); }

		private double minimum;

		/// <summary>The value at the end of the track. See <see cref="Minimum"/> for what a change keeps.</summary>
		public double Maximum { get => maximum; set => ChangeRule(() => maximum = value); }

		private double maximum;

		public bool SmallChange { get; set; }

		public bool LargeChange { get; set; }

		public Slider(Vector2 positionOfTrackFirstValue, double widthInPixels, double minimum = 0, double maximum = 1, Orientation orientation = UI.Orientation.Horizontal)
		{
			sliderTextWidget = new TextWidget("", 0, 0, justification: Justification.Center);
			sliderTextWidget.AutoExpandBoundsToText = true;
			AddChild(sliderTextWidget);

			View = new SlideView(this);
			OriginRelativeParent = positionOfTrackFirstValue;
			TotalWidthInPixels = widthInPixels;
			Orientation = orientation;
			Minimum = minimum;
			Maximum = maximum;
			// The caller sizes the track (TotalWidthInPixels is device pixels and theirs to scale), but the
			// thumb is ours, so its defaults have to carry the device scale themselves or a Retina slider ends
			// up a hairline thumb beside doubled text.
			ThumbWidth = 10 * GuiWidget.DeviceScale;
			ThumbHeight = 20 * GuiWidget.DeviceScale;

			MinimumSize = new Vector2(Width, Height);
		}

		public Slider(Vector2 lowerLeft, Vector2 upperRight)
			: this(new Vector2(lowerLeft.X, lowerLeft.Y + (upperRight.Y - lowerLeft.Y) / 2), upperRight.X - lowerLeft.X)
		{
		}

		public Slider(double lowerLeftX, double lowerLeftY, double upperRightX, double upperRightY)
			: this(new Vector2(lowerLeftX, lowerLeftY + (upperRightY - lowerLeftY) / 2), upperRightX - lowerLeftX)
		{
		}

		public override RectangleDouble LocalBounds
		{
			get
			{
				return View.GetTotalBounds();
			}
			set
			{
				//OriginRelativeParent = new Vector2(value.Left, value.Bottom - View.GetTotalBounds().Bottom);
				//throw new Exception("Figure out what this should do.");
			}
		}

		public void SetRange(double minimum, double maximum)
		{
			Minimum = minimum;
			Maximum = maximum;
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			View.DoDrawBeforeChildren(graphics2D);
			base.OnDraw(graphics2D);
			View.DoDrawAfterChildren(graphics2D);
		}

		public RectangleDouble GetThumbHitBounds()
		{
			if (Orientation == Orientation.Horizontal)
			{
				return new RectangleDouble(-ThumbWidth / 2 + PositionPixelsFromFirstValue, -ThumbHeight / 2,
					ThumbWidth / 2 + PositionPixelsFromFirstValue, ThumbHeight / 2);
			}
			else
			{
				return new RectangleDouble(-ThumbHeight / 2, -ThumbWidth / 2 + PositionPixelsFromFirstValue,
					ThumbHeight / 2, ThumbWidth / 2 + PositionPixelsFromFirstValue);
			}
		}

		public double GetPosition0To1FromValue(double value)
		{
			return (value - Minimum) / (Maximum - Minimum);
		}

		public double GetPositionPixelsFromValue(double value)
		{
			return ThumbWidth / 2 + TrackWidth * GetPosition0To1FromValue(value);
		}

		public RectangleDouble GetTrackHitBounds()
		{
			if (Orientation == Orientation.Horizontal)
			{
				return new RectangleDouble(0, -ThumbHeight / 2,
					TotalWidthInPixels, ThumbHeight / 2);
			}
			else
			{
				return new RectangleDouble(-ThumbHeight / 2, 0, ThumbHeight / 2, TotalWidthInPixels);
			}
		}

		private double valueOnMouseDown;

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			valueOnMouseDown = Value;
			double oldValue = Value;
			Vector2 mousePos = mouseEvent.Position;
			RectangleDouble thumbBounds = GetThumbHitBounds();
			if (thumbBounds.Contains(mousePos))
			{
				if (Orientation == Orientation.Horizontal)
				{
					mouseDownOffsetFromThumbCenter = mousePos.X - PositionPixelsFromFirstValue;
				}
				else
				{
					mouseDownOffsetFromThumbCenter = mousePos.Y - PositionPixelsFromFirstValue;
				}
				downOnThumb = true;
				SliderGrabed?.Invoke(this, mouseEvent);
			}
			else // let's check if we are on the track
			{
				RectangleDouble trackHitBounds = GetTrackHitBounds();
				if (trackHitBounds.Contains(mousePos))
				{
					MoveThumbTo(Orientation == Orientation.Horizontal ? mousePos.X : mousePos.Y);
				}
			}

			if (oldValue != Value)
			{
				if (ValueChanged != null)
				{
					ValueChanged(this, mouseEvent);
				}
				Invalidate();
			}

			base.OnMouseDown(mouseEvent);
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			Vector2 mousePos = mouseEvent.Position;
			if (downOnThumb)
			{
				double oldValue = Value;
				MoveThumbTo((Orientation == Orientation.Horizontal ? mousePos.X : mousePos.Y) - mouseDownOffsetFromThumbCenter);
				if (oldValue != Value)
				{
					if (ValueChanged != null)
					{
						ValueChanged(this, mouseEvent);
					}
					Invalidate();
				}
			}
			base.OnMouseMove(mouseEvent);
		}

		public override void OnMouseEnterBounds(MouseEventArgs mouseEvent)
		{
			// Hover shades the thumb, so the change has to be painted.
			Invalidate();
			base.OnMouseEnterBounds(mouseEvent);
		}

		public override void OnMouseLeaveBounds(MouseEventArgs mouseEvent)
		{
			Invalidate();
			base.OnMouseLeaveBounds(mouseEvent);
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			downOnThumb = false;
			Invalidate();
			base.OnMouseUp(mouseEvent);

			SliderReleased?.Invoke(this, mouseEvent);
		}
	}
}