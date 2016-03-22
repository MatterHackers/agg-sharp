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
using MatterHackers.Agg.Image;
using MatterHackers.Agg.OpenGlGui;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters3D;
using MatterHackers.PolygonMesh;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.RayTracer;
using MatterHackers.RayTracer.Traceable;
using MatterHackers.RenderOpenGl;
using MatterHackers.RenderOpenGl.OpenGl;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;

namespace MatterHackers.MeshVisualizer
{
	public class DrawGlContentEventArgs : EventArgs
	{
		public bool ZBuffered { get; }

		public DrawGlContentEventArgs(bool zBuffered)
		{
			ZBuffered = zBuffered;
		}
	}

	public class MeshViewerWidget : GuiWidget
	{
		static public ImageBuffer BedImage = null;
		public List<InteractionVolume> interactionVolumes = new List<InteractionVolume>();
		public InteractionVolume SelectedInteractionVolume = null;
		public bool MouseDownOnInteractionVolume { get { return SelectedInteractionVolume != null; } }

		public PartProcessingInfo partProcessingInfo;
		private static ImageBuffer lastCreatedBedImage = new ImageBuffer();
		private static Point2D lastLinesCount;
		private static Dictionary<int, RGBA_Bytes> materialColors = new Dictionary<int, RGBA_Bytes>();
		private BackgroundWorker backgroundWorker = null;
		private RGBA_Bytes bedBaseColor = new RGBA_Bytes(245, 245, 255);
		static public Vector2 BedCenter { get; private set; }
		private RGBA_Bytes bedMarkingsColor = RGBA_Bytes.Black;
		private static BedShape bedShape = BedShape.Rectangular;
		private static Mesh buildVolume = null;
		private static Vector3 displayVolume;
		private List<MeshGroup> meshesToRender = new List<MeshGroup>();
		private List<Matrix4X4> meshTransforms = new List<Matrix4X4>();
		private static Mesh printerBed = null;
		private RenderTypes renderType = RenderTypes.Shaded;
		private int selectedMeshGroupIndex = -1;

		public double SnapGridDistance { get; set; } = 1;

		private TrackballTumbleWidget trackballTumbleWidget;

		private int volumeIndexWithMouseDown = -1;

		public MeshViewerWidget(Vector3 displayVolume, Vector2 bedCenter, BedShape bedShape, string startingTextMessage = "")
		{
			RenderType = RenderTypes.Shaded;
			RenderBed = true;
			RenderBuildVolume = false;
			//SetMaterialColor(1, RGBA_Bytes.LightGray, RGBA_Bytes.White);
			BedColor = new RGBA_Floats(.8, .8, .8, .7).GetAsRGBA_Bytes();
			BuildVolumeColor = new RGBA_Floats(.2, .8, .3, .2).GetAsRGBA_Bytes();

			trackballTumbleWidget = new TrackballTumbleWidget();
			trackballTumbleWidget.DrawRotationHelperCircle = false;
			trackballTumbleWidget.DrawGlContent += trackballTumbleWidget_DrawGlContent;
			trackballTumbleWidget.TransformState = TrackBallController.MouseDownType.Rotation;

			AddChild(trackballTumbleWidget);

			CreatePrintBed(displayVolume, bedCenter, bedShape);

			trackballTumbleWidget.AnchorAll();

			partProcessingInfo = new PartProcessingInfo(startingTextMessage);

			GuiWidget labelContainer = new GuiWidget();
			labelContainer.AnchorAll();
			labelContainer.AddChild(partProcessingInfo);
			labelContainer.Selectable = false;

			this.AddChild(labelContainer);
		}

		public event EventHandler LoadDone;

		public enum BedShape { Rectangular, Circular };

		public bool AllowBedRenderingWhenEmpty { get; set; }

		public RGBA_Bytes BedColor { get; set; }

		public RGBA_Bytes BuildVolumeColor { get; set; }

		public Vector3 DisplayVolume { get { return displayVolume; } }

		public static AxisAlignedBoundingBox GetAxisAlignedBoundingBox(List<MeshGroup> meshGroups)
		{
			AxisAlignedBoundingBox totalMeshBounds = new AxisAlignedBoundingBox(Vector3.NegativeInfinity, Vector3.NegativeInfinity);
			bool first = true;
			foreach (MeshGroup meshGroup in meshGroups)
			{
				AxisAlignedBoundingBox meshBounds = meshGroup.GetAxisAlignedBoundingBox();
				if (first)
				{
					totalMeshBounds = meshBounds;
					first = false;
				}
				else
				{
					totalMeshBounds = AxisAlignedBoundingBox.Union(totalMeshBounds, meshBounds);
				}
			}

			return totalMeshBounds;
		}

