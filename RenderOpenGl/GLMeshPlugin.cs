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

#define INTERLIEVED_VERTEX_DATA
//#define USE_VBO

using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using MatterHackers.Agg;
using MatterHackers.Agg.Image;

using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;

using OpenTK.Graphics.OpenGL;

namespace MatterHackers.RenderOpenGl
{
#if INTERLIEVED_VERTEX_DATA
    public struct VertexData
    {
        public float textureU;
        public float textureV;
        public float normalsX;
        public float normalsY;
        public float normalsZ;
        public float positionsX;
        public float positionsY;
        public float positionsZ;

        public static readonly int Stride = Marshal.SizeOf(default(VertexData));
    }

    public class SubMesh
    {
        public ImageBuffer texture = null;
#if USE_VBO
        public int count;
        public int vboHandle;
#else
        public VectorPOD<VertexData> vertexDatas = new VectorPOD<VertexData>();
#endif
    }
#else
    public class SubMesh
    {
        public ImageBuffer texture = null;
        public VectorPOD<float> textureUVs = new VectorPOD<float>();
        public VectorPOD<float> positions = new VectorPOD<float>();
        public VectorPOD<float> normals = new VectorPOD<float>();
    }
#endif

    public class GLMeshPlugin
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

        private static ConditionalWeakTable<Mesh, GLMeshPlugin> meshesWithCacheData = new ConditionalWeakTable<Mesh, GLMeshPlugin>();

        private static List<RemoveData> glDataNeedingToBeDeleted = new List<RemoveData>();

        public List<SubMesh> subMeshs;

        private int meshUpdateCount;

        static public void DeleteUnusedGLResources()
        {
            using (TimedLock.Lock(glDataNeedingToBeDeleted, "GLMeshPluginDeleteUnused"))
            {
                // We run this in here to ensure that we are on the correct thread and have the correct
                // glcontext realized.
                for (int i = glDataNeedingToBeDeleted.Count - 1; i >= 0; i--)
                {
                    GL.DeleteBuffer(glDataNeedingToBeDeleted[i].vboHandle);
                    glDataNeedingToBeDeleted.RemoveAt(i);
                }
            }
        }

        static public GLMeshPlugin GetGLMeshPlugin(Mesh meshToGetDisplayListFor)
        {
            GLMeshPlugin plugin;
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
                GLMeshPlugin newPlugin = new GLMeshPlugin();
                meshesWithCacheData.Add(meshToGetDisplayListFor, newPlugin);
                newPlugin.CreateRenderData(meshToGetDisplayListFor);
                newPlugin.meshUpdateCount = meshToGetDisplayListFor.ChangedCount;

                return newPlugin;
            }

