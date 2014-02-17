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
    public class SimpleTextTabWidget : Tab
    {
        public SimpleTextTabWidget(TabPage tabPageControledByTab)
            : this(tabPageControledByTab, 12, RGBA_Bytes.DarkGray, RGBA_Bytes.White, RGBA_Bytes.Black, RGBA_Bytes.White)
        {
        }

        public SimpleTextTabWidget(TabPage tabPageControledByTab, double pointSize, 
            RGBA_Bytes selectedTextColor, RGBA_Bytes selectedBackgroundColor,
            RGBA_Bytes normalTextColor, RGBA_Bytes normalBackgroundColor)
            : base(tabPageControledByTab.Text, new GuiWidget(), new GuiWidget(), new GuiWidget(), tabPageControledByTab)
        {
            AddText(tabPageControledByTab.Text, selectedWidget, selectedTextColor, selectedBackgroundColor, pointSize);
            AddText(tabPageControledByTab.Text, normalWidget, normalTextColor, normalBackgroundColor, pointSize);
            //hoverWidget;

            tabPageControledByTab.TextChanged += new EventHandler(tabPageControledByTab_TextChanged);

            SetBoundsToEncloseChildren();
        }

        void tabPageControledByTab_TextChanged(object sender, EventArgs e)
        {
            normalWidget.Children[0].Text = ((GuiWidget)sender).Text;
            normalWidget.SetBoundsToEncloseChildren();
            selectedWidget.Children[0].Text = ((GuiWidget)sender).Text;
            selectedWidget.SetBoundsToEncloseChildren();
            SetBoundsToEncloseChildren();
        }

        private void AddText(string tabText, GuiWidget widgetState, RGBA_Bytes textColor, RGBA_Bytes backgroundColor, double pointSize)
        {
            TextWidget tabTitle = new TextWidget(tabText, pointSize: pointSize, textColor: textColor);
            tabTitle.AutoExpandBoundsToText = true;
            widgetState.AddChild(tabTitle);
            widgetState.Selectable = false;
            widgetState.SetBoundsToEncloseChildren();
            widgetState.BackgroundColor = backgroundColor;
        }
    }

    public class Tab : GuiWidget
    {
        RGBA_Bytes backgroundColor = new RGBA_Bytes(230, 230, 230);

        TabPage tabPageControledByTab;
        protected GuiWidget normalWidget;
        protected GuiWidget hoverWidget;
        protected GuiWidget selectedWidget;

        public Tab(string tabName, GuiWidget normalWidget, GuiWidget hoverWidget, GuiWidget pressedWidget,
            TabPage tabPageControledByTab)
        {
            base.Name = tabName;
            this.normalWidget = normalWidget;
            this.hoverWidget = hoverWidget;
            this.selectedWidget = pressedWidget;
            AddChild(normalWidget);
            AddChild(hoverWidget);
            hoverWidget.Visible = false;
            AddChild(pressedWidget);
            pressedWidget.Visible = false;
            Padding = new BorderDouble(5, 3,20,3);
            this.tabPageControledByTab = tabPageControledByTab;
            SetBoundsToEncloseChildren();
        }

        public TabBar TabBarContaningTab
        {
            get { return (TabBar)Parent; }
        }

        public TabPage TabPageControlledByTab
        {
            get { return tabPageControledByTab; }
        }

        public override string Name
        {
            set
            {
                throw new Exception("You should not change this it is required for the interface to this tab.");
            }
        }

        public override string Text
        {
            get
            {
                throw new Exception("You are probably looking for the Name not the text of a tab.");
            }
            set
            {
                throw new Exception("You are probably looking for the Name not the text of a tab.");
            }
        }

        public override void OnDraw(Graphics2D graphics2D)
        {
            bool selected = TabPageControlledByTab == TabBarContaningTab.GetActivePage();

#if true
            if (selected)
            {
                normalWidget.Visible = false;
                hoverWidget.Visible = false;
                selectedWidget.Visible = true;
            }
            else
            {
                normalWidget.Visible = true;
                hoverWidget.Visible = false;
                selectedWidget.Visible = false;
            }

            base.OnDraw(graphics2D);
#else
            switch (TabBarContaningTab.FlowDirection)
            {
                case UI.FlowDirection.LeftToRight:
                    if (selected)
                    {
                        graphics2D.Clear(RGBA_Bytes.White);
                        tabTitle.OriginRelativeParent = tabTitle.OriginRelativeParent + new Vector2(0, 1);
                        RectangleDouble screenIntBounds = LocalBounds;
                        screenIntBounds.Bottom -= 2;
                        RoundedRect rect = new RoundedRect(screenIntBounds, 3);
                        rect.radius(0, 0, 3, 3);
                        graphics2D.Render(new Stroke(rect, 1), RGBA_Bytes.Black);
                    }
                    else
                    {
                        graphics2D.Clear(backgroundColor);
                        RectangleDouble screenIntBounds = LocalBounds;
                        screenIntBounds.Top -= 2;
                        RoundedRect rect = new RoundedRect(screenIntBounds, 3);
                        rect.radius(0, 0, 3, 3);
                        graphics2D.Render(new Stroke(rect, 1), RGBA_Bytes.Black);
                    }
                    break;

                case UI.FlowDirection.TopToBottom:
                    if (selected)
                    {
                        graphics2D.Clear(RGBA_Bytes.White);
                        tabTitle.OriginRelativeParent = tabTitle.OriginRelativeParent + new Vector2(-1, 0);
                        RectangleDouble screenIntBounds = LocalBounds;
                        screenIntBounds.Right += 2;
                        RoundedRect rect = new RoundedRect(screenIntBounds, 3);
                        rect.radius(3, 0, 3, 0);
                        graphics2D.Render(new Stroke(rect, 1), RGBA_Bytes.Black);
                    }
                    else
                    {
                        graphics2D.Clear(backgroundColor);
                        RectangleDouble screenIntBounds = LocalBounds;
                        screenIntBounds.Left += 2;
                        RoundedRect rect = new RoundedRect(screenIntBounds, 3);
                        rect.radius(3, 0, 0, 3);
                        graphics2D.Render(new Stroke(rect, 1), RGBA_Bytes.Black);
                    }
                    break;

                default:
                    throw new NotImplementedException();
            }

            base.OnDraw(graphics2D);

            if (selected)
            {
                switch (TabBarContaningTab.FlowDirection)
                {
                    case UI.FlowDirection.LeftToRight:
                        tabTitle.OriginRelativeParent = tabTitle.OriginRelativeParent + new Vector2(0, -1);
                        break;

                    case UI.FlowDirection.TopToBottom:
                        tabTitle.OriginRelativeParent = tabTitle.OriginRelativeParent + new Vector2(1, 0);
                        break;
                }
            }
#endif
        }
    }
}
