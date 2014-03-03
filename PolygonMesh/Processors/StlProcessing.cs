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

using MatterHackers.Agg;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;

namespace MatterHackers.PolygonMesh.Processors
{
    public static class StlProcessing
    {
        public static void Save(Mesh meshToSave, string fileName)
        {
            FileStream file = new FileStream(fileName, FileMode.Create, FileAccess.Write);

            Save(meshToSave, file);
            file.Close();
        }

        public static void Save(Mesh meshToSave, Stream stream)
        {
            StreamWriter streamWriter = new StreamWriter(stream);

            streamWriter.WriteLine("solid Default");

            foreach(Face face in meshToSave.Faces)
            {
                List<Vector3> positionsCCW = new List<Vector3>();
                foreach (FaceEdge faceEdge in face.FaceEdgeIterator())
                {
                    positionsCCW.Add(faceEdge.vertex.Position);
                }

                int numPolys = positionsCCW.Count - 2;
                int secondIndex = 1;
                int thirdIndex = 2;
                for(int polyIndex = 0; polyIndex < numPolys; polyIndex++)
                {
                    streamWriter.WriteLine("  facet normal " + FormatForStl(face.normal));
                    streamWriter.WriteLine("    outer loop");
                    streamWriter.WriteLine("      vertex " + FormatForStl(positionsCCW[0]));
                    streamWriter.WriteLine("      vertex " + FormatForStl(positionsCCW[secondIndex]));
                    streamWriter.WriteLine("      vertex " + FormatForStl(positionsCCW[thirdIndex]));
                    streamWriter.WriteLine("    endloop");
                    streamWriter.WriteLine("  endfacet");

                    secondIndex = thirdIndex;
                    thirdIndex++;
                }
            }

            streamWriter.WriteLine("endsolid Default");

            streamWriter.Close();
        }

