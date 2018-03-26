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

namespace Net3dBool
{
	public enum Status { UNKNOWN, INSIDE, OUTSIDE, SAME, OPPOSITE, BOUNDARY };

	/// <summary>
	/// Representation of a 3D face (triangle).
	/// </summary>
	public class Face //: IPrimitive
	{
		/** first vertex */
		public Vertex v1;
		/** second vertex */
		public Vertex v2;
		/** third vertex */
		public Vertex v3;

		private Vector3 center;

		/** face status relative to a solid  */
		private readonly static double EqualityTolerance = 1e-10f;
		private enum Side { UP, DOWN, ON, NONE };
		private AxisAlignedBoundingBox boundCache;
		private bool cachedBounds = false;
		private Plane planeCache;
		private Status status;

		/** face status if it is still unknown */
		/** face status if it is inside a solid */
		/** face status if it is outside a solid */
		/** face status if it is coincident with a solid face */
		/** face status if it is coincident with a solid face with opposite orientation*/
		/** point status if it is up relative to an edge - see linePositionIn_ methods */
		/** point status if it is down relative to an edge - see linePositionIn_ methods */
		/** point status if it is on an edge - see linePositionIn_ methods */
		/** point status if it isn't up, down or on relative to an edge - see linePositionIn_ methods */
		/** tolerance value to test equalities */
		//---------------------------------CONSTRUCTORS---------------------------------//

		/// <summary>
		/// Default constructor
		/// </summary>
		private Face()
		{
		}

		/// <summary>
		/// * Constructs a face with unknown status.
		/// </summary>
		/// <param name="v1">a face vertex</param>
		/// <param name="v2">a face vertex</param>
		/// <param name="v3">a face vertex</param>
		public Face(Vertex v1, Vertex v2, Vertex v3)
		{
			this.v1 = v1;
			this.v2 = v2;
			this.v3 = v3;
			center = (v1.Position + v2.Position + v3.Position) / 3.0;

			status = Status.UNKNOWN;
		}

		/// <summary>
		/// Clones the face object
		/// </summary>
		/// <returns>cloned face object</returns>
		public Face Clone()
		{
			Face clone = new Face();
			clone.v1 = v1.Clone();
			clone.v2 = v2.Clone();
			clone.v3 = v3.Clone();
			clone.center = center;
			clone.status = status;
			return clone;
		}

		/// <summary>
		/// Computes closest distance from a vertex to a plane
		/// </summary>
		/// <param name="vertex">vertex used to compute the distance</param>
		/// <param name="face">face representing the plane where it is contained</param>
		/// <returns>the closest distance from the vertex to the plane</returns>
		public double ComputeDistance(Vertex vertex)
		{
			double distToV1 = this.Plane.DistanceToPlaneFromOrigin;
			double distToVertex = Vector3.Dot(Normal, vertex.Position);
			double distFromFacePlane = distToVertex - distToV1;
			return distFromFacePlane;
		}

		/**
     * Makes a string definition for the Face object
     *
     * @return the string definition
     */

		public bool Equals(Face face)
		{
			bool cond1 = v1.Equals(face.v1) && v2.Equals(face.v2) && v3.Equals(face.v3);
			bool cond2 = v1.Equals(face.v2) && v2.Equals(face.v3) && v3.Equals(face.v1);
			bool cond3 = v1.Equals(face.v3) && v2.Equals(face.v1) && v3.Equals(face.v2);

			return cond1 || cond2 || cond3;
		}

		public double GetIntersectCost()
		{
			return 350;
		}

		public Vector3 GetCenter()
		{
			return center;
		}

		public double GetArea()
		{
			//area = (a * c * sen(B))/2
			Vector3 p1 = v1.GetPosition();
			Vector3 p2 = v2.GetPosition();
			Vector3 p3 = v3.GetPosition();
			Vector3 xy = new Vector3(p2.X - p1.X, p2.Y - p1.Y, p2.Z - p1.Z);
			Vector3 xz = new Vector3(p3.X - p1.X, p3.Y - p1.Y, p3.Z - p1.Z);

			double a = (p1 - p2).Length;
			double c = (p1 - p3).Length;
			double B = Vector3.CalculateAngle(xy, xz);

			return (a * c * Math.Sin(B)) / 2d;
		}

