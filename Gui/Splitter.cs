﻿/*
Copyright (c) 2017, Lars Brubaker, John Lewin
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

using MatterHackers.VectorMath;
using System;

namespace MatterHackers.Agg.UI
{
	public class Splitter : GuiWidget
	{
		private SplitterBar splitterBar;

		private double _splitterDistance;

		public Splitter()
		{
			splitterBar = new SplitterBar(this)
			{
				BackgroundColor = Color.Transparent,
				Width = 6,
			};

			SplitterDistance = 120;

			AddChild(Panel1);
			AddChild(splitterBar);
			AddChild(Panel2);

			AnchorAll();
		}

		public event EventHandler DistanceChanged;

		public Orientation Orientation
		{
			get
			{
				return splitterBar.Orientation;
			}

			set
			{
				splitterBar.Orientation = value;
			}
		}

		public GuiWidget Panel1 { get; } = new GuiWidget();

		public GuiWidget Panel2 { get; } = new GuiWidget();

		public Color SplitterBackground
		{
			get => splitterBar.BackgroundColor;
			set => splitterBar.BackgroundColor = value;
		}

		public double SplitterDistance
		{
			get => _splitterDistance;
			set
			{
                if (Math.Abs(_splitterDistance - value) > .000001)
                {
					_splitterDistance = value;
					if (Orientation == Orientation.Vertical)
					{
						// make sure we respect minimum size
						_splitterDistance = Math.Max(_splitterDistance, Panel2.MinimumSize.X);
						_splitterDistance = Width == 0 ? _splitterDistance : Math.Min(_splitterDistance, Width - Panel1.MinimumSize.X - splitterBar.Width);
						if (Panel1Ratio != 0)
						{
                            Panel1Ratio = _splitterDistance / Width;
                        }
					}
					else
					{
						// make sure we respect minimum size
						_splitterDistance = Math.Max(_splitterDistance, Panel2.MinimumSize.Y);
						_splitterDistance = Height == 0 ? _splitterDistance : Math.Min(_splitterDistance, Height - Panel1.MinimumSize.Y - splitterBar.Height);
						if (Panel1Ratio != 0)
						{
							Panel1Ratio = _splitterDistance / Height;
						}
					}

					if(GuiWidget.DefaultEnforceIntegerBounds)
					{
						_splitterDistance = Math.Round(_splitterDistance);
					}


					if (!ajustingRatio)
					{
						OnBoundsChanged(null);
					}
				}
			}
		}

		double _panel1Ratio = 0;
		public double Panel1Ratio
		{
			get
			{
				return _panel1Ratio;
			}

			set
			{
                if (Math.Abs(_panel1Ratio - value) > .00001)
                {
                    _panel1Ratio = value;
                    if (Orientation == Orientation.Vertical
                        && Width > 0)
                    {
                        SplitterDistance = 1 - Width * _panel1Ratio;
                    }
                    else if (Height > 0)
                    {
                        SplitterDistance = Height * _panel1Ratio;
                    }
				}
			}
		}

		public double SplitterSize
		{
			get
			{
				if (Orientation == Orientation.Vertical)
				{
					return splitterBar.Width;
				}

				return splitterBar.Height;
			}
			set
			{
				if (Orientation == Orientation.Vertical)
				{
					if (splitterBar.Width != value)
					{
						splitterBar.Width = value;
						OnBoundsChanged(null);
					}
				}
				else
				{
					if (splitterBar.Height != value)
					{
						splitterBar.Height = value;
						OnBoundsChanged(null);
					}
				}
			}
		}

		bool ajustingRatio = false;

		public override void OnBoundsChanged(EventArgs e)
		{
			if(Panel1Ratio != 0
				&& Height > 0)
			{
				ajustingRatio = true;
				this.SplitterDistance = Height * this.Panel1Ratio;
				ajustingRatio = false;
			}

			if (Orientation == Orientation.Vertical)
			{
				Panel1.LocalBounds = new RectangleDouble(0, 0, SplitterDistance, LocalBounds.Height);

				splitterBar.OriginRelativeParent = new Vector2(SplitterDistance, 0);
				splitterBar.LocalBounds = new RectangleDouble(0, 0, splitterBar.Width, Height);

				Panel2.OriginRelativeParent = new Vector2(SplitterDistance + splitterBar.Width, 0);
				Panel2.LocalBounds = new RectangleDouble(0, 0, LocalBounds.Width - SplitterDistance - splitterBar.Width, LocalBounds.Height);
			}
			else
			{
				Panel2.OriginRelativeParent = new Vector2(0, 0);
				Panel2.LocalBounds = new RectangleDouble(0, 0, LocalBounds.Width, SplitterDistance);

				splitterBar.OriginRelativeParent = new Vector2(0, SplitterDistance);
				splitterBar.LocalBounds = new RectangleDouble(0, 0, Width, splitterBar.Height);

				Panel1.OriginRelativeParent = new Vector2(0, SplitterDistance + splitterBar.Height);
				Panel1.LocalBounds = new RectangleDouble(Panel1.Border.Left, Panel1.Border.Bottom, LocalBounds.Width - Panel1.DeviceMarginAndBorder.Width, LocalBounds.Height - SplitterDistance - splitterBar.Height - Panel1.DeviceMarginAndBorder.Height);
			}

			base.OnBoundsChanged(e);
		}

		private class SplitterBar : GuiWidget
		{
			private Vector2 DownPosition;
			private bool mouseDownOnBar = false;
			private double mouseDownPosition = -1;
			private Splitter parentSplitter;
			private Orientation _orientation =  Orientation.Vertical;
			public Orientation Orientation
			{
				get { return _orientation; }
				set
				{
					_orientation = value;
					if (value == Orientation.Vertical)
					{
						this.Cursor = Cursors.VSplit;
					}
					else
					{
						this.Cursor = Cursors.HSplit;
					}
				}
			}

			public SplitterBar(Splitter splitter)
			{
				this.parentSplitter = splitter;
				this.Cursor = Cursors.VSplit;
			}

			public override void OnMouseDown(MouseEventArgs mouseEvent)
			{
				mouseDownPosition = parentSplitter.SplitterDistance;

				if (PositionWithinLocalBounds(mouseEvent.X, mouseEvent.Y))
				{
					mouseDownOnBar = true;
					DownPosition = new Vector2(mouseEvent.X, mouseEvent.Y);
					DownPosition += OriginRelativeParent;
				}
				else
				{
					mouseDownOnBar = false;
				}
				base.OnMouseDown(mouseEvent);
			}

			public override void OnMouseMove(MouseEventArgs mouseEvent)
			{
				if (mouseDownOnBar)
				{
					Vector2 mousePosition = new Vector2(mouseEvent.X, mouseEvent.Y);
					mousePosition += OriginRelativeParent;
					double newSplitterPosition = parentSplitter.SplitterDistance;
					if (Orientation == Orientation.Vertical)
					{
						double deltaX = mousePosition.X - DownPosition.X;
						newSplitterPosition += deltaX;

						if (newSplitterPosition < Parent.LocalBounds.Left + Parent.Padding.Left)
						{
							newSplitterPosition = Parent.LocalBounds.Left + Parent.Padding.Left;
						}
						else if (newSplitterPosition > Parent.LocalBounds.Right - Width - Parent.Padding.Right)
						{
							newSplitterPosition = Parent.LocalBounds.Right - Width - Parent.Padding.Right;
						}
					}
					else
					{
						double deltaY = mousePosition.Y - DownPosition.Y;
						newSplitterPosition += deltaY;

						if (newSplitterPosition < Parent.LocalBounds.Bottom + Parent.Padding.Bottom)
						{
							newSplitterPosition = Parent.LocalBounds.Bottom + Parent.Padding.Bottom;
						}
						else if (newSplitterPosition > Parent.LocalBounds.Top - Height - Parent.Padding.Top)
						{
							newSplitterPosition = Parent.LocalBounds.Top - Height - Parent.Padding.Top;
						}
					}

					parentSplitter.SplitterDistance = newSplitterPosition;
					DownPosition = mousePosition;
				}
				base.OnMouseMove(mouseEvent);
			}

			public override void OnMouseUp(MouseEventArgs mouseEvent)
			{
				if (mouseDownPosition != parentSplitter.SplitterDistance)
				{
					parentSplitter.DistanceChanged?.Invoke(this, null);
				}

				mouseDownOnBar = false;
				base.OnMouseUp(mouseEvent);
			}
		}
	}
}