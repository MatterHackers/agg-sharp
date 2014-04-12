using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using MatterHackers.Agg.UI;

namespace MatterHackers.Agg
{
    public class SystemWindowCreator_WindowsForms : SystemWindowCreatorPlugin
    {
        bool SetInitialDesktopPosition = false;
        Point2D InitialDesktopPosition = new Point2D();

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

            GuiHalWidget windowsFormsTopWindow = GuiHalFactory.CreatePrimarySurface(systemWindow);

            windowsFormsTopWindow.Caption = systemWindow.Title;
            windowsFormsTopWindow.AddChild(systemWindow);
            windowsFormsTopWindow.MinimumSize = systemWindow.MinimumSize;

            if (SetInitialDesktopPosition)
            {
                systemWindow.DesktopPosition = InitialDesktopPosition;
            }

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
            if (systemWindow.Parent != null)
            {
                GuiHalWidget windowsFromsTopWindow = (GuiHalWidget)systemWindow.Parent;
                return windowsFromsTopWindow.DesktopPosition;
            }

            return new Point2D();
        }

        public override void SetDesktopPosition(SystemWindow systemWindow, Point2D position)
        {
            if (systemWindow.Parent != null)
            {
                GuiHalWidget windowsFromsTopWindow = (GuiHalWidget)systemWindow.Parent;
                windowsFromsTopWindow.DesktopPosition = position;
            }
            else
            {
                SetInitialDesktopPosition = true;
                InitialDesktopPosition = position;
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
