/*
Copyright (c) 2013, Lars Brubaker
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
using System.Linq;
using System.Text;

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.Font;

namespace MatterHackers.Agg
{
    public class TabPagesPage : TabPage
    {
        const int offset = 20;
        public TabPagesPage()
            : base("Nested Tabs")
        {
            Padding = new BorderDouble(10);

            TabControl control1 = CreatePopulatedTabControl(Orientation.Vertical);
            TabControl control2 = CreatePopulatedTabControl(Orientation.Horizontal);
            control1.GetTabPage(0).AddChild(control2);
            TabControl control3 = CreatePopulatedTabControl(Orientation.Vertical);
            control2.GetTabPage(0).AddChild(control3);
            AddChild(control1);
        }

        TabControl CreatePopulatedTabControl(Orientation orientation)
        {
            TabControl tabControl = new TabControl(orientation);
            {
                TabPage page1 = new TabPage("Page 1");
                page1.Padding = new BorderDouble(5);
                tabControl.AddTab(page1, "Page 1");
                tabControl.AddTab(new TabPage("Page 2"), "Page 2");
                tabControl.AddTab(new TabPage("Page 3"), "Page 3");
            }

            return tabControl;
        }
    }
}
