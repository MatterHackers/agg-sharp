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
//#define USE_GLES2
#define INTERLIEVED_VERTEX_DATA
//#define USE_VBO

using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.CompilerServices;

using MatterHackers.Agg;
using MatterHackers.Agg.Image;

using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;

#if USE_GLES2
using OpenTK.Graphics.ES20;
#else
using OpenTK.Graphics.OpenGL;
#endif

namespace MatterHackers.RenderOpenGl
{
    public static class RenderMeshToGl
    {
        private static String vertexShaderCode =
            "attribute vec4 vPosition;" +
            "void main() {" +
            "  gl_Position = vPosition;" +
            "}";
        
        private static String fragmentShaderSource =
            "precision mediump float;" +
            "uniform vec4 vColor;" +
            "void main() {" +
            "  gl_FragColor = vColor;" +
            "}";

        static NamedExecutionTimer RenderMeshToGL_DrawToGL = new NamedExecutionTimer("RenderMeshToGL_DrawToGL");
        static NamedExecutionTimer RenderMeshToGL_DrawArrays = new NamedExecutionTimer("RenderMeshToGL_DrawArrays");
        static void DrawToGL(Mesh meshToRender)
        {
#if USE_GLES2
            int vertexShader = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(vertexShader, vertexShaderCode);
            GL.CompileShader(vertexShader);

            int fragmentShader = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(fragmentShader, fragmentShaderSource);
            GL.CompileShader(fragmentShader);

            GL.CompileShader(vertexShader);

            GL.UseProgram(vertexShader);
            GL.UseProgram(fragmentShader);
#endif
            RenderMeshToGL_DrawToGL.Start();
            GLMeshPlugin glMeshPlugin = GLMeshPlugin.GetGLMeshPlugin(meshToRender);
            for (int i = 0; i < glMeshPlugin.subMeshs.Count; i++)
            {
                SubMesh subMesh = glMeshPlugin.subMeshs[i];
                // Make sure the GLMeshPlugin has a reference to hold onto the image so it does not go away before this.
                if (subMesh.texture != null)
                {
                    ImageGlPlugin glPlugin = ImageGlPlugin.GetImageGlPlugin(subMesh.texture, true);
                    GL.Enable(EnableCap.Texture2D);
                    GL.BindTexture(TextureTarget.Texture2D, glPlugin.GLTextureHandle);
                }
                else
                {
                    GL.Disable(EnableCap.Texture2D);
                }

#if INTERLIEVED_VERTEX_DATA
#if USE_VBO
                GL.BindBuffer(BufferTarget.ArrayBuffer, subMesh.vboHandle);
                GL.InterleavedArrays(InterleavedArrayFormat.T2fN3fV3f, 0, new IntPtr());
#else
                GL.InterleavedArrays(InterleavedArrayFormat.T2fN3fV3f, 0, subMesh.vertexDatas.Array);
#endif
#endif
                if (subMesh.texture != null)
                {
#if INTERLIEVED_VERTEX_DATA
                    //GL.TexCoordPointer(2, TexCoordPointerType.Float, VertexData.Stride, subMesh.vertexDatas.Array);
#else
                    GL.TexCoordPointer(2, TexCoordPointerType.Float, 0, subMesh.textureUVs.Array);
#endif
                    //GL.EnableClientState(ArrayCap.TextureCoordArray);
                }
                else
                {
                    GL.DisableClientState(ArrayCap.TextureCoordArray);
                }

#if INTERLIEVED_VERTEX_DATA
                //GL.VertexPointer(3, VertexPointerType.Float, VertexData.Stride, subMesh.vertexDatas.Array);
                //GL.NormalPointer(NormalPointerType.Float, VertexData.Stride, subMesh.vertexDatas.Array);
#else
                GL.VertexPointer(3, VertexPointerType.Float, 0, subMesh.positions.Array);
                GL.NormalPointer(NormalPointerType.Float, 0, subMesh.normals.Array);
#endif
                //GL.EnableClientState(ArrayCap.VertexArray);
                //GL.EnableClientState(ArrayCap.NormalArray);

                RenderMeshToGL_DrawArrays.Start();
#if INTERLIEVED_VERTEX_DATA
#if USE_VBO
                GL.DrawArrays(PrimitiveType.Triangles, 0, subMesh.count);
                GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
#else
                GL.DrawArrays(BeginMode.Triangles, 0, subMesh.vertexDatas.Count);
#endif
#else
                GL.DrawArrays(PrimitiveType.Triangles, 0, subMesh.positions.Count / 3);
#endif
                RenderMeshToGL_DrawArrays.Stop();

                GL.DisableClientState(ArrayCap.NormalArray);
                GL.DisableClientState(ArrayCap.VertexArray);

                if (subMesh.texture != null)
                {
                    GL.DisableClientState(ArrayCap.TextureCoordArray);
                }
            }
            RenderMeshToGL_DrawToGL.Stop();
        }

