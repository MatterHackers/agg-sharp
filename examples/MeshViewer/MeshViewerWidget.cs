/*
Copyright (c) 2016, Lars Brubaker, John Lewin
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

#define DO_LIGHTING

using MatterHackers.Agg;
using MatterHackers.Agg.Image;
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
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Threading;
using MatterHackers.VectorMath.TrackBall;

namespace MatterHackers.MeshVisualizer
{
	public enum BedShape { Rectangular, Circular };

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
		public InteractionVolume SelectedInteractionVolume { get; set; } = null;
		public InteractionVolume HoveredInteractionVolume { get; set; } = null;
		public bool MouseDownOnInteractionVolume { get { return SelectedInteractionVolume != null; } }

		public PartProcessingInfo partProcessingInfo;
		private static ImageBuffer lastCreatedBedImage = new ImageBuffer();
		private static Dictionary<int, Color> materialColors = new Dictionary<int, Color>();
		private Color bedBaseColor = new Color(245, 245, 255);
		static public Vector2 BedCenter { get; private set; }
		private Color bedMarkingsColor = Color.Black;
		private static BedShape bedShape = BedShape.Rectangular;
		private static Mesh buildVolume = null;
		private static Vector3 displayVolume;
		private static Mesh printerBed = null;
		private RenderTypes renderType = RenderTypes.Shaded;

		private float[] ambientLight = { 0.2f, 0.2f, 0.2f, 1.0f };

		private float[] diffuseLight0 = { 0.7f, 0.7f, 0.7f, 1.0f };
		private float[] specularLight0 = { 0.5f, 0.5f, 0.5f, 1.0f };
		private float[] lightDirection0 = { -1, -1, 1, 0.0f };

		private float[] diffuseLight1 = { 0.5f, 0.5f, 0.5f, 1.0f };
		private float[] specularLight1 = { 0.3f, 0.3f, 0.3f, 1.0f };
		private float[] lightDirection1 = { 1, 1, 1, 0.0f };

		public double SnapGridDistance { get; set; } = 1;

		private TrackballTumbleWidget trackballTumbleWidget;

		private int volumeIndexWithMouseDown = -1;

		private Color accentColor = Color.Blue;

		public MeshViewerWidget(Vector3 displayVolume, Vector2 bedCenter, BedShape bedShape, string startingTextMessage = "")
		{
			Scene.SelectionChanged += (sender, e) =>
			{
				Invalidate();
			};
			RenderType = RenderTypes.Shaded;
			RenderBed = true;
			RenderBuildVolume = false;
			//SetMaterialColor(1, RGBA_Bytes.LightGray, RGBA_Bytes.White);
			BedColor = new ColorF(.8, .8, .8, .7).ToColor();
			BuildVolumeColor = new ColorF(.2, .8, .3, .2).ToColor();

			trackballTumbleWidget = new TrackballTumbleWidget(this.World, this);
			trackballTumbleWidget.TransformState = TrackBallTransformType.Rotation;

			AddChild(trackballTumbleWidget);

			CreatePrintBed(displayVolume, bedCenter, bedShape);

			trackballTumbleWidget.AnchorAll();

			partProcessingInfo = new PartProcessingInfo(startingTextMessage);

			GuiWidget labelContainer = new GuiWidget();
			labelContainer.AnchorAll();
			labelContainer.AddChild(partProcessingInfo);
			labelContainer.Selectable = false;

			SetMaterialColor(1, accentColor);

			this.AddChild(labelContainer);
		}

		public WorldView World { get; } = new WorldView(0, 0);

		public event EventHandler LoadDone;

		public bool AllowBedRenderingWhenEmpty { get; set; }

		public Color BedColor { get; set; }

		public Color BuildVolumeColor { get; set; }

		public Vector3 DisplayVolume { get { return displayVolume; } }

		public override void OnLoad(EventArgs args)
		{
			// some debug code to be able to click on parts
			if (false)
			{
				AfterDraw += (sender, e) =>
				{
					foreach (var child in Scene.Children)
					{
						this.World.RenderDebugAABB(e.Graphics2D, child.TraceData().GetAxisAlignedBoundingBox());
						this.World.RenderDebugAABB(e.Graphics2D, child.GetAxisAlignedBoundingBox(Matrix4X4.Identity));
					}
				};
			}

			base.OnLoad(args);
		}

		public override void FindNamedChildrenRecursive(string nameToSearchFor, List<WidgetAndPosition> foundChildren, RectangleDouble touchingBounds, SearchType seachType, bool allowInvalidItems = true)
		{
			foreach (var child in Scene.Children)
			{
				string object3DName = child.Name;
				if (object3DName == null && child.MeshPath != null)
				{
					object3DName = Path.GetFileName(child.MeshPath);
				}

				bool nameFound = false;

				if (seachType == SearchType.Exact)
				{
					if (object3DName == nameToSearchFor)
					{
						nameFound = true;
					}
				}
				else
				{
					if (nameToSearchFor == ""
						|| object3DName.Contains(nameToSearchFor))
					{
						nameFound = true;
					}
				}

				if (nameFound)
				{
					AxisAlignedBoundingBox bounds = child.TraceData().GetAxisAlignedBoundingBox();

					RectangleDouble screenBoundsOfObject3D = RectangleDouble.ZeroIntersection;
					for(int i=0; i<4; i++)
					{
						screenBoundsOfObject3D.ExpandToInclude(this.World.GetScreenPosition(bounds.GetTopCorner(i)));
						screenBoundsOfObject3D.ExpandToInclude(this.World.GetScreenPosition(bounds.GetBottomCorner(i)));
					}

					if (touchingBounds.IsTouching(screenBoundsOfObject3D))
					{
						Vector3 renderPosition = bounds.Center;
						Vector2 objectCenterScreenSpace = this.World.GetScreenPosition(renderPosition);
						Point2D screenPositionOfObject3D = new Point2D((int)objectCenterScreenSpace.X, (int)objectCenterScreenSpace.Y);

						foundChildren.Add(new WidgetAndPosition(this, screenPositionOfObject3D, object3DName));
					}
				}
			}

			base.FindNamedChildrenRecursive(nameToSearchFor, foundChildren, touchingBounds, seachType, allowInvalidItems);
		}

		public InteractiveScene Scene { get; } = new InteractiveScene();

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
					foreach(var renderTransfrom in Scene.VisibleMeshes())
					{
						renderTransfrom.Mesh.MarkAsChanged();
					}
				}
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

		public static Color GetMaterialColor(int materialIndexBase1)
		{
			lock (materialColors)
			{
				if (materialColors.ContainsKey(materialIndexBase1))
				{
					return materialColors[materialIndexBase1];
				}
			}

			// we currently expect at most 4 extruders
			return ColorF.FromHSL((materialIndexBase1 % 4) / 4.0, .5, .5).ToColor();
		}

		public static Color GetSelectedMaterialColor(int materialIndexBase1)
		{
			double hue0To1;
			double saturation0To1;
			double lightness0To1;
			GetMaterialColor(materialIndexBase1).ToColorF().GetHSL(out hue0To1, out saturation0To1, out lightness0To1);

			// now make it a bit lighter and less saturated
			saturation0To1 = Math.Min(1, saturation0To1 * 2);
			lightness0To1 = Math.Min(1, lightness0To1 * 1.2);

			// we sort of expect at most 4 extruders
			return ColorF.FromHSL(hue0To1, saturation0To1, lightness0To1).ToColor();
		}

		public static void SetMaterialColor(int materialIndexBase1, Color color)
		{
			lock (materialColors)
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

		public void CreateGlDataObject(IObject3D item)
		{
			if(item.Mesh != null)
			{
				GLMeshTrianglePlugin.Get(item.Mesh);
			}

			foreach (IObject3D child in item.Children.Where(o => o.Mesh != null))
			{
				GLMeshTrianglePlugin.Get(child.Mesh);
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

			double sizeForMarking = Math.Max(displayVolumeToBuild.X, displayVolumeToBuild.Y);
			double divisor = 10;
			int skip = 1;
			if (sizeForMarking > 1000)
			{
				divisor = 100;
				skip = 10;
			}
			else if (sizeForMarking > 300)
			{
				divisor = 50;
				skip = 5;
			}

			switch (bedShape)
			{
				case BedShape.Rectangular:
					if (displayVolumeToBuild.Z > 0)
					{
						buildVolume = PlatonicSolids.CreateCube(displayVolumeToBuild);
						foreach (Vertex vertex in buildVolume.Vertices)
						{
							vertex.Position = vertex.Position + new Vector3(0, 0, displayVolumeToBuild.Z / 2);
						}
					}
					CreateRectangularBedGridImage(displayVolumeToBuild, bedCenter, divisor, skip);
					printerBed = PlatonicSolids.CreateCube(displayVolumeToBuild.X, displayVolumeToBuild.Y, 1.8);
					{
						Face face = printerBed.Faces[0];
						MeshHelper.PlaceTextureOnFace(face, BedImage);
					}
					break;

				case BedShape.Circular:
					{
						if (displayVolumeToBuild.Z > 0)
						{
							buildVolume = VertexSourceToMesh.Extrude(new Ellipse(new Vector2(), displayVolumeToBuild.X / 2, displayVolumeToBuild.Y / 2), displayVolumeToBuild.Z);
							foreach (Vertex vertex in buildVolume.Vertices)
							{
								vertex.Position = vertex.Position + new Vector3(0, 0, .2);
							}
						}
						CreateCircularBedGridImage((int)(displayVolumeToBuild.X / divisor), (int)(displayVolumeToBuild.Y / divisor), skip);
						printerBed = VertexSourceToMesh.Extrude(new Ellipse(new Vector2(), displayVolumeToBuild.X / 2, displayVolumeToBuild.Y / 2), 2);
						{
							foreach (Face face in printerBed.Faces)
							{
								if (face.Normal.Z > 0)
								{
									face.SetTexture(0, BedImage);
									foreach (FaceEdge faceEdge in face.FaceEdges())
									{
										faceEdge.SetUv(0, new Vector2((displayVolumeToBuild.X / 2 + faceEdge.FirstVertex.Position.X) / displayVolumeToBuild.X,
											(displayVolumeToBuild.Y / 2 + faceEdge.FirstVertex.Position.Y) / displayVolumeToBuild.Y));
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

		public bool SuppressUiVolumes { get; set; } = false;

		public async Task LoadItemIntoScene(string itemPath, Vector2 bedCenter = new Vector2(), string itemName = null)
		{
			if (File.Exists(itemPath))
			{
				BeginProgressReporting("Loading Mesh");

				// TODO: How to we handle mesh load errors? How do we report success?
				IObject3D loadedItem = await Task.Run(() => Object3D.Load(itemPath, CancellationToken.None, progress: ReportProgress0to100));
				if (loadedItem != null)
				{
					if (itemName != null)
					{
						loadedItem.Name = itemName;
					}

					// SetMeshAfterLoad
					Scene.Children.Modify(children =>
					{
						if (loadedItem.Mesh != null)
						{
							// STLs currently load directly into the mesh rather than as a group like AMF
							children.Add(loadedItem);
						}
						else
						{
							children.AddRange(loadedItem.Children);
						}
					});

					CreateGlDataObject(loadedItem);
				}
				else
				{
					partProcessingInfo.centeredInfoText.Text = string.Format("Sorry! No 3D view available\nfor this file.");
				}

				EndProgressReporting();

				// Invoke LoadDone event
				LoadDone?.Invoke(this, null);
			}
			else
			{
				partProcessingInfo.centeredInfoText.Text = string.Format("{0}\n'{1}'", "File not found on disk.", Path.GetFileName(itemPath));
			}
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			base.OnDraw(graphics2D);

			this.SetGlContext();

			foreach (var object3D in Scene.Children)
			{
				DrawObject(object3D, false);
			}

			if (RenderBed)
			{
				GLHelper.Render(printerBed, this.BedColor);
			}

			if (buildVolume != null && RenderBuildVolume)
			{
				GLHelper.Render(buildVolume, this.BuildVolumeColor);
			}

			// we don't want to render the bed or build volume before we load a model.
			if (Scene.HasChildren() || AllowBedRenderingWhenEmpty)
			{
				if (false) // this is code to draw a small axis indicator
				{
					double big = 10;
					double small = 1;
					Mesh xAxis = PlatonicSolids.CreateCube(big, small, small);
					GLHelper.Render(xAxis, Color.Red);
					Mesh yAxis = PlatonicSolids.CreateCube(small, big, small);
					GLHelper.Render(yAxis, Color.Green);
					Mesh zAxis = PlatonicSolids.CreateCube(small, small, big);
					GLHelper.Render(zAxis, Color.Blue);
				}
			}

			DrawInteractionVolumes();

			//if (!SuppressUiVolumes)
			{
				foreach (InteractionVolume interactionVolume in interactionVolumes)
				{
					interactionVolume.Draw2DContent(graphics2D);
				}
			}

			this.UnsetGlContext();
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			base.OnMouseDown(mouseEvent);

			int volumeHitIndex;
			Ray ray = this.World.GetRayForLocalBounds(mouseEvent.Position);
			IntersectInfo info;
			if (this.Scene.SelectedItem != null
				&& !SuppressUiVolumes 
				&& FindInteractionVolumeHit(ray, out volumeHitIndex, out info))
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

			if (SuppressUiVolumes)
			{
				return;
			}

			Ray ray = this.World.GetRayForLocalBounds(mouseEvent.Position);
			IntersectInfo info = null;
			if (MouseDownOnInteractionVolume && volumeIndexWithMouseDown != -1)
			{
				MouseEvent3DArgs mouseEvent3D = new MouseEvent3DArgs(mouseEvent, ray, info);
				interactionVolumes[volumeIndexWithMouseDown].OnMouseMove(mouseEvent3D);
			}
			else
			{
				MouseEvent3DArgs mouseEvent3D = new MouseEvent3DArgs(mouseEvent, ray, info);

				int volumeHitIndex;
				FindInteractionVolumeHit(ray, out volumeHitIndex, out info);

				for (int i = 0; i < interactionVolumes.Count; i++)
				{
					if (i == volumeHitIndex)
					{
						interactionVolumes[i].MouseOver = true;
						interactionVolumes[i].MouseMoveInfo = info;

						HoveredInteractionVolume = interactionVolumes[i];
					}
					else
					{
						interactionVolumes[i].MouseOver = false;
						interactionVolumes[i].MouseMoveInfo = null;
					}

					interactionVolumes[i].OnMouseMove(mouseEvent3D);
				}
			}
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			Invalidate();

			if(SuppressUiVolumes)
			{
				return;
			}

			int volumeHitIndex;
			Ray ray = this.World.GetRayForLocalBounds(mouseEvent.Position);
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

		public void ResetView()
		{
			trackballTumbleWidget.ZeroVelocity();

			this.World.Reset();
			this.World.Scale = .03;
			this.World.Translate(-new Vector3(BedCenter));
			this.World.Rotate(Quaternion.FromEulerAngles(new Vector3(0, 0, MathHelper.Tau / 16)));
			this.World.Rotate(Quaternion.FromEulerAngles(new Vector3(-MathHelper.Tau * .19, 0, 0)));
		}

		private void CreateCircularBedGridImage(int linesInX, int linesInY, int increment = 1)
		{
			Vector2 bedImageCentimeters = new Vector2(linesInX, linesInY);
			BedImage = new ImageBuffer(1024, 1024);
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
					graphics2D.DrawString((count * increment).ToString(), linePos + 2, BedImage.Height / 2, pointSize, color: bedMarkingsColor);

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

		private void CreateRectangularBedGridImage(Vector3 displayVolumeToBuild, Vector2 bedCenter, double divisor, double skip)
		{
			lock (lastCreatedBedImage)
			{
				BedImage = new ImageBuffer(1024, 1024);
				Graphics2D graphics2D = BedImage.NewGraphics2D();
				graphics2D.Clear(bedBaseColor);
				{
					double lineDist = BedImage.Width / (displayVolumeToBuild.X / divisor);

					double xPositionCm = (-(displayVolume.X / 2.0) + bedCenter.X) / divisor;
					int xPositionCmInt = (int)Math.Round(xPositionCm);
					double fraction = xPositionCm - xPositionCmInt;
					int pointSize = 20;
					graphics2D.DrawString((xPositionCmInt * skip).ToString(), 4, 4, pointSize, color: bedMarkingsColor);
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
						graphics2D.DrawString((xPositionCmInt * skip).ToString(), linePos + 4, 4, pointSize, color: bedMarkingsColor);
					}
				}
				{
					double lineDist = BedImage.Height / (displayVolumeToBuild.Y / divisor);

					double yPositionCm = (-(displayVolume.Y / 2.0) + bedCenter.Y) / divisor;
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

						graphics2D.DrawString((yPositionCmInt * skip).ToString(), 4, linePos + 4, pointSize, color: bedMarkingsColor);
					}
				}

				lastCreatedBedImage = BedImage;
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

			List<IPrimitive> uiTraceables = new List<IPrimitive>();
			foreach (InteractionVolume interactionVolume in interactionVolumes)
			{
				if (interactionVolume.CollisionVolume != null)
				{
					IPrimitive traceData = interactionVolume.CollisionVolume;
					uiTraceables.Add(new Transform(traceData, interactionVolume.TotalTransform));
				}
			}
			IPrimitive allUiObjects = BoundingVolumeHierarchy.CreateNewHierachy(uiTraceables);

			info = allUiObjects.GetClosestIntersection(ray);
			if (info != null)
			{
				for (int i = 0; i < interactionVolumes.Count; i++)
				{
					List<IBvhItem> insideBounds = new List<IBvhItem>();
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

		private string progressReportingPrimaryTask = "";

		public void BeginProgressReporting(string taskDescription)
		{
			progressReportingPrimaryTask = taskDescription;

			partProcessingInfo.Visible = true;
			partProcessingInfo.progressControl.PercentComplete = 0;
			partProcessingInfo.centeredInfoText.Text = taskDescription + "...";
		}

		public void EndProgressReporting()
		{
			progressReportingPrimaryTask = "";
			partProcessingInfo.Visible = false;
		}

		public void ReportProgress0to100(double progress0To1, string processingState)
		{
			UiThread.RunOnIdle(() =>
			{
				int percentComplete = (int)(progress0To1 * 100);
				partProcessingInfo.centeredInfoText.Text =  "{0} {1}%...".FormatWith(progressReportingPrimaryTask, percentComplete);
				partProcessingInfo.progressControl.PercentComplete = percentComplete;

				// Only assign to textbox if value passed through
				if (processingState != null)
				{
					partProcessingInfo.centeredInfoDescription.Text = processingState;
				}
			});
		}

		private void DrawObject(IObject3D object3D, bool parentSelected)
		{
			foreach(var item in object3D.VisibleMeshes())
			{
				bool isSelected = parentSelected ||
					Scene.SelectedItem != null && (object3D == Scene.SelectedItem || Scene.SelectedItem.Children.Contains(object3D));

				GLHelper.Render(item.Mesh, item.WorldColor(), item.WorldMatrix(), RenderType);
			}
		}

		private void DrawInteractionVolumes()
		{
			if(SuppressUiVolumes)
			{
				return;
			}

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

		private void SetGlContext()
		{
			GL.ClearDepth(1.0);
			GL.Clear(ClearBufferMask.DepthBufferBit);   // Clear the Depth Buffer

			GL.PushAttrib(AttribMask.ViewportBit);
			RectangleDouble screenRect = this.TransformToScreenSpace(LocalBounds);
			GL.Viewport((int)screenRect.Left, (int)screenRect.Bottom, (int)screenRect.Width, (int)screenRect.Height);

			GL.ShadeModel(ShadingModel.Smooth);

			GL.FrontFace(FrontFaceDirection.Ccw);
			GL.CullFace(CullFaceMode.Back);

			GL.DepthFunc(DepthFunction.Lequal);

			GL.Disable(EnableCap.DepthTest);
			//ClearToGradient();

#if DO_LIGHTING
			GL.Light(LightName.Light0, LightParameter.Ambient, ambientLight);

			GL.Light(LightName.Light0, LightParameter.Diffuse, diffuseLight0);
			GL.Light(LightName.Light0, LightParameter.Specular, specularLight0);

			GL.Light(LightName.Light0, LightParameter.Ambient, new float[] { 0, 0, 0, 0 });
			GL.Light(LightName.Light1, LightParameter.Diffuse, diffuseLight1);
			GL.Light(LightName.Light1, LightParameter.Specular, specularLight1);

			GL.ColorMaterial(MaterialFace.FrontAndBack, ColorMaterialParameter.AmbientAndDiffuse);

			GL.Enable(EnableCap.Light0);
			GL.Enable(EnableCap.Light1);
			GL.Enable(EnableCap.DepthTest);
			GL.Enable(EnableCap.Blend);
			GL.Enable(EnableCap.Normalize);
			GL.Enable(EnableCap.Lighting);
			GL.Enable(EnableCap.ColorMaterial);

			Vector3 lightDirectionVector = new Vector3(lightDirection0[0], lightDirection0[1], lightDirection0[2]);
			lightDirectionVector.Normalize();
			lightDirection0[0] = (float)lightDirectionVector.X;
			lightDirection0[1] = (float)lightDirectionVector.Y;
			lightDirection0[2] = (float)lightDirectionVector.Z;
			GL.Light(LightName.Light0, LightParameter.Position, lightDirection0);
			GL.Light(LightName.Light1, LightParameter.Position, lightDirection1);
#endif

			// set the projection matrix
			GL.MatrixMode(MatrixMode.Projection);
			GL.PushMatrix();
			GL.LoadMatrix(this.World.ProjectionMatrix.GetAsDoubleArray());

			// set the modelview matrix
			GL.MatrixMode(MatrixMode.Modelview);
			GL.PushMatrix();
			GL.LoadMatrix(this.World.ModelviewMatrix.GetAsDoubleArray());
		}

		private void UnsetGlContext()
		{
			GL.MatrixMode(MatrixMode.Projection);
			GL.PopMatrix();

			GL.MatrixMode(MatrixMode.Modelview);
			GL.PopMatrix();

#if DO_LIGHTING
			GL.Disable(EnableCap.ColorMaterial);
			GL.Disable(EnableCap.Lighting);
			GL.Disable(EnableCap.Light0);
			GL.Disable(EnableCap.Light1);
#endif
			GL.Disable(EnableCap.Normalize);
			GL.Disable(EnableCap.Blend);
			GL.Disable(EnableCap.DepthTest);

			GL.PopAttrib();
		}

		public class PartProcessingInfo : FlowLayoutWidget
		{
			internal TextWidget centeredInfoDescription;
			internal TextWidget centeredInfoText;
			internal ProgressControl progressControl;

			internal PartProcessingInfo(string startingTextMessage)
				: base(FlowDirection.TopToBottom)
			{
				progressControl = new ProgressControl("", Color.Black, Color.Black);
				progressControl.HAnchor = HAnchor.Center;
				AddChild(progressControl);
				progressControl.Visible = false;
				//progressControl.ProgressChanged += (sender, e) =>
				//{
				//	progressControl.Visible = true;
				//};

				centeredInfoText = new TextWidget(startingTextMessage);
				centeredInfoText.HAnchor = HAnchor.Center;
				centeredInfoText.AutoExpandBoundsToText = true;
				AddChild(centeredInfoText);

				centeredInfoDescription = new TextWidget("");
				centeredInfoDescription.HAnchor = HAnchor.Center;
				centeredInfoDescription.AutoExpandBoundsToText = true;
				AddChild(centeredInfoDescription);

				VAnchor |= VAnchor.Center;
				HAnchor |= HAnchor.Center;
			}
		}
	}
}