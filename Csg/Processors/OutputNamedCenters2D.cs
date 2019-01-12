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
	public class OutputNamedCenters
	{
		private bool outputAsScad = false;
		private string nameWeAreLookingFor;

		static public void Save(CsgObject objectToProcess, string nameWeAreLookingFor, string fileName, bool outputAsScad)
		{
			FileStream file = new FileStream(fileName, FileMode.Create, FileAccess.Write);
			StreamWriter sw = new StreamWriter(file);

			OutputNamedCenters visitor = new OutputNamedCenters(nameWeAreLookingFor, outputAsScad);
			string fileString = visitor.LookForNamedPartRecursive((dynamic)objectToProcess, Matrix4X4.Identity);

			sw.Write(fileString);
			sw.Close();
			file.Close();
		}

		public OutputNamedCenters(string nameWeAreLookingFor, bool outputAsScad)
		{
			this.outputAsScad = outputAsScad;
			this.nameWeAreLookingFor = nameWeAreLookingFor;
		}

		#region Visitor Pattern Functions

		public string LookForNamedPartRecursive(CsgObject objectToProcess, Matrix4X4 accumulatedMatrix)
		{
			throw new Exception("You must write the specialized function for this type.");
		}

		#region PrimitiveWrapper

		public string LookForNamedPartRecursive(CsgObjectWrapper objectToProcess, Matrix4X4 accumulatedMatrix)
		{
			return LookForNamedPartRecursive((dynamic)objectToProcess.root, accumulatedMatrix);
		}

		#endregion PrimitiveWrapper

		#region Solid

		public string LookForNamedPartRecursive(Solid objectToProcess, Matrix4X4 accumulatedMatrix)
		{
			if (objectToProcess.Name == nameWeAreLookingFor)
			{
				Vector3 position = Vector3Ex.TransformPosition(objectToProcess.GetCenter(), accumulatedMatrix);
				if (outputAsScad)
				{
					string output = "translate([" + position.X.ToString() + ", " + position.Y.ToString() + ", " + position.Z.ToString() + "])\n";
					output += "sphere(1, $fn=10);\n";
					return output;
				}
				else
				{
					Vector2 position2D = new Vector2(position.X, position.Y);
					return position2D.X.ToString("0.000") + ", " + position2D.Y.ToString("0.000") + "\n";
				}
			}

			return "";
		}

		#endregion Solid

		#region Union

		public string LookForNamedPartRecursive(Union objectToProcess, Matrix4X4 accumulatedMatrix)
		{
			StringBuilder totalString = new StringBuilder();
			foreach (CsgObject objectToOutput in objectToProcess.allObjects)
			{
				totalString.Append(LookForNamedPartRecursive((dynamic)objectToOutput, accumulatedMatrix));
			}

			return totalString.ToString();
		}

		#endregion Union

		#region Difference

		public string LookForNamedPartRecursive(Difference objectToProcess, Matrix4X4 accumulatedMatrix)
		{
			StringBuilder totalString = new StringBuilder();
			totalString.Append(LookForNamedPartRecursive((dynamic)objectToProcess.primary, accumulatedMatrix));
			foreach (CsgObject objectToOutput in objectToProcess.allSubtracts)
			{
				totalString.Append(LookForNamedPartRecursive((dynamic)objectToOutput, accumulatedMatrix));
			}

			return totalString.ToString();
		}

		#endregion Difference

		#region Intersection

		public string LookForNamedPartRecursive(Intersection objectToProcess, Matrix4X4 accumulatedMatrix)
		{
			return LookForNamedPartRecursive((dynamic)objectToProcess.a, accumulatedMatrix) + LookForNamedPartRecursive((dynamic)objectToProcess.b, accumulatedMatrix);
		}

		#endregion Intersection

		#region Transform

		public string LookForNamedPartRecursive(TransformBase objectToProcess, Matrix4X4 accumulatedMatrix)
		{
			accumulatedMatrix = objectToProcess.transform * accumulatedMatrix;
			return LookForNamedPartRecursive((dynamic)objectToProcess.objectToTransform, accumulatedMatrix);
		}

		#endregion Transform

		#endregion Visitor Pattern Functions
	}
}