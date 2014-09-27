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
using System.ComponentModel;
using System.IO;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.OpenGlGui;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.PolygonMesh;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.RenderOpenGl;
using MatterHackers.VectorMath;

namespace MatterHackers.MeshVisualizer
{
    public class MeshViewerWidget : GuiWidget
    {
        BackgroundWorker backgroundWorker = null;

        public RGBA_Bytes PartColor { get; set; }
        public RGBA_Bytes SelectedPartColor { get; set; }
        public RGBA_Bytes BedColor { get; set; }
        public RGBA_Bytes BuildVolumeColor { get; set; }

        Vector3 displayVolume;
        public Vector3 DisplayVolume { get { return displayVolume; } }

        public bool AlwaysRenderBed { get; set; }
        public bool RenderBed { get; set; }
        public bool RenderBuildVolume { get; set; }

        RenderTypes renderType = RenderTypes.Shaded;
        public RenderTypes RenderType
        {
            get { return renderType; }
            set
            {
                if (renderType != value)
                {
                    renderType = value;
                    foreach (Mesh mesh in Meshes)
                    {
                        mesh.MarkAsChanged();
                    }
                }
            }
        }

        public ImageBuffer BedImage;

        // need to know about (or just rebuild)
        
        // Bed Center Change
        // Bed Size Change
        // Part Centering
        // Bed Rectange on Image
        // Bed Image Change
        // Bed Shape Change

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

        TrackballTumbleWidget trackballTumbleWidget;
        public TrackballTumbleWidget TrackballTumbleWidget
        {
            get
            {
                return trackballTumbleWidget;
            }
        }

        int selectedMeshIndex = 0;
        public int SelectedMeshIndex
        {
            get { return selectedMeshIndex; }
            set { selectedMeshIndex = value; }
        }

        public Mesh SelectedMesh
        {
            get 
            {
                if (Meshes.Count > 0)
                {
                    return Meshes[selectedMeshIndex];
                }

                return null;
            }
        }

        public ScaleRotateTranslate SelectedMeshTransform 
        {
            get 
            {
                if (MeshTransforms.Count > 0)
                {
                    return MeshTransforms[selectedMeshIndex];
                }

                return ScaleRotateTranslate.Identity();
            }

            set
            {
                MeshTransforms[selectedMeshIndex] = value;
            }
        }

        List<ScaleRotateTranslate> meshTransforms = new List<ScaleRotateTranslate>();
        public List<ScaleRotateTranslate> MeshTransforms { get { return meshTransforms; } }

        List<Mesh> meshesToRender = new List<Mesh>();
        public List<Mesh> Meshes { get { return meshesToRender; } }

        public event EventHandler LoadDone;

        Mesh printerBed = null;
        public Mesh PrinterBed { get { return printerBed; } }
        Mesh buildVolume = null;

        public enum BedShape { Rectangular, Circular };
        BedShape bedShape = BedShape.Rectangular;
        Vector2 bedCenter;

