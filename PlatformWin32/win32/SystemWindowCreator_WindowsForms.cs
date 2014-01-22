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

            GuiHalWidget windowsFromsTopWindow;
            switch (systemWindow.BitDepth)
            {
                case SystemWindow.ValidDepthVaules.Depth24:
                    windowsFromsTopWindow = GuiHalFactory.CreatePrimarySurface((int)systemWindow.Width, (int)systemWindow.Height,
                        GuiHalWidget.CreateFlags.Resizable, GuiHalWidget.PixelFormat.PixelFormatBgr24, systemWindow.StencilBufferDepth);
                    break;

                case SystemWindow.ValidDepthVaules.Depth32:
                    windowsFromsTopWindow = GuiHalFactory.CreatePrimarySurface((int)systemWindow.Width, (int)systemWindow.Height,
                        GuiHalWidget.CreateFlags.Resizable, GuiHalWidget.PixelFormat.PixelFormatBgra32, systemWindow.StencilBufferDepth);
                    break;

                case SystemWindow.ValidDepthVaules.DepthFloat:
                    windowsFromsTopWindow = GuiHalFactory.CreatePrimarySurface((int)systemWindow.Width, (int)systemWindow.Height,
                        GuiHalWidget.CreateFlags.Resizable, GuiHalWidget.PixelFormat.PixelFormatRgbaFloat, systemWindow.StencilBufferDepth);
                    break;

                default:
                    throw new NotImplementedException();
            }

            windowsFromsTopWindow.Caption = systemWindow.Title;
            windowsFromsTopWindow.AddChild(systemWindow);
            systemWindow.AnchorAll();
            systemWindow.TitleChanged += new EventHandler(TitelChangedEventHandler);
            // and make sure the title is correct right now
            TitelChangedEventHandler(systemWindow, null);

            if (haveInitializedMainWindow)
            {
                if (systemWindow.IsModal)
                {
                    windowsFromsTopWindow.ShowModal();
                }
                else
                {
                    windowsFromsTopWindow.Show();
                }
            }
            else
            {
                windowsFromsTopWindow.Run();
            }
        }

        void TitelChangedEventHandler(object sender, EventArgs e)
        {
            SystemWindow systemWindow = ((SystemWindow)sender);
            GuiHalWidget windowsFromsTopWindow = (GuiHalWidget)systemWindow.Parent;
            windowsFromsTopWindow.Caption = systemWindow.Title;
        }
    }
}