            return plugin;
        }

        private GLMeshPlugin()
        {
            // This is private as you can't build one of these. You have to call GetImageGLDisplayListPlugin.
        }

        void AddRemoveData()
        {
#if USE_VBO
            using (TimedLock.Lock(glDataNeedingToBeDeleted, "~GLMeshPlugin"))
            {
                foreach (SubMesh subMesh in subMeshs)
                {
                    glDataNeedingToBeDeleted.Add(new RemoveData(subMesh.vboHandle));
                }
            }
#endif
        }

        ~GLMeshPlugin()
        {
            AddRemoveData();
        }

        private void CreateRenderData(Mesh meshToBuildListFor)
        {
            subMeshs = new List<SubMesh>();
            SubMesh currentSubMesh = null;
            VectorPOD<VertexData> vertexDatas = new VectorPOD<VertexData>();
            // first make sure all the textures are created
            foreach (Face face in meshToBuildListFor.Faces)
            {
                if (face.GetTexture(0) != null)
                {
                    ImageGlPlugin.GetImageGlPlugin(face.GetTexture(0), true);
                }

                if (subMeshs.Count == 0 || subMeshs[subMeshs.Count-1].texture != face.GetTexture(0))
                {
                    SubMesh newSubMesh = new SubMesh();
                    newSubMesh.texture = face.GetTexture(0);
                    subMeshs.Add(newSubMesh);

#if USE_VBO
                    if (currentSubMesh != null)
                    {
                        CreateVBOForSubMesh(vertexDatas, currentSubMesh);
                        vertexDatas.Clear();
                    }
                    currentSubMesh = subMeshs[subMeshs.Count - 1];
#else
                    currentSubMesh = subMeshs[subMeshs.Count - 1];
                    vertexDatas = currentSubMesh.vertexDatas;
#endif
                }

                Vector2[] textureUV = new Vector2[2];
                Vector3[] position = new Vector3[2];
                int vertexIndex = 0;
                foreach (FaceEdge faceEdge in face.FaceEdgeIterator())
                {
                    if (vertexIndex < 2)
                    {
                        textureUV[vertexIndex] = faceEdge.GetUVs(0);
                        position[vertexIndex] = faceEdge.vertex.Position;
                    }
                    else
                    {
#if INTERLIEVED_VERTEX_DATA
                        VertexData tempVertex;
                        tempVertex.textureU = (float)textureUV[0].x; tempVertex.textureV = (float)textureUV[0].y;
                        tempVertex.positionsX = (float)position[0].x; tempVertex.positionsY = (float)position[0].y; tempVertex.positionsZ = (float)position[0].z;
                        tempVertex.normalsX = (float)face.normal.x; tempVertex.normalsY = (float)face.normal.y; tempVertex.normalsZ = (float)face.normal.z;
                        vertexDatas.Add(tempVertex);

                        tempVertex.textureU = (float)textureUV[1].x; tempVertex.textureV = (float)textureUV[1].y;
                        tempVertex.positionsX = (float)position[1].x; tempVertex.positionsY = (float)position[1].y; tempVertex.positionsZ = (float)position[1].z;
                        tempVertex.normalsX = (float)face.normal.x; tempVertex.normalsY = (float)face.normal.y; tempVertex.normalsZ = (float)face.normal.z;
                        vertexDatas.Add(tempVertex);

                        Vector2 textureUV2 = faceEdge.GetUVs(0);
                        Vector3 position2 = faceEdge.vertex.Position;
                        tempVertex.textureU = (float)textureUV2.x; tempVertex.textureV = (float)textureUV2.y;
                        tempVertex.positionsX = (float)position2.x; tempVertex.positionsY = (float)position2.y; tempVertex.positionsZ = (float)position2.z;
                        tempVertex.normalsX = (float)face.normal.x; tempVertex.normalsY = (float)face.normal.y; tempVertex.normalsZ = (float)face.normal.z;
                        vertexDatas.Add(tempVertex);
#else
                        currentSubMesh.textureUVs.Add((float)textureUV[0].x); currentSubMesh.textureUVs.Add((float)textureUV[0].y);
                        currentSubMesh.positions.Add((float)position[0].x); currentSubMesh.positions.Add((float)position[0].y); currentSubMesh.positions.Add((float)position[0].z);
                        currentSubMesh.normals.Add((float)face.normal.x); currentSubMesh.normals.Add((float)face.normal.y); currentSubMesh.normals.Add((float)face.normal.z);

                        currentSubMesh.textureUVs.Add((float)textureUV[1].x); currentSubMesh.textureUVs.Add((float)textureUV[1].y);
                        currentSubMesh.positions.Add((float)position[1].x); currentSubMesh.positions.Add((float)position[1].y); currentSubMesh.positions.Add((float)position[1].z);
                        currentSubMesh.normals.Add((float)face.normal.x); currentSubMesh.normals.Add((float)face.normal.y); currentSubMesh.normals.Add((float)face.normal.z);

                        Vector2 textureUV2 = faceEdge.GetUVs(0);
                        Vector3 position2 = faceEdge.vertex.Position;
                        currentSubMesh.textureUVs.Add((float)textureUV2.x); currentSubMesh.textureUVs.Add((float)textureUV2.y);
                        currentSubMesh.positions.Add((float)position2.x); currentSubMesh.positions.Add((float)position2.y); currentSubMesh.positions.Add((float)position2.z);
                        currentSubMesh.normals.Add((float)face.normal.x); currentSubMesh.normals.Add((float)face.normal.y); currentSubMesh.normals.Add((float)face.normal.z);
#endif

                        textureUV[1] = faceEdge.GetUVs(0);
                        position[1] = faceEdge.vertex.Position;
                    }

                    vertexIndex++;
                }
            }

            CreateVBOForSubMesh(vertexDatas, currentSubMesh);
        }

        private static void CreateVBOForSubMesh(VectorPOD<VertexData> vertexDatas, SubMesh currentSubMesh)
        {
#if USE_VBO
            currentSubMesh.count = vertexDatas.Count;
            currentSubMesh.vboHandle = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, currentSubMesh.vboHandle);
            GL.BufferData(BufferTarget.ArrayBuffer, new IntPtr(currentSubMesh.count * VertexData.Stride), vertexDatas.Array, BufferUsageHint.StaticDraw);
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
#endif
        }

        public void Render()
        {
        }
    }
}