        public MeshViewerWidget(Vector3 displayVolume, Vector2 bedCenter, BedShape bedShape, string startingTextMessage = "")
        {
            RenderType = RenderTypes.Shaded;
            RenderBed = true;
            RenderBuildVolume = false;
            PartColor = RGBA_Bytes.White;
            SelectedPartColor = RGBA_Bytes.White;
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
            if(this.bedCenter == bedCenter 
                && this.bedShape == bedShape
                && this.displayVolume == displayVolume)
            {
                return;
            }

            this.bedCenter = bedCenter;
            this.bedShape = bedShape;
            displayVolume = Vector3.ComponentMax(displayVolume, new Vector3(1,1,1));
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

        void trackballTumbleWidget_DrawGlContent(object sender, EventArgs e)
        {
            for (int i = 0; i < Meshes.Count; i++)
            {
                Mesh meshToRender = Meshes[i];
                RGBA_Bytes drawColor = PartColor;
                if (meshToRender == SelectedMesh)
                {
                    drawColor = SelectedPartColor;
                }

                RenderMeshToGl.Render(meshToRender, drawColor, MeshTransforms[i].TotalTransform, RenderType);
            }

            // we don't want to render the bed or bulid volume before we load a model.
            if (Meshes.Count > 0 || AlwaysRenderBed)
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

        public void LoadMesh(string meshPathAndFileName)
        {
            if (File.Exists(meshPathAndFileName))
            {
                partProcessingInfo.Visible = true;
                partProcessingInfo.progressControl.PercentComplete = 0;

                backgroundWorker = new BackgroundWorker();
                backgroundWorker.WorkerSupportsCancellation = true;

                backgroundWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(backgroundWorker_RunWorkerCompleted);

                bool loadingMeshFile = false;
                switch (Path.GetExtension(meshPathAndFileName).ToUpper())
                {
                    case ".STL":
                        {
                            backgroundWorker.DoWork += (object sender, DoWorkEventArgs e) =>
                            {
                                Mesh loadedMesh = StlProcessing.Load(meshPathAndFileName, backgroundWorker_ProgressChanged);
                                SetMeshAfterLoad(loadedMesh);
                                e.Result = loadedMesh;
                            };
                            backgroundWorker.RunWorkerAsync();
                            loadingMeshFile = true;
                        }
                        break;

                    case ".AMF":
                        {
                            backgroundWorker.DoWork += (object sender, DoWorkEventArgs e) =>
                            {
                                Mesh loadedMesh = AmfProcessing.Load(meshPathAndFileName, backgroundWorker_ProgressChanged);
                                SetMeshAfterLoad(loadedMesh);
                                e.Result = loadedMesh;
                            };
                            backgroundWorker.RunWorkerAsync();
                            loadingMeshFile = true;
                        }
                        break;

                    default:
                        loadingMeshFile = false;
                        break;
                }

                if (loadingMeshFile)
                {
                    partProcessingInfo.centeredInfoText.Text = "Loading Mesh...";
                }
                else
                {
                    partProcessingInfo.centeredInfoText.Text = string.Format("Sorry! No 3D view available\nfor this file type '{0}'.", Path.GetExtension(meshPathAndFileName).ToUpper());
                }
            }
            else
            {
                partProcessingInfo.centeredInfoText.Text = string.Format("{0}\n'{1}'", "File not found on disk.", Path.GetFileName(meshPathAndFileName));
            }
        }

        public void SetMeshAfterLoad(Mesh loadedMesh)
        {
            Meshes.Clear();

            if (loadedMesh == null)
            {
                partProcessingInfo.centeredInfoText.Text = string.Format("Sorry! No 3D view available\nfor this file.");
            }
            else
            {
                meshTransforms.Add(ScaleRotateTranslate.Identity());

                int index = meshTransforms.Count - 1;
                // get the ScaleRotateTranslate matrices set up
                {
                    AxisAlignedBoundingBox bounds = loadedMesh.GetAxisAlignedBoundingBox(meshTransforms[index].TotalTransform);
                    Vector3 boundsCenter = (bounds.maxXYZ + bounds.minXYZ) / 2;
                    loadedMesh.Translate(-boundsCenter);
                }

                // make sure the mesh is centered and on the bed
                {
                    AxisAlignedBoundingBox bounds = loadedMesh.GetAxisAlignedBoundingBox(meshTransforms[index].TotalTransform);
                    Vector3 boundsCenter = (bounds.maxXYZ + bounds.minXYZ) / 2;
                    ScaleRotateTranslate moved = meshTransforms[index];
                    moved.translation *= Matrix4X4.CreateTranslation(-boundsCenter + new Vector3(0, 0, bounds.ZSize / 2));
                    meshTransforms[index] = moved;
                }

                Meshes.Add(loadedMesh);

                trackballTumbleWidget.TrackBallController = new TrackBallController();
                trackballTumbleWidget.OnBoundsChanged(null);
                trackballTumbleWidget.TrackBallController.Scale = .03;
                trackballTumbleWidget.TrackBallController.Rotate(Quaternion.FromEulerAngles(new Vector3(0, 0, MathHelper.Tau / 16)));
                trackballTumbleWidget.TrackBallController.Rotate(Quaternion.FromEulerAngles(new Vector3(-MathHelper.Tau * .19, 0, 0)));
            }
        }

        void backgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            partProcessingInfo.Visible = false;

            if (LoadDone != null)
            {
                LoadDone(this, null);
            }
        }

        bool backgroundWorker_ProgressChanged(double progress0To1, string processingState)
        {
            UiThread.RunOnIdle((object state) =>
            {
                int percentComplete = (int)(progress0To1 * 100 + .5);
                partProcessingInfo.centeredInfoText.Text = "Loading Mesh {0}%...".FormatWith(percentComplete);
                partProcessingInfo.progressControl.PercentComplete = percentComplete;
                partProcessingInfo.centeredInfoDescription.Text = processingState;
            });
            return true;
        }

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
        }

        public override void OnMouseUp(MouseEventArgs mouseEvent)
        {
            trackballTumbleWidget.DrawRotationHelperCircle = false;
            Invalidate();

            base.OnMouseUp(mouseEvent);
        }

        RGBA_Bytes bedMarkingsColor = RGBA_Bytes.Black;
        RGBA_Bytes bedBaseColor = RGBA_Bytes.White;
        void CreateRectangularBedGridImage(int linesInX, int linesInY)
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

        void CreateCircularBedGridImage(int linesInX, int linesInY)
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
    }
}

