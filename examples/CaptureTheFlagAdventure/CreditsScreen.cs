using System;
using System.Collections.Generic;
using AGG;
using AGG.Image;
using AGG.VertexSource;
using AGG.UI;

using AGG.Transform;

using Gaming.Math;
using Gaming.Game;
using Gaming.Graphics;

namespace CTFA
{
    /// <summary>
    /// Description of CreditsMenu.
    /// </summary>
    public class CreditsMenu : GUIWidget
    {
        public delegate void CancelMenuEventHandler(GUIWidget button);
        public event CancelMenuEventHandler CancelMenu;

        public CreditsMenu(rect_d bounds)
        {
            Bounds = bounds;

            ImageSequence cancelButtonSequence = (ImageSequence)DataAssetCache.Instance.GetAsset(typeof(ImageSequence), "NumPlayersCancelButton");
            ButtonWidget cancelGameButton = new ButtonWidget(400, 200, new ThreeImageButtonView(cancelButtonSequence.GetImageByIndex(0), cancelButtonSequence.GetImageByIndex(1), cancelButtonSequence.GetImageByIndex(2)));
            AddChild(cancelGameButton);
            cancelGameButton.ButtonClick += new ButtonWidget.ButtonEventHandler(OnCancelMenuButton);
        }

        public override void OnDraw(RendererBase rendererToDrawWith)
        {
            ImageSequence menuBackground = (ImageSequence)DataAssetCache.Instance.GetAsset(typeof(ImageSequence), "CreditsScreen");
            rendererToDrawWith.Render(menuBackground.GetImageByIndex(0), 0, 0);

            base.OnDraw(rendererToDrawWith);
        }

        private void OnCancelMenuButton(object sender, MouseEventArgs mouseEvent)
        {
            if (CancelMenu != null)
            {
                CancelMenu(this);
            }
        }
    }
}
