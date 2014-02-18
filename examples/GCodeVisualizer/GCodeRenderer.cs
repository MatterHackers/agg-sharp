using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using MatterHackers.VectorMath;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.GCodeVisualizer
{
    public class GCodeRenderer
    {
        public GCodeVertexSource gCodeView;
        GCodeFile gCodeFileToDraw;

        public GCodeRenderer(GCodeFile gCodeFileToDraw)
        {
            gCodeView = new GCodeVertexSource(gCodeFileToDraw);
        }

        public void Render(Graphics2D graphics2D, int activeLayerIndex, Affine transform, double layerScale, GCodeVertexSource.RenderType renderType)
        {
            double extrusionLineWidths = 0.2 * layerScale;
            double movementLineWidth = 0.35 * layerScale;
            RGBA_Bytes movementColor = new RGBA_Bytes(10, 190, 15);

            VertexSourceApplyTransform transformedPathStorage = new VertexSourceApplyTransform(gCodeView, transform);

            Stroke stroke = new Stroke(transformedPathStorage, extrusionLineWidths);

            stroke.line_cap(LineCap.Round);
            stroke.line_join(LineJoin.Round);

            // This code renders the layer:
            gCodeView.WhatToRender = GCodeVertexSource.RenderType.Extrusions;
            graphics2D.Render(stroke, activeLayerIndex, RGBA_Bytes.Black);

            if ((renderType & GCodeVertexSource.RenderType.Moves) == GCodeVertexSource.RenderType.Moves)
            {
                stroke.width(movementLineWidth);
                gCodeView.WhatToRender = GCodeVertexSource.RenderType.Moves;
                graphics2D.Render(stroke, activeLayerIndex, movementColor);
            }

            if ((renderType & GCodeVertexSource.RenderType.Retractions) == GCodeVertexSource.RenderType.Retractions)
            {
                gCodeView.WhatToRender = GCodeVertexSource.RenderType.Retractions;
                graphics2D.Render(transformedPathStorage, activeLayerIndex, RGBA_Bytes.Red);
            }
        }
    }
}
