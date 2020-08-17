/*
Copyright (c) 2014, Lars Brubaker
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
using System.Linq;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.UI
{
	public class ScrollBar : GuiWidget
	{
		private readonly GuiWidget background;

		private readonly ScrollableWidget parentScrollWidget;

		private readonly ThumDragWidget thumb;

		private bool mouseInBounds = false;

		private ShowState showState = ShowState.WhenRequired;

		internal ScrollBar(ScrollableWidget parent, Orientation orientation = Orientation.Vertical)
			: this(parent, new DefaultThumbBackground(), new DefaultThumbView(), orientation)
		{
		}

		internal ScrollBar(ScrollableWidget parent, GuiWidget background, GuiWidget thumbView, Orientation orientation = Orientation.Vertical)
		{
			parentScrollWidget = parent;

			this.background = background;
			thumb = new ThumDragWidget(orientation);
			thumb.AddChild(thumbView);

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

		/// <summary>
		/// Gets or sets the amount to grow each side of the thumb in Y on Hover
		/// </summary>
		public static int GrowThumbBy { get; set; } = 3;

		public static double ScrollBarWidth { get; set; } = 15 * GuiWidget.DeviceScale;

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
					showState = value;
					switch (showState)
					{
						case ShowState.Never:
							Visible = false;
							break;

						case ShowState.WhenRequired:
							break;

						case ShowState.Always:
							break;

						default:
							throw new NotImplementedException();
					}
				}
			}
		}

		internal double ThumbHeight
		{
			get
			{
				Vector2 ratioOfViewToContents0To1 = parentScrollWidget.RatioOfViewToContents0To1();
				return ratioOfViewToContents0To1.Y * parentScrollWidget.Height;
			}
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			if (!thumb.BoundsRelativeToParent.Contains(mouseEvent.X, mouseEvent.Y))
			{
				// we did not click on the thumb so we want to move the scroll bar towards the click
				if (mouseEvent.Y < thumb.OriginRelativeParent.Y)
				{
					MoveThumb(new Vector2(0, -thumb.Height));
				}
				else
				{
					MoveThumb(new Vector2(0, thumb.Height));
				}
			}

			base.OnMouseDown(mouseEvent);
		}

		public override void OnMouseEnterBounds(MouseEventArgs mouseEvent)
		{
			mouseInBounds = true;
			base.OnMouseEnterBounds(mouseEvent);

			this.UpdateScrollBar();
		}

		public override void OnMouseLeaveBounds(MouseEventArgs mouseEvent)
		{
			mouseInBounds = false;
			base.OnMouseLeaveBounds(mouseEvent);

			this.UpdateScrollBar();
		}

		internal void MoveThumb(Vector2 deltaToMove)
		{
			double notThumbHeight = parentScrollWidget.Height - ThumbHeight;
			double changeRatio = deltaToMove.Y / notThumbHeight;
			parentScrollWidget.ScrollRatioFromTop0To1 += new Vector2(0, changeRatio);
		}

		private void Bounds_Changed(object sender, EventArgs e)
		{
			UpdateScrollBar();
		}

		private void UpdateScrollBar()
		{
			switch (Show)
			{
				case ShowState.WhenRequired:
					if (parentScrollWidget.ScrollArea.Height > parentScrollWidget.Height)
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
					// fix the bounds of the scroll bar background
					LocalBounds = new RectangleDouble(0, 0, ScrollBarWidth, parentScrollWidget.Height);
					background.LocalBounds = LocalBounds;

					// On hover, grow the thumb bounds by the given value
					int growAmount = mouseInBounds ? 0 : ScrollBar.GrowThumbBy;
					thumb.LocalBounds = new RectangleDouble(growAmount, 0, ScrollBarWidth - growAmount, ThumbHeight);

					Vector2 scrollRatioFromTop0To1 = parentScrollWidget.ScrollRatioFromTop0To1;
					double notThumbHeight = parentScrollWidget.Height - ThumbHeight;
					thumb.OriginRelativeParent = new Vector2(0, notThumbHeight * scrollRatioFromTop0To1.Y);
					break;

				case ShowState.Never:
					Visible = false;
					break;
			}

			// HACK: Workaround to fix problems with initial positioning - set padding on ScrollArea to force layout
			this.parentScrollWidget.ScrollArea.Padding = 0;
		}

		public class DefaultThumbBackground : GuiWidget
		{
			public DefaultThumbBackground()
			{
				this.BackgroundColor = DefaultBackgroundColor;
			}

			public static Color DefaultBackgroundColor { get; set; } = Color.LightGray;
		}

		public class DefaultThumbView : GuiWidget
		{
			public static Color ThumbColor { get; set; } = Color.DarkGray;

			public override void OnDraw(Graphics2D graphics2D)
			{
				var rect = new RoundedRect(LocalBounds, 0);
				// RoundedRect rect = new RoundedRect(LocalBounds, 0);
				graphics2D.Render(rect, ThumbColor);
				base.OnDraw(graphics2D);
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

		public override void OnBoundsChanged(EventArgs e)
		{
			if (Children.Count != 1)
			{
				throw new Exception("We should have one child that is the thum view.");
			}

			Children.FirstOrDefault().LocalBounds = LocalBounds;
			base.OnBoundsChanged(e);
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			MouseDownOnThumb = true;
			mouseDownPosition = new Vector2(mouseEvent.X, mouseEvent.Y);

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
			base.OnMouseUp(mouseEvent);
		}
	}
}