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
using System.Xml;
using System.Globalization;
using System.Text.RegularExpressions;
using ICSharpCode.SharpZipLib.Zip;

using MatterHackers.Agg;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;

namespace MatterHackers.PolygonMesh.Processors
{
    public static class AmfProcessing
    {
        public static bool Save(List<MeshGroup> meshToSave, string fileName, MeshOutputSettings outputInfo = null)
        {
            using (FileStream file = new FileStream(fileName, FileMode.Create, FileAccess.Write))
            {
                return Save(meshToSave, file, outputInfo);
            }
        }

        static string Indent(int index)
        {
            return new String(' ', index * 2);
        }

        public static bool Save(List<MeshGroup> meshToSave, Stream stream, MeshOutputSettings outputInfo)
        {
            TextWriter amfFile = new StreamWriter(stream);
            amfFile.WriteLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            amfFile.WriteLine("<amf unit=\"millimeter\" version=\"1.1\">");
            if(outputInfo != null)
            {
                foreach(KeyValuePair<string, string> metaData in outputInfo.MetaDataKeyValue)
                {
                    amfFile.WriteLine(Indent(1) + "<metadata type=\"{0}\">{1}</metadata>".FormatWith(metaData.Key, metaData.Value));
                }
            }
            {
                int objectId = 1;
                foreach (MeshGroup meshGroup in meshToSave)
                {
                    amfFile.WriteLine(Indent(1) + "<object id=\"{0}\">".FormatWith(objectId++));
                    {
                        int vertexCount = 0;
                        List<int> meshVertexStart = new List<int>();
                        amfFile.WriteLine(Indent(2) + "<mesh>");
                        {
                            amfFile.WriteLine(Indent(3) + "<vertices>");
                            {
                                foreach (Mesh mesh in meshGroup.Meshes)
                                {
                                    meshVertexStart.Add(vertexCount);
                                    foreach (Vertex vertex in mesh.Vertices)
                                    {
                                        Vector3 position = vertex.Position;
                                        amfFile.WriteLine(Indent(4) + "<vertex>");
                                        {
                                            amfFile.WriteLine(Indent(5) + "<coordinates>");
                                            amfFile.WriteLine(Indent(6) + "<x>{0}</x>".FormatWith(position.x));
                                            amfFile.WriteLine(Indent(6) + "<y>{0}</y>".FormatWith(position.y));
                                            amfFile.WriteLine(Indent(6) + "<z>{0}</z>".FormatWith(position.z));
                                            amfFile.WriteLine(Indent(5) + "</coordinates>");
                                        }
                                        amfFile.WriteLine(Indent(4) + "</vertex>");
                                        vertexCount++;
                                    }
                                }
                            }
                            amfFile.WriteLine(Indent(3) + "</vertices>");
                            for (int meshIndex = 0; meshIndex < meshGroup.Meshes.Count; meshIndex++)
                            {
                                int firstVertexIndex = meshVertexStart[meshIndex];
                                Mesh mesh = meshGroup.Meshes[meshIndex];
                                MeshMaterialData material = MeshMaterialData.Get(mesh);
                                if (material.MaterialIndex == -1)
                                {
                                    amfFile.WriteLine(Indent(3) + "<volume>");
                                }
                                else
                                {
                                    amfFile.WriteLine(Indent(3) + "<volume materialid=\"{0}\">".FormatWith(material.MaterialIndex));
                                }
                                {
                                    for(int faceIndex = 0; faceIndex < mesh.Faces.Count; faceIndex++)
                                    {
                                        Face face = mesh.Faces[faceIndex];
                                        List<Vertex> positionsCCW = new List<Vertex>();
                                        foreach (FaceEdge faceEdge in face.FaceEdges())
                                        {
                                            positionsCCW.Add(faceEdge.firstVertex);
                                        }

                                        int numPolys = positionsCCW.Count - 2;
                                        int secondIndex = 1;
                                        int thirdIndex = 2;
                                        for (int polyIndex = 0; polyIndex < numPolys; polyIndex++)
                                        {
                                            amfFile.WriteLine(Indent(4) + "<triangle>");
                                            foreach (FaceEdge faceEdge in face.FaceEdges())
                                            {
                                                amfFile.WriteLine(Indent(5) + "<v1>{0}</v1>".FormatWith(firstVertexIndex + mesh.Vertices.IndexOf(positionsCCW[0])));
                                                amfFile.WriteLine(Indent(5) + "<v2>{0}</v2>".FormatWith(firstVertexIndex + mesh.Vertices.IndexOf(positionsCCW[secondIndex])));
                                                amfFile.WriteLine(Indent(5) + "<v3>{0}</v3>".FormatWith(firstVertexIndex + mesh.Vertices.IndexOf(positionsCCW[thirdIndex])));
                                            }
                                            amfFile.WriteLine(Indent(4) + "</triangle>");

                                            secondIndex = thirdIndex;
                                            thirdIndex++;
                                        }
                                    }
                                }
                                amfFile.WriteLine(Indent(3) + "</volume>");
                            }
                        }
                        amfFile.WriteLine(Indent(2) + "</mesh>");
                    }
                    amfFile.WriteLine(Indent(1) + "</object>");
                }

                HashSet<int> materials = new HashSet<int>();
                foreach (MeshGroup meshGroup in meshToSave)
                {
                    foreach(Mesh mesh in meshGroup.Meshes)
                    {
                        MeshMaterialData material = MeshMaterialData.Get(mesh);
                        if (material.MaterialIndex != -1)
                        {
                            materials.Add(material.MaterialIndex);
                        }
                    }
                }

                foreach(int material in materials)
                {
                    amfFile.WriteLine(Indent(1) + "<material id=\"{0}\">".FormatWith(material));
                    amfFile.WriteLine(Indent(2) + "<metadata type=\"Name\">Material {0}</metadata>".FormatWith(material));
                    amfFile.WriteLine(Indent(1) + "</material>");
                }
            }
            amfFile.WriteLine("</amf>");
            amfFile.Flush();
            return true;
        }

