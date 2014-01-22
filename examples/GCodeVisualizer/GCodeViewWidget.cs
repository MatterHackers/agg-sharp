using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Diagnostics;

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.RasterizerScanline;

namespace MatterHackers.GCodeVisualizer
{
    public class GCodeViewWidget : GuiWidget
    {
        public EventHandler DoneLoading;

        public ProgressChangedEventHandler LoadingProgressChanged;

        bool renderGrid = true;
        public bool RenderGrid 
        {
            get { return renderGrid; }
            set
            {
                if (renderGrid != value)
                {
                    renderGrid = value;
                    Invalidate();
                }
            }
        }

        public enum ETransformState { Move, Scale };

        public ETransformState TransformState { get; set; }

        bool renderMoves = true;
        public bool RenderMoves
        {
            get { return renderMoves; }
            set
            {
                if (renderMoves != value)
                {
                    renderMoves = value;
                    Invalidate();
                }
            }
        }

        BackgroundWorker backgroundWorker = null;
        Vector2 lastMousePosition = new Vector2(0, 0);
        Vector2 mouseDownPosition = new Vector2(0, 0);

        double layerScale = 1;
        int activeLayerIndex;
        Vector2 gridSizeMm;
        Vector2 gridCenterMm;
        Affine ScallingTransform
        {
            get
            {
                return Affine.NewScaling(layerScale, layerScale);
            }
        }

        Affine TotalTransform
        {
            get
            {
                Affine transform = Affine.NewIdentity();
                transform *= Affine.NewTranslation(unscaledRenderOffset);

                // scale to view 
                transform *= ScallingTransform;
                transform *= Affine.NewTranslation(Width / 2, Height / 2);

                return transform;
            }
        }

        Vector2 unscaledRenderOffset = new Vector2(0, 0);

        public string FileNameAndPath;
        public GCodeFile loadedGCode;
        public GCodeVertexSource gCodeView;

        public event EventHandler ActiveLayerChanged;

        public GCodeFile LoadedGCode
        {
            get
            {
                return loadedGCode;
            }
        }

        public int ActiveLayerIndex
        {
            get
            {
                return activeLayerIndex;
            }

            set
            {
                if (activeLayerIndex != value)
                {
                    activeLayerIndex = value;

                    if (gCodeView == null || activeLayerIndex < 0)
                    {
                        activeLayerIndex = 0;
                    }
                    else if (activeLayerIndex >= gCodeView.NumLayers)
                    {
                        activeLayerIndex = gCodeView.NumLayers - 1;
                    }
                    Invalidate();

                    if (ActiveLayerChanged != null)
                    {
                        ActiveLayerChanged(this, null);
                    }
                }
            }
        }

        public GCodeViewWidget(Vector2 gridSizeMm, Vector2 gridCenterMm)
        {
            this.gridSizeMm = gridSizeMm;
            this.gridCenterMm = gridCenterMm;
            LocalBounds = new RectangleDouble(0, 0, 100, 100);
            DoubleBuffer = true;
            AnchorAll();
        }

        public void SetGCodeAfterLoad(GCodeFile loadedGCode)
        {
            this.loadedGCode = loadedGCode;
            if (loadedGCode == null)
            {
                TextWidget noGCodeLoaded = new TextWidget(string.Format("Not a valid GCode file."));
                noGCodeLoaded.Margin = new BorderDouble(0, 0, 0, 0);
                noGCodeLoaded.VAnchor = Agg.UI.VAnchor.ParentCenter;
                noGCodeLoaded.HAnchor = Agg.UI.HAnchor.ParentCenter;
                this.AddChild(noGCodeLoaded);
            }
            else
            {
                activeLayerIndex = 0;
                CenterPartInView();
            }
        }

        void backgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            SetGCodeAfterLoad((GCodeFile)e.Result);

            if(DoneLoading != null)
            {
                DoneLoading(this, null);
            }
        }