		public bool HaveSelection
		{
			get { return MeshGroups.Count > 0 && SelectedMeshGroupIndex > -1; }
		}

		public List<MeshGroup> MeshGroups { get { return meshesToRender; } }

		public List<Matrix4X4> MeshGroupTransforms { get { return meshTransforms; } }

		public Mesh PrinterBed { get { return printerBed; } }

		public bool RenderBed { get; set; }

		public bool RenderBuildVolume { get; set; }

		public RenderTypes RenderType
		{
			get { return renderType; }
			set
			{
				if (renderType != value)
				{
					renderType = value;
					foreach (MeshGroup meshGroup in MeshGroups)
					{
						foreach (Mesh mesh in meshGroup.Meshes)
						{
							mesh.MarkAsChanged();
						}
					}
				}
			}
		}

		public MeshGroup SelectedMeshGroup
		{
			get
			{
				if (HaveSelection)
				{
					return MeshGroups[SelectedMeshGroupIndex];
				}

				return null;
			}
		}

		public int SelectedMeshGroupIndex
		{
			get
			{
				if (selectedMeshGroupIndex >= MeshGroups.Count)
				{
					selectedMeshGroupIndex = MeshGroups.Count - 1;
				}

				return selectedMeshGroupIndex;
			}
			set
			{
				selectedMeshGroupIndex = value;
			}
		}

		public Matrix4X4 SelectedMeshGroupTransform
		{
			get
			{
				if (HaveSelection)
				{
					return MeshGroupTransforms[selectedMeshGroupIndex];
				}

				return Matrix4X4.Identity;
			}

			set
			{
				MeshGroupTransforms[selectedMeshGroupIndex] = value;
			}
		}

		public TrackballTumbleWidget TrackballTumbleWidget
		{
			get
			{
				return trackballTumbleWidget;
			}
		}

		public static void AssertDebugNotDefined()
		{
			#if DEBUG
			throw new Exception("DEBUG is defined and should not be!");
			#endif
		}

		public static RGBA_Bytes GetMaterialColor(int materialIndexBase1)
		{
			lock(materialColors)
			{
				if (materialColors.ContainsKey(materialIndexBase1))
				{
					return materialColors[materialIndexBase1];
				}
			}

			// we currently expect at most 4 extruders
			return RGBA_Floats.FromHSL((materialIndexBase1 % 4) / 4.0, .5, .5).GetAsRGBA_Bytes();
		}

		public static RGBA_Bytes GetSelectedMaterialColor(int materialIndexBase1)
		{
			double hue0To1;
			double saturation0To1;
			double lightness0To1;
			GetMaterialColor(materialIndexBase1).GetAsRGBA_Floats().GetHSL(out hue0To1, out saturation0To1, out lightness0To1);

			// now make it a bit lighter and less saturated
			saturation0To1 = Math.Min(1, saturation0To1 * 2);
			lightness0To1 = Math.Min(1, lightness0To1 * 1.2);

			// we sort of expect at most 4 extruders
			return RGBA_Floats.FromHSL(hue0To1, saturation0To1, lightness0To1).GetAsRGBA_Bytes();
		}

		public static void SetMaterialColor(int materialIndexBase1, RGBA_Bytes color)
		{
			lock(materialColors)
			{
				if (!materialColors.ContainsKey(materialIndexBase1))
				{
					materialColors.Add(materialIndexBase1, color);
				}
				else
				{
					materialColors[materialIndexBase1] = color;
				}
			}
		}

		public void CreateGlDataForMeshes(List<MeshGroup> meshGroupsToPrepare)
		{
			for (int i = 0; i < meshGroupsToPrepare.Count; i++)
			{
				MeshGroup meshGroupToPrepare = meshGroupsToPrepare[i];

				foreach (Mesh meshToPrepare in meshGroupToPrepare.Meshes)
				{
					GLMeshTrianglePlugin glMeshPlugin = GLMeshTrianglePlugin.Get(meshToPrepare);
				}
			}
		}

