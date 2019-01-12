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
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using MatterHackers.VectorMath;

namespace MatterHackers.PolygonMesh.Processors
{
	public static class StlProcessing
	{
		public static bool Save(this Mesh meshToSave, string fileName, CancellationToken cancellationToken, MeshOutputSettings outputInfo = null)
		{
			using (FileStream file = new FileStream(fileName, FileMode.Create, FileAccess.Write))
			{
				if (outputInfo == null)
				{
					outputInfo = new MeshOutputSettings();
				}
				return Save(meshToSave, file, cancellationToken, outputInfo);
			}
		}

		public static bool Save(Mesh mesh, Stream stream, CancellationToken cancellationToken, MeshOutputSettings outputInfo)
		{
			switch (outputInfo.OutputTypeSetting)
			{
				case MeshOutputSettings.OutputType.Ascii:
					{
						StreamWriter streamWriter = new StreamWriter(stream);

						streamWriter.WriteLine("solid Default");

						for (int faceIndex = 0; faceIndex < mesh.Faces.Count; faceIndex++)
						{
							if (cancellationToken.IsCancellationRequested)
							{
								return false;
							}

							var face = mesh.Faces[faceIndex];

							streamWriter.WriteLine("  facet normal " + FormatForStl(mesh.FaceNormals[faceIndex]));
							streamWriter.WriteLine("    outer loop");
							streamWriter.WriteLine("      vertex " + FormatForStl(mesh.Vertices[face.v0]));
							streamWriter.WriteLine("      vertex " + FormatForStl(mesh.Vertices[face.v0]));
							streamWriter.WriteLine("      vertex " + FormatForStl(mesh.Vertices[face.v0]));
							streamWriter.WriteLine("    endloop");
							streamWriter.WriteLine("  endfacet");
						}

						streamWriter.WriteLine("endsolid Default");

						streamWriter.Close();
					}
					break;

				case MeshOutputSettings.OutputType.Binary:
					using (BinaryWriter bw = new BinaryWriter(stream))
					{
						// 80 bytes of nothing
						bw.Write(new Byte[80]);
						// the number of triangles
						bw.Write(mesh.Faces.Count);
						int binaryPolyCount = 0;
						for (int faceIndex = 0; faceIndex < mesh.Faces.Count; faceIndex++)
						{
							if (cancellationToken.IsCancellationRequested)
							{
								return false;
							}

							var face = mesh.Faces[faceIndex];

							binaryPolyCount++;
							// save the normal (all 0 so it can compress better)
							WriteToBinaryStl(bw, mesh.FaceNormals[faceIndex]);
							// save the position
							WriteToBinaryStl(bw, mesh.Vertices[face.v0]);
							WriteToBinaryStl(bw, mesh.Vertices[face.v1]);
							WriteToBinaryStl(bw, mesh.Vertices[face.v2]);

							// and the attribute
							bw.Write((ushort)0);
						}

						bw.BaseStream.Position = 80;

						// the number of triangles
						bw.Write(binaryPolyCount);
					}
					break;
			}
			return true;
		}

		private static void WriteToBinaryStl(BinaryWriter bw, Vector3Float p)
		{
			bw.Write(p.X);
			bw.Write(p.Y);
			bw.Write(p.Z);
		}

		public static Mesh Load(string fileName, CancellationToken cancellationToken, Action<double, string> reportProgress = null)
		{
			// Early exit if not STL
			if (Path.GetExtension(fileName).ToUpper() != ".STL") return null;

			using (Stream fileStream = File.OpenRead(fileName))
			{
				// Call the Load signature taking a stream and file extension
				return Load(fileStream, cancellationToken, reportProgress);
			}
		}