        public static List<MeshGroup> Load(string fileName, ReportProgressRatio reportProgress = null)
        {
            List<MeshGroup> loadedMesh = null;
            if (Path.GetExtension(fileName).ToUpper() == ".AMF")
            {
                try
                {
                    if (File.Exists(fileName))
                    {
                        Stream fileStream = File.OpenRead(fileName);

                        loadedMesh = ParseFileContents(fileStream, reportProgress);
                    }
                }
#if DEBUG
                catch (IOException)
                {
                    return null;
                }
#else
                catch (Exception)
                {
                    return null;
                }
#endif
            }

            return loadedMesh;
        }

        public static List<MeshGroup> Load(Stream fileStream, ReportProgressRatio reportProgress = null)
        {
            List<MeshGroup> loadedMeshes;
            try
            {
                loadedMeshes = ParseFileContents(fileStream, reportProgress);
            }
#if DEBUG
            catch (IOException)
            {
                return null;
            }
#else
            catch (Exception)
            {
                return null;
            }
#endif

            return loadedMeshes;
        }

        static bool IsZipFile(Stream fs)
        {
            int elements = 4;
            if (fs.Length < elements)
            {
                return false;
            }

            string zipToken = "50-4B-03-04";
            byte[] fileToken = new byte[elements];

            fs.Position = 0;
            int bytesRead = fs.Read(fileToken, 0, elements);
            fs.Position = 0;

            if (BitConverter.ToString(fileToken) == zipToken)
            {
                return true;
            }

            return false;
        }

        internal class ProgressData
        {
            ReportProgressRatio reportProgress;
            Stopwatch maxProgressReport = new Stopwatch();
            Stream positionStream;
            long bytesInFile;
            bool loadCanceled;
            internal bool LoadCanceled { get { return loadCanceled; } }

            internal ProgressData(Stream positionStream, ReportProgressRatio reportProgress)
            {
                this.reportProgress = reportProgress;
                this.positionStream = positionStream;
                maxProgressReport.Start();
                bytesInFile = (long)positionStream.Length;
            }

            internal void ReportProgress0To50(out bool continueProcessing)
            {
                if (reportProgress != null && maxProgressReport.ElapsedMilliseconds > 200)
                {
                    reportProgress(positionStream.Position / (double)bytesInFile * .5, "Loading Mesh", out continueProcessing);
                    if (!continueProcessing)
                    {
                        loadCanceled = true;
                    }
                    maxProgressReport.Restart();
                }
                else
                {
                    continueProcessing = true;
                }
            }
        }

