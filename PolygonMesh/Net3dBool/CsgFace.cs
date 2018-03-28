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
	/// <summary>
	/// face status relative to a solid
	/// </summary>
	public enum FaceStatus
	{
		/// <summary>
		/// face status if it is still unknown
		/// </summary>
		Unknown,
		/// <summary>
		/// face status if it is inside a solid
		/// </summary>
		Inside,
		/// <summary>
		/// face status if it is outside a solid
		/// </summary>
		Outside,
		/// <summary>
		/// face status if it is coincident with a solid face
		/// </summary>
		Same,
		/// <summary>
		/// face status if it is coincident with a solid face with opposite orientation
		/// </summary>
		Opposite,
		Boundary
	};

	public enum LineSide
	{
		/// <summary>
		/// point status if it is up relative to an edge
		/// </summary>
		Up,
		/// <summary>
		/// point status if it is down relative to an edge
		/// </summary>
		Down,
		/// <summary>
		/// point status if it is on an edge
		/// </summary>
		On,
		/// <summary>
		/// point status if it isn't up, down or on relative to an edge
		/// </summary>
		None
	};

	/// <summary>
	/// Representation of a 3D face (triangle).
	/// </summary>
	public class CsgFace
	{
		public Vertex v1;
		public Vertex v2;
		public Vertex v3;

		private Vector3 center;

		/// <summary>
		/// tolerance value to test equalities
		/// </summary>
		private readonly static double EqualityTolerance = 1e-10f;
		private AxisAlignedBoundingBox boundCache;
		private bool cachedBounds = false;
		private Plane planeCache;
		public FaceStatus Status { get; private set; }

		/// <summary>
		/// Default constructor
		/// </summary>
		private CsgFace()
		{
		}

		/// <summary>
		/// Constructs a face with unknown status.
		/// </summary>
		/// <param name="v1">a face vertex</param>
		/// <param name="v2">a face vertex</param>
		/// <param name="v3">a face vertex</param>
		public CsgFace(Vertex v1, Vertex v2, Vertex v3)
		{
			this.v1 = v1;
			this.v2 = v2;
			this.v3 = v3;
			center = (v1.Position + v2.Position + v3.Position) / 3.0;

			Status = FaceStatus.Unknown;
		}

		/// <summary>
		/// Clones the face object
		/// </summary>
		/// <returns>cloned face object</returns>
		public CsgFace Clone()
		{
			CsgFace clone = new CsgFace();
			clone.v1 = v1.Clone();
			clone.v2 = v2.Clone();
			clone.v3 = v3.Clone();
			clone.center = center;
			clone.Status = Status;
			return clone;
		}

		/// <summary>
		/// Computes closest distance from a vertex to a plane
		/// </summary>
		/// <param name="vertex">vertex used to compute the distance</param>
		/// <param name="face">face representing the plane where it is contained</param>
		/// <returns>the closest distance from the vertex to the plane</returns>
		public double DistanceFromPlane(Vertex vertex)
		{
			double distToV1 = this.Plane.DistanceToPlaneFromOrigin;
			double distToVertex = Vector3.Dot(Normal, vertex.Position);
			double distFromFacePlane = distToVertex - distToV1;
			return distFromFacePlane;
		}

		public bool Equals(CsgFace face)
		{
			bool cond1 = v1.Equals(face.v1) && v2.Equals(face.v2) && v3.Equals(face.v3);
			bool cond2 = v1.Equals(face.v2) && v2.Equals(face.v3) && v3.Equals(face.v1);
			bool cond3 = v1.Equals(face.v3) && v2.Equals(face.v1) && v3.Equals(face.v2);

			return cond1 || cond2 || cond3;
		}

		public double GetArea()
		{
			//area = (a * c * sen(B))/2
			Vector3 p1 = v1.Position;
			Vector3 p2 = v2.Position;
			Vector3 p3 = v3.Position;
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
				boundCache = new AxisAlignedBoundingBox(new Vector3[] { v1.Position, v2.Position, v3.Position });
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
					Vector3 p1 = v1.Position;
					Vector3 p2 = v2.Position;
					Vector3 p3 = v3.Position;
					planeCache = new Plane(p1, p2, p3);
				}

				return planeCache;
			}
		}

		public Vector3 Normal => Plane.PlaneNormal;

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
		public void RayTraceClassify(CsgObject3D obj)
		{
			var random = new Random();

			//creating a ray starting at the face baricenter going to the normal direction
			Ray ray = new Ray(center, Normal);
			//Line ray = new Line(GetNormal(), center);
			ray.PerturbDirection(random);

			bool success;
			double distance;
			Vector3 intersectionPoint;
			CsgFace closestFace = null;
			double closestDistance;

			do
			{
				success = true;
				closestDistance = Double.MaxValue;
				//for each face from the other solid...
				//foreach (Face face in obj.Faces.AllObjects())
				foreach (CsgFace face in obj.Faces.AlongRay(ray))
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
				Status = FaceStatus.Outside;
			}
			else //face found: test dot product
			{
				double dotProduct = Vector3.Dot(closestFace.Normal, ray.directionNormal);

				//distance = 0: coplanar faces
				if (Math.Abs(closestDistance) < EqualityTolerance)
				{
					if (dotProduct > EqualityTolerance)
					{
						Status = FaceStatus.Same;
					}
					else if (dotProduct < -EqualityTolerance)
					{
						Status = FaceStatus.Opposite;
					}
				}
				else if (dotProduct > EqualityTolerance)
				{
					//dot product > 0 (same direction): inside face
					Status = FaceStatus.Inside;
				}
				else if (dotProduct < -EqualityTolerance)
				{
					//dot product < 0 (opposite direction): outside face
					Status = FaceStatus.Outside;
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
			FaceStatus status1 = v1.Status;
			FaceStatus status2 = v2.Status;
			FaceStatus status3 = v3.Status;

			if (status1 == FaceStatus.Inside || status1 == FaceStatus.Outside)
			{
				this.Status = status1;
				return true;
			}
			else if (status2 == FaceStatus.Inside || status2 == FaceStatus.Outside)
			{
				this.Status = status2;
				return true;
			}
			else if (status3 == FaceStatus.Inside || status3 == FaceStatus.Outside)
			{
				this.Status = status3;
				return true;
			}
			else
			{
				return false;
			}
		}

		public override string ToString()
		{
			return v1.ToString() + "\n" + v2.ToString() + "\n" + v3.ToString();
		}

		/// <summary>
		/// Gets the position of a point relative to a line in the x plane
		/// </summary>
		/// <param name="point">point to be tested</param>
		/// <param name="pointLine1">one of the line ends</param>
		/// <param name="pointLine2">one of the line ends</param>
		/// <returns>position of the point relative to the line - UP, DOWN, ON, NONE</returns>
		private static LineSide LineSideInX(Vector3 point, Vector3 pointLine1, Vector3 pointLine2)
		{
			double a, b, z;
			if ((Math.Abs(pointLine1.Y - pointLine2.Y) > EqualityTolerance) && (((point.Y >= pointLine1.Y) && (point.Y <= pointLine2.Y)) || ((point.Y <= pointLine1.Y) && (point.Y >= pointLine2.Y))))
			{
				a = (pointLine2.Z - pointLine1.Z) / (pointLine2.Y - pointLine1.Y);
				b = pointLine1.Z - a * pointLine1.Y;
				z = a * point.Y + b;
				if (z > point.Z + EqualityTolerance)
				{
					return LineSide.Up;
				}
				else if (z < point.Z - EqualityTolerance)
				{
					return LineSide.Down;
				}
				else
				{
					return LineSide.On;
				}
			}
			else
			{
				return LineSide.None;
			}
		}

		/// <summary>
		/// Gets the position of a point relative to a line in the y plane
		/// </summary>
		/// <param name="point">point to be tested</param>
		/// <param name="pointLine1">one of the line ends</param>
		/// <param name="pointLine2">one of the line ends</param>
		/// <returns>position of the point relative to the line - UP, DOWN, ON, NONE</returns>
		private static LineSide LineSideInY(Vector3 point, Vector3 pointLine1, Vector3 pointLine2)
		{
			double a, b, z;
			if ((Math.Abs(pointLine1.X - pointLine2.X) > EqualityTolerance) && (((point.X >= pointLine1.X) && (point.X <= pointLine2.X)) || ((point.X <= pointLine1.X) && (point.X >= pointLine2.X))))
			{
				a = (pointLine2.Z - pointLine1.Z) / (pointLine2.X - pointLine1.X);
				b = pointLine1.Z - a * pointLine1.X;
				z = a * point.X + b;
				if (z > point.Z + EqualityTolerance)
				{
					return LineSide.Up;
				}
				else if (z < point.Z - EqualityTolerance)
				{
					return LineSide.Down;
				}
				else
				{
					return LineSide.On;
				}
			}
			else
			{
				return LineSide.None;
			}
		}

		/// <summary>
		/// Gets the position of a point relative to a line in the z plane
		/// </summary>
		/// <param name="point">point to be tested</param>
		/// <param name="pointLine1">one of the line ends</param>
		/// <param name="pointLine2">one of the line ends</param>
		/// <returns>position of the point relative to the line - UP, DOWN, ON, NONE</returns>
		private static LineSide LineSideInZ(Vector3 point, Vector3 pointLine1, Vector3 pointLine2)
		{
			double a, b, y;
			if ((Math.Abs(pointLine1.X - pointLine2.X) > EqualityTolerance) && (((point.X >= pointLine1.X) && (point.X <= pointLine2.X)) || ((point.X <= pointLine1.X) && (point.X >= pointLine2.X))))
			{
				a = (pointLine2.Y - pointLine1.Y) / (pointLine2.X - pointLine1.X);
				b = pointLine1.Y - a * pointLine1.X;
				y = a * point.X + b;
				if (y > point.Y + EqualityTolerance)
				{
					return LineSide.Up;
				}
				else if (y < point.Y - EqualityTolerance)
				{
					return LineSide.Down;
				}
				else
				{
					return LineSide.On;
				}
			}
			else
			{
				return LineSide.None;
			}
		}

		/// <summary>
		/// Checks if the the face contains a point
		/// </summary>
		/// <param name="point">point to be tested</param>
		/// <returns>true if the face contains the point, false otherwise</returns>
		private bool ContainsPoint(Vector3 point)
		{
			LineSide result1;
			LineSide result2;
			LineSide result3;

			//if x is constant...
			if (Math.Abs(Normal.X) > EqualityTolerance)
			{
				//tests on the x plane
				result1 = LineSideInX(point, v1.Position, v2.Position);
				result2 = LineSideInX(point, v2.Position, v3.Position);
				result3 = LineSideInX(point, v3.Position, v1.Position);
			}

			//if y is constant...
			else if (Math.Abs(Normal.Y) > EqualityTolerance)
			{
				//tests on the y plane
				result1 = LineSideInY(point, v1.Position, v2.Position);
				result2 = LineSideInY(point, v2.Position, v3.Position);
				result3 = LineSideInY(point, v3.Position, v1.Position);
			}
			else
			{
				//tests on the z plane
				result1 = LineSideInZ(point, v1.Position, v2.Position);
				result2 = LineSideInZ(point, v2.Position, v3.Position);
				result3 = LineSideInZ(point, v3.Position, v1.Position);
			}

			//if the point is up and down two lines...
			if (((result1 == LineSide.Up) || (result2 == LineSide.Up) || (result3 == LineSide.Up)) && ((result1 == LineSide.Down) || (result2 == LineSide.Down) || (result3 == LineSide.Down)))
			{
				return true;
			}
			//if the point is on of the lines...
			else if ((result1 == LineSide.On) || (result2 == LineSide.On) || (result3 == LineSide.On))
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