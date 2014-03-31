using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Reflection;

namespace MatterHackers.Agg.UI
{
    public abstract class SystemWindowCreatorPlugin
    {
        public abstract void ShowSystemWindow(SystemWindow systemWindow);
    }

    public class SystemWindow : GuiWidget
    {
        static SystemWindowCreatorPlugin globalSystemWindowCreator;
        public EventHandler TitleChanged;

        public bool IsModal { get; set; }
        public bool UseOpenGL { get; set; }
        public int StencilBufferDepth { get; set; }
        string title = "";
        public string Title 
        {
            get 
            { 
                return title; 
            } 
            set 
            {
                if (title != value)
                {
                    title = value;
                    if (TitleChanged != null)
                    {
                        TitleChanged(this, null);
                    }
                }
            } 
        }

        public enum ValidDepthVaules { Depth24, Depth32, DepthFloat };
        ValidDepthVaules bitDepth = ValidDepthVaules.Depth32;
        public ValidDepthVaules BitDepth { get { return bitDepth; } set { bitDepth = value; } }

        public override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            if (Parent != null)
            {
                Parent.Close();
            }
        }

        public SystemWindow(double width, double height)
            : base(width, height, SizeLimitsToSet.None)
        {
            if (globalSystemWindowCreator == null)
            {
                string pluginPath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                PluginFinder<SystemWindowCreatorPlugin> systemWindowCreatorFinder = new PluginFinder<SystemWindowCreatorPlugin>(pluginPath);
                if (systemWindowCreatorFinder.Plugins.Count != 1)
                {
					throw new Exception(string.Format("Did not find any SystemWindowCreators in Plugin path ({0}.", pluginPath));
                }
                globalSystemWindowCreator = systemWindowCreatorFinder.Plugins[0];
            }
        }

        public override VectorMath.Vector2 MinimumSize
        {
            get
            {
                return base.MinimumSize;
            }
            set
            {
                base.MinimumSize = value;
                if (Parent != null)
                {
                    Parent.MinimumSize = value;
                }
            }
        }

        public override void BringToFront()
        {
            Parent.BringToFront();
        }

        public void ShowAsSystemWindow()
        {
            if (Parent != null)
            {
                throw new Exception("To be a system window you cannot be a child of another widget.");
            }
            globalSystemWindowCreator.ShowSystemWindow(this);
        }

        public static void AssertDebugNotDefined()
        {
#if DEBUG
            throw new Exception("DEBUG is defined and should not be!");
#endif
        }
    }
}
