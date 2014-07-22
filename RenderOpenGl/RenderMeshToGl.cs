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
//#define USE_VBO


using MatterHackers.Agg;
using MatterHackers.PolygonMesh;
//using MatterHackers.RenderOpenGl.OpenGl;
using OpenTK.Graphics.OpenGL;
using MatterHackers.VectorMath;

namespace MatterHackers.RenderOpenGl
{
    public enum RenderTypes { Hidden, Shaded, Outlines, Polygons };

    public static class RenderMeshToGl
    {
#if USE_GLES2
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
#endif

        static void DrawToGL(Mesh meshToRender)
        {
			#if USE_OPENGL
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
            GLMeshTrianglePlugin glMeshPlugin = GLMeshTrianglePlugin.Get(meshToRender);
            for (int i = 0; i < glMeshPlugin.subMeshs.Count; i++)
            {

				SubTriangleMesh subMesh = glMeshPlugin.subMeshs[i];
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


#if USE_VBO
                GL.BindBuffer(BufferTarget.ArrayBuffer, subMesh.vboHandle);
                GL.InterleavedArrays(InterleavedArrayFormat.T2fN3fV3f, 0, new IntPtr());
#else
                GL.InterleavedArrays(InterleavedArrayFormat.T2fN3fV3f, 0, subMesh.vertexDatas.Array);
#endif
                if (subMesh.texture != null)
                {
                    //GL.TexCoordPointer(2, TexCoordPointerType.Float, VertexData.Stride, subMesh.vertexDatas.Array);
                    //GL.EnableClientState(ArrayCap.TextureCoordArray);
                }
                else
                {
                    GL.DisableClientState(ArrayCap.TextureCoordArray);
                }

                //GL.VertexPointer(3, VertexPointerType.Float, VertexData.Stride, subMesh.vertexDatas.Array);
                //GL.NormalPointer(NormalPointerType.Float, VertexData.Stride, subMesh.vertexDatas.Array);
                //GL.EnableClientState(ArrayCap.VertexArray);
                //GL.EnableClientState(ArrayCap.NormalArray);

#if USE_VBO
                GL.DrawArrays(PrimitiveType.Triangles, 0, subMesh.count);
                GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
#else
                GL.DrawArrays(BeginMode.Triangles, 0, subMesh.vertexDatas.Count);
#endif

                GL.DisableClientState(ArrayCap.NormalArray);
                GL.DisableClientState(ArrayCap.VertexArray);

                if (subMesh.texture != null)
                {
                    GL.DisableClientState(ArrayCap.TextureCoordArray);
                }
            }
			#endif
        }

