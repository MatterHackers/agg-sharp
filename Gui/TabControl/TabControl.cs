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

using MatterHackers.Agg.Font;
using System;
using System.Collections.Generic;

namespace MatterHackers.Agg.UI
{
	// TODO: break this up into a model-controller and a view. LBB
	public class TabControl : FlowLayoutWidget
	{
		private Dictionary<string, TabPage> tabPages = new Dictionary<string, TabPage>();

		private StyledTypeFace typeFaceStyle = new StyledTypeFace(LiberationSansFont.Instance, 12);
		private TabBar tabBar;

		public TabBar TabBar { get { return tabBar; } }

		private Orientation orientation;

		public Orientation Orientation
		{
			get { return orientation; }
			set
			{
				orientation = value;
				switch (orientation)
				{
					case Orientation.Horizontal:
						FlowDirection = UI.FlowDirection.TopToBottom;
						tabBar.FlowDirection = FlowDirection.LeftToRight;
						tabBar.HAnchor = UI.HAnchor.ParentLeft | UI.HAnchor.ParentRight;
						break;

					case Orientation.Vertical:
						FlowDirection = UI.FlowDirection.LeftToRight;
						tabBar.FlowDirection = FlowDirection.TopToBottom;
						tabBar.VAnchor = VAnchor.ParentTop | VAnchor.ParentBottom;
						break;

					default:
						throw new NotImplementedException();
				}
			}
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			// If no selection exists by the first draw, select index 0 if applicable
			if (SelectedTabIndex == -1 && tabPages.Count > 0)
			{
				SelectedTabIndex = 0;
			}

			base.OnDraw(graphics2D);
		}

		public TabControl(Orientation orientation = Orientation.Horizontal, GuiWidget separator = null)
		{
			AnchorAll();

			GuiWidget tabPageArea = new GuiWidget();

			tabBar = new TabBar(FlowDirection.LeftToRight, tabPageArea);
			
			base.AddChild(tabBar);

			if (separator != null)
			{
				base.AddChild(separator);
			}

			base.AddChild(tabPageArea);

			tabPageArea.AnchorAll();

			this.Orientation = orientation;
		}

		public override void AddChild(GuiWidget child, int indexInChildrenList = -1)
		{
			throw new Exception("You cannot add controls directly to a TabControl. Add the controls to a TabPage and then add that to the TabControl.");
		}

		private void SelectTab(Tab tabToSwitchTo)
		{
			tabBar.SelectTab(tabToSwitchTo);
		}

		public TabPage GetActivePage()
		{
			return tabBar.GetActivePage();
		}

		public int SelectedTabIndex
		{
			get
			{
				return tabBar.SelectedTabIndex;
			}

			set
			{
				tabBar.SelectedTabIndex = value;
			}
		}

		public TabPage GetTabPage(int index)
		{
			if (index >= tabBar.Children.Count)
			{
				throw new IndexOutOfRangeException();
			}

			Tab tab = (Tab)tabBar.Children[index];
			if (tab != null)
			{
				return tab.TabPage;
			}

			throw new Exception("Somehow there is an object in the tabBar that is not a Tab.");
		}

		public TabPage GetTabPage(string tabName)
		{
			TabPage tabPage;
			tabPages.TryGetValue(tabName, out tabPage);
			return tabPage;
		}

		public string SelectedTabName => tabBar.SelectedTabName;

		public int TabCount => tabPages.Count;

		public void SelectTab(int index)
		{
			Tab foundTab = null;
			int tabCount = 0;
			foreach (GuiWidget child in tabBar.Children)
			{
				Tab tab = child as Tab;
				if (tab != null)
				{
					foundTab = tab;
					if (tabCount == index)
					{
						break;
					}
					tabCount++;
				}
			}

			if (foundTab != null)
			{
				SelectTab(foundTab);
				return;
			}

			throw new Exception("Somehow there is an object in the tabBar that is not a Tab.");
		}

		public bool SelectTab(string tabName)
		{
			return tabBar.SelectTab(tabName);
		}

		public void AddTab(TabPage tabPageWidget, string internalTabName)
		{
			Tab newTab = new SimpleTextTabWidget(tabPageWidget, internalTabName);
			AddTab(newTab);
		}

		public void AddTab(Tab newTab)
		{
			TabPage tabPageWidget = newTab.TabPage;

			// Use name, not text
			tabPages.Add(newTab.Name, tabPageWidget);

			switch (Orientation)
			{
				case Orientation.Horizontal:
					newTab.VAnchor = VAnchor.ParentCenter;
					break;

				case Orientation.Vertical:
					newTab.HAnchor = HAnchor.ParentLeft | HAnchor.ParentRight;
					break;
			}

			tabBar.AddChild(newTab);

			tabBar.TabPageContainer.AddChild(tabPageWidget);

			tabPageWidget.Visible = false;
		}
	}
}
 