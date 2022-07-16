/*
Copyright (c) 2022, Lars Brubaker, John Lewin
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
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.ImageProcessing;
using MatterHackers.Localizations;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.UI
{
	public class ThemeConfig
	{
		public ImageBuffer RestoreNormal { get; private set; }
		public ImageBuffer RestoreHover { get; private set; }

		public Color SlightShade { get; set; } = new Color("#00000028");
		public Color MinimalShade { get; set; } = new Color("#0000000F");
		public Color TextColor { get; set; } = new Color("#333");
		public Color BackgroundColor { get; set; } = new Color("#fff");
		public Color PrimaryAccentColor { get; set; } = new Color("#B58900");
		public BorderDouble TextButtonPadding { get; } = new BorderDouble(14, 0);
		public double ButtonHeight => 32 * GuiWidget.DeviceScale;
		public double ButtonRadius { get; set; } = 3;

		public int FontSize7 { get; } = 7;

		public int FontSize8 { get; } = 8;

		public int FontSize9 { get; } = 9;

		public int FontSize10 { get; } = 10;

		public int FontSize11 { get; } = 11;

		public int FontSize12 { get; } = 12;

		public int FontSize14 { get; } = 14;

		public int DefaultFontSize { get; set; } = 11;

		public int DefaultContainerPadding { get; } = 5;

		public int H1PointSize { get; } = 11;

		public double TabButtonHeight => 30 * GuiWidget.DeviceScale;

		public double MenuGutterWidth => 35 * GuiWidget.DeviceScale;

		public double MicroButtonHeight => 20 * GuiWidget.DeviceScale;

		private double MicroButtonWidth => 30 * GuiWidget.DeviceScale;

		private readonly int defaultScrollBarWidth = 120;

		public void MakeRoundedButton(GuiWidget button, Color? boarderColor = null)
		{
			if (button is ThemedTextButton textButton)
			{
				textButton.VAnchor |= VAnchor.Fit;
				textButton.HAnchor |= HAnchor.Fit;
				textButton.HoverColor = this.AccentMimimalOverlay;
				textButton.Padding = new BorderDouble(7, 5);
				if (boarderColor != null)
				{
					textButton.BorderColor = boarderColor.Value;
				}
				else
				{
					textButton.BorderColor = this.TextColor;
				}
				textButton.BackgroundOutlineWidth = 1;
				textButton.BackgroundRadius = textButton.Height / 2;
			}
		}

		internal void RemovePrimaryActionStyle(GuiWidget guiWidget)
		{
			guiWidget.BackgroundColor = Color.Transparent;

			// Buttons in toolbars should revert to ToolbarButtonHover when reset
			bool parentIsToolbar = guiWidget.Parent?.Parent is Toolbar;

			switch (guiWidget)
			{
				case ThemedFlowButton flowButton:
					flowButton.HoverColor = parentIsToolbar ? this.ToolbarButtonHover : Color.Transparent;
					break;
				case ThemedButton button:
					button.HoverColor = parentIsToolbar ? this.ToolbarButtonHover : Color.Transparent;
					break;
			}
		}


		public BorderDouble ButtonSpacing { get; } = new BorderDouble(right: 3);

		public BorderDouble ToolbarPadding { get; } = 3;

		public BorderDouble TabbarPadding { get; } = new BorderDouble(3, 1);

		/// <summary>
		/// Gets the height or width of a given vertical or horizontal splitter bar
		/// </summary>
		public int SplitterWidth
		{
			get
			{
				double splitterSize = 6 * GuiWidget.DeviceScale;

				if (GuiWidget.TouchScreenMode)
				{
					splitterSize *= 1.4;
				}

				return (int)splitterSize;
			}
		}

		public PresetColors PresetColors { get; set; } = new PresetColors();

		public bool IsDarkTheme { get; set; }

		public Color Shade { get; set; }

		public Color DarkShade { get; set; }


		public Color TabBarBackground { get; set; }

		public Color InactiveTabColor { get; set; }

		public Color InteractionLayerOverlayColor { get; set; }

		public TextWidget CreateHeading(string text)
		{
			return new TextWidget(text, pointSize: this.H1PointSize, textColor: this.TextColor, bold: true)
			{
				Margin = new BorderDouble(0, 5)
			};
		}

		public Color SplitterBackground { get; set; } = new Color(0, 0, 0, 60);

		public Color TabBodyBackground { get; set; }

		public Color ToolbarButtonBackground { get; set; } = Color.Transparent;

		public Color ToolbarButtonHover => this.SlightShade;

		public Color ToolbarButtonDown => this.MinimalShade;

		public Color ThumbnailBackground { get; set; }

		public Color AccentMimimalOverlay { get; set; }

		public BorderDouble SeparatorMargin { get; }

		public ImageBuffer GeneratingThumbnailIcon { get; set; }

		public class StateColor
		{
			public Color BackgroundColor { get; set; }

			public Color ForegroundColor { get; set; }

			public Color BorderColor { get; set; }

			public Color TextColor { get; set; }

			public Color LightTextColor { get; set; }
		}

		public class ThreeStateColor
		{
			public StateColor Focused { get; set; } = new StateColor();

			public StateColor Hovered { get; set; } = new StateColor();

			public StateColor Inactive { get; set; } = new StateColor();
		}

		public class DropListStyle : ThreeStateColor
		{
			public StateColor Open { get; set; } = new StateColor();
		}

		public ThreeStateColor EditFieldColors { get; set; } = new ThreeStateColor();

		public Color LightTextColor { get; set; }

		public Color BorderColor { get; set; }

		public Color BorderColor40 { get; set; }

		public Color BorderColor20 { get; set; }

		public void EnsureDefaults()
		{
			// EnsureDefaults is called after deserialization and at a point when state should be fully loaded. Invoking RebuildTheme here ensures icons shaded correctly
			this.RebuildTheme();
		}

		public Color RowBorder { get; set; }

		public DropListStyle DropList { get; set; } = new DropListStyle();

		public Color DisabledColor { get; set; }

		public Color SplashAccentColor { get; set; }

		public Color BedBackgroundColor { get; set; }

		public Color SectionBackgroundColor { get; set; }

		public Color PopupBorderColor { get; set; }

		public Color BedColor { get; set; }

		public Color UnderBedColor { get; set; }

		public Color PrinterBedTextColor { get; set; }

		public GridColors BedGridColors { get; set; } = new GridColors();

		public GuiWidget CreateSearchButton()
		{
			return new ThemedIconButton(StaticData.Instance.LoadIcon("icon_search_24x24.png", 16, 16).SetToColor(TextColor), this)
			{
				ToolTipText = "Search".Localize(),
			};
		}

		public ThemeConfig()
		{
			this.SeparatorMargin = (this.ButtonSpacing * 2).Clone(left: this.ButtonSpacing.Right);
			this.RebuildTheme();
		}

		public void SetDefaults()
		{
			this.DisabledColor = new Color(this.LightTextColor, 50);
			this.SplashAccentColor = new Color(this.PrimaryAccentColor, 185).OverlayOn(Color.White).ToColor();
		}

		public void RebuildTheme()
		{
			int size = (int)(16 * GuiWidget.DeviceScale);

			// On Android, use red icon as no hover events, otherwise transparent and red on hover
			RestoreNormal = ColorCircle(size, (AggContext.OperatingSystem == OSType.Android) ? new Color(200, 0, 0) : Color.Transparent);
			RestoreHover = ColorCircle(size, new Color("#DB4437"));

			//this.GeneratingThumbnailIcon = StaticData.Instance.LoadIcon("building_thumbnail_40x40.png", 40, 40).SetToColor(TextColor);

			ScrollBar.DefaultBackgroundColor = this.TextColor.WithAlpha(30);
			ScrollBar.DefaultThumbColor = this.TextColor.WithAlpha(130);
			ScrollBar.DefaultThumbHoverColor = this.PrimaryAccentColor.WithAlpha(130);
		}

		public ThemedRadioTextButton CreateMicroRadioButton(string text, IList<GuiWidget> siblingRadioButtonList = null)
		{
			var radioButton = new ThemedRadioTextButton(text, this, this.FontSize8)
			{
				SiblingRadioButtonList = siblingRadioButtonList,
				Padding = new BorderDouble(5, 0),
				SelectedBackgroundColor = this.SlightShade,
				UnselectedBackgroundColor = this.SlightShade,
				HoverColor = this.AccentMimimalOverlay,
				Margin = new BorderDouble(right: 1),
				HAnchor = HAnchor.Absolute,
				Height = this.MicroButtonHeight,
				Width = this.MicroButtonWidth
			};

			// Add to sibling list if supplied
			siblingRadioButtonList?.Add(radioButton);

			return radioButton;
		}

		public ThemedTextButton CreateLightDialogButton(string text)
		{
			return CreateDialogButton(text, new Color(Color.White, 15), new Color(Color.White, 25));
		}

		public ThemedTextButton CreateDialogButton(string text)
		{
			return CreateDialogButton(text, this.SlightShade, this.SlightShade.WithAlpha(75));
		}

		public ThemedTextButton CreateDialogButton(string text, Color backgroundColor, Color hoverColor)
		{
			return new ThemedTextButton(text, this)
			{
				BackgroundColor = backgroundColor,
				HoverColor = hoverColor,
				MinimumSize = new Vector2(75, 0),
				Margin = this.ButtonSpacing
			};
		}

		public Color GetBorderColor(int alpha)
		{
			return new Color(this.BorderColor, alpha);
		}

		// Compute an opaque color from a source and a target with alpha
		public Color ResolveColor(Color background, Color overlay)
		{
			return ResolveColor2(background, overlay);
		}

		// Compute an opaque color from a source and a target with alpha
		public static Color ResolveColor2(Color background, Color overlay)
		{
			return new BlenderBGRA().Blend(background, overlay);
		}

		private static ImageBuffer ColorCircle(int size, Color color)
		{
			var imageBuffer = new ImageBuffer(size, size);
			Graphics2D normalGraphics = imageBuffer.NewGraphics2D();
			var center = new Vector2(size / 2.0, size / 2.0);

			Color barColor;
			if (color != Color.Transparent)
			{
				normalGraphics.Circle(center, size / 2.0, color);
				barColor = Color.White;
			}
			else
			{
				barColor = new Color("#999");
			}

			normalGraphics.Line(center + new Vector2(-size / 4.0, -size / 4.0), center + new Vector2(size / 4.0, size / 4.0), barColor, 2 * GuiWidget.DeviceScale);
			normalGraphics.Line(center + new Vector2(-size / 4.0, size / 4.0), center + new Vector2(size / 4.0, -size / 4.0), barColor, 2 * GuiWidget.DeviceScale);

			return imageBuffer;
		}

		public MenuItem CreateCheckboxMenuItem(string text, string itemValue, bool itemChecked, BorderDouble padding, EventHandler eventHandler)
		{
			var checkbox = new CheckBox(text)
			{
				Checked = itemChecked
			};
			checkbox.CheckedStateChanged += eventHandler;

			return new MenuItem(checkbox, itemValue)
			{
				Padding = padding,
			};
		}

		public void ApplyBottomBorder(GuiWidget widget, bool shadedBorder = false)
		{
			widget.BorderColor = shadedBorder ? this.MinimalShade : this.BorderColor20;

			this.ApplyBorder(widget, new BorderDouble(bottom: 1), shadedBorder);
		}

		public void ApplyBorder(GuiWidget widget, BorderDouble border, bool shadedBorder = false)
		{
			widget.BorderColor = shadedBorder ? this.MinimalShade : this.BorderColor20;
			widget.Border = border;
		}
	}

	public class PresetColors
	{
		public Color MaterialPreset { get; set; } = Color.Orange;

		public Color ScenePreset { get; set; } = Color.Green;

		public Color QualityPreset { get; set; } = Color.Yellow;

		public Color UserOverride { get; set; } = new Color(68, 95, 220, 150);
	}

	public class GridColors
	{
		public Color Red { get; set; }

		public Color Green { get; set; }

		public Color Blue { get; set; }

		public Color Line { get; set; }
	}

	public class SplitButtonParams
	{
		public ImageBuffer Icon { get; set; }

		public bool ButtonEnabled { get; set; } = true;

		public string ButtonName { get; set; }

		public Action<GuiWidget> ButtonAction { get; set; }

		public string ButtonTooltip { get; set; }

		public Action MenuAction { get; set; }

		public string ButtonText { get; set; }

		public Color BackgroundColor { get; set; }
	}
}