		public void CreatePrintBed(Vector3 displayVolume, Vector2 bedCenter, BedShape bedShape)
		{
			if (MeshViewerWidget.BedCenter == bedCenter
				&& MeshViewerWidget.bedShape == bedShape
				&& MeshViewerWidget.displayVolume == displayVolume)
			{
				return;
			}

			MeshViewerWidget.BedCenter = bedCenter;
			MeshViewerWidget.bedShape = bedShape;
			MeshViewerWidget.displayVolume = displayVolume;
			Vector3 displayVolumeToBuild = Vector3.ComponentMax(displayVolume, new Vector3(1, 1, 1));

			switch (bedShape)
			{
				case BedShape.Rectangular:
					if (displayVolumeToBuild.z > 0)
					{
						buildVolume = PlatonicSolids.CreateCube(displayVolumeToBuild);
						foreach (Vertex vertex in buildVolume.Vertices)
						{
							vertex.Position = vertex.Position + new Vector3(0, 0, displayVolumeToBuild.z / 2);
						}
					}
					CreateRectangularBedGridImage(displayVolumeToBuild, bedCenter);
					printerBed = PlatonicSolids.CreateCube(displayVolumeToBuild.x, displayVolumeToBuild.y, 4);
					{
						Face face = printerBed.Faces[0];
						CommonShapes.PlaceTextureOnFace(face, BedImage);
					}
					break;

				case BedShape.Circular:
					{
						if (displayVolumeToBuild.z > 0)
						{
							buildVolume = VertexSourceToMesh.Extrude(new Ellipse(new Vector2(), displayVolumeToBuild.x / 2, displayVolumeToBuild.y / 2), displayVolumeToBuild.z);
							foreach (Vertex vertex in buildVolume.Vertices)
							{
								vertex.Position = vertex.Position + new Vector3(0, 0, .2);
							}
						}
						CreateCircularBedGridImage((int)(displayVolumeToBuild.x / 10), (int)(displayVolumeToBuild.y / 10));
						printerBed = VertexSourceToMesh.Extrude(new Ellipse(new Vector2(), displayVolumeToBuild.x / 2, displayVolumeToBuild.y / 2), 2);
						{
							foreach (Face face in printerBed.Faces)
							{
								if (face.normal.z > 0)
								{
									FaceTextureData faceData = FaceTextureData.Get(face);
									faceData.Textures.Add(BedImage);
									foreach (FaceEdge faceEdge in face.FaceEdges())
									{
										FaceEdgeTextureUvData edgeUV = FaceEdgeTextureUvData.Get(faceEdge);
										edgeUV.TextureUV.Add(new Vector2((displayVolumeToBuild.x / 2 + faceEdge.firstVertex.Position.x) / displayVolumeToBuild.x,
											(displayVolumeToBuild.y / 2 + faceEdge.firstVertex.Position.y) / displayVolumeToBuild.y));
									}
								}
							}
						}
					}
					break;

				default:
					throw new NotImplementedException();
			}

			foreach (Vertex vertex in printerBed.Vertices)
			{
				vertex.Position = vertex.Position - new Vector3(-bedCenter, 2.2);
			}

			if (buildVolume != null)
			{
				foreach (Vertex vertex in buildVolume.Vertices)
				{
					vertex.Position = vertex.Position - new Vector3(-bedCenter, 2.2);
				}
			}

			Invalidate();
		}

		public AxisAlignedBoundingBox GetBoundsForSelection()
		{
			Matrix4X4 selectedMeshTransform = SelectedMeshGroupTransform;
			AxisAlignedBoundingBox selectedBounds = SelectedMeshGroup.GetAxisAlignedBoundingBox(selectedMeshTransform);
			return selectedBounds;
		}

		public enum CenterPartAfterLoad { DO, DONT }
		
