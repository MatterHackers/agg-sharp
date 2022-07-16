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
using System.Collections.Generic;

namespace MatterHackers.Agg.UI
{
    public class ThemedRadioTextButton : ThemedTextButton, IRadioButton
    {
        public IList<GuiWidget> SiblingRadioButtonList { get; set; }

        public event EventHandler CheckedStateChanged;

        public ThemedRadioTextButton(string text, ThemeConfig theme, double pointSize = -1)
            : base(text, theme, pointSize)
        {
            SelectedBackgroundColor = theme.SlightShade;
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
                    if (Checked)
                    {
                        return SelectedBackgroundColor.AdjustLightness(.9).ToColor();
                    }

                    return MouseDownColor;
                }
                else if (firstWidgetUnderMouse
                    && Enabled)
                {
                    if (Checked)
                    {
                        return SelectedBackgroundColor.AdjustLightness(.8).ToColor();
                    }

                    return HoverColor;
                }
                else
                {
                    return base.BackgroundColor;
                }
            }
            set => base.BackgroundColor = value;
        }

        protected override void OnClick(MouseEventArgs mouseEvent)
        {
            base.OnClick(mouseEvent);

            bool newValue = true;

            bool checkStateChanged = newValue != Checked;

            Checked = newValue;

            // After setting CheckedState, fire event if different
            if (checkStateChanged)
            {
                OnCheckStateChanged();
            }
        }

        public Color SelectedBackgroundColor { get; set; }

        public Color UnselectedBackgroundColor { get; set; }

        private bool _checked;

        public bool Checked
        {
            get => _checked;
            set
            {
                if (_checked != value)
                {
                    _checked = value;
                    if (_checked)
                    {
                        this.UncheckSiblings();
                    }

                    OnCheckStateChanged();
                }

                BackgroundColor = _checked ? SelectedBackgroundColor : UnselectedBackgroundColor;
            }
        }

        public bool DrawUnderline { get; set; } = true;

        public override void OnMouseEnterBounds(MouseEventArgs mouseEvent)
        {
            base.OnMouseEnterBounds(mouseEvent);
            Invalidate();
        }

        public override void OnMouseLeaveBounds(MouseEventArgs mouseEvent)
        {
            base.OnMouseLeaveBounds(mouseEvent);
            Invalidate();
        }

        public virtual void OnCheckStateChanged()
        {
            CheckedStateChanged?.Invoke(this, null);
        }

        public override void OnDraw(Graphics2D graphics2D)
        {
            if (Checked && DrawUnderline)
            {
                graphics2D.Rectangle(LocalBounds.Left, 0, LocalBounds.Right, 2, theme.PrimaryAccentColor);
            }

            base.OnDraw(graphics2D);
        }
    }
}