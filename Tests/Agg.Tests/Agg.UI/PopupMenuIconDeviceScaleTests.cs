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

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// Menu items rasterize (radio) or load (checkbox) their icon once and hold onto it, which is what keeps a
	/// long menu cheap. The catch is that both are built at <see cref="GuiWidget.DeviceScale"/>, and the scale
	/// can change under a live widget when the window moves to a display of another scale - so the cache has to
	/// remember the scale it was built at, not merely that it was built.
	/// </summary>
	/// <remarks>
	/// Keyless <c>[NotInParallel]</c>: <see cref="GuiWidget.DeviceScale"/> and <see cref="StaticData.RootPath"/>
	/// are both process wide. See <c>GuiDeviceScaleTests</c> and <c>ConstructorHygieneTests</c> for the same
	/// save-and-restore pattern.
	/// </remarks>
	public class PopupMenuIconDeviceScaleTests
	{
		[Test]
		[NotInParallel]
		public async Task RadioMenuItemIconFollowsADisplayScaleChange()
		{
			double savedDeviceScale = GuiWidget.DeviceScale;

			try
			{
				GuiWidget.DeviceScale = 1;

				var menuItem = new PopupMenu.RadioMenuItem(new GuiWidget(), new ThemeConfig())
				{
					Checked = true,

					// OnLoad registers with this list and does not check it for null, so a radio item without
					// one throws on its first draw - CreateBoolMenuItem always hands one over.
					SiblingRadioButtonList = new List<GuiWidget>()
				};

				Draw(menuItem);

				await Assert.That(menuItem.Image.Width).IsEqualTo(16)
					.Because("the radio circle is 16 design units, so it is 16 pixels on a 1x display");

				GuiWidget.DeviceScale = 2;

				Draw(menuItem);

				await Assert.That(menuItem.Image.Width).IsEqualTo(32)
					.Because("the text beside it doubled when the menu moved to a 2x display");
			}
			finally
			{
				GuiWidget.DeviceScale = savedDeviceScale;
			}
		}

		[Test]
		[NotInParallel]
		public async Task CheckboxMenuItemIconFollowsADisplayScaleChange()
		{
			double savedDeviceScale = GuiWidget.DeviceScale;
			string savedRootPath = StaticData.RootPath;
			string tempRoot = Path.Combine(Path.GetTempPath(), "AggPopupMenuIconScale_" + Path.GetRandomFileName());

			Directory.CreateDirectory(Path.Combine(tempRoot, "Icons"));

			try
			{
				StaticData.RootPath = tempRoot;

				// A blank icon of the size the real fa-check_16.png is, so the only thing under test is the
				// scaling StaticData does on the way out.
				var sourceIcon = new ImageBuffer(16, 16);
				sourceIcon.NewGraphics2D().Clear(Color.White);
				ImageIO.SaveImageData(Path.Combine(tempRoot, "Icons", "fa-check_16.png"), sourceIcon);

				GuiWidget.DeviceScale = 1;

				var menuItem = new PopupMenu.CheckboxMenuItem(new GuiWidget(), new ThemeConfig())
				{
					Checked = true
				};

				Draw(menuItem);

				await Assert.That(menuItem.Image.Width).IsEqualTo(16)
					.Because("the check mark is 16 design units, so it is 16 pixels on a 1x display");

				GuiWidget.DeviceScale = 2;

				Draw(menuItem);

				await Assert.That(menuItem.Image.Width).IsEqualTo(32)
					.Because("the text beside it doubled when the menu moved to a 2x display");
			}
			finally
			{
				GuiWidget.DeviceScale = savedDeviceScale;
				StaticData.RootPath = savedRootPath;
				Directory.Delete(tempRoot, true);
			}
		}

		/// <summary>
		/// A disabled item draws a faded copy of its icon rather than the icon itself, and that copy is derived
		/// from whatever <see cref="PopupMenu.MenuItem.Image"/> held when it was first asked for. Rebuilding the
		/// icon for a new scale has to drop the copy too, or the greyed-out items in a menu stay at the old
		/// display's size while the enabled ones beside them resize.
		/// </summary>
		[Test]
		[NotInParallel]
		public async Task ADisabledMenuItemIconFollowsADisplayScaleChange()
		{
			double savedDeviceScale = GuiWidget.DeviceScale;

			try
			{
				GuiWidget.DeviceScale = 1;

				var menuItem = new PopupMenu.RadioMenuItem(new GuiWidget(), new ThemeConfig())
				{
					Checked = true,
					SiblingRadioButtonList = new List<GuiWidget>()
				};

				menuItem.Enabled = false;

				Draw(menuItem);

				await Assert.That(menuItem.DisabledImage.Width).IsEqualTo(16)
					.Because("the faded radio circle is the same 16 design units as the solid one");

				GuiWidget.DeviceScale = 2;

				Draw(menuItem);

				await Assert.That(menuItem.DisabledImage.Width).IsEqualTo(32)
					.Because("the enabled items beside it doubled when the menu moved to a 2x display");
			}
			finally
			{
				GuiWidget.DeviceScale = savedDeviceScale;
			}
		}

		/// <summary>
		/// Paints the item into a throwaway surface. Drawing is what a menu item does when the display scale
		/// has moved out from under it - OnLoad has long since run by then.
		/// </summary>
		private static void Draw(GuiWidget widget)
		{
			var surface = new ImageBuffer(200, 60);

			widget.OnDraw(surface.NewGraphics2D());
		}
	}
}
