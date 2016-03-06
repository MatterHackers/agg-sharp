using MatterHackers.VectorMath;

/**
* Representation of a 3D face (triangle).
*
* <br><br>See:
* D. H. Laidlaw, W. B. Trumbore, and J. F. Hughes.
* "Constructive Solid Geometry for Polyhedral Objects"
* SIGGRAPH Proceedings, 1986, p.161.
*
* original author: Danilo Balby Silva Castanheira (danbalby@yahoo.com)
*
* Ported from Java to C# by Sebastian Loncar, Web: http://loncar.de
* Project: https://github.com/Arakis/Net3dBool
*/

using System;

namespace Net3dBool
{
	public class Face
	{
		/** first vertex */
		public static int INSIDE = 2;
		public static int OPPOSITE = 5;
		public static int OUTSIDE = 3;
		public static int SAME = 4;
		public static int UNKNOWN = 1;
		public Vertex v1;
		/** second vertex */
		public Vertex v2;
		/** third vertex */
		public Vertex v3;

		/** face status relative to a solid  */
		private static int DOWN = 7;
		private static int NONE = 9;
		private static int ON = 8;
		private static double EqualityTolerance = 1e-10f;
		private static int UP = 6;
		private Bound boundCache;
		private bool cachedBounds = false;
		private bool cachedNormal = false;
		private Vector3 normalCache;
		private int status;

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

		/**
     * Constructs a face with unknown status.
     *
     * @param v1 a face vertex
     * @param v2 a face vertex
     * @param v3 a face vertex
     */

		public Face(Vertex v1, Vertex v2, Vertex v3)
		{
			this.v1 = v1;
			this.v2 = v2;
			this.v3 = v3;

			status = UNKNOWN;
		}

		private Face()
		{
		}

		//-----------------------------------OVERRIDES----------------------------------//

		/**
     * Clones the face object
     *
     * @return cloned face object
     */

		public Face Clone()
		{
			Face clone = new Face();
			clone.v1 = v1.Clone();
			clone.v2 = v2.Clone();
			clone.v3 = v3.Clone();
			clone.status = status;
			return clone;
		}

		/**
     * Makes a string definition for the Face object
     *
     * @return the string definition
     */

		public bool Equals(Face face)
		{
			bool cond1 = v1.equals(face.v1) && v2.equals(face.v2) && v3.equals(face.v3);
			bool cond2 = v1.equals(face.v2) && v2.equals(face.v3) && v3.equals(face.v1);
			bool cond3 = v1.equals(face.v3) && v2.equals(face.v1) && v3.equals(face.v2);

			return cond1 || cond2 || cond3;
		}

		public double GetArea()
		{
			//area = (a * c * sen(B))/2
			Vector3 p1 = v1.GetPosition();
			Vector3 p2 = v2.GetPosition();
			Vector3 p3 = v3.GetPosition();
			Vector3 xy = new Vector3(p2.x - p1.x, p2.y - p1.y, p2.z - p1.z);
			Vector3 xz = new Vector3(p3.x - p1.x, p3.y - p1.y, p3.z - p1.z);

			double a = (p1 - p2).Length;
			double c = (p1 - p3).Length;
			double B = Vector3.CalculateAngle(xy, xz);

			return (a * c * Math.Sin(B)) / 2d;
		}

		public Bound GetBound()
		{
			if (!cachedBounds)
			{
				boundCache = new Bound(v1.GetPosition(), v2.GetPosition(), v3.GetPosition());
				cachedBounds = true;
			}

			return boundCache;
		}

		public Vector3 GetNormal()
		{
			if (!cachedNormal)
			{
				Vector3 p1 = v1.GetPosition();
				Vector3 p2 = v2.GetPosition();
				Vector3 p3 = v3.GetPosition();
				Vector3 xy, xz, normal;

				xy = new Vector3(p2.x - p1.x, p2.y - p1.y, p2.z - p1.z);
				xz = new Vector3(p3.x - p1.x, p3.y - p1.y, p3.z - p1.z);

				normal = Vector3.Cross(xy, xz);
				normal.Normalize();

				normalCache = normal;
				cachedNormal = true;
			}

			return normalCache;
		}

