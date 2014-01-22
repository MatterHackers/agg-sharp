using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using System.Threading;

using MatterHackers.Agg.UI;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.VertexSource;
using MatterHackers.Agg.RasterizerScanline;

namespace MatterHackers.CameraCalibration
{
    public static class CameraCalibrationApp
    {
        [STAThread]
        public static void Main(string[] args)
        {
            GuiHalFactory.SetGuiBackend(GuiHalFactory.KnownGuiFactoriesIndexes.WindowsFormsBitmap);
            //GuiHalFactory.SetGuiBackend(GuiHalFactory.KnownGuiFactoriesIndexes.WindowsFormsOpenGL);

            CameraCalibrationWidgetFactory appWidgetFactory = new CameraCalibrationWidgetFactory();

            AppWidgetInfo appWidgetInfo = appWidgetFactory.GetAppParameters();
            GuiHalWidget primaryWindow = GuiHalFactory.CreatePrimarySurface(appWidgetInfo.width, appWidgetInfo.height, GuiHalWidget.CreateFlags.Resizable, GuiHalWidget.PixelFormat.PixelFormatBgra32);
            //GuiHalWidget primaryWindow = GuiHalFactory.CreatePrimarySurface(appWidgetInfo.width, appWidgetInfo.height, GuiHalWidget.CreateFlags.Resizable, GuiHalWidget.PixelFormat.PixelFormatBgr24);
            //GuiHalWidget primaryWindow = GuiHalFactory.CreatePrimarySurface(appWidgetInfo.width, appWidgetInfo.height, GuiHalWidget.CreateFlags.Resizable, GuiHalWidget.PixelFormat.PixelFormatRgbaFloat);

            primaryWindow.Caption = appWidgetInfo.caption;

            primaryWindow.AddChild(appWidgetFactory.NewWidget());
            primaryWindow.Run();
        }
    }
}
