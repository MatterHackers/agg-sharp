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

using MatterHackers.Agg;
using MatterHackers.PolygonMesh;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml;
using System.Threading;

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

		public static IObject3D Load(string amfPath, ReportProgressRatio<(double ratio, string state)> reportProgress = null, IObject3D source = null)
		{
			using (var stream = File.OpenRead(amfPath))
			{
				return Load(stream, reportProgress, source);
			}
		}

		public static IObject3D Load(Stream fileStream, ReportProgressRatio<(double ratio, string state)> reportProgress = null, IObject3D source = null)
		{
			IObject3D root = source ?? new Object3D();
			root.ItemType = Object3DTypes.Group;

			IObject3D context = null;

			double parsingFileRatio = .5;
			int totalMeshes = 0;
			Stopwatch time = Stopwatch.StartNew();

			using (var decompressedStream = GetCompressedStreamIfRequired(fileStream))
			{
				using (var reader = XmlReader.Create(decompressedStream))
				{
					List<Vector3> vertices = null;
					Mesh mesh = null;

					ProgressData progressData = new ProgressData(fileStream, reportProgress);

					while (reader.Read())
					{
						if (!reader.IsStartElement())
						{
							continue;
						}

						switch (reader.Name)
						{
							case "object":
								// Move context to a new MeshGroup
								context = new Object3D()
								{
									ItemType = Object3DTypes.Model,
									Persistable = false
								};
								root.Children.Add(context);
								break;

							case "mesh":
								// Move context to a new Mesh
								mesh = new Mesh();
								context.Mesh = mesh;

								totalMeshes += 1;
								break;

							case "vertices":
								vertices = ReadAllVertices(reader, progressData);
								break;

							case "volume":
								ReadVolume(reader, vertices, mesh, progressData);
								break;
						}
					}
				}

				fileStream.Dispose();
			}

			double currentMeshProgress = 0;
			double ratioLeftToUse = 1 - parsingFileRatio;
			double progressPerMesh = 1.0 / totalMeshes * ratioLeftToUse;

			foreach (var item in root.Children)
			{
				var keepProcessing = new CancellationTokenSource();
				item.Mesh.CleanAndMergMesh(reportProgress:
					((double progress0To1, string processingState) progress, CancellationTokenSource continueProcessing) =>
					{
						if (reportProgress != null)
						{
							double currentTotalProgress = parsingFileRatio + currentMeshProgress;
							reportProgress((currentTotalProgress + progress.progress0To1 * progressPerMesh, progress.processingState), continueProcessing);
							keepProcessing = continueProcessing;
						}
					});

				if (keepProcessing.IsCancellationRequested)
				{
					return null;
				}
				currentMeshProgress += progressPerMesh;
			}

			time.Stop();
			Debug.WriteLine(string.Format("AMF Load in {0:0.00}s", time.Elapsed.TotalSeconds));

			time.Restart();
			bool hasValidMesh = root.Children.Where(item => item.Mesh.Faces.Count > 0).Any();
			Debug.WriteLine("hasValidMesh: " + time.ElapsedMilliseconds);

			if (reportProgress != null)
			{
				var continueProcessingTemp = new CancellationTokenSource();
				reportProgress((1, ""), continueProcessingTemp);
			}

			return (hasValidMesh) ? root : null;
		}

		/// <summary>
		/// Writes the mesh to disk in a zip container
		/// </summary>
		/// <param name="meshToSave">The mesh to save</param>
		/// <param name="fileName">The file path to save at</param>
		/// <param name="outputInfo">Extra meta data to store in the file</param>
		/// <returns>The results of the save operation</returns>
		public static bool Save(List<MeshGroup> meshToSave, string fileName, MeshOutputSettings outputInfo = null)
		{
			try
			{
				using (Stream stream = File.OpenWrite(fileName))
				using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Create))
				{
					ZipArchiveEntry zipEntry = archive.CreateEntry(Path.GetFileName(fileName));
					using (var entryStream = zipEntry.Open())
					{
						return Save(meshToSave, entryStream, outputInfo);
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

		public static bool Save(List<MeshGroup> meshToSave, Stream stream, MeshOutputSettings outputInfo)
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

				var continueProcessing = new CancellationTokenSource();

				int totalMeshes = 0;
				foreach (MeshGroup meshGroup in meshToSave)
				{
					foreach (Mesh mesh in meshGroup.Meshes)
					{
						totalMeshes++;
					}
				}

				double ratioPerMesh = 1d / totalMeshes;
				double currentRation = 0;
				for (int meshGroupIndex = 0; meshGroupIndex < meshToSave.Count; meshGroupIndex++)
				{
					MeshGroup meshGroup = meshToSave[meshGroupIndex];
					amfFile.WriteLine(Indent(1) + "<object id=\"{0}\">".FormatWith(objectId++));
					{
						int vertexCount = 0;
						List<int> meshVertexStart = new List<int>();
						amfFile.WriteLine(Indent(2) + "<mesh>");
						{
							amfFile.WriteLine(Indent(3) + "<vertices>");
							{
								for (int meshIndex = 0; meshIndex < meshGroup.Meshes.Count; meshIndex++)
								{
									Mesh mesh = meshGroup.Meshes[meshIndex];
									double vertCount = (double)mesh.Vertices.Count;

									meshVertexStart.Add(vertexCount);
									for (int vertexIndex = 0; vertexIndex < mesh.Vertices.Count; vertexIndex++)
									{
										Vertex vertex = mesh.Vertices[vertexIndex];
										if (outputInfo.ReportProgress != null)
										{
											outputInfo.ReportProgress((currentRation + vertexIndex / vertCount * ratioPerMesh * .5, ""), continueProcessing);
										}

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
									currentRation += ratioPerMesh * .5;
								}
							}

							amfFile.WriteLine(Indent(3) + "</vertices>");
							for (int meshIndex = 0; meshIndex < meshGroup.Meshes.Count; meshIndex++)
							{
								Mesh mesh = meshGroup.Meshes[meshIndex];
								int firstVertexIndex = meshVertexStart[meshIndex];
								MeshMaterialData material = MeshMaterialData.Get(mesh);
								if (material.MaterialIndex == -1)
								{
									amfFile.WriteLine(Indent(3) + "<volume>");
								}
								else
								{
									amfFile.WriteLine(Indent(3) + "<volume materialid=\"{0}\">".FormatWith(material.MaterialIndex));
								}

								double faceCount = (double)mesh.Faces.Count;
								for (int faceIndex = 0; faceIndex < mesh.Faces.Count; faceIndex++)
								{
									if (outputInfo.ReportProgress != null)
									{
										outputInfo.ReportProgress((currentRation + faceIndex / faceCount * ratioPerMesh * .5, ""), continueProcessing);
									}

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
										amfFile.WriteLine(Indent(5) + "<v1>{0}</v1>".FormatWith(firstVertexIndex + mesh.Vertices.IndexOf(positionsCCW[0])));
										amfFile.WriteLine(Indent(5) + "<v2>{0}</v2>".FormatWith(firstVertexIndex + mesh.Vertices.IndexOf(positionsCCW[secondIndex])));
										amfFile.WriteLine(Indent(5) + "<v3>{0}</v3>".FormatWith(firstVertexIndex + mesh.Vertices.IndexOf(positionsCCW[thirdIndex])));
										amfFile.WriteLine(Indent(4) + "</triangle>");

										secondIndex = thirdIndex;
										thirdIndex++;
									}
								}

								currentRation += ratioPerMesh * .5;
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
					foreach (Mesh mesh in meshGroup.Meshes)
					{
						MeshMaterialData material = MeshMaterialData.Get(mesh);
						if (material.MaterialIndex != -1)
						{
							materials.Add(material.MaterialIndex);
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

		public static bool SaveUncompressed(List<MeshGroup> meshToSave, string fileName, MeshOutputSettings outputInfo = null)
		{
			using (FileStream file = new FileStream(fileName, FileMode.Create, FileAccess.Write))
			{
				return Save(meshToSave, file, outputInfo);
			}
		}

		private static string Indent(int index)
		{
			return new String(' ', index * 2);
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
						Vector3 vertex = new Vector3();

						string nextSibling = null;
						if (reader.ReadToDescendant("x"))
						{
							do
							{
								switch (reader.Name)
								{
									case "x":
										vertex.x = reader.ReadElementContentAsDouble();
										nextSibling = "y";
										break;

									case "y":
										vertex.y = reader.ReadElementContentAsDouble();
										nextSibling = "z";
										break;

									case "z":
										vertex.z = reader.ReadElementContentAsDouble();
										nextSibling = null;

										break;
								}
							} while (nextSibling != null && reader.ReadToNextSibling(nextSibling));
						}

						var continueProcessing = new CancellationTokenSource();
						progressData.ReportProgress0To50(continueProcessing);

						vertices.Add(vertex);
					}

					reader.MoveToEndElement("vertex");
				} while (reader.ReadToNextSibling("vertex"));
			}

			return vertices;
		}

		private static List<Vector3> ReadVolume(XmlReader reader, List<Vector3> vertices, Mesh mesh, ProgressData progressData)
		{
			string materialId = reader["materialid"];
			if (mesh != null && materialId != null)
			{
				MeshMaterialData material = MeshMaterialData.Get(mesh);
				material.MaterialIndex = int.Parse(materialId);
			}

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
						} while (nextSibling != null && reader.ReadToNextSibling(nextSibling));
					}

					if (indices[0] != indices[1]
						&& indices[0] != indices[2]
						&& indices[1] != indices[2]
						&& vertices[indices[0]] != vertices[indices[1]]
						&& vertices[indices[1]] != vertices[indices[2]]
						&& vertices[indices[2]] != vertices[indices[0]])
					{
						var triangle = new Vertex[]
						{
									mesh.CreateVertex(vertices[indices[0]], CreateOption.CreateNew, SortOption.WillSortLater),
									mesh.CreateVertex(vertices[indices[1]], CreateOption.CreateNew, SortOption.WillSortLater),
									mesh.CreateVertex(vertices[indices[2]], CreateOption.CreateNew, SortOption.WillSortLater),
						};
						mesh.CreateFace(triangle, CreateOption.CreateNew);
					}

					var continueProcessing = new CancellationTokenSource();
					progressData.ReportProgress0To50(continueProcessing);

					reader.MoveToEndElement("triangle");
				} while (reader.ReadToNextSibling("triangle"));
			}

			return vertices;
		}

		internal class ProgressData
		{
			private long bytesInFile;
			private bool loadCanceled;
			private Stopwatch maxProgressReport = new Stopwatch();
			private Stream positionStream;
			private ReportProgressRatio<(double ratio, string state)> reportProgress;

			internal ProgressData(Stream positionStream, ReportProgressRatio<(double ratio, string state)> reportProgress)
			{
				this.reportProgress = reportProgress;
				this.positionStream = positionStream;
				maxProgressReport.Start();
				bytesInFile = (long)positionStream.Length;
			}

			internal bool LoadCanceled { get { return loadCanceled; } }

			internal void ReportProgress0To50(CancellationTokenSource continueProcessing)
			{
				if (reportProgress != null && maxProgressReport.ElapsedMilliseconds > 200)
				{
					reportProgress((positionStream.Position / (double)bytesInFile * .5, "Loading Mesh"), continueProcessing);
					if (continueProcessing.IsCancellationRequested)
					{
						loadCanceled = true;
					}
					maxProgressReport.Restart();
				}
			}
		}
	}

	public static class ExtensionMethods
	{
		public static void MoveToEndElement(this XmlReader reader, string xname)
		{
			while (!reader.EOF && !(reader.NodeType == XmlNodeType.EndElement && reader.Name == xname))
			{
				reader.Read();
			}
		}
	}
}