		public int GetStatus()
		{
			return status;
		}

		public void Invert()
		{
			Vertex vertexTemp = v2;
			v2 = v1;
			v1 = vertexTemp;
		}

		public void RayTraceClassify(Object3D obj)
		{
			//creating a ray starting at the face baricenter going to the normal direction
			Vector3 p0 = new Vector3();
			p0.x = (v1.x + v2.x + v3.x) / 3d;
			p0.y = (v1.y + v2.y + v3.y) / 3d;
			p0.z = (v1.z + v2.z + v3.z) / 3d;
			Line ray = new Line(GetNormal(), p0);

			bool success;
			double dotProduct, distance;
			Vector3 intersectionPoint;
			Face closestFace = null;
			double closestDistance;

			do
			{
				success = true;
				closestDistance = Double.MaxValue;
				//for each face from the other solid...
				for (int faceIndex = 0; faceIndex < obj.GetNumFaces(); faceIndex++)
				{
					Face face = obj.GetFace(faceIndex);
					dotProduct = Vector3.Dot(face.GetNormal(), ray.Direction);
					intersectionPoint = ray.ComputePlaneIntersection(face.GetNormal(), face.v1.GetPosition());

					//if ray intersects the plane...
					if (intersectionPoint.x != double.PositiveInfinity)
					{
						distance = ray.ComputePointToPointDistance(intersectionPoint);

						//if ray lies in plane...
						if (Math.Abs(distance) < EqualityTolerance && Math.Abs(dotProduct) < EqualityTolerance)
						{
							//disturb the ray in order to not lie into another plane
							ray.PerturbDirection();
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
									//this face is the closest face untill now
									closestDistance = distance;
									closestFace = face;
								}
							}
						}
					}
				}
			} while (success == false);

