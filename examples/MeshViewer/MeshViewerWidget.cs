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
using System.Diagnostics;
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

        public enum RenderTypes { Shaded, Outlines, Polygons };
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

        double partScale;
        ImageBuffer bedCentimeterGridImage;
        TextWidget meshLoadingStateInfoText;
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

        public Matrix4X4 SelectedMeshTransform 
        {
            get 
            {
                if (MeshTransforms.Count > 0)
                {
                    return MeshTransforms[selectedMeshIndex];
                }

                return Matrix4X4.Identity;
            }

            set
            {
                MeshTransforms[selectedMeshIndex] = value;
            }
        }

        List<Matrix4X4> meshTransforms = new List<Matrix4X4>();
        public List<Matrix4X4> MeshTransforms { get { return meshTransforms; } }

        List<Mesh> meshesToRender = new List<Mesh>();
        public List<Mesh> Meshes { get { return meshesToRender; } }

        public event EventHandler LoadDone;

        Mesh printerBed = null;
        Mesh buildVolume = null;

        public enum BedShape { Rectangular, Circular };
        BedShape bedShape = BedShape.Rectangular;

        public MeshViewerWidget(Vector3 displayVolume, double scale, BedShape bedShape)
        {
            this.bedShape = bedShape;
            this.displayVolume = displayVolume;
            RenderType = RenderTypes.Shaded;
            RenderBed = true;
            RenderBuildVolume = false;
            PartColor = RGBA_Bytes.White;
            SelectedPartColor = RGBA_Bytes.White;
            BedColor = new RGBA_Floats(.8, .8, .8, .5).GetAsRGBA_Bytes();
            BuildVolumeColor = new RGBA_Floats(.2, .8, .3, .2).GetAsRGBA_Bytes();

            this.partScale = scale;
            trackballTumbleWidget = new TrackballTumbleWidget();
            trackballTumbleWidget.DrawRotationHelperCircle = false;
            trackballTumbleWidget.DrawGlContent += trackballTumbleWidget_DrawGlContent;

            AddChild(trackballTumbleWidget);

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
                    printerBed = PlatonicSolids.CreateCube(displayVolume.x, displayVolume.y, 2);
                    {
                        Face face = printerBed.Faces[0];
                        {
                            FaceData faceData = new FaceData();
                            faceData.Textures.Add(bedCentimeterGridImage);
                            face.Data = faceData;
                            foreach (FaceEdge faceEdge in face.FaceEdgeIterator())
                            {
                                FaceEdgeData edgeUV = new FaceEdgeData();
                                edgeUV.TextureUV.Add(new Vector2((displayVolume.x / 2 + faceEdge.vertex.Position.x) / displayVolume.x,
                                    (displayVolume.y / 2 + faceEdge.vertex.Position.y) / displayVolume.y));
                                faceEdge.Data = edgeUV;
                            }
                        }
                    }
                    foreach (Vertex vertex in printerBed.Vertices)
                    {
                        vertex.Position = vertex.Position - new Vector3(0, 0, 1.2);
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
                                    FaceData faceData = new FaceData();
                                    faceData.Textures.Add(bedCentimeterGridImage);
                                    face.Data = faceData;
                                    foreach (FaceEdge faceEdge in face.FaceEdgeIterator())
                                    {
                                        FaceEdgeData edgeUV = new FaceEdgeData();
                                        edgeUV.TextureUV.Add(new Vector2((displayVolume.x / 2 + faceEdge.vertex.Position.x) / displayVolume.x,
                                            (displayVolume.y / 2 + faceEdge.vertex.Position.y) / displayVolume.y));
                                        faceEdge.Data = edgeUV;
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

            trackballTumbleWidget.AnchorAll();
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

                switch(RenderType)
                {
                    case RenderTypes.Shaded:
                        RenderMeshToGl.Render(meshToRender, drawColor, MeshTransforms[i]);
                        break;

                    case RenderTypes.Polygons:
                        RenderMeshToGl.Render(meshToRender, drawColor, MeshTransforms[i], true);
                        break;

                    case RenderTypes.Outlines:
                        RenderMeshToGl.Render(meshToRender, drawColor, MeshTransforms[i]);
                        break;
                }
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
                backgroundWorker = new BackgroundWorker();
                backgroundWorker.WorkerReportsProgress = true;
                backgroundWorker.WorkerSupportsCancellation = true;

                backgroundWorker.ProgressChanged += new ProgressChangedEventHandler(backgroundWorker_ProgressChanged);
                backgroundWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(backgroundWorker_RunWorkerCompleted);

                bool loadingMeshFile = false;
                switch (Path.GetExtension(meshPathAndFileName).ToUpper())
                {
                    case ".STL":
                        {
                            StlProcessing.LoadInBackground(backgroundWorker, meshPathAndFileName);
                            loadingMeshFile = true;
                        }
                        break;

                    case ".AMF":
                        {
                            AmfProcessing amfLoader = new AmfProcessing();
                            amfLoader.LoadInBackground(backgroundWorker, meshPathAndFileName);
                            loadingMeshFile = true;
                        }
                        break;

                    default:
                        loadingMeshFile = false;
                        break;
                }

                if (loadingMeshFile)
                {
                    meshLoadingStateInfoText = new TextWidget("Loading Mesh...");
                    meshLoadingStateInfoText.HAnchor = HAnchor.ParentCenter;
                    meshLoadingStateInfoText.VAnchor = VAnchor.ParentCenter;
                    meshLoadingStateInfoText.AutoExpandBoundsToText = true;

                    GuiWidget labelContainer = new GuiWidget();
                    labelContainer.AnchorAll();
                    labelContainer.AddChild(meshLoadingStateInfoText);
                    labelContainer.Selectable = false;

                    this.AddChild(labelContainer);
                }
                else
                {
                    TextWidget no3DView = new TextWidget(string.Format("Sorry! No 3D view available for this file type '{0}'.", Path.GetExtension(meshPathAndFileName).ToUpper()));
                    no3DView.Margin = new BorderDouble(0, 0, 0, 0);
                    no3DView.VAnchor = Agg.UI.VAnchor.ParentCenter;
                    no3DView.HAnchor = Agg.UI.HAnchor.ParentCenter;
                    this.AddChild(no3DView);
                }
            }
            else
            {
                string startingMessage = string.Format("{0}\n'{1}'", "File not found on disk.", Path.GetFileName(meshPathAndFileName));
                TextWidget no3DView = new TextWidget(startingMessage);
                no3DView.Margin = new BorderDouble(0, 0, 0, 0);
                no3DView.VAnchor = Agg.UI.VAnchor.ParentCenter;
                no3DView.HAnchor = Agg.UI.HAnchor.ParentCenter;
                this.AddChild(no3DView);
            }
        }

        public void SetMeshAfterLoad(Mesh loadedMesh)
        {
            Meshes.Clear();

            if (loadedMesh == null)
            {
                TextWidget no3DView = new TextWidget(string.Format("Sorry! No 3D view available for this file."));
                no3DView.Margin = new BorderDouble(0, 0, 0, 0);
                no3DView.VAnchor = Agg.UI.VAnchor.ParentCenter;
                no3DView.HAnchor = Agg.UI.HAnchor.ParentCenter;
                this.AddChild(no3DView);
            }
            else
            {
                Meshes.Add(loadedMesh);
                meshTransforms.Add(Matrix4X4.Identity);

                trackballTumbleWidget.TrackBallController = new TrackBallController();
                trackballTumbleWidget.OnBoundsChanged(null);
                trackballTumbleWidget.TrackBallController.Scale = .03;
                trackballTumbleWidget.TrackBallController.Rotate(Quaternion.FromEulerAngles(new Vector3(0, 0, MathHelper.Tau / 16)));
                trackballTumbleWidget.TrackBallController.Rotate(Quaternion.FromEulerAngles(new Vector3(-MathHelper.Tau * .19, 0, 0)));
                if (partScale != 1)
                {
                    foreach (Vertex vertex in Meshes[0].Vertices)
                    {
                        vertex.Position = vertex.Position * partScale;
                    }
                }

                // make sure the mesh is centered and on the bed
                {
                    int index = Meshes.Count - 1;
                    AxisAlignedBoundingBox bounds = loadedMesh.GetAxisAlignedBoundingBox(meshTransforms[index]);
                    Vector3 boundsCenter = (bounds.maxXYZ + bounds.minXYZ) / 2;
                    meshTransforms[index] *= Matrix4X4.CreateTranslation(-boundsCenter + new Vector3(0, 0, bounds.ZSize / 2));
                }
            }
        }

        void backgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            SetMeshAfterLoad((Mesh)e.Result);
            meshLoadingStateInfoText.Text = "";

            if (LoadDone != null)
            {
                LoadDone(this, null);
            }
        }

        void backgroundWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            meshLoadingStateInfoText.Text = string.Format("Loading Mesh {0}%...", e.ProgressPercentage);
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

        void CreateRectangularBedGridImage(int linesInX, int linesInY)
        {
            Vector2 bedImageCentimeters = new Vector2(linesInX, linesInY);
            bedCentimeterGridImage = new ImageBuffer(1024, 1024, 32, new BlenderBGRA());
            Graphics2D graphics2D = bedCentimeterGridImage.NewGraphics2D();
            graphics2D.Clear(RGBA_Bytes.White);
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
        }

        void CreateCircularBedGridImage(int linesInX, int linesInY)
        {
            Vector2 bedImageCentimeters = new Vector2(linesInX, linesInY);
            bedCentimeterGridImage = new ImageBuffer(1024, 1024, 32, new BlenderBGRA());
            Graphics2D graphics2D = bedCentimeterGridImage.NewGraphics2D();
            graphics2D.Clear(RGBA_Bytes.White);
#if true
            {
                double lineDist = bedCentimeterGridImage.Width / (double)linesInX;

                int count = 1;
                int pointSize = 20;
                graphics2D.DrawString(count.ToString(), 0, 0, pointSize);
                double currentRadius = lineDist;
                Vector2 bedCenter = new Vector2(bedCentimeterGridImage.Width / 2, bedCentimeterGridImage.Height / 2);
                for (double linePos = lineDist + bedCentimeterGridImage.Width / 2; linePos < bedCentimeterGridImage.Width; linePos += lineDist)
                {
                    int linePosInt = (int)linePos;
                    graphics2D.DrawString(count.ToString(), linePos + 2, bedCentimeterGridImage.Height / 2, pointSize);

                    Ellipse circle = new Ellipse(bedCenter, currentRadius);
                    Stroke outline = new Stroke(circle);
                    graphics2D.Render(outline, RGBA_Bytes.Black);
                    currentRadius += lineDist;
                    count++;
                }

                graphics2D.Line(0, bedCentimeterGridImage.Height / 2, bedCentimeterGridImage.Width, bedCentimeterGridImage.Height / 2, RGBA_Bytes.Black);
                graphics2D.Line(bedCentimeterGridImage.Width / 2, 0, bedCentimeterGridImage.Width/2, bedCentimeterGridImage.Height, RGBA_Bytes.Black);
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

