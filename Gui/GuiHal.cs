using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.IO;

using MatterHackers.Agg.Transform;
using MatterHackers.Agg.Image;

namespace MatterHackers.Agg.UI
{
    public abstract class GuiHalWidget : GuiWidget
    {
        public int milliSecondsToSleepEachIdle = 16;

        public enum PixelFormat
        {
            PixelFormatBgr24,
            PixelFormatBgra32,
            PixelFormatRgbaFloat
        }

        public abstract string Caption
        {
            get;
            set;
        }

        public abstract void ShowModal();
        public abstract void Show();
        public abstract void Run();

        public abstract Point2D DesktopPosition { get; set; }
    
        [Flags]
        public enum WindowFlags
        {
            None = 0,
            Resizable = 1,
            KeepAspectRatio = 2,
            UseOpenGL = 4,
        }

        public virtual void OnInitialize()
        {
        }

        protected SystemWindow windowWeAreHosting;
        // format - see enum pix_format_e {};
        // flip_y - true if you want to have the Y-axis flipped vertically.
        public GuiHalWidget(SystemWindow windowWeAreHosting)
            : base(windowWeAreHosting.Width, windowWeAreHosting.Height, SizeLimitsToSet.None)
        {
            this.windowWeAreHosting = windowWeAreHosting;
        }

        public delegate String ClipboardGetTextDelegate();
        static event ClipboardGetTextDelegate getClipboardText;
        public static String ClipboardGetText()
        {
            if (getClipboardText == null)
            {
                throw new Exception("You need to 'GuiHalWidget.SetClipboardFunctions(System.Windows.Forms.Clipboard.GetText, System.Windows.Forms.Clipboard.SetText, System.Windows.Forms.Clipboard.ContainsText);' from your main thread.");
            }
            
            return getClipboardText();
        }

        public delegate void ClipboardSetTextDelegate(String text);
        static event ClipboardSetTextDelegate setClipboardText;
        public static void ClipboardSetText(String text)
        {
            if (setClipboardText == null)
            {
                throw new Exception("You need to 'GuiHalWidget.SetClipboardFunctions(System.Windows.Forms.Clipboard.GetText, System.Windows.Forms.Clipboard.SetText, System.Windows.Forms.Clipboard.ContainsText);' from your main thread.");
                // if this fails add 
                // GuiHalWidget.SetClipboardFunctions(System.Windows.Forms.Clipboard.GetText, System.Windows.Forms.Clipboard.SetText, System.Windows.Forms.Clipboard.ContainsText);
                // before you call the unit tests
            }

            setClipboardText(text);
        }

        public delegate bool ClipboardContanisTextDelegate();
        static event ClipboardContanisTextDelegate containsClipboardText;
        public static bool ClipboardContainsText()
        {
            if (containsClipboardText == null)
            {
                throw new Exception("You need to 'GuiHalSurface.containsClipboardText += System.Windows.Forms.Clipboard.ContainsText;' from your main thread.");
            }

            return containsClipboardText();
        }

        static public void SetClipboardFunctions(ClipboardGetTextDelegate getText, ClipboardSetTextDelegate setText, ClipboardContanisTextDelegate containsText)
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

        abstract public void OnControlChanged();

        public double width() { return BoundsRelativeToParent.Width; }
        public double height() { return BoundsRelativeToParent.Height; }

        // Get raw display handler depending on the system. 
        // For win32 its an HDC, for other systems it can be a pointer to some
        // structure. See the implementation files for detals.
        // It's provided "as is", so, first you should check if it's not null.
        // If it's null the raw_display_handler is not supported. Also, there's 
        // no guarantee that this function is implemented, so, in some 
        // implementations you may have simply an unresolved symbol when linking.
        //public void* raw_display_handler();
    }

    // interface for a GUI hardware abstraction layer
    public interface IGuiFactory
    {
        GuiHalWidget CreateSurface(SystemWindow windowWeAreHosting);
    }

    // the static class used to 
    public static class GuiHalFactory
    {
        static IGuiFactory halGuiFactory;

        public static void SetGuiBackend(IGuiFactory factoryToUse)
        {
            if (GuiHalFactory.halGuiFactory != null)
            {
                throw new NotSupportedException("You can only set the graphics target one time in an application.");
            }

            GuiHalFactory.halGuiFactory = factoryToUse;
        }

        static GuiHalWidget primaryHalWidget;
        public static GuiHalWidget PrimaryHalWidget
        {
            get
            {
                return primaryHalWidget;
            }
        }

        public static GuiHalWidget CreatePrimarySurface(SystemWindow windowWeAreHosting)
        {
            if (halGuiFactory == null)
            {
                throw new NotSupportedException("You must call 'SetGuiBackend' with a GuiFactory before you can create any surfaces");
            }

            GuiHalWidget createdSurface = halGuiFactory.CreateSurface(windowWeAreHosting);
            if (primaryHalWidget == null)
            {
                primaryHalWidget = createdSurface;
            }

            return createdSurface;
        }
    }
}
