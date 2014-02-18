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
    public class GCodeRenderer
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

        GCodeFile gCodeFileToDraw;
        public double RetractionDistance { get; set; }
        public double RetractionDrawRadius { get; set; }

        public GCodeRenderer(GCodeFile gCodeFileToDraw)
        {
            RetractionDistance = .5;
            RetractionDrawRadius = 1;
            this.gCodeFileToDraw = gCodeFileToDraw;
        }

        public void Render(Graphics2D graphics2D, int activeLayerIndex, Affine transform, double layerScale, GCodeRenderer.RenderType renderType)
        {
            double extrusionLineWidths = 0.2 * layerScale;
            double movementLineWidth = 0.35 * layerScale;
            RGBA_Bytes movementColor = new RGBA_Bytes(10, 190, 15);

            int currentVertexIndex = gCodeFileToDraw.IndexOfChangeInZ[activeLayerIndex]; ;
            double currentZ = gCodeFileToDraw.GCodeCommandQueue[currentVertexIndex].Position.z;

            PathStorage pathStorage = new PathStorage();
            VertexSourceApplyTransform transformedPathStorage = new VertexSourceApplyTransform(pathStorage, transform);
            Stroke stroke = new Stroke(transformedPathStorage, extrusionLineWidths);

            stroke.line_cap(LineCap.Round);
            stroke.line_join(LineJoin.Round);

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

                switch (GetNextRenderType(currentVertexIndex) & renderType)
                {
                    case RenderType.None:
                        currentVertexIndex++;
                        break;

                    case RenderType.Extrusions:
                        DrawRetractionIfRequired(graphics2D, transform, layerScale, renderType, currentInstruction, previousInstruction);
                        currentVertexIndex = GetNextPath(pathStorage, currentVertexIndex, true);
                        graphics2D.Render(stroke, 0, RGBA_Bytes.Black);

                        break;

                    case RenderType.Moves:
                        DrawRetractionIfRequired(graphics2D, transform, layerScale, renderType, currentInstruction, previousInstruction);
                        currentVertexIndex = GetNextPath(pathStorage, currentVertexIndex, false);
                        graphics2D.Render(stroke, 0, movementColor);
                        break;
                }
            }
        }

        private void DrawRetractionIfRequired(Graphics2D graphics2D, Affine transform, double layerScale, GCodeRenderer.RenderType renderType, PrinterMachineInstruction currentInstruction, PrinterMachineInstruction previousInstruction)
        {
            if ((renderType & RenderType.Retractions) == RenderType.Retractions
                && currentInstruction.xyzPosition == previousInstruction.xyzPosition
                && Math.Abs(currentInstruction.EPosition - previousInstruction.EPosition) > RetractionDistance)
            {
                Vector2 position = new Vector2(currentInstruction.X, currentInstruction.Y);
                transform.transform(ref position);
                Ellipse extrusion = new Ellipse(position, RetractionDrawRadius * layerScale);

                if (currentInstruction.EPosition - previousInstruction.EPosition > 0)
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

        private int GetNextPath(PathStorage pathStorage, int currentVertexIndex, bool extrusions)
        {
            pathStorage.remove_all();
            PrinterMachineInstruction startInstruction = gCodeFileToDraw.GCodeCommandQueue[currentVertexIndex];

            if (currentVertexIndex > 0 && currentVertexIndex < gCodeFileToDraw.GCodeCommandQueue.Count)
            {
                PrinterMachineInstruction previousInstruction = gCodeFileToDraw.GCodeCommandQueue[currentVertexIndex - 1];
                pathStorage.Add(previousInstruction.Position.x, previousInstruction.Position.y, ShapePath.FlagsAndCommand.CommandMoveTo);
            }
            else
            {
                pathStorage.Add(startInstruction.Position.x, startInstruction.Position.y, ShapePath.FlagsAndCommand.CommandMoveTo);
            }
            double currentZ = startInstruction.Position.z;

            for (int i = currentVertexIndex; i < gCodeFileToDraw.GCodeCommandQueue.Count; i++)
            {
                PrinterMachineInstruction currentInstruction = gCodeFileToDraw.GCodeCommandQueue[i];
                PrinterMachineInstruction previousInstruction = gCodeFileToDraw.GCodeCommandQueue[i - 1];
                if (currentInstruction.Z != currentZ)
                {
                    // we are done with the whole layer so we can let the next function know that we are advancing to the end of the gcode
                    break; 
                }

                if (Math.Abs(currentInstruction.EPosition - previousInstruction.EPosition) > RetractionDistance)
                {
                    return Math.Max(currentVertexIndex + 1, i);
                }

                if (gCodeFileToDraw.IsExtruding(i) == extrusions)
                {
                    pathStorage.Add(currentInstruction.Position.x, currentInstruction.Position.y, ShapePath.FlagsAndCommand.CommandLineTo);
                }
                else
                {
                    pathStorage.Add(currentInstruction.Position.x, currentInstruction.Position.y, ShapePath.FlagsAndCommand.CommandStop);
                    return Math.Max(currentVertexIndex + 1, i);
                }
            }

            return gCodeFileToDraw.GCodeCommandQueue.Count;
        }

        RenderType GetNextRenderType(int currentVertexIndex)
        {
            PrinterMachineInstruction currentInstruction = gCodeFileToDraw.GCodeCommandQueue[currentVertexIndex];
            PrinterMachineInstruction previousInstruction = gCodeFileToDraw.GCodeCommandQueue[currentVertexIndex - 1];

            if (gCodeFileToDraw.IsExtruding(currentVertexIndex))
            {
                return RenderType.Extrusions;
            }
            else
            {
                return RenderType.Moves;
            }
        }
    }
}