		public void LoadMesh(string meshPathAndFileName, CenterPartAfterLoad centerPart, Vector2 bedCenter = new Vector2())
		{
			if (File.Exists(meshPathAndFileName))
			{
				partProcessingInfo.Visible = true;
				partProcessingInfo.progressControl.PercentComplete = 0;

				backgroundWorker = new BackgroundWorker();
				backgroundWorker.WorkerSupportsCancellation = true;

				backgroundWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(backgroundWorker_RunWorkerCompleted);

				backgroundWorker.DoWork += (object sender, DoWorkEventArgs e) =>
				{
					List<MeshGroup> loadedMeshGroups = MeshFileIo.Load(meshPathAndFileName, reportProgress0to100);
					SetMeshAfterLoad(loadedMeshGroups, centerPart, bedCenter);
					e.Result = loadedMeshGroups;
				};
				backgroundWorker.RunWorkerAsync();
				partProcessingInfo.centeredInfoText.Text = "Loading Mesh...";
			}
			else
			{
				partProcessingInfo.centeredInfoText.Text = string.Format("{0}\n'{1}'", "File not found on disk.", Path.GetFileName(meshPathAndFileName));
			}
		}

		public override void OnClosed(EventArgs e)
		{
			if (backgroundWorker != null)
			{
				backgroundWorker.CancelAsync();
			}
			base.OnClosed(e);
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			base.OnDraw(graphics2D);

			foreach (InteractionVolume interactionVolume in interactionVolumes)
			{
				interactionVolume.Draw2DContent(graphics2D);
			}
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			base.OnMouseDown(mouseEvent);

			if (trackballTumbleWidget.MouseCaptured)
			{
				if (trackballTumbleWidget.TransformState == TrackBallController.MouseDownType.Rotation || mouseEvent.Button == MouseButtons.Right)
				{
					trackballTumbleWidget.DrawRotationHelperCircle = true;
				}
			}

			int volumeHitIndex;
			Ray ray = trackballTumbleWidget.GetRayFromScreen(mouseEvent.Position);
			IntersectInfo info;
			if (FindInteractionVolumeHit(ray, out volumeHitIndex, out info))
			{
				MouseEvent3DArgs mouseEvent3D = new MouseEvent3DArgs(mouseEvent, ray, info);
				volumeIndexWithMouseDown = volumeHitIndex;
				interactionVolumes[volumeHitIndex].OnMouseDown(mouseEvent3D);
				SelectedInteractionVolume = interactionVolumes[volumeHitIndex];
            }
			else
			{
				SelectedInteractionVolume = null;
			}
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			base.OnMouseMove(mouseEvent);

			Ray ray = trackballTumbleWidget.GetRayFromScreen(mouseEvent.Position);
			IntersectInfo info = null;
			if (MouseDownOnInteractionVolume && volumeIndexWithMouseDown != -1)
			{
				MouseEvent3DArgs mouseEvent3D = new MouseEvent3DArgs(mouseEvent, ray, info);
				interactionVolumes[volumeIndexWithMouseDown].OnMouseMove(mouseEvent3D);
			}
			else
			{
				int volumeHitIndex;
				if (FindInteractionVolumeHit(ray, out volumeHitIndex, out info))
				{
					if (volumeIndexWithMouseDown == volumeHitIndex)
					{
						MouseEvent3DArgs mouseEvent3D = new MouseEvent3DArgs(mouseEvent, ray, info);
						interactionVolumes[volumeHitIndex].OnMouseMove(mouseEvent3D);
					}
				}
				for (int i = 0; i < interactionVolumes.Count; i++)
				{
					if (i == volumeHitIndex)
					{
						interactionVolumes[i].MouseOver = true;
						interactionVolumes[i].MouseMoveInfo = info;
                    }
					else
					{
						interactionVolumes[i].MouseOver = false;
						interactionVolumes[i].MouseMoveInfo = null;
					}
				}
			}
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			trackballTumbleWidget.DrawRotationHelperCircle = false;
			Invalidate();

			int volumeHitIndex;
			Ray ray = trackballTumbleWidget.GetRayFromScreen(mouseEvent.Position);
			IntersectInfo info;
			bool anyInteractionVolumeHit = FindInteractionVolumeHit(ray, out volumeHitIndex, out info);
			MouseEvent3DArgs mouseEvent3D = new MouseEvent3DArgs(mouseEvent, ray, info);

			if (MouseDownOnInteractionVolume && volumeIndexWithMouseDown != -1)
			{
				interactionVolumes[volumeIndexWithMouseDown].OnMouseUp(mouseEvent3D);
				SelectedInteractionVolume = null;

				volumeIndexWithMouseDown = -1;
			}
			else
			{
				volumeIndexWithMouseDown = -1;

				if (anyInteractionVolumeHit)
				{
					interactionVolumes[volumeHitIndex].OnMouseUp(mouseEvent3D);
				}
				SelectedInteractionVolume = null;
			}

			base.OnMouseUp(mouseEvent);
		}

