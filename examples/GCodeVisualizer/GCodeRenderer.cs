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
using System.Linq;
using System.Text;

using MatterHackers.VectorMath;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.GCodeVisualizer
{
    [Flags]
    public enum RenderType
    {
        None = 0,
        Extrusions = 1,
        Moves = 2,
        Retractions = 4,
        All = Extrusions | Moves | Retractions
    };

    public abstract class RenderFeatureBase
    {
        public abstract void Render(Graphics2D graphics2D, Affine transform, double layerScale, RenderType renderType);
    }

    public class RenderFeatureRetract : RenderFeatureBase
    {
        public static double RetractionDistance  = .5;
        public static double RetractionDrawRadius = 1;

        double amount;
        double mmPerSecond;
        Vector3 position;
        public RenderFeatureRetract(Vector3 position, double amount, double mmPerSecond)
        {
            this.amount = amount;
            this.mmPerSecond = mmPerSecond;
            this.position = position;
        }

        public override void Render(Graphics2D graphics2D, Affine transform, double layerScale, RenderType renderType)
        {
            if ((renderType & RenderType.Retractions) == RenderType.Retractions)
            {
                Vector2 position = new Vector2(this.position.x, this.position.y);
                transform.transform(ref position);
                Ellipse extrusion = new Ellipse(position, RetractionDrawRadius * layerScale);

                if (amount > 0)
                {
                    // unretraction
                    graphics2D.Render(extrusion, RGBA_Bytes.Blue);
                }
                else
                {
                    // retraction
                    graphics2D.Render(extrusion, RGBA_Bytes.Red);
                }
            }
        }
    }

    public class RenderFeatureTravel : RenderFeatureBase
    {
        protected Vector3 start;
        protected Vector3 end;
        protected double travelSpeed;

        public RenderFeatureTravel(Vector3 start, Vector3 end, double travelSpeed)
        {
            this.start = start;
            this.end = end;
            this.travelSpeed = travelSpeed;
        }

        public override void Render(Graphics2D graphics2D, Affine transform, double layerScale, RenderType renderType)
        {
            if ((renderType & RenderType.Moves) == RenderType.Moves)
            {
                double movementLineWidth = 0.35 * layerScale;
                RGBA_Bytes movementColor = new RGBA_Bytes(10, 190, 15);

                PathStorage pathStorage = new PathStorage();
                VertexSourceApplyTransform transformedPathStorage = new VertexSourceApplyTransform(pathStorage, transform);
                Stroke stroke = new Stroke(transformedPathStorage, movementLineWidth);

                stroke.line_cap(LineCap.Round);
                stroke.line_join(LineJoin.Round);

                pathStorage.Add(start.x, start.y, ShapePath.FlagsAndCommand.CommandMoveTo);
                pathStorage.Add(end.x, end.y, ShapePath.FlagsAndCommand.CommandLineTo);

                graphics2D.Render(stroke, 0, movementColor);
            }
        }
    }

    public class RenderFeatureExtrusion : RenderFeatureTravel
    {
        double totalExtrusion;

        public RenderFeatureExtrusion(Vector3 start, Vector3 end, double travelSpeed, double totalExtrusion)
            : base(start, end, travelSpeed)
        {
            this.totalExtrusion = totalExtrusion;
        }

        public override void Render(Graphics2D graphics2D, Affine transform, double layerScale, RenderType renderType)
        {
            if ((renderType & RenderType.Extrusions) == RenderType.Extrusions)
            {
                double extrusionLineWidths = 0.2 * layerScale;
                RGBA_Bytes extrusionColor = RGBA_Bytes.Black;

                PathStorage pathStorage = new PathStorage();
                VertexSourceApplyTransform transformedPathStorage = new VertexSourceApplyTransform(pathStorage, transform);
                Stroke stroke = new Stroke(transformedPathStorage, extrusionLineWidths);

                stroke.line_cap(LineCap.Round);
                stroke.line_join(LineJoin.Round);

                pathStorage.Add(start.x, start.y, ShapePath.FlagsAndCommand.CommandMoveTo);
                pathStorage.Add(end.x, end.y, ShapePath.FlagsAndCommand.CommandLineTo);

                graphics2D.Render(stroke, 0, extrusionColor);
            }
        }
    }

    public class GCodeRenderer
    {
        List<List<RenderFeatureBase>> renderFeatures = new List<List<RenderFeatureBase>>();

        GCodeFile gCodeFileToDraw;

        public GCodeRenderer(GCodeFile gCodeFileToDraw)
        {
            this.gCodeFileToDraw = gCodeFileToDraw;

            for (int i = 0; i < gCodeFileToDraw.NumChangesInZ; i++)
            {
                renderFeatures.Add(new List<RenderFeatureBase>());
            }
        }

        void CreateFeaturesForLayer(int layerToCreate)
        {
            List<RenderFeatureBase> renderFeaturesForLayer = renderFeatures[layerToCreate];

            int currentVertexIndex = gCodeFileToDraw.IndexOfChangeInZ[layerToCreate];
            double currentZ = gCodeFileToDraw.GCodeCommandQueue[currentVertexIndex].Position.z;

            while (currentVertexIndex < gCodeFileToDraw.GCodeCommandQueue.Count)
            {
                PrinterMachineInstruction currentInstruction = gCodeFileToDraw.GCodeCommandQueue[currentVertexIndex];
                PrinterMachineInstruction previousInstruction = currentInstruction;
                if (currentVertexIndex > 0)
                {
                    previousInstruction = gCodeFileToDraw.GCodeCommandQueue[currentVertexIndex - 1];
                }
                if (currentInstruction.Z != currentZ)
                {
                    break;
                }

                if (currentInstruction.Position == previousInstruction.Position)
                {
                    if (Math.Abs(currentInstruction.EPosition - previousInstruction.EPosition) > 0)
                    {
                        // this is a retraction
                        renderFeaturesForLayer.Add(new RenderFeatureRetract(currentInstruction.Position, currentInstruction.EPosition - previousInstruction.EPosition, currentInstruction.FeedRate));
                    }
                    if (currentInstruction.Line.StartsWith("G10"))
                    {
                        renderFeaturesForLayer.Add(new RenderFeatureRetract(currentInstruction.Position, -1, currentInstruction.FeedRate));
                    }
                    else if (currentInstruction.Line.StartsWith("G11"))
                    {
                        renderFeaturesForLayer.Add(new RenderFeatureRetract(currentInstruction.Position, 1, currentInstruction.FeedRate));
                    }
                }
                else
                {
                    if (gCodeFileToDraw.IsExtruding(currentVertexIndex))
                    {
                        renderFeaturesForLayer.Add(new RenderFeatureExtrusion(previousInstruction.Position, currentInstruction.Position, currentInstruction.FeedRate, currentInstruction.EPosition - previousInstruction.EPosition));
                    }
                    else
                    {
                        renderFeaturesForLayer.Add(new RenderFeatureTravel(previousInstruction.Position, currentInstruction.Position, currentInstruction.FeedRate));
                    }
                }

                currentVertexIndex++;
            }
        }

        public void Render(Graphics2D graphics2D, int activeLayerIndex, Affine transform, double layerScale, RenderType renderType)
        {
            if (renderFeatures.Count > 0)
            {
                if (renderFeatures[activeLayerIndex].Count == 0)
                {
                    CreateFeaturesForLayer(activeLayerIndex);
                }

                foreach (RenderFeatureBase feature in renderFeatures[activeLayerIndex])
                {
                    feature.Render(graphics2D, transform, layerScale, renderType);
                }
            }
        }
    }
}
