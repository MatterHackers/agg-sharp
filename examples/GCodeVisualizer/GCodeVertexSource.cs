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
