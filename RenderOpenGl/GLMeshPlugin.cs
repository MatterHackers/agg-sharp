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

using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.CompilerServices;

using MatterHackers.Agg;
using MatterHackers.Agg.Image;

using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;

using OpenTK.Graphics.OpenGL;

namespace MatterHackers.RenderOpenGl
{
    public class SubMesh
    {
        public ImageBuffer texture = null;
        public VectorPOD<float> textureUVs = new VectorPOD<float>();
        public VectorPOD<float> positions = new VectorPOD<float>();
        public VectorPOD<float> normals = new VectorPOD<float>();
    }

    public class GLMeshPlugin
    {
        struct RemoveData
        {
            internal int listHandle;

            public RemoveData(int listHandle)
            {
                // TODO: Complete member initialization
                this.listHandle = listHandle;
            }
        }

        public delegate void DrawToGL(Mesh meshToRender);

        private static ConditionalWeakTable<Mesh, GLMeshPlugin> meshesWithCacheData = new ConditionalWeakTable<Mesh, GLMeshPlugin>();

        private static List<RemoveData> glDataNeedingToBeDeleted = new List<RemoveData>();

        public List<SubMesh> subMesh;

        internal int glDisplayListHandle;
        private int meshUpdateCount;

        static public void DeleteUnusedGLResources()
        {
            using (TimedLock.Lock(glDataNeedingToBeDeleted, "GLMeshPluginDeleteUnused"))
            {
                // We run this in here to ensure that we are on the correct thread and have the correct
                // glcontext realized.
                for (int i = glDataNeedingToBeDeleted.Count - 1; i >= 0; i--)
                {
                    GL.DeleteLists(glDataNeedingToBeDeleted[i].listHandle, 1);
                    glDataNeedingToBeDeleted.RemoveAt(i);
                }
            }
        }

        static public GLMeshPlugin GetGLMeshPlugin(Mesh meshToGetDisplayListFor)
        {
            GLMeshPlugin plugin;
            meshesWithCacheData.TryGetValue(meshToGetDisplayListFor, out plugin);

            using (TimedLock.Lock(glDataNeedingToBeDeleted, "GLMeshPluginUpdate"))
            {
                if (plugin != null && meshToGetDisplayListFor.ChangedCount != plugin.meshUpdateCount)
                {
                    plugin.meshUpdateCount = meshToGetDisplayListFor.ChangedCount;
                    // this could be better, but for now we will just throw it in the delete list
                    glDataNeedingToBeDeleted.Add(new RemoveData(plugin.GLDisplayList));
                    plugin.BuildDisplayList(meshToGetDisplayListFor);
                    plugin.meshUpdateCount = meshToGetDisplayListFor.ChangedCount;
                }
            }

            DeleteUnusedGLResources();

            if (plugin == null)
            {
                GLMeshPlugin newPlugin = new GLMeshPlugin();
                meshesWithCacheData.Add(meshToGetDisplayListFor, newPlugin);
                newPlugin.BuildDisplayList(meshToGetDisplayListFor);
                newPlugin.meshUpdateCount = meshToGetDisplayListFor.ChangedCount;

                return newPlugin;
            }

            return plugin;
        }

        public int GLDisplayList
        {
            get
            {
                return glDisplayListHandle;
            }
        }

        private GLMeshPlugin()
        {
            // This is private as you can't build one of these. You have to call GetImageGLDisplayListPlugin.
        }

        ~GLMeshPlugin()
        {
            using (TimedLock.Lock(glDataNeedingToBeDeleted, "~GLMeshPlugin"))
            {
                glDataNeedingToBeDeleted.Add(new RemoveData(glDisplayListHandle));
            }
        }

        private void BuildDisplayList(Mesh meshToBuildListFor)
        {
            subMesh = new List<SubMesh>();
            // first make sure all the textures are created
            foreach (Face face in meshToBuildListFor.Faces)
            {
                if (face.GetTexture(0) != null)
                {
                    ImageGlPlugin.GetImageGlPlugin(face.GetTexture(0), true);
                }

                if (subMesh.Count == 0 || subMesh[subMesh.Count-1].texture != face.GetTexture(0))
                {
                    SubMesh newGroup = new SubMesh();
                    newGroup.texture = face.GetTexture(0);
                    subMesh.Add(newGroup);
                }

                SubMesh currentSubMesh = subMesh[subMesh.Count - 1];
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

                        textureUV[1] = faceEdge.GetUVs(0);
                        position[1] = faceEdge.vertex.Position;
                    }

                    vertexIndex++;
                }
            }

            // Create the texture handle and display list handle
            glDisplayListHandle = GL.GenLists(1);
            int[] glTextures = new int[1];
            GL.GenTextures(1, glTextures);

            //Create a display list and bind a texture to it
            GL.NewList((uint)(glDisplayListHandle), ListMode.Compile);

            GL.EndList();
        }
    }
}

