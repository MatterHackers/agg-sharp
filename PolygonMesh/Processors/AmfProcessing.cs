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
using System.Xml.Linq;
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

            Stopwatch maxProgressReport = new Stopwatch();
            maxProgressReport.Start();
            Mesh meshFromAmfFile = new Mesh();

            // do the loading
            {
                string amfContent = LoadAmfIntoString(amfStream);
                TextReader textReader = new StringReader(amfContent);
                XDocument xmlTree = XDocument.Load(textReader);
                double scale = GetScaling(xmlTree);

                foreach (XElement objects in xmlTree.Descendants("object"))
                {
                    foreach (XElement meshes in xmlTree.Descendants("mesh"))
                    {
                        foreach (XElement vertices in xmlTree.Descendants("vertices"))
                        {
                            foreach (XElement vertex in xmlTree.Descendants("vertex"))
                            {
                                foreach (XElement coordinates in xmlTree.Descendants("coordinates"))
                                {
                                }
                            }
                        }

                        foreach (XElement volume in xmlTree.Descendants("volume"))
                        {
                            foreach (XElement triangles in xmlTree.Descendants("triangle"))
                            {
                            }
                        }
                    }
                }

                if (reportProgress != null && maxProgressReport.ElapsedMilliseconds > 200)
                {
                    int bytesInFile = 1000;
                    if (!reportProgress(amfStream.Position / (double)bytesInFile * parsingFileRatio, "Loading Polygons"))
                    {
                        amfStream.Close();
                        return null;
                    }
                    maxProgressReport.Restart();
                }
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

        private static string LoadAmfIntoString(Stream amfStream)
        {
            string amfContent = "";
            if (IsZipFile(amfStream))
            {
                ZipFile zip = new ZipFile(amfStream);
                bool isValid = zip.TestArchive(false);
                foreach (ZipEntry zipEntry in zip)
                {
                    Stream zipStream = zip.GetInputStream(zipEntry);
                    StreamReader sr = new StreamReader(zipStream);
                    amfContent = sr.ReadToEnd();
                }
            }
            else
            {
                amfStream.Position = 0;
                StreamReader sr = new StreamReader(amfStream);
                amfContent = sr.ReadToEnd();
            }

            return amfContent;
        }

        private static double GetScaling(XDocument xmlTree)
        {
            switch (xmlTree.Root.Attribute("unit").Value.ToLower())
            {
                case "millimeter":
                    return 1;

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
