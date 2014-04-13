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
using System.Globalization;
using System.Threading;

using MatterHackers.Agg;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;

namespace MatterHackers.PolygonMesh.Processors
{
    public class AmfProcessing
    {
        public enum DistanceUnits { meter, millimeter, micrometer, feet, inch };

        //DistanceUnits FileDistanceUnits = DistanceUnits.millimeter;

        public void Save(Mesh meshToSave, string fileName)
        {
            FileStream file = new FileStream(fileName, FileMode.Create, FileAccess.Write);

            Save(meshToSave, file);
            file.Close();
        }

        public void Save(Mesh meshToSave, Stream stream)
        {
            throw new NotImplementedException("Copied from STL code. Needs to be writen for AMF.");
#if false
            StreamWriter streamWriter = new StreamWriter(stream);

            streamWriter.WriteLine("solid Default");

            foreach (Face face in meshToSave.Faces)
            {
                List<Vector3> positionsCCW = new List<Vector3>();
                foreach (FaceEdge faceEdge in face.FaceEdgeIterator())
                {
                    positionsCCW.Add(faceEdge.vertex.Position);
                }
                if (positionsCCW.Count == 3)
                {
                    streamWriter.WriteLine("  facet normal " + FormatForStl(face.normal));
                    streamWriter.WriteLine("    outer loop");
                    streamWriter.WriteLine("      vertex " + FormatForStl(positionsCCW[0]));
                    streamWriter.WriteLine("      vertex " + FormatForStl(positionsCCW[1]));
                    streamWriter.WriteLine("      vertex " + FormatForStl(positionsCCW[2]));
                    streamWriter.WriteLine("    endloop");
                    streamWriter.WriteLine("  endfacet");
                }
                else
                {
                    // feed this into a tesselator and get back the triangles to emit to the stl file
                }
            }

            streamWriter.WriteLine("endsolid Default");

            streamWriter.Close();
#endif
        }

        public Mesh Load(string fileName)
        {
            Mesh loadedMesh = null;
            if (Path.GetExtension(fileName).ToUpper() == ".AMF")
            {
                try
                {
                    if (File.Exists(fileName))
                    {
                        FileStream fileStream = File.OpenRead(fileName);

                        BinaryReader br = new BinaryReader(fileStream);
                        byte[] fileContents = br.ReadBytes((int)fileStream.Length);

                        DoWorkEventArgs doWorkEventArgs = new DoWorkEventArgs(fileContents);
                        ParseFileContents(null, doWorkEventArgs);
                        loadedMesh = (Mesh)doWorkEventArgs.Result;

                        fileStream.Close();
                    }
                }
                catch (IOException)
                {
                }
            }

            return loadedMesh;
        }

        public Mesh Load(Stream fileStream)
        {
            Mesh loadedMesh = null;
            try
            {
                BinaryReader br = new BinaryReader(fileStream);
                byte[] fileContents = br.ReadBytes((int)fileStream.Length);

                DoWorkEventArgs doWorkEventArgs = new DoWorkEventArgs(fileContents);
                ParseFileContents(null, doWorkEventArgs);
                loadedMesh = (Mesh)doWorkEventArgs.Result;

                fileStream.Close();
            }
            catch (IOException)
            {
            }

            return loadedMesh;
        }