		public AxisAlignedBoundingBox GetBound()
		{
			if (!cachedBounds)
			{
				boundCache = new AxisAlignedBoundingBox(new Vector3[] { v1.GetPosition(), v2.GetPosition(), v3.GetPosition() });
				cachedBounds = true;
			}

			return boundCache;
		}

		public Plane Plane
		{
			get
			{
				if (planeCache.PlaneNormal == Vector3.Zero)
				{
					Vector3 p1 = v1.GetPosition();
					Vector3 p2 = v2.GetPosition();
					Vector3 p3 = v3.GetPosition();
					planeCache = new Plane(p1, p2, p3);
				}

				return planeCache;
			}
		}

		public Vector3 Normal
		{
			get
			{
				return Plane.PlaneNormal;
			}
		}

		public Status GetStatus()
		{
			return status;
		}

		public void Invert()
		{
			Vertex vertexTemp = v2;
			v2 = v1;
			v1 = vertexTemp;
		}

		/// <summary>
		/// Classifies the face based on the ray trace technique
		/// </summary>
		/// <param name="obj">object3d used to compute the face status</param>
		public void RayTraceClassify(Object3D obj)
		{
			var random = new Random();

			//creating a ray starting at the face baricenter going to the normal direction
			Ray ray = new Ray(center, Normal);
			//Line ray = new Line(GetNormal(), center);
			ray.PerturbDirection(random);

			bool success;
			double distance;
			Vector3 intersectionPoint;
			Face closestFace = null;
			double closestDistance;

			do
			{
				success = true;
				closestDistance = Double.MaxValue;
				//for each face from the other solid...
				//foreach (Face face in obj.Faces.AllObjects())
				foreach (Face face in obj.Faces.AlongRay(ray))
				{
					double hitDistance;
					bool front;

					//if ray intersects the plane...
					if (face.Plane.RayHitPlane(ray, out hitDistance, out front))
					{
						double dotProduct = Vector3.Dot(face.Normal, ray.directionNormal);
						distance = hitDistance;
						intersectionPoint = ray.origin + ray.directionNormal * hitDistance;
						ray.maxDistanceToConsider = hitDistance;

						//if ray lies in plane...
						if (Math.Abs(distance) < EqualityTolerance && Math.Abs(dotProduct) < EqualityTolerance)
						{
							//disturb the ray in order to not lie into another plane
							ray.PerturbDirection(random);
							success = false;
							break;
						}

						//if ray starts in plane...
						if (Math.Abs(distance) < EqualityTolerance && Math.Abs(dotProduct) > EqualityTolerance)
						{
							//if ray intersects the face...
							if (face.ContainsPoint(intersectionPoint))
							{
								//faces coincide
								closestFace = face;
								closestDistance = 0;
								break;
							}
						}

						//if ray intersects plane...
						else if (Math.Abs(dotProduct) > EqualityTolerance && distance > EqualityTolerance)
						{
							if (distance < closestDistance)
							{
								//if ray intersects the face;
								if (face.ContainsPoint(intersectionPoint))
								{
									//this face is the closest face until now
									closestDistance = distance;
									closestFace = face;
								}
							}
						}
					}
				}
			} while (success == false);

			
			if (closestFace == null)
			{
				//none face found: outside face
				status = Status.OUTSIDE;
			}
			else //face found: test dot product
			{
				double dotProduct = Vector3.Dot(closestFace.Normal, ray.directionNormal);

				//distance = 0: coplanar faces
				if (Math.Abs(closestDistance) < EqualityTolerance)
				{
					if (dotProduct > EqualityTolerance)
					{
						status = Status.SAME;
					}
					else if (dotProduct < -EqualityTolerance)
					{
						status = Status.OPPOSITE;
					}
				}
				else if (dotProduct > EqualityTolerance)
				{
					//dot product > 0 (same direction): inside face
					status = Status.INSIDE;
				}
				else if (dotProduct < -EqualityTolerance)
				{
					//dot product < 0 (opposite direction): outside face
					status = Status.OUTSIDE;
				}
			}
		}

