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
using System.Globalization;
using System.Text;
using System.IO;

using MatterHackers.Agg;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.VertexSource;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;

namespace MatterHackers.GCodeVisualizer
{
    public class GCodeVertexSource : IVertexSource
    {
        int currentVertexIndex;
        int currentLayer;
        GCodeFile gCodeFileToDraw;

        public double RetractionDistance { get; set; }

        public GCodeVertexSource(GCodeFile gCodeFile)
        {
            RetractionDistance = .5;
            this.gCodeFileToDraw = gCodeFile;
        }

        public int NumLayers
        {
            get { return gCodeFileToDraw.NumChangesInZ; }
        }

        public int GetNumSegmentsForLayer(int layerIndex)
        {
            return 1;
        }

        public int[] GetColorForSegmentsForLayer(int layerIndex)
        {
            int[] temp = new int[] { 1 };
            return temp;
        }

        public enum RenderType { Extrusions, Moves, Retractions };
        public RenderType WhatToRender { get; set; }

        public IEnumerable<VertexData> VertexIterator()
        {
            double lastEPosition = 0;
            VertexData startPosition = new VertexData();
            startPosition.position.x = gCodeFileToDraw.GCodeCommandQueue[currentVertexIndex].Position.x;
            startPosition.position.y = gCodeFileToDraw.GCodeCommandQueue[currentVertexIndex].Position.y;
            double currentZ = gCodeFileToDraw.GCodeCommandQueue[currentVertexIndex].Position.z;
            startPosition.command = MatterHackers.Agg.ShapePath.FlagsAndCommand.CommandMoveTo;
            yield return startPosition;

            bool needAMoveTo = false;
            for (int i = currentVertexIndex + 1; i < gCodeFileToDraw.GCodeCommandQueue.Count; i++)
            {
                PrinterMachineInstruction currentInstruction = gCodeFileToDraw.GCodeCommandQueue[i];
                PrinterMachineInstruction previousInstruction = gCodeFileToDraw.GCodeCommandQueue[i - 1];
                if (currentInstruction.Z != currentZ)
                {
                    break;
                }

                bool isExtruding = gCodeFileToDraw.IsExtruding(i);

                switch (WhatToRender)
                {
                    case RenderType.Extrusions:
                        if (isExtruding)
                        {
                            if (needAMoveTo)
                            {
                                VertexData gcodeMoveTo = new VertexData();

                                gcodeMoveTo.position.x = previousInstruction.Position.x;
                                gcodeMoveTo.position.y = gCodeFileToDraw.GCodeCommandQueue[i - 1].Position.y;
                                gcodeMoveTo.command = ShapePath.FlagsAndCommand.CommandMoveTo;
                                yield return gcodeMoveTo;
                                needAMoveTo = false;
                            }

                            VertexData gcodeLineTo = new VertexData();

                            gcodeLineTo.position.x = currentInstruction.Position.x;
                            gcodeLineTo.position.y = currentInstruction.Position.y;
                            gcodeLineTo.command = ShapePath.FlagsAndCommand.CommandLineTo;
                            yield return gcodeLineTo;
                        }
                        else
                        {
                            needAMoveTo = true;
                        }
                        break;

                    case RenderType.Moves:
                        if (!isExtruding)
                        {
                            if (needAMoveTo)
                            {
                                VertexData gcodeMoveTo = new VertexData();

                                gcodeMoveTo.position.x = previousInstruction.Position.x;
                                gcodeMoveTo.position.y = previousInstruction.Position.y;
                                gcodeMoveTo.command = ShapePath.FlagsAndCommand.CommandMoveTo;
                                yield return gcodeMoveTo;
                                needAMoveTo = false;
                            }

                            VertexData gcodeLineTo = new VertexData();

                            gcodeLineTo.position.x = currentInstruction.Position.x;
                            gcodeLineTo.position.y = currentInstruction.Position.y;
                            gcodeLineTo.command = ShapePath.FlagsAndCommand.CommandLineTo;
                            yield return gcodeLineTo;
                        }
                        else
                        {
                            needAMoveTo = true;
                        }
                        break;

                    case RenderType.Retractions:
                        if (currentInstruction.xyzPosition == previousInstruction.xyzPosition
                            && Math.Abs(currentInstruction.EPosition - lastEPosition) > RetractionDistance)
                        {
                            Ellipse extrusion = new Ellipse(new Vector2(currentInstruction.X, currentInstruction.Y), 1.5);
                            foreach (VertexData vertexData in extrusion.VertexIterator())
                            {
                                if (vertexData.command != ShapePath.FlagsAndCommand.CommandStop)
                                {
                                    yield return vertexData;
                                }
                            }
                        }
                        lastEPosition = currentInstruction.EPosition;
                        break;
                }
            }

            VertexData lastCommand = new VertexData();
            lastCommand.command = MatterHackers.Agg.ShapePath.FlagsAndCommand.CommandStop;
            yield return lastCommand;
        }

        IEnumerator<VertexData> currentEnumerator;
        public void rewind(int layerIndex)
        {
            currentLayer = layerIndex;
            currentVertexIndex = gCodeFileToDraw.IndexOfChangeInZ[layerIndex];

            currentEnumerator = VertexIterator().GetEnumerator();
            currentEnumerator.MoveNext();
        }

        public ShapePath.FlagsAndCommand vertex(out double x, out double y)
        {
            x = currentEnumerator.Current.position.x;
            y = currentEnumerator.Current.position.y;
            ShapePath.FlagsAndCommand command = currentEnumerator.Current.command;

            currentEnumerator.MoveNext();

            return command;
        }
    }
}