        public static Mesh Load(string fileName)
        {
            Mesh loadedMesh = null;
            if (Path.GetExtension(fileName).ToUpper() == ".STL")
            {
                try
                {
                    if (File.Exists(fileName))
                    {
                        Stream fileStream = File.OpenRead(fileName);

                        DoWorkEventArgs doWorkEventArgs = new DoWorkEventArgs(fileStream);
                        ParseFileContents(null, doWorkEventArgs);
                        loadedMesh = (Mesh)doWorkEventArgs.Result;
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

        public static Mesh Load(Stream fileStream)
        {
            Mesh loadedMesh = null;
            try
            {
                DoWorkEventArgs doWorkEventArgs = new DoWorkEventArgs(fileStream);
                ParseFileContents(null, doWorkEventArgs);
                loadedMesh = (Mesh)doWorkEventArgs.Result;
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

        public static void LoadInBackground(BackgroundWorker backgroundWorker, string fileName)
        {
            if (Path.GetExtension(fileName).ToUpper() == ".STL")
            {
                try
                {
                    if (File.Exists(fileName))
                    {
                        Stream fileStream = File.OpenRead(fileName);

                        backgroundWorker.DoWork += new DoWorkEventHandler(ParseFileContents);

                        backgroundWorker.RunWorkerAsync(fileStream);
                    }
                    else
                    {
                        backgroundWorker.RunWorkerAsync(null);
                    }
                }
                catch (IOException)
                {
                }
            }
            else
            {
                backgroundWorker.RunWorkerAsync(null);
            }
        }

        public static void ParseFileContents(object sender, DoWorkEventArgs doWorkEventArgs)
        {
            Stopwatch time = new Stopwatch();
            time.Start();
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

            Stream stlStream = (Stream)doWorkEventArgs.Argument;
            if (stlStream == null)
            {
                return;
            }

            //MemoryStream stlStream = new MemoryStream();
            //stlStreamIn.CopyTo(stlStream);

            Stopwatch maxProgressReport = new Stopwatch();
            maxProgressReport.Start();
            Mesh meshFromStlFile = new Mesh();
            //meshFromStlFile.MaxDistanceToConsiderVertexAsSame = .0000005;
            meshFromStlFile.MaxDistanceToConsiderVertexAsSame = 0; // only vertecies that are the exact same point will be merged.
            long bytesInFile = stlStream.Length;
            if (bytesInFile <= 80)
            {
                return;
            }

            byte[] first160Bytes = new byte[160];
            stlStream.Read(first160Bytes, 0, 160);
            byte[] ByteOredrMark = new byte[] { 0xEF, 0xBB, 0xBF };
            int startOfString = 0;
            if (first160Bytes[0] == ByteOredrMark[0] && first160Bytes[0] == ByteOredrMark[0] && first160Bytes[0] == ByteOredrMark[0])
            {
                startOfString = 3;
            }
            string first160BytesOfSTLFile = System.Text.Encoding.UTF8.GetString(first160Bytes, startOfString, first160Bytes.Length - startOfString);
            if (first160BytesOfSTLFile.StartsWith("solid") && first160BytesOfSTLFile.Contains("facet"))
            {
                stlStream.Position = 0;
                StreamReader stlReader = new StreamReader(stlStream);
                int lineIndex = 0;
                string currentLine = stlReader.ReadLine(); lineIndex++;
                currentLine = stlReader.ReadLine(); lineIndex++;// move past solid
                // ths is an ascii stl
                do
                {
                    if (currentLine == null) // found end of file
                    {
                        break;
                    }
                    // skip blank lines
                    while (currentLine.Trim() == "")
                    {
                        currentLine = stlReader.ReadLine(); lineIndex++;
                    }
                    if (currentLine.Trim().StartsWith("endsolid"))
                    {
                        break;
                    }
                    if (!currentLine.Trim().StartsWith("facet normal"))
                    {
                        // If there are more polygons they need to start with facet normal.
                        // So if they didn't we stop loading and return whatever we have.
                        break;
                    }
                    currentLine = stlReader.ReadLine(); lineIndex++;
                    if (!currentLine.Trim().StartsWith("outer loop"))
                    {
                        throw new IOException("Error in STL file: expected 'outer loop'.");
                    }
                    currentLine = stlReader.ReadLine(); lineIndex++;

                    Vector3 vector1; 
                    bool goodPolygon = ParseLine(meshFromStlFile, currentLine, out vector1);
                    currentLine = stlReader.ReadLine(); lineIndex++;
                    
                    Vector3 vector2; 
                    goodPolygon &= ParseLine(meshFromStlFile, currentLine, out vector2);
                    currentLine = stlReader.ReadLine(); lineIndex++;
                    
                    Vector3 vector3; 
                    goodPolygon &= ParseLine(meshFromStlFile, currentLine, out vector3);
                    currentLine = stlReader.ReadLine(); lineIndex++;

                    if (currentLine == null)
                    {
                        return;
                    }

                    if (goodPolygon && !Vector3.Collinear(vector1, vector2, vector3))
                    {
                        Vertex vertex1 = meshFromStlFile.CreateVertex(vector1, true);
                        Vertex vertex2 = meshFromStlFile.CreateVertex(vector2, true);
                        Vertex vertex3 = meshFromStlFile.CreateVertex(vector3, true);
                        if (vertex1.Data.ID == vertex2.Data.ID || vertex2.Data.ID == vertex3.Data.ID || vertex1.Data.ID == vertex3.Data.ID)
                        {
                            //throw new Exception("All vertecies should be generated no matter what. Check that the STL loader is not colapsing faces.");
                        }
                        else
                        {
                            meshFromStlFile.CreateFace(new Vertex[] { vertex1, vertex2, vertex3 });
                        }
                    }
                    
                    if (sender != null)
                    {
                        BackgroundWorker backgroundWorker = (BackgroundWorker)sender;
                        if (backgroundWorker.CancellationPending)
                        {
                            stlStream.Close();
                            return;
                        }

                        if(backgroundWorker.WorkerReportsProgress && maxProgressReport.ElapsedMilliseconds > 200)
                        {
                            backgroundWorker.ReportProgress((int)(stlStream.Position * 100 / bytesInFile));
                            maxProgressReport.Restart();
                        }
                    }

                    if (!currentLine.Trim().StartsWith("endloop"))
                    {
                        throw new IOException("Error in STL file: expected 'endloop'.");
                    }
                    currentLine = stlReader.ReadLine(); lineIndex++;
                    if (!currentLine.Trim().StartsWith("endfacet"))
                    {
                        throw new IOException("Error in STL file: expected 'endfacet'.");
                    }
                    currentLine = stlReader.ReadLine(); lineIndex++;
                } while (true);
            }
            else
            {
                // load it as a binary stl
                // skip the first 80 bytes
                // read in the number of triangles
                stlStream.Position = 0;
                BinaryReader br = new BinaryReader(stlStream);
                byte[] fileContents = br.ReadBytes((int)stlStream.Length);
                int currentPosition = 80;
                uint numTriangles = System.BitConverter.ToUInt32(fileContents, currentPosition);
                long bytesForNormals = numTriangles * 3 * 4;
                long bytesForVertices = numTriangles * 3 * 4;
                long bytesForAttributs = numTriangles * 2;
                currentPosition += 4;
                long numBytesRequiredForVertexData = currentPosition + bytesForNormals + bytesForVertices + bytesForAttributs;
                if (fileContents.Length < numBytesRequiredForVertexData || numTriangles < 4)
                {
                    stlStream.Close();
                    return;
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

                    if (sender != null)
                    {
                        BackgroundWorker backgroundWorker = (BackgroundWorker)sender;
                        if (backgroundWorker.CancellationPending)
                        {
                            stlStream.Close();
                            return;
                        }

                        if(backgroundWorker.WorkerReportsProgress && maxProgressReport.ElapsedMilliseconds > 200)
                        {
                            backgroundWorker.ReportProgress(i * 100 / (int)numTriangles);
                            maxProgressReport.Restart();
                        }
                    }

                    if (!Vector3.Collinear(vector[0], vector[1], vector[2]))
                    {
                        Vertex vertex1 = meshFromStlFile.CreateVertex(vector[0], true);
                        Vertex vertex2 = meshFromStlFile.CreateVertex(vector[1], true);
                        Vertex vertex3 = meshFromStlFile.CreateVertex(vector[2], true);
                        meshFromStlFile.CreateFace(new Vertex[] { vertex1, vertex2, vertex3 }, true);
                    }
                }
                //uint numTriangles = System.BitConverter.ToSingle(fileContents, 80);
            }

            // TODO: merge all the vetexes that are in the same place together
            meshFromStlFile.MergeVertecies();

            doWorkEventArgs.Result = meshFromStlFile;

            time.Stop();
            Debug.WriteLine(string.Format("STL Load in {0:0.00}s", time.Elapsed.Seconds));

            stlStream.Close();
        }

        public static string FormatForStl(Vector3 value)
        {
            return string.Format("{0:0.000000} {1:0.000000} {2:0.000000}", value.x, value.y, value.z);
        }

        private static bool ParseLine(Mesh meshFromStlFile, string thisLine, out Vector3 vertexPosition)
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
