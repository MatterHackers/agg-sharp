/*
Copyright (c) 2019, Lars Brubaker, John Lewin
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
using MatterHackers.Agg.Font;

namespace MatterHackers.Agg.UI
{
	public class MenuItemStatesView : GuiWidget
	{
		private readonly GuiWidget normalState;
		private readonly GuiWidget overState;

		public MenuItemStatesView(GuiWidget normalState, GuiWidget overState)
		{
			overState.HAnchor |= HAnchor.Stretch;
			normalState.HAnchor |= HAnchor.Stretch;
			HAnchor = HAnchor.Stretch | HAnchor.Fit;
			VAnchor = VAnchor.Fit;
			Selectable = false;
			this.normalState = normalState;
			this.overState = overState;
			AddChild(normalState);
			AddChild(overState);

			overState.Visible = false;
		}

		public override void OnParentChanged(EventArgs ex)
		{
			// We don't need to remove these as the parent we are attached to is the held list that gets turned
			// into the menu list when required and unhooking these breaks that list from working.
			// This will get cleared when the list is no longer need and the menu (the parent) is removed)
			Parent.MouseLeave += (s, e) => this.Highlighted = false;
			Parent.MouseEnter += (s, e) =>
			{
				ClearActiveHighlight();
				this.Highlighted = true;
			};

			base.OnParentChanged(ex);
		}

		public override void SendToChildren(object objectToRoute)
		{
			if (objectToRoute is MenuItem.MenuClosedMessage)
			{
				this.Highlighted = false;
			}

			base.SendToChildren(objectToRoute);
		}

		public bool Highlighted
		{
			get
			{
				return overState.Visible;
			}

			set
			{
				overState.Visible = value;
				normalState.Visible = !value;
			}
		}

		private void ClearActiveHighlight()
		{
			// Find the FlowLayoutWidget containing this MenuItemStatesView  instance
			var dropListContainer = this.Parents<FlowLayoutWidget>().FirstOrDefault();

			// Loop over any sibling MenuItem widgets
			foreach (var menuItem in dropListContainer.Children<MenuItem>())
			{
				// Find the MenuItemStatesView instance that they contain and set highlighted to false
				var statesView = menuItem.Children<MenuItemStatesView>().FirstOrDefault();
				if (statesView != null)
				{
					statesView.Highlighted = false;
				}
			}
		}
	}

	public class MenuItemColorStatesView : GuiWidget
	{
		private readonly TextWidget textWidget;

		public Color NormalBackgroundColor { get; set; }

		public Color OverBackgroundColor { get; set; }

		public Color DisabledBackgroundColor { get; set; }

		public Color NormalTextColor { get; set; }

		public Color OverTextColor { get; set; }

		public Color DisabledTextColor { get; set; }

		public double PointSize { get { return textWidget.PointSize; } set { textWidget.PointSize = value; } }

		public MenuItemColorStatesView(string name, Color textColor, TypeFace typeFace = null)
		{
			HAnchor = HAnchor.Stretch | HAnchor.Fit;
			VAnchor = VAnchor.Fit;
			Selectable = false;

			textWidget = new TextWidget(name, textColor: textColor, typeFace: typeFace)
			{
				AutoExpandBoundsToText = true,
				HAnchor = HAnchor.Left
			};

			AddChild(textWidget);
		}

		public override void OnParentChanged(EventArgs ex)
		{
			// We don't need to remove these as the parent we are attached to is the held list that gets turned
			// into the menu list when required and unhooking these breaks that list from working.
			// This will get cleared when the list is no longer need and the menu (the parent) is removed)
			Parent.MouseLeave += (s, e) => this.Highlighted = false;
			Parent.MouseEnter += (s, e) =>
			{
				ClearActiveHighlight();
				this.Highlighted = true;
			};

			Parent.EnabledChanged += (s, e) =>
			{
				if (Parent.Enabled)
				{
					textWidget.TextColor = NormalTextColor;
				}
				else
				{
					textWidget.TextColor = DisabledTextColor;
				}
			};

			base.OnParentChanged(ex);
		}

		public override void SendToChildren(object objectToRoute)
		{
			if (objectToRoute is MenuItem.MenuClosedMessage)
			{
				this.Highlighted = false;
			}

			base.SendToChildren(objectToRoute);
		}

		public bool Highlighted
		{
			get
			{
				return BackgroundColor == OverBackgroundColor;
			}

			set
			{
				if (value)
				{
					BackgroundColor = OverBackgroundColor;
					if (Parent.Enabled)
					{
						textWidget.TextColor = OverTextColor;
					}
					else
					{
						textWidget.TextColor = DisabledTextColor;
					}
				}
				else
				{
					BackgroundColor = NormalBackgroundColor;
					if (Parent.Enabled)
					{
						textWidget.TextColor = NormalTextColor;
					}
					else
					{
						textWidget.TextColor = DisabledTextColor;
					}
				}
			}
		}

		private void ClearActiveHighlight()
		{
			// Find the FlowLayoutWidget containing this MenuItemStatesView  instance
			var dropListContainer = this.Parents<FlowLayoutWidget>().FirstOrDefault();

			// Loop over any sibling MenuItem widgets
			foreach (var menuItem in dropListContainer.Children<MenuItem>())
			{
				// Find the MenuItemStatesView instance that they contain and set highlighted to false
				var statesView = menuItem.Children<MenuItemStatesView>().FirstOrDefault();
				if (statesView != null)
				{
					statesView.Highlighted = false;
				}
			}
		}
	}
}