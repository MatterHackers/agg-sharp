﻿/*
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
using System.Collections.Generic;

namespace MatterHackers.Agg.UI
{
	// TODO: break this up into a model-controller and a view. LBB
	public class TabControl : FlowLayoutWidget
	{
		private Dictionary<string, TabPage> tabPages = new Dictionary<string, TabPage>();

		private TabBar tabBar;

		public TabBar TabBar => tabBar;

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
						tabBar.HAnchor = UI.HAnchor.Left | UI.HAnchor.Right;
						break;

					case Orientation.Vertical:
						FlowDirection = UI.FlowDirection.LeftToRight;
						tabBar.FlowDirection = FlowDirection.TopToBottom;
						tabBar.VAnchor = VAnchor.Top | VAnchor.Bottom;
						break;

					default:
						throw new NotImplementedException();
				}
			}
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

		public override void OnLoad(EventArgs args)
		{
			// If no selection exists by the first draw, select index 0 if applicable
			if (SelectedTabIndex == -1 && tabPages.Count > 0)
			{
				SelectedTabIndex = 0;
			}

			base.OnLoad(args);
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

			if (tabBar.Children[index] is Tab tab)
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

		public int TextPointSize { get; set; }

		public void SelectTab(int index)
		{
			Tab foundTab = null;
			int tabCount = 0;
			foreach (GuiWidget child in tabBar.Children)
			{
				if (child is Tab tab)
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
			AddTab(new TextTab(tabPageWidget, internalTabName));
		}

		public void AddTab(Tab newTab, int tabPosition = -1)
		{
			var tabPage = newTab.TabPage;

			// Use name, not text
			tabPages.Add(newTab.Name, tabPage);

			tabBar.AddChild(newTab, tabPosition);

			tabBar.TabPageContainer.AddChild(tabPage);

			tabPage.Visible = false;
		}
	}
}
