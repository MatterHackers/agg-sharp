using System;
using System.Collections.Generic;
using System.IO;

using MatterHackers.Agg;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.VertexSource;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.Font;


namespace MatterHackers.CSGOpenGL
{
    public class CSGOpenGLApplication : SystemWindow
    {
        protected MeshViewerWidget meshViewerWidget;
        protected StencilCSG csgTest;
        
        Button openFileButton;
        CheckBox bedCheckBox;
        CheckBox wireframeCheckBox;
        GuiWidget viewArea;

        public MeshViewerWidget MeshViewWidget
        {
            get { return meshViewerWidget; }
        }

        public CSGOpenGLApplication(string meshFileToLoad = "")
            : base(800, 600)
        {
            MinimumSize = new VectorMath.Vector2(200, 200);
            Title = "MatterHackers MeshViewr";
            UseOpenGL = true;
            StencilBufferDepth = 8;
            BitDepth = ValidDepthVaules.Depth24;

            FlowLayoutWidget mainContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
            mainContainer.AnchorAll();

            viewArea = new GuiWidget();

            viewArea.AnchorAll();

            double bedXSize = 200;
            double bedYSize = 200;
            double scale = 1;
            meshViewerWidget = new MeshViewerWidget(bedXSize, bedYSize, scale);

            MeshViewWidget.AnchorAll();

            viewArea.AddChild(MeshViewWidget);

            mainContainer.AddChild(viewArea);

            FlowLayoutWidget buttonPanel = new FlowLayoutWidget(FlowDirection.LeftToRight);
            buttonPanel.HAnchor = HAnchor.ParentLeftRight;
            buttonPanel.Padding = new BorderDouble(3, 3);
            buttonPanel.BackgroundColor = RGBA_Bytes.DarkGray;

            if (meshFileToLoad != "")
            {
                MeshViewWidget.LoadMesh(meshFileToLoad);
            }
            else
            {
                openFileButton = new Button("Open 3D File", 0, 0);
                openFileButton.Click += new Button.ButtonEventHandler(openFileButton_ButtonClick);
                buttonPanel.AddChild(openFileButton);
            }

            bedCheckBox = new CheckBox("Bed");
            bedCheckBox.Checked = true;
            buttonPanel.AddChild(bedCheckBox);

            wireframeCheckBox = new CheckBox("Wireframe");
            buttonPanel.AddChild(wireframeCheckBox);

            GuiWidget leftRightSpacer = new GuiWidget();
            leftRightSpacer.HAnchor = HAnchor.ParentLeftRight;
            buttonPanel.AddChild(leftRightSpacer);

            mainContainer.AddChild(buttonPanel);

            this.AddChild(mainContainer);
            this.AnchorAll();

            bedCheckBox.CheckedStateChanged += new CheckBox.CheckedStateChangedEventHandler(bedCheckBox_CheckedStateChanged);
            wireframeCheckBox.CheckedStateChanged += new CheckBox.CheckedStateChangedEventHandler(wireframeCheckBox_CheckedStateChanged);
        }

        void wireframeCheckBox_CheckedStateChanged(object sender, EventArgs e)
        {
            MeshViewWidget.ShowWireFrame = wireframeCheckBox.Checked;
        }

        void bedCheckBox_CheckedStateChanged(object sender, EventArgs e)
        {
            MeshViewWidget.RenderBed = bedCheckBox.Checked;
        }

        void openFileButton_ButtonClick(object sender, MouseEventArgs mouseEvent)
        {
            GuiHalWidget.OpenFileDialogParams openParams = new GuiHalWidget.OpenFileDialogParams("3D Mesh Files|*.stl;*.amf");
            Stream streamToLoadFrom = GuiHalFactory.PrimaryHalWidget.OpenFileDialog(openParams);

            MeshViewWidget.LoadMesh(openParams.FileName);

            Invalidate();
        }

        int count = 0;
        public override void OnDraw(Graphics2D graphics2D)
        {
            if (count++ == 20)
            {
                StencilCSG csgTest = new StencilCSG(300, 300);
                AddChild(csgTest);
            }
            this.NewGraphics2D().Clear(new RGBA_Bytes(255, 255, 255));

            base.OnDraw(graphics2D);
        }

        [STAThread]
        public static void Main(string[] args)
        {
            CSGOpenGLApplication app = new CSGOpenGLApplication();
            app.ShowAsSystemWindow();
        }
    }
}
