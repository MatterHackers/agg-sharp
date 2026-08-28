/*
Copyright (c) 2026, Lars Brubaker, John Lewin
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
        public Color PrimaryAccentColor { get; set; } = new Color("#7AD7F0");
        public BorderDouble TextButtonPadding { get; } = new BorderDouble(14, 0);
        public double ButtonHeight => 32 * GuiWidget.DeviceScale;

        public static ThemeConfig DefaultTheme()
        {
            var theme = new ThemeConfig()
            {
                DefaultFontSize = 11,
                EditFieldColors = new ThreeStateColor()
                {
                    Focused = new StateColor()
                    {
                        BackgroundColor = new Color("#fff"),
                        ForegroundColor = new Color("#00000000"),
                        BorderColor = new Color("#FF7F00"),
                        TextColor = new Color("#222222"),
                        LightTextColor = new Color("#6e6e6e")
                    },
                    Hovered = new StateColor()
                    {
                        BackgroundColor = new Color("#fff"),
                        ForegroundColor = new Color("#00000000"),
                        BorderColor = new Color("#FF7F00"),
                        TextColor = new Color("#00000000")
                        // LightTextColor = new Color("#")
                    },
                    Inactive = new StateColor()
                    {
                        BackgroundColor = new Color("#fff"),
                        ForegroundColor = new Color("#00000000"),
                        BorderColor = new Color("#ccc"),
                        TextColor = new Color("#222222"),
                        LightTextColor = new Color("#6e6e6e")
                    }
                },
            };

            theme.ButtonBackgroundColor = Color.LightGray;
            theme.BorderColor20 = Color.Black.WithAlpha(140);
            theme.AccentMimimalOverlay = theme.PrimaryAccentColor.WithAlpha(128);
            theme.SlightShade = theme.PrimaryAccentColor.WithAlpha(80);
            theme.MinimalShade = theme.PrimaryAccentColor.WithAlpha(60);
            theme.RowBorder = theme.TextColor;

            theme.ButtonBackgroundColor = theme.BackgroundColor.WithLightness(0.9).ToColor();
            return theme;
        }

        public static ThemeConfig DefaultMenuTheme()
        {
            var theme = new ThemeConfig()
            {
                DefaultFontSize = 11,
                EditFieldColors = new ThreeStateColor()
                {
                    Focused = new StateColor()
                    {
                        BackgroundColor = new Color("#fff"),
                        ForegroundColor = new Color("#00000000"),
                        BorderColor = new Color("#FF7F00"),
                        TextColor = new Color("#222222"),
                        LightTextColor = new Color("#6e6e6e")
                    },
                    Hovered = new StateColor()
                    {
                        BackgroundColor = new Color("#fff"),
                        ForegroundColor = new Color("#00000000"),
                        BorderColor = new Color("#FF7F00"),
                        TextColor = new Color("#00000000")
                        // LightTextColor = new Color("#")
                    },
                    Inactive = new StateColor()
                    {
                        BackgroundColor = new Color("#fff"),
                        ForegroundColor = new Color("#00000000"),
                        BorderColor = new Color("#ccc"),
                        TextColor = new Color("#222222"),
                        LightTextColor = new Color("#6e6e6e")
                    }
                },
                BackgroundColor = Color.LightGray
            };

            theme.ButtonBackgroundColor = Color.LightGray;
            theme.BorderColor20 = Color.Black.WithAlpha(140);
            theme.AccentMimimalOverlay = theme.PrimaryAccentColor.WithAlpha(128);
            theme.SlightShade = theme.PrimaryAccentColor.WithAlpha(80);
            theme.MinimalShade = theme.PrimaryAccentColor.WithAlpha(60);
            theme.RowBorder = theme.TextColor;

            return theme;
        }

        /// <summary>
        /// The small X used to close a dialog or clear a field.
        /// </summary>
        /// <remarks>
        /// The glyphs are drawn here rather than handed out from <see cref="RestoreNormal"/> because an
        /// ImageWidget is exactly as big as the bitmap it is given, and a ThemeConfig outlives the display it
        /// was built on - a window dragged to a monitor of another scale rebuilds its widgets against the
        /// same theme instance. Reusing the constructor's bitmaps left this button, alone among the chrome,
        /// at the size of whichever display the theme happened to be created on.
        /// </remarks>
        public GuiWidget CreateSmallResetButton()
        {
            var (normal, hover) = RestoreImages();

            return new HoverImageWidget(normal, hover)
            {
                VAnchor = VAnchor.Center,

                // Design units - GuiWidget.Margin multiplies by DeviceScale on the way in. Only the bitmap
                // below is in device pixels, because an ImageWidget's bounds are its pixels.
                Margin = new BorderDouble(0, 0, 5, 0)
            };
        }


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

        /// <summary>
        /// The height of one popup menu row, not counting <see cref="MenuRowInset"/>.
        /// </summary>
        /// <remarks>
        /// Deliberately shorter than <see cref="ButtonHeight"/>: a menu is a dense list to read down, not a
        /// row of click targets, and Windows 11 - the styling reference here - lands near this once the
        /// inset above and below each row is added back on.
        /// </remarks>
        public double MenuRowHeight => 24 * GuiWidget.DeviceScale;

        /// <summary>
        /// Corner radius of the menu panel itself, in device units. Windows 11 rounds menu popups at 8.
        /// </summary>
        public double MenuPopupRadius => DefaultMenuPopupRadius;

        /// <summary>
        /// Corner radius of a row's hover and keyboard highlight, in device units.
        /// </summary>
        /// <remarks>
        /// Rounding the highlight is half of what keeps the first and last rows from poking square corners
        /// out of the rounded panel; <see cref="MenuRowInset"/> is the other half.
        /// </remarks>
        public double MenuRowRadius => DefaultMenuRowRadius;

        /// <summary>
        /// <see cref="MenuPopupRadius"/> for widgets that have no theme to ask.
        /// </summary>
        /// <remarks>
        /// The legacy menu family - Menu, MenuItem and the states views, which drop down lists are built
        /// from - predates ThemeConfig and is coloured by per widget properties instead. It still has to
        /// round the same way a PopupMenu does or drop downs and menus stop matching, so the menu chrome
        /// radii are readable without an instance. They are the same numbers, not a second set.
        /// </remarks>
        public static double DefaultMenuPopupRadius => 8 * GuiWidget.DeviceScale;

        /// <summary>
        /// <see cref="MenuRowRadius"/> for widgets that have no theme to ask. See
        /// <see cref="DefaultMenuPopupRadius"/> for why this exists.
        /// </summary>
        public static double DefaultMenuRowRadius => 4 * GuiWidget.DeviceScale;

        /// <summary>
        /// The gap between a row's highlight and the edge of the menu panel, in design units (a widget's
        /// Margin is multiplied by DeviceScale on the way in, so this must not be pre-scaled).
        /// </summary>
        /// <remarks>
        /// Windows 11 insets the highlight rather than clipping it: with the highlight held clear of the
        /// panel's rounded corners there is nothing for those corners to cut off, so the top and bottom rows
        /// look the same as every row between them.
        /// </remarks>
        public BorderDouble MenuRowInset { get; } = new BorderDouble(3, 2);

        public double MicroButtonHeight => 20 * GuiWidget.DeviceScale;

        private double MicroButtonWidth => 30 * GuiWidget.DeviceScale;

        public void MakeRoundedButton(GuiWidget button, Color? borderColor = null)
        {
            if (button is ThemedTextButton textButton)
            {
                textButton.VAnchor |= VAnchor.Fit;
                textButton.HAnchor |= HAnchor.Fit;
                textButton.HoverColor = AccentMimimalOverlay;
                textButton.Padding = new BorderDouble(7, 5);
                if (borderColor != null)
                {
                    textButton.BorderColor = borderColor.Value;
                }
                else
                {
                    textButton.BorderColor = TextColor;
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
                    flowButton.HoverColor = parentIsToolbar ? ToolbarButtonHover : Color.Transparent;
                    break;
                case ThemedButton button:
                    button.HoverColor = parentIsToolbar ? ToolbarButtonHover : Color.Transparent;
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

        public Color TabBarBackground { get; set; } = new Color("#f5f5f5");

        public Color InactiveTabColor { get; set; }

        public Color InteractionLayerOverlayColor { get; set; }

        public TextWidget CreateHeading(string text)
        {
            return new TextWidget(text, pointSize: H1PointSize, textColor: TextColor, bold: true)
            {
                Margin = new BorderDouble(0, 5)
            };
        }

        public Color SplitterBackground { get; set; } = new Color(0, 0, 0, 60);

        public Color TabBodyBackground { get; set; }

        public Color ToolbarButtonBackground { get; set; } = Color.Transparent;

        public Color ToolbarButtonHover => SlightShade;

        public Color ToolbarButtonDown => MinimalShade;

        public Color ThumbnailBackground { get; set; }

        public Color AccentMimimalOverlay { get; set; }

        public BorderDouble SeparatorMargin { get; }

        /// <summary>
        /// The placeholder shown while a real thumbnail is rendered. Supplied by the application - agg has no
        /// icon of its own for this - and, being rasterized at a fixed device size, re-supplied when
        /// <see cref="GuiWidget.DeviceScale"/> changes.
        /// </summary>
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

        public Color BorderColor20 { get; set; }

        public void EnsureDefaults()
        {
            // EnsureDefaults is called after deserialization and at a point when state should be fully loaded. Invoking RebuildTheme here ensures icons shaded correctly
            RebuildTheme();
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
        public Color ButtonBackgroundColor { get; set; }

        public GuiWidget CreateSearchButton()
        {
            return new ThemedIconButton(StaticData.Instance.LoadIcon("icon_search_24x24.png", 16, 16).GrayToColor(TextColor), this)
            {
                ToolTipText = "Search".Localize(),
            };
        }

        public ThemeConfig()
        {
            SeparatorMargin = (ButtonSpacing * 2).Clone(left: ButtonSpacing.Right);
            RebuildTheme();
        }

        public void SetDefaults()
        {
            DisabledColor = new Color(LightTextColor, 50);
            SplashAccentColor = new Color(PrimaryAccentColor, 185).OverlayOn(Color.White).ToColor();
        }

        /// <summary>
        /// Rasterizes the two states of the small X glyph for the current <see cref="GuiWidget.DeviceScale"/>.
        /// </summary>
        private static (ImageBuffer normal, ImageBuffer hover) RestoreImages()
        {
            int size = (int)(16 * GuiWidget.DeviceScale);

            // On Android, use red icon as no hover events, otherwise transparent and red on hover
            return (ColorCircle(size, AggContext.OperatingSystem == OSType.Android ? new Color(200, 0, 0) : Color.Transparent),
                ColorCircle(size, new Color("#DB4437")));
        }

        /// <summary>
        /// Re-derives everything the theme rasterizes up front, so that a change to
        /// <see cref="GuiWidget.DeviceScale"/> reaches the bitmaps as well as the widgets.
        /// </summary>
        /// <remarks>
        /// New ImageBuffer instances every time - anything already holding one keeps the old bitmap, which is
        /// why the caller has to be a point where the UI is about to be built again.
        /// </remarks>
        public void RebuildTheme()
        {
            (RestoreNormal, RestoreHover) = RestoreImages();

            //this.GeneratingThumbnailIcon = StaticData.Instance.LoadIcon("building_thumbnail_40x40.png", 40, 40).SetToColor(TextColor);

            ScrollBar.DefaultBackgroundColor = TextColor.WithAlpha(30);
            ScrollBar.DefaultThumbColor = TextColor.WithAlpha(130);
            ScrollBar.DefaultThumbHoverColor = PrimaryAccentColor.WithAlpha(130);
        }

        public ThemedRadioTextButton CreateMicroRadioButton(string text, IList<GuiWidget> siblingRadioButtonList = null)
        {
            var radioButton = new ThemedRadioTextButton(text, this, FontSize8)
            {
                SiblingRadioButtonList = siblingRadioButtonList,
                Padding = new BorderDouble(5, 0),
                SelectedBackgroundColor = SlightShade,
                UnselectedBackgroundColor = SlightShade,
                HoverColor = AccentMimimalOverlay,
                Margin = new BorderDouble(right: 1),
                HAnchor = HAnchor.Absolute,
                Height = MicroButtonHeight,
                Width = MicroButtonWidth
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
            return CreateDialogButton(text, SlightShade, SlightShade.WithAlpha(75));
        }

        public ThemedTextButton CreateDialogButton(string text, Color backgroundColor, Color hoverColor)
        {
            return new ThemedTextButton(text, this)
            {
                BackgroundColor = backgroundColor,
                HoverColor = hoverColor,
                MinimumSize = new Vector2(75 * GuiWidget.DeviceScale, 0),
                Margin = ButtonSpacing
            };
        }

        public Color GetBorderColor(int alpha)
        {
            return new Color(BorderColor, alpha);
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
            widget.BorderColor = shadedBorder ? MinimalShade : BorderColor20;

            ApplyBorder(widget, new BorderDouble(bottom: 1), shadedBorder);
        }

        public void ApplyBorder(GuiWidget widget, BorderDouble border, bool shadedBorder = false)
        {
            widget.BorderColor = shadedBorder ? MinimalShade : BorderColor20;
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

        public Action<PopupMenu> ExtendPopupMenu { get; set; }

        public string ButtonText { get; set; }

        public Color BackgroundColor { get; set; }
    }
}