        public static List<MeshGroup> ParseFileContents(Stream amfStream, ReportProgressRatio reportProgress)
        {
            Stopwatch time = new Stopwatch();
            time.Start(); 
            
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

            double parsingFileRatio = .5;

            if (amfStream == null)
            {
                return null;
            }

            List<MeshGroup> meshGroups = null;

            // do the loading
            {
                Stream amfCompressedStream = GetCompressedStreamIfRequired(amfStream);
                XmlReader xmlTree = XmlReader.Create(amfCompressedStream);
                while (xmlTree.Read())
                {
                    if (xmlTree.Name == "amf")
                    {
                        break;
                    }
                }
                double scale = GetScaling(xmlTree);

                ProgressData progressData = new ProgressData(amfStream, reportProgress);

                meshGroups = new List<MeshGroup>();

                while (xmlTree.Read())
                {
                    if (xmlTree.Name == "object")
                    {
                        using(XmlReader objectTree = xmlTree.ReadSubtree())
                        {
                            meshGroups.Add(ReadObject(objectTree, scale, progressData));
                            if (progressData.LoadCanceled)
                            {
                                return null;
                            }
                        }
                    }
                }
            }

#if true
            // merge all the vetexes that are in the same place together
            int totalMeshes = 0;
            foreach (MeshGroup meshGroup in meshGroups)
            {
                foreach (Mesh mesh in meshGroup.Meshes)
                {
                    totalMeshes++;
                }
            }

            double currentMeshProgress = 0;
            double ratioLeftToUse = 1 - parsingFileRatio;
            double progressPerMesh = 1.0 / totalMeshes * ratioLeftToUse;
            foreach (MeshGroup meshGroup in meshGroups)
            {
                foreach (Mesh mesh in meshGroup.Meshes)
                {
                    bool keepProcessing = true;
                    mesh.CleanAndMergMesh(
                        (double progress0To1, string processingState, out bool continueProcessing) =>
                        {
                            if (reportProgress != null)
                            {
                                double currentTotalProgress = parsingFileRatio + currentMeshProgress;
                                reportProgress(currentTotalProgress + progress0To1 * progressPerMesh, processingState, out continueProcessing);
                                keepProcessing = continueProcessing;
                            }
                            else
                            {
                                continueProcessing = true;
                            }
                        }
                        );
                    if (!keepProcessing)
                    {
                        amfStream.Close();
                        return null;
                    }
                    currentMeshProgress += progressPerMesh;
                }
            }
#endif

            time.Stop();
            Debug.WriteLine(string.Format("AMF Load in {0:0.00}s", time.Elapsed.TotalSeconds));

            amfStream.Close();
            return meshGroups;
        }

        private static MeshGroup ReadObject(XmlReader xmlTree, double scale, ProgressData progressData)
        {
            MeshGroup meshGroup = new MeshGroup();
            while (xmlTree.Read())
            {
                if (xmlTree.Name == "mesh")
                {
                    using (XmlReader meshTree = xmlTree.ReadSubtree())
                    {
                        ReadMesh(meshTree, meshGroup, scale, progressData);
                        if (progressData.LoadCanceled)
                        {
                            return null;
                        }
                    }
                }
            }

            return meshGroup;
        }

        private static void ReadMesh(XmlReader xmlTree, MeshGroup meshGroup, double scale, ProgressData progressData)
        {
            List<Vector3> vertices = new List<Vector3>();
            while (xmlTree.Read())
            {
                switch(xmlTree.Name)
                {
                    case "vertices":
                        using (XmlReader verticesTree = xmlTree.ReadSubtree())
                        {
                            ReadVertices(verticesTree, vertices, scale, progressData);
                            if (progressData.LoadCanceled)
                            {
                                return;
                            }
                        }
                        break;

                    case "volume":
                        string materialId = xmlTree["materialid"];
                        Mesh loadedMesh = null;
                        using (XmlReader volumeTree = xmlTree.ReadSubtree())
                        {
                            loadedMesh = ReadVolume(volumeTree, vertices, progressData);
                            if (progressData.LoadCanceled)
                            {
                                return;
                            }
                            meshGroup.Meshes.Add(loadedMesh);
                        }
                        if (loadedMesh != null && materialId != null)
                        {
                            MeshMaterialData material = MeshMaterialData.Get(loadedMesh);
                            material.MaterialIndex = int.Parse(materialId);
                        }
                        break;
                }
            }
        }

