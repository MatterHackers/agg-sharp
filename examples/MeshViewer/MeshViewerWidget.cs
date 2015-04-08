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
using MatterHackers.PolygonMesh;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.RayTracer;
using MatterHackers.RayTracer.Traceable;
using MatterHackers.RenderOpenGl;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;

namespace MatterHackers.MeshVisualizer
{
	public class MeshViewerWidget : GuiWidget
	{
		private BackgroundWorker backgroundWorker = null;
		private int selectedMeshGroupIndex = -1;

		public RGBA_Bytes BedColor { get; set; }

		public RGBA_Bytes BuildVolumeColor { get; set; }

		private double snapGridDistance = 1;

		public double SnapGridDistance
		{
			get { return snapGridDistance; }
			set { snapGridDistance = value; }
		}

		private Vector3 displayVolume;

		public Vector3 DisplayVolume { get { return displayVolume; } }

		public bool AllowBedRenderingWhenEmpty { get; set; }

		public bool RenderBed { get; set; }

		public bool RenderBuildVolume { get; set; }

		private RenderTypes renderType = RenderTypes.Shaded;

		public ImageBuffer BedImage;
		private TrackballTumbleWidget trackballTumbleWidget;

		private static Dictionary<int, RGBA_Bytes> materialColors = new Dictionary<int, RGBA_Bytes>();

		public static RGBA_Bytes GetMaterialColor(int materialIndexBase1)
		{
			using (TimedLock.Lock(materialColors, "GettingMaterialColors"))
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
			using (TimedLock.Lock(materialColors, "SettingMaterialColors"))
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

		public class PartProcessingInfo : FlowLayoutWidget
		{
			internal ProgressControl progressControl;
			internal TextWidget centeredInfoText;
			internal TextWidget centeredInfoDescription;

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

		public PartProcessingInfo partProcessingInfo;

		public TrackballTumbleWidget TrackballTumbleWidget
		{
			get
			{
				return trackballTumbleWidget;
			}
		}

		public bool HaveSelection
		{
			get { return MeshGroups.Count > 0 && SelectedMeshGroupIndex > -1; }
		}

		public int SelectedMeshGroupIndex
		{
			get { return selectedMeshGroupIndex; }
			set { selectedMeshGroupIndex = value; }
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

		public ScaleRotateTranslate SelectedMeshGroupTransform
		{
			get
			{
				if (HaveSelection)
				{
					return MeshGroupTransforms[selectedMeshGroupIndex];
				}

				return ScaleRotateTranslate.Identity();
			}

			set
			{
				MeshGroupTransforms[selectedMeshGroupIndex] = value;
			}
		}

		private List<ScaleRotateTranslate> meshTransforms = new List<ScaleRotateTranslate>();

		public List<ScaleRotateTranslate> MeshGroupTransforms { get { return meshTransforms; } }

		private List<MeshGroup> meshesToRender = new List<MeshGroup>();

		public List<MeshGroup> MeshGroups { get { return meshesToRender; } }

		public event EventHandler LoadDone;

		private Mesh printerBed = null;

		public Mesh PrinterBed { get { return printerBed; } }

		private Mesh buildVolume = null;

		public enum BedShape { Rectangular, Circular };

		private BedShape bedShape = BedShape.Rectangular;
		private Vector2 bedCenter;

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

		public void CreatePrintBed(Vector3 displayVolume, Vector2 bedCenter, BedShape bedShape)
		{
			if (this.bedCenter == bedCenter
				&& this.bedShape == bedShape
				&& this.displayVolume == displayVolume)
			{
				return;
			}

			this.bedCenter = bedCenter;
			this.bedShape = bedShape;
			displayVolume = Vector3.ComponentMax(displayVolume, new Vector3(1, 1, 1));
			this.displayVolume = displayVolume;

			switch (bedShape)
			{
				case BedShape.Rectangular:
					if (displayVolume.z > 0)
					{
						buildVolume = PlatonicSolids.CreateCube(displayVolume);
						foreach (Vertex vertex in buildVolume.Vertices)
						{
							vertex.Position = vertex.Position + new Vector3(0, 0, displayVolume.z / 2);
						}
					}
					CreateRectangularBedGridImage((int)(displayVolume.x / 10), (int)(displayVolume.y / 10));
					printerBed = PlatonicSolids.CreateCube(displayVolume.x, displayVolume.y, 4);
					{
						Face face = printerBed.Faces[0];
						{
							FaceTextureData faceData = FaceTextureData.Get(face);
							faceData.Textures.Add(BedImage);
							foreach (FaceEdge faceEdge in face.FaceEdges())
							{
								FaceEdgeTextureUvData edgeUV = FaceEdgeTextureUvData.Get(faceEdge);
								edgeUV.TextureUV.Add(new Vector2((displayVolume.x / 2 + faceEdge.firstVertex.Position.x) / displayVolume.x,
									(displayVolume.y / 2 + faceEdge.firstVertex.Position.y) / displayVolume.y));
							}
						}
					}
					foreach (Vertex vertex in printerBed.Vertices)
					{
						vertex.Position = vertex.Position - new Vector3(0, 0, 2.2);
					}
					break;

				case BedShape.Circular:
					{
						if (displayVolume.z > 0)
						{
							buildVolume = VertexSourceToMesh.Extrude(new Ellipse(new Vector2(), displayVolume.x / 2, displayVolume.y / 2), displayVolume.z);
							foreach (Vertex vertex in buildVolume.Vertices)
							{
								vertex.Position = vertex.Position + new Vector3(0, 0, .2);
							}
						}
						CreateCircularBedGridImage((int)(displayVolume.x / 10), (int)(displayVolume.y / 10));
						printerBed = VertexSourceToMesh.Extrude(new Ellipse(new Vector2(), displayVolume.x / 2, displayVolume.y / 2), 2);
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
										edgeUV.TextureUV.Add(new Vector2((displayVolume.x / 2 + faceEdge.firstVertex.Position.x) / displayVolume.x,
											(displayVolume.y / 2 + faceEdge.firstVertex.Position.y) / displayVolume.y));
									}
								}
							}
						}

						foreach (Vertex vertex in printerBed.Vertices)
						{
							vertex.Position = vertex.Position - new Vector3(0, 0, 2.2);
						}
					}
					break;

				default:
					throw new NotImplementedException();
			}

