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
using System.Collections.Generic;
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Widgets
{
	/// <summary>
	/// The "Menus" window, a port of agg-gui's demo-ui/src/windows/menu_demo.rs: one shared menu model shown
	/// through a menu bar (File / Edit / View) and a right-click context area, with a log of what was picked.
	/// Built on <see cref="MenuBarWidget"/> and <see cref="MenuModelPopupBuilder"/>, with agg-gui's Font Awesome
	/// row icons from <see cref="IconFont"/>.
	/// </summary>
	public class MenusWindow : FlowLayoutWidget
	{
		public const string ContextAreaText = "Right-click here: icons, disabled rows, checks, shortcuts, submenus, shadow.";

		public const string FirstLogLine = "Right-click the test area or open a top menu.";

		/// <summary>How many actions the log keeps, and how many of the newest it shows, as agg-gui's.</summary>
		public const int LogCapacity = 8;

		public const int LogLinesShown = 3;

		private readonly DemoTheme demoTheme;
		private readonly ThemeConfig theme;
		private readonly List<string> log = new List<string> { FirstLogLine };

		// agg-gui's items own their check/radio state; here it is held per menu (keyed by action id) so the
		// marks survive the popup being rebuilt each time it opens.
		private readonly HashSet<string> checkedIds = new HashSet<string>();
		private readonly Dictionary<string, string> radioChoice = new Dictionary<string, string>();

		private readonly List<TextWidget> texts = new List<TextWidget>();
		private readonly List<TextWidget> logLines = new List<TextWidget>();
		private readonly WrappedTextWidget areaText;

		public MenusWindow(DemoTheme demoTheme)
			: base(FlowDirection.TopToBottom)
		{
			this.demoTheme = demoTheme;
			this.theme = demoTheme.Theme;
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Stretch;

			// menu_demo.rs's starting marks: Show Slider, Option A and Leaf Two on.
			foreach (string prefix in new[] { "file", "view", "context" })
			{
				this.checkedIds.Add(prefix + ".show-slider");
				this.checkedIds.Add(prefix + ".leaf-two");
				this.radioChoice[prefix] = prefix + ".option-a";
			}

			this.MenuBar = new MenuBarWidget(this.TopMenus(), this.theme) { Name = "Menus Menu Bar" };

			// The bar names its titles "{Text} Menu", which the shell's own bar already uses for View.
			foreach (ThemedTextButton title in this.MenuBar.Children.OfType<ThemedTextButton>())
			{
				title.Name = "Menus " + title.Name;
			}

			this.AddChild(this.MenuBar);

			this.ContextArea = new GuiWidget
			{
				Name = "Menus Context Area",
				HAnchor = HAnchor.Stretch,
				Height = 120 * DeviceScale,
				Margin = new BorderDouble(14, 0, 14, 14),
				Padding = new BorderDouble(12),
				BackgroundRadius = 6 * DeviceScale,
				BackgroundOutlineWidth = 1,
			};
			this.ContextArea.MouseDown += this.ContextArea_MouseDown;
			// Wrapped: agg-gui's line runs past a narrow window's edge.
			this.areaText = new WrappedTextWidget(ContextAreaText, DemoText.Points(DemoText.BodyPixels), textColor: this.theme.TextColor)
			{
				VAnchor = VAnchor.Top | VAnchor.Fit,
				Selectable = false,
			};
			this.ContextArea.AddChild(this.areaText);
			this.AddChild(this.ContextArea);

			var logColumn = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
				Padding = new BorderDouble(16, 8),
			};
			logColumn.AddChild(this.Label("Action log:"));
			for (int i = 0; i < LogLinesShown; i++)
			{
				TextWidget line = this.Label("");
				line.Name = $"Menus Log {i}";
				this.logLines.Add(line);
				logColumn.AddChild(line);
			}

			this.AddChild(logColumn);

			this.ShowLog();
			this.Recolor();
			demoTheme.ThemeChanged += this.OnThemeChanged;
		}

		public MenuBarWidget MenuBar { get; }

		public GuiWidget ContextArea { get; }

		/// <summary>The kept log lines, oldest first.</summary>
		public IReadOnlyList<string> Log => this.log;

		/// <summary>The context menu the last right-click opened, or null before the first.</summary>
		public PopupMenu ContextMenu { get; private set; }

		public override void OnClosed(EventArgs e)
		{
			// The theme outlives the window.
			this.demoTheme.ThemeChanged -= this.OnThemeChanged;
			base.OnClosed(e);
		}

		/// <summary>Whether the check item <paramref name="id"/> (e.g. "file.show-slider") is on.</summary>
		public bool IsChecked(string id) => this.checkedIds.Contains(id);

		/// <summary>The chosen radio item's id in the menu <paramref name="prefix"/> ("file", "view" or "context").</summary>
		public string RadioChoice(string prefix) => this.radioChoice[prefix];

		private void ContextArea_MouseDown(object sender, MouseEventArgs e)
		{
			if (e.Button != MouseButtons.Right)
			{
				return;
			}

			// agg-gui builds a fresh context menu on every right-click.
			this.ContextMenu = new PopupMenu(this.theme) { Name = "Menus Context Menu" };
			MenuModelPopupBuilder.AddItems(this.ContextMenu, this.SharedItems("context"), this.theme);
			this.ContextMenu.ShowMenu(this.ContextArea, e);
		}

		private IReadOnlyList<MenuItemModel> TopMenus()
		{
			return new[]
			{
				new MenuItemModel { Text = "File", SubMenuItems = () => this.SharedItems("file") },
				new MenuItemModel
				{
					Text = "Edit",
					SubMenuItems = () => new[]
					{
						this.Action("Undo", "edit.undo", "Ctrl+Z", IconFont.Undo),
						Disabled(this.Action("Redo", "edit.redo", "Ctrl+Y", IconFont.Redo)),
						new MenuItemModel { IsSeparator = true },
						this.Action("Copy", "edit.copy", "Ctrl+C", IconFont.Copy),
					},
				},
				new MenuItemModel { Text = "View", SubMenuItems = () => this.SharedItems("view") },
			};
		}

		/// <summary>menu_demo.rs's shared_items: the rows the File and View menus and the context menu all show.</summary>
		private IReadOnlyList<MenuItemModel> SharedItems(string prefix)
		{
			return new[]
			{
				this.Action("New", prefix + ".new", "Ctrl+N", IconFont.Plus),
				this.Action("Open", prefix + ".open", "Ctrl+O", IconFont.FolderOpen),
				new MenuItemModel { IsSeparator = true },
				this.Check("Show Slider", prefix + ".show-slider"),
				this.Radio("Option A", prefix, prefix + ".option-a"),
				this.Radio("Option B", prefix, prefix + ".option-b"),
				Disabled(this.Action("Disabled Item", prefix + ".disabled")),
				new MenuItemModel
				{
					Text = "More",
					AutomationName = "menus." + prefix + ".more",
					IconGlyph = IconFont.CaretRight,
					IconTypeFace = IconFont.TypeFace,
					SubMenuItems = () => new[]
					{
						this.Action("Nested Action", prefix + ".nested"),
						new MenuItemModel
						{
							Text = "Deep Submenu",
							AutomationName = "menus." + prefix + ".deep",
							SubMenuItems = () => new[]
							{
								this.Action("Leaf One", prefix + ".leaf-one"),
								// The one check row agg-gui does not keep_open(): picking it closes the menu.
								CloseOnPick(this.Check("Leaf Two", prefix + ".leaf-two")),
							},
						},
					},
				},
			};
		}

		private static MenuItemModel CloseOnPick(MenuItemModel item)
		{
			item.CloseMenuOnPick = true;
			return item;
		}

		private static MenuItemModel Disabled(MenuItemModel item)
		{
			item.IsEnabled = () => false;
			return item;
		}

		/// <summary>A command row named "menus.{id}" that logs its id when picked, with an optional <see cref="IconFont"/> glyph.</summary>
		private MenuItemModel Action(string text, string id, string shortcut = null, string iconGlyph = null)
		{
			return new MenuItemModel
			{
				Text = text,
				AutomationName = "menus." + id,
				ShortcutText = shortcut,
				IconGlyph = iconGlyph,
				IconTypeFace = iconGlyph != null ? IconFont.TypeFace : null,
				Action = () => this.PushLog(id),
			};
		}

		private MenuItemModel Check(string text, string id)
		{
			MenuItemModel item = this.Action(text, id);
			item.IsChecked = () => this.checkedIds.Contains(id);
			item.Action = () =>
			{
				if (!this.checkedIds.Remove(id))
				{
					this.checkedIds.Add(id);
				}

				this.PushLog(id);
			};
			return item;
		}

		private MenuItemModel Radio(string text, string prefix, string id)
		{
			MenuItemModel item = this.Action(text, id);
			item.IsRadio = true;
			item.IsChecked = () => this.radioChoice[prefix] == id;
			item.Action = () =>
			{
				this.radioChoice[prefix] = id;
				this.PushLog(id);
			};
			return item;
		}

		private void PushLog(string action)
		{
			this.log.Add("Action fired: " + action);
			if (this.log.Count > LogCapacity)
			{
				this.log.RemoveAt(0);
			}

			this.ShowLog();
		}

		/// <summary>The newest entries first, as agg-gui paints them.</summary>
		private void ShowLog()
		{
			for (int i = 0; i < this.logLines.Count; i++)
			{
				int index = this.log.Count - 1 - i;
				this.logLines[i].Text = index >= 0 ? this.log[index] : "";
			}
		}

		private TextWidget Label(string text)
		{
			var widget = new TextWidget(text, pointSize: DemoText.Points(DemoText.BodyPixels), textColor: this.theme.TextColor)
			{
				HAnchor = HAnchor.Left,
				Margin = new BorderDouble(0, 2),
				AutoExpandBoundsToText = true,
			};
			this.texts.Add(widget);
			return widget;
		}

		private void OnThemeChanged(object sender, EventArgs e)
		{
			this.Recolor();
			this.Invalidate();
		}

		/// <summary>Pushes the current theme into the widgets that copied their colours when they were built;
		/// the menus read the theme each time they open.</summary>
		private void Recolor()
		{
			// agg-gui's menu bar sits on its own band, a shade off the window's fill.
			this.MenuBar.BackgroundColor = this.demoTheme.Palette.PanelFill;
			this.ContextArea.BackgroundColor = this.demoTheme.Palette.PanelFill;
			this.ContextArea.BorderColor = this.demoTheme.Palette.WidgetStroke;
			this.areaText.TextColor = this.theme.TextColor;
			foreach (TextWidget text in this.texts)
			{
				text.TextColor = this.theme.TextColor;
			}

			foreach (ThemedTextButton title in this.MenuBar.Children.OfType<ThemedTextButton>())
			{
				title.TextColor = this.theme.TextColor;
			}
		}
	}
}
