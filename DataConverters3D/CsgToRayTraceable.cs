/*
Copyright (c) 2013, Lars Brubaker
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
using MatterHackers.Csg;
using MatterHackers.Csg.Operations;
using MatterHackers.Csg.Solids;
using MatterHackers.Csg.Transform;
using MatterHackers.RayTracer;
using MatterHackers.RayTracer.Traceable;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;
using System.Text;

namespace MatterHackers.DataConverters3D
{
	public class CsgToRayTraceable
	{
		public static MaterialAbstract DefaultMaterial = new SolidMaterial(RGBA_Floats.Green, 0, 0, 0);

		public CsgToRayTraceable()
		{
		}

		#region Visitor Pattern Functions

		public IPrimitive GetIPrimitiveRecursive(CsgObject objectToProcess)
		{
			throw new Exception("You must write the specialized function for this type.");
		}

		#region PrimitiveWrapper

		public IPrimitive GetIPrimitiveRecursive(CsgObjectWrapper objectToProcess)
		{
			return GetIPrimitiveRecursive((dynamic)objectToProcess.Root);
		}

		#endregion PrimitiveWrapper

		#region Box

		public IPrimitive GetIPrimitiveRecursive(BoxPrimitive objectToProcess)
		{
			return new BoxShape(Vector3.Zero, objectToProcess.Size, DefaultMaterial);
		}

		#endregion Box

		#region Cylinder

		public IPrimitive GetIPrimitiveRecursive(Cylinder.CylinderPrimitive objectToProcess)
		{
			return new CylinderShape(objectToProcess.Radius1, objectToProcess.Height, DefaultMaterial);
		}

		#endregion Cylinder

		#region Sphere

		public IPrimitive GetIPrimitiveRecursive(Sphere objectToProcess)
		{
			throw new NotImplementedException();
#if false
            string info = "";
            if (objectToProcess.Name.Length > 0 && (objectToProcess.Name[0] == '#' || objectToProcess.Name[0] == '%'))
            {
                info = objectToProcess.Name[0] + " ";
            }

            info += "sphere(" + objectToProcess.Radius.ToString() + ");" + AddNameAsComment(objectToProcess);
            return ApplyIndent(info, level);
#endif
		}

		#endregion Sphere

		#region Transform

		public IPrimitive GetIPrimitiveRecursive(TransformBase objectToProcess)
		{
			return new Transform(GetIPrimitiveRecursive((dynamic)objectToProcess.ObjectToTransform), objectToProcess.ActiveTransform);
		}

		#endregion Transform

		#region Union

		public IPrimitive GetIPrimitiveRecursive(Union objectToProcess)
		{
			List<IPrimitive> items = new List<IPrimitive>();
			foreach (CsgObject copiedObject in objectToProcess.AllObjects)
			{
				items.Add(GetIPrimitiveRecursive((dynamic)copiedObject));
			}

			return BoundingVolumeHierarchy.CreateNewHierachy(items);
		}

		#endregion Union

		#region Difference

		public IPrimitive GetIPrimitiveRecursive(MatterHackers.Csg.Operations.Difference objectToProcess)
		{
			List<IPrimitive> subtractItems = new List<IPrimitive>();
			foreach (CsgObject copiedObject in objectToProcess.AllSubtracts)
			{
				subtractItems.Add(GetIPrimitiveRecursive((dynamic)copiedObject));
			}

			return new MatterHackers.RayTracer.Traceable.Difference(GetIPrimitiveRecursive((dynamic)objectToProcess.Primary), BoundingVolumeHierarchy.CreateNewHierachy(subtractItems));
		}

		#endregion Difference

		#region Intersection

		public IPrimitive GetIPrimitiveRecursive(Intersection objectToProcess)
		{
			throw new NotImplementedException();
			//return ApplyIndent("intersection()" + AddNameAsComment(objectToProcess) + "\n{\n" + GetRayTraceableRecursive((dynamic)objectToProcess.a, level + 1) + "\n" + GetRayTraceableRecursive((dynamic)objectToProcess.b, level + 1) + "\n}", level);
		}

		#endregion Intersection

		#endregion Visitor Patern Functions

		#region SCAD Formating Functions

		protected string AddNameAsComment(CsgObject objectToProcess)
		{
			if (objectToProcess.Name != "")
			{
				return " // " + objectToProcess.Name;
			}

			return "";
		}

		protected string ApplyIndent(string source, int level)
		{
			if (level > 0)
			{
				StringBuilder final = new StringBuilder();

				string[] splitOnReturn = source.Split('\n');
				for (int i = 0; i < splitOnReturn.Length; i++)
				{
					final.Append(Spaces(4));
					final.Append(splitOnReturn[i]);
					if (i < splitOnReturn.Length - 1)
					{
						final.Append('\n');
					}
				}

				return final.ToString();
			}

			return source;
		}

		private string Spaces(int num)
		{
			StringBuilder spaces = new StringBuilder();
			for (int i = 0; i < num; i++)
			{
				spaces.Append(" ");
			}

			return spaces.ToString();
		}

		#endregion SCAD Formating Functions
	}
}