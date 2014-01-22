using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;

using MatterHackers.Agg.UI;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.VertexSource;
using MatterHackers.Agg.RasterizerScanline;

namespace MatterHackers.Agg
{
    public class CaptionSorter : IComparer<AppWidgetFactory>
    {
        public int Compare(AppWidgetFactory a, AppWidgetFactory b)
        {
            return a.GetAppParameters().title.CompareTo(b.GetAppParameters().title);
        }
    }

    class DemoRunner : GuiWidget
    {
        static PluginFinder<AppWidgetFactory> appWidgetFinder = new PluginFinder<AppWidgetFactory>(".", new CaptionSorter());
        public DemoRunner()
        {
            TabControl tabControl = new TabControl(Orientation.Vertical);
            AddChild(tabControl);
            tabControl.AnchorAll();

            int count = appWidgetFinder.Plugins.Count;
            for (int i = 0; i < count; i++)
            {
                if (appWidgetFinder.Plugins[i].GetAppParameters().title != "Demo Runner")
                {
                    TabPage tabPage = new TabPage(appWidgetFinder.Plugins[i].GetAppParameters().title);
                    tabPage.AddChild(appWidgetFinder.Plugins[i].NewWidget());
                    tabControl.AddTab(tabPage);
                }
            }

            AnchorAll();
        }

        static bool usedMainThread = false;
        public static void StartADemo(AppWidgetFactory appWidgetFactory)
        {
            if (usedMainThread)
            {
                System.AppDomainSetup domainSetup = new AppDomainSetup();
                domainSetup.ApplicationBase = ".";
                System.AppDomain newDomain = null;

                bool usePermissionTest = false;
                if (usePermissionTest)
                {
                    System.Security.PermissionSet permissionSet = new System.Security.PermissionSet(System.Security.Permissions.PermissionState.None);
                    permissionSet.AddPermission(new System.Security.Permissions.SecurityPermission(System.Security.Permissions.SecurityPermissionFlag.Execution));
                    permissionSet.AddPermission(new System.Security.Permissions.FileIOPermission(System.Security.Permissions.PermissionState.Unrestricted));

                    newDomain = System.AppDomain.CreateDomain(appWidgetFactory.GetAppParameters().title, null, domainSetup, permissionSet, null);
                }
                else
                {
                    newDomain = System.AppDomain.CreateDomain(appWidgetFactory.GetAppParameters().title);
                }

                string[] args = { appWidgetFactory.GetAppParameters().title };
                newDomain.ExecuteAssembly("./DemoRunner.exe", args);
                //System.AppDomain.Unload(newDomain);
            }
            else
            {
                usedMainThread = true;
                appWidgetFactory.CreateWidgetAndRunInWindow();
                //appWidgetFactory.CreateWidgetAndRunInWindow(AppWidgetFactory.ValidDepthVaules.Depth32, AppWidgetFactory.RenderSurface.OpenGL);
            }
        }

        [STAThread]
        public static void Main(string[] args)
        {
            GuiHalWidget.SetClipboardFunctions(System.Windows.Forms.Clipboard.GetText, System.Windows.Forms.Clipboard.SetText, System.Windows.Forms.Clipboard.ContainsText); 
            
            MatterHackers.Agg.Tests.AggDrawingTests.RunAllTests();
            MatterHackers.VectorMath.Tests.UnitTests.Run();
            MatterHackers.Agg.UI.Tests.UnitTests.Run();
            MatterHackers.Agg.Image.UnitTests.Run();
            MatterHackers.PolygonMesh.UnitTests.UnitTests.Run();
            if (args.Length > 0)
            {
                bool foundADemo = false;
                for (int i = 0; i < appWidgetFinder.Plugins.Count; i++)
                {
                    if (args[0] == appWidgetFinder.Plugins[i].GetAppParameters().title)
                    {
                        foundADemo = true;
                        StartADemo(appWidgetFinder.Plugins[i]);
                    }
                }

                if (foundADemo)
                {
                    return;
                }
            }

            StartADemo(new DemoRunnerFactory());

            //StartADemo(new MatterCadWidgetFactory());
            //StartADemo(new GCodeVisualizerFactory());
            //StartADemo(new ComponentRenderingFactory());
            //StartADemo(new LionFactory());
            //StartADemo(new GradientsFactory());
            //StartADemo(new ImageFiltersFactory());
            //StartADemo(new LionOutlineFactory());
            //StartADemo(new image_resample());
            //StartADemo(new FloodFillDemo());
            //StartADemo(new aa_demoFactory());
            //StartADemo(new BlurFactory());
            //StartADemo(new GuiTesterFactory());
        }
    }

    public class DemoRunnerFactory : AppWidgetFactory
    {
		public override GuiWidget NewWidget()
        {
            return new DemoRunner();
        }

		public override AppWidgetInfo GetAppParameters()
        {
            AppWidgetInfo appWidgetInfo = new AppWidgetInfo(
            "Utility",
            "Demo Runner",
            "Use this to run all the demos.",
            640,
            480);

            return appWidgetInfo;
        }
    }
}