		public Vector3[] Positions()
		{
			return new Vector3[] { v1.Position, v2.Position, v3.Position };
		}

		/// <summary>
		/// Classifies the face if one of its vertices are classified as INSIDE or OUTSIDE
		/// </summary>
		/// <returns>true if the face could be classified, false otherwise</returns>
		public bool SimpleClassify()
		{
			Status status1 = v1.GetStatus();
			Status status2 = v2.GetStatus();
			Status status3 = v3.GetStatus();

			if (status1 == Status.INSIDE || status1 == Status.OUTSIDE)
			{
				this.status = status1;
				return true;
			}
			else if (status2 == Status.INSIDE || status2 == Status.OUTSIDE)
			{
				this.status = status2;
				return true;
			}
			else if (status3 == Status.INSIDE || status3 == Status.OUTSIDE)
			{
				this.status = status3;
				return true;
			}
			else
			{
				return false;
			}
		}

		public override string ToString()
		{
			return v1.toString() + "\n" + v2.toString() + "\n" + v3.toString();
		}

		/**
     * Checks if a face is equal to another. To be equal, they have to have equal
     * vertices in the same order
     *
     * @param anObject the other face to be tested
     * @return true if they are equal, false otherwise.
     */
		//-------------------------------------GETS-------------------------------------//

		/**
     * Gets the face bound
     *
     * @return face bound
     */
		/**
     * Gets the face normal
     *
     * @return face normal
     */
		/**
     * Gets the face status
     *
     * @return face status - UNKNOWN, INSIDE, OUTSIDE, SAME OR OPPOSITE
     */
		/**
     * Gets the face area
     *
     * @return face area
     */
		//-------------------------------------OTHERS-----------------------------------//

		/** Invert face direction (normal direction) */

		//------------------------------------PRIVATES----------------------------------//

		/// <summary>
		/// Gets the position of a point relative to a line in the x plane
		/// </summary>
		/// <param name="point">point to be tested</param>
		/// <param name="pointLine1">one of the line ends</param>
		/// <param name="pointLine2">one of the line ends</param>
		/// <returns>position of the point relative to the line - UP, DOWN, ON, NONE</returns>
		private static Side LinePositionInX(Vector3 point, Vector3 pointLine1, Vector3 pointLine2)
		{
			double a, b, z;
			if ((Math.Abs(pointLine1.Y - pointLine2.Y) > EqualityTolerance) && (((point.Y >= pointLine1.Y) && (point.Y <= pointLine2.Y)) || ((point.Y <= pointLine1.Y) && (point.Y >= pointLine2.Y))))
			{
				a = (pointLine2.Z - pointLine1.Z) / (pointLine2.Y - pointLine1.Y);
				b = pointLine1.Z - a * pointLine1.Y;
				z = a * point.Y + b;
				if (z > point.Z + EqualityTolerance)
				{
					return Side.UP;
				}
				else if (z < point.Z - EqualityTolerance)
				{
					return Side.DOWN;
				}
				else
				{
					return Side.ON;
				}
			}
			else
			{
				return Side.NONE;
			}
		}

