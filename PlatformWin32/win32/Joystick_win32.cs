/*
Copyright (c) 2014, Lars Brubaker
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
using System.Text;
using System.Runtime.InteropServices;

namespace MatterHackers.Agg.UI
{
    public class Joystick_HAL
    {
        [DllImport("WinMM.dll")]
        internal static extern int joyGetPosEx(int uJoyID, ref JOYINFOEX pji);


        /// <summary>
        /// Value type containing joystick position information.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        internal struct JOYINFOEX
        {
            /// <summary>Size of structure, in bytes.</summary>
            public int Size;
            /// <summary>Flags to indicate what information is valid for the device.</summary>
            public int Flags;
            //public JoystickInfoFlags Flags;
            /// <summary>X axis.</summary>
            public int X;
            /// <summary>Y axis.</summary>
            public int Y;
            /// <summary>Z axis.</summary>
            public int Z;
            /// <summary>Rudder position.</summary>
            public int Rudder;
            /// <summary>5th axis position.</summary>
            public int Axis5;
            /// <summary>6th axis position.</summary>
            public int Axis6;
            /// <summary>State of buttons.</summary>
            public int Buttons;
            //public JoystickButtons Buttons;
            /// <summary>Currently pressed button.</summary>
            public int ButtonNumber;
            /// <summary>Angle of the POV hat, in degrees (0 - 35900, divide by 100 to get 0 - 359 degrees.</summary>
            public int POV;
            /// <summary>Reserved.</summary>
            int Reserved1;
            /// <summary>Reserved.</summary>
            int Reserved2;
        }

        public class JoystickState
        {
            public float x1;
            public float y1;
            public bool b1;
        }

        static public JoystickState GetJoysticState(int JoystickIndex)
        {
            JOYINFOEX joystickInfo = new JOYINFOEX();
            joystickInfo.Size = Marshal.SizeOf(joystickInfo);
            int result = joyGetPosEx(JoystickIndex, ref joystickInfo);

            JoystickState joy = new JoystickState();
            if (result == 0)
            {
                joy.x1 = ((float)joystickInfo.X / 32768.0f - 1.0f);
                joy.y1 = -((float)joystickInfo.Y / 32768.0f - 1.0f);
                joy.b1 = joystickInfo.Z < 18000;
            }
            else
            {
                joy.x1 = 0;
                joy.y1 = 0;
                joy.b1 = false;
            }
            return joy;
        }
    }
}
