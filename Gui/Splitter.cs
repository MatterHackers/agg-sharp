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

using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;
using System;

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
			}

			protected bool MouseDownOnBar
			{
				get { return mouseDownOnBar; }
				set { mouseDownOnBar = value; }
			}

			public override void OnDraw(Graphics2D graphics2D)
			{
				RoundedRect roundRect = new RoundedRect(LocalBounds.Left, LocalBounds.Bottom, LocalBounds.Right, LocalBounds.Top, 0);
				graphics2D.Render(roundRect, new RGBA_Bytes(0, 0, 0, 60));
				base.OnDraw(graphics2D);
			}

			override public void OnMouseDown(MouseEventArgs mouseEvent)
			{
				if (PositionWithinLocalBounds(mouseEvent.X, mouseEvent.Y))
				{
					MouseDownOnBar = true;
					DownPosition = new Vector2(mouseEvent.X, mouseEvent.Y);
					DownPosition += OriginRelativeParent;
				}
				else
				{
					MouseDownOnBar = false;
				}
				base.OnMouseDown(mouseEvent);
			}

			override public void OnMouseUp(MouseEventArgs mouseEvent)
			{
				MouseDownOnBar = false;
				base.OnMouseUp(mouseEvent);
			}

			override public void OnMouseMove(MouseEventArgs mouseEvent)
			{
				if (MouseDownOnBar)
				{
					Vector2 mousePosition = new Vector2(mouseEvent.X, mouseEvent.Y);
					mousePosition += OriginRelativeParent;
					double deltaX = mousePosition.x - DownPosition.x;
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

		private GuiWidget panel1 = new GuiWidget();
		private GuiWidget panel2 = new GuiWidget();

		public Orientation Orientation { get; set; }

		private SplitterBar splitterBar = new SplitterBar();

		public Splitter()
		{
			splitterBar.Width = 6;
			SplitterDistance = 120;

			//Panel1.DebugShowBounds = true;
			//Panel2.DebugShowBounds = true;

			AddChild(Panel1);
			AddChild(Panel2);
			AddChild(splitterBar);

			AnchorAll();
		}

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

		public GuiWidget Panel1
		{
			get
			{
				return panel1;
			}
		}

		public GuiWidget Panel2
		{
			get
			{
				return panel2;
			}
		}

		public override void OnBoundsChanged(EventArgs e)
		{
			splitterBar.LocalBounds = new RectangleDouble(0, 0, splitterBar.Width, Height);
			splitterBar.OriginRelativeParent = new Vector2(SplitterDistance, 0);
			Panel1.LocalBounds = new RectangleDouble(0, 0, SplitterDistance - splitterBar.Width / 2, LocalBounds.Height);
			Panel2.LocalBounds = new RectangleDouble(0, 0, LocalBounds.Width - SplitterDistance - splitterBar.Width / 2, LocalBounds.Height);
			Panel2.OriginRelativeParent = new Vector2(SplitterDistance + splitterBar.Width, 0);

			base.OnBoundsChanged(e);
		}
	}
}