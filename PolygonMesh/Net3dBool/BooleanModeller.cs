/*
The MIT License (MIT)

Copyright (c) 2014 Sebastian Loncar

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

See:
D. H. Laidlaw, W. B. Trumbore, and J. F. Hughes.
"Constructive Solid Geometry for Polyhedral Objects"
SIGGRAPH Proceedings, 1986, p.161.

original author: Danilo Balby Silva Castanheira (danbalby@yahoo.com)

Ported from Java to C# by Sebastian Loncar, Web: http://loncar.de
Optomized and refactored by: Lars Brubaker (larsbrubaker@matterhackers.com)
Project: https://github.com/MatterHackers/agg-sharp (an included library)
*/

using MatterHackers.VectorMath;
using System.Collections.Generic;
using MatterHackers.Agg;
using System;
using System.Threading;
using System.Text;
using System.IO;

namespace Net3dBool
{
	/// <summary>
	/// Class used to apply boolean operations on solids.
	/// Two 'Solid' objects are submitted to this class constructor. There is a methods for
	/// each boolean operation. Each of these return a 'Solid' resulting from the application
	/// of its operation into the submitted solids.
	/// </summary>
	public class BooleanModeller
	{
		/** solid where boolean operations will be applied */
		private Object3D object1;
		private Object3D object2;

		//--------------------------------CONSTRUCTORS----------------------------------//

		/**
     * Constructs a BooleanModeller object to apply boolean operation in two solids.
     * Makes preliminary calculations
     *
     * @param solid1 first solid where boolean operations will be applied
     * @param solid2 second solid where boolean operations will be applied
     */

		public BooleanModeller(Solid solid1, Solid solid2)
			: this(solid1, solid2, null, CancellationToken.None)
		{
		}

		public abstract class DebugFace : Net3dBool.Object3D.IFaceDebug
		{
			public static bool HasPosition(Face face, Vector3 position)
			{
				if (face.v1.Position.Equals(position, .0001))
				{
					return true;
				}
				if (face.v2.Position.Equals(position, .0001))
				{
					return true;
				}
				if (face.v3.Position.Equals(position, .0001))
				{
					return true;
				}

				return false;
			}

			public static bool AreEqual(double a, double b, double errorRange = .001)
			{
				if (a < b + errorRange
					&& a > b - errorRange)
				{
					return true;
				}

				return false;
			}

			public static bool FaceAtHeight(Face face, double height)
			{
				if (!AreEqual(face.v1.Position.Z, height))
				{
					return false;
				}

				if (!AreEqual(face.v2.Position.Z, height))
				{
					return false;
				}

				if (!AreEqual(face.v3.Position.Z, height))
				{
					return false;
				}

				return true;
			}

			public static bool FaceAtXy(Face face, double x, double y)
			{
				if (!AreEqual(face.v1.Position.X, x)
					|| !AreEqual(face.v1.Position.Y, y))
				{
					return false;
				}

				if (!AreEqual(face.v2.Position.X, x)
					|| !AreEqual(face.v2.Position.Y, y))
				{
					return false;
				}

				if (!AreEqual(face.v3.Position.X, x)
					|| !AreEqual(face.v3.Position.Y, y))
				{
					return false;
				}

				return true;
			}

			public static string GetCoords(Face face)
			{
				var offset = new Vector2(10, 2);
				var scale = 30;
				Vector2 p1 = (new Vector2(face.v1.Position.X, -face.v1.Position.Y) + offset) * scale;
				Vector2 p2 = (new Vector2(face.v2.Position.X, -face.v2.Position.Y) + offset) * scale;
				Vector2 p3 = (new Vector2(face.v3.Position.X, -face.v3.Position.Y) + offset) * scale;
				string coords = $"{p1.X:0.0}, {p1.Y:0.0}";
				coords += $", {p2.X:0.0}, {p2.Y:0.0}";
				coords += $", {p3.X:0.0}, {p3.Y:0.0}";
				return $"<polygon points=\"{coords}\" style=\"fill: #FF000022; stroke: purple; stroke - width:1\" />";
			}

			public abstract void Evaluate(Face face);
		}

		public class DebugSplitFace : DebugFace
		{
			string firstPolygonDebug;
			StringBuilder htmlContent = new StringBuilder();
			StringBuilder allPolygonDebug = new StringBuilder();
			StringBuilder individualPolygonDebug = new StringBuilder();
			int allCount;