		public void SetMeshAfterLoad(List<MeshGroup> loadedMeshGroups, CenterPartAfterLoad centerPart, Vector2 bedCenter)
		{
			MeshGroups.Clear();

			if (loadedMeshGroups == null)
			{
				partProcessingInfo.centeredInfoText.Text = string.Format("Sorry! No 3D view available\nfor this file.");
			}
			else
			{
				CreateGlDataForMeshes(loadedMeshGroups);

				AxisAlignedBoundingBox bounds = new AxisAlignedBoundingBox(Vector3.Zero, Vector3.Zero);
				bool first = true;
				foreach (MeshGroup meshGroup in loadedMeshGroups)
				{
					if (first)
					{
						bounds = meshGroup.GetAxisAlignedBoundingBox();
						first = false;
					}
					else
					{
						bounds = AxisAlignedBoundingBox.Union(bounds, meshGroup.GetAxisAlignedBoundingBox());
					}
				}

				// add all the loaded meshes
				foreach (MeshGroup meshGroup in loadedMeshGroups)
				{
					meshTransforms.Add(Matrix4X4.Identity);
					MeshGroups.Add(meshGroup);
				}

				if (centerPart == CenterPartAfterLoad.DO)
				{
					// make sure the entire load is centered and on the bed
					Vector3 boundsCenter = (bounds.maxXYZ + bounds.minXYZ) / 2;
					for (int i = 0; i < MeshGroups.Count; i++)
					{
						meshTransforms[i] *= Matrix4X4.CreateTranslation(-boundsCenter + new Vector3(0, 0, bounds.ZSize / 2) + new Vector3(bedCenter));
					}
				}
				
				trackballTumbleWidget.TrackBallController = new TrackBallController();
				trackballTumbleWidget.OnBoundsChanged(null);

				ResetView();
			}
		}

		public void ResetView()
		{
			trackballTumbleWidget.ZeroVelocity();
			trackballTumbleWidget.TrackBallController.Reset();
			trackballTumbleWidget.TrackBallController.Scale = .03;
			trackballTumbleWidget.TrackBallController.Translate(-new Vector3(BedCenter));
			trackballTumbleWidget.TrackBallController.Rotate(Quaternion.FromEulerAngles(new Vector3(0, 0, MathHelper.Tau / 16)));
			trackballTumbleWidget.TrackBallController.Rotate(Quaternion.FromEulerAngles(new Vector3(-MathHelper.Tau * .19, 0, 0)));
		}

		private void backgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
		{
			partProcessingInfo.Visible = false;

			if (LoadDone != null)
			{
				LoadDone(this, null);
			}
		}

		private void CreateCircularBedGridImage(int linesInX, int linesInY)
		{
			Vector2 bedImageCentimeters = new Vector2(linesInX, linesInY);
			BedImage = new ImageBuffer(1024, 1024, 32, new BlenderBGRA());
			Graphics2D graphics2D = BedImage.NewGraphics2D();
			graphics2D.Clear(bedBaseColor);
			{
				double lineDist = BedImage.Width / (double)linesInX;

				int count = 1;
				int pointSize = 16;
				graphics2D.DrawString(count.ToString(), 4, 4, pointSize, color: bedMarkingsColor);
				double currentRadius = lineDist;
				Vector2 bedCenter = new Vector2(BedImage.Width / 2, BedImage.Height / 2);
				for (double linePos = lineDist + BedImage.Width / 2; linePos < BedImage.Width; linePos += lineDist)
				{
					int linePosInt = (int)linePos;
					graphics2D.DrawString(count.ToString(), linePos + 2, BedImage.Height / 2, pointSize, color: bedMarkingsColor);

					Ellipse circle = new Ellipse(bedCenter, currentRadius);
					Stroke outline = new Stroke(circle);
					graphics2D.Render(outline, bedMarkingsColor);
					currentRadius += lineDist;
					count++;
				}

				graphics2D.Line(0, BedImage.Height / 2, BedImage.Width, BedImage.Height / 2, bedMarkingsColor);
				graphics2D.Line(BedImage.Width / 2, 0, BedImage.Width / 2, BedImage.Height, bedMarkingsColor);
			}
		}

