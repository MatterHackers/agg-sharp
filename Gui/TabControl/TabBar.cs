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

namespace MatterHackers.Agg.UI
{
	public class TabBar : FlowLayoutWidget
	{
		public event EventHandler TabIndexChanged;

		internal Tab currentVisibleTab;

		public Color BorderColor { get; set; } = new Color(0, 0, 0, 255);

		public TabBar(FlowDirection direction, GuiWidget tabPageArea)
			: base(direction)
		{
			this.TabPageContainer = tabPageArea;
		}

		public GuiWidget TabPageContainer { get; }

		public override void AddChild(GuiWidget child, int indexInChildrenList = -1)
		{
			if (child is Tab newTab)
			{
				newTab.Selected += Tab_Selected;
			}
			base.AddChild(child, indexInChildrenList);
		}

		public override void OnChildAdded(EventArgs e)
		{
			SetBoundsToEncloseChildren();
			base.OnChildAdded(e);
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			switch (FlowDirection)
			{
				case UI.FlowDirection.LeftToRight:
					graphics2D.Line(LocalBounds.Left, LocalBounds.Bottom, LocalBounds.Right, LocalBounds.Bottom, this.BorderColor);
					break;

				case UI.FlowDirection.TopToBottom:
					graphics2D.Line(LocalBounds.Right, LocalBounds.Bottom, LocalBounds.Right, LocalBounds.Top, this.BorderColor);
					break;
			}
			base.OnDraw(graphics2D);
		}

		public virtual void OnTabIndexChanged()
		{
			TabIndexChanged?.Invoke(this, null);
		}

		public int SelectedTabIndex
		{
			get
			{
				return Children.IndexOf(currentVisibleTab);
			}

			set
			{
				SelectTab(value);
			}
		}

		public string SelectedTabName
		{
			get
			{
				return currentVisibleTab?.Name;
			}
		}

		public void SelectTab(int index)
		{
			if (index >= Children.Count)
			{
				throw new IndexOutOfRangeException();
			}

			// We keep a count of the tabs so that we can have non-tabs in the tab bar like spacers or
			// other things.
			int tabCount = 0;
			foreach (GuiWidget child in Children)
			{
				if (child is Tab tab)
				{
					if (index == tabCount)
					{
						SelectTab(tab);
						return;
					}
					tabCount++;
				}
			}

			throw new Exception("Somehow there is an object in the tabBar that is not a Tab.");
		}

		public bool SelectTab(string tabName)
		{
			foreach (var tab in this.Children<Tab>())
			{
				if (tab.Name == tabName)
				{
					SelectTab(tab);
					return true;
				}
			}

			return false;
		}

		private void Tab_Selected(object sender, EventArgs e)
		{
			UiThread.RunOnIdle(() =>
			{
				if (sender is Tab clickedTab)
				{
					SelectTab(clickedTab);
				}
			});
		}

		public void SelectTab(Tab tabToSwitchTo)
		{
			if (currentVisibleTab != tabToSwitchTo)
			{
				// Hide all but the current tab
				foreach (GuiWidget tabPage in TabPageContainer.Children)
				{
					tabPage.Visible = tabToSwitchTo.TabPage == tabPage;
				}

				currentVisibleTab = tabToSwitchTo;

				OnTabIndexChanged();

				Invalidate();
			}
		}

		public TabPage GetActivePage()
		{
			return currentVisibleTab.TabPage;
		}
	}
}