/*
Copyright (c) 2017, Lars Brubaker, John Lewin
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
	public class TextTab : ThreeViewTab
	{
		public TextTab(TabPage tabPage, string internalTabName)
			: this(tabPage, internalTabName, 12, Color.DarkGray, Color.White, Color.Black, Color.White)
		{
		}

		public TextTab(TabPage tabPage, string internalTabName, double pointSize,
			Color selectedTextColor, Color selectedBackgroundColor,
			Color normalTextColor, Color normalBackgroundColor, int fixedSize = 40, bool useUnderlineStyling = false)
			: base(internalTabName, new GuiWidget(), new GuiWidget(), new GuiWidget(), tabPage)
		{
			this.Padding = 0;
			this.Margin = 0;

			normalWidget.HAnchor = HAnchor.Fit;
			normalWidget.Padding = new BorderDouble(10);

			selectedWidget.HAnchor = HAnchor.Fit;
			selectedWidget.Padding = new BorderDouble(10, 0);

			AddText(tabPage.Text, selectedWidget, selectedTextColor, selectedBackgroundColor, pointSize, true, fixedSize, useUnderlineStyling);
			AddText(tabPage.Text, normalWidget, normalTextColor, normalBackgroundColor, pointSize, false, fixedSize, useUnderlineStyling);

			// Bind changes on TabPage.Text to ensure 
			tabPage.TextChanged += (s, e) =>
			{
				if (s is GuiWidget widget)
				{
					normalWidget.Children[0].Text = widget.Text;
					selectedWidget.Children[0].Text = widget.Text;
				}
			};

			this.HAnchor = HAnchor.Fit;
			this.VAnchor = VAnchor.Fit;
		}

		private void AddText(string tabText, GuiWidget viewWidget, Color textColor, Color backgroundColor, double pointSize, bool isActive, int fixedSize, bool useUnderlineStyling)
		{
			var tabTitle = new TextWidget(tabText, pointSize: pointSize, textColor: textColor)
			{
				VAnchor = VAnchor.Center,
				AutoExpandBoundsToText = true,
			};
			viewWidget.AddChild(tabTitle);

			viewWidget.Selectable = false;
			viewWidget.BackgroundColor = backgroundColor;
		}
	}

	public abstract class ThreeViewTab : Tab
	{
		public static int UnderlineHeight { get; set; } = 2;

		protected GuiWidget normalWidget;
		protected GuiWidget hoverWidget;
		protected GuiWidget selectedWidget;

		public ThreeViewTab(string tabName, GuiWidget normalWidget, GuiWidget hoverWidget, GuiWidget selectedWidget, TabPage tabPage)
			: base (tabName, tabPage)
		{
			this.normalWidget = normalWidget;
			this.hoverWidget = hoverWidget;
			this.selectedWidget = selectedWidget;

			AddChild(normalWidget);
			AddChild(hoverWidget);
			AddChild(selectedWidget);

			hoverWidget.Visible = false;
			selectedWidget.Visible = false;

			this.VAnchor = VAnchor.Fit;
			this.HAnchor = HAnchor.Fit;
		}

		protected override void OnTabIndexChanged()
		{
			if (TabBarContaningTab != null)
			{
				bool selected = TabPage == TabBarContaningTab.GetActivePage();

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
			}

			base.OnTabIndexChanged();
		}
	}

	public abstract class Tab : GuiWidget
	{
		public event EventHandler Selected;

		private bool registerListener = true;

		public Tab(string tabName, TabPage tabPage)
		{
			this.Name = tabName;
			this.Padding = new BorderDouble(5, 3, 20, 3);
			this.TabPage = tabPage;

			this.VAnchor = VAnchor.Fit;
			this.HAnchor = HAnchor.Fit;
		}

		public virtual void OnSelected(EventArgs e)
		{
			Selected?.Invoke(this, e);
		}

		public void Select()
		{
			OnSelected(null);
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			OnSelected(mouseEvent);
			base.OnClick(mouseEvent);
		}

		public override void OnParentChanged(EventArgs e)
		{
			if (registerListener)
			{
				TabBarContaningTab.TabIndexChanged += TabBarContaningTab_TabIndexChanged;
				registerListener = false;
			}

			base.OnParentChanged(e);
		}

		private void TabBarContaningTab_TabIndexChanged(object sender, EventArgs e)
		{
			this.OnTabIndexChanged();
		}

		public override void OnClosed(EventArgs e)
		{
			if (this.TabBarContaningTab != null)
			{
				this.TabBarContaningTab.TabIndexChanged -= TabBarContaningTab_TabIndexChanged;
			}

			base.OnClosed(e);
		}

		protected virtual void OnTabIndexChanged()
		{
		}

		public TabBar TabBarContaningTab => Parent as TabBar;

		public TabPage TabPage { get; }
	}
}