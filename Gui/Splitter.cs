/*
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

using System;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.UI
{
	public class Splitter : GuiWidget
	{
		private class SplitterBar : GuiWidget
		{
			private bool mouseDownOnBar = false;
			private Vector2 DownPosition;

			public SplitterBar()
			{
				this.Cursor = Cursors.VSplit;
			}

			override public void OnMouseDown(MouseEventArgs mouseEvent)
			{
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

			override public void OnMouseUp(MouseEventArgs mouseEvent)
			{
				mouseDownOnBar = false;
				base.OnMouseUp(mouseEvent);
			}

			override public void OnMouseMove(MouseEventArgs mouseEvent)
			{
				if (mouseDownOnBar)
				{
					Vector2 mousePosition = new Vector2(mouseEvent.X, mouseEvent.Y);
					mousePosition += OriginRelativeParent;
					double deltaX = mousePosition.X - DownPosition.X;
					double newSplitterPosition = ((Splitter)Parent).SplitterDistance + deltaX;

					if (newSplitterPosition < Parent.LocalBounds.Left + Parent.Padding.Left)
					{
						newSplitterPosition = Parent.LocalBounds.Left + Parent.Padding.Left;
					}
					else if (newSplitterPosition > Parent.LocalBounds.Right - Width - Parent.Padding.Right)
					{
						newSplitterPosition = Parent.LocalBounds.Right - Width - Parent.Padding.Right;
					}

					((Splitter)Parent).SplitterDistance = newSplitterPosition;
					DownPosition = mousePosition;
				}
				base.OnMouseMove(mouseEvent);
			}
		}

		private SplitterBar splitterBar = new SplitterBar()
		{
			BackgroundColor = Color.Transparent
		};

		public Splitter()
		{
			splitterBar.Width = 6;
			SplitterDistance = 120;

			AddChild(Panel1);
			AddChild(Panel2);
			AddChild(splitterBar);

			AnchorAll();
		}

		public Color SplitterBackground
		{
			get => splitterBar.BackgroundColor;
			set => splitterBar.BackgroundColor = value;
		}

		public Orientation Orientation { get; set; }

		public GuiWidget Panel1 { get; } = new GuiWidget();

		public GuiWidget Panel2 { get; } = new GuiWidget();

		public double SplitterWidth
		{
			get
			{
				return splitterBar.Width;
			}

			set
			{
				if (splitterBar.Width != value)
				{
					splitterBar.Width = value;
					OnBoundsChanged(null);
				}
			}
		}

		private double splitterDistance;

		public double SplitterDistance
		{
			get
			{
				return splitterDistance;
			}
			set
			{
				if (splitterDistance != value)
				{
					splitterDistance = value;
					OnBoundsChanged(null);
				}
			}
		}

		public override void OnBoundsChanged(EventArgs e)
		{
			splitterBar.LocalBounds = new RectangleDouble(0, 0, splitterBar.Width, Height);
			splitterBar.OriginRelativeParent = new Vector2(SplitterDistance, 0);
			Panel1.LocalBounds = new RectangleDouble(0, 0, SplitterDistance, LocalBounds.Height);
			Panel2.LocalBounds = new RectangleDouble(0, 0, LocalBounds.Width - SplitterDistance - splitterBar.Width, LocalBounds.Height);
			Panel2.OriginRelativeParent = new Vector2(SplitterDistance + splitterBar.Width, 0);

			base.OnBoundsChanged(e);
		}
	}
}