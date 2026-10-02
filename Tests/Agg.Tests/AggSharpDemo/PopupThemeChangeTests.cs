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

using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using MatterHackers.AggSharpDemo.GuiDemo;
using MatterHackers.GuiAutomation;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	/// <summary>
	/// A theme picked while a menu is open shows on that menu at once (agg-gui paints its menus from the current
	/// visuals every frame): View > Color and View > Theme leave the menu open, and it used to keep the colours
	/// it was built with, so the pick looked like it had done nothing.
	/// </summary>
	[NotInParallel(new[] { nameof(AutomationRunner.ShowWindowAndExecuteTests), nameof(ThemeConfig.Current) })]
	public class PopupThemeChangeTests
	{
		[After(Test)]
		public void DrainTheIdleQueue() => PumpIdle();

		private static void PumpIdle()
		{
			for (int i = 0; i < 4; i++)
			{
				UiThread.InvokePendingActions();
			}
		}

		private static int ChangedSubscribers(ThemeConfig theme)
		{
			var field = typeof(ThemeConfig).GetField("Changed", BindingFlags.NonPublic | BindingFlags.Instance);
			return ((Delegate)field.GetValue(theme))?.GetInvocationList().Length ?? 0;
		}

		private static T Named<T>(GuiWidget root, string name)
			where T : GuiWidget
		{
			return root.Descendants<T>().Single(w => w.Name == name);
		}

		/// <summary>The colour of the icon's most opaque pixel: the glyph's ink.</summary>
		private static Color Ink(ImageBuffer icon)
		{
			Color ink = Color.Transparent;
			for (int y = 0; y < icon.Height; y++)
			{
				for (int x = 0; x < icon.Width; x++)
				{
					var pixel = icon.GetPixel(x, y);
					if (pixel.alpha > ink.alpha)
					{
						ink = pixel;
					}
				}
			}

			return ink;
		}

		/// <summary>Builds the demo shell in a window and opens View > Color the way a tap does.</summary>
		private static (DemoTheme demoTheme, SystemWindow window) OpenViewColor()
		{
			var demoTheme = new DemoTheme(ThemePreference.Light, AccentColor.Blue);
			var window = new SystemWindow(800, 500);
			window.AddChild(new GuiDemoShell(demoTheme));

			var title = Named<GuiWidget>(window, "View Menu");
			var center = title.TransformToScreenSpace(title.LocalBounds).Center;
			window.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, center.X, center.Y, 0));
			window.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, center.X, center.Y, 0));
			PumpIdle();
			Named<PopupMenu.SubMenuItemButton>(window, "view.accent").OpenSubMenu();
			PumpIdle();

			return (demoTheme, window);
		}

		/// <summary>A popup menu built from <paramref name="theme"/>, shown in a window.</summary>
		private static (PopupMenu menu, SystemWindow window) ShowMenu(ThemeConfig theme, Action<PopupMenu> populate)
		{
			var window = new SystemWindow(400, 300);
			var anchor = new GuiWidget(20, 20) { Position = new Vector2(10, 250) };
			window.AddChild(anchor);

			var menu = new PopupMenu(theme);
			populate(menu);
			window.ShowPopup(theme, new MatePoint(anchor), new MatePoint(menu));

			return (menu, window);
		}

		[Test]
		public async Task SwatchRowsShowOnlyTheSwatchAndATapOnItPicksTheRow()
		{
			var (demoTheme, window) = OpenViewColor();
			var row = Named<PopupMenu.MenuItem>(window, "view.accent.green");

			// Nothing in the row but the row itself paints a fill: no button box behind the swatch
			await Assert.That(row.Descendants<GuiWidget>().Where(w => w.BackgroundColor.Alpha0To255 > 0).Select(w => w.GetType().Name).ToList()).IsEmpty();
			await Assert.That(row.Descendants<GuiWidget>().Any(w => w.Selectable)).IsFalse()
				.Because("a tap on the swatch is the row's");

			var swatch = row.Descendants<ImageWidget>().Single();
			var point = swatch.TransformToScreenSpace(swatch.LocalBounds).Center;
			window.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, point.X, point.Y, 0) { PointerType = PointerType.Touch });
			PumpIdle();
			window.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, point.X, point.Y, 0) { PointerType = PointerType.Touch });
			PumpIdle();

			await Assert.That(demoTheme.Accent).IsEqualTo(AccentColor.Green);
			window.Children.OfType<PopupMenu>().Last().DismissAll();
			PumpIdle();
			window.Close();
		}

		[Test]
		public async Task OnlyTheColoursTheMenuTookFromTheThemeFollowIt()
		{
			// A light theme whose window fill and button fill are the same colour: each must still go its own way
			var shared = new Color(240, 240, 240);
			var theme = new ThemeConfig() { BackgroundColor = shared, ButtonBackgroundColor = shared, TextColor = new Color(20, 20, 20) };
			theme.EditFieldColors.Inactive.BackgroundColor = shared;

			GuiWidget literal = null;
			TextWidget literalText = null;
			ThemedTextEditWidget field = null;
			var (menu, window) = ShowMenu(theme, m =>
			{
				m.CreateMenuItem("Row");
				// A caller's own colours that happen to equal the theme's
				literal = new GuiWidget(20, 20) { BackgroundColor = shared };
				m.AddChild(literal);
				literalText = new TextWidget("Literal", textColor: theme.TextColor);
				m.AddChild(literalText);
				field = new ThemedTextEditWidget("edit", theme, pixelWidth: 80);
				m.AddChild(field);
			});

			var darkWindow = new Color(38, 38, 46);
			var darkButton = new Color(56, 56, 66);
			var darkText = new Color(230, 230, 235);
			theme.BackgroundColor = darkWindow;
			theme.ButtonBackgroundColor = darkButton;
			theme.TextColor = darkText;
			theme.NotifyChanged();

			var row = menu.Descendants<PopupMenu.MenuItem>().Single();
			await Assert.That(menu.BackgroundColor).IsEqualTo(darkWindow);
			await Assert.That(row.FillColor).IsEqualTo(darkButton).Because("a button takes the button fill, not the window fill it equalled");
			await Assert.That(row.Descendants<TextWidget>().Single().TextColor).IsEqualTo(darkText);
			await Assert.That(literal.BackgroundColor).IsEqualTo(shared);
			await Assert.That(literalText.TextColor).IsEqualTo(new Color(20, 20, 20));

			// The edit field still draws per state from the theme rather than having one fill pinned on it
			await Assert.That(field.BackgroundColor).IsEqualTo(theme.EditFieldColors.Inactive.BackgroundColor);
			field.Focus();
			await Assert.That(field.BackgroundColor).IsEqualTo(theme.EditFieldColors.Focused.BackgroundColor);

			window.Close();
			await Assert.That(ChangedSubscribers(theme)).IsEqualTo(0);
		}

		[Test]
		public async Task OpenViewColorMenuFollowsTheThemePickedFromIt()
		{
			var (demoTheme, window) = OpenViewColor();
			var theme = demoTheme.Theme;

			var menus = window.Children.OfType<PopupMenu>().ToList();
			await Assert.That(menus.Count).IsEqualTo(2).Because("the View menu and its Color sub menu are both up");
			// The leaf's own click, which keeps the menu open (agg-gui's keep_open)
			Named<PopupMenu.MenuItem>(window, "view.accent.red").InvokeClick();
			PumpIdle();

			await Assert.That(demoTheme.Accent).IsEqualTo(AccentColor.Red);
			await Assert.That(window.Children.OfType<PopupMenu>().Count()).IsEqualTo(2);
			foreach (var row in menus.SelectMany(m => m.Descendants<PopupMenu.MenuItem>()))
			{
				await Assert.That(row.HoverColor).IsEqualTo(theme.AccentMimimalOverlay)
					.Because($"{row.Name}'s highlight is the accent picked, not the one the menu opened with");
			}

			// The open View title is drawn in the accent too
			await Assert.That(Named<GuiWidget>(window, "View Menu").BackgroundColor).IsEqualTo(theme.AccentMimimalOverlay);

			// Light to dark, as View > Theme > Dark does: the panels, the labels and the glyph icons all turn
			demoTheme.SetPreference(ThemePreference.Dark);
			PumpIdle();

			foreach (var menu in menus)
			{
				await Assert.That(menu.BackgroundColor).IsEqualTo(DemoPalette.Dark.WindowFill);
				foreach (var text in menu.Descendants<TextWidget>())
				{
					await Assert.That(text.TextColor).IsEqualTo(DemoPalette.Dark.TextColor)
						.Because($"'{text.Text}' was built in the light theme's text colour");
				}
			}

			// A toggle row carries its glyph on a button beside the label (its Image is the check mark)
			var backendIcon = Named<PopupMenu.MenuItem>(window, "view.backend").Descendants<ImageWidget>().Single().Image;
			await Assert.That(Ink(backendIcon).red).IsEqualTo(DemoPalette.Dark.TextColor.red)
				.Because("a glyph icon is drawn in the theme's text colour, so it is redrawn with it");

			// Closing the chain lets go of the theme
			// From the sub menu, which holds the focus that keeps the chain up
			menus[1].DismissAll();
			PumpIdle();

			await Assert.That(window.Children.OfType<PopupMenu>().Count()).IsEqualTo(0);
			await Assert.That(ChangedSubscribers(theme)).IsEqualTo(0).Because("nothing is left listening once the menus are down");

			window.Close();
		}

		[Test]
		public async Task OpenDropDownListFollowsATheThemeChange()
		{
			var demoTheme = new DemoTheme(ThemePreference.Light, AccentColor.Blue);
			var theme = demoTheme.Theme;
			var window = new SystemWindow(400, 300);

			var themed = new DropDownList("none", theme.TextColor) { Name = "themed" };
			themed.AddItem("Item 0");
			themed.AddItem("Item 1");
			themed.AddItem(new ImageBuffer(16, 16), "Item 2");
			themed.SelectedIndex = 0;
			window.AddChild(themed);
			themed.Position = new Vector2(10, 200);

			// A caller's own colours (MatterCAD's MHDropDownList sets these) are not the theme's to change
			var custom = new DropDownList("none", theme.TextColor) { Name = "custom" };
			var customBackground = new Color(10, 200, 30);
			var customText = new Color(250, 240, 5);
			custom.MenuItemsBackgroundColor = customBackground;
			custom.MenuItemsTextColor = customText;
			custom.AddItem("Custom 0");
			window.AddChild(custom);
			custom.Position = new Vector2(200, 200);

			int subscribersBefore = ChangedSubscribers(theme);

			themed.InvokeClick();
			var container = themed.Parents<SystemWindow>().First().Descendants<DropDownContainer>().Single();
			await Assert.That(ChangedSubscribers(theme)).IsGreaterThan(subscribersBefore);

			demoTheme.SetPreference(ThemePreference.Dark);
			demoTheme.SetAccent(AccentColor.Green);

			await Assert.That(container.BackgroundColor).IsEqualTo(DemoPalette.Dark.WindowFill);
			var rows = container.Descendants<MenuItemColorStatesView>().ToList();
			await Assert.That(rows.Count).IsEqualTo(2);
			await Assert.That(rows[0].SelectedBackgroundColor).IsEqualTo(DemoTheme.ColorOf(AccentColor.Green))
				.Because("the current choice is marked in the accent picked");
			await Assert.That(rows[1].BackgroundColor).IsEqualTo(DemoPalette.Dark.WindowFill);
			await Assert.That(rows[1].Descendants<TextWidget>().Single().TextColor).IsEqualTo(DemoPalette.Dark.TextColor);
			await Assert.That(rows[1].OverBackgroundColor).IsEqualTo(theme.SlightShade);

			// The icon row swaps two prebuilt widgets; the idle one follows too
			var iconRow = container.Descendants<MenuItemStatesView>().Single();
			await Assert.That(iconRow.Descendants<TextWidget>().All(t => t.TextColor == DemoPalette.Dark.TextColor)).IsTrue();

			container.CloseMenu();
			PumpIdle();
			await Assert.That(ChangedSubscribers(theme)).IsEqualTo(subscribersBefore);

			// A list opened after the change shows the new theme, and one with its own colours keeps them
			custom.InvokeClick();
			var customContainer = window.Descendants<DropDownContainer>().Single();
			await Assert.That(customContainer.BackgroundColor).IsEqualTo(customBackground);
			var customRow = customContainer.Descendants<MenuItemColorStatesView>().Single();
			await Assert.That(customRow.NormalBackgroundColor).IsEqualTo(customBackground);
			await Assert.That(customRow.NormalTextColor).IsEqualTo(customText);

			demoTheme.SetPreference(ThemePreference.Light);
			await Assert.That(customContainer.BackgroundColor).IsEqualTo(customBackground);
			await Assert.That(customRow.NormalBackgroundColor).IsEqualTo(customBackground);
			await Assert.That(customRow.NormalTextColor).IsEqualTo(customText);
			customContainer.CloseMenu();
			PumpIdle();

			window.Close();
		}
	}
}
