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
	public class TextTab : Tab
	{
		public TextTab(TabPage tabPage, string internalTabName)
			: this(tabPage, internalTabName, 12, RGBA_Bytes.DarkGray, RGBA_Bytes.White, RGBA_Bytes.Black, RGBA_Bytes.White)
		{
		}

		public TextTab(TabPage tabPage, string internalTabName, double pointSize,
			RGBA_Bytes selectedTextColor, RGBA_Bytes selectedBackgroundColor,
			RGBA_Bytes normalTextColor, RGBA_Bytes normalBackgroundColor, int fixedSize = 40, bool useUnderlineStyling = false)
			: base(internalTabName, new GuiWidget(), new GuiWidget(), new GuiWidget(), tabPage)
		{
			this.Padding = new BorderDouble(5, 0);
			this.Margin = new BorderDouble(0, 0, 10, 0);

			AddText(tabPage.Text, selectedWidget, selectedTextColor, selectedBackgroundColor, pointSize, true, fixedSize, useUnderlineStyling);
			AddText(tabPage.Text, normalWidget, normalTextColor, normalBackgroundColor, pointSize, false, fixedSize, useUnderlineStyling);

			// Bind changes on TabPage.Text to ensure 
			tabPage.TextChanged += (s, e) =>
			{
				// TODO: Why the heavy use of SetBoundsToEncloseChildren? Shouldn't XAnchor.Fit cover this?
				normalWidget.Children[0].Text = ((GuiWidget)s).Text;
				normalWidget.SetBoundsToEncloseChildren();

				selectedWidget.Children[0].Text = ((GuiWidget)s).Text;
				selectedWidget.SetBoundsToEncloseChildren();

				SetBoundsToEncloseChildren();
			};

			SetBoundsToEncloseChildren();
		}

		private void AddText(string tabText, GuiWidget viewWidget, RGBA_Bytes textColor, RGBA_Bytes backgroundColor, double pointSize, bool isActive, int fixedSize, bool useUnderlineStyling)
		{
			var tabTitle = new TextWidget(tabText, pointSize: pointSize, textColor: textColor)
			{
				VAnchor = VAnchor.Center,
				AutoExpandBoundsToText = true,
			};
			viewWidget.AddChild(tabTitle);

			viewWidget.Selectable = false;
			viewWidget.BackgroundColor = backgroundColor;

			EnforceSizingAdornActive(viewWidget, isActive, useUnderlineStyling, fixedSize);
		}
	}

	public abstract class Tab : GuiWidget
	{
		public static int UnderlineHeight { get; set; } = 2;

		private RGBA_Bytes backgroundColor = new RGBA_Bytes(230, 230, 230);

		protected GuiWidget normalWidget;
		protected GuiWidget hoverWidget;
		protected GuiWidget selectedWidget;

		public event EventHandler Selected;

		public Tab(string tabName, GuiWidget normalWidget, GuiWidget hoverWidget, GuiWidget selectedWidget,
			TabPage tabPage)
		{
			this.Name = tabName;
			this.normalWidget = normalWidget;
			this.hoverWidget = hoverWidget;
			this.selectedWidget = selectedWidget;
			this.Padding = new BorderDouble(5, 3, 20, 3);
			this.TabPage = tabPage;

			AddChild(normalWidget);
			AddChild(hoverWidget);
			AddChild(selectedWidget);

			hoverWidget.Visible = false;
			selectedWidget.Visible = false;
			
			SetBoundsToEncloseChildren();
		}

		public override void OnParentChanged(EventArgs e)
		{
			TabBarContaningTab.TabIndexChanged += SelectionChanged;
			base.OnParentChanged(e);
		}

		public virtual void OnSelected(EventArgs e)
		{
			Selected?.Invoke(this, e);
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			OnSelected(mouseEvent);
			base.OnClick(mouseEvent);
		}

		private void SelectionChanged(object sender, EventArgs e)
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
		}

		public TabBar TabBarContaningTab => Parent as TabBar;

		public TabPage TabPage { get; }

		protected static void EnforceSizingAdornActive(GuiWidget viewWidget, bool isActive, bool useUnderlineStyle, int controlHeight = 40, int controlMargin = 0)
		{
			viewWidget.Height = controlHeight;
			viewWidget.Margin = controlMargin;

			if (isActive && useUnderlineStyle)
			{
				// Adorn the active tab with a underline bar
				viewWidget.AddChild(new GuiWidget()
				{
					HAnchor = HAnchor.Stretch,
					Height = UnderlineHeight,
					BackgroundColor = ActiveTheme.Instance.PrimaryAccentColor,
					VAnchor = VAnchor.Bottom
				});
			}

			RectangleDouble childrenBounds = viewWidget.GetMinimumBoundsToEncloseChildren();
			viewWidget.LocalBounds = new RectangleDouble(childrenBounds.Left, viewWidget.LocalBounds.Bottom, childrenBounds.Right, viewWidget.LocalBounds.Top);
		}
	}
}