		// Note: Changing the Load(Stream) return type - this is a breaking change but methods with the same name should return the same type
		public static Mesh Load(Stream fileStream, CancellationToken cancellationToken, Action<double, string> reportProgress = null)
		{
			try
			{
				// Parse STL
				Mesh loadedMesh = ParseFileContents(fileStream, cancellationToken, reportProgress);

				// TODO: Sync with AMF processing and have ParseFileContents return List<MeshGroup>?
				//
				// Return the loaded mesh wrapped in a MeshGroup, wrapped in a List
				return loadedMesh;
			}
#if DEBUG
			catch (IOException e)
			{
				Debug.Print(e.Message);
				BreakInDebugger();
				return null;
			}
#else
            // TODO: Consider not suppressing exceptions like this or at least logging them. Troubleshooting when this
            // scenario occurs is impossible and likely results in an undiagnosable null reference error
            catch (Exception)
            {
                return null;
            }
#endif
		}

		static NumberStyles style = NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent | NumberStyles.AllowLeadingSign;
		static CultureInfo culture = CultureInfo.InvariantCulture;
		private static Vector3 Convert(string line)
		{
			Vector3 vector0;
			int currentPosition = "vertex".Length;
			string number = GetNumber(line, ref currentPosition);
			double.TryParse(number, style, culture, out vector0.X);

			number = GetNumber(line, ref currentPosition);
			double.TryParse(number, style, culture, out vector0.Y);

			number = GetNumber(line, ref currentPosition);
			double.TryParse(number, style, culture, out vector0.Z);

			return vector0;
		}

		private static string GetNumber(string line, ref int currentPosition)
		{
			while(line[currentPosition] == ' ')
			{
				currentPosition++;
			}

			int numberLength = 0;
			while(currentPosition < line.Length && line[currentPosition] != ' ')
			{
				currentPosition++;
				numberLength++;
			}

			return line.Substring(currentPosition-numberLength, numberLength);
		}

		public static Mesh ParseFileContents(Stream stlStream, CancellationToken cancellationToken, Action<double, string> reportProgress)
		{
			Stopwatch time = new Stopwatch();
			time.Start();
			Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

			if (stlStream == null)
			{
				return null;
			}

			Stopwatch maxProgressReport = new Stopwatch();
			maxProgressReport.Start();
			Mesh meshFromStlFile = new Mesh();
			long bytesInFile = stlStream.Length;
			if (bytesInFile <= 80)
			{
				return null;
			}

			byte[] first160Bytes = new byte[160];
			stlStream.Read(first160Bytes, 0, 160);
			byte[] ByteOredrMark = new byte[] { 0xEF, 0xBB, 0xBF };
			int startOfString = 0;
			if (first160Bytes[0] == ByteOredrMark[0] && first160Bytes[0] == ByteOredrMark[0] && first160Bytes[0] == ByteOredrMark[0])
			{
				startOfString = 3;
			}
			Dictionary<(double, double, double), int> postionToIndex = new Dictionary<(double, double, double), int>();
			int GetIndex(Vector3 position)
			{
				int index;
				if(postionToIndex.TryGetValue((position.X, position.Y,position.Z), out index))
				{
					return index;
				}

				var count = meshFromStlFile.Vertices.Count;
				postionToIndex.Add((position.X, position.Y, position.Z), count);
				meshFromStlFile.Vertices.Add(position);
				return count;
			}
			string first160BytesOfSTLFile = System.Text.Encoding.UTF8.GetString(first160Bytes, startOfString, first160Bytes.Length - startOfString);
			if (first160BytesOfSTLFile.StartsWith("solid") && first160BytesOfSTLFile.Contains("facet"))
			{
				stlStream.Position = 0;
				StreamReader stlReader = new StreamReader(stlStream);
				int vectorIndex = 0;
				Vector3 vector0 = new Vector3(0, 0, 0);
				Vector3 vector1 = new Vector3(0, 0, 0);
				Vector3 vector2 = new Vector3(0, 0, 0);
				string line = stlReader.ReadLine();

				while (line != null)
				{
					line = line.Trim();
					if (line.StartsWith("vertex"))
					{
						vectorIndex++;
						switch (vectorIndex)
						{
							case 1:
								vector0 = Convert(line);
								break;

							case 2:
								vector1 = Convert(line);
								break;

							case 3:
								vector2 = Convert(line);
								if (!Vector3.Collinear(vector0, vector1, vector2))
								{
									int iv0 = GetIndex(vector0);
									int iv1 = GetIndex(vector1);
									int iv2 = GetIndex(vector2);
									meshFromStlFile.Faces.Add((iv0, iv1, iv2));
								}
								vectorIndex = 0;
								break;
						}
					}
					line = stlReader.ReadLine();

					if (reportProgress != null && maxProgressReport.ElapsedMilliseconds > 200)
					{
						reportProgress(stlStream.Position / (double)bytesInFile, "Loading Polygons");
						if (cancellationToken.IsCancellationRequested)
						{
							stlStream.Close();
							return null;
						}
						maxProgressReport.Restart();
					}
				}
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
				long bytesForVertices = numTriangles * 3 * 4 * 3;
				long bytesForAttributs = numTriangles * 2;
				currentPosition += 4;
				long numBytesRequiredForVertexData = currentPosition + bytesForNormals + bytesForVertices + bytesForAttributs;
				if (fileContents.Length < numBytesRequiredForVertexData || numTriangles < 4)
				{
					stlStream.Close();
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
						reportProgress(i / (double)numTriangles, "Loading Polygons");
						if (cancellationToken.IsCancellationRequested)
						{
							stlStream.Close();
							return null;
						}
						maxProgressReport.Restart();
					}

					if (!Vector3.Collinear(vector[0], vector[1], vector[2]))
					{
						int iv0 = GetIndex(vector[0]);
						int iv1 = GetIndex(vector[1]);
						int iv2 = GetIndex(vector[2]);
						meshFromStlFile.Faces.Add((iv0, iv1, iv2));
					}
				}
			}

