/*
Copyright (c) 2026, John Lewin, Lars Brubaker
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
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.Agg.UI
{
    public class ThemedButton : GuiWidget
    {
        protected ThemeConfig theme;

        private bool hasKeyboardFocus;

        /// <summary>
        /// If this button has focus and enter is pressed, press the button
        /// </summary>
        public bool ClickOnEnter { get; set; } = true;

        public ThemedButton(ThemeConfig theme)
        {
            this.theme = theme;
            // Bound rather than copied, so a button in an open popup follows a theme changed in place
            // (ThemeConfig.Changed); a colour a caller sets afterwards replaces the binding's
            ThemeBindings.Bind(this, nameof(HoverColor), theme, t => t.SlightShade, () => HoverColor, c => HoverColor = c);
            ThemeBindings.Bind(this, nameof(MouseDownColor), theme, t => t.MinimalShade, () => MouseDownColor, c => MouseDownColor = c);
            Margin = new BorderDouble(3, 0);
            Cursor = Cursors.Hand;
            ThemeBindings.Bind(this, nameof(BackgroundColor), theme, t => t.ButtonBackgroundColor, () => FillColor, c => FillColor = c);

            // The theme's optional outline. GuiWidget draws it inside the bounds, so an outlined button is
            // the same size as a plain one; at the default width of 0 nothing is drawn.
            if (theme.ButtonBorderWidth > 0)
            {
                BackgroundOutlineWidth = theme.ButtonBorderWidth;
                BorderColor = theme.ButtonBorderColor;
            }

            TabStop = true;
        }

        public Color HoverColor { get; set; } = Color.Transparent;

        public Color MouseDownColor { get; set; } = Color.Transparent;

        /// <summary>
        /// The fill while disabled, or null to keep the normal fill. A button whose fill carries its meaning (a
        /// primary action's accent) sets this so that, disabled, it stops looking clickable.
        /// </summary>
        public Color? DisabledFillColor { get; set; }

        /// <summary>
        /// The outline. Under a theme that outlines its buttons (ButtonBorderWidth above 0) a disabled button's
        /// outline fades toward the theme background, as its label does; other themes draw it unchanged.
        /// </summary>
        public override Color BorderColor
        {
            get
            {
                var color = base.BorderColor;
                if (!Enabled
                    && theme.ButtonBorderWidth > 0
                    && BackgroundOutlineWidth > 0)
                {
                    return color.Blend(theme.BackgroundColor, .6);
                }

                return color;
            }

            set => base.BorderColor = value;
        }

        public override void OnMouseDown(MouseEventArgs mouseEvent)
        {
            base.OnMouseDown(mouseEvent);
            Invalidate();
        }

        public override void OnMouseUp(MouseEventArgs mouseEvent)
        {
            base.OnMouseUp(mouseEvent);
            Invalidate();
        }

        protected override void OnClick(MouseEventArgs mouseEvent)
        {
            if (mouseEvent.Button == MouseButtons.Left)
            {
                base.OnClick(mouseEvent);
            }
        }

        public override void OnKeyUp(KeyEventArgs keyEvent)
        {
            if (ClickOnEnter
                && (keyEvent.KeyCode == Keys.Enter
                || keyEvent.KeyCode == Keys.Space))
            {
                UiThread.RunOnIdle(InvokeClick);
                keyEvent.Handled = true;
            }

            base.OnKeyUp(keyEvent);
        }

        public override void OnMouseEnterBounds(MouseEventArgs mouseEvent)
        {
            Invalidate();
            base.OnMouseEnterBounds(mouseEvent);
        }

        public override void OnMouseLeaveBounds(MouseEventArgs mouseEvent)
        {
            Invalidate();
            base.OnMouseLeaveBounds(mouseEvent);
        }

        public override Color BackgroundColor
        {
            get
            {
                if (!Enabled && DisabledFillColor is Color disabledFill)
                {
                    return disabledFill;
                }

                var firstWidgetUnderMouse = ContainsFirstUnderMouseRecursive();
                if (MouseCaptured
                    && firstWidgetUnderMouse
                    && Enabled)
                {
                    return MouseDownColor;
                }
                else if (firstWidgetUnderMouse
                    && Enabled)
                {
                    return HoverFill(base.BackgroundColor);
                }
                else
                {
                    return base.BackgroundColor;
                }
            }
            set => base.BackgroundColor = value;
        }

        /// <summary>The resting fill, whatever state the button is drawn in (the getter above answers per state).</summary>
        internal Color FillColor
        {
            get => base.BackgroundColor;
            set => base.BackgroundColor = value;
        }

        /// <summary>
        /// The hovered fill. Normally <see cref="HoverColor"/> replaces the fill outright - MatterCAD's toolbar
        /// buttons have a transparent fill and a translucent hover, and depend on that. A theme that sets
        /// <see cref="ThemeConfig.ButtonHoverShadesFill"/> instead gets an opaque fill shaded by the hover
        /// colour, so an outlined white button darkens a touch rather than turning see-through.
        /// </summary>
        private Color HoverFill(Color fill)
        {
            if (theme.ButtonHoverShadesFill
                && fill.Alpha0To255 == 255
                && HoverColor.Alpha0To255 < 255)
            {
                return ThemeConfig.ResolveColor2(fill, HoverColor);
            }

            return HoverColor;
        }

        public override void OnFocusChanged(EventArgs e)
        {
            hasKeyboardFocus = Focused && !ContainsFirstUnderMouseRecursive();
            Invalidate();

            base.OnFocusChanged(e);
        }

        public override void OnDraw(Graphics2D graphics2D)
        {
            base.OnDraw(graphics2D);

            if (TabStop
                && hasKeyboardFocus)
            {
                var bounds = LocalBounds;
                var stroke = 1 * DeviceScale;
                var expand = stroke / 2;
                var rect = new RoundedRect(bounds.Left + expand,
                    bounds.Bottom + expand,
                    bounds.Right - expand,
                    bounds.Top - expand);
                rect.radius(BackgroundRadius.SW,
                    BackgroundRadius.SE,
                    BackgroundRadius.NE,
                    BackgroundRadius.NW);

                var rectOutline = new Stroke(rect, stroke);

                graphics2D.Render(rectOutline, theme.EditFieldColors.Focused.BorderColor);
            }
        }
    }
}