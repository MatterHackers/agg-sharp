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
    public class AARenderToGLTesselator : CachedTesselator
    {
        public AARenderToGLTesselator()
        {
        }

        protected void DrawNonAATriangle(Vector2 p0, Vector2 p1, Vector2 p2)
        {
            GL.Begin(BeginMode.Triangles);
            {
                // P1
                GL.TexCoord2(.2, .25);
                GL.Vertex2(p0.x, p0.y);

                // P2
                GL.TexCoord2(.2, .75);
                GL.Vertex2(p1.x, p1.y);

                // P3
                GL.TexCoord2(.9, .5);
                GL.Vertex2(p2.x, p2.y);
            }
            GL.End();
        }

        protected void Draw1EdgeTriangle(Vector2 p0, Vector2 p1, Vector2 p2)
        {
            //return;
            if (p0 == p1 || p1 == p2 || p2 == p0)
            {
                return;
            }
            Vector2 edegP0P1Vector = p1 - p0;
            Vector2 edgeP0P1Normal = edegP0P1Vector;
            edgeP0P1Normal.Normalize();

#if AA_TIPS
            Vector2 edegP2P1Vector = p1 - p2;
            Vector2 edgeP2P1Normal = edegP2P1Vector;
            edgeP2P1Normal.Normalize();

            Vector2 edegP2P0Vector = p0 - p2;
            Vector2 edgeP2P0Normal = edegP2P0Vector;
            edgeP2P0Normal.Normalize();
#endif

            Vector2 Normal = edgeP0P1Normal.PerpendicularRight;
            double edgeDotP3 = Vector2.Dot(Normal, p2 - p0);
            if (edgeDotP3 < 0)
            {
                edgeDotP3 = -edgeDotP3;
            }
            else
            {
                Normal = -Normal;
            }

            Vector2 edgeP0Offset = p0 + Normal;
            Vector2 edgeP1Offset = p1 + Normal;

            GL.Begin(BeginMode.TriangleFan);
            {
                GL.TexCoord2(1 / 1023.0, .25);
                GL.Vertex2(p0.x, p0.y);

#if AA_TIPS
                // the new point
                GL.TexCoord2(0, 1);
                GL.Vertex2(p0.x + edgeP2P0Normal.x, p0.y + edgeP2P0Normal.y);
#endif

                GL.TexCoord2(0, .25);
                GL.Vertex2(edgeP0Offset.x, edgeP0Offset.y);

                GL.TexCoord2(0, .75);
                GL.Vertex2(edgeP1Offset.x, edgeP1Offset.y);

                GL.TexCoord2(1 / 1023.0, .75);
                GL.Vertex2(p1.x, p1.y);

                GL.TexCoord2((1 + edgeDotP3) / 1023.0, .25);
                GL.Vertex2(p2.x, p2.y);
            }
            GL.End();

#if AA_TIPS
            GL.Begin(BeginMode.Triangles);
            {
                GL.TexCoord2(.5, 1);
                GL.Vertex2(p1.x, p1.y);

                GL.TexCoord2(0, 1);
                GL.Vertex2(edgeP1Offset.x, edgeP1Offset.y);

                // the new point
                GL.TexCoord2(0, 1);
                GL.Vertex2(p0.x + edgeP2P1Normal.x, p0.y + edgeP2P1Normal.y);
            }
            GL.End();
#endif
        }

        protected void Draw2EdgeTriangle(Vector2 p0, Vector2 p1, Vector2 p2)
        {
            //Draw3EdgeTriangle(p0, p1, p2);
            Vector2 centerPoint = p0 + p1 + p2;
            centerPoint /= 3;

            Draw1EdgeTriangle(p0, p1, centerPoint);
            Draw1EdgeTriangle(p1, p2, centerPoint);
            DrawNonAATriangle(p2, p0, centerPoint);
        }

        protected void Draw3EdgeTriangle(Vector2 p0, Vector2 p1, Vector2 p2)
        {
            Vector2 centerPoint = p0 + p1 + p2;
            centerPoint /= 3;

            Draw1EdgeTriangle(p0, p1, centerPoint);
            Draw1EdgeTriangle(p1, p2, centerPoint);
            Draw1EdgeTriangle(p2, p0, centerPoint);
        }

        public void RenderLastToGL()
        {
            int numIndicies = IndicesCache.Count;
            for (int i = 0; i < numIndicies; i += 3)
            {
                Vector2 v0 = VerticesCache[IndicesCache[i + 0].Index].Position;
                Vector2 v1 = VerticesCache[IndicesCache[i + 1].Index].Position;
                Vector2 v2 = VerticesCache[IndicesCache[i + 2].Index].Position;
                if (v0 == v1 || v1 == v2 || v2 == v0)
                {
                    continue;
                }

                int e0 = IndicesCache[i + 0].IsEdge ? 1 : 0;
                int e1 = IndicesCache[i + 1].IsEdge ? 1 : 0;
                int e2 = IndicesCache[i + 2].IsEdge ? 1 : 0;
                switch (e0 + e1 + e2)
                {
                    case 0:
                        DrawNonAATriangle(v0, v1, v2);
                        break;

                    case 1:
                        if (e0 == 1)
                        {
                            Draw1EdgeTriangle(v0, v1, v2);
                        }
                        else if (e1 == 1)
                        {
                            Draw1EdgeTriangle(v1, v2, v0);
                        }
                        else
                        {
                            Draw1EdgeTriangle(v2, v0, v1);
                        }
                        break;

                    case 2:
                        if (e0 == 1)
                        {
                            if (e1 == 1)
                            {
                                Draw2EdgeTriangle(v0, v1, v2);
                            }
                            else
                            {
                                Draw2EdgeTriangle(v2, v0, v1);
                            }
                        }
                        else
                        {
                            Draw2EdgeTriangle(v1, v2, v0);
                        }
                        break;

                    case 3:
                        Draw3EdgeTriangle(v0, v1, v2);
                        break;
                }
            }
        }
    }
}
