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

using MatterHackers.VectorMath;
using MatterHackers.RenderOpenGl.OpenGl;

namespace MatterHackers.RenderOpenGl
{
    public class AARenderToGLTesselator : CachedTesselator
    {
        Vector2 fanPStart;
        Vector2 fanTStart;
        Vector2 fanPNext;
        Vector2 fanTNext;

        public AARenderToGLTesselator()
        {
        }

        protected void DrawNonAATriangle(Vector2 p0, Vector2 p1, Vector2 p2)
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

            Vector2 Normal = edgeP0P1Normal.GetPerpendicularRight();
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

            Vector2 texP0 = new Vector2(1 / 1023.0, .25);
            Vector2 texP1 = new Vector2(1 / 1023.0, .75);
            Vector2 texP2 = new Vector2((1 + edgeDotP3) / 1023.0, .25);
            Vector2 texEdgeP0Offset = new Vector2(0, .25);
            Vector2 texEdgeP1Offset = new Vector2(0, .75); 

            FanStart(texP0, p0, texEdgeP0Offset, edgeP0Offset);
            FanDo(texEdgeP1Offset, edgeP1Offset);
            FanDo(texP1, p1);
            FanDo(texP2, p2);
        }

        void FanStart(Vector2 fanTStart, Vector2 fanPStart, Vector2 fanTNext, Vector2 fanPNext)
        {
			this.fanTStart = fanTStart;
            this.fanPStart = fanPStart;
			this.fanTNext = fanTNext;
            this.fanPNext = fanPNext;
        }

        void FanDo(Vector2 fanTEnd, Vector2 fanPEnd)
        {
            GL.TexCoord2(fanTStart.x, fanTStart.y);
            GL.Vertex2(fanPStart.x, fanPStart.y);

            GL.TexCoord2(fanTNext.x, fanTNext.y);
            GL.Vertex2(fanPNext.x, fanPNext.y);

            GL.TexCoord2(fanTEnd.x, fanTEnd.y);
            GL.Vertex2(fanPEnd.x, fanPEnd.y);

			fanTNext = fanTEnd;
            fanPNext = fanPEnd;
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
			GL.Begin(BeginMode.Triangles);

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

            GL.End();
        }
    }
}
