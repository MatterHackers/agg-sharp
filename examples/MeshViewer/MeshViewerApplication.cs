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

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.RenderOpenGl;
using MatterHackers.VectorMath;
using System;
using System.Diagnostics;
using System.IO;

namespace MatterHackers.MeshVisualizer
{
	public class MeshViewerApplication : SystemWindow
	{
		protected MeshViewerWidget meshViewerWidget;

		private Button openFileButton;
		private CheckBox bedCheckBox;
		private CheckBox wireframeCheckBox;
		private GuiWidget viewArea;

		public MeshViewerWidget MeshViewerWidget
		{
			get { return meshViewerWidget; }
		}

		public MeshViewerApplication(string meshFileToLoad = "")
			: base(800, 600)
		{
			BackgroundColor = RGBA_Bytes.White;
			MinimumSize = new VectorMath.Vector2(200, 200);
			Title = "MatterHackers MeshViewr";
			UseOpenGL = true;

			FlowLayoutWidget mainContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
			mainContainer.AnchorAll();

			viewArea = new GuiWidget();

			viewArea.AnchorAll();

			Vector3 viewerVolume = new Vector3(200, 200, 200);
			meshViewerWidget = new MeshViewerWidget(viewerVolume, new Vector2(100, 100), MeshViewerWidget.BedShape.Rectangular, "No Part Loaded");

			meshViewerWidget.AnchorAll();

			viewArea.AddChild(meshViewerWidget);

			mainContainer.AddChild(viewArea);

			FlowLayoutWidget buttonPanel = new FlowLayoutWidget(FlowDirection.LeftToRight);
			buttonPanel.HAnchor = HAnchor.ParentLeftRight;
			buttonPanel.Padding = new BorderDouble(3, 3);
			buttonPanel.BackgroundColor = RGBA_Bytes.DarkGray;

			if (meshFileToLoad != "")
			{
				meshViewerWidget.LoadItemIntoScene(meshFileToLoad, MeshViewerWidget.CenterPartAfterLoad.DO);
			}
			else
			{
				openFileButton = new Button("Open 3D File", 0, 0);
				openFileButton.Click += new EventHandler(openFileButton_ButtonClick);
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
			bedCheckBox.CheckedStateChanged += bedCheckBox_CheckedStateChanged;
			wireframeCheckBox.CheckedStateChanged += wireframeCheckBox_CheckedStateChanged;
		}

		private void wireframeCheckBox_CheckedStateChanged(object sender, EventArgs e)
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

		private void bedCheckBox_CheckedStateChanged(object sender, EventArgs e)
		{
			meshViewerWidget.RenderBed = bedCheckBox.Checked;
		}

		private void openFileButton_ButtonClick(object sender, EventArgs mouseEvent)
		{
			UiThread.RunOnIdle(DoOpenFileButton_ButtonClick);
		}

		private void DoOpenFileButton_ButtonClick()
		{
			FileDialog.OpenFileDialog(
				new OpenFileDialogParams("3D Mesh Files|*.stl;*.amf"),
				(openParams) =>
				{
					meshViewerWidget.LoadItemIntoScene(openParams.FileName, MeshViewerWidget.CenterPartAfterLoad.DO);
				});

			Invalidate();
		}

		public override void OnDragEnter(FileDropEventArgs fileDropEventArgs)
		{
			foreach (string file in fileDropEventArgs.DroppedFiles)
			{
				string extension = Path.GetExtension(file).ToUpper();
				if ((extension != "" && MeshFileIo.ValidFileExtensions().Contains(extension)))
				{
					fileDropEventArgs.AcceptDrop = true;
				}
			}
			base.OnDragEnter(fileDropEventArgs);
		}

		public override void OnDragOver(FileDropEventArgs fileDropEventArgs)
		{
			foreach (string file in fileDropEventArgs.DroppedFiles)
			{
				string extension = Path.GetExtension(file).ToUpper();
				if ((extension != "" && MeshFileIo.ValidFileExtensions().Contains(extension)))
				{
					fileDropEventArgs.AcceptDrop = true;
				}
			}
			base.OnDragOver(fileDropEventArgs);
		}

		public override void OnDragDrop(FileDropEventArgs fileDropEventArgs)
		{
			foreach (string droppedFileName in fileDropEventArgs.DroppedFiles)
			{
				string extension = Path.GetExtension(droppedFileName).ToUpper();
				if ((extension != "" && MeshFileIo.ValidFileExtensions().Contains(extension)))
				{
					meshViewerWidget.LoadItemIntoScene(droppedFileName, MeshViewerWidget.CenterPartAfterLoad.DO);
					break;
				}
			}

			base.OnDragDrop(fileDropEventArgs);
		}

		private Stopwatch totalDrawTime = new Stopwatch();
		private int drawCount = 0;

		public override void OnDraw(Graphics2D graphics2D)
		{
			totalDrawTime.Restart();
			base.OnDraw(graphics2D);
			totalDrawTime.Stop();

			if (true)
			{
				long memory = GC.GetTotalMemory(false);
				this.Title = string.Format("Allocated = {0:n0} : {1}ms, d{2} Size = {3}x{4}", memory, totalDrawTime.ElapsedMilliseconds, drawCount++, this.Width, this.Height);
				//GC.Collect();
			}
		}

		[STAThread]
		public static void Main(string[] args)
		{
			MeshViewerApplication app = new MeshViewerApplication();
			app.ShowAsSystemWindow();
		}
	}
}