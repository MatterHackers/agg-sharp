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

using System.Collections.Generic;
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>A <see cref="MenuBarWidget"/> title leads with its menu's icon when the model has one, as agg-gui's
	/// "\u{F009} Demos" does; a title without one is unchanged.</summary>
	public class MenuBarTitleIconTests
	{
		[Test]
		public async Task ATitleWithAnIconGlyphDrawsItAheadOfTheText()
		{
			var theme = new ThemeConfig();
			var bar = new MenuBarWidget(
				new List<MenuItemModel>
				{
					new MenuItemModel { Text = "Demos", IconGlyph = "#", SubMenuItems = () => new List<MenuItemModel>() },
					new MenuItemModel { Text = "Demos", SubMenuItems = () => new List<MenuItemModel>() },
				},
				theme);
			var root = new GuiWidget(400, 60);
			root.AddChild(bar);
			root.PerformLayout();

			var iconed = (ThemedTextButton)bar.Children[0];
			var plain = (ThemedTextButton)bar.Children[1];
			await Assert.That(iconed.Name).IsEqualTo("Demos Menu");
			double extra = (MenuBarTitle.IconSize + MenuBarTitle.IconGap) * GuiWidget.DeviceScale;
			await Assert.That(iconed.Width - plain.Width).IsEqualTo(extra).Within(1.0);

			// The glyph is drawn in the title's text colour in the space the padding made for it.
			iconed.TextColor = Color.Red;
			var image = new ImageBuffer((int)iconed.Width, (int)iconed.Height);
			iconed.OnDraw(image.NewGraphics2D());
			int red = 0;
			for (int y = 0; y < image.Height; y++)
			{
				for (int x = 0; x < (int)(iconed.DevicePadding.Left); x++)
				{
					Color pixel = image.GetPixel(x, y);
					red += pixel.red > 100 && pixel.green < 60 ? 1 : 0;
				}
			}

			await Assert.That(red).IsGreaterThan(0);
		}
	}
}
