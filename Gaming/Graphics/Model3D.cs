using System;
using System.Collections.Generic;
using System.Text;

using Tao.OpenGl;

using COLLADA;

namespace Gaming.Graphics
{
    public class Model3D
    {
        COLLADA.Document m_ColladaFile;

        public Model3D()
        {
        }

        public Model3D(String fileToLoad)
        {
            m_ColladaFile = new COLLADA.Document(fileToLoad);
        }

        public int[] GetTriangles(int geometryIndex, int listIndex)
        {
            return m_ColladaFile.geometries[geometryIndex].m_Mesh.m_Triangles.ps[listIndex];
        }

        public float[] GetVertices(int geometryIndex, int sourceIndex)
        {
            COLLADA.Document.Source source = m_ColladaFile.geometries[geometryIndex].m_Mesh.m_Sources[sourceIndex];
            float[] vertices = (float[])((COLLADA.Document.Array<float>)source.array).arr;
            return vertices;
        }

        public void Render()
        {
            Random shipRand = new Random(10);

            int numGeometries = m_ColladaFile.geometries.Count;
            for (int geometryIndex = 0; geometryIndex < numGeometries; geometryIndex++)
            {
                Gl.glPushMatrix();
                List<Document.TransformNode> sceneTransforms = m_ColladaFile.visualScenes[0].nodes[geometryIndex].transforms;
                for(int transformIndex = 0; transformIndex < sceneTransforms.Count; transformIndex++)
                {
                    Document.TransformNode sceneTransform = sceneTransforms[transformIndex];
                    if (sceneTransform.GetType() == typeof(Document.Translate))
                    {
                        Gl.glTranslatef(sceneTransform[0], sceneTransform[1], sceneTransform[2]);
                    }
                }

                int[] triangles = GetTriangles(geometryIndex, 0);
                float[] vertices = GetVertices(geometryIndex, 0);
                int numInputs = m_ColladaFile.geometries[0].m_Mesh.m_Triangles.Inputs.Count;
                COLLADA.Document.Input input0 = m_ColladaFile.geometries[0].m_Mesh.m_Triangles.Inputs[0];
                if (input0.semantic != "VERTEX") throw new Exception("We need to find the VERTEX offset");
                int vertexOffset = input0.offset;

                Gl.glBegin(Gl.GL_TRIANGLES);                                        // Drawing Using Triangles
                int numTrianglesIndexes = triangles.Length;
                for (int triangleVertexIndex = vertexOffset; triangleVertexIndex < numTrianglesIndexes; triangleVertexIndex += numInputs)
                {
                    int vertexIndex = triangles[triangleVertexIndex];
                    float gray = (float)shipRand.NextDouble();
                    Gl.glColor3f(gray, gray, gray);
                    Gl.glVertex3f(
                        vertices[vertexIndex * 3 + 0],
                        vertices[vertexIndex * 3 + 1],
                        vertices[vertexIndex * 3 + 2]
                        );
                }
                Gl.glEnd();                                                         // Finished Drawing The Triangle
                Gl.glPopMatrix();
            }
        }
    }
}
