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
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using MatterHackers.Agg;
using MatterHackers.Agg.Image;

using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;

namespace MatterHackers.RenderOpenGl
{
    public struct TriangleVertexData
    {
        public float textureU;
        public float textureV;
        public float normalsX;
        public float normalsY;
        public float normalsZ;
        public float positionsX;
        public float positionsY;
        public float positionsZ;

        public static readonly int Stride = Marshal.SizeOf(default(TriangleVertexData));
    }

    public class SubTriangleMesh
    {
        public ImageBuffer texture = null;
        public VectorPOD<TriangleVertexData> vertexDatas = new VectorPOD<TriangleVertexData>();
    }

    public class GLMeshTrianglePlugin
    {
        struct RemoveData
        {
            internal int vboHandle;

            public RemoveData(int vboHandle)
            {
                this.vboHandle = vboHandle;
            }
        }

        public delegate void DrawToGL(Mesh meshToRender);

        private static ConditionalWeakTable<Mesh, GLMeshTrianglePlugin> meshesWithCacheData = new ConditionalWeakTable<Mesh, GLMeshTrianglePlugin>();

        private static List<RemoveData> glDataNeedingToBeDeleted = new List<RemoveData>();

        public List<SubTriangleMesh> subMeshs;

        private int meshUpdateCount;

        static public void DeleteUnusedGLResources()
        {
            using (TimedLock.Lock(glDataNeedingToBeDeleted, "GLMeshPluginDeleteUnused"))
            {
                // We run this in here to ensure that we are on the correct thread and have the correct
                // glcontext realized.
                for (int i = glDataNeedingToBeDeleted.Count - 1; i >= 0; i--)
                {
                    //GL.DeleteBuffers(glDataNeedingToBeDeleted[i].vboHandle);
                    glDataNeedingToBeDeleted.RemoveAt(i);
                }
            }
        }

        static public GLMeshTrianglePlugin Get(Mesh meshToGetDisplayListFor)
        {
            GLMeshTrianglePlugin plugin;
            meshesWithCacheData.TryGetValue(meshToGetDisplayListFor, out plugin);

                if (plugin != null && meshToGetDisplayListFor.ChangedCount != plugin.meshUpdateCount)
                {
                    plugin.meshUpdateCount = meshToGetDisplayListFor.ChangedCount;
                    plugin.AddRemoveData();
                    plugin.CreateRenderData(meshToGetDisplayListFor);
                    plugin.meshUpdateCount = meshToGetDisplayListFor.ChangedCount;
                }

            DeleteUnusedGLResources();

            if (plugin == null)
            {
                GLMeshTrianglePlugin newPlugin = new GLMeshTrianglePlugin();
                meshesWithCacheData.Add(meshToGetDisplayListFor, newPlugin);
                newPlugin.CreateRenderData(meshToGetDisplayListFor);
                newPlugin.meshUpdateCount = meshToGetDisplayListFor.ChangedCount;

                return newPlugin;
            }

            return plugin;
        }

        private GLMeshTrianglePlugin()
        {
            // This is private as you can't build one of these. You have to call GetImageGLDisplayListPlugin.
        }

        void AddRemoveData()
        {
        }

        ~GLMeshTrianglePlugin()
        {
            AddRemoveData();
        }

        private void CreateRenderData(Mesh meshToBuildListFor)
        {
            subMeshs = new List<SubTriangleMesh>();
            SubTriangleMesh currentSubMesh = null;
            VectorPOD<TriangleVertexData> vertexDatas = new VectorPOD<TriangleVertexData>();
            // first make sure all the textures are created
            foreach (Face face in meshToBuildListFor.Faces)
            {
                ImageBuffer faceTexture = face.GetTexture(0);
                if (faceTexture != null)
                {
                    ImageGlPlugin.GetImageGlPlugin(faceTexture, true);
                }

                // don't compare the data of the texture but rather if they are just the same object
                if (subMeshs.Count == 0 || (object)subMeshs[subMeshs.Count - 1].texture != (object)faceTexture)
                {
                    SubTriangleMesh newSubMesh = new SubTriangleMesh();
                    newSubMesh.texture = faceTexture;
                    subMeshs.Add(newSubMesh);

                    currentSubMesh = subMeshs[subMeshs.Count - 1];
                    vertexDatas = currentSubMesh.vertexDatas;
                }

                Vector2[] textureUV = new Vector2[2];
                Vector3[] position = new Vector3[2];
                int vertexIndex = 0;
                foreach (FaceEdge faceEdge in face.FaceEdges())
                {
                    if (vertexIndex < 2)
                    {
                        textureUV[vertexIndex] = faceEdge.GetUVs(0);
                        position[vertexIndex] = faceEdge.firstVertex.Position;
                    }
                    else
                    {
                        TriangleVertexData tempVertex;
                        tempVertex.textureU = (float)textureUV[0].x; tempVertex.textureV = (float)textureUV[0].y;
                        tempVertex.positionsX = (float)position[0].x; tempVertex.positionsY = (float)position[0].y; tempVertex.positionsZ = (float)position[0].z;
                        tempVertex.normalsX = (float)face.normal.x; tempVertex.normalsY = (float)face.normal.y; tempVertex.normalsZ = (float)face.normal.z;
                        vertexDatas.Add(tempVertex);

                        tempVertex.textureU = (float)textureUV[1].x; tempVertex.textureV = (float)textureUV[1].y;
                        tempVertex.positionsX = (float)position[1].x; tempVertex.positionsY = (float)position[1].y; tempVertex.positionsZ = (float)position[1].z;
                        tempVertex.normalsX = (float)face.normal.x; tempVertex.normalsY = (float)face.normal.y; tempVertex.normalsZ = (float)face.normal.z;
                        vertexDatas.Add(tempVertex);

                        Vector2 textureUV2 = faceEdge.GetUVs(0);
                        Vector3 position2 = faceEdge.firstVertex.Position;
                        tempVertex.textureU = (float)textureUV2.x; tempVertex.textureV = (float)textureUV2.y;
                        tempVertex.positionsX = (float)position2.x; tempVertex.positionsY = (float)position2.y; tempVertex.positionsZ = (float)position2.z;
                        tempVertex.normalsX = (float)face.normal.x; tempVertex.normalsY = (float)face.normal.y; tempVertex.normalsZ = (float)face.normal.z;
                        vertexDatas.Add(tempVertex);

                        textureUV[1] = faceEdge.GetUVs(0);
                        position[1] = faceEdge.firstVertex.Position;
                    }

                    vertexIndex++;
                }
            }
        }

        public void Render()
        {
        }

        public static void AssertDebugNotDefined()
        {
#if DEBUG
            throw new Exception("DEBUG is defined and should not be!");
#endif
        }
    }
}