        private static Mesh ReadVolume(XmlReader xmlTree, List<Vector3> vertices, ProgressData progressData)
        {
            Mesh newMesh = new Mesh();
            while (xmlTree.Read())
            {
                if (xmlTree.Name == "triangle")
                {
                    using (XmlReader triangleTree = xmlTree.ReadSubtree())
                    {
                        while (triangleTree.Read())
                        {
                            int[] indices = new int[3];
                            while (triangleTree.Read())
                            {
                                switch (triangleTree.Name)
                                {
                                    case "v1":
                                        string v1 = triangleTree.ReadString();
                                        indices[0] = int.Parse(v1);
                                        break;

                                    case "v2":
                                        string v2 = triangleTree.ReadString();
                                        indices[1] = int.Parse(v2);
                                        break;

                                    case "v3":
                                        string v3 = triangleTree.ReadString();
                                        indices[2] = int.Parse(v3);
                                        break;

                                    case "map":
                                        using (XmlReader mapTree = triangleTree.ReadSubtree())
                                        {
                                        }
                                        // a texture map, has u1...un and v1...vn
                                        break;

                                    default:
                                        break;
                                }
                            }
                            if (indices[0] != indices[1]
                                && indices[0] != indices[2]
                                && indices[1] != indices[2]
                                && vertices[indices[0]] != vertices[indices[1]]
                                && vertices[indices[1]] != vertices[indices[2]]
                                && vertices[indices[2]] != vertices[indices[0]])
                            {
                                Vertex[] triangle = new Vertex[]
                                {
                                    newMesh.CreateVertex(vertices[indices[0]], true, true),
                                    newMesh.CreateVertex(vertices[indices[1]], true, true),
                                    newMesh.CreateVertex(vertices[indices[2]], true, true),
                                };
                                newMesh.CreateFace(triangle, true);
                            }

                            bool continueProcessing;
                            progressData.ReportProgress0To50(out continueProcessing);
                            if (!continueProcessing)
                            {
                                // this is what we should do but it requires a bit more debugging.
                                return null;
                            }
                        }
                    }
                }
            }
            return newMesh;
        }

        private static void ReadVertices(XmlReader xmlTree, List<Vector3> vertices, double scale, ProgressData progressData)
        {
            while (xmlTree.Read())
            {
                if (xmlTree.Name == "vertices")
                {
                    using (XmlReader verticesTree = xmlTree.ReadSubtree())
                    {
                        while (verticesTree.Read())
                        {
                            if (xmlTree.Name == "vertex")
                            {
                                using (XmlReader vertexTree = verticesTree.ReadSubtree())
                                {
                                    while (vertexTree.Read())
                                    {
                                        if (vertexTree.Name == "coordinates")
                                        {
                                            using (XmlReader coordinatesTree = vertexTree.ReadSubtree())
                                            {
                                                Vector3 position = new Vector3();
                                                while (coordinatesTree.Read())
                                                {
                                                    switch (coordinatesTree.Name)
                                                    {
                                                        case "x":
                                                            string x = coordinatesTree.ReadString();
                                                            position.x = double.Parse(x);
                                                            break;

                                                        case "y":
                                                            string y = coordinatesTree.ReadString();
                                                            position.y = double.Parse(y);
                                                            break;

                                                        case "z":
                                                            string z = coordinatesTree.ReadString();
                                                            position.z = double.Parse(z);
                                                            break;

                                                        default:
                                                            break;
                                                    }
                                                }
                                                position *= scale;
                                                vertices.Add(position);
                                            }
                                            bool continueProcessing;
                                            progressData.ReportProgress0To50(out continueProcessing);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private static Stream GetCompressedStreamIfRequired(Stream amfStream)
        {
            if (IsZipFile(amfStream))
            {
                ZipFile zip = new ZipFile(amfStream);
                bool isValid = zip.TestArchive(false);
                foreach (ZipEntry zipEntry in zip)
                {
                    return zip.GetInputStream(zipEntry);
                }
            }

            amfStream.Position = 0;

            return amfStream;
        }

        private static double GetScaling(XmlReader xmlTree)
        {
            string units = xmlTree["unit"];
            if (units == null)
            {
                // the amf does not specify any units
                return 1;
            }

            switch (units.ToLower())
            {
                case "millimeter":
                    return 1;

                case "centimeter":
                    return 10;

                case "meter":
                    return 1000;

                case "inch":
                    return 25.4;

                case "feet":
                    return 304.8;

                case "micron":
                    return 0.001;

                default:
#if DEBUG
                    throw new NotImplementedException();
#else
                return 1;
#endif
            }
        }
    }
}