        public void LoadInBackground(BackgroundWorker backgroundWorker, string fileName)
        {
            if (Path.GetExtension(fileName).ToUpper() == ".AMF")
            {
                try
                {
                    if (File.Exists(fileName))
                    {
                        FileStream fileStream = File.OpenRead(fileName);

                        BinaryReader br = new BinaryReader(fileStream);
                        byte[] fileContents = br.ReadBytes((int)fileStream.Length);

                        backgroundWorker.DoWork += new DoWorkEventHandler(ParseFileContents);

                        backgroundWorker.RunWorkerAsync(fileContents);

                        fileStream.Close();
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

        //public Mesh ParseFileContents(byte[] fileContents)
        public void ParseFileContents(object sender, DoWorkEventArgs e)
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            throw new NotImplementedException("Copied from STL code. Needs to be writen for AMF.");
#if false
            byte[] fileContents = (byte[])e.Argument;
            if (fileContents == null)
            {
                return;
            }
            Stopwatch maxProgressReport = new Stopwatch();
            maxProgressReport.Start();
            
            string amfFileString = System.Text.Encoding.UTF8.GetString(fileContents, 0, fileContents.Length);
            if (!amfFileString.StartsWith(@"<?xml version=""1.0"" encoding=""UTF-8""?>"""))
            {
                throw new Exception(@"AMF files should start with '<?xml version=""1.0"" encoding=""UTF-8""?>""'");
            }

            Mesh meshFromStlFile = new Mesh();
            //meshFromStlFile.MaxDistanceToConsiderVertexAsSame = .0000005;
            meshFromStlFile.MaxDistanceToConsiderVertexAsSame = 0; // only vertices that are the exact same point will be merged.
            if (fileContents.Length <= 80)
            {
                throw new IOException("The file you have passed is not a valid STL file.");
            }
            string first80BytesOfSTLFile = System.Text.Encoding.UTF8.GetString(fileContents, 0, 80);
            if (first80BytesOfSTLFile.StartsWith("solid") && first80BytesOfSTLFile.Contains("facet normal"))
            {
                string stlFileString = System.Text.Encoding.UTF8.GetString(fileContents, 0, fileContents.Length);
                //stlFileString = new 
                string[] splitOnLF = stlFileString.Split('\n');
                stlFileString = stlFileString.Replace("\r\n", "\n");
                stlFileString = stlFileString.Replace('\r', '\n');
                int lineIndex = 1;
                // ths is an ascii stl
                do
                {
                    // skip blank lines
                    while (splitOnLF[lineIndex].Trim() == "")
                    {
                        lineIndex++;
                        if (lineIndex == splitOnLF.Length)
                        {
                            throw new IOException("Error in STL file: found no data.");
                        }
                    }
                    if (splitOnLF[lineIndex].Trim().StartsWith("endsolid"))
                    {
                        break;
                    }
                    if (!splitOnLF[lineIndex++].Trim().StartsWith("facet normal"))
                    {
                        throw new IOException("Error in STL file: expected 'facet normal'.");
                    }
                    if (!splitOnLF[lineIndex++].Trim().StartsWith("outer loop"))
                    {
                        throw new IOException("Error in STL file: expected 'outer loop'.");
                    }

                    Vector3 vector1 = ParseLine(meshFromStlFile, splitOnLF, lineIndex++);
                    Vector3 vector2 = ParseLine(meshFromStlFile, splitOnLF, lineIndex++);
                    Vector3 vector3 = ParseLine(meshFromStlFile, splitOnLF, lineIndex++);
                    if (!Vector3.Collinear(vector1, vector2, vector3))
                    {
                        Vertex vertex1 = meshFromStlFile.CreateVertex(vector1);
                        Vertex vertex2 = meshFromStlFile.CreateVertex(vector2);
                        Vertex vertex3 = meshFromStlFile.CreateVertex(vector3);
                        if (vertex1.Data.ID == vertex2.Data.ID || vertex2.Data.ID == vertex3.Data.ID || vertex1.Data.ID == vertex3.Data.ID)
                        {
                            //throw new Exception("All vertices should be generated no matter what. Check that the STL loader is not colapsing faces.");
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
                            return;
                        }

                        if (backgroundWorker.WorkerReportsProgress && maxProgressReport.ElapsedMilliseconds > 200)
                        {
                            backgroundWorker.ReportProgress(lineIndex * 100 / splitOnLF.Length);
                            maxProgressReport.Restart();
                        }
                    }

                    if (!splitOnLF[lineIndex++].Trim().StartsWith("endloop"))
                    {
                        throw new IOException("Error in STL file: expected 'endloop'.");
                    }
                    if (!splitOnLF[lineIndex++].Trim().StartsWith("endfacet"))
                    {
                        throw new IOException("Error in STL file: expected 'endfacet'.");
                    }
                } while (true);
            }
            else
            {
                // load it as a binary stl
                // skip the first 80 bytes
                // read in the number of triangles
                int currentPosition = 80;
                uint numTriangles = System.BitConverter.ToUInt32(fileContents, currentPosition);
                currentPosition += 4;
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
                            return;
                        }

                        if (backgroundWorker.WorkerReportsProgress && maxProgressReport.ElapsedMilliseconds > 200)
                        {
                            backgroundWorker.ReportProgress(i * 100 / (int)numTriangles);
                            maxProgressReport.Restart();
                        }
                    }

                    if (!Vector3.Collinear(vector[0], vector[1], vector[2]))
                    {
                        Vertex vertex1 = meshFromStlFile.CreateVertex(vector[0]);
                        Vertex vertex2 = meshFromStlFile.CreateVertex(vector[1]);
                        Vertex vertex3 = meshFromStlFile.CreateVertex(vector[2]);
                        meshFromStlFile.CreateFace(new Vertex[] { vertex1, vertex2, vertex3 });
                    }
                }
                //uint numTriangles = System.BitConverter.ToSingle(fileContents, 80);

            }

            e.Result = meshFromStlFile;
#endif
        }

        public string FormatForStl(Vector3 value)
        {
            return string.Format("{0:0.000000} {1:0.000000} {2:0.000000}", value.x, value.y, value.z);
        }

        private Vector3 ParseLine(Mesh meshFromStlFile, string[] splitOnLF, int lineIndex)
        {
            string thisLine = splitOnLF[lineIndex++].Trim();
            string noDoubleSpaces = thisLine;
            while (noDoubleSpaces.Contains("  "))
            {
                noDoubleSpaces = noDoubleSpaces.Replace("  ", " ");
            }
            string[] splitOnSpace = noDoubleSpaces.Split(' ');
            return new Vector3(double.Parse(splitOnSpace[1]), double.Parse(splitOnSpace[2]), double.Parse(splitOnSpace[3]));
        }
    }
}