        void backgroundWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            if (LoadingProgressChanged != null)
            {
                LoadingProgressChanged(this, e);
            }
        }

        PathStorage grid = new PathStorage();
        public override void OnDraw(Graphics2D graphics2D)
        {
            if (loadedGCode != null)
            {
                Affine transform = TotalTransform;

                double extrusionLineWidths = 0.2 * layerScale;
                double movementLineWidth = 0.35 * layerScale;
                RGBA_Bytes movementColor = new RGBA_Bytes(10, 190, 15);
                CreateGrid(transform);

                VertexSourceApplyTransform transformedPathStorage = new VertexSourceApplyTransform(gCodeView, transform);
                if (false)//graphics2D.DestImage != null)
                {
#if false
                    LineProfileAnitAlias lineProfile = new LineProfileAnitAlias(extrusionLineWidths, new gamma_none());
                    OutlineRenderer outlineRenderer = new OutlineRenderer(graphics2D.DestImage, lineProfile);
                    rasterizer_outline_aa rasterizer = new rasterizer_outline_aa(outlineRenderer);

                    rasterizer.RenderAllPaths(grid, new RGBA_Bytes[] { RGBA_Bytes.LightGray }, new int[] { 0 }, 1);

                    rasterizer.line_join(rasterizer_outline_aa.outline_aa_join_e.outline_miter_accurate_join);
                    rasterizer.round_cap(true);

                    gCodeView.WhatToRender = GCodeVertexSource.RenderType.RenderExtrusions;
                    {
                        RGBA_Bytes[] colors = new RGBA_Bytes[] { RGBA_Bytes.Black };
                        int[] pathIndex = new int[] { activeLayerIndex };
                        rasterizer.RenderAllPaths(transformedPathStorage, colors, pathIndex, 1);
                    }

                    lineProfile.width(movementLineWidth);
                    gCodeView.WhatToRender = GCodeVertexSource.RenderType.RenderMoves;
                    {
                        RGBA_Bytes[] colors = new RGBA_Bytes[] { movementColor };
                        int[] pathIndex = new int[] { activeLayerIndex };
                        rasterizer.RenderAllPaths(transformedPathStorage, colors, pathIndex, 1);
                    }

                    graphics2D.DestImage.MarkImageChanged();
#endif
                }
                else
                {
                    Stroke stroke = new Stroke(grid, extrusionLineWidths);

                    stroke.line_cap(LineCap.Round);
                    stroke.line_join(LineJoin.Round);

                    if (RenderGrid)
                    {
                        graphics2D.Render(stroke, RGBA_Bytes.LightGray);
                    }

                    stroke.VertexSource = transformedPathStorage;

                    // This code renders the layer:
                    gCodeView.WhatToRender = GCodeVertexSource.RenderType.RenderExtrusions;
                    graphics2D.Render(stroke, activeLayerIndex, RGBA_Bytes.Black);

                    if(RenderMoves)
                    {
                        stroke.width(movementLineWidth);
                        gCodeView.WhatToRender = GCodeVertexSource.RenderType.RenderMoves;
                        graphics2D.Render(stroke, activeLayerIndex, movementColor);
                    }
                }
            }

            base.OnDraw(graphics2D);
        }

        public void CreateGrid(Affine transform)
        {
            Vector2 gridOffset = gridCenterMm - gridSizeMm / 2;
            if (gridSizeMm.x > 0 && gridSizeMm.y > 0)
            {
                grid.remove_all();
                for (int y = 0; y <= gridSizeMm.y; y += 10)
                {
                    Vector2 start = new Vector2(0, y) + gridOffset;
                    Vector2 end = new Vector2(gridSizeMm.x, y) + gridOffset;
                    transform.transform(ref start);
                    transform.transform(ref end);
                    grid.MoveTo((int)(start.x + .5), (int)(start.y + .5) + .5);
                    grid.LineTo((int)(int)(end.x + .5), (int)(end.y + .5) + .5);
                }

                for (int x = 0; x <= gridSizeMm.x; x += 10)
                {
                    Vector2 start = new Vector2(x, 0) + gridOffset;
                    Vector2 end = new Vector2(x, gridSizeMm.y) + gridOffset;
                    transform.transform(ref start);
                    transform.transform(ref end);
                    grid.MoveTo((int)(start.x + .5) + .5, (int)(start.y + .5));
                    grid.LineTo((int)(end.x + .5) + .5, (int)(end.y + .5));
                }
            }
        }

        public override void OnMouseDown(MouseEventArgs mouseEvent)
        {
            base.OnMouseDown(mouseEvent);
            if (MouseCaptured)
            {
                mouseDownPosition.x = mouseEvent.X;
                mouseDownPosition.y = mouseEvent.Y;

                lastMousePosition = mouseDownPosition;
            }
        }

        public override void OnMouseWheel(MouseEventArgs mouseEvent)
        {
            base.OnMouseWheel(mouseEvent);
            if (FirstWidgetUnderMouse) // TODO: find a good way to decide if you are what the wheel is trying to do
            {
                Vector2 mousePreScale = new Vector2(mouseEvent.X, mouseEvent.Y);
                TotalTransform.inverse_transform(ref mousePreScale);

                const double deltaFor1Click = 120;
                layerScale = layerScale + layerScale * (mouseEvent.WheelDelta / deltaFor1Click) * .1;

                Vector2 mousePostScale = new Vector2(mouseEvent.X, mouseEvent.Y);
                TotalTransform.inverse_transform(ref mousePostScale);

                unscaledRenderOffset += (mousePostScale - mousePreScale);

                Invalidate();
            }
        }

        public override void OnMouseMove(MouseEventArgs mouseEvent)
        {
            base.OnMouseMove(mouseEvent);
            Vector2 mousePos = new Vector2(mouseEvent.X, mouseEvent.Y);
            if (MouseCaptured)
            {
                Vector2 mouseDelta = mousePos - lastMousePosition;
                switch (TransformState)
                {
                    case ETransformState.Move:
                        ScallingTransform.inverse_transform(ref mouseDelta);

                        unscaledRenderOffset += mouseDelta;
                        break;

                    case ETransformState.Scale:
                        double zoomDelta = 1;
                        if (mouseDelta.y < 0)
                        {
                            zoomDelta = 1 - (-1 * mouseDelta.y / 100);
                        }
                        else if(mouseDelta.y > 0)
                        {
                            zoomDelta = 1 + (1 * mouseDelta.y / 100);
                        }

                        Vector2 mousePreScale = mouseDownPosition;
                        TotalTransform.inverse_transform(ref mousePreScale);


                        layerScale *= zoomDelta;

                        Vector2 mousePostScale = mouseDownPosition;
                        TotalTransform.inverse_transform(ref mousePostScale);
                        
                        unscaledRenderOffset += (mousePostScale - mousePreScale);
                        break;

                    default:
                        throw new NotImplementedException();
                }

                Invalidate();
            }
            lastMousePosition = mousePos;
        }

        public void Load(string gcodePathAndFileName)
        {
            this.FileNameAndPath = gcodePathAndFileName;
            backgroundWorker = new BackgroundWorker();
            backgroundWorker.WorkerReportsProgress = true;
            backgroundWorker.WorkerSupportsCancellation = true;

            backgroundWorker.ProgressChanged += new ProgressChangedEventHandler(backgroundWorker_ProgressChanged);
            backgroundWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(backgroundWorker_RunWorkerCompleted);

            loadedGCode = null;
            GCodeFile.LoadInBackground(backgroundWorker, gcodePathAndFileName);
        }

        public override void OnClosed(EventArgs e)
        {
            if (backgroundWorker != null)
            {
                backgroundWorker.CancelAsync();
            }
            base.OnClosed(e);
        }

        public override RectangleDouble LocalBounds
        {
            get
            {
                return base.LocalBounds;
            }
            set
            {
                double oldWidth = Width;
                double oldHeight = Height;
                base.LocalBounds = value;
                if (oldWidth > 0)
                {
                    layerScale = layerScale * (Width / oldWidth);
                }
                else if(gCodeView != null)
                {
                    CenterPartInView();
                }
            }
        }

        public void CenterPartInView()
        {
            gCodeView = new GCodeVertexSource(loadedGCode);
            RectangleDouble partBounds = loadedGCode.GetBounds();
            Vector2 weightedCenter = loadedGCode.GetWeightedCenter();

            unscaledRenderOffset = -weightedCenter;
            layerScale = Math.Min(Height / partBounds.Height, Width / partBounds.Width);

            Invalidate();
        }
    }
}
