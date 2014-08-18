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
    public class RenderFeatureExtrusion : RenderFeatureTravel
    {
        float extrusionVolumeMm3;
        float layerHeight;
        RGBA_Bytes color;

        public RenderFeatureExtrusion(Vector3 start, Vector3 end, double travelSpeed, double totalExtrusionMm, double filamentDiameterMm, double layerHeight, RGBA_Bytes color)
            : base(start, end, travelSpeed)
        {
            this.color = color;
            double fillamentRadius = filamentDiameterMm / 2;
            double areaSquareMm = (fillamentRadius * fillamentRadius) * Math.PI;

            this.extrusionVolumeMm3 = (float)(areaSquareMm * totalExtrusionMm);
            this.layerHeight = (float)layerHeight;
        }

        double GetRadius(RenderType renderType)
        {
            double radius = .2;
            if ((renderType & RenderType.SimulateExtrusion) == RenderType.SimulateExtrusion)
            {
                double area = extrusionVolumeMm3 / ((end - start).Length);
                radius = Math.Sqrt(area / Math.PI);
            }

            return radius;
        }

        public override void CreateRender3DData(VectorPOD<ColorVertexData> colorVertexData, VectorPOD<int> indexData, Affine transform, double layerScale, RenderType renderType)
        {
            if ((renderType & RenderType.Extrusions) == RenderType.Extrusions)
            {
                double radius = GetRadius(renderType);
                if ((renderType & RenderType.SpeedColors) == RenderType.SpeedColors)
                {
                    CreateCylinder(colorVertexData, indexData, new Vector3(start), new Vector3(end), radius, 6, color, layerHeight);
                }
                else
                {
                    CreateCylinder(colorVertexData, indexData, new Vector3(start), new Vector3(end), radius, 6, GCodeRenderer.ExtrusionColor, layerHeight);
                }
            }
        }

        public override void Render(Graphics2D graphics2D, Affine transform, double layerScale, RenderType renderType)
        {
            if ((renderType & RenderType.Extrusions) == RenderType.Extrusions)
            {
                double extrusionLineWidths = GetRadius(renderType) * 2 * layerScale;
                RGBA_Bytes extrusionColor = RGBA_Bytes.Black;
                if ((renderType & RenderType.SpeedColors) == RenderType.SpeedColors)
                {
                    extrusionColor = color;
                }

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
}
