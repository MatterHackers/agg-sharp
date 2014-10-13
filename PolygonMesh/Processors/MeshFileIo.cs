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
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Threading;
using System.Globalization;
using System.Text.RegularExpressions;

using MatterHackers.Agg;
using MatterHackers.PolygonMesh;
using MatterHackers.PolygonMesh.Csg;
using MatterHackers.VectorMath;

namespace MatterHackers.PolygonMesh.Processors
{
    public class MeshOutputSettings
    {
        public enum OutputType { Ascii, Binary };
        public OutputType OutputTypeSetting = OutputType.Binary;
        public Dictionary<string, string> MetaDataKeyValue = new Dictionary<string, string>();

        public MeshOutputSettings()
        {
        }

        public MeshOutputSettings(OutputType outputTypeSetting, string[] metaDataKeyValuePairs)
        {
            this.OutputTypeSetting = outputTypeSetting;
            for (int i = 0; i < metaDataKeyValuePairs.Length / 2; i++)
            {
                MetaDataKeyValue.Add(metaDataKeyValuePairs[i * 2], metaDataKeyValuePairs[i * 2 + 1]);
            }
        }
    }

    public static class MeshFileIo
    {
        public static string ValidFileExtensions()
        {
            return ".STL;.AMF";
        }

        public static List<MeshGroup> Load(string meshPathAndFileName, ReportProgress reportProgress = null)
        {
            switch (Path.GetExtension(meshPathAndFileName).ToUpper())
            {
                case ".STL":
                    return StlProcessing.Load(meshPathAndFileName, reportProgress);

                case ".AMF":
                    return AmfProcessing.Load(meshPathAndFileName, reportProgress);

                default:
                    return null;
            }
        }

        public static bool Save(List<MeshGroup> meshGroupsToSave, string meshPathAndFileName, MeshOutputSettings outputInfo = null)
        {
            switch (Path.GetExtension(meshPathAndFileName).ToUpper())
            {
                case ".STL":
                    Mesh mesh = DoMerge(meshGroupsToSave, false);
                    return StlProcessing.Save(mesh, meshPathAndFileName, outputInfo);

                case ".AMF":
                    return AmfProcessing.Save(meshGroupsToSave, meshPathAndFileName, outputInfo);

                default:
                    return false;
            }
        }

        public static Mesh DoMerge(List<MeshGroup> meshGroupsToMerge, bool doCSGMerge = false)
        {
            Mesh allPolygons = new Mesh();
            if (doCSGMerge)
            {
                foreach (MeshGroup meshGroup in meshGroupsToMerge)
                {
                    foreach (Mesh mesh in meshGroup.Meshes)
                    {
                        allPolygons = CsgOperations.PerformOperation(allPolygons, mesh, CsgNode.Union);
                    }
                }
            }
            else
            {
                foreach (MeshGroup meshGroup in meshGroupsToMerge)
                {
                    foreach (Mesh mesh in meshGroup.Meshes)
                    {
                        foreach (Face face in mesh.Faces)
                        {
                            List<Vertex> faceVertices = new List<Vertex>();
                            foreach (FaceEdge faceEdgeToAdd in face.FaceEdges())
                            {
                                // we allow duplicates (the true) to make sure we are not changing the loaded models acuracy.
                                Vertex newVertex = allPolygons.CreateVertex(faceEdgeToAdd.firstVertex.Position, true, true);
                                faceVertices.Add(newVertex);
                            }

                            // we allow duplicates (the true) to make sure we are not changing the loaded models acuracy.
                            allPolygons.CreateFace(faceVertices.ToArray(), true);
                        }
                    }
                }

                allPolygons.CleanAndMergMesh();
            }

            return allPolygons;
        }
    }
}
