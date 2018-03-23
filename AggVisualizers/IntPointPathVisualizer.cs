using System;
using System.Collections.Generic;
using System.Diagnostics;
using ClipperLib;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.RenderOpenGl;
using MatterHackers.RenderOpenGl.OpenGl;
using MatterHackers.VectorMath;
using Microsoft.VisualStudio.DebuggerVisualizers;
using Newtonsoft.Json;

[assembly: DebuggerVisualizer(
	typeof(AggVisualizers.IntPointPathVisualizer),
	typeof(VisualizerObjectSource),
	Target = typeof(List<List<IntPoint>>),
	Description = "Agg Polygons Visualizer")]

namespace AggVisualizers
{
	public class IntPointPathVisualizer : DialogDebuggerVisualizer
	{
		protected override void Show(IDialogVisualizerService windowService, IVisualizerObjectProvider objectProvider)
		{
			if (windowService == null)
			{
				throw new ArgumentNullException("windowService");
			}

			if (objectProvider == null)
			{
				throw new ArgumentNullException("objectProvider");
			}

			if (objectProvider.GetObject() is List<List<IntPoint>> polygons)
			{
				var vertexStorage = PlatingHelper.PolygonToVertexStorage(polygons);

				var polygonsMesh = VertexSourceToMesh.Extrude(vertexStorage, zHeight: 30);

				// Position
				var aabb = polygonsMesh.GetAxisAlignedBoundingBox();
				polygonsMesh.Transform(Matrix4X4.CreateTranslation(-aabb.Center));
				polygonsMesh.Transform(Matrix4X4.CreateScale(1.6 / aabb.XSize));

				var systemWindow = new SystemWindow(800, 600);
				var lighting = new LightingData();

				//Debugger.Launch();
				systemWindow.AfterDraw += (s, e) =>
				{
					var screenSpaceBounds = systemWindow.TransformToScreenSpace(systemWindow.LocalBounds);

					WorldView world = new WorldView(screenSpaceBounds.Width, screenSpaceBounds.Height);
					//world.Translate(new Vector3(0, 0, 0));
					//world.Rotate(Quaternion.FromEulerAngles(new Vector3(rotateX, 0, 0)));

					GLHelper.SetGlContext(world, screenSpaceBounds, lighting);
					GLHelper.Render(polygonsMesh, Color.White);
					GLHelper.UnsetGlContext();
				};

				using (var displayForm = new OpenGLSystemWindow()
				{
					AggSystemWindow = systemWindow
				})
				{
					//System.Diagnostics.Debugger.Launch();
					windowService.ShowDialog(displayForm);
				}
			}
		}
	}
}
