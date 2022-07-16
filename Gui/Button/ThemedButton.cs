/*
Copyright (c) 2022, John Lewin, Lars Brubaker
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

        public ThemedButton(ThemeConfig theme)
        {
            this.theme = theme;
            HoverColor = theme.SlightShade;
            MouseDownColor = theme.MinimalShade;
            Margin = 3;
            Cursor = Cursors.Hand;
            BackgroundColor = theme.BackgroundColor.WithLightness(0.9).ToColor();

            TabStop = true;
        }

        public Color HoverColor { get; set; } = Color.Transparent;

        public Color MouseDownColor { get; set; } = Color.Transparent;

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
            if (keyEvent.KeyCode == Keys.Enter
                || keyEvent.KeyCode == Keys.Space)
            {
                UiThread.RunOnIdle(InvokeClick);
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
                    return HoverColor;
                }
                else
                {
                    return base.BackgroundColor;
                }
            }
            set => base.BackgroundColor = value;
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