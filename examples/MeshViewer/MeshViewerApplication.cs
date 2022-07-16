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
using System.Diagnostics;
using System.IO;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.RenderOpenGl;
using MatterHackers.VectorMath;

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

		public MeshViewerApplication(bool doDepthPeeling, string meshFileToLoad = "")
			: base(2200, 600)
		{
			this.doDepthPeeling = doDepthPeeling;
			BackgroundColor = Color.White;
			MinimumSize = new Vector2(200, 200);
			Title = "MatterHackers MeshViewr";
			UseOpenGL = true;

			FlowLayoutWidget mainContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
			mainContainer.AnchorAll();

			viewArea = new GuiWidget();

			viewArea.AnchorAll();

			Vector3 viewerVolume = new Vector3(200, 200, 200);
			meshViewerWidget = new MeshViewerWidget(viewerVolume, new Vector2(100, 100), BedShape.Rectangular, "No Part Loaded");

			meshViewerWidget.AnchorAll();

			if (!doDepthPeeling)
			{
				viewArea.AddChild(meshViewerWidget);
			}

			mainContainer.AddChild(viewArea);

			FlowLayoutWidget buttonPanel = new FlowLayoutWidget(FlowDirection.LeftToRight);
			buttonPanel.HAnchor = HAnchor.Stretch;
			buttonPanel.Padding = new BorderDouble(3, 3);
			buttonPanel.BackgroundColor = Color.DarkGray;

			if (meshFileToLoad != "")
			{
				meshViewerWidget.LoadItemIntoScene(meshFileToLoad);
			}
			else
			{
				openFileButton = new Button("Open 3D File", 0, 0);
				openFileButton.Click += openFileButton_ButtonClick;
				buttonPanel.AddChild(openFileButton);
			}

			bedCheckBox = new CheckBox("Bed");
			bedCheckBox.Checked = true;
			buttonPanel.AddChild(bedCheckBox);

			wireframeCheckBox = new CheckBox("Wireframe");
			buttonPanel.AddChild(wireframeCheckBox);

			GuiWidget leftRightSpacer = new GuiWidget();
			leftRightSpacer.HAnchor = HAnchor.Stretch;
			buttonPanel.AddChild(leftRightSpacer);

			mainContainer.AddChild(buttonPanel);

			this.AddChild(mainContainer);
			this.AnchorAll();

			bedCheckBox.CheckedStateChanged += (s, e) =>
			{
				meshViewerWidget.RenderBed = bedCheckBox.Checked;
			};

			wireframeCheckBox.CheckedStateChanged += (s, e) =>
			{
				if (wireframeCheckBox.Checked)
				{
					meshViewerWidget.RenderType = RenderTypes.Polygons;
				}
				else
				{
					meshViewerWidget.RenderType = RenderTypes.Shaded;
				}
			};
		}

		private void openFileButton_ButtonClick(object sender, EventArgs mouseEvent)
		{
			UiThread.RunOnIdle(DoOpenFileButton_ButtonClick);
		}

		private string ValidFileExtensions { get; } = ".STL;.AMF;.OBJ";

		private void DoOpenFileButton_ButtonClick()
		{
			AggContext.FileDialogs.OpenFileDialog(
				new OpenFileDialogParams("3D Mesh Files|*.stl;*.amf;*.obj"),
				async (openParams) =>
				{
					await meshViewerWidget.LoadItemIntoScene(openParams.FileName);
					var children = meshViewerWidget.Scene.Children;
					children[children.Count - 1].Color = Color.FireEngineRed.WithAlpha(100);
				});

			Invalidate();
		}

		public override void OnMouseEnterBounds(MouseEventArgs mouseEvent)
		{
			if (mouseEvent.DragFiles?.Count > 0)
			{
				foreach (string file in mouseEvent.DragFiles)
				{
					string extension = Path.GetExtension(file).ToUpper();
					if ((extension != "" && this.ValidFileExtensions.Contains(extension)))
					{
						mouseEvent.AcceptDrop = true;
					}
				}
			}

			base.OnMouseEnterBounds(mouseEvent);
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			if (mouseEvent.DragFiles?.Count > 0)
			{
				foreach (string file in mouseEvent.DragFiles)
				{
					string extension = Path.GetExtension(file).ToUpper();
					if ((extension != "" && this.ValidFileExtensions.Contains(extension)))
					{
						mouseEvent.AcceptDrop = true;
					}
				}
			}

			base.OnMouseMove(mouseEvent);
		}


		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			if (mouseEvent.DragFiles?.Count > 0)
			{
				foreach (string droppedFileName in mouseEvent.DragFiles)
				{
					string extension = Path.GetExtension(droppedFileName).ToUpper();
					if ((extension != "" && this.ValidFileExtensions.Contains(extension)))
					{
						meshViewerWidget.LoadItemIntoScene(droppedFileName);
						break;
					}
				}
			}
			base.OnMouseUp(mouseEvent);
		}

		private Stopwatch totalDrawTime = new Stopwatch();
		private int drawCount = 0;
		private DepthPeeling depthPeeling;
		private bool doDepthPeeling;

		public override void OnDraw(Graphics2D graphics2D)
		{
			if (doDepthPeeling)
			{
				if (depthPeeling != null)
				{
					depthPeeling.glutDisplayFunc(meshViewerWidget.World, meshViewerWidget.Scene.Children[0]);
				}
				else if (meshViewerWidget.Scene.Children.Count > 0)
				{
					depthPeeling = new DepthPeeling(meshViewerWidget.Scene.Children[0].Mesh);
					depthPeeling.ReshapeFunc((int)Width, (int)Height);
				}
			}

			totalDrawTime.Restart();
			base.OnDraw(graphics2D);
			totalDrawTime.Stop();

			if (true)
			{
				long memory = GC.GetTotalMemory(false);
				this.Title = string.Format("Allocated = {0:n0} : {1}ms, d{2} Size = {3}x{4}", memory, totalDrawTime.ElapsedMilliseconds, drawCount++, this.Width, this.Height);
				//GC.Collect();
			}

			UiThread.RunOnIdle(Invalidate);
		}

		[STAThread]
		public static void Main(string[] args)
		{
			// Force OpenGL
			var Glfw = false;
			if (Glfw)
			{
				AggContext.Config.ProviderTypes.SystemWindowProvider = "MatterHackers.GlfwProvider.GlfwWindowProvider, MatterHackers.GlfwProvider";
			}
			else
			{
				AggContext.Config.ProviderTypes.SystemWindowProvider = "MatterHackers.Agg.UI.OpenGLWinformsWindowProvider, agg_platform_win32";
			}

			var downloadsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

			List<string> possibleMeshes = new()
			{
				"architest_18.stl",
				"Runout Sensor.stl",
				"Swoop Shell_rev9.STL",
				"Engine-Benchmark.stl"
			};

			possibleMeshes.AddRange(args);

			var meshPath = "";
			foreach (var path in possibleMeshes)
			{
				meshPath = Path.Combine(downloadsDirectory, path);
				if (File.Exists(meshPath))
				{
					break;
				}
			}

			MeshViewerApplication app = new MeshViewerApplication(true, meshPath);
			SingleWindowProvider.SetWindowTheme(Color.Black, 12, () => new Button("X", 0, 0), 3, Color.LightGray, Color.DarkGray);
			app.ShowAsSystemWindow();
		}
	}
}