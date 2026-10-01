/*
Copyright (c) 2026, Lars Brubaker
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
*/

using MatterHackers.Agg;
using MatterHackers.Agg.UI;

namespace MatterHackers.AggSharpDemo.GuiDemo
{
	/// <summary>
	/// A demo sidebar's search box, shared by <see cref="DemoSidebar"/> and <see cref="AggDemoSidebar"/>: agg-gui's
	/// TextField with radius 6 and a 1px outline rather than the square border, and a placeholder of
	/// "\u{F002}  Search…". Our text runs have no Font Awesome fallback, so the magnifier is its own widget
	/// (<see cref="Icon"/>), 12px like the placeholder, and the placeholder text starts after it.
	/// </summary>
	public class SidebarSearchBox : ThemedTextEditWidget
	{
		/// <summary>agg-gui's TextField corner radius.</summary>
		private const double SearchRadius = 6;

		/// <summary>The search placeholder's font size (12px), which its magnifier glyph matches.</summary>
		private const double SearchIconSize = 12;

		/// <param name="name">The box's automation name; its icon is named name + " Icon".</param>
		public SidebarSearchBox(string name, ThemeConfig theme)
			: base("", theme, messageWhenEmptyAndNotSelected: "Search…")
		{
			this.Name = name;
			this.HAnchor = HAnchor.Stretch;
			this.Margin = new BorderDouble(10, 6, 10, 2);
			this.Border = 0;
			this.BackgroundRadius = SearchRadius * DeviceScale;
			this.BackgroundOutlineWidth = 1 * DeviceScale;

			this.Icon = new IconGlyphWidget(IconFont.Search, theme.EditFieldColors.Inactive.LightTextColor, SearchIconSize)
			{
				Name = name + " Icon",
				HAnchor = HAnchor.Left,
				VAnchor = VAnchor.Center,
				Margin = new BorderDouble(left: 5),
			};
			this.AddChild(this.Icon);

			// HAnchor.Left, not the default Absolute, so the margin is honoured
			this.NoContentFieldDescription.HAnchor = HAnchor.Left;
			this.NoContentFieldDescription.Margin = new BorderDouble(left: 5 + SearchIconSize + 5);
			this.TextChanged += (s, e) => this.Icon.Visible = this.Text.Length == 0;
		}

		/// <summary>The magnifier drawn in the box while it is empty.</summary>
		public IconGlyphWidget Icon { get; }

		/// <summary>The field's fill and border come from the theme's EditFieldColors every time it draws; its text
		/// and hint colours are copied from them only on a focus change, so they are copied again here.</summary>
		public void ApplyTheme(DemoTheme demoTheme)
		{
			ThemeConfig.StateColor fieldColors = this.ActualTextEditWidget.InternalTextEditWidget.Focused
				? demoTheme.Theme.EditFieldColors.Focused
				: demoTheme.Theme.EditFieldColors.Inactive;
			this.ActualTextEditWidget.TextColor = fieldColors.TextColor;
			this.NoContentFieldDescription.TextColor = fieldColors.LightTextColor;
			this.Icon.Color = fieldColors.LightTextColor;
		}
	}
}
