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

        [Flags]
        public enum CreateFlags
        {
            None = 0,
            Resizable = 1,
            FullScreen = 2,
        }

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
    
        protected ImageFormats m_format;
        protected int m_bpp;
        protected CreateFlags m_window_flags;
        protected int initialWidth;
        protected int initialHeight;

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

        //-----------------------------------------------------------pix_format_e
        // Possible formats of the rendering buffer. Initially I thought that it's
        // reasonable to create the buffer and the rendering functions in 
        // accordance with the native pixel format of the system because it 
        // would have no overhead for pixel format conversion. 
        // But eventually I came to a conclusion that having a possibility to 
        // convert pixel formats on demand is a good idea. First, it was X11 where 
        // there lots of different formats and visuals and it would be great to 
        // render everything in, say, RGB-24 and display it automatically without
        // any additional efforts. The second reason is to have a possibility to 
        // debug renderers for different pixel formats and colorspaces having only 
        // one computer and one system.
        //
        // This stuff is not included into the basic AGG functionality because the 
        // number of supported pixel formats (and/or colorspaces) can be great and 
        // if one needs to add new format it would be good only to add new 
        // rendering files without having to modify any existing ones (a general 
        // principle of incapsulation and isolation).
        //
        // Using a particular pixel format doesn't obligatory mean the necessity
        // of software conversion. For example, win32 API can natively display 
        // gray8, 15-bit RGB, 24-bit BGR, and 32-bit BGRA formats. 
        // This list can be (and will be!) extended in future.
        public enum ImageFormats
        {
            pix_format_undefined = 0,  // By default. No conversions are applied 
            pix_format_bw,             // 1 bit per color B/W
            pix_format_gray8,          // Simple 256 level grayscale
            pix_format_gray16,         // Simple 65535 level grayscale
            pix_format_rgb555,         // 15 bit rgb. Depends on the byte ordering!
            pix_format_rgb565,         // 16 bit rgb. Depends on the byte ordering!
            pix_format_rgbAAA,         // 30 bit rgb. Depends on the byte ordering!
            pix_format_rgbBBA,         // 32 bit rgb. Depends on the byte ordering!
            pix_format_bgrAAA,         // 30 bit bgr. Depends on the byte ordering!
            pix_format_bgrABB,         // 32 bit bgr. Depends on the byte ordering!
            pix_format_rgb24,          // R-G-B, one byte per color component
            pix_format_bgr24,          // B-G-R, native win32 BMP format.
            pix_format_rgba32,         // R-G-B-A, one byte per color component
            pix_format_argb32,         // A-R-G-B, native MAC format
            pix_format_abgr32,         // A-B-G-R, one byte per color component
            pix_format_bgra32,         // B-G-R-A, native win32 BMP format
            pix_format_rgb48,          // R-G-B, 16 bits per color component
            pix_format_bgr48,          // B-G-R, native win32 BMP format.
            pix_format_rgba64,         // R-G-B-A, 16 bits byte per color component
            pix_format_argb64,         // A-R-G-B, native MAC format
            pix_format_abgr64,         // A-B-G-R, one byte per color component
            pix_format_bgra64,         // B-G-R-A, native win32 BMP format
            pix_format_rgba_float,       // R-G-B-A, all values stored as floats

            end_of_pix_formats
        };

        public static int GetBitDepthForPixelFormat(ImageFormats pixelFormat)
        {
            switch(pixelFormat)
            {
                case ImageFormats.pix_format_bgr24:
                case ImageFormats.pix_format_rgb24:
                    return 24;

                case ImageFormats.pix_format_bgra32:
                case ImageFormats.pix_format_rgba32:
                    return 32;

                case ImageFormats.pix_format_rgba_float:
                    return 32 * 4;

                default:
                    throw new System.NotImplementedException();
            }
        }

        // format - see enum pix_format_e {};
        // flip_y - true if you want to have the Y-axis flipped vertically.
        public GuiHalWidget(ImageFormats format)
        {
            m_format = format;
            m_bpp = GetBitDepthForPixelFormat(format);
        }

        public delegate String ClipboardGetTextDelegate();
        static event ClipboardGetTextDelegate getClipboardText;
        public static String ClipboardGetText()
        {
            if (getClipboardText == null)
            {
                throw new Exception("You need to 'GuiHalSurface.getClipboardText += System.Windows.Forms.Clipboard.GetText;' from your main thread.");
            }
            
            return getClipboardText();
        }

        public delegate void ClipboardSetTextDelegate(String text);
        static event ClipboardSetTextDelegate setClipboardText;
        public static void ClipboardSetText(String text)
        {
            if (setClipboardText == null)
            {
                throw new Exception("You need to 'GuiHalSurface.setClipboardText += System.Windows.Forms.Clipboard.SetText;' from your main thread.");
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

        // The very same parameters that were used in the constructor
        public ImageFormats format() { return m_format; }

        public int bpp() { return m_bpp; }

        abstract public void OnControlChanged();

        public double width() { return BoundsRelativeToParent.Width; }
        public double height() { return BoundsRelativeToParent.Height; }
        public double initial_width() { return initialWidth; }
        public double initial_height() { return initialHeight; }
        public CreateFlags window_flags() { return m_window_flags; }

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
        GuiHalWidget CreateSurface(int Width, int Height, GuiHalWidget.CreateFlags flags, GuiHalWidget.PixelFormat pixelFormat, int stencilDepth);
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

        public static GuiHalWidget CreatePrimarySurface(int width, int height, GuiHalWidget.CreateFlags flags, GuiHalWidget.PixelFormat pixelFormat, int stencilDepth)
        {
            if (halGuiFactory == null)
            {
                throw new NotSupportedException("You must call 'SetGuiBackend' with a GuiFactory before you can create any surfaces");
            }

            GuiHalWidget createdSurface = halGuiFactory.CreateSurface(width, height, flags, pixelFormat, stencilDepth);
            if (primaryHalWidget == null)
            {
                primaryHalWidget = createdSurface;
            }

            return createdSurface;
        }
    }
}
