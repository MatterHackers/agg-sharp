using System;
using System.Collections.Generic;
using System.IO;

using MatterHackers.Agg;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.VertexSource;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.Font;

using MatterHackers.VectorMath;

/*
 * TODO:
 *  show the z height for the visible layer 
 *  show the temperature for the layer (if more than one figure something out)
 *  show the x y z for the vertex under the pointer
 *  put in a verticle zoom control (google maps is the model)
 *  put the open gcode in a file menu (good reason to write a menu widget :) )
 *  make soom zoom maintain what is in the current center of the screen
 * 
 * DONE:
 *  mouse wheel to zoom
 *  use the mouse to move the model
 *  make a true model and view for the gcode
 *  remember last directory next time opening file dialog
*/

namespace MatterHackers.GCodeVisualizer
{
    public class GCodeViewerApplication : SystemWindow
    {
        Button openFileButton;
        TextWidget layerCountTextWidget;
        NumberEdit currentLayerIndex;
        Button prevLayerButton;
        Button nextLayerButton;

        GCodeViewWidget gCodeViewWidget;

        public GCodeViewerApplication(string gCodeToLoad = "")
            : base(800, 600)
        {
            MinimumSize = new VectorMath.Vector2(200, 200);
            Title = "MatterHackers GCodeVisualizer";
            gCodeViewWidget = new GCodeViewWidget(new Vector2(), new Vector2(100, 100));
            AddChild(gCodeViewWidget);

            FlowLayoutWidget keepOnTop = new FlowLayoutWidget();

            prevLayerButton = new Button("<<", 0, 0);
            prevLayerButton.Click += new Button.ButtonEventHandler(prevLayer_ButtonClick);
            keepOnTop.AddChild(prevLayerButton);

            currentLayerIndex = new NumberEdit(1, pixelWidth: 40);
            keepOnTop.AddChild(currentLayerIndex);
            currentLayerIndex.EditComplete += new EventHandler(layerCountTextWidget_EditComplete);

            layerCountTextWidget = new TextWidget("/1____", 12);
            keepOnTop.AddChild(layerCountTextWidget);

            nextLayerButton = new Button(">>", 0, 0);
            nextLayerButton.Click += new Button.ButtonEventHandler(nextLayer_ButtonClick);
            keepOnTop.AddChild(nextLayerButton);

            if (gCodeToLoad != "")
            {
                gCodeViewWidget.Load(gCodeToLoad);
            }
            else
            {
                openFileButton = new Button("Open GCode", 0, 0);
                openFileButton.Click += new Button.ButtonEventHandler(openFileButton_ButtonClick);
                keepOnTop.AddChild(openFileButton);
            }

            AddChild(keepOnTop);

            AnchorAll();
            currentLayerIndex.Focus();
        }

        void SetActiveLayer(int layer)
        {
            gCodeViewWidget.ActiveLayerIndex = layer;
            currentLayerIndex.Value = gCodeViewWidget.ActiveLayerIndex + 1;

            Invalidate();
        }

        void layerCountTextWidget_EditComplete(object sender, EventArgs e)
        {
            SetActiveLayer((int)currentLayerIndex.Value - 1);
        }

        void nextLayer_ButtonClick(object sender, MouseEventArgs mouseEvent)
        {
            SetActiveLayer(gCodeViewWidget.ActiveLayerIndex + 1);
        }

        void prevLayer_ButtonClick(object sender, MouseEventArgs mouseEvent)
        {
            SetActiveLayer(gCodeViewWidget.ActiveLayerIndex - 1);
        }

        void openFileButton_ButtonClick(object sender, MouseEventArgs mouseEvent)
        {
            OpenFileDialogParams openParams = new OpenFileDialogParams("gcode files|*.gcode");
            Stream streamToLoadFrom = FileDialog.OpenFileDialog(ref openParams);

            if (openParams.FileName != null && openParams.FileName != "")
            {
                gCodeViewWidget.Load(openParams.FileName);
                currentLayerIndex.Value = 0;
                currentLayerIndex.MaxValue = gCodeViewWidget.gCodeView.NumLayers;
            }

            Invalidate();
        }

        public override void OnDraw(Graphics2D graphics2D)
        {
            this.NewGraphics2D().Clear(new RGBA_Bytes(255, 255, 255));

            if (gCodeViewWidget.LoadedGCode != null)
            {
                layerCountTextWidget.Text = "/" + gCodeViewWidget.gCodeView.NumLayers.ToString();
            }

            base.OnDraw(graphics2D);
        }

        [STAThread]
        public static void Main(string[] args)
        {
            GCodeViewerApplication app = new GCodeViewerApplication();
            app.ShowAsSystemWindow();
        }
    }

    public class GCodeVisualizerFactory : AppWidgetFactory
    {
		public override GuiWidget NewWidget()
        {
            return new GCodeViewerApplication();
        }

		public override AppWidgetInfo GetAppParameters()
        {
            AppWidgetInfo appWidgetInfo = new AppWidgetInfo(
            "Other",
            "G Code Visualizer",
            "A sample application to visualize the g-code created for a rep-rap type FDM machine.",
            600,
            400);

            return appWidgetInfo;
        }
    }
}
