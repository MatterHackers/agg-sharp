/*
Copyright (c) 2013, Lars Brubaker
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

using MatterHackers.Agg.VertexSource;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;

namespace MatterHackers.RenderOpenGl
{
    public static class VertexSourceToMesh
    {
        public static Mesh Extrude(IVertexSource vertexSource, double zHeight)
        {
            vertexSource.rewind();
            CachedTesselator teselatedSource = new CachedTesselator();
            Graphics2DOpenGL.SendShapeToTesselator(teselatedSource, vertexSource);

            Mesh extrudedVertexSource = new Mesh();

            int numIndicies = teselatedSource.IndicesCache.Count;

            // build the top first so it will render first when we are translucent
            for (int i = 0; i < numIndicies; i += 3)
            {
                Vector2 v0 = teselatedSource.VerticesCache[teselatedSource.IndicesCache[i + 0].Index].Position;
                Vector2 v1 = teselatedSource.VerticesCache[teselatedSource.IndicesCache[i + 1].Index].Position;
                Vector2 v2 = teselatedSource.VerticesCache[teselatedSource.IndicesCache[i + 2].Index].Position;
                if (v0 == v1 || v1 == v2 || v2 == v0)
                {
                    continue;
                }

                Vertex topVertex0 = extrudedVertexSource.CreateVertex(new Vector3(v0, zHeight));
                Vertex topVertex1 = extrudedVertexSource.CreateVertex(new Vector3(v1, zHeight));
                Vertex topVertex2 = extrudedVertexSource.CreateVertex(new Vector3(v2, zHeight));

                extrudedVertexSource.CreateFace(new Vertex[] { topVertex0, topVertex1, topVertex2 });
            }

            // then the outside edge
            for (int i = 0; i < numIndicies; i += 3)
            {
                Vector2 v0 = teselatedSource.VerticesCache[teselatedSource.IndicesCache[i + 0].Index].Position;
                Vector2 v1 = teselatedSource.VerticesCache[teselatedSource.IndicesCache[i + 1].Index].Position;
                Vector2 v2 = teselatedSource.VerticesCache[teselatedSource.IndicesCache[i + 2].Index].Position;
                if (v0 == v1 || v1 == v2 || v2 == v0)
                {
                    continue;
                }

                Vertex bottomVertex0 = extrudedVertexSource.CreateVertex(new Vector3(v0, 0));
                Vertex bottomVertex1 = extrudedVertexSource.CreateVertex(new Vector3(v1, 0));
                Vertex bottomVertex2 = extrudedVertexSource.CreateVertex(new Vector3(v2, 0));

                Vertex topVertex0 = extrudedVertexSource.CreateVertex(new Vector3(v0, zHeight));
                Vertex topVertex1 = extrudedVertexSource.CreateVertex(new Vector3(v1, zHeight));
                Vertex topVertex2 = extrudedVertexSource.CreateVertex(new Vector3(v2, zHeight));

                if (teselatedSource.IndicesCache[i + 0].IsEdge)
                {
                    extrudedVertexSource.CreateFace(new Vertex[] { bottomVertex0, bottomVertex1, topVertex1, topVertex0 });
                }

                if (teselatedSource.IndicesCache[i + 1].IsEdge)
                {
                    extrudedVertexSource.CreateFace(new Vertex[] { bottomVertex1, bottomVertex2, topVertex2, topVertex1 });
                }

                if (teselatedSource.IndicesCache[i + 2].IsEdge)
                {
                    extrudedVertexSource.CreateFace(new Vertex[] { bottomVertex2, bottomVertex0, topVertex0, topVertex2 });
                }
            }

            // then the bottom
            for (int i = 0; i < numIndicies; i += 3)
            {
                Vector2 v0 = teselatedSource.VerticesCache[teselatedSource.IndicesCache[i + 0].Index].Position;
                Vector2 v1 = teselatedSource.VerticesCache[teselatedSource.IndicesCache[i + 1].Index].Position;
                Vector2 v2 = teselatedSource.VerticesCache[teselatedSource.IndicesCache[i + 2].Index].Position;
                if (v0 == v1 || v1 == v2 || v2 == v0)
                {
                    continue;
                }

                Vertex bottomVertex0 = extrudedVertexSource.CreateVertex(new Vector3(v0, 0));
                Vertex bottomVertex1 = extrudedVertexSource.CreateVertex(new Vector3(v1, 0));
                Vertex bottomVertex2 = extrudedVertexSource.CreateVertex(new Vector3(v2, 0));

                extrudedVertexSource.CreateFace(new Vertex[] { bottomVertex2, bottomVertex1, bottomVertex0 });
            }

            return extrudedVertexSource;
        }
    }
}
