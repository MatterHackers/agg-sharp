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
        GCodeFile gCodeModel;
        double currentZ;
        List<int> firstVertexIndexInValidLayer = new List<int>();

        public GCodeVertexSource(GCodeFile layer)
        {
            gCodeModel = layer;

            switch (gCodeModel.IndexOfChangeInZ.Count)
            {
                case 0:
                    firstVertexIndexInValidLayer.Add(0);
                    break;

                case 1:
                    {
                        int indexOfChangeInZ = gCodeModel.IndexOfChangeInZ[0];
                        firstVertexIndexInValidLayer.Add(indexOfChangeInZ);
                    }
                    break;

                default:
                    {
                        for (int i = 0; i < gCodeModel.IndexOfChangeInZ.Count; i++)
                        {
                            int indexOfChangeInZ = gCodeModel.IndexOfChangeInZ[i];
                            double testZ = gCodeModel.GCodeCommandQueue[indexOfChangeInZ].xyzPosition.z;
                            
                            bool extruderWasAdvanced = false;
                            if (i + 1 < gCodeModel.IndexOfChangeInZ.Count)
                            {
                                for (int j = indexOfChangeInZ; j < gCodeModel.IndexOfChangeInZ[i + 1]; j++)
                                {
                                    if (gCodeModel.GCodeCommandQueue[j].EPosition > gCodeModel.GCodeCommandQueue[indexOfChangeInZ].EPosition)
                                    {
                                        extruderWasAdvanced = true;
                                    }
                                }
                            }
                            else
                            {
                                // always take the last layer
                                extruderWasAdvanced = true;
                            }

                            if (extruderWasAdvanced)
                            {
                                firstVertexIndexInValidLayer.Add(gCodeModel.IndexOfChangeInZ[i]);
                            }
                        }

                        if (firstVertexIndexInValidLayer.Count == 0)
                        {
                            for (int i = 0; i < gCodeModel.IndexOfChangeInZ.Count - 2; i++)
                            {
                                int indexOfChangeInZ = gCodeModel.IndexOfChangeInZ[i];
                                double testZ = gCodeModel.GCodeCommandQueue[indexOfChangeInZ].xyzPosition.z;

                                firstVertexIndexInValidLayer.Add(gCodeModel.IndexOfChangeInZ[i]);
                            }
                        }
                    }
                    break;
            }
        }

        public int NumLayers
        {
            get { return firstVertexIndexInValidLayer.Count; }
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

        public enum RenderType { RenderExtrusions, RenderMoves };
        public RenderType WhatToRender { get; set; }

        public IEnumerable<VertexData> VertexIterator()
        {
            VertexData startPosition = new VertexData();
            startPosition.position.x = gCodeModel.GCodeCommandQueue[currentVertexIndex].Position.x;
            startPosition.position.y = gCodeModel.GCodeCommandQueue[currentVertexIndex].Position.y;
            startPosition.command = MatterHackers.Agg.ShapePath.FlagsAndCommand.CommandMoveTo;
            yield return startPosition;

            bool needAMoveTo = false;
            for (int i = currentVertexIndex + 1; i < gCodeModel.GCodeCommandQueue.Count; i++)
            {
                if (gCodeModel.GCodeCommandQueue[i].Z != currentZ)
                {
                    break;
                }

                bool isExtruding = gCodeModel.IsExtruding(i);

                if (WhatToRender == RenderType.RenderExtrusions)
                {
                    if (isExtruding)
                    {
                        if (needAMoveTo)
                        {
                            VertexData gcodeMoveTo = new VertexData();

                            gcodeMoveTo.position.x = gCodeModel.GCodeCommandQueue[i - 1].Position.x;
                            gcodeMoveTo.position.y = gCodeModel.GCodeCommandQueue[i - 1].Position.y;
                            gcodeMoveTo.command = ShapePath.FlagsAndCommand.CommandMoveTo;
                            yield return gcodeMoveTo;
                            needAMoveTo = false;
                        }

                        VertexData gcodeLineTo = new VertexData();

                        gcodeLineTo.position.x = gCodeModel.GCodeCommandQueue[i].Position.x;
                        gcodeLineTo.position.y = gCodeModel.GCodeCommandQueue[i].Position.y;
                        gcodeLineTo.command = ShapePath.FlagsAndCommand.CommandLineTo;
                        yield return gcodeLineTo;
                    }
                    else
                    {
                        needAMoveTo = true;
                    }
                }
                else
                {
                    if (!isExtruding)
                    {
                        if (needAMoveTo)
                        {
                            VertexData gcodeMoveTo = new VertexData();

                            gcodeMoveTo.position.x = gCodeModel.GCodeCommandQueue[i - 1].Position.x;
                            gcodeMoveTo.position.y = gCodeModel.GCodeCommandQueue[i - 1].Position.y;
                            gcodeMoveTo.command = ShapePath.FlagsAndCommand.CommandMoveTo;
                            yield return gcodeMoveTo;
                            needAMoveTo = false;
                        }

                        VertexData gcodeLineTo = new VertexData();

                        gcodeLineTo.position.x = gCodeModel.GCodeCommandQueue[i].Position.x;
                        gcodeLineTo.position.y = gCodeModel.GCodeCommandQueue[i].Position.y;
                        gcodeLineTo.command = ShapePath.FlagsAndCommand.CommandLineTo;
                        yield return gcodeLineTo;
                    }
                    else
                    {
                        needAMoveTo = true;
                    }
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
            currentVertexIndex = firstVertexIndexInValidLayer[layerIndex];
            currentZ = gCodeModel.GCodeCommandQueue[currentVertexIndex].Z;

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
