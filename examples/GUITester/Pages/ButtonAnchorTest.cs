using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.Font;

namespace MatterHackers.Agg
{
    public class ButtonAnchorTestPage : TabPage
    {
        const int offset = 20;
        public ButtonAnchorTestPage()
            : base("Button Anchor Tests")
        {
            CreateButton(UI.HAnchor.ParentLeft, UI.VAnchor.ParentBottom);
            CreateButton(UI.HAnchor.ParentCenter, UI.VAnchor.ParentBottom);
            CreateButton(UI.HAnchor.ParentRight, UI.VAnchor.ParentBottom);

            CreateButton(UI.HAnchor.ParentLeft, UI.VAnchor.ParentCenter);
            CreateButton(UI.HAnchor.ParentCenter, UI.VAnchor.ParentCenter);
            CreateButton(UI.HAnchor.ParentRight, UI.VAnchor.ParentCenter);

            CreateButton(UI.HAnchor.ParentLeft, UI.VAnchor.ParentTop);
            CreateButton(UI.HAnchor.ParentCenter, UI.VAnchor.ParentTop);
            CreateButton(UI.HAnchor.ParentRight, UI.VAnchor.ParentTop);
        }

        private void CreateButton(HAnchor hAnchor, VAnchor vAnchor)
        {
            Button anchorButton = new Button(hAnchor.ToString() + " " + vAnchor.ToString());
            anchorButton.HAnchor = hAnchor;
            anchorButton.VAnchor = vAnchor;
            anchorButton.Margin = new BorderDouble(offset);
            AddChild(anchorButton);
        }

        public override void OnDraw(Graphics2D graphics2D)
        {
            graphics2D.Line(Width / 2, 0, Width / 2, Height, RGBA_Bytes.Red);
            graphics2D.Line(0, Height / 2, Width, Height / 2, RGBA_Bytes.Red);
            base.OnDraw(graphics2D);
        }
    }
}
