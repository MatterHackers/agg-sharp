using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.Font;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg
{
    public class SliderControlsPage : TabPage
    {
        Slider horizontalSlider;
        Slider verticalSlider;
        CheckBox changeSliderText;

        public SliderControlsPage()
            : base("Slider Widget")
        {
            horizontalSlider = new Slider(new Vector2(20, 60), 100);
            AddChild(horizontalSlider);
            horizontalSlider.Text = "{0:0.0}";

            changeSliderText = new CheckBox(10, 200, "Show Text");
            changeSliderText.Checked = true;
            AddChild(changeSliderText);
            changeSliderText.CheckedStateChanged += new CheckBox.CheckedStateChangedEventHandler(changeSliderText_CheckedStateChanged);

            verticalSlider = new Slider(new Vector2(320, 60), 100, orientation: Orientation.Vertical);
            AddChild(verticalSlider);
            verticalSlider.Text = "{0:0.0}";
        }

        void changeSliderText_CheckedStateChanged(object sender, EventArgs e)
        {
            if (changeSliderText.Checked)
            {
                horizontalSlider.Text = "{0:0.0}";
                verticalSlider.Text = "{0:0.0}";
            }
            else
            {
                horizontalSlider.Text = "";
                verticalSlider.Text = "";
            }
        }
    }
}
