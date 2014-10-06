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
        public enum OutputType { Ascii, Binary };

        public static void Save(Mesh meshToSave, string fileName, OutputType outputType = OutputType.Binary)
        {
            FileStream file = new FileStream(fileName, FileMode.Create, FileAccess.Write);

            Save(meshToSave, file, outputType);
            file.Close();
        }

        public static void Save(Mesh meshToSave, Stream stream, OutputType outputType)
        {
#if true
            TextWriter amfFile = new StreamWriter(stream);
            amfFile.WriteLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            amfFile.WriteLine("<amf unit=\"millimeter\" version=\"1.1\">");
            {
                amfFile.WriteLine("<object id=\"{0}\">");
                {
                    amfFile.WriteLine("<mesh>");
                    {
                        amfFile.WriteLine("<vertices>");
                        {
                            amfFile.WriteLine("<vertex>");
                            {
                                amfFile.WriteLine("<coordinates>");
                                amfFile.WriteLine("<x>{0}</x>");
                                amfFile.WriteLine("<y>{0}</y>");
                                amfFile.WriteLine("<z>{0}</z>");
                                amfFile.WriteLine("<coordinates>");
                            }
                            amfFile.WriteLine("<vertex>");
                        }
                        amfFile.WriteLine("<vertices>");
                        amfFile.WriteLine("<volume materialid=\"{0}\">");
                        {
                            amfFile.WriteLine("<triangle>");
                            amfFile.WriteLine("<v1>0</v1>");
                            amfFile.WriteLine("<v2>1</v2>");
                            amfFile.WriteLine("<v3>2</v3>");
                            amfFile.WriteLine("</triangle>");
                        }
                        amfFile.WriteLine("</volume>");
                    }
                    amfFile.WriteLine("</mesh>");
                }
                amfFile.WriteLine("</object>");
                amfFile.WriteLine("<material id=\"{0}\">");
                amfFile.WriteLine("</material");
            }
            amfFile.WriteLine("</amf>");
#else
            switch (outputType)
            {
                case OutputType.Ascii:
                    {
                        StreamWriter streamWriter = new StreamWriter(stream);

                        streamWriter.WriteLine("solid Default");

                        foreach (Face face in meshToSave.Faces)
                        {
                            List<Vector3> positionsCCW = new List<Vector3>();
                            foreach (FaceEdge faceEdge in face.FaceEdges())
                            {
                                positionsCCW.Add(faceEdge.firstVertex.Position);
                            }

                            int numPolys = positionsCCW.Count - 2;
                            int secondIndex = 1;
                            int thirdIndex = 2;
                            for (int polyIndex = 0; polyIndex < numPolys; polyIndex++)
                            {
                                secondIndex = thirdIndex;
                                thirdIndex++;
                            }
                        }

                        streamWriter.WriteLine("endsolid Default");

                        streamWriter.Close();
                    }
                    break;

                case OutputType.Binary:
                    using (BinaryWriter bw = new BinaryWriter(stream))
                    {
                        // 80 bytes of nothing
                        bw.Write(new Byte[80]);
                        // the number of tranigles
                        bw.Write(meshToSave.Faces.Count);
                        int binaryPolyCount = 0;
                        foreach (Face face in meshToSave.Faces)
                        {
                            List<Vector3> positionsCCW = new List<Vector3>();
                            foreach (FaceEdge faceEdge in face.FaceEdges())
                            {
                                positionsCCW.Add(faceEdge.firstVertex.Position);
                            }

                            int numPolys = positionsCCW.Count - 2;
                            int secondIndex = 1;
                            int thirdIndex = 2;
                            for (int polyIndex = 0; polyIndex < numPolys; polyIndex++)
                            {
                                binaryPolyCount++;
                                // save the normal (all 0 so it can compress better)
                                bw.Write((float)0);
                                bw.Write((float)0);
                                bw.Write((float)0);
                                // save the position
                                bw.Write((float)positionsCCW[0].x); bw.Write((float)positionsCCW[0].y); bw.Write((float)positionsCCW[0].z);
                                bw.Write((float)positionsCCW[secondIndex].x); bw.Write((float)positionsCCW[secondIndex].y); bw.Write((float)positionsCCW[secondIndex].z);
                                bw.Write((float)positionsCCW[thirdIndex].x); bw.Write((float)positionsCCW[thirdIndex].y); bw.Write((float)positionsCCW[thirdIndex].z);

                                // and the attribute
                                bw.Write((ushort)0);

                                secondIndex = thirdIndex;
                                thirdIndex++;
                            }
                        }
                        bw.BaseStream.Position = 80;
                        // the number of tranigles
                        bw.Write(binaryPolyCount);
                    }
                    break;
            }
#endif
        }

        public static Mesh Load(string fileName, ReportProgress reportProgress = null)
        {
            Mesh loadedMesh = null;
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

        public static Mesh Load(Stream fileStream, ReportProgress reportProgress = null)
        {
            Mesh loadedMesh = null;
            try
            {
                loadedMesh = ParseFileContents(fileStream, reportProgress);
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

            return loadedMesh;
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
            ReportProgress reportProgress;
            Stopwatch maxProgressReport = new Stopwatch();
            Stream positionStream;
            long bytesInFile;

            internal ProgressData(Stream positionStream, ReportProgress reportProgress)
            {
                this.reportProgress = reportProgress;
                this.positionStream = positionStream;
                maxProgressReport.Start();
                bytesInFile = (long)positionStream.Length;
            }

            internal bool ReportProgress()
            {
                if (reportProgress != null && maxProgressReport.ElapsedMilliseconds > 200)
                {
                    bool continueProcessing = reportProgress(positionStream.Position / (double)bytesInFile * .5, "Loading Mesh");
                    maxProgressReport.Restart();
                    return continueProcessing;
                }

                return true;
            }
        }

        public static Mesh ParseFileContents(Stream amfStream, ReportProgress reportProgress)
        {
            Stopwatch time = new Stopwatch();
            time.Start(); 
            
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

            double parsingFileRatio = .5;

            if (amfStream == null)
            {
                return null;
            }

            Mesh meshFromAmfFile = new Mesh();

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

                List<MeshGroup> meshGroups = new List<MeshGroup>();

                while (xmlTree.Read())
                {
                    if (xmlTree.Name == "object")
                    {
                        using(XmlReader objectTree = xmlTree.ReadSubtree())
                        {
                            meshGroups.Add(ReadObject(objectTree, scale, progressData));
                        }
                        meshFromAmfFile = meshGroups[0].Meshes[0];
                    }
                }
            }

#if true
            // merge all the vetexes that are in the same place together
            meshFromAmfFile.CleanAndMergMesh(
                (double progress0To1, string processingState) =>
                {
                    if (reportProgress != null)
                    {
                        reportProgress(parsingFileRatio + progress0To1 * (1 - parsingFileRatio), processingState);
                    }
                    return true;
                }
            );
#endif

            time.Stop();
            Debug.WriteLine(string.Format("AMF Load in {0:0.00}s", time.Elapsed.TotalSeconds));

            amfStream.Close();
            return meshFromAmfFile;
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
                        }
                        break;

                    case "volume":
                        string materialId = xmlTree["materialid"];
                        using (XmlReader volumeTree = xmlTree.ReadSubtree())
                        {
                            meshGroup.Meshes.Add(ReadVolume(volumeTree, vertices, progressData));
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
                                && indices[1] != indices[2])
                            {
                                Vertex[] triangle = new Vertex[]
                                {
                                    newMesh.CreateVertex(vertices[indices[0]], true, true),
                                    newMesh.CreateVertex(vertices[indices[1]], true, true),
                                    newMesh.CreateVertex(vertices[indices[2]], true, true),
                                };
                                newMesh.CreateFace(triangle, true);
                            }

                            progressData.ReportProgress();
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
                                            progressData.ReportProgress();
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
