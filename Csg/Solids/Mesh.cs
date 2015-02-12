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
using System.IO;

using MatterHackers.VectorMath;
using MatterHackers.Csg.Transform;

namespace MatterHackers.Csg.Solids
{
	public class Mesh : CsgObject
	{
		PolygonMesh.Mesh polygonMesh;

		string sourceFileName = null;
		public string FilePath
		{
			get
			{
				if (sourceFileName != null)
				{
					return sourceFileName;
				}
				else
				{
					if (polygonMesh != null)
					{
						// save to disk and return the path
						sourceFileName = Path.ChangeExtension(Path.GetRandomFileName(), ".stl");
						PolygonMesh.Processors.StlProcessing.Save(polygonMesh, sourceFileName);
						return sourceFileName;
					}
					else
					{
						throw new Exception("You have to have a mesh or a valid mesh file.");
					}
				}
			}
		}

		public Mesh(string fileOnDisk, string name = "")
			: base(name)
		{
			sourceFileName = fileOnDisk;
		}

		public Mesh(MatterHackers.PolygonMesh.Mesh polygonMesh, string name = "")
			: base(name)
		{
			this.polygonMesh = polygonMesh;
		}

		public PolygonMesh.Mesh GetMesh()
		{
			if (polygonMesh == null)
			{
				polygonMesh = MatterHackers.PolygonMesh.Processors.StlProcessing.Load(sourceFileName);
			}

			return polygonMesh;
		}

		public override AxisAlignedBoundingBox GetAxisAlignedBoundingBox()
		{
			return GetMesh().GetAxisAlignedBoundingBox();
		}
	}
}
