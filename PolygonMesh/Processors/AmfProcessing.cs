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
                                streamWriter.WriteLine("  facet normal " + FormatForAmf(face.normal));
                                streamWriter.WriteLine("    outer loop");
                                streamWriter.WriteLine("      vertex " + FormatForAmf(positionsCCW[0]));
                                streamWriter.WriteLine("      vertex " + FormatForAmf(positionsCCW[secondIndex]));
                                streamWriter.WriteLine("      vertex " + FormatForAmf(positionsCCW[thirdIndex]));
                                streamWriter.WriteLine("    endloop");
                                streamWriter.WriteLine("  endfacet");

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

            //MemoryStream amfStream = new MemoryStream();
            //amfStreamIn.CopyTo(amfStream);

            Stopwatch maxProgressReport = new Stopwatch();
            maxProgressReport.Start();
            Mesh meshFromAmfFile = new Mesh();
            //meshFromAmfFile.MaxDistanceToConsiderVertexAsSame = .0000005;
            meshFromAmfFile.MaxDistanceToConsiderVertexAsSame = 0; // only vertices that are the exact same point will be merged.
            long bytesInFile = amfStream.Length;
            if (bytesInFile <= 80)
            {
                return null;
            }

            byte[] first160Bytes = new byte[160];
            amfStream.Read(first160Bytes, 0, 160);
            byte[] ByteOredrMark = new byte[] { 0xEF, 0xBB, 0xBF };
            int startOfString = 0;
            if (first160Bytes[0] == ByteOredrMark[0] && first160Bytes[0] == ByteOredrMark[0] && first160Bytes[0] == ByteOredrMark[0])
            {
                startOfString = 3;
            }
            string first160BytesOfAmfFile = System.Text.Encoding.UTF8.GetString(first160Bytes, startOfString, first160Bytes.Length - startOfString);
            if (first160BytesOfAmfFile.StartsWith("solid") && first160BytesOfAmfFile.Contains("facet"))
            {
                amfStream.Position = 0;
                StreamReader amfReader = new StreamReader(amfStream);
                int vectorIndex = 0;
                Vector3 vector0 = new Vector3(0, 0, 0);
                Vector3 vector1 = new Vector3(0, 0, 0);
                Vector3 vector2 = new Vector3(0, 0, 0);
                string line = amfReader.ReadLine();
                Regex onlySingleSpaces = new Regex("\\s+", RegexOptions.Compiled);
                while (line != null)
                {
                    line = onlySingleSpaces.Replace(line, " ");
                    var parts = line.Trim().Split(' ');
                    if (parts[0].Trim() == "vertex")
                    {
                        vectorIndex++;
                        switch (vectorIndex)
                        {
                            case 1:
                                vector0.x = Convert.ToDouble(parts[1]);
                                vector0.y = Convert.ToDouble(parts[2]);
                                vector0.z = Convert.ToDouble(parts[3]);
                                break;
                            case 2:
                                vector1.x = Convert.ToDouble(parts[1]);
                                vector1.y = Convert.ToDouble(parts[2]);
                                vector1.z = Convert.ToDouble(parts[3]);
                                break;
                            case 3:
                                vector2.x = Convert.ToDouble(parts[1]);
                                vector2.y = Convert.ToDouble(parts[2]);
                                vector2.z = Convert.ToDouble(parts[3]);
                                if (!Vector3.Collinear(vector0, vector1, vector2))
                                {
                                    Vertex vertex1 = meshFromAmfFile.CreateVertex(vector0, true, true);
                                    Vertex vertex2 = meshFromAmfFile.CreateVertex(vector1, true, true);
                                    Vertex vertex3 = meshFromAmfFile.CreateVertex(vector2, true, true);
                                    meshFromAmfFile.CreateFace(new Vertex[] { vertex1, vertex2, vertex3 }, true);
                                }
                                vectorIndex = 0;
                                break;
                        }
                    }
                    line = amfReader.ReadLine();

                    if (reportProgress != null && maxProgressReport.ElapsedMilliseconds > 200)
                    {
                        if (!reportProgress(amfStream.Position / (double)bytesInFile * parsingFileRatio, "Loading Polygons"))
                        {
                            amfStream.Close();
                            return null;
                        }
                        maxProgressReport.Restart();
                    }
                }
            }
            else
            {
                // load it as a binary amf
                // skip the first 80 bytes
                // read in the number of triangles
                amfStream.Position = 0;
                BinaryReader br = new BinaryReader(amfStream);
                byte[] fileContents = br.ReadBytes((int)amfStream.Length);
                int currentPosition = 80;
                uint numTriangles = System.BitConverter.ToUInt32(fileContents, currentPosition);
                long bytesForNormals = numTriangles * 3 * 4;
                long bytesForVertices = numTriangles * 3 * 4 * 3;
                long bytesForAttributs = numTriangles * 2;
                currentPosition += 4;
                long numBytesRequiredForVertexData = currentPosition + bytesForNormals + bytesForVertices + bytesForAttributs;
                if (fileContents.Length < numBytesRequiredForVertexData || numTriangles < 4)
                {
                    amfStream.Close();
                    return null;
                }
                Vector3[] vector = new Vector3[3];
                for (int i = 0; i < numTriangles; i++)
                {
                    // skip the normal 
                    currentPosition += 3 * 4;
                    for (int j = 0; j < 3; j++)
                    {
                        vector[j] = new Vector3(
                            System.BitConverter.ToSingle(fileContents, currentPosition + 0 * 4),
                            System.BitConverter.ToSingle(fileContents, currentPosition + 1 * 4),
                            System.BitConverter.ToSingle(fileContents, currentPosition + 2 * 4));
                        currentPosition += 3 * 4;
                    }
                    currentPosition += 2; // skip the attribute

                    if (reportProgress != null && maxProgressReport.ElapsedMilliseconds > 200)
                    {
                        if (!reportProgress(i / (double)numTriangles * parsingFileRatio, "Loading Polygons"))
                        {
                            amfStream.Close();
                            return null;
                        }
                        maxProgressReport.Restart();
                    }

                    if (!Vector3.Collinear(vector[0], vector[1], vector[2]))
                    {
                        Vertex vertex1 = meshFromAmfFile.CreateVertex(vector[0], true, true);
                        Vertex vertex2 = meshFromAmfFile.CreateVertex(vector[1], true, true);
                        Vertex vertex3 = meshFromAmfFile.CreateVertex(vector[2], true, true);
                        meshFromAmfFile.CreateFace(new Vertex[] { vertex1, vertex2, vertex3 }, true);
                    }
                }
                //uint numTriangles = System.BitConverter.ToSingle(fileContents, 80);
            }

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

            time.Stop();
            Debug.WriteLine(string.Format("AMF Load in {0:0.00}s", time.Elapsed.TotalSeconds));

            amfStream.Close();
            return meshFromAmfFile;
        }

        public static string FormatForAmf(Vector3 value)
        {
            return string.Format("{0:0.000000} {1:0.000000} {2:0.000000}", value.x, value.y, value.z);
        }

        private static bool ParseLine(Mesh meshFromAmfFile, string thisLine, out Vector3 vertexPosition)
        {
            if (thisLine == null)
            {
                vertexPosition = new Vector3();
                return true;
            }
            thisLine = thisLine.Trim();
            string noDoubleSpaces = thisLine;
            while (noDoubleSpaces.Contains("  "))
            {
                noDoubleSpaces = noDoubleSpaces.Replace("  ", " ");
            }
            string[] splitOnSpace = noDoubleSpaces.Split(' ');
            vertexPosition = new Vector3();
            bool goodParse = double.TryParse(splitOnSpace[1], out vertexPosition.x);
            goodParse &= double.TryParse(splitOnSpace[2], out vertexPosition.y);
            goodParse &= double.TryParse(splitOnSpace[3], out vertexPosition.z);
            return goodParse;
        }
    }
}