		private void CreateRectangularBedGridImage(Vector3 displayVolumeToBuild, Vector2 bedCenter)
		{
			int linesInX = (int)Math.Ceiling(displayVolumeToBuild.x / 10);
			int linesInY = (int)Math.Ceiling(displayVolumeToBuild.y / 10);

			lock(lastCreatedBedImage)
			{
				Vector2 bedImageCentimeters = new Vector2(linesInX, linesInY);

				BedImage = new ImageBuffer(1024, 1024, 32, new BlenderBGRA());
				Graphics2D graphics2D = BedImage.NewGraphics2D();
				graphics2D.Clear(bedBaseColor);
				{
					double lineDist = BedImage.Width / (displayVolumeToBuild.x / 10.0);

					double xPositionCm = (-(displayVolume.x / 2.0) + bedCenter.x) / 10.0;
					int xPositionCmInt = (int)Math.Round(xPositionCm);
					double fraction = xPositionCm - xPositionCmInt;
					int pointSize = 20;
					graphics2D.DrawString(xPositionCmInt.ToString(), 4, 4, pointSize, color: bedMarkingsColor);
					for (double linePos = lineDist * (1 - fraction); linePos < BedImage.Width; linePos += lineDist)
					{
						xPositionCmInt++;
						int linePosInt = (int)linePos;
						int lineWidth = 1;
						if (xPositionCmInt == 0)
						{
							lineWidth = 2;
						}
						graphics2D.Line(linePosInt, 0, linePosInt, BedImage.Height, bedMarkingsColor, lineWidth);
						graphics2D.DrawString(xPositionCmInt.ToString(), linePos + 4, 4, pointSize, color: bedMarkingsColor);
					}
				}
				{
					double lineDist = BedImage.Height / (displayVolumeToBuild.y / 10.0);

					double yPositionCm = (-(displayVolume.y / 2.0) + bedCenter.y) / 10.0;
					int yPositionCmInt = (int)Math.Round(yPositionCm);
					double fraction = yPositionCm - yPositionCmInt;
					int pointSize = 20;
					for (double linePos = lineDist * (1 - fraction); linePos < BedImage.Height; linePos += lineDist)
					{
						yPositionCmInt++;
						int linePosInt = (int)linePos;
						int lineWidth = 1;
						if (yPositionCmInt == 0)
						{
							lineWidth = 2;
						}
						graphics2D.Line(0, linePosInt, BedImage.Height, linePosInt, bedMarkingsColor, lineWidth);

						graphics2D.DrawString(yPositionCmInt.ToString(), 4, linePos + 4, pointSize, color: bedMarkingsColor);
					}
				}

				lastCreatedBedImage = BedImage;
				lastLinesCount = new Point2D(linesInX, linesInY);
			}
		}

		private bool FindInteractionVolumeHit(Ray ray, out int interactionVolumeHitIndex, out IntersectInfo info)
		{
			interactionVolumeHitIndex = -1;
			if (interactionVolumes.Count == 0 || interactionVolumes[0].CollisionVolume == null)
			{
				info = null;
				return false;
			}

			List<IPrimitive> mesheTraceables = new List<IPrimitive>();
			foreach (InteractionVolume interactionVolume in interactionVolumes)
			{
				if (interactionVolume.CollisionVolume != null)
				{
					IPrimitive traceData = interactionVolume.CollisionVolume;
					mesheTraceables.Add(new Transform(traceData, interactionVolume.TotalTransform));
				}
			}
			IPrimitive allObjects = BoundingVolumeHierarchy.CreateNewHierachy(mesheTraceables);

			info = allObjects.GetClosestIntersection(ray);
			if (info != null)
			{
				for (int i = 0; i < interactionVolumes.Count; i++)
				{
					List<IPrimitive> insideBounds = new List<IPrimitive>();
					if (interactionVolumes[i].CollisionVolume != null)
					{
						interactionVolumes[i].CollisionVolume.GetContained(insideBounds, info.closestHitObject.GetAxisAlignedBoundingBox());
						if (insideBounds.Contains(info.closestHitObject))
						{
							interactionVolumeHitIndex = i;
							return true;
						}
					}
				}
			}

			return false;
		}

