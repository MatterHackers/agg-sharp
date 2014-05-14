/*
Copyright (c) 2014, Lars Brubaker
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met: 

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer. 
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution. 

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies, 
either expressed or implied, of the FreeBSD Project.
*/

using System;
using System.Collections.Generic;
using System.IO;

using MatterHackers.Agg;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.VertexSource;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.Font;
using MatterHackers.RenderOpenGl;
using MatterHackers.VectorMath;

namespace MatterHackers.MeshVisualizer
{
    public class MeshViewerApplication : SystemWindow
    {
        protected MeshViewerWidget meshViewerWidget;
        
        Button openFileButton;
        CheckBox bedCheckBox;
        CheckBox wireframeCheckBox;
        GuiWidget viewArea;

        public MeshViewerWidget MeshViewerWidget
        {
            get { return meshViewerWidget; }
        }

        public MeshViewerApplication(string meshFileToLoad = "")
            : base(800, 600)
        {
            MinimumSize = new VectorMath.Vector2(200, 200);
            Title = "MatterHackers MeshViewr";
            UseOpenGL = true;

            FlowLayoutWidget mainContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
            mainContainer.AnchorAll();

            viewArea = new GuiWidget();

            viewArea.AnchorAll();

            Vector3 viewerVolume = new Vector3(200, 200, 200);
            double scale = 1;
            meshViewerWidget = new MeshViewerWidget(viewerVolume, scale, MeshViewerWidget.BedShape.Rectangular, "No Part Loaded");

            meshViewerWidget.AnchorAll();

            viewArea.AddChild(meshViewerWidget);

            mainContainer.AddChild(viewArea);

            FlowLayoutWidget buttonPanel = new FlowLayoutWidget(FlowDirection.LeftToRight);
            buttonPanel.HAnchor = HAnchor.ParentLeftRight;
            buttonPanel.Padding = new BorderDouble(3, 3);
            buttonPanel.BackgroundColor = RGBA_Bytes.DarkGray;

            if (meshFileToLoad != "")
            {
                meshViewerWidget.LoadMesh(meshFileToLoad);
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

            AddHandlers();
        }

        private void AddHandlers()
        {
            bedCheckBox.CheckedStateChanged += new CheckBox.CheckedStateChangedEventHandler(bedCheckBox_CheckedStateChanged);
            wireframeCheckBox.CheckedStateChanged += new CheckBox.CheckedStateChangedEventHandler(wireframeCheckBox_CheckedStateChanged);
        }

        void wireframeCheckBox_CheckedStateChanged(object sender, EventArgs e)
        {
            if (wireframeCheckBox.Checked)
            {
                meshViewerWidget.RenderType = RenderTypes.Polygons;
            }
            else
            {
                meshViewerWidget.RenderType = RenderTypes.Shaded;
            }
        }

        void bedCheckBox_CheckedStateChanged(object sender, EventArgs e)
        {
            meshViewerWidget.RenderBed = bedCheckBox.Checked;
        }

        void openFileButton_ButtonClick(object sender, MouseEventArgs mouseEvent)
        {
            OpenFileDialogParams openParams = new OpenFileDialogParams("3D Mesh Files|*.stl;*.amf");
            Stream streamToLoadFrom = FileDialog.OpenFileDialog(ref openParams);

            meshViewerWidget.LoadMesh(openParams.FileName);

            Invalidate();
        }

        public override void OnDraw(Graphics2D graphics2D)
        {
            this.NewGraphics2D().Clear(new RGBA_Bytes(255, 255, 255));

            base.OnDraw(graphics2D);
        }

        [STAThread]
        public static void Main(string[] args)
        {
            MeshViewerApplication app = new MeshViewerApplication();
            app.ShowAsSystemWindow();
        }
    }
}
