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

//#define use_timers
//#define AA_TIPS

using System;
using System.Collections.Generic;

using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.VertexSource;
using MatterHackers.Agg.Transform;
using MatterHackers.VectorMath;

using Tesselate;

using OpenTK.Graphics.OpenGL;

namespace MatterHackers.RenderOpenGl
{
    public class RenderToGLTesselator : VertexTesselatorAbstract
    {
        List<AddedVertex> m_Vertices = new List<AddedVertex>();

        internal class AddedVertex
        {
            internal AddedVertex(double x, double y)
            {
                m_X = x;
                m_Y = y;
            }
            internal double m_X;
            internal double m_Y;
        };

        public RenderToGLTesselator()
        {
            callBegin += BeginCallBack;
            callEnd += EndCallBack;
            callVertex += VertexCallBack;
            //callEdgeFlag += EdgeFlagCallBack;
            callCombine += CombineCallBack;
        }

        public override void BeginPolygon()
        {
            m_Vertices.Clear();

            base.BeginPolygon();
        }

        public void BeginCallBack(Tesselator.TriangleListType type)
        {
            switch (type)
            {
                case Tesselator.TriangleListType.Triangles:
                    GL.Begin(BeginMode.Triangles);
                    break;

                case Tesselator.TriangleListType.TriangleFan:
                    GL.Begin(BeginMode.TriangleFan);
                    break;

                case Tesselator.TriangleListType.TriangleStrip:
                    GL.Begin(BeginMode.TriangleStrip);
                    break;
            }
        }

        public void EndCallBack()
        {
            GL.End();
        }

        public void VertexCallBack(int index)
        {
            GL.Vertex2(m_Vertices[index].m_X, m_Vertices[index].m_Y);
        }

        public void EdgeFlagCallBack(bool IsEdge)
        {
            GL.EdgeFlag(IsEdge);
        }

        public void CombineCallBack(double[] coords3, int[] data4,
            double[] weight4, out int outData)
        {
            outData = AddVertex(coords3[0], coords3[1], false);
        }

        public override void AddVertex(double x, double y)
        {
            AddVertex(x, y, true);
        }

        public int AddVertex(double x, double y, bool passOnToTesselator)
        {
            int index = m_Vertices.Count;
            m_Vertices.Add(new AddedVertex(x, y));
            double[] coords = new double[3];
            coords[0] = x;
            coords[1] = y;
            if (passOnToTesselator)
            {
                AddVertex(coords, index);
            }
            return index;
        }
    }
}