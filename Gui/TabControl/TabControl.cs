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
using System.Collections.Generic;
using System.Text;

using MatterHackers.Agg.Font;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.UI
{
    // TODO: break this up into a model-controler and a view. LBB
    public class TabControl : FlowLayoutWidget
    {
        internal class TabPageCollection : Dictionary<String, TabPage>
        {
            public new void Add(String key, TabPage tab)
            {
                base.Add(key, tab);
            }
        }

        TabPageCollection tabPages = new TabPageCollection();

        StyledTypeFace typeFaceStyle = new StyledTypeFace(LiberationSansFont.Instance, 12);
        TabBar tabBar;

        public TabBar TabBar { get { return tabBar; } }

        Orientation orientation;
        public Orientation Orientation
        {
            get { return orientation; }
            set 
            {
                orientation = value;
                switch (orientation)
                {
                    case UI.Orientation.Horizontal:
                        FlowDirection = UI.FlowDirection.TopToBottom;
                        tabBar.FlowDirection = FlowDirection.LeftToRight;
                        tabBar.HAnchor = UI.HAnchor.ParentLeft | UI.HAnchor.ParentRight;
                        break;

                    case UI.Orientation.Vertical:
                        FlowDirection = UI.FlowDirection.LeftToRight;
                        tabBar.FlowDirection = FlowDirection.TopToBottom;
                        tabBar.VAnchor = UI.VAnchor.ParentTop | UI.VAnchor.ParentBottom;
                        break;

                    default:
                        throw new NotImplementedException();
                }
            }
        }

        public override void OnDraw(Graphics2D graphics2D)
        {
            base.OnDraw(graphics2D);
        }

        public TabControl(Orientation orientation = Orientation.Horizontal)
        {
            DebugShowBounds = false;
            AnchorAll();

            GuiWidget tabPageArea = new GuiWidget();
            tabBar = new TabBar(FlowDirection.LeftToRight, tabPageArea);
            //tabBar.LocalBounds = new RectangleDouble(0, 0, 20, 20);
            base.AddChild(tabBar);
            //tabPageArea.BackgroundColor = new RGBA_Bytes(0, 255, 0, 50);
            base.AddChild(tabPageArea);
            tabPageArea.AnchorAll();
            this.Orientation = orientation;
        }

        public override void AddChild(GuiWidget child, int indexInChildrenList = -1)
        {
            throw new Exception("You cannot add controls directly to a TabControl. Add the controls to a TabPage and then add that to the TabControl.");
        }

        void SelectTab(Tab tabToSwitchTo)
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

        public string SelectedTabName
        {
            get
            {
                return tabBar.SelectedTabName;
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
                return tab.TabPageControlledByTab;
            }

            throw new Exception("Somehow there is an object in the tabBar that is not a Tab.");
        }

        public TabPage GetTabPage(string tabName)
        {
            foreach (GuiWidget child in tabBar.Children)
            {
                Tab tab = (Tab)child;
                if (tab != null && tab.Text == tabName)
                {
                    return tab.TabPageControlledByTab;
                }
            }

            throw new Exception("You asked to switch to a page that is not in the TabControl.");
        }

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

        public void SwitchToPage(TabPage page)
        {
            tabBar.SwitchToPage(page);
        }

        public void AddTab(TabPage tabPageWidget)
        {
            Tab newTab = new SimpleTextTabWidget(tabPageWidget);
            AddTab(newTab);
        }

        public void AddTab(Tab newTab)
        {
            TabPage tabPageWidget = newTab.TabPageControlledByTab;
            tabPages.Add(tabPageWidget.Text, tabPageWidget);

            switch (Orientation)
            {
                case UI.Orientation.Horizontal:
                    break;

                case UI.Orientation.Vertical:
                    newTab.HAnchor = UI.HAnchor.ParentLeft | UI.HAnchor.ParentRight;
                    break;
            }
            tabBar.AddChild(newTab);

            tabBar.TabPageArea.AddChild(tabPageWidget);

            if (tabBar.TabPageArea.Children.Count == 1)
            {
                tabBar.currentVisibleTab = newTab;
            }
            else
            {
                tabPageWidget.Visible = false;
            }

            tabPageWidget.OnTabIndexChanged();
        }
    }
}
