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

		public Color TrackColor { get; set; }

		public double TrackHeight { get; set; }

		public TickPlacement TextPlacement { get; set; }

		public Color TextColor { get; set; }

		public StyledTypeFace TextStyle { get; set; }

		public Color ThumbColor { get; set; }

		public TickPlacement TickPlacement { get; set; }

		public Color TickColor { get; set; }

		public SlideView(Slider sliderWidget)
		{
			sliderAttachedTo = sliderWidget;

			TrackHeight = 3;

			TextColor = Color.Black;
			TrackColor = new Color(220, 220, 220);
			ThumbColor = DefaultViewFactory.DefaultBlue;

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
				textPosition = new Vector2(0, -24);
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

		public void DoDrawAfterChildren(Graphics2D graphics2D)
		{
			RoundedRect track = new RoundedRect(GetTrackBounds(), TrackHeight / 2);
			Vector2 ValuePrintPosition;
			if (sliderAttachedTo.Orientation == Orientation.Horizontal)
			{
				ValuePrintPosition = new Vector2(sliderAttachedTo.TotalWidthInPixels / 2, -sliderAttachedTo.ThumbHeight - 12);
			}
			else
			{
				ValuePrintPosition = new Vector2(0, -sliderAttachedTo.ThumbHeight - 12);
			}

			// draw the track
			graphics2D.Render(track, TrackColor);

			// now do the thumb
			RectangleDouble thumbBounds = sliderAttachedTo.GetThumbHitBounds();
			RoundedRect thumbOutside = new RoundedRect(thumbBounds, sliderAttachedTo.ThumbWidth / 2);
			graphics2D.Render(thumbOutside, ColorF.GetTweenColor(ThumbColor.GetAsRGBA_Floats(), ColorF.Black.GetAsRGBA_Floats(), .2).GetAsRGBA_Bytes());
			thumbBounds.Inflate(-1);
			RoundedRect thumbInside = new RoundedRect(thumbBounds, sliderAttachedTo.ThumbWidth / 2);
			graphics2D.Render(thumbInside, ThumbColor);
		}
	}

	public class Slider : GuiWidget
	{
		internal TextWidget sliderTextWidget; // this will print the 'Text' object for this widget.

		public event EventHandler ValueChanged;

		public event EventHandler SliderReleased;

		public SlideView View { get; set; }

		private double mouseDownOffsetFromThumbCenter;
		private bool downOnThumb = false;

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
			}
		}

		public double Value
		{
			get
			{
				return Minimum + (Maximum - Minimum) * Position0To1;
			}
			set
			{
				double newPosition0To1 = Math.Max(0, Math.Min((value - Minimum) / (Maximum - Minimum), 1));
				if (newPosition0To1 != Position0To1)
				{
					Position0To1 = newPosition0To1;
					if (ValueChanged != null)
					{
						ValueChanged(this, null);
					}
					Invalidate();
				}
			}
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

		public double TotalWidthInPixels { get; set; }

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

		public double Minimum { get; set; }

		public double Maximum { get; set; }

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
			ThumbWidth = 10;
			ThumbHeight = 20;

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
			}
			else // let's check if we are on the track
			{
				RectangleDouble trackHitBounds = GetTrackHitBounds();
				if (trackHitBounds.Contains(mousePos))
				{
					if (Orientation == Orientation.Horizontal)
					{
						PositionPixelsFromFirstValue = mousePos.X;
					}
					else
					{
						PositionPixelsFromFirstValue = mousePos.Y;
					}
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
				if (Orientation == Orientation.Horizontal)
				{
					PositionPixelsFromFirstValue = mousePos.X - mouseDownOffsetFromThumbCenter;
				}
				else
				{
					PositionPixelsFromFirstValue = mousePos.Y - mouseDownOffsetFromThumbCenter;
				}
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

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			downOnThumb = false;
			base.OnMouseUp(mouseEvent);
			if (valueOnMouseDown != Value && SliderReleased != null)
			{
				SliderReleased(this, mouseEvent);
			}
		}
	}
}