			reportProgress?.Invoke(1, "");

			if (cancellationToken.IsCancellationRequested)
			{
				return null;
			}

			time.Stop();
			Debug.WriteLine(string.Format("STL Load in {0:0.00}s", time.Elapsed.TotalSeconds));

			stlStream.Close();
			return meshFromStlFile;
		}

		public static string FormatForStl(Vector3 value)
		{
			return string.Format("{0:0.000000} {1:0.000000} {2:0.000000}", value.X, value.Y, value.Z);
		}

		public static string FormatForStl(Vector3Float value)
		{
			return string.Format("{0:0.000000} {1:0.000000} {2:0.000000}", value.X, value.Y, value.Z);
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
			bool goodParse = double.TryParse(splitOnSpace[1], out vertexPosition.X);
			goodParse &= double.TryParse(splitOnSpace[2], out vertexPosition.Y);
			goodParse &= double.TryParse(splitOnSpace[3], out vertexPosition.Z);
			return goodParse;
		}

		public static bool IsBinary(string fileName)
		{
			try
			{
				using (Stream stlStream = new FileStream(fileName, FileMode.Open, FileAccess.Read))
				{
					long bytesInFile = stlStream.Length;
					if (bytesInFile <= 80)
					{
						return false;
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
						return false;
					}
				}
			}
			catch (Exception e) 
			{
				Debug.Print(e.Message);
				BreakInDebugger();
			}

			return true;
		}

		public static long GetEstimatedMemoryUse(string fileLocation)
		{
			try
			{
				using (Stream stream = new FileStream(fileLocation, FileMode.Open, FileAccess.Read))
				{
					if (IsBinary(fileLocation))
					{
						return (long)(stream.Length * 13.5);
					}
					else
					{
						return (long)(stream.Length * 2.5);
					}
				}
			}
			catch (Exception e)
			{
				Debug.Print(e.Message);
				BreakInDebugger();
				return 0;
			}
		}

		[Conditional("DEBUG")]
		public static void BreakInDebugger(string description = "")
		{
			Debug.WriteLine(description);
			Debugger.Break();
		}
	}
}