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
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Net3dBool
{
	/// <summary>
	/// Represents a 3d face vertex.
	/// </summary>
	public class Vertex
	{
		public Vector3 Position;
		// references to vertices conected to it by an edge
		private List<Vertex> adjacentVertices;
		// vertex status relative to other object
		public FaceStatus Status { get; set; }

		// tolerance value to test equalities
		private readonly static double EqualityTolerance = 1e-5f;

		/// <summary>
		/// Constructs a vertex with unknown status
		/// </summary>
		/// <param name="position"></param>
		public Vertex(Vector3 position)
		{
			this.Position = position;

			adjacentVertices = new List<Vertex>();
			Status = FaceStatus.Unknown;
		}

		/// <summary>
		/// Constructs a vertex with unknown status
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <param name="z"></param>
		public Vertex(double x, double y, double z)
		{
			this.Position.X = x;
			this.Position.Y = y;
			this.Position.Z = z;

			adjacentVertices = new List<Vertex>();
			Status = FaceStatus.Unknown;
		}

		/// <summary>
		/// Constructs a vertex with definite status
		/// </summary>
		/// <param name="position">vertex position</param>
		/// <param name="status">vertex status - UNKNOWN, BOUNDARY, INSIDE or OUTSIDE</param>
		public Vertex(Vector3 position, FaceStatus status)
		{
			Position.X = position.X;
			Position.Y = position.Y;
			Position.Z = position.Z;

			adjacentVertices = new List<Vertex>();
			this.Status = status;
		}

		/// <summary>
		/// Constructs a vertex with a definite status
		/// </summary>
		/// <param name="x">coordinate on the x axis</param>
		/// <param name="y">coordinate on the y axis</param>
		/// <param name="z">coordinate on the z axis</param>
		/// <param name="status">vertex status - UNKNOWN, BOUNDARY, INSIDE or OUTSIDE</param>
		public Vertex(double x, double y, double z, FaceStatus status)
		{
			this.Position = new Vector3(x, y, z);

			adjacentVertices = new List<Vertex>();
			this.Status = status;
		}

		/// <summary>
		/// Default constructor
		/// </summary>
		private Vertex()
		{
		}

		/// <summary>
		/// Clones the vertex object
		/// </summary>
		/// <returns>cloned vertex object</returns>
		public Vertex Clone()
		{
			Vertex clone = new Vertex();
			clone.Position = Position;
			clone.Status = Status;
			clone.adjacentVertices = new List<Vertex>();
			for (int i = 0; i < adjacentVertices.Count; i++)
			{
				clone.adjacentVertices.Add(adjacentVertices[i].Clone());
			}

			return clone;
		}

		public override string ToString()
		{
			return "(" + Position.X + ", " + Position.Y + ", " + Position.Z + ")";
		}

		/// <summary>
		/// Checks if an vertex is equal to another. To be equal, they have to have the
		/// same coordinates(with some tolerance)
		/// </summary>
		/// <param name="vertex"></param>
		/// <returns></returns>
		public bool Equals(Vertex vertex)
		{
			return Position.Equals(vertex.Position, EqualityTolerance);
		}

		/// <summary>
		/// Gets an array with the adjacent vertices
		/// </summary>
		/// <returns></returns>
		public Vertex[] GetAdjacentVertices()
		{
			Vertex[] vertices = new Vertex[adjacentVertices.Count];
			for (int i = 0; i < adjacentVertices.Count; i++)
			{
				vertices[i] = adjacentVertices[i];
			}
			return vertices;
		}

		/// <summary>
		/// Sets a vertex as being adjacent to it
		/// </summary>
		/// <param name="adjacentVertex"></param>
		public void AddAdjacentVertex(Vertex adjacentVertex)
		{
			if (!adjacentVertices.Contains(adjacentVertex))
			{
				adjacentVertices.Add(adjacentVertex);
			}
		}

		/// <summary>
		/// Sets the vertex status, setting equally the adjacent ones
		/// </summary>
		/// <param name="status"></param>
		public void Mark(FaceStatus status)
		{
			var items = new Stack<Vertex>(new Vertex[] { this });
			while (items.Any())
			{
				Vertex item = items.Pop();

				//mark vertex
				item.Status = status;

				//mark adjacent vertices
				Vertex[] adjacentVerts = GetAdjacentVertices();
				for (int i = 0; i < adjacentVerts.Length; i++)
				{
					if (adjacentVerts[i].Status == FaceStatus.Unknown)
					{
						items.Push(adjacentVertices[i]);
					}
				}
			}
		}
	}
}

