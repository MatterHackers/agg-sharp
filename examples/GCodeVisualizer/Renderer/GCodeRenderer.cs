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
using System.Runtime.InteropServices;
using MatterHackers.Agg;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.RenderOpenGl.OpenGl;
using MatterHackers.VectorMath;

namespace MatterHackers.GCodeVisualizer
{
    [Flags]
    public enum RenderType
    {
        None = 0,
        Extrusions = 1,
        Moves = 2,
        Retractions = 4,
        SpeedColors = 8,
        SimulateExtrusion = 16,
        All = Extrusions | Moves | Retractions
    };

    public class GCodeRenderer
    {
        List<int> layerStartIndex = new List<int>();
        List<List<int>> featureStartIndex = new List<List<int>>();
        List<List<RenderFeatureBase>> renderFeatures = new List<List<RenderFeatureBase>>();
        int TotalRenderFeatures = 0;

        public static RGBA_Bytes ExtrusionColor = RGBA_Bytes.White;
        public static RGBA_Bytes TravelColor = RGBA_Bytes.Green;

        GCodeFile gCodeFileToDraw;

        ExtrusionColors extrusionColors;

        public GCodeRenderer(GCodeFile gCodeFileToDraw)
        {
            if (gCodeFileToDraw != null)
            {
                this.gCodeFileToDraw = gCodeFileToDraw;

                for (int i = 0; i < gCodeFileToDraw.NumChangesInZ; i++)
                {
                    renderFeatures.Add(new List<RenderFeatureBase>());
                }
            }
        }

        public void CreateFeaturesForLayerIfRequired(int layerToCreate)
        {
            if (extrusionColors == null 
                && gCodeFileToDraw != null 
                && gCodeFileToDraw.Count > 0)
            {
                extrusionColors = new ExtrusionColors();
                HashSet<float> speeds = new HashSet<float>();
                PrinterMachineInstruction prevInstruction = gCodeFileToDraw.Instruction(0);
                for (int i = 1; i < gCodeFileToDraw.Count; i++)
                {
                    PrinterMachineInstruction instruction = gCodeFileToDraw.Instruction(i);
                    if (instruction.EPosition > prevInstruction.EPosition)
                    {
                        speeds.Add((float)instruction.FeedRate);
                    }

                    prevInstruction = instruction;
                }

                foreach (float speed in speeds)
                {
                    extrusionColors.GetColorForSpeed(speed);
                }
            }

            if (renderFeatures.Count == 0 
                || renderFeatures[layerToCreate].Count > 0)
            {
                return;
            }

            List<RenderFeatureBase> renderFeaturesForLayer = renderFeatures[layerToCreate];

            int startRenderIndex = gCodeFileToDraw.IndexOfChangeInZ[layerToCreate];
            int endRenderIndex = gCodeFileToDraw.Count - 1;
            if (layerToCreate < gCodeFileToDraw.IndexOfChangeInZ.Count - 1)
            {
                endRenderIndex = gCodeFileToDraw.IndexOfChangeInZ[layerToCreate + 1];
            }

            for (int i = startRenderIndex; i < endRenderIndex; i++ )
            {
                PrinterMachineInstruction currentInstruction = gCodeFileToDraw.Instruction(i);
                PrinterMachineInstruction previousInstruction = currentInstruction;
                if (i > 0)
                {
                    previousInstruction = gCodeFileToDraw.Instruction(i - 1);
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
                    if (gCodeFileToDraw.IsExtruding(i))
                    {
                        double layerThickness = gCodeFileToDraw.GetLayerHeight();
                        if (layerToCreate == 0)
                        {
                            layerThickness = gCodeFileToDraw.GetFirstLayerHeight();
                        }
                        renderFeaturesForLayer.Add(new RenderFeatureExtrusion(previousInstruction.Position, currentInstruction.Position, currentInstruction.FeedRate, currentInstruction.EPosition - previousInstruction.EPosition, gCodeFileToDraw.GetFilamentDiamter(), layerThickness, extrusionColors.GetColorForSpeed((float)currentInstruction.FeedRate)));
                    }
                    else
                    {
                        renderFeaturesForLayer.Add(new RenderFeatureTravel(previousInstruction.Position, currentInstruction.Position, currentInstruction.FeedRate));
                    }
                }
            }

            TotalRenderFeatures += renderFeaturesForLayer.Count;
        }

