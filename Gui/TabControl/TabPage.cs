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

namespace MatterHackers.Agg.UI
{
	public class TabPage : GuiWidget
	{
		public TabPage(string tabTitle)
		{
			AnchorAll();
			Text = tabTitle;
		}

		public TabPage(GuiWidget widgetToAddToPage, string tabTitle)
			: this(tabTitle)
		{
			widgetToAddToPage.AnchorAll();
			AddChild(widgetToAddToPage);
		}
	}

	/// <summary>
	/// A TabPage widget which defers construction of its root widget until made visible
	/// </summary>
	public class LazyTabPage : TabPage
	{
		public Func<GuiWidget> Generator { get; set; }

		private GuiWidget rootWidget = null;

		public LazyTabPage(string tabTitle) : base(tabTitle)
		{
		}

		public override bool Visible
		{
			get { return base.Visible; }
			set
			{
				base.Visible = value;

				if (value && rootWidget == null)
				{
					rootWidget = Generator();
					rootWidget.AnchorAll();
					AddChild(rootWidget);
				}
			}
		}

		public void Reload()
		{
			if (rootWidget != null)
			{
				rootWidget.Close();
				rootWidget = null;
			}

			if (Visible)
			{
				Visible = true;
			}
		}
	}
}