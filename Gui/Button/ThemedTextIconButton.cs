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
using MatterHackers.Agg.Image;
using MatterHackers.ImageProcessing;

namespace MatterHackers.Agg.UI
{
    public class ThemedTextIconButton : ThemedFlowButton
    {
        private TextWidget textWidget;

        private ImageBuffer icon;

        private bool drawIconOverlayOnDisabled;

        /// <summary>
        /// When true, the icon is shown alpha-dimmed while the button is disabled and restored to
        /// the original artwork when it is enabled again. This used to paint a translucent rect of
        /// theme.BackgroundColor over the icon instead, which only blended on buttons that actually
        /// sat on the theme background - on theme.MinimalShade in the light theme it was a white
        /// box over a dark glyph.
        /// </summary>
        public bool DrawIconOverlayOnDisabled
        {
            get => drawIconOverlayOnDisabled;
            set
            {
                drawIconOverlayOnDisabled = value;
                // Callers set this from an object initializer, so Enabled may already have been
                // changed (and OnEnabledChanged already fired) before we were turned on.
                ApplyEnabledStateToIcon();
            }
        }

        public ThemedTextIconButton(string text, ImageBuffer icon, ThemeConfig theme)
            : base(theme)
        {
            HAnchor = HAnchor.Fit;
            VAnchor = VAnchor.Absolute | VAnchor.Center;
            Height = theme.ButtonHeight;
            Padding = theme.TextButtonPadding;

            BackgroundRadius = theme.ButtonRadius * DeviceScale;

            this.icon = icon;

            AddChild(ImageWidget = new ImageWidget(icon)
            {
                VAnchor = VAnchor.Center,
                Selectable = false
            });

            // TODO: Only needed because TextWidget violates normal padding/margin rules
            var textContainer = new GuiWidget()
            {
                Padding = new BorderDouble(8, 4, 2, 4),
                HAnchor = HAnchor.Fit,
                VAnchor = VAnchor.Center | VAnchor.Fit,
                Selectable = false
            };
            AddChild(textContainer);

            textContainer.AddChild(textWidget = new TextWidget(text, pointSize: theme.DefaultFontSize, textColor: theme.TextColor));
        }

        public override void OnEnabledChanged(EventArgs e)
        {
            ApplyEnabledStateToIcon();

            base.OnEnabledChanged(e);
        }

        /// <summary>
        /// Replaces the icon the button draws with a dimmed copy while it is disabled.
        /// </summary>
        private void ApplyEnabledStateToIcon()
        {
            if (ImageWidget == null)
            {
                // OnEnabledChanged can reach us from the base constructor, before there is an icon
                return;
            }

            if (Enabled || !DrawIconOverlayOnDisabled)
            {
                ImageWidget.Image = icon;
            }
            else
            {
                // Lazy construct on first access, as ThemedIconButton does
                disabledIcon ??= icon.AjustAlpha(0.2);
                ImageWidget.Image = disabledIcon;
            }

            Invalidate();
        }

        private ImageBuffer disabledIcon;

        /// <summary>
        /// Changes the icon the button draws, keeping the disabled presentation in step.
        /// </summary>
        public void SetIcon(ImageBuffer imageBuffer)
        {
            icon = imageBuffer;
            disabledIcon = null;
            ApplyEnabledStateToIcon();
        }

        /// <summary>The widget drawing the icon; its Image is swapped when the button is disabled.</summary>
        public ImageWidget ImageWidget { get; }

        /// <summary>
        /// When true, the button will resize to fit its text whenever Text is changed.
        /// Enable this on buttons whose label changes dynamically at runtime.
        /// </summary>
        public bool AutoExpandBoundsToText
        {
            get => textWidget.AutoExpandBoundsToText;
            set => textWidget.AutoExpandBoundsToText = value;
        }

        public override string Text
        {
            get => textWidget.Text;
            set
            {
                textWidget.Text = value;
                if (textWidget.AutoExpandBoundsToText)
                {
                    textWidget.DoExpandBoundsToText();
                }
            }
        }
    }
}