			//none face found: outside face
			if (closestFace == null)
			{
				status = OUTSIDE;
			}
			//face found: test dot product
			else
			{
				dotProduct = Vector3.Dot(closestFace.GetNormal(), ray.Direction);

				//distance = 0: coplanar faces
				if (Math.Abs(closestDistance) < EqualityTolerance)
				{
					if (dotProduct > EqualityTolerance)
					{
						status = SAME;
					}
					else if (dotProduct < -EqualityTolerance)
					{
						status = OPPOSITE;
					}
				}

				//dot product > 0 (same direction): inside face
				else if (dotProduct > EqualityTolerance)
				{
					status = INSIDE;
				}

				//dot product < 0 (opposite direction): outside face
				else if (dotProduct < -EqualityTolerance)
				{
					status = OUTSIDE;
				}
			}
		}

		public bool SimpleClassify()
		{
			int status1 = v1.getStatus();
			int status2 = v2.getStatus();
			int status3 = v3.getStatus();

			if (status1 == Vertex.INSIDE || status1 == Vertex.OUTSIDE)
			{
				this.status = status1;
				return true;
			}
			else if (status2 == Vertex.INSIDE || status2 == Vertex.OUTSIDE)
			{
				this.status = status2;
				return true;
			}
			else if (status3 == Vertex.INSIDE || status3 == Vertex.OUTSIDE)
			{
				this.status = status3;
				return true;
			}
			else
			{
				return false;
			}
		}

		public String toString()
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
		//------------------------------------CLASSIFIERS-------------------------------//

		/**
     * Classifies the face if one of its vertices are classified as INSIDE or OUTSIDE
     *
     * @return true if the face could be classified, false otherwise
     */
		/**
     * Classifies the face based on the ray trace technique
     *
     * @param object object3d used to compute the face status
     */
		//------------------------------------PRIVATES----------------------------------//

		/**
     * Checks if the the face contains a point
     *
     * @param point to be tested
     * @param true if the face contains the point, false otherwise
     */

		private static int LinePositionInX(Vector3 point, Vector3 pointLine1, Vector3 pointLine2)
		{
			double a, b, z;
			if ((Math.Abs(pointLine1.y - pointLine2.y) > EqualityTolerance) && (((point.y >= pointLine1.y) && (point.y <= pointLine2.y)) || ((point.y <= pointLine1.y) && (point.y >= pointLine2.y))))
			{
				a = (pointLine2.z - pointLine1.z) / (pointLine2.y - pointLine1.y);
				b = pointLine1.z - a * pointLine1.y;
				z = a * point.y + b;
				if (z > point.z + EqualityTolerance)
				{
					return UP;
				}
				else if (z < point.z - EqualityTolerance)
				{
					return DOWN;
				}
				else
				{
					return ON;
				}
			}
			else
			{
				return NONE;
			}
		}

		private static int LinePositionInY(Vector3 point, Vector3 pointLine1, Vector3 pointLine2)
		{
			double a, b, z;
			if ((Math.Abs(pointLine1.x - pointLine2.x) > EqualityTolerance) && (((point.x >= pointLine1.x) && (point.x <= pointLine2.x)) || ((point.x <= pointLine1.x) && (point.x >= pointLine2.x))))
			{
				a = (pointLine2.z - pointLine1.z) / (pointLine2.x - pointLine1.x);
				b = pointLine1.z - a * pointLine1.x;
				z = a * point.x + b;
				if (z > point.z + EqualityTolerance)
				{
					return UP;
				}
				else if (z < point.z - EqualityTolerance)
				{
					return DOWN;
				}
				else
				{
					return ON;
				}
			}
			else
			{
				return NONE;
			}
		}

		private static int LinePositionInZ(Vector3 point, Vector3 pointLine1, Vector3 pointLine2)
		{
			double a, b, y;
			if ((Math.Abs(pointLine1.x - pointLine2.x) > EqualityTolerance) && (((point.x >= pointLine1.x) && (point.x <= pointLine2.x)) || ((point.x <= pointLine1.x) && (point.x >= pointLine2.x))))
			{
				a = (pointLine2.y - pointLine1.y) / (pointLine2.x - pointLine1.x);
				b = pointLine1.y - a * pointLine1.x;
				y = a * point.x + b;
				if (y > point.y + EqualityTolerance)
				{
					return UP;
				}
				else if (y < point.y - EqualityTolerance)
				{
					return DOWN;
				}
				else
				{
					return ON;
				}
			}
			else
			{
				return NONE;
			}
		}

		private bool ContainsPoint(Vector3 point)
		{
			int result1, result2, result3;
			Vector3 normal = GetNormal();

			//if x is constant...
			if (Math.Abs(normal.x) > EqualityTolerance)
			{
				//tests on the x plane
				result1 = LinePositionInX(point, v1.GetPosition(), v2.GetPosition());
				result2 = LinePositionInX(point, v2.GetPosition(), v3.GetPosition());
				result3 = LinePositionInX(point, v3.GetPosition(), v1.GetPosition());
			}

			//if y is constant...
			else if (Math.Abs(normal.y) > EqualityTolerance)
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
			if (((result1 == UP) || (result2 == UP) || (result3 == UP)) && ((result1 == DOWN) || (result2 == DOWN) || (result3 == DOWN)))
			{
				return true;
			}
			//if the point is on of the lines...
			else if ((result1 == ON) || (result2 == ON) || (result3 == ON))
			{
				return true;
			}
			else
			{
				return false;
			}
		}

		/**
     * Gets the position of a point relative to a line in the x plane
     *
     * @param point point to be tested
     * @param pointLine1 one of the line ends
     * @param pointLine2 one of the line ends
     * @return position of the point relative to the line - UP, DOWN, ON, NONE
     */
		/**
     * Gets the position of a point relative to a line in the y plane
     *
     * @param point point to be tested
     * @param pointLine1 one of the line ends
     * @param pointLine2 one of the line ends
     * @return position of the point relative to the line - UP, DOWN, ON, NONE
     */
		/**
     * Gets the position of a point relative to a line in the z plane
     *
     * @param point point to be tested
     * @param pointLine1 one of the line ends
     * @param pointLine2 one of the line ends
     * @return position of the point relative to the line - UP, DOWN, ON, NONE
     */
	}
}