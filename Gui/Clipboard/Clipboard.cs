using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MatterHackers.Agg.UI
{
    public class Clipboard
    {
        public delegate String ClipboardGetTextDelegate();
        static event ClipboardGetTextDelegate getClipboardText;
        public static String GetText()
        {
            if (getClipboardText == null)
            {
                throw new Exception("You need to 'Clipboard.SetSystemClipboardFunctions(System.Windows.Forms.Clipboard.GetText, System.Windows.Forms.Clipboard.SetText, System.Windows.Forms.Clipboard.ContainsText);' from your main thread.");
            }

            return getClipboardText();
        }

        public delegate void ClipboardSetTextDelegate(String text);
        static event ClipboardSetTextDelegate setClipboardText;
        public static void SetText(String text)
        {
            if (setClipboardText == null)
            {
                throw new Exception("You need to 'Clipboard.SetSystemClipboardFunctions(System.Windows.Forms.Clipboard.GetText, System.Windows.Forms.Clipboard.SetText, System.Windows.Forms.Clipboard.ContainsText);' from your main thread.");
                // if this fails add 
                // GuiHalWidget.SetClipboardFunctions(System.Windows.Forms.Clipboard.GetText, System.Windows.Forms.Clipboard.SetText, System.Windows.Forms.Clipboard.ContainsText);
                // before you call the unit tests
            }

            setClipboardText(text);
        }

        public delegate bool ClipboardContanisTextDelegate();
        static event ClipboardContanisTextDelegate containsClipboardText;
        public static bool ContainsText()
        {
            if (containsClipboardText == null)
            {
                throw new Exception("You need to 'GuiHalSurface.containsClipboardText += System.Windows.Forms.Clipboard.ContainsText;' from your main thread.");
            }

            return containsClipboardText();
        }

        static public void SetSystemClipboardFunctions(ClipboardGetTextDelegate getText, ClipboardSetTextDelegate setText, ClipboardContanisTextDelegate containsText)
        {
            if (getClipboardText == null)
            {
                if (getText == null || setText == null || containsText == null)
                {
                    throw new Exception("You must set all the clipboard functinos at once.  If we are going to use any we need them all.");
                }
                getClipboardText = getText;
                setClipboardText = setText;
                containsClipboardText = containsText;
            }
        }
    }
}