		private void reportProgress0to100(double progress0To1, string processingState, out bool continueProcessing)
		{
			if (this.WidgetHasBeenClosed)
			{
				continueProcessing = false;
			}
			else
			{
				continueProcessing = true;
			}

			UiThread.RunOnIdle(() =>
			{
				int percentComplete = (int)(progress0To1 * 100);
				partProcessingInfo.centeredInfoText.Text = "Loading Mesh {0}%...".FormatWith(percentComplete);
				partProcessingInfo.progressControl.PercentComplete = percentComplete;
				partProcessingInfo.centeredInfoDescription.Text = processingState;
			});
		}

		private void trackballTumbleWidget_DrawGlContent(object sender, EventArgs e)
		{
			for (int groupIndex = 0; groupIndex < MeshGroups.Count; groupIndex++)
			{
				MeshGroup meshGroupToRender = MeshGroups[groupIndex];

				int part = 0;
				foreach (Mesh meshToRender in meshGroupToRender.Meshes)
				{
					MeshMaterialData meshData = MeshMaterialData.Get(meshToRender);
					RGBA_Bytes drawColor = GetMaterialColor(meshData.MaterialIndex);
					if (meshGroupToRender == SelectedMeshGroup)
					{
						drawColor = GetSelectedMaterialColor(meshData.MaterialIndex);
					}

					RenderMeshToGl.Render(meshToRender, drawColor, MeshGroupTransforms[groupIndex], RenderType);
					part++;
				}
			}

			// we don't want to render the bed or build volume before we load a model.
			if (MeshGroups.Count > 0 || AllowBedRenderingWhenEmpty)
			{
				if (RenderBed)
				{
					RenderMeshToGl.Render(printerBed, this.BedColor);
				}

				if (buildVolume != null && RenderBuildVolume)
				{
					RenderMeshToGl.Render(buildVolume, this.BuildVolumeColor);
				}

				if (false) // this is code to draw a small axis indicator
				{
					double big = 10;
					double small = 1;
					Mesh xAxis = PlatonicSolids.CreateCube(big, small, small);
					RenderMeshToGl.Render(xAxis, RGBA_Bytes.Red);
					Mesh yAxis = PlatonicSolids.CreateCube(small, big, small);
					RenderMeshToGl.Render(yAxis, RGBA_Bytes.Green);
					Mesh zAxis = PlatonicSolids.CreateCube(small, small, big);
					RenderMeshToGl.Render(zAxis, RGBA_Bytes.Blue);
				}
			}

			DrawInteractionVolumes(e);
		}

		private void DrawInteractionVolumes(EventArgs e)
		{
			// draw on top of anything that is already drawn
			foreach (InteractionVolume interactionVolume in interactionVolumes)
			{
				if (interactionVolume.DrawOnTop)
				{
					GL.Disable(EnableCap.DepthTest);
					interactionVolume.DrawGlContent(new DrawGlContentEventArgs(false));
					GL.Enable(EnableCap.DepthTest);
				}
			}

			// Draw again setting the depth buffer and ensuring that all the interaction objects are sorted as well as we can
			foreach (InteractionVolume interactionVolume in interactionVolumes)
			{
				interactionVolume.DrawGlContent(new DrawGlContentEventArgs(true));
			}
		}

		public class PartProcessingInfo : FlowLayoutWidget
		{
			internal TextWidget centeredInfoDescription;
			internal TextWidget centeredInfoText;
			internal ProgressControl progressControl;

			internal PartProcessingInfo(string startingTextMessage)
				: base(FlowDirection.TopToBottom)
			{
				progressControl = new ProgressControl("", RGBA_Bytes.Black, RGBA_Bytes.Black);
				progressControl.HAnchor = HAnchor.ParentCenter;
				AddChild(progressControl);
				progressControl.Visible = false;
				progressControl.ProgressChanged += (sender, e) =>
				{
					progressControl.Visible = true;
				};

				centeredInfoText = new TextWidget(startingTextMessage);
				centeredInfoText.HAnchor = HAnchor.ParentCenter;
				centeredInfoText.AutoExpandBoundsToText = true;
				AddChild(centeredInfoText);

				centeredInfoDescription = new TextWidget("");
				centeredInfoDescription.HAnchor = HAnchor.ParentCenter;
				centeredInfoDescription.AutoExpandBoundsToText = true;
				AddChild(centeredInfoDescription);

				VAnchor |= VAnchor.ParentCenter;
				HAnchor |= HAnchor.ParentCenter;
			}
		}
	}
}