        static void DrawWithWireOverlay(Mesh meshToRender, RenderTypes renderType)
        {
#if USE_GLES2
			#if USE_OPENGL
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
			#endif
#else
			#if USE_OPENGL
			GLMeshTrianglePlugin glMeshPlugin = GLMeshTrianglePlugin.Get(meshToRender);

            GL.Enable(EnableCap.PolygonOffsetFill);
            GL.PolygonOffset(1, 1);

            DrawToGL(meshToRender);

            GL.Color4(0.0f, 0.0f, 0.0f, 1.0f);

            GL.PolygonOffset(0, 0);
            GL.Disable(EnableCap.PolygonOffsetFill);
            GL.Disable(EnableCap.Lighting);
			#endif
#if true
			#if USE_OPENGL
			GL.DisableClientState(ArrayCap.TextureCoordArray);
            GLMeshWirePlugin glWireMeshPlugin = null;
            if (renderType == RenderTypes.Outlines)
            {
                glWireMeshPlugin = GLMeshWirePlugin.Get(meshToRender, MathHelper.Tau / 8);
            }
            else
            {
                glWireMeshPlugin = GLMeshWirePlugin.Get(meshToRender);
            }

            VectorPOD<WireVertexData> edegLines = glWireMeshPlugin.edgeLinesData;
            GL.InterleavedArrays(InterleavedArrayFormat.V3f, 0, edegLines.Array);
            GL.DrawArrays(BeginMode.Lines, 0, edegLines.Count);

            //GL.Color4(1.0f, 1.0f, 1.0f, 1.0f);
            //VectorPOD<WireVertexData> nonManifoldEdges = glWireMeshPlugin.nonManifoldData;
            //GL.InterleavedArrays(InterleavedArrayFormat.V3f, 0, nonManifoldEdges.Array);
            //GL.DrawArrays(BeginMode.Lines, 0, nonManifoldEdges.Count);

            GL.DisableClientState(ArrayCap.NormalArray);
            GL.DisableClientState(ArrayCap.VertexArray);
			#endif
#else
			#if USE_OPENGL
			GL.Begin(BeginMode.Lines);
            foreach (MeshEdge edge in meshToRender.meshEdges)
            {
                if (renderType == RenderTypes.Outlines)
                {
                    if (edge.GetNumFacesSharingEdge() == 2)
                    {
                        FaceEdge firstFaceEdge = edge.firstFaceEdge;
                        FaceEdge nextFaceEdge = edge.firstFaceEdge.radialNextFaceEdge;
                        double angle = Vector3.CalculateAngle(firstFaceEdge.containingFace.normal, nextFaceEdge.containingFace.normal);
                        if (angle > MathHelper.Tau * .1)
                        {
                            GL.Color4(0.0, 0.0, 0.0, 1.0);
                            GL.Color4(1.0, 1.0, 1.0, 1.0);
                            GL.Vertex3(edge.VertexOnEnd[0].Position.x, edge.VertexOnEnd[0].Position.y, edge.VertexOnEnd[0].Position.z);
                            GL.Vertex3(edge.VertexOnEnd[1].Position.x, edge.VertexOnEnd[1].Position.y, edge.VertexOnEnd[1].Position.z);
                        }
                    }
                    else
                    {
                        GL.Color4(1.0, 1.0, 1.0, 1.0);
                        GL.Vertex3(edge.VertexOnEnd[0].Position.x, edge.VertexOnEnd[0].Position.y, edge.VertexOnEnd[0].Position.z);
                        GL.Vertex3(edge.VertexOnEnd[1].Position.x, edge.VertexOnEnd[1].Position.y, edge.VertexOnEnd[1].Position.z);
                    }
                }
                else
                {
                    if (edge.GetNumFacesSharingEdge() == 2)
                    {
                        GL.Color4(0.0, 0.0, 0.0, 1.0);
                    }
                    else
                    {
                        GL.Color4(1.0, 1.0, 1.0, 1.0);
                    }

                    GL.Vertex3(edge.VertexOnEnd[0].Position.x, edge.VertexOnEnd[0].Position.y, edge.VertexOnEnd[0].Position.z);
                    GL.Vertex3(edge.VertexOnEnd[1].Position.x, edge.VertexOnEnd[1].Position.y, edge.VertexOnEnd[1].Position.z);
                }
            }
            GL.End();
			#endif
#endif
			#if USE_OPENGL
            GL.Enable(EnableCap.Lighting);
			#endif
#endif
        }

        public static void Render(Mesh meshToRender, IColorType partColor, RenderTypes renderType = RenderTypes.Shaded)
        {
            Render(meshToRender, partColor, Matrix4X4.Identity, renderType);
        }

        public static void Render(Mesh meshToRender, IColorType partColor, Matrix4X4 transform, RenderTypes renderType)
        {
            if (meshToRender != null)
            {
				#if USE_OPENGL
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

                switch (renderType)
                {
                    case RenderTypes.Hidden:
                        break;

                    case RenderTypes.Shaded:
                        DrawToGL(meshToRender);
                        break;

                    case RenderTypes.Polygons:
                    case RenderTypes.Outlines:
                        DrawWithWireOverlay(meshToRender, renderType);
                        break;
                }

                GL.PopMatrix();
				#endif
            }
        }
    }
}

