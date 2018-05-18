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
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
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
		private Button openFileButton;
		private TextWidget layerCountTextWidget;
		private NumberEdit currentLayerIndex;
		private Button prevLayerButton;
		private Button nextLayerButton;

		private GCodeViewWidget gCodeViewWidget;

		public GCodeViewerApplication(string gCodeToLoad = "")
			: base(800, 600)
		{
			this.Title = "G Code Visualizer";

			MinimumSize = new VectorMath.Vector2(200, 200);
			Title = "MatterHackers GCodeVisualizer";
			gCodeViewWidget = new GCodeViewWidget(new Vector2(), new Vector2(100, 100));
			AddChild(gCodeViewWidget);

			FlowLayoutWidget keepOnTop = new FlowLayoutWidget();

			prevLayerButton = new Button("<<", 0, 0);
			prevLayerButton.Click += prevLayer_ButtonClick;
			keepOnTop.AddChild(prevLayerButton);

			currentLayerIndex = new NumberEdit(1, pixelWidth: 40);
			keepOnTop.AddChild(currentLayerIndex);
			currentLayerIndex.EditComplete += new EventHandler(layerCountTextWidget_EditComplete);

			layerCountTextWidget = new TextWidget("/1____", 12);
			keepOnTop.AddChild(layerCountTextWidget);

			nextLayerButton = new Button(">>", 0, 0);
			nextLayerButton.Click += nextLayer_ButtonClick;
			keepOnTop.AddChild(nextLayerButton);

			if (gCodeToLoad != "")
			{
				gCodeViewWidget.LoadFile(gCodeToLoad);
			}
			else
			{
				openFileButton = new Button("Open GCode", 0, 0);
				openFileButton.Click += openFileButton_ButtonClick;
				keepOnTop.AddChild(openFileButton);
			}

			AddChild(keepOnTop);

			AnchorAll();
			UiThread.RunOnIdle(currentLayerIndex.Focus);
		}

		public string DemoCategory { get; } = "Other";

		public string DemoDescription { get; } = "A sample application to visualize the g-code created for a rep-rap type FDM machine.";

		private void SetActiveLayer(int layer)
		{
			gCodeViewWidget.ActiveLayerIndex = layer;
			currentLayerIndex.Value = gCodeViewWidget.ActiveLayerIndex + 1;

			Invalidate();
		}

		private void layerCountTextWidget_EditComplete(object sender, EventArgs e)
		{
			SetActiveLayer((int)currentLayerIndex.Value - 1);
		}

		private void nextLayer_ButtonClick(object sender, EventArgs mouseEvent)
		{
			SetActiveLayer(gCodeViewWidget.ActiveLayerIndex + 1);
		}

		private void prevLayer_ButtonClick(object sender, EventArgs mouseEvent)
		{
			SetActiveLayer(gCodeViewWidget.ActiveLayerIndex - 1);
		}

		private void openFileButton_ButtonClick(object sender, EventArgs mouseEvent)
		{
			UiThread.RunOnIdle(() =>
			{
				OpenFileDialogParams openParams = new OpenFileDialogParams("gcode files|*.gcode");
				AggContext.FileDialogs.OpenFileDialog(openParams, onFileSelected);
			});
		}

		private void onFileSelected(OpenFileDialogParams openParams)
		{
			if (!string.IsNullOrEmpty(openParams.FileName))
			{
				gCodeViewWidget.LoadFile(openParams.FileName);
				currentLayerIndex.Value = 0;
				currentLayerIndex.MaxValue = gCodeViewWidget.LoadedGCode.NumChangesInZ;
			}

			Invalidate();
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			this.NewGraphics2D().Clear(new Color(255, 255, 255));

			if (gCodeViewWidget.LoadedGCode != null)
			{
				layerCountTextWidget.Text = "/" + gCodeViewWidget.LoadedGCode.NumChangesInZ.ToString();
			}

			base.OnDraw(graphics2D);
		}

		[STAThread]
		public static void Main(string[] args)
		{
			GCodeViewerApplication app = new GCodeViewerApplication();
			app.UseOpenGL = true;
			app.DoubleBuffer = true;
			app.BackBuffer.SetRecieveBlender(new BlenderPreMultBGRA());
			app.ShowAsSystemWindow();

			//var demoWidget = new aa_demo();

			//var systemWindow = new SystemWindow(600, 400);
			//systemWindow.Title = demoWidget.Title;
			//systemWindow.AddChild(demoWidget);
			//systemWindow.ShowAsSystemWindow();
		}
	}
}