        static void DrawWithWireOverlay(Mesh meshToRender)
        {
#if USE_GLES2
            GLMeshWireframePlugin glMeshPlugin = GLMeshWireframePlugin.GetGLMeshWireframePlugin(meshToRender);

            GL.Enable(EnableCap.Blend);
            GL.Disable(EnableCap.Lighting);
            GL.Enable(EnableCap.Texture2D);
            GL.BindTexture(TextureTarget.Texture2D, glMeshPlugin.textureHandle);

            GL.TexCoordPointer(2, TexCoordPointerType.Float, 0, glMeshPlugin.textureUVs.Array);
            GL.EnableClientState(EnableCap.TextureCoordArray);

            GL.VertexPointer(3, VertexPointerType.Float, 0, glMeshPlugin.positions.Array);
            GL.EnableClientState(EnableCap.VertexArray);

            GL.DrawArrays(PrimitiveType.Lines, 0, glMeshPlugin.positions.Count / 3);

            GL.Disable(EnableCap.Texture2D);
            GL.DisableClientState(EnableCap.TextureCoordArray);
            GL.DisableClientState(EnableCap.VertexArray);
            GL.Disable(EnableCap.Blend);
#else
            GLMeshPlugin glMeshPlugin = GLMeshPlugin.GetGLMeshPlugin(meshToRender);

            GL.Enable(EnableCap.PolygonOffsetFill);
            GL.PolygonOffset(1, 1);

            DrawToGL(meshToRender);

            GL.Color4(0.0f, 0.0f, 0.0f, 1.0f);

            GL.PolygonOffset(0, 0);
            GL.Disable(EnableCap.PolygonOffsetFill);
            GL.Disable(EnableCap.Lighting);

            GL.Begin(BeginMode.Lines);
            foreach (MeshEdge edge in meshToRender.meshEdges)
            {
#if false // a fun hack to show only the edges of the parts rather than all the polygon edges
                if (edge.GetNumFacesSharingEdge() == 2)
                {
                    FaceEdge firstFaceEdge = edge.firstFaceEdge;
                    FaceEdge nextFaceEdge = edge.firstFaceEdge.radialNextFaceEdge;
                    double angle = Vector3.CalculateAngle(firstFaceEdge.face.normal, nextFaceEdge.face.normal);
                    if (angle > MathHelper.Tau * .1)
                    {
                    GL.Color4(0.0, 0.0, 0.0, 1.0);
                    GL.Color4(1.0, 1.0, 1.0, 1.0);
                    GL.Vertex3(edge.vertex1.Position.x, edge.vertex1.Position.y, edge.vertex1.Position.z);
                    GL.Vertex3(edge.vertex2.Position.x, edge.vertex2.Position.y, edge.vertex2.Position.z);
                    }
                }
                else
                {
                    GL.Color4(1.0, 1.0, 1.0, 1.0);
                    GL.Vertex3(edge.vertex1.Position.x, edge.vertex1.Position.y, edge.vertex1.Position.z);
                    GL.Vertex3(edge.vertex2.Position.x, edge.vertex2.Position.y, edge.vertex2.Position.z);
                }
#else
                if (edge.GetNumFacesSharingEdge() == 2)
                {
                    GL.Color4(0.0, 0.0, 0.0, 1.0);
                }
                else
                {
                    GL.Color4(1.0, 1.0, 1.0, 1.0);
                }
                GL.Vertex3(edge.vertex1.Position.x, edge.vertex1.Position.y, edge.vertex1.Position.z);
                GL.Vertex3(edge.vertex2.Position.x, edge.vertex2.Position.y, edge.vertex2.Position.z);
#endif
            }
            GL.End();

            GL.Enable(EnableCap.Lighting);
#endif
        }

        public static void Render(Mesh meshToRender, IColorType partColor, bool overlayWireFrame = false)
        {
            Render(meshToRender, partColor, Matrix4X4.Identity, overlayWireFrame);
        }

        static NamedExecutionTimer RenderMeshToGL_Render = new NamedExecutionTimer("RenderMeshToGL_Render");
        public static void Render(Mesh meshToRender, IColorType partColor, Matrix4X4 transform, bool overlayWireFrame = false)
        {
            RenderMeshToGL_Render.Start();
            if (meshToRender != null)
            {
                GL.Color4(partColor.Red0To1, partColor.Green0To1, partColor.Blue0To1, partColor.Alpha0To1);

                if (partColor.Alpha0To1 < 1)
                {
                    GL.Enable(EnableCap.Blend);
                }
                else
                {
                    GL.Disable(EnableCap.Blend);
                }

                GL.MatrixMode(MatrixMode.Modelview);
                GL.PushMatrix();
                GL.MultMatrix(transform.GetAsFloatArray());

                if (overlayWireFrame)
                {
                    DrawWithWireOverlay(meshToRender);
                }
                else
                {
                    DrawToGL(meshToRender);
                }

                GL.PopMatrix();
            }
            RenderMeshToGL_Render.Stop();
        }
    }
}

