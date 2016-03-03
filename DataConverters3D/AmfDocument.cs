using MatterHackers.Agg;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml;
using static MatterHackers.DataConverters3D.AmfProcessing;

namespace MatterHackers.DataConverters3D
{
	public static class AmfDocument
	{
		public static IObject3D Load(string amfPath, ReportProgressRatio reportProgress = null, IObject3D source = null)
		{
			using (var stream = File.OpenRead(amfPath))
			{
				return Load(stream, reportProgress, source);
			}
		}

		public static IObject3D Load(Stream fileStream, ReportProgressRatio reportProgress = null, IObject3D source = null)
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
									PersistNode = false
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
#if true
			double currentMeshProgress = 0;
			double ratioLeftToUse = 1 - parsingFileRatio;
			double progressPerMesh = 1.0 / totalMeshes * ratioLeftToUse;

			foreach (var item in root.Children)
			{
				bool keepProcessing = true;
				item.Mesh.CleanAndMergMesh(reportProgress:
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
					});

				if (!keepProcessing)
				{
					return null;
				}
				currentMeshProgress += progressPerMesh;
			}
#endif

			time.Stop();
			Debug.WriteLine(string.Format("AMF Load in {0:0.00}s", time.Elapsed.TotalSeconds));

			time.Restart();
			bool hasValidMesh = root.Children.Where(item => item.Mesh.Faces.Count > 0).Any();
			Debug.WriteLine("hasValidMesh: " + time.ElapsedMilliseconds);

			if (reportProgress != null)
			{
				bool continueProcessingTemp;
				reportProgress(1, "", out continueProcessingTemp);
			}

			return (hasValidMesh) ? root : null;
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

						bool continueProcessing;
						progressData.ReportProgress0To50(out continueProcessing);

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

					bool continueProcessing;
					progressData.ReportProgress0To50(out continueProcessing);

					reader.MoveToEndElement("triangle");

				} while (reader.ReadToNextSibling("triangle"));
			}

			return vertices;
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
