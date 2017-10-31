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

using MatterHackers.Csg.Operations;
using MatterHackers.Csg.Solids;
using MatterHackers.Csg.Transform;
using MatterHackers.VectorMath;
using System;
using System.IO;
using System.Text;

namespace MatterHackers.Csg.Processors
{
	public class OpenSCadOutput
	{
		public static int NumberOfCylinderSegments = 60;
		public static void Save(CsgObject objectToProcess, string fileName, string prepend = "")
		{
			FileStream file = new FileStream(fileName, FileMode.Create, FileAccess.Write);

			Save(objectToProcess, file, prepend);
			file.Close();
		}

		public static void Save(CsgObject objectToProcess, Stream stream, string prepend = "")
		{
			StreamWriter sw = new StreamWriter(stream);
			string fileString = GetScadString(objectToProcess, prepend);

			sw.Write(fileString);
			sw.Close();
		}

		public static string GetScadString(CsgObject objectToProcess, string prepend = "")
		{
			OpenSCadOutput visitor = new OpenSCadOutput();
			return prepend + visitor.GetScadOutputRecursive((dynamic)objectToProcess);
		}

		public OpenSCadOutput()
		{
		}

		#region Visitor Paten Functions

		public string GetScadOutputRecursive(CsgObject objectToProcess, int level = 0)
		{
			throw new Exception("You must write the specialized function for this type.");
		}

		#region PrimitiveWrapper

		public string GetScadOutputRecursive(CsgObjectWrapper objectToProcess, int level = 0)
		{
			if (objectToProcess is IWriteSCad)
			{
				string info = AddRenderInfoIfReqired(objectToProcess);
				info += ((IWriteSCad)objectToProcess).GetSCadOutput();
				return ApplyIndent(info, level);
			}

			return GetScadOutputRecursive((dynamic)objectToProcess.Root, level);
		}

		#endregion PrimitiveWrapper

		#region Box

		public string GetScadOutputRecursive(BoxPrimitive objectToProcess, int level = 0)
		{
			string info = AddRenderInfoIfReqired(objectToProcess);

			if (objectToProcess.CreateCentered)
			{
				info += "cube([" + objectToProcess.Size.X.ToString() + ", " + objectToProcess.Size.Y.ToString() + ", " + objectToProcess.Size.Z.ToString() + "], center=true);" + AddNameAsComment(objectToProcess);
			}
			else
			{
				info += "cube([" + objectToProcess.Size.X.ToString() + ", " + objectToProcess.Size.Y.ToString() + ", " + objectToProcess.Size.Z.ToString() + "]);" + AddNameAsComment(objectToProcess);
			}
			return ApplyIndent(info, level);
		}

		private string AddRenderInfoIfReqired(CsgObject objectToProcess)
		{
			string info = "";
			if (objectToProcess.Name.Length > 0 && (objectToProcess.Name[0] == '#' || objectToProcess.Name[0] == '%'))
			{
				info = objectToProcess.Name[0] + " ";
			}

			return info;
		}

		#endregion Box

		#region Cylinder

		public string GetScadOutputRecursive(Cylinder.CylinderPrimitive objectToProcess, int level = 0)
		{
			string info = AddRenderInfoIfReqired(objectToProcess);

			info += "cylinder(r1=" + objectToProcess.Radius1.ToString() + ", r2=" + objectToProcess.Radius2.ToString() + ", h=" + objectToProcess.Height.ToString() + ", center=true, $fn={0});".FormatWith(NumberOfCylinderSegments) + AddNameAsComment(objectToProcess);

			return ApplyIndent(info, level);
		}

		#endregion Cylinder

		#region RotateExtrude

		public string GetScadOutputRecursive(RotateExtrude.RotateExtrudePrimitive objectToProcess, int level = 0)
		{
			string info = AddRenderInfoIfReqired(objectToProcess);

			string rotate_extrude = "rotate_extrude(convexity = 10, $fn = {0})".FormatWith(NumberOfCylinderSegments);
			string translate = "translate([" + objectToProcess.AxisOffset.ToString() + ", 0, 0])";
			string thingToRotate = "polygon( points=[";
			foreach (Vector2 point in objectToProcess.Points)
			{
				thingToRotate += "[" + point.X.ToString() + ", " + point.Y.ToString() + "], ";
			}
			thingToRotate += "] );";

			info += rotate_extrude + translate + thingToRotate + AddNameAsComment(objectToProcess);

			return ApplyIndent(info, level);
		}

		#endregion RotateExtrude

		#region LinearExtrude

