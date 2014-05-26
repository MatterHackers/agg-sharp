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
using System.Runtime.InteropServices;

using MatterHackers.VectorMath;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg;
using MatterHackers.Agg.VertexSource;

using OpenTK.Graphics.OpenGL;

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
        public abstract void Render3D(VectorPOD<ColorVertexData> colorVertexData, Affine transform, double layerScale, RenderType renderType);
    }

    public class RenderFeatureRetract : RenderFeatureBase
    {
        public static double RetractionDrawRadius = 1;

        double extrusionAmount;
        double mmPerSecond;
        Vector3 position;
        public RenderFeatureRetract(Vector3 position, double extrusionAmount, double mmPerSecond)
        {
            this.extrusionAmount = extrusionAmount;
            this.mmPerSecond = mmPerSecond;
            this.position = position;
        }

        private double Radius(double layerScale)
        {
            double radius = RetractionDrawRadius * layerScale;
            double area = Math.PI * radius * radius;
            area *= Math.Abs(extrusionAmount);
            radius = Math.Sqrt(area / Math.PI);
            return radius;
        }

        public override void Render3D(VectorPOD<ColorVertexData> colorVertexData, Affine transform, double layerScale, RenderType renderType)
        {
            ColorVertexData start;
            ColorVertexData end;
            if (extrusionAmount > 0)
            {
                // unretraction
                start = new ColorVertexData(position + new Vector3(0, 0, Radius(1)), RGBA_Bytes.Blue);
                end = new ColorVertexData(position + new Vector3(0, 0, Radius(1)), RGBA_Bytes.Blue);
            }
            else
            {
                // retraction
                start = new ColorVertexData(position + new Vector3(0, 0, Radius(1)), RGBA_Bytes.Red);
                end = new ColorVertexData(position + new Vector3(0, 0, Radius(1)), RGBA_Bytes.Red);
            }

            colorVertexData.Add(start);
            colorVertexData.Add(end);
        }

        public override void Render(Graphics2D graphics2D, Affine transform, double layerScale, RenderType renderType)
        {
            if ((renderType & RenderType.Retractions) == RenderType.Retractions)
            {
                Vector2 position = new Vector2(this.position.x, this.position.y);
                transform.transform(ref position);
                double radius = Radius(layerScale);
                Ellipse extrusion = new Ellipse(position, radius);

                if (extrusionAmount > 0)
                {
                    // unretraction
                    graphics2D.Render(extrusion, new RGBA_Bytes(RGBA_Bytes.Blue, 200));
                }
                else
                {
                    // retraction
                    graphics2D.Render(extrusion, new RGBA_Bytes(RGBA_Bytes.Red, 200));
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

        public override void Render3D(VectorPOD<ColorVertexData> colorVertexData, Affine transform, double layerScale, RenderType renderType)
        {
            ColorVertexData startV = new ColorVertexData(start, RGBA_Bytes.Green);
            ColorVertexData endV = new ColorVertexData(end, RGBA_Bytes.Green);

            colorVertexData.Add(startV);
            colorVertexData.Add(endV);
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
                if (end.x != start.x || end.y != start.y)
                {
                    pathStorage.Add(end.x, end.y, ShapePath.FlagsAndCommand.CommandLineTo);
                }
                else
                {
                    pathStorage.Add(end.x + .01, end.y, ShapePath.FlagsAndCommand.CommandLineTo);
                }
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

        public override void Render3D(VectorPOD<ColorVertexData> colorVertexData, Affine transform, double layerScale, RenderType renderType)
        {
            ColorVertexData startV = new ColorVertexData(start, RGBA_Bytes.White);
            ColorVertexData endV = new ColorVertexData(end, RGBA_Bytes.White);

            colorVertexData.Add(startV);
            colorVertexData.Add(endV);
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

    public struct ColorVertexData
    {
        public byte r;
        public byte g;
        public byte b;
        public byte a;

        public float x;
        public float y;
        public float z;

        public static readonly int Stride = Marshal.SizeOf(default(ColorVertexData));

        public ColorVertexData(Vector3 position, RGBA_Bytes color)
        {
            r = (Byte)color.Red0To255;
            g = (Byte)color.Green0To255;
            b = (Byte)color.Blue0To255;
            a = (Byte)color.Alpha0To255;

            x = (float)position.x;
            y = (float)position.y;
            z = (float)position.z;
        }
    }

    public class GCodeRenderer
    {
        VectorPOD<ColorVertexData> colorVertexData = new VectorPOD<ColorVertexData>();
        VectorPOD<int> featureStartIndex = new VectorPOD<int>();
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

        void CreateFeaturesForLayerIfRequired(int layerToCreate)
        {
            if (renderFeatures[layerToCreate].Count > 0)
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
                        renderFeaturesForLayer.Add(new RenderFeatureExtrusion(previousInstruction.Position, currentInstruction.Position, currentInstruction.FeedRate, currentInstruction.EPosition - previousInstruction.EPosition));
                    }
                    else
                    {
                        renderFeaturesForLayer.Add(new RenderFeatureTravel(previousInstruction.Position, currentInstruction.Position, currentInstruction.FeedRate));
                    }
                }
            }
        }

        public int GetNumFeatures(int layerToCountFeaturesOn)
        {
            CreateFeaturesForLayerIfRequired(layerToCountFeaturesOn);
            return renderFeatures[layerToCountFeaturesOn].Count;
        }

        public void Render3D(int activeLayerIndex, Affine transform, double layerScale, RenderType renderType,
            double featureToStartOnRatio0To1, double featureToEndOnRatio0To1)
        {
            if (renderFeatures.Count > 0)
            {
                colorVertexData.Clear();

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
                    feature.Render3D(colorVertexData, transform, layerScale, renderType);
                }

                GL.DisableClientState(ArrayCap.TextureCoordArray);

                GL.InterleavedArrays(InterleavedArrayFormat.C4ubV3f, 0, colorVertexData.Array);
                GL.DrawArrays(BeginMode.Lines, 0, colorVertexData.Count);

                GL.DisableClientState(ArrayCap.NormalArray);
                GL.DisableClientState(ArrayCap.VertexArray);

            }
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

                for(int i=startFeature; i<endFeature; i++)
                {
                    RenderFeatureBase feature = renderFeatures[activeLayerIndex][i];
                    feature.Render(graphics2D, transform, layerScale, renderType);
                }
            }
        }
    }
}
