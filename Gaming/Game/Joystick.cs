using System;
using System.Collections.Generic;
using System.Text;
using AGG;
using AGG.UI;

namespace Gaming.Game
{
    public class Joystick
    {
        int index;
        public bool button1;
        public float xAxis1;
        public float yAxis1;

        public Joystick(int index)
        {
            this.index = index;
        }

        public void Read()
        {
            if (index >= 0 && index < 16)
            {
                button1 = Joystick_HAL.GetJoysticState(index).b1;
                xAxis1 = Joystick_HAL.GetJoysticState(index).x1;
                yAxis1 = Joystick_HAL.GetJoysticState(index).y1;
            }
        }
    }
}
