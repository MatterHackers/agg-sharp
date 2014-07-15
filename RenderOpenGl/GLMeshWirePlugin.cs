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
using System.Text;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using MatterHackers.Agg;
using MatterHackers.Agg.Image;

using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;


#if USE_GLES
using OpenTK.Graphics.ES11;
#elif USE_OPENGL
using OpenTK.Graphics.OpenGL;
#endif

namespace MatterHackers.RenderOpenGl
{
    public struct WireVertexData
    {
        public float positionsX;
        public float positionsY;
        public float positionsZ;

        public static readonly int Stride = Marshal.SizeOf(default(WireVertexData));
    }

    public class GLMeshWirePlugin
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

        private static ConditionalWeakTable<Mesh, GLMeshWirePlugin> meshesWithCacheData = new ConditionalWeakTable<Mesh, GLMeshWirePlugin>();

        private static List<RemoveData> glDataNeedingToBeDeleted = new List<RemoveData>();

        public VectorPOD<WireVertexData> manifoldData =new VectorPOD<WireVertexData>();

        private int meshUpdateCount;
        private double nonPlanarAngleRequired;

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

        static public GLMeshWirePlugin Get(Mesh meshToGetDisplayListFor, double nonPlanarAngleRequired = 0)
        {
            GLMeshWirePlugin plugin;
            meshesWithCacheData.TryGetValue(meshToGetDisplayListFor, out plugin);

            if (plugin != null 
                && (meshToGetDisplayListFor.ChangedCount != plugin.meshUpdateCount
                || nonPlanarAngleRequired != plugin.nonPlanarAngleRequired))
            {
                plugin.meshUpdateCount = meshToGetDisplayListFor.ChangedCount;
                plugin.AddRemoveData();
                plugin.CreateRenderData(meshToGetDisplayListFor);
                plugin.meshUpdateCount = meshToGetDisplayListFor.ChangedCount;
                plugin.nonPlanarAngleRequired = nonPlanarAngleRequired;
            }

            DeleteUnusedGLResources();

            if (plugin == null)
            {
                GLMeshWirePlugin newPlugin = new GLMeshWirePlugin();
                meshesWithCacheData.Add(meshToGetDisplayListFor, newPlugin);
                newPlugin.CreateRenderData(meshToGetDisplayListFor);
                newPlugin.meshUpdateCount = meshToGetDisplayListFor.ChangedCount;

                return newPlugin;
            }

            return plugin;
        }

        private GLMeshWirePlugin()
        {
            // This is private as you can't build one of these. You have to call GetImageGLDisplayListPlugin.
        }

        void AddRemoveData()
        {
        }

        ~GLMeshWirePlugin()
        {
            AddRemoveData();
        }

        private void CreateRenderData(Mesh meshToBuildListFor)
        {
            manifoldData = new VectorPOD<WireVertexData>();
            // first make sure all the textures are created
            foreach (MeshEdge meshEdge in meshToBuildListFor.meshEdges)
            {
                WireVertexData tempVertex;
                tempVertex.positionsX = (float)meshEdge.VertexOnEnd[0].Position.x;
                tempVertex.positionsY = (float)meshEdge.VertexOnEnd[0].Position.y;
                tempVertex.positionsZ = (float)meshEdge.VertexOnEnd[0].Position.z;
                manifoldData.Add(tempVertex);

                tempVertex.positionsX = (float)meshEdge.VertexOnEnd[1].Position.x;
                tempVertex.positionsY = (float)meshEdge.VertexOnEnd[1].Position.y;
                tempVertex.positionsZ = (float)meshEdge.VertexOnEnd[1].Position.z;
                manifoldData.Add(tempVertex);
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

