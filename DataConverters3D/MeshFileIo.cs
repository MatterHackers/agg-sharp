/*
Copyright (c) 2018, Lars Brubaker
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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.PolygonMesh;
using MatterHackers.PolygonMesh.Csg;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.VectorMath;

namespace MatterHackers.DataConverters3D
{
	public static class MeshFileIo
	{
		public static bool Save(IObject3D item, string meshPathAndFileName, CancellationToken cancellationToken, MeshOutputSettings outputInfo = null, Action<double, string> reportProgress = null)
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
						Mesh mesh = DoMergeAndTransform(item, outputInfo, cancellationToken);
						return StlProcessing.Save(mesh, meshPathAndFileName, cancellationToken, outputInfo);

					case ".AMF":
						outputInfo.ReportProgress = reportProgress;
						return AmfDocument.Save(item, meshPathAndFileName, outputInfo);

					case ".OBJ":
						outputInfo.ReportProgress = reportProgress;
						return ObjSupport.Save(item, meshPathAndFileName, outputInfo);

					default:
						return false;
				}
			}
			catch (Exception)
			{
				return false;
			}
		}

		public static Mesh DoMergeAndTransform(IObject3D item, MeshOutputSettings outputInfo, CancellationToken cancellationToken)
		{
			var visibleMeshes = item.VisibleMeshes().Where((i) => i.WorldPersistable());
			if (visibleMeshes.Count() == 1)
			{
				var first = visibleMeshes.First();
				if(first.WorldMatrix() == Matrix4X4.Identity)
				{
					return first.Mesh;
				}
			}

			Mesh allPolygons = new Mesh();

			foreach (var rawItem in visibleMeshes)
			{
				var mesh = rawItem.Mesh.Copy(cancellationToken);
				mesh.Transform(rawItem.WorldMatrix());
				if (outputInfo.CsgOptionState == MeshOutputSettings.CsgOption.DoCsgMerge)
				{
					allPolygons = CsgOperations.Union(allPolygons, mesh, null, cancellationToken);
				}
				else
				{
					allPolygons.CopyFaces(mesh);
				}
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
					return AmfDocument.GetEstimatedMemoryUse(fileLocation);

				case ".OBJ":
					throw new NotImplementedException();
			}

			return 0;
		}
	}
}