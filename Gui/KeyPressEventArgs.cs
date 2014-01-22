using System;
using System.Collections.Generic;
using System.Text;

namespace MatterHackers.Agg.UI
{
    public delegate void KeyPressEventHandler(object sender, KeyPressEventArgs keyEvent);

    public class KeyPressEventArgs
    {
        char m_KeyChar;
        bool m_Handled;

        public KeyPressEventArgs(char keyChar)
        {
            m_Handled = false;
            m_KeyChar = keyChar;
        }

        public bool Handled { get { return m_Handled; } set { m_Handled = value; } }
        public char KeyChar { get { return m_KeyChar; } set { m_KeyChar = value; } }
    }
}