		/// <summary>
		///  This is the function that generates the correct scad code for our LinearExtrude
		/// </summary>
		/// <param name="objectToProcess"></param>
		/// <param name="level"></param>
		/// <returns></returns>
		public string GetScadOutputRecursive(LinearExtrude.LinearExtrudePrimitive objectToProcess, int level = 0)
		{
			string info = AddRenderInfoIfReqired(objectToProcess);

			string linear_extrude = String.Format("linear_extrude(height = {0}, center = true, convexity = 10, twist = {1})", objectToProcess.height, MathHelper.RadiansToDegrees(objectToProcess.twistRadians));
			string thingToRotate = "polygon( points=[";
			foreach (Vector2 point in objectToProcess.Points)
			{
				thingToRotate += "[" + point.X.ToString() + ", " + point.Y.ToString() + "], ";
			}
			thingToRotate += "] );";

			info += linear_extrude + thingToRotate + AddNameAsComment(objectToProcess);

			return ApplyIndent(info, level);
		}

		#endregion LinearExtrude

		#region Mesh

		/// <summary>
		///  This is the function that generates the correct scad code for our Mesh
		/// </summary>
		/// <param name="objectToProcess"></param>
		/// <param name="level"></param>
		/// <returns></returns>
		public string GetScadOutputRecursive(MeshContainer objectToProcess, int level = 0)
		{
			string info = AddRenderInfoIfReqired(objectToProcess);

			string mesh_string = "import(\"{0}\");".FormatWith(objectToProcess.FilePath);

			info += mesh_string + AddNameAsComment(objectToProcess);

			return ApplyIndent(info, level);
		}

		#endregion Mesh

		#region NGonExtrusion

		public string GetScadOutputRecursive(NGonExtrusion.NGonExtrusionPrimitive objectToProcess, int level = 0)
		{
			string info = AddRenderInfoIfReqired(objectToProcess);

			info += "cylinder(r1=" + objectToProcess.Radius1.ToString() + ", r2=" + objectToProcess.Radius1.ToString() + ", h=" + objectToProcess.Height.ToString() + ", center=true, $fn=" + objectToProcess.NumSides.ToString() + ");" + AddNameAsComment(objectToProcess);

			return ApplyIndent(info, level);
		}

		#endregion NGonExtrusion

		#region Sphere

		public string GetScadOutputRecursive(Sphere objectToProcess, int level = 0)
		{
			string info = AddRenderInfoIfReqired(objectToProcess);

			info += "sphere(" + objectToProcess.Radius.ToString() + ", $fn={0});".FormatWith(NumberOfCylinderSegments) + AddNameAsComment(objectToProcess);
			return ApplyIndent(info, level);
		}

		#endregion Sphere

		#region Transform

		public string GetScadOutputRecursive(TransformBase objectToProcess, int level = 0)
		{
			return ApplyIndent(AddRenderInfoIfReqired(objectToProcess) + "multmatrix(m = [ ["
				+ objectToProcess.transform.Column0.ToString("0.#######") + "],["
				+ objectToProcess.transform.Column1.ToString("0.#######") + "],["
				+ objectToProcess.transform.Column2.ToString("0.#######") + "],["
				+ objectToProcess.transform.Column3.ToString("0.#######") + "] ])" + AddNameAsComment(objectToProcess) + "\n{\n" + GetScadOutputRecursive((dynamic)objectToProcess.objectToTransform, level + 1) + "\n}", level);
		}

		#endregion Transform

		#region Union

		public string GetScadOutputRecursive(Union objectToProcess, int level = 0)
		{
			StringBuilder totalString = new StringBuilder();
			totalString.Append("union()" + AddNameAsComment(objectToProcess) + "\n{\n");
			foreach (CsgObject objectToOutput in objectToProcess.AllObjects)
			{
				totalString.Append(GetScadOutputRecursive((dynamic)objectToOutput, level + 1) + "\n");
			}
			totalString.Append("}");

			return ApplyIndent(totalString.ToString(), level);
		}

		#endregion Union

		#region Difference

		public string GetScadOutputRecursive(Difference objectToProcess, int level = 0)
		{
			StringBuilder totalString = new StringBuilder();
			totalString.Append("difference()" + AddNameAsComment(objectToProcess) + "\n{\n");
			totalString.Append(GetScadOutputRecursive((dynamic)objectToProcess.Primary, level + 1) + "\n");
			foreach (CsgObject objectToOutput in objectToProcess.AllSubtracts)
			{
				totalString.Append(GetScadOutputRecursive((dynamic)objectToOutput, level + 1) + "\n");
			}
			totalString.Append("}");

			return ApplyIndent(totalString.ToString(), level);
		}

		#endregion Difference

		#region Intersection

		public string GetScadOutputRecursive(Intersection objectToProcess, int level = 0)
		{
			return ApplyIndent("intersection()" + AddNameAsComment(objectToProcess) + "\n{\n" + GetScadOutputRecursive((dynamic)objectToProcess.a, level + 1) + "\n" + GetScadOutputRecursive((dynamic)objectToProcess.b, level + 1) + "\n}", level);
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