		/// <summary>
		/// Gets the position of a point relative to a line in the y plane
		/// </summary>
		/// <param name="point">point to be tested</param>
		/// <param name="pointLine1">one of the line ends</param>
		/// <param name="pointLine2">one of the line ends</param>
		/// <returns>position of the point relative to the line - UP, DOWN, ON, NONE</returns>
		private static Side LinePositionInY(Vector3 point, Vector3 pointLine1, Vector3 pointLine2)
		{
			double a, b, z;
			if ((Math.Abs(pointLine1.X - pointLine2.X) > EqualityTolerance) && (((point.X >= pointLine1.X) && (point.X <= pointLine2.X)) || ((point.X <= pointLine1.X) && (point.X >= pointLine2.X))))
			{
				a = (pointLine2.Z - pointLine1.Z) / (pointLine2.X - pointLine1.X);
				b = pointLine1.Z - a * pointLine1.X;
				z = a * point.X + b;
				if (z > point.Z + EqualityTolerance)
				{
					return Side.UP;
				}
				else if (z < point.Z - EqualityTolerance)
				{
					return Side.DOWN;
				}
				else
				{
					return Side.ON;
				}
			}
			else
			{
				return Side.NONE;
			}
		}

		/// <summary>
		/// Gets the position of a point relative to a line in the z plane
		/// </summary>
		/// <param name="point">point to be tested</param>
		/// <param name="pointLine1">one of the line ends</param>
		/// <param name="pointLine2">one of the line ends</param>
		/// <returns>position of the point relative to the line - UP, DOWN, ON, NONE</returns>
		private static Side LinePositionInZ(Vector3 point, Vector3 pointLine1, Vector3 pointLine2)
		{
			double a, b, y;
			if ((Math.Abs(pointLine1.X - pointLine2.X) > EqualityTolerance) && (((point.X >= pointLine1.X) && (point.X <= pointLine2.X)) || ((point.X <= pointLine1.X) && (point.X >= pointLine2.X))))
			{
				a = (pointLine2.Y - pointLine1.Y) / (pointLine2.X - pointLine1.X);
				b = pointLine1.Y - a * pointLine1.X;
				y = a * point.X + b;
				if (y > point.Y + EqualityTolerance)
				{
					return Side.UP;
				}
				else if (y < point.Y - EqualityTolerance)
				{
					return Side.DOWN;
				}
				else
				{
					return Side.ON;
				}
			}
			else
			{
				return Side.NONE;
			}
		}

		/// <summary>
		/// Checks if the the face contains a point
		/// </summary>
		/// <param name="point">point to be tested</param>
		/// <returns>true if the face contains the point, false otherwise</returns>
		private bool ContainsPoint(Vector3 point)
		{
			Side result1;
			Side result2;
			Side result3;

			//if x is constant...
			if (Math.Abs(Normal.X) > EqualityTolerance)
			{
				//tests on the x plane
				result1 = LinePositionInX(point, v1.GetPosition(), v2.GetPosition());
				result2 = LinePositionInX(point, v2.GetPosition(), v3.GetPosition());
				result3 = LinePositionInX(point, v3.GetPosition(), v1.GetPosition());
			}

			//if y is constant...
			else if (Math.Abs(Normal.Y) > EqualityTolerance)
			{
				//tests on the y plane
				result1 = LinePositionInY(point, v1.GetPosition(), v2.GetPosition());
				result2 = LinePositionInY(point, v2.GetPosition(), v3.GetPosition());
				result3 = LinePositionInY(point, v3.GetPosition(), v1.GetPosition());
			}
			else
			{
				//tests on the z plane
				result1 = LinePositionInZ(point, v1.GetPosition(), v2.GetPosition());
				result2 = LinePositionInZ(point, v2.GetPosition(), v3.GetPosition());
				result3 = LinePositionInZ(point, v3.GetPosition(), v1.GetPosition());
			}

			//if the point is up and down two lines...
			if (((result1 == Side.UP) || (result2 == Side.UP) || (result3 == Side.UP)) && ((result1 == Side.DOWN) || (result2 == Side.DOWN) || (result3 == Side.DOWN)))
			{
				return true;
			}
			//if the point is on of the lines...
			else if ((result1 == Side.ON) || (result2 == Side.ON) || (result3 == Side.ON))
			{
				return true;
			}
			else
			{
				return false;
			}
		}
	}
}