			Invalidate();
		}

		public override void OnClosed(EventArgs e)
		{
			if (backgroundWorker != null)
			{
				backgroundWorker.CancelAsync();
			}
			base.OnClosed(e);
		}

		public List<InteractionVolume> interactionVolumes = new List<InteractionVolume>();

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

					RenderMeshToGl.Render(meshToRender, drawColor, MeshGroupTransforms[groupIndex].TotalTransform, RenderType);
					part++;
				}
			}

			foreach (InteractionVolume interactionVolume in interactionVolumes)
			{
				interactionVolume.DrawGlContent(e);
			}

			// we don't want to render the bed or bulid volume before we load a model.
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
			}
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			base.OnDraw(graphics2D);

			foreach (InteractionVolume interactionVolume in interactionVolumes)
			{
				interactionVolume.Draw2DContent(graphics2D);
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

		public enum CenterPartAfterLoad { DO, DONT }

		public void LoadMesh(string meshPathAndFileName, CenterPartAfterLoad centerPart)
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
					SetMeshAfterLoad(loadedMeshGroups, centerPart);
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

		public void SetMeshAfterLoad(List<MeshGroup> loadedMeshGroups, CenterPartAfterLoad centerPart)
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

				foreach (MeshGroup meshGroup in loadedMeshGroups)
				{
					// make sure the mesh is centered about the origin so rotations will come from a reasonable place
					ScaleRotateTranslate centering = ScaleRotateTranslate.Identity();
					centering.SetCenteringForMeshGroup(meshGroup);
					meshTransforms.Add(centering);
					MeshGroups.Add(meshGroup);
				}

				if (centerPart == CenterPartAfterLoad.DO)
				{
					// make sure the entile load is centered and on the bed
					Vector3 boundsCenter = (bounds.maxXYZ + bounds.minXYZ) / 2;
					for (int i = 0; i < MeshGroups.Count; i++)
					{
						ScaleRotateTranslate moved = meshTransforms[i];
						moved.translation *= Matrix4X4.CreateTranslation(-boundsCenter + new Vector3(0, 0, bounds.ZSize / 2));
						meshTransforms[i] = moved;
					}
				}

				trackballTumbleWidget.TrackBallController = new TrackBallController();
				trackballTumbleWidget.OnBoundsChanged(null);
				trackballTumbleWidget.TrackBallController.Scale = .03;
				trackballTumbleWidget.TrackBallController.Rotate(Quaternion.FromEulerAngles(new Vector3(0, 0, MathHelper.Tau / 16)));
				trackballTumbleWidget.TrackBallController.Rotate(Quaternion.FromEulerAngles(new Vector3(-MathHelper.Tau * .19, 0, 0)));
			}
		}

		private void backgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
		{
			partProcessingInfo.Visible = false;

			if (LoadDone != null)
			{
				LoadDone(this, null);
			}
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

			UiThread.RunOnIdle((object state) =>
			{
				int percentComplete = (int)(progress0To1 * 100);
				partProcessingInfo.centeredInfoText.Text = "Loading Mesh {0}%...".FormatWith(percentComplete);
				partProcessingInfo.progressControl.PercentComplete = percentComplete;
				partProcessingInfo.centeredInfoDescription.Text = processingState;
			});
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
				IPrimitive traceData = interactionVolume.CollisionVolume;
				mesheTraceables.Add(new Transform(traceData, interactionVolume.TotalTransform));
			}
			IPrimitive allObjects = BoundingVolumeHierarchy.CreateNewHierachy(mesheTraceables);

			info = allObjects.GetClosestIntersection(ray);
			if (info != null)
			{
				for (int i = 0; i < interactionVolumes.Count; i++)
				{
					List<IPrimitive> insideBounds = new List<IPrimitive>();
					interactionVolumes[i].CollisionVolume.GetContained(insideBounds, info.closestHitObject.GetAxisAlignedBoundingBox());
					if (insideBounds.Contains(info.closestHitObject))
					{
						interactionVolumeHitIndex = i;
						return true;
					}
				}
			}

			return false;
		}

		private int volumeIndexWithMouseDown = -1;
		public bool MouseDownOnInteractionVolume = false;

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			base.OnMouseDown(mouseEvent);

			if (trackballTumbleWidget.MouseCaptured)
			{
				if (trackballTumbleWidget.TransformState == TrackBallController.MouseDownType.Rotation)
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
				MouseDownOnInteractionVolume = true;
			}
			else
			{
				MouseDownOnInteractionVolume = false;
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
					}
					else
					{
						interactionVolumes[i].MouseOver = false;
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
				MouseDownOnInteractionVolume = false;
				interactionVolumes[volumeIndexWithMouseDown].OnMouseUp(mouseEvent3D);

				volumeIndexWithMouseDown = -1;
			}
			else
			{
				MouseDownOnInteractionVolume = false;
				volumeIndexWithMouseDown = -1;

				if (anyInteractionVolumeHit)
				{
					interactionVolumes[volumeHitIndex].OnMouseUp(mouseEvent3D);
				}
			}

			base.OnMouseUp(mouseEvent);
		}

		private RGBA_Bytes bedMarkingsColor = RGBA_Bytes.Black;
		private RGBA_Bytes bedBaseColor = new RGBA_Bytes(245, 245, 255);

		private void CreateRectangularBedGridImage(int linesInX, int linesInY)
		{
			Vector2 bedImageCentimeters = new Vector2(linesInX, linesInY);

			BedImage = new ImageBuffer(1024, 1024, 32, new BlenderBGRA());
			Graphics2D graphics2D = BedImage.NewGraphics2D();
			graphics2D.Clear(bedBaseColor);
			{
				double lineDist = BedImage.Width / (double)linesInX;

				int count = 1;
				int pointSize = 20;
				graphics2D.DrawString(count.ToString(), 4, 4, pointSize, color: bedMarkingsColor);
				for (double linePos = lineDist; linePos < BedImage.Width; linePos += lineDist)
				{
					count++;
					int linePosInt = (int)linePos;
					graphics2D.Line(linePosInt, 0, linePosInt, BedImage.Height, bedMarkingsColor);
					graphics2D.DrawString(count.ToString(), linePos + 4, 4, pointSize, color: bedMarkingsColor);
				}
			}
			{
				double lineDist = BedImage.Height / (double)linesInY;

				int count = 1;
				int pointSize = 16;
				for (double linePos = lineDist; linePos < BedImage.Height; linePos += lineDist)
				{
					count++;
					int linePosInt = (int)linePos;
					graphics2D.Line(0, linePosInt, BedImage.Height, linePosInt, bedMarkingsColor);
					graphics2D.DrawString(count.ToString(), 4, linePos + 4, pointSize, color: bedMarkingsColor);
				}
			}
		}

		private void CreateCircularBedGridImage(int linesInX, int linesInY)
		{
			Vector2 bedImageCentimeters = new Vector2(linesInX, linesInY);
			BedImage = new ImageBuffer(1024, 1024, 32, new BlenderBGRA());
			Graphics2D graphics2D = BedImage.NewGraphics2D();
			graphics2D.Clear(bedBaseColor);
#if true
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
#else
            {
                double lineDist = bedCentimeterGridImage.Width / (double)linesInX;

                int count = 1;
                int pointSize = 20;
                graphics2D.DrawString(count.ToString(), 0, 0, pointSize);
                for (double linePos = lineDist; linePos < bedCentimeterGridImage.Width; linePos += lineDist)
                {
                    count++;
                    int linePosInt = (int)linePos;
                    graphics2D.Line(linePosInt, 0, linePosInt, bedCentimeterGridImage.Height, RGBA_Bytes.Black);
                    graphics2D.DrawString(count.ToString(), linePos, 0, pointSize);
                }
            }
            {
                double lineDist = bedCentimeterGridImage.Height / (double)linesInY;

                int count = 1;
                int pointSize = 20;
                for (double linePos = lineDist; linePos < bedCentimeterGridImage.Height; linePos += lineDist)
                {
                    count++;
                    int linePosInt = (int)linePos;
                    graphics2D.Line(0, linePosInt, bedCentimeterGridImage.Height, linePosInt, RGBA_Bytes.Black);
                    graphics2D.DrawString(count.ToString(), 0, linePos, pointSize);
                }
            }
#endif
		}

		public static void AssertDebugNotDefined()
		{
#if DEBUG
			throw new Exception("DEBUG is defined and should not be!");
#endif
		}

		public AxisAlignedBoundingBox GetBoundsForSelection()
		{
			Matrix4X4 selectedMeshTransform = SelectedMeshGroupTransform.TotalTransform;
			AxisAlignedBoundingBox selectedBounds = SelectedMeshGroup.GetAxisAlignedBoundingBox(selectedMeshTransform);
			return selectedBounds;
		}
	}
}