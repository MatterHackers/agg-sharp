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
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Widgets
{
	/// <summary>
	/// The "Popups" window, a port of agg-gui's popups demo (demo-ui/src/windows/popups_demo.rs, itself egui's):
	/// a code-styled configurator for an <see cref="AnchoredPopup"/> - parent and child anchor points, a preset,
	/// the gap and the close behavior - above a trigger that opens the popup on a left click, a small context
	/// menu on a right click and a tooltip on hover.
	/// </summary>
	public class PopupsWindow : GuiWidget
	{
		public const string TriggerText = "Click, right-click and hover me!";

		public const string TriggerTip = "Tooltips are popups, too!";

		public const double DefaultGap = 4;

		private readonly DemoTheme demoTheme;
		private readonly ThemeConfig theme;

		// Widgets that copy a colour when built, recoloured on ThemeChanged.
		private readonly List<TextWidget> texts = new List<TextWidget>();
		private readonly List<DropDownList> combos = new List<DropDownList>();
		private readonly FlowLayoutWidget panel;
		private readonly GuiWidget separator;
		private readonly TextWidget hintText;
		private readonly WrappedTextWidget menusPointer;

		private readonly HashSet<GuiWidget> hookedAncestors = new HashSet<GuiWidget>();
		private SystemWindow hostWindow;

		// Set while the window writes the combos itself, so their SelectionChanged does not read it as a pick.
		private bool syncing;

		// The trigger's MouseDown runs before the SystemWindow's for the same press; this keeps the press from
		// being applied twice.
		private bool pressHandledByTrigger;

		public PopupsWindow(DemoTheme demoTheme)
		{
			this.demoTheme = demoTheme;
			this.theme = demoTheme.Theme;
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Stretch;

			var root = new FlowLayoutWidget(FlowDirection.TopToBottom) { HAnchor = HAnchor.Stretch, VAnchor = VAnchor.Stretch };
			this.AddChild(root);

			var scroll = new ScrollableWidget(autoScroll: true)
			{
				Name = "Popups Config Scroll",
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
			};
			scroll.ScrollArea.HAnchor = HAnchor.Stretch;
			root.AddChild(scroll);

			this.panel = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
				Padding = new BorderDouble(12),
			};
			scroll.AddChild(this.panel);

			// "let align = RectAlign {" with the reset button on the right.
			var headerRow = new FlowLayoutWidget { HAnchor = HAnchor.Stretch, Margin = new BorderDouble(0, 3) };
			headerRow.AddChild(this.Code("let align = RectAlign {"));
			headerRow.AddChild(new HorizontalSpacer());
			// Font Awesome's rotate-left stands in for egui's "⟲", as in agg-gui; Recolor draws it.
			this.ResetButton = new ThemedIconButton(this.ResetIcon(), this.theme)
			{
				Name = "Popups Reset",
				VAnchor = VAnchor.Center,
				ToolTipText = "Reset to defaults",
			};
			this.ResetButton.Click += (s, e) => this.Reset();
			headerRow.AddChild(this.ResetButton);
			this.panel.AddChild(headerRow);

			string[] anchorLabels = Align2.All.Select(a => a.Label).ToArray();
			this.ParentCombo = this.ComboRow("    parent: Align2::", "Popups Parent", anchorLabels, null);
			this.ChildCombo = this.ComboRow("    child: Align2::", "Popups Child", anchorLabels, null);
			TextWidget closeBrace = this.Code("};");
			closeBrace.HAnchor = HAnchor.Left;
			closeBrace.VAnchor = VAnchor.Absolute;
			this.panel.AddChild(closeBrace);

			// "<Select Preset>" stands for a hand-composed pair, as egui's preset combo does.
			this.PresetCombo = this.ComboRow("let align = RectAlign::", "Popups Preset", new[] { "<Select Preset>" }.Concat(RectAlign.Presets.Select(p => p.Label)), null);

			var gapRow = this.Row("let gap = ");
			this.GapValue = new DragValue(DefaultGap, 0, 40, this.theme)
			{
				Name = "Popups Gap",
				Step = 1,
				Decimals = 0,
				VAnchor = VAnchor.Center,
			};
			gapRow.AddChild(this.GapValue);

			this.BehaviorCombo = this.ComboRow(
				"let close_behavior = PopupCloseBehavior::",
				"Popups Close Behavior",
				AnchoredPopup.CloseBehaviors.Select(b => b.Label),
				AnchoredPopup.CloseBehaviors.Select(b => b.Description).ToArray());

			var openRow = this.Row("let popup_open = ");
			this.OpenCheckBox = new CheckBox("", this.theme.TextColor, DemoText.Points(DemoText.BodyPixels))
			{
				Name = "Popups Open",
				VAnchor = VAnchor.Center,
			};
			openRow.AddChild(this.OpenCheckBox);

			this.separator = new GuiWidget
			{
				HAnchor = HAnchor.Stretch,
				Height = Math.Max(1, Math.Round(DeviceScale)),
				Margin = new BorderDouble(0, 6),
			};
			this.panel.AddChild(this.separator);

			this.menusPointer = new WrappedTextWidget("For nested submenus, checkmarks, radios and shortcuts, see the Menus demo.", DemoText.Points(12));
			this.panel.AddChild(this.menusPointer);

			// The band under the configurator: the trigger and what it does. Its deep bottom padding is agg-gui's
			// upward bias - it leaves the default BottomStart popup room to open below before it has to flip.
			var band = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
				Padding = new BorderDouble(12, 90, 12, 16),
			};
			root.AddChild(band);

			this.Trigger = this.demoTheme.AccentButton(new ThemedTextButton(TriggerText, this.theme)
			{
				Name = "Popups Trigger",
				HAnchor = HAnchor.Center,
				ToolTipText = TriggerTip,
			});
			this.Trigger.MouseDown += this.Trigger_MouseDown;
			band.AddChild(this.Trigger);

			this.hintText = this.Text("Left-click: popup (menu)   Right-click: context menu", DemoText.Points(12));
			this.hintText.HAnchor = HAnchor.Center;
			this.hintText.VAnchor = VAnchor.Absolute;
			this.hintText.Margin = new BorderDouble(0, 0, 0, 6);
			band.AddChild(this.hintText);

			this.ContextActionText = this.Text("", DemoText.Points(12));
			this.ContextActionText.Name = "Popups Context Action";
			this.ContextActionText.HAnchor = HAnchor.Center;
			this.ContextActionText.VAnchor = VAnchor.Absolute;
			band.AddChild(this.ContextActionText);

			this.PopupPanel = new PopupsPopupPanel(DemoText.Points(DemoText.BodyPixels));

			// agg-gui's paint_popup shadow: the panel's rounded rect in 22% black, 4 px right and down, unblurred.
			// A sibling under the panel rather than part of it, since a widget cannot paint past its own bounds
			// and the panel's bounds are what the placement measures.
			this.PopupShadow = new GuiWidget
			{
				Name = "Popups Popup Shadow",
				HAnchor = HAnchor.Absolute,
				VAnchor = VAnchor.Absolute,
				Selectable = false,
				BackgroundColor = new Color(Color.Black, (int)Math.Round(0.22 * 255)),
				BackgroundRadius = this.PopupPanel.BackgroundRadius,
			};

			this.ParentCombo.SelectionChanged += (s, e) => this.AlignComboChanged();
			this.ChildCombo.SelectionChanged += (s, e) => this.AlignComboChanged();
			this.PresetCombo.SelectionChanged += (s, e) => this.PresetChanged();
			this.GapValue.ValueChanged += (s, e) =>
			{
				this.Popup.Gap = this.GapValue.Value;
				this.RefreshPopup();
			};
			this.BehaviorCombo.SelectionChanged += (s, e) =>
			{
				this.Popup.CloseBehavior = AnchoredPopup.CloseBehaviors[Math.Max(0, this.BehaviorCombo.SelectedIndex)].Behavior;
				this.RefreshPopup();
			};
			this.OpenCheckBox.CheckedStateChanged += (s, e) =>
			{
				if (!this.syncing)
				{
					this.SetPopupOpen(this.OpenCheckBox.Checked);
				}
			};

			this.Reset();
			this.Recolor();
			demoTheme.ThemeChanged += this.OnThemeChanged;
		}

		/// <summary>The popup's open state, placement and close behavior.</summary>
		public AnchoredPopup Popup { get; } = new AnchoredPopup();

		/// <summary>What the popup shows; parented to the SystemWindow while it is open.</summary>
		public PopupsPopupPanel PopupPanel { get; }

		/// <summary>The panel's drop shadow, shown and placed with it.</summary>
		public GuiWidget PopupShadow { get; }

		public ThemedTextButton Trigger { get; }

		public ThemedIconButton ResetButton { get; }

		public DropDownList ParentCombo { get; }

		public DropDownList ChildCombo { get; }

		public DropDownList PresetCombo { get; }

		public DragValue GapValue { get; }

		public DropDownList BehaviorCombo { get; }

		public CheckBox OpenCheckBox { get; }

		/// <summary>The last context menu action, under the trigger.</summary>
		public TextWidget ContextActionText { get; }

		/// <summary>The right-click menu's items: agg-gui's context_items, nesting included.</summary>
		public IReadOnlyList<MenuItemModel> ContextItems() => new[]
		{
			this.ContextItem("Cut", "cut", IconFont.Cut, "Ctrl+X"),
			this.ContextItem("Copy", "copy", IconFont.Copy, "Ctrl+C"),
			new MenuItemModel { IsSeparator = true },
			new MenuItemModel
			{
				Text = "More",
				AutomationName = "Popups Context more",
				IconGlyph = IconFont.CaretRight,
				IconTypeFace = IconFont.TypeFace,
				SubMenuItems = () => new[] { this.ContextItem("Nested action", "nested"), this.ContextItem("Another one", "nested-2") },
			},
		};

		/// <summary>egui's reset button: every setting back to its default, the popup closed.</summary>
		public void Reset()
		{
			this.syncing = true;
			this.ParentCombo.SelectedIndex = RectAlign.BottomStart.Parent.AllIndex;
			this.ChildCombo.SelectedIndex = RectAlign.BottomStart.Child.AllIndex;
			this.PresetCombo.SelectedIndex = RectAlign.BottomStart.PresetIndex + 1;
			this.BehaviorCombo.SelectedIndex = (int)PopupCloseBehavior.CloseOnClick;
			this.syncing = false;
			this.GapValue.Value = DefaultGap;
			this.Popup.Align = RectAlign.BottomStart;
			this.Popup.Gap = DefaultGap;
			this.Popup.CloseBehavior = PopupCloseBehavior.CloseOnClick;
			this.SetPopupOpen(false);
		}

		/// <summary>Opens or closes the popup, keeping the checkbox and the SystemWindow in step.</summary>
		public void SetPopupOpen(bool open)
		{
			this.hostWindow ??= this.PopupHostWindow();
			if (open && this.hostWindow == null)
			{
				// Not on screen yet - nowhere to show it.
				open = false;
			}

			this.Popup.IsOpen = open;
			if (open)
			{
				if (this.PopupPanel.Parent == null)
				{
					// One panel is shown and taken down again for the life of the window.
					this.PopupShadow.ClearRemovedFlag();
					this.PopupPanel.ClearRemovedFlag();
					this.hostWindow.AddChild(this.PopupShadow);
					this.hostWindow.AddChild(this.PopupPanel);
					this.hostWindow.MouseDown += this.HostWindow_MouseDown;
					this.hostWindow.KeyDown += this.HostWindow_KeyDown;
					this.HookAncestors();
				}

				this.RefreshPopup();
			}
			else if (this.PopupPanel.Parent != null)
			{
				this.hostWindow.MouseDown -= this.HostWindow_MouseDown;
				this.hostWindow.KeyDown -= this.HostWindow_KeyDown;
				this.UnhookAncestors();
				this.PopupPanel.Parent.RemoveChild(this.PopupPanel);
				this.PopupShadow.Parent?.RemoveChild(this.PopupShadow);
			}

			this.syncing = true;
			this.OpenCheckBox.Checked = open;
			this.syncing = false;
			this.Trigger.Invalidate();
		}

		public override void OnClosed(EventArgs e)
		{
			// The theme and the SystemWindow outlive the window.
			this.SetPopupOpen(false);
			this.demoTheme.ThemeChanged -= this.OnThemeChanged;
			base.OnClosed(e);
		}

		/// <summary>The press, in the popup's (SystemWindow) space, run through agg-gui's order: the close behavior
		/// first, then the trigger.</summary>
		private void HandlePress(Vector2 windowPosition, MouseButtons button, MouseEventArgs mouseEvent)
		{
			var viewport = new Vector2(this.hostWindow?.Width ?? 0, this.hostWindow?.Height ?? 0);
			bool onTrigger = this.TriggerBounds().Contains(windowPosition);
			if (button == MouseButtons.Left)
			{
				PopupClickOutcome outcome = this.Popup.OnMouseDown(windowPosition, viewport);
				if (outcome.Closed)
				{
					this.SetPopupOpen(false);
				}

				if (!outcome.Consumed && onTrigger)
				{
					this.SetPopupOpen(!this.Popup.IsOpen);
				}
			}
			else if (button == MouseButtons.Right && onTrigger && !this.Popup.Contains(windowPosition, viewport))
			{
				this.SetPopupOpen(false);
				var menu = new PopupMenu(this.theme) { Name = "Popups Context Menu" };
				MenuModelPopupBuilder.AddItems(menu, this.ContextItems());
				menu.ShowMenu(this.Trigger, mouseEvent);
			}
		}

		private void Trigger_MouseDown(object sender, MouseEventArgs mouseEvent)
		{
			this.hostWindow ??= this.PopupHostWindow();
			if (this.hostWindow == null)
			{
				return;
			}

			Vector2 windowPosition = this.Trigger.TransformToParentSpace(this.hostWindow, mouseEvent.Position);
			this.HandlePress(windowPosition, mouseEvent.Button, mouseEvent);

			// The SystemWindow sees this same press next; only tell it to skip when it is listening.
			this.pressHandledByTrigger = this.PopupPanel.Parent != null;
		}

		private void HostWindow_MouseDown(object sender, MouseEventArgs mouseEvent)
		{
			if (this.pressHandledByTrigger)
			{
				this.pressHandledByTrigger = false;
				return;
			}

			this.HandlePress(mouseEvent.Position, mouseEvent.Button, mouseEvent);
		}

		private void HostWindow_KeyDown(object sender, KeyEventArgs keyEvent)
		{
			// Escape closes the popup whatever its close behavior, so IgnoreClicks stays keyboard dismissable.
			if (keyEvent.KeyCode == Keys.Escape && this.Popup.OnEscape())
			{
				this.SetPopupOpen(false);
				keyEvent.Handled = true;
			}
		}

		private RectangleDouble TriggerBounds()
		{
			return this.hostWindow == null ? default : this.Trigger.TransformToParentSpace(this.hostWindow, this.Trigger.LocalBounds);
		}

		/// <summary>Re-places and re-describes the open popup.</summary>
		private void RefreshPopup()
		{
			this.PopupPanel.Describe(this.Popup.Align, this.Popup.Gap, this.Popup.CloseBehavior);
			if (!this.Popup.IsOpen || this.hostWindow == null)
			{
				return;
			}

			this.Popup.Anchor = this.TriggerBounds();
			this.Popup.Size = this.PopupPanel.Size;
			RectangleDouble rect = this.Popup.Rect(new Vector2(this.hostWindow.Width, this.hostWindow.Height));
			this.PopupPanel.Position = new Vector2(rect.Left, rect.Bottom);
			this.PopupShadow.Size = this.PopupPanel.Size;
			this.PopupShadow.Position = this.PopupPanel.Position + new Vector2(4, -4) * DeviceScale;
		}

		private void AlignComboChanged()
		{
			if (this.syncing)
			{
				return;
			}

			this.Popup.Align = new RectAlign(
				Align2.All[Math.Max(0, this.ParentCombo.SelectedIndex)].Align,
				Align2.All[Math.Max(0, this.ChildCombo.SelectedIndex)].Align);

			// The preset combo names the pair, or shows the placeholder for one with no name.
			this.syncing = true;
			this.PresetCombo.SelectedIndex = this.Popup.Align.PresetIndex + 1;
			this.syncing = false;
			this.RefreshPopup();
		}

		private void PresetChanged()
		{
			if (this.syncing || this.PresetCombo.SelectedIndex < 1)
			{
				return;
			}

			RectAlign preset = RectAlign.Presets[this.PresetCombo.SelectedIndex - 1].Align;
			this.syncing = true;
			this.ParentCombo.SelectedIndex = preset.Parent.AllIndex;
			this.ChildCombo.SelectedIndex = preset.Child.AllIndex;
			this.syncing = false;
			this.Popup.Align = preset;
			this.RefreshPopup();
		}

		// The popup lives in the SystemWindow, so moving or resizing anything between it and the trigger (the demo
		// window being dragged, say) has to move it too - as ShowPopup does for menus.
		private void HookAncestors()
		{
			foreach (GuiWidget ancestor in this.Trigger.Parents<GuiWidget>().Where(p => p != this.hostWindow))
			{
				if (this.hookedAncestors.Add(ancestor))
				{
					ancestor.PositionChanged += this.Ancestor_Moved;
					ancestor.BoundsChanged += this.Ancestor_Moved;
				}
			}
		}

		private void UnhookAncestors()
		{
			foreach (GuiWidget ancestor in this.hookedAncestors)
			{
				ancestor.PositionChanged -= this.Ancestor_Moved;
				ancestor.BoundsChanged -= this.Ancestor_Moved;
			}

			this.hookedAncestors.Clear();
		}

		private void Ancestor_Moved(object sender, EventArgs e) => this.RefreshPopup();

		private MenuItemModel ContextItem(string text, string action, string iconGlyph = null, string shortcut = null)
		{
			return new MenuItemModel
			{
				Text = text,
				AutomationName = "Popups Context " + action,
				IconGlyph = iconGlyph,
				IconTypeFace = iconGlyph != null ? IconFont.TypeFace : null,
				ShortcutText = shortcut,
				Action = () => this.ContextActionText.Text = "Context action: " + action,
			};
		}

		private ImageBuffer ResetIcon() => GlyphIcon.Render(IconFont.Undo, IconFont.TypeFace, this.theme.TextColor, (int)Math.Round(14 * DeviceScale));

		private TextWidget Text(string text, double pointSize)
		{
			var widget = new TextWidget(text, pointSize: pointSize, textColor: this.theme.TextColor)
			{
				AutoExpandBoundsToText = true,
				VAnchor = VAnchor.Center,
			};
			this.texts.Add(widget);
			return widget;
		}

		/// <summary>A caption of the code-style configurator.</summary>
		private TextWidget Code(string text)
		{
			TextWidget widget = this.Text(text, DemoText.Points(DemoText.BodyPixels));
			widget.Margin = new BorderDouble(0, 3, 8, 3);
			return widget;
		}

		private FlowLayoutWidget Row(string caption)
		{
			var row = new FlowLayoutWidget { HAnchor = HAnchor.Stretch, Margin = new BorderDouble(0, 3) };
			row.AddChild(this.Code(caption));
			this.panel.AddChild(row);
			return row;
		}

		/// <summary>A caption and a combo box, with an optional hover tooltip per item.</summary>
		private DropDownList ComboRow(string caption, string name, IEnumerable<string> options, string[] tips)
		{
			FlowLayoutWidget row = this.Row(caption);
			var combo = new DropDownList("", this.theme.TextColor, pointSize: DemoText.Points(DemoText.BodyPixels))
			{
				Name = name,
				VAnchor = VAnchor.Center,
			};
			int i = 0;
			foreach (string option in options)
			{
				MenuItem item = combo.AddItem(option);
				item.Name = name + " " + option;
				if (tips != null)
				{
					item.ToolTipText = tips[i];
				}

				i++;
			}

			row.AddChild(combo);
			this.combos.Add(combo);
			return combo;
		}

		private void OnThemeChanged(object sender, EventArgs e)
		{
			this.Recolor();
			this.Invalidate();
		}

		/// <summary>Pushes the current theme into the widgets that copied their colours when they were built.</summary>
		private void Recolor()
		{
			DemoPalette palette = this.demoTheme.Palette;
			foreach (TextWidget text in this.texts)
			{
				text.TextColor = palette.TextColor;
			}

			this.hintText.TextColor = palette.TextDim;
			this.ContextActionText.TextColor = this.theme.PrimaryAccentColor;
			this.menusPointer.TextColor = palette.TextColor;
			this.OpenCheckBox.TextColor = palette.TextColor;
			this.ResetButton.SetIcon(this.ResetIcon());

			// The field and chevron follow ThemeConfig.Current on their own; the text and popup rows are copied
			// into the list when it is built, so a theme change still has to push them.
			foreach (DropDownList combo in this.combos)
			{
				combo.TextColor = this.theme.TextColor;
				combo.MenuItemsBackgroundColor = this.theme.BackgroundColor;
				combo.MenuItemsTextColor = this.theme.TextColor;
				combo.MenuItemsBorderColor = palette.WindowStroke;
				combo.MenuItemsBackgroundHoverColor = this.theme.SlightShade;
				combo.MenuItemsTextHoverColor = this.theme.TextColor;
			}

			this.panel.BackgroundColor = palette.PanelFill;
			this.separator.BackgroundColor = palette.Separator;
			this.PopupPanel.Recolor(palette);
		}
	}
}
