/*
Copyright (c) 2015, Lars Brubaker
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
using MatterHackers.PolygonMesh.Csg;
using MatterHackers.PolygonMesh.Processors;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace MatterHackers.DataConverters3D
{
	public static class MeshFileIo
	{
		public static string ValidFileExtensions()
		{
			return ".STL;.AMF";
		}

		public static IObject3D Load(Stream fileStream, string fileExtension, ReportProgressRatio reportProgress = null, IObject3D source = null)
		{
			switch (fileExtension.ToUpper())
			{
				case ".STL":

					var result = source ?? new Object3D { ItemType = Object3DTypes.Model };
					result.Mesh = StlProcessing.Load(fileStream, reportProgress);
					return result;

				case ".AMF":
					//return AmfProcessing.Load(fileStream, reportProgress);
					return AmfDocument.Load(fileStream, reportProgress, source);

				default:
					return null;
			}
		}

		public static IObject3D Load(string meshPathAndFileName, ReportProgressRatio reportProgress = null, IObject3D source = null)
		{
			try
			{
				using (Stream stream = new FileStream(meshPathAndFileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
				{
					var loadedItem = Load(stream, Path.GetExtension(meshPathAndFileName), reportProgress, source);
					loadedItem.MeshPath = meshPathAndFileName;

					return loadedItem;
				}
			}
			catch(Exception e)
			{
				Debug.Print(e.Message);
				return null;
			}
		}

		public static async Task<IObject3D> LoadAsync(string meshPathAndFileName, ReportProgressRatio reportProgress = null)
		{
			return await Task.Run(() => Load(meshPathAndFileName, reportProgress));
		}

		public static bool Save(IObject3D context, string meshPathAndFileName, MeshOutputSettings outputInfo = null, ReportProgressRatio reportProgress = null)
		{
			// TODO: Seems conceptually correct but needs validation and refinements
			var meshGroups = new List<MeshGroup> { context.Flatten() };
			return Save(meshGroups, meshPathAndFileName, outputInfo, reportProgress);
		}

		public static bool Save(Mesh mesh, string meshPathAndFileName, MeshOutputSettings outputInfo = null)
		{
			return Save(new MeshGroup(mesh), meshPathAndFileName, outputInfo);
		}

		public static bool Save(MeshGroup meshGroupToSave, string meshPathAndFileName, MeshOutputSettings outputInfo = null)
		{
			List<MeshGroup> meshGroupsToSave = new List<MeshGroup>();
			meshGroupsToSave.Add(meshGroupToSave);
			return Save(meshGroupsToSave, meshPathAndFileName, outputInfo);
		}

		public static bool Save(List<MeshGroup> meshGroupsToSave, string meshPathAndFileName, MeshOutputSettings outputInfo = null, ReportProgressRatio reportProgress = null)
		{
			try
			{
				if (outputInfo == null)
				{
					outputInfo = new MeshOutputSettings();
				}
				switch (Path.GetExtension(meshPathAndFileName).ToUpper())
				{
					case ".STL":
						Mesh mesh = DoMerge(meshGroupsToSave, outputInfo);
						return StlProcessing.Save(mesh, meshPathAndFileName, outputInfo);

					case ".AMF":
						outputInfo.ReportProgress = reportProgress;
						return AmfProcessing.Save(meshGroupsToSave, meshPathAndFileName, outputInfo);

					default:
						return false;
				}
			}
			catch (Exception)
			{
				return false;
			}
		}

		public static Mesh DoMerge(List<MeshGroup> meshGroupsToMerge, MeshOutputSettings outputInfo)
		{
			Mesh allPolygons = new Mesh();
			if (outputInfo.CsgOptionState == MeshOutputSettings.CsgOption.DoCsgMerge)
			{
				foreach (MeshGroup meshGroup in meshGroupsToMerge)
				{
					foreach (Mesh mesh in meshGroup.Meshes)
					{
						allPolygons = CsgOperations.Union(allPolygons, mesh);
					}
				}
			}
			else
			{
				foreach (MeshGroup meshGroup in meshGroupsToMerge)
				{
					foreach (Mesh mesh in meshGroup.Meshes)
					{
						int currentMeshMaterialIntdex = MeshMaterialData.Get(mesh).MaterialIndex;
						if (outputInfo.MaterialIndexsToSave == null || outputInfo.MaterialIndexsToSave.Contains(currentMeshMaterialIntdex))
						{
							foreach (Face face in mesh.Faces)
							{
								List<Vertex> faceVertices = new List<Vertex>();
								foreach (FaceEdge faceEdgeToAdd in face.FaceEdges())
								{
									// we allow duplicates (the true) to make sure we are not changing the loaded models acuracy.
									Vertex newVertex = allPolygons.CreateVertex(faceEdgeToAdd.firstVertex.Position, CreateOption.CreateNew, SortOption.WillSortLater);
									faceVertices.Add(newVertex);
								}

								// we allow duplicates (the true) to make sure we are not changing the loaded models acuracy.
								allPolygons.CreateFace(faceVertices.ToArray(), CreateOption.CreateNew);
							}
						}
					}
				}

				allPolygons.CleanAndMergMesh();
			}

			return allPolygons;
		}

		public static long GetEstimatedMemoryUse(string fileLocation)
		{
			switch (Path.GetExtension(fileLocation).ToUpper())
			{
				case ".STL":
					return StlProcessing.GetEstimatedMemoryUse(fileLocation);

				case ".AMF":
					return AmfProcessing.GetEstimatedMemoryUse(fileLocation);
			}

			return 0;
		}

		public static string ComputeSHA1(string destPath)
		{
			// Alternatively: MD5.Create(),  new SHA256Managed()
			var timer = Stopwatch.StartNew();

			using (var stream = new BufferedStream(File.OpenRead(destPath), 1200000))
			{
				byte[] checksum = SHA1.Create().ComputeHash(stream);
				Console.WriteLine("SHA1 computed in {0}ms", timer.ElapsedMilliseconds);
				return BitConverter.ToString(checksum).Replace("-", String.Empty);
			}
		}

	}
}