			public override void Evaluate(Face thisFace)
			{
				if (FaceAtHeight(thisFace, -3))
				{
					string coords = GetCoords(thisFace);
					if (allCount < 12)
					{
						int svgHeight = 340;

						allPolygonDebug.AppendLine(coords);

						if (allCount == 0)
						{
							firstPolygonDebug = coords;

							htmlContent.AppendLine("<!DOCTYPE html>");
							htmlContent.AppendLine("<html>");
							htmlContent.AppendLine("<body>");
							htmlContent.AppendLine("<br>Full</br>");
						}
						else
						{
							individualPolygonDebug.AppendLine($"<br>{allCount}</br>");
							individualPolygonDebug.AppendLine($"<svg height='{svgHeight}' width='640'>");
							individualPolygonDebug.AppendLine(firstPolygonDebug);
							individualPolygonDebug.AppendLine(coords);
							individualPolygonDebug.AppendLine("</svg>");
						}

						if (allCount == 6)
						{
							int a = 0;
						}

						allCount++;

						if (allCount == 12)
						{
							htmlContent.AppendLine($"<svg height='{svgHeight}' width='640'>");

							htmlContent.Append(allPolygonDebug.ToString());

							htmlContent.AppendLine("</svg>");

							htmlContent.Append(individualPolygonDebug.ToString());

							htmlContent.AppendLine("</body>");
							htmlContent.AppendLine("</html>");

							File.WriteAllText("C:/Temp/DebugOutput.html", htmlContent.ToString());
						}
					}
				}
				if (FaceAtXy(thisFace, -5, -5))
				{
					int a = 0;
				}
				if (HasPosition(thisFace, new Vector3(-5, -5, -3))
					&& FaceAtHeight(thisFace, -3))
				{
					int a = 0;
				}
			}
		}

		public class DebugCuttingFace : DebugFace
		{
			string firstPolygonDebug;
			StringBuilder htmlContent = new StringBuilder();
			StringBuilder allPolygonDebug = new StringBuilder();
			StringBuilder individualPolygonDebug = new StringBuilder();
			int allCount;

			public override void Evaluate(Face thisFace)
			{
				if (FaceAtHeight(thisFace, -3))
				{
					string coords = GetCoords(thisFace);
					if (allCount < 12)
					{
						int svgHeight = 340;

						allPolygonDebug.AppendLine(coords);

						if (allCount == 0)
						{
							firstPolygonDebug = coords;

							htmlContent.AppendLine("<!DOCTYPE html>");
							htmlContent.AppendLine("<html>");
							htmlContent.AppendLine("<body>");
							htmlContent.AppendLine("<br>Full</br>");
						}
						else
						{
							individualPolygonDebug.AppendLine($"<br>{allCount}</br>");
							individualPolygonDebug.AppendLine($"<svg height='{svgHeight}' width='640'>");
							individualPolygonDebug.AppendLine(firstPolygonDebug);
							individualPolygonDebug.AppendLine(coords);
							individualPolygonDebug.AppendLine("</svg>");
						}

						if (allCount == 6)
						{
							int a = 0;
						}

						allCount++;

						if (allCount == 12)
						{
							htmlContent.AppendLine($"<svg height='{svgHeight}' width='640'>");

							htmlContent.Append(allPolygonDebug.ToString());

							htmlContent.AppendLine("</svg>");

							htmlContent.Append(individualPolygonDebug.ToString());

							htmlContent.AppendLine("</body>");
							htmlContent.AppendLine("</html>");

							File.WriteAllText("C:/Temp/DebugOutput.html", htmlContent.ToString());
						}
					}
				}
				if (FaceAtXy(thisFace, -5, -5))
				{
					int a = 0;
				}
				if (HasPosition(thisFace, new Vector3(-5, -5, -3))
					&& FaceAtHeight(thisFace, -3))
				{
					int a = 0;
				}
			}
		}

		public BooleanModeller(Solid solid1, Solid solid2, Action<string, double> reporter, CancellationToken cancelationToken)
		{
			//representation to apply boolean operations
			reporter?.Invoke("Object3D1", 0.1);
			object1 = new Object3D(solid1);

			reporter?.Invoke("Object3D2", 0.2);
			object2 = new Object3D(solid2);

			Object3D object1Copy = new Object3D(solid1);

			//split the faces so that none of them intercepts each other
			reporter?.Invoke("Split Faces2", 0.4);
			object1.SplitFaces(object2, cancelationToken);

			reporter?.Invoke("Split Faces1", 0.6);
			object2.SplitFaces(object1Copy, cancelationToken);// new DebugSplitFace());//, new DebugCuttingFace());

			//classify faces as being inside or outside the other solid
			reporter?.Invoke("Classify Faces2", 0.8);
			object1.ClassifyFaces(object2);

			reporter?.Invoke("Classify Faces1", 0.9);
			object2.ClassifyFaces(object1);

			reporter?.Invoke("Classify Faces1", 1);
		}