        public int GetNumFeatures(int layerToCountFeaturesOn)
        {
            CreateFeaturesForLayerIfRequired(layerToCountFeaturesOn);
            return renderFeatures[layerToCountFeaturesOn].Count;
        }

        public void Render(Graphics2D graphics2D, int activeLayerIndex, Affine transform, double layerScale, RenderType renderType,
            double featureToStartOnRatio0To1, double featureToEndOnRatio0To1)
        {
            if (renderFeatures.Count > 0)
            {
                CreateFeaturesForLayerIfRequired(activeLayerIndex);

                int featuresOnLayer = renderFeatures[activeLayerIndex].Count;
                int endFeature = (int)(featuresOnLayer * featureToEndOnRatio0To1 + .5);
                endFeature = Math.Max(0, Math.Min(endFeature, featuresOnLayer));

                int startFeature = (int)(featuresOnLayer * featureToStartOnRatio0To1 + .5);
                startFeature = Math.Max(0, Math.Min(startFeature, featuresOnLayer));

                // try to make sure we always draw at least one feature
                if (endFeature <= startFeature)
                {
                    endFeature = Math.Min(startFeature + 1, featuresOnLayer);
                }
                if (startFeature >= endFeature)
                {
                    // This can only happen if the sart and end are set to the last feature
                    // Try to set the start feture to one from the end
                    startFeature = Math.Max(endFeature - 1, 0);
                }

                for (int i = startFeature; i < endFeature; i++)
                {
                    RenderFeatureBase feature = renderFeatures[activeLayerIndex][i];
                    if (feature != null)
                    {
                        feature.Render(graphics2D, transform, layerScale, renderType);
                    }
                }
            }
        }

        void Create3DData(Affine transform, double layerScale, RenderType renderType, int lastLayerIndex, 
            VectorPOD<ColorVertexData> colorVertexData,
            VectorPOD<int> vertexIndexArray)
        {
            colorVertexData.Clear();
            vertexIndexArray.Clear();
            layerStartIndex.Clear();
            layerStartIndex.Capacity = gCodeFileToDraw.NumChangesInZ;
            featureStartIndex.Clear();
            layerStartIndex.Capacity = gCodeFileToDraw.NumChangesInZ;

            bool canOnlyShowOneLayer = TotalRenderFeatures > MAX_RENDER_FEATURES_TO_ALLOW_3D;
            if (canOnlyShowOneLayer)
            {
                layerStartIndex.Add(vertexIndexArray.Count);

                for (int layerIndex = 0; layerIndex < lastLayerIndex+1; layerIndex++)
                {
                    featureStartIndex.Add(new List<int>());
                }

                for (int i = 0; i < renderFeatures[lastLayerIndex].Count; i++)
                {
                    featureStartIndex[lastLayerIndex].Add(vertexIndexArray.Count);
                    RenderFeatureBase feature = renderFeatures[lastLayerIndex][i];
                    feature.CreateRender3DData(colorVertexData, vertexIndexArray, transform, layerScale, renderType);
                }

                singleLayerIndex = lastLayerIndex;
            }
            else
            {
                for (int layerIndex = 0; layerIndex < gCodeFileToDraw.NumChangesInZ; layerIndex++)
                {
                    layerStartIndex.Add(vertexIndexArray.Count);
                    featureStartIndex.Add(new List<int>());

                    for (int i = 0; i < renderFeatures[layerIndex].Count; i++)
                    {
                        featureStartIndex[layerIndex].Add(vertexIndexArray.Count);
                        RenderFeatureBase feature = renderFeatures[layerIndex][i];
                        if (feature != null)
                        {
                            feature.CreateRender3DData(colorVertexData, vertexIndexArray, transform, layerScale, renderType);
                        }
                    }
                }
            }
        }

