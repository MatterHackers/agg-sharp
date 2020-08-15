/*
Copyright (c) 2017, John Lewin, Lars Brubaker
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
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Xml;
using MatterHackers.Agg;
using MatterHackers.PolygonMesh;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.VectorMath;

namespace MatterHackers.DataConverters3D
{
	public static class AmfDocument
	{
		[Conditional("DEBUG")]
		public static void BreakInDebugger(string description = "")
		{
			Debug.WriteLine(description);
			Debugger.Break();
		}

		public static Stream GetCompressedStreamIfRequired(Stream amfStream)
		{
			if (IsZipFile(amfStream))
			{
				var archive = new ZipArchive(amfStream, ZipArchiveMode.Read);
				return archive.Entries[0].Open();
			}

			amfStream.Position = 0;
			return amfStream;
		}

		public static long GetEstimatedMemoryUse(string fileLocation)
		{
			try
			{
				using (Stream stream = new FileStream(fileLocation, FileMode.Open, FileAccess.Read, FileShare.Read))
				{
					if (IsZipFile(stream))
					{
						return (long)(stream.Length * 57);
					}
					else
					{
						return (long)(stream.Length * 3.7);
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

		public static IObject3D Load(string amfPath, CancellationToken cancellationToken, Action<double, string> reportProgress = null)
		{
			using (var stream = File.OpenRead(amfPath))
			{
				return Load(stream, cancellationToken, reportProgress);
			}
		}

		public static IObject3D Load(Stream fileStream, CancellationToken cancellationToken, Action<double, string> reportProgress = null)
		{
			IObject3D root = new Object3D();

			IObject3D context = null;

			int totalMeshes = 0;
			var time = Stopwatch.StartNew();

			var materials = new Dictionary<string, ColorF>();
			var objectMaterialDictionary = new Dictionary<IObject3D, string>();

			using (var decompressedStream = GetCompressedStreamIfRequired(fileStream))
			{
				using (var reader = XmlReader.Create(decompressedStream))
				{
					List<Vector3> vertices = null;
					Mesh mesh = null;

					var progressData = new ProgressData(fileStream, reportProgress);

					while (reader.Read())
					{
						if (!reader.IsStartElement())
						{
							continue;
						}

						switch (reader.Name)
						{
							case "object":
								break;

							case "mesh":
								break;

							case "vertices":
								vertices = ReadAllVertices(reader, progressData);
								break;

							case "volume":
								context = new Object3D();
								root.Children.Add(context);
								mesh = new Mesh();
								context.SetMeshDirect(mesh);
								totalMeshes += 1;

								string materialId;
								ReadVolume(reader, vertices, mesh, progressData, out materialId);
								objectMaterialDictionary.Add(context, materialId);
								break;

							case "material":
								ReadMaterial(reader, materials);
								break;
						}
					}
				}

				fileStream.Dispose();
			}

			foreach (var keyValue in objectMaterialDictionary)
			{
				ColorF color = ColorF.White;
				if (keyValue.Value == null
					|| !materials.TryGetValue(keyValue.Value, out color))
				{
					color = ColorF.White;
				}

				keyValue.Key.Color = color.ToColor();
			}

			time.Stop();
			Debug.WriteLine(string.Format("AMF Load in {0:0.00}s", time.Elapsed.TotalSeconds));

			time.Restart();
			bool hasValidMesh = root.Children.Where(item => item.Mesh.Faces.Count > 0).Any();
			Debug.WriteLine("hasValidMesh: " + time.ElapsedMilliseconds);

			reportProgress?.Invoke(1, "");

			return hasValidMesh ? root : null;
		}

		/// <summary>
		/// Writes the mesh to disk in a zip container
		/// </summary>
		/// <param name="item">The object to save</param>
		/// <param name="fileName">The location to save to</param>
		/// <param name="outputInfo">Extra meta data to store in the file</param>
		/// <returns>The results of the save operation</returns>
		public static bool Save(IObject3D item, string fileName, MeshOutputSettings outputInfo = null)
		{
			try
			{
				if (outputInfo?.OutputTypeSetting == MeshOutputSettings.OutputType.Ascii)
				{
					using (Stream stream = File.OpenWrite(fileName))
					{
						return Save(item, stream, outputInfo);
					}
				}
				else
				{
					using (Stream stream = File.OpenWrite(fileName))
					{
						using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
						{
							ZipArchiveEntry zipEntry = archive.CreateEntry(Path.GetFileName(fileName));
							using (var entryStream = zipEntry.Open())
							{
								return Save(item, entryStream, outputInfo);
							}
						}
					}
				}
			}
			catch (Exception e)
			{
				Debug.Print(e.Message);
				BreakInDebugger();
				return false;
			}
		}

		public static bool Save(IObject3D itemToSave, Stream stream, MeshOutputSettings outputInfo)
		{
			TextWriter amfFile = new StreamWriter(stream);
			amfFile.WriteLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
			amfFile.WriteLine("<amf unit=\"millimeter\" version=\"1.1\">");
			if (outputInfo != null)
			{
				foreach (KeyValuePair<string, string> metaData in outputInfo.MetaDataKeyValue)
				{
					amfFile.WriteLine(Indent(1) + "<metadata type=\"{0}\">{1}</metadata>".FormatWith(metaData.Key, metaData.Value));
				}
			}

			{
				int objectId = 1;

				var visibleMeshes = itemToSave.VisibleMeshes();
				int totalMeshes = visibleMeshes.Count();

				double ratioPerMesh = 1d / totalMeshes;
				double currentRatio = 0;

				var groupedByExtruder = visibleMeshes.GroupBy(i => i.WorldMaterialIndex());

				foreach (var group in groupedByExtruder)
				{
					amfFile.WriteLine(Indent(1) + "<object id=\"{0}\">".FormatWith(objectId++));
					{
						int vertexCount = 0;
						var meshVertexStart = new List<int>();
						amfFile.WriteLine(Indent(2) + "<mesh>");
						{
							amfFile.WriteLine(Indent(3) + "<vertices>");
							{
								foreach (var item in group)
								{
									var matrix = item.WorldMatrix();
									var mesh = item.Mesh;
									double meshVertCount = (double)mesh.Vertices.Count;

									meshVertexStart.Add(vertexCount);
									for (int vertexIndex = 0; vertexIndex < meshVertCount; vertexIndex++)
									{
										var position = mesh.Vertices[vertexIndex].Transform(matrix);
										outputInfo?.ReportProgress?.Invoke(currentRatio + vertexIndex / meshVertCount * ratioPerMesh * .5, "");

										amfFile.WriteLine(Indent(4) + "<vertex>");
										{
											amfFile.WriteLine(Indent(5) + "<coordinates>");
											amfFile.WriteLine(Indent(6) + "<x>{0}</x>".FormatWith(position.X));
											amfFile.WriteLine(Indent(6) + "<y>{0}</y>".FormatWith(position.Y));
											amfFile.WriteLine(Indent(6) + "<z>{0}</z>".FormatWith(position.Z));
											amfFile.WriteLine(Indent(5) + "</coordinates>");
										}

										amfFile.WriteLine(Indent(4) + "</vertex>");
										vertexCount++;
									}

									currentRatio += ratioPerMesh * .5;
								}
							}

							int meshIndex = 0;
							amfFile.WriteLine(Indent(3) + "</vertices>");
							foreach (var item in group)
							{
								var mesh = item.Mesh;
								var materialIndex = item.WorldMaterialIndex();
								int firstVertexIndex = meshVertexStart[meshIndex++];
								if (materialIndex == -1)
								{
									amfFile.WriteLine(Indent(3) + "<volume>");
								}
								else
								{
									amfFile.WriteLine(Indent(3) + "<volume materialid=\"{0}\">".FormatWith(materialIndex));
								}

								double faceCount = (double)mesh.Faces.Count;
								for (int faceIndex = 0; faceIndex < faceCount; faceIndex++)
								{
									outputInfo?.ReportProgress?.Invoke(currentRatio + faceIndex / faceCount * ratioPerMesh * .5, "");

									Face face = mesh.Faces[faceIndex];

									amfFile.WriteLine(Indent(4) + "<triangle>");
									amfFile.WriteLine(Indent(5) + $"<v1>{firstVertexIndex + face.v0}</v1>");
									amfFile.WriteLine(Indent(5) + $"<v2>{firstVertexIndex + face.v1}</v2>");
									amfFile.WriteLine(Indent(5) + $"<v3>{firstVertexIndex + face.v2}</v3>");
									amfFile.WriteLine(Indent(4) + "</triangle>");
								}

								currentRatio += ratioPerMesh * .5;
								amfFile.WriteLine(Indent(3) + "</volume>");
							}
						}

						amfFile.WriteLine(Indent(2) + "</mesh>");
					}

					amfFile.WriteLine(Indent(1) + "</object>");
				}

				HashSet<int> materials = new HashSet<int>();
				foreach (var group in groupedByExtruder)
				{
					foreach (var item in group)
					{
						var materialIndex = item.WorldMaterialIndex();
						if (materialIndex != -1)
						{
							materials.Add(materialIndex);
						}
					}
				}

				foreach (int material in materials)
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

		public static bool SaveUncompressed(IObject3D item, string fileName, MeshOutputSettings outputInfo = null)
		{
			using (var file = new FileStream(fileName, FileMode.Create, FileAccess.Write))
			{
				return Save(item, file, outputInfo);
			}
		}

		private static string Indent(int index)
		{
			return new string(' ', index * 2);
		}

		private static bool IsZipFile(Stream fs)
		{
			int elements = 4;
			if (fs.Length < elements)
			{
				return false;
			}

			byte[] fileToken = new byte[elements];

			fs.Position = 0;
			fs.Read(fileToken, 0, elements);
			fs.Position = 0;

			// Zip files should start with the expected four byte token
			return BitConverter.ToString(fileToken) == "50-4B-03-04";
		}

		private static List<Vector3> ReadAllVertices(XmlReader reader, ProgressData progressData)
		{
			var vertices = new List<Vector3>();

			if (reader.ReadToDescendant("vertex"))
			{
				do
				{
					if (reader.ReadToDescendant("coordinates"))
					{
						var vertex = default(Vector3);

						string nextSibling = null;
						if (reader.ReadToDescendant("x"))
						{
							do
							{
								switch (reader.Name)
								{
									case "x":
										vertex.X = reader.ReadElementContentAsDouble();
										nextSibling = "y";
										break;

									case "y":
										vertex.Y = reader.ReadElementContentAsDouble();
										nextSibling = "z";
										break;

									case "z":
										vertex.Z = reader.ReadElementContentAsDouble();
										nextSibling = null;

										break;
								}
							}
							while (nextSibling != null && (reader.Name != nextSibling ? reader.ReadToNextSibling(nextSibling) : true));
						}

						progressData.ReportProgress0To50();

						vertices.Add(vertex);
					}

					MoveToEndElement(reader, "vertex");
				}
				while (reader.ReadToNextSibling("vertex"));
			}

			return vertices;
		}

		private static void ReadMaterial(XmlReader reader, Dictionary<string, ColorF> materials)
		{
			var id = reader["id"];
			var color = ColorF.White;

			if (reader.ReadToDescendant("color"))
			{
				if (reader.ReadToDescendant("r"))
				{
					color.red = reader.ReadElementContentAsFloat();
					color.green = reader.ReadElementContentAsFloat();
					color.blue = reader.ReadElementContentAsFloat();
				}
			}

			materials.Add(id, color);
		}

		private static List<Vector3> ReadVolume(XmlReader reader, List<Vector3> vertices, Mesh mesh, ProgressData progressData, out string material)
		{
			material = reader["materialid"];

			if (reader.ReadToDescendant("triangle"))
			{
				do
				{
					var indices = new int[3];
					string nextSibling = null;
					if (reader.ReadToDescendant("v1"))
					{
						do
						{
							switch (reader.Name)
							{
								case "v1":
									indices[0] = reader.ReadElementContentAsInt();
									nextSibling = "v2";
									break;

								case "v2":
									indices[1] = reader.ReadElementContentAsInt();
									nextSibling = "v3";
									break;

								case "v3":
									indices[2] = reader.ReadElementContentAsInt();
									nextSibling = null;
									break;
							}
						}
						while (nextSibling != null && (reader.Name != nextSibling ? reader.ReadToNextSibling(nextSibling) : true));
					}

					if (indices[0] != indices[1]
						&& indices[0] != indices[2]
						&& indices[1] != indices[2]
						&& vertices[indices[0]] != vertices[indices[1]]
						&& vertices[indices[1]] != vertices[indices[2]]
						&& vertices[indices[2]] != vertices[indices[0]])
					{
						mesh.CreateFace(new Vector3[] { vertices[indices[0]], vertices[indices[1]], vertices[indices[2]] });
					}

					progressData.ReportProgress0To50();

					MoveToEndElement(reader, "triangle");
				}
				while (reader.ReadToNextSibling("triangle"));
			}

			return vertices;
		}

		internal class ProgressData
		{
			private long bytesInFile;

			private Stopwatch maxProgressReport = new Stopwatch();

			private Stream positionStream;

			private Action<double, string> reportProgress;

			internal ProgressData(Stream positionStream, Action<double, string> reportProgress)
			{
				this.reportProgress = reportProgress;
				this.positionStream = positionStream;
				maxProgressReport.Start();
				bytesInFile = (long)positionStream.Length;
			}

			internal void ReportProgress0To50()
			{
				if (reportProgress != null && maxProgressReport.ElapsedMilliseconds > 200)
				{
					reportProgress(positionStream.Position / (double)bytesInFile * .5, "Loading Mesh");
					maxProgressReport.Restart();
				}
			}
		}

		public static void MoveToEndElement(XmlReader reader, string xname)
		{
			while (!reader.EOF && !(reader.NodeType == XmlNodeType.EndElement && reader.Name == xname))
			{
				reader.Read();
			}
		}
	}
}