		private BooleanModeller()
		{
		}

		//-------------------------------BOOLEAN_OPERATIONS-----------------------------//

		/**
     * Gets the solid generated by the union of the two solids submitted to the constructor
     *
     * @return solid generated by the union of the two solids submitted to the constructor
     */

		public Solid GetDifference()
		{
			object2.InvertInsideFaces();
			Solid result = ComposeSolid(Status.OUTSIDE, Status.OPPOSITE, Status.INSIDE);
			object2.InvertInsideFaces();

			return result;
		}

		public Solid GetIntersection()
		{
			return ComposeSolid(Status.INSIDE, Status.SAME, Status.INSIDE);
		}

		public Solid GetUnion()
		{
			return ComposeSolid(Status.OUTSIDE, Status.SAME, Status.OUTSIDE);
		}

		/**
     * Gets the solid generated by the intersection of the two solids submitted to the constructor
     *
     * @return solid generated by the intersection of the two solids submitted to the constructor.
     * The generated solid may be empty depending on the solids. In this case, it can't be used on a scene
     * graph. To check this, use the Solid.isEmpty() method.
     */
		/** Gets the solid generated by the difference of the two solids submitted to the constructor.
     * The fist solid is substracted by the second.
     *
     * @return solid generated by the difference of the two solids submitted to the constructor
     */
		//--------------------------PRIVATES--------------------------------------------//

		/**
     * Composes a solid based on the faces status of the two operators solids:
     * Status.INSIDE, Status.OUTSIDE, Status.SAME, Status.OPPOSITE
     *
     * @param faceStatus1 status expected for the first solid faces
     * @param faceStatus2 other status expected for the first solid faces
     * (expected a status for the faces coincident with second solid faces)
     * @param faceStatus3 status expected for the second solid faces
     */

		private Solid ComposeSolid(Status faceStatus1, Status faceStatus2, Status faceStatus3)
		{
			var vertices = new List<Vertex>();
			var indices = new List<int>();

			//group the elements of the two solids whose faces fit with the desired status
			GroupObjectComponents(object1, vertices, indices, faceStatus1, faceStatus2);
			GroupObjectComponents(object2, vertices, indices, faceStatus3, faceStatus3);

			//turn the arrayLists to arrays
			Vector3[] verticesArray = new Vector3[vertices.Count];
			for (int i = 0; i < vertices.Count; i++)
			{
				verticesArray[i] = vertices[i].GetPosition();
			}
			int[] indicesArray = new int[indices.Count];
			for (int i = 0; i < indices.Count; i++)
			{
				indicesArray[i] = indices[i];
			}

			//returns the solid containing the grouped elements
			return new Solid(verticesArray, indicesArray);
		}

		/**
     * Fills solid arrays with data about faces of an object generated whose status
     * is as required
     *
     * @param object3d solid object used to fill the arrays
     * @param vertices vertices array to be filled
     * @param indices indices array to be filled
     * @param faceStatus1 a status expected for the faces used to to fill the data arrays
     * @param faceStatus2 a status expected for the faces used to to fill the data arrays
     */

		private void GroupObjectComponents(Object3D obj, List<Vertex> vertices, List<int> indices, Status faceStatus1, Status faceStatus2)
		{
			//for each face..
			foreach(Face face in obj.Faces.AllObjects())
			{
				//if the face status fits with the desired status...
				if (face.GetStatus() == faceStatus1 || face.GetStatus() == faceStatus2)
				{
					//adds the face elements into the arrays
					Vertex[] faceVerts = { face.v1, face.v2, face.v3 };
					for (int j = 0; j < faceVerts.Length; j++)
					{
						if (vertices.Contains(faceVerts[j]))
						{
							indices.Add(vertices.IndexOf(faceVerts[j]));
						}
						else
						{
							indices.Add(vertices.Count);
							vertices.Add(faceVerts[j]);
						}
					}
				}
			}
		}
	}
}