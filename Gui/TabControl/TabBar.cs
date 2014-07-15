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
    public class TabBar : FlowLayoutWidget
    {
        public event EventHandler TabIndexChanged;

        GuiWidget tabPageArea;
        RGBA_Bytes borderColor = new RGBA_Bytes(0, 0, 0, 255);
        internal Tab currentVisibleTab;

        public RGBA_Bytes BorderColor 
        {
            get
            {
                return borderColor;
            }
            set
            {
                this.borderColor = value;
            }
        
        }

        public TabBar(FlowDirection direction, GuiWidget tabPageArea)
            : base(direction)
        {
            this.tabPageArea = tabPageArea;
        }

        public GuiWidget TabPageArea
        {
            get { return tabPageArea; }
        }

        public override void AddChild(GuiWidget child, int indexInChildrenList = -1)
        {
            Tab newTab = child as Tab;
            if (newTab != null)
            {
                newTab.MouseDown += new MouseEventHandler(SwitchToTab);
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
            if (TabIndexChanged != null)
            {
                TabIndexChanged(this, null);
            }
        }

        public int SelectedTabIndex
        {
            get
            {
                int selectedIndex = 0;
                for (int childIndex = 0; childIndex < Children.Count; childIndex++)
                {
                    Tab tab = Children[childIndex] as Tab;
                    if (tab != null)
                    {
                        if (tab == currentVisibleTab)
                        {
                            return selectedIndex;
                        }
                        selectedIndex++;
                    }
                }
                throw new Exception("Somehow there is no tab currently selected.");
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
                for (int index = 0; index < Children.Count; index++)
                {
                    Tab tab = Children[index] as Tab;
                    if (tab == currentVisibleTab)
                    {
                        return tab.Name;
                    }
                }
                throw new Exception("Somehow there is no tab currently selected.");
            }

            set
            {
                throw new NotImplementedException();
                SelectTab(value);
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
                Tab tab = child as Tab;
                if (tab != null)
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
            foreach (GuiWidget child in Children)
            {
                Tab tab = child as Tab;
                if (tab != null && tab.Name == tabName)
                {
                    SelectTab(tab);
                    return true;
                }
            }

            return false;
        }

        void SwitchToTab(object tabMouseDownOn, MouseEventArgs mouseEvent)
        {
            UiThread.RunOnIdle((state) =>
                {
                    Tab clickedTab = (Tab)tabMouseDownOn;
                    if (clickedTab != null)
                    {
                        SelectTab(clickedTab);
                    }
                });
        }

        public void SelectTab(Tab tabToSwitchTo)
        {
            if (currentVisibleTab != tabToSwitchTo)
            {
                foreach (GuiWidget tabPage in tabPageArea.Children)
                {
                    if (tabToSwitchTo.TabPageControlledByTab != tabPage)
                    {
                        tabPage.Visible = false;
                    }
                    else
                    {
                        tabPage.Visible = true;
                    }
                }

                tabToSwitchTo.TabPageControlledByTab.Visible = true;
                currentVisibleTab = tabToSwitchTo;

                OnTabIndexChanged();

                Invalidate();
            }
        }

        public TabPage GetActivePage()
        {
            return currentVisibleTab.TabPageControlledByTab;
        }

        public void SwitchToPage(TabPage page)
        {
            foreach (GuiWidget child in Children)
            {
                Tab tab = (Tab)child;
                if (tab != null && tab.TabPageControlledByTab == page)
                {
                    SelectTab(tab);
                    return;
                }
            }

            throw new Exception("You asked to switch to a page that is not in the TabControl.");
        }
    }
}
