using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using MatterHackers.Agg.UI;

namespace MatterHackers.Agg
{
    public class SystemWindowCreator_WindowsForms : SystemWindowCreatorPlugin
    {
        public override void ShowSystemWindow(SystemWindow systemWindow)
        {
            bool haveInitializedMainWindow = false;
            if (GuiHalFactory.PrimaryHalWidget != null)
            {
                haveInitializedMainWindow = true;
            }

            if (!haveInitializedMainWindow)
            {
                if (systemWindow.UseOpenGL)
                {
                    GuiHalFactory.SetGuiBackend(new WindowsFormsOpenGLFactory());
                }
                else
                {
                    GuiHalFactory.SetGuiBackend(new WindowsFormsBitmapFactory());
                }
            }

            GuiHalWidget windowsFormsTopWindow;
            switch (systemWindow.BitDepth)
            {
                case SystemWindow.ValidDepthVaules.Depth24:
                    windowsFormsTopWindow = GuiHalFactory.CreatePrimarySurface((int)systemWindow.Width, (int)systemWindow.Height,
                        GuiHalWidget.CreateFlags.Resizable, GuiHalWidget.PixelFormat.PixelFormatBgr24, systemWindow.StencilBufferDepth);
                    break;

                case SystemWindow.ValidDepthVaules.Depth32:
                    windowsFormsTopWindow = GuiHalFactory.CreatePrimarySurface((int)systemWindow.Width, (int)systemWindow.Height,
                        GuiHalWidget.CreateFlags.Resizable, GuiHalWidget.PixelFormat.PixelFormatBgra32, systemWindow.StencilBufferDepth);
                    break;

                case SystemWindow.ValidDepthVaules.DepthFloat:
                    windowsFormsTopWindow = GuiHalFactory.CreatePrimarySurface((int)systemWindow.Width, (int)systemWindow.Height,
                        GuiHalWidget.CreateFlags.Resizable, GuiHalWidget.PixelFormat.PixelFormatRgbaFloat, systemWindow.StencilBufferDepth);
                    break;

                default:
                    throw new NotImplementedException();
            }

            windowsFormsTopWindow.Caption = systemWindow.Title;
            windowsFormsTopWindow.AddChild(systemWindow);
            windowsFormsTopWindow.MinimumSize = systemWindow.MinimumSize;
            systemWindow.AnchorAll();
            systemWindow.TitleChanged += new EventHandler(TitelChangedEventHandler);
            // and make sure the title is correct right now
            TitelChangedEventHandler(systemWindow, null);

            if (haveInitializedMainWindow)
            {
                if (systemWindow.IsModal)
                {
                    windowsFormsTopWindow.ShowModal();
                }
                else
                {
                    windowsFormsTopWindow.Show();
                }
            }
            else
            {
                windowsFormsTopWindow.Run();
            }
        }

        public override Point2D GetDesktopPosition(SystemWindow systemWindow)
        {
            GuiHalWidget windowsFromsTopWindow = (GuiHalWidget)systemWindow.Parent;
            return windowsFromsTopWindow.DesktopPosition;
        }

        public override void SetDesktopPosition(SystemWindow systemWindow, Point2D position)
        {
            GuiHalWidget windowsFromsTopWindow = (GuiHalWidget)systemWindow.Parent;
            windowsFromsTopWindow.DesktopPosition = position;
        }

        void TitelChangedEventHandler(object sender, EventArgs e)
        {
            SystemWindow systemWindow = ((SystemWindow)sender);
            GuiHalWidget windowsFromsTopWindow = (GuiHalWidget)systemWindow.Parent;
            windowsFromsTopWindow.Caption = systemWindow.Title;
        }
    }
}
