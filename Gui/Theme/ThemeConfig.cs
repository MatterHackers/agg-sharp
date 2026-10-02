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
        private static ThemeConfig current;

        /// <summary>
        /// The process-wide theme that library widgets built without an explicit theme (CheckBox, RadioButton
        /// and the like) read when they draw, so changing a colour on it - or pointing it at another theme -
        /// shows on the next frame without rebuilding them. Starts as <see cref="DefaultTheme"/>; setting null
        /// goes back to a fresh default rather than throwing. Volatile so a theme set on one thread is fully
        /// published before another thread draws with it.
        /// </summary>
        public static ThemeConfig Current
        {
            get
            {
                var theme = System.Threading.Volatile.Read(ref current);
                if (theme == null)
                {
                    theme = DefaultTheme();
                    System.Threading.Interlocked.CompareExchange(ref current, theme, null);
                    theme = System.Threading.Volatile.Read(ref current);
                }

                return theme;
            }

            set => System.Threading.Volatile.Write(ref current, value);
        }

        /// <summary>
        /// Raised by <see cref="NotifyChanged"/> after the theme's colours were changed in place. Open popups
        /// (menus, their sub menus and drop down lists) listen while they are up and restyle the colours they
        /// copied from this theme when they were built, so a theme picked from an open menu shows on that menu
        /// at once instead of only on the next one opened.
        /// </summary>
        public event EventHandler Changed;

        /// <summary>
        /// Tells whatever is showing this theme that its colours changed. Call it after changing them in place;
        /// the properties themselves raise nothing, as a theme is normally built once (or deserialized) whole.
        /// Replacing <see cref="Current"/> with a new object raises nothing either: what is already built keeps
        /// the theme object it was built from.
        /// </summary>
        public void NotifyChanged() => this.Changed?.Invoke(this, EventArgs.Empty);

        public ImageBuffer RestoreNormal { get; private set; }
        public ImageBuffer RestoreHover { get; private set; }

        public Color SlightShade { get; set; } = new Color("#00000028");
        public Color MinimalShade { get; set; } = new Color("#0000000F");
        public Color TextColor { get; set; } = new Color("#333");
        public Color BackgroundColor { get; set; } = new Color("#fff");
        public Color PrimaryAccentColor { get; set; } = new Color("#7AD7F0");
        public BorderDouble TextButtonPadding { get; } = new BorderDouble(14, 0);
        /// <summary>The height of a themed button, in device pixels: <see cref="ButtonDesignHeight"/> scaled.</summary>
        public double ButtonHeight => ButtonDesignHeight * GuiWidget.DeviceScale;

        /// <summary>The height of a themed button (and of icon buttons' square), in design units.</summary>
        public double ButtonDesignHeight { get; set; } = 32;

        /// <summary>
        /// agg's default look, the one agg apps get unless they build their own theme: a warm off-white window,
        /// white controls with a 1 px warm grey border rounded at 8, a deep teal accent, and the Strip
        /// segmented control. The property initializers are deliberately the older look instead, because
        /// MatterCAD deserializes its themes over a bare <c>new ThemeConfig()</c>; the new look lives here only.
        /// </summary>
        public static ThemeConfig DefaultTheme()
        {
            var accent = new Color("#1f5f7a");
            var border = new Color("#c9c6bd");
            var text = new Color("#1d2226");
            var muted = new Color("#5b6166");
            var white = new Color("#ffffff");

            var theme = new ThemeConfig()
            {
                DefaultFontSize = 11,
                PrimaryAccentColor = accent,
                TextColor = text,
                SecondaryTextColor = new Color("#3c4449"),
                MutedTextColor = muted,
                LightTextColor = muted,
                BackgroundColor = new Color("#fbfaf7"),
                ButtonBackgroundColor = white,
                ControlFillColor = white,
                ControlBorderColor = border,

                ButtonBorderWidth = 1,
                ButtonBorderColor = border,
                ButtonHoverShadesFill = true,
                ButtonDesignHeight = 36,

                ButtonRadius = 8,
                FieldRadius = 8,
                DropDownRadius = 8,
                CardRadius = 10,
                ContainerRadius = 10,

                AccentTintColor = new Color("#eef4f6"),
                AccentTintBorderColor = new Color("#c8dbe2"),
                WarningTextColor = new Color("#8a4a12"),

                OnAccentMinimumContrast = 4.5,
                FieldDesignHeight = 36,
                SegmentedHeight = 40,
                SegmentedStyle = SegmentedStyle.Strip,

                EditFieldColors = new ThreeStateColor()
                {
                    Focused = EditFieldState(white, accent, text, muted),
                    Hovered = EditFieldState(white, accent, text, muted),
                    Inactive = EditFieldState(white, border, text, muted),
                },
            };

            theme.BorderColor20 = Color.Black.WithAlpha(140);
            theme.AccentMimimalOverlay = theme.PrimaryAccentColor.WithAlpha(128);
            theme.SlightShade = theme.PrimaryAccentColor.WithAlpha(80);
            theme.MinimalShade = theme.PrimaryAccentColor.WithAlpha(60);
            theme.RowBorder = theme.TextColor;

            return theme;
        }

        private static StateColor EditFieldState(Color fill, Color border, Color text, Color lightText)
        {
            return new StateColor()
            {
                BackgroundColor = fill,
                ForegroundColor = Color.Transparent,
                BorderColor = border,
                TextColor = text,
                LightTextColor = lightText,
            };
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

        // Shape, border and colour tokens. Every initializer is the look the widget had before it read the
        // token: MatterCAD deserializes its theme into a new ThemeConfig and keeps these for anything its
        // theme file does not name, so a default that drifted would restyle MatterCAD unasked. Radii,
        // widths and heights are design units (multiplied by GuiWidget.DeviceScale where drawn). Derived
        // values are get-only so MatterCAD's ThemeContractResolver, which saves only writable properties,
        // never bakes today's derivation into a user's saved theme.

        /// <summary>
        /// The outside height (border included) of themed text and number fields and drop downs, in design
        /// units, with their text centred. 0, the default, keeps the height their font and padding give them,
        /// exactly as before this token existed.
        /// </summary>
        public double FieldDesignHeight { get; set; } = 0;

        /// <summary>Corner radius of themed text and number fields. 0 draws the square field.</summary>
        public double FieldRadius { get; set; } = 0;

        /// <summary>Corner radius of a DropDownList's field, measured on the outside of its outline.</summary>
        public double DropDownRadius { get; set; } = 4;

        /// <summary>Corner radius of a <see cref="SelectableCard"/>.</summary>
        public double CardRadius { get; set; } = 10;

        /// <summary>Corner radius of a grouping container such as an <see cref="InfoBox"/>.</summary>
        public double ContainerRadius { get; set; } = 10;

        /// <summary>Outline width of themed buttons. 0 draws no outline.</summary>
        public double ButtonBorderWidth { get; set; } = 0;

        /// <summary>Outline colour of themed buttons, used when <see cref="ButtonBorderWidth"/> is above 0.</summary>
        public Color ButtonBorderColor { get; set; } = Color.Transparent;

        /// <summary>
        /// When true, hovering a button whose fill is opaque shades that fill with its HoverColor rather than
        /// replacing it. Off by default: MatterCAD's toolbars and dialogs are built around the replace
        /// behaviour, and a translucent fill always keeps it either way.
        /// </summary>
        public bool ButtonHoverShadesFill { get; set; } = false;

        /// <summary>
        /// The outline of fields, drop downs, segmented strips and cards. Transparent (the default) means unset:
        /// each widget keeps its own default (a DropDownList's agg-gui widget stroke, a field's EditFieldColors
        /// border). Widgets read it through <see cref="ControlBorderColorIfSet"/>.
        /// </summary>
        /// <remarks>
        /// Unset is Transparent rather than null because MatterCAD saves themes with a resolver that skips
        /// Transparent colours, while agg's Color converter reads a saved null back as Transparent - a nullable
        /// token came back from its first save and reload as a real, invisible border.
        /// </remarks>
        public Color ControlBorderColor { get; set; } = Color.Transparent;

        /// <summary><see cref="ControlBorderColor"/>, or null while it is unset (Transparent).</summary>
        public Color? ControlBorderColorIfSet => ControlBorderColor.Alpha0To255 == 0 ? null : ControlBorderColor;

        /// <summary>
        /// The fill of drop downs, a Strip segmented control and unselected cards. Transparent (the default)
        /// means unset, which is <see cref="ButtonBackgroundColor"/>; read it through
        /// <see cref="ResolvedControlFillColor"/>. Not a getter that falls back: MatterCAD saves every writable
        /// property, and would save today's ButtonBackgroundColor under this name and pin it.
        /// </summary>
        public Color ControlFillColor { get; set; } = Color.Transparent;

        /// <summary><see cref="ControlFillColor"/>, or null while it is unset (Transparent).</summary>
        public Color? ControlFillColorIfSet => ControlFillColor.Alpha0To255 == 0 ? null : ControlFillColor;

        /// <summary><see cref="ControlFillColor"/>, or <see cref="ButtonBackgroundColor"/> when it is unset.</summary>
        public Color ResolvedControlFillColor => ControlFillColorIfSet ?? ButtonBackgroundColor;

        /// <summary>Text one step quieter than <see cref="TextColor"/>: an unselected card's title.</summary>
        public Color SecondaryTextColor { get; set; } = new Color("#3c4449");

        /// <summary>Descriptions, hints and section headers.</summary>
        public Color MutedTextColor { get; set; } = new Color("#5b6166");

        /// <summary>A light wash of the accent: a selected card's or an info box's fill.</summary>
        public Color AccentTintColor { get; set; } = new Color("#eef4f6");

        /// <summary>The outline drawn around an <see cref="AccentTintColor"/> fill.</summary>
        public Color AccentTintBorderColor { get; set; } = new Color("#c8dbe2");

        /// <summary>Text that warns without being an error.</summary>
        public Color WarningTextColor { get; set; } = new Color("#8a4a12");

        /// <summary>
        /// Ink that stays readable on <see cref="PrimaryAccentColor"/>: the segmented control's long-standing rule
        /// (<see cref="TextColor"/> pushed to 3:1 against the accent). A theme that raises
        /// <see cref="OnAccentMinimumContrast"/> above 3 gets white or black, whichever reads better, wherever
        /// that rule falls short of it - DefaultTheme's dark teal otherwise gets a hard-to-read grey.
        /// </summary>
        public Color OnAccentTextColor
        {
            get
            {
                var accent = PrimaryAccentColor;
                var ink = TextColor.WithContrast(accent, 3).ToColor();
                if (OnAccentMinimumContrast <= 3 || ContrastRatio(ink, accent) >= OnAccentMinimumContrast)
                {
                    return ink;
                }

                return ContrastRatio(Color.White, accent) >= ContrastRatio(Color.Black, accent) ? Color.White : Color.Black;
            }
        }

        /// <summary>
        /// The WCAG contrast <see cref="OnAccentTextColor"/> must reach. 3 (the default) is exactly the old rule,
        /// which existing themes - AggSharpDemo's agg-gui accents among them - are drawn with; DefaultTheme asks
        /// for 4.5, the body-text minimum.
        /// </summary>
        public double OnAccentMinimumContrast { get; set; } = 3;

        /// <summary>The WCAG 2 contrast ratio of two opaque colours, from 1 (identical) to 21 (black on white).</summary>
        public static double ContrastRatio(Color a, Color b)
        {
            static double Channel(int value)
            {
                double c = value / 255.0;
                return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
            }

            static double Luminance(Color c) => 0.2126 * Channel(c.red) + 0.7152 * Channel(c.green) + 0.0722 * Channel(c.blue);

            double la = Luminance(a), lb = Luminance(b);
            return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
        }

        /// <summary>The height of a single-line control, in device pixels. The same as <see cref="ButtonHeight"/>.</summary>
        public double ControlHeight => ButtonHeight;

        /// <summary>The height of a <see cref="SegmentedControl"/>, in design units.</summary>
        public double SegmentedHeight { get; set; } = 24;

        /// <summary>How a <see cref="SegmentedControl"/> is drawn.</summary>
        public SegmentedStyle SegmentedStyle { get; set; } = SegmentedStyle.Pill;

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

        /// <summary>
        /// The left gutter a menu row reserves for its icon, in device pixels. Rows without an icon show it as
        /// empty space, so it is sized to the 16 unit icons menus actually use (5 either side) rather than to
        /// the much wider band it used to be.
        /// </summary>
        public double MenuGutterWidth => 26 * GuiWidget.DeviceScale;

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

        /// <summary>
        /// A small, bold, uppercase label in <see cref="MutedTextColor"/> that opens a group of settings.
        /// Named "<c>text</c> Header".
        /// </summary>
        public TextWidget CreateSectionHeader(string text)
        {
            return new TextWidget(text.ToUpperInvariant(), pointSize: DefaultFontSize - 2, textColor: MutedTextColor, bold: true)
            {
                Name = text + " Header",
                Margin = new BorderDouble(0, 2),
            };
        }

        /// <summary>
        /// The one button that carries a view's main action: accent fill, bold <see cref="OnAccentTextColor"/>
        /// text, a darker accent while hovered and pressed. Disabled, it turns to a pale accent wash with
        /// <see cref="MutedTextColor"/> text, so it no longer looks clickable.
        /// </summary>
        public ThemedTextButton CreatePrimaryButton(string text)
        {
            var accent = PrimaryAccentColor;
            var button = new ThemedTextButton(text, this)
            {
                Name = text + " Button",
                BackgroundColor = accent,
                HoverColor = accent.Blend(Color.Black, .1),
                MouseDownColor = accent.Blend(Color.Black, .18),
                TextColor = OnAccentTextColor,
                Bold = true,
                DisabledFillColor = accent.Blend(BackgroundColor, .8),
            };

            // After TextColor, which resets it.
            button.DisabledTextColor = MutedTextColor;

            // A primary action has no outline: the fill is the whole look.
            button.BackgroundOutlineWidth = 0;
            return button;
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

        public GridColors BedGridColors { get; set; } = new GridColors();
        public Color ButtonBackgroundColor { get; set; }

        public GuiWidget CreateSearchButton()
        {
            return new ThemedIconButton(StaticData.Instance.LoadIcon(StaticData.Instance.PreferSvgIcon("icon_search_24x24.png"), 16, 16).GrayToColor(TextColor), this)
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

    /// <summary>The two looks of a <see cref="SegmentedControl"/>.</summary>
    public enum SegmentedStyle
    {
        /// <summary>A shaded rounded track with the selected segment as an inset accent pill (agg-gui, macOS).</summary>
        Pill,

        /// <summary>
        /// A <see cref="ThemeConfig.ResolvedControlFillColor"/> strip in a 1 px rounded outline, square full height
        /// dividers, and the selected segment filled edge to edge with the accent.
        /// </summary>
        Strip,
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