        static readonly int MAX_RENDER_FEATURES_TO_ALLOW_3D = 250000;
        GCodeVertexBuffer vertexBuffer;
        RenderType lastRenderType = RenderType.None;
        int singleLayerIndex = 0;
        public void Render3D(int startLayerIndex, int endLayerIndex, Affine transform, double layerScale, RenderType renderType,
            double featureToStartOnRatio0To1, double featureToEndOnRatio0To1)
        {
            for (int layerIndex = 0; layerIndex < gCodeFileToDraw.NumChangesInZ; layerIndex++)
            {
                CreateFeaturesForLayerIfRequired(layerIndex);
            }

            if (renderFeatures.Count > 0)
            {
                bool canOnlyShowOneLayer = TotalRenderFeatures > MAX_RENDER_FEATURES_TO_ALLOW_3D;

                // If its the first render or we change what we are trying to render then create vertex data.
                if (lastRenderType != renderType
                    || (canOnlyShowOneLayer && endLayerIndex-1 != singleLayerIndex))
                {
                    VectorPOD<ColorVertexData> colorVertexData = new VectorPOD<ColorVertexData>();
                    VectorPOD<int> vertexIndexArray = new VectorPOD<int>();

                    Create3DData(transform, layerScale, renderType, endLayerIndex - 1, colorVertexData, vertexIndexArray);
                    
                    vertexBuffer = new GCodeVertexBuffer();
                    vertexBuffer.SetVertexData(colorVertexData.Array);
                    vertexBuffer.SetIndexData(vertexIndexArray.Array);

                    lastRenderType = renderType;
                }

                GL.DisableClientState(ArrayCap.TextureCoordArray);
                GL.PushAttrib(AttribMask.EnableBit);
                GL.Enable(EnableCap.PolygonSmooth);

                // draw all the layers from start to end-2
                if (endLayerIndex - 1 > startLayerIndex && !canOnlyShowOneLayer)
                {
                    int ellementCount = layerStartIndex[endLayerIndex - 1] - layerStartIndex[startLayerIndex];

                    vertexBuffer.renderRange(layerStartIndex[startLayerIndex], ellementCount);
                }

                // draw the partial layer of end-1 from startratio to endratio
                {
                    int layerIndex = endLayerIndex - 1;
                    int featuresOnLayer = renderFeatures[layerIndex].Count;
                    int startFeature = (int)(featuresOnLayer * featureToStartOnRatio0To1 + .5);
                    startFeature = Math.Max(0, Math.Min(startFeature, featuresOnLayer));

                    int endFeature = (int)(featuresOnLayer * featureToEndOnRatio0To1 + .5);
                    endFeature = Math.Max(0, Math.Min(endFeature, featuresOnLayer));

                    // try to make sure we always draw at least one feature
                    if (endFeature <= startFeature)
                    {
                        endFeature = Math.Min(startFeature + 1, featuresOnLayer);
                    }
                    if (startFeature >= endFeature)
                    {
                        // This can only happen if the sart and end are set to the last feature
                        // Try to set the start feture to one from the end
                        startFeature = Math.Max(endFeature - 1, 0);
                    }

                    if (endFeature > startFeature)
                    {
                        int ellementCount = featureStartIndex[layerIndex][endFeature - 1] - featureStartIndex[layerIndex][startFeature];

                        vertexBuffer.renderRange(featureStartIndex[layerIndex][startFeature], ellementCount);
                    }
                }
                GL.PopAttrib();
            }
        }
    }
}
