using MatterHackers.VectorMath;

/**
* Representation of a 3d line or a ray (represented by a direction and a point).
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
	public class Line
	{
		/** a line point */
		private static Random rnd = new Random();
		private readonly static double EqualityTolerance = 1e-10f;
		private Vector3 startPoint;
		/** line direction */

		public Line(Face face1, Face face2)
		{
			Vector3 normalFace1 = face1.GetNormal();
			Vector3 normalFace2 = face2.GetNormal();

			//direction: cross product of the faces normals
			Direction = Vector3.Cross(normalFace1, normalFace2);

			//if direction lenght is not zero (the planes aren't parallel )...
			if (!(Direction.Length < EqualityTolerance))
			{
				//getting a line point, zero is set to a coordinate whose direction
				//component isn't zero (line intersecting its origin plan)
				startPoint = new Vector3();
				double d1 = -(normalFace1.x * face1.v1.x + normalFace1.y * face1.v1.y + normalFace1.z * face1.v1.z);
				double d2 = -(normalFace2.x * face2.v1.x + normalFace2.y * face2.v1.y + normalFace2.z * face2.v1.z);
				if (Math.Abs(Direction.x) > EqualityTolerance)
				{
					startPoint.x = 0;
					startPoint.y = (d2 * normalFace1.z - d1 * normalFace2.z) / Direction.x;
					startPoint.z = (d1 * normalFace2.y - d2 * normalFace1.y) / Direction.x;
				}
				else if (Math.Abs(Direction.y) > EqualityTolerance)
				{
					startPoint.x = (d1 * normalFace2.z - d2 * normalFace1.z) / Direction.y;
					startPoint.y = 0;
					startPoint.z = (d2 * normalFace1.x - d1 * normalFace2.x) / Direction.y;
				}
				else
				{
					startPoint.x = (d2 * normalFace1.y - d1 * normalFace2.y) / Direction.z;
					startPoint.y = (d1 * normalFace2.x - d2 * normalFace1.x) / Direction.z;
					startPoint.z = 0;
				}
			}

			Direction.Normalize();
		}

		public Line(Vector3 direction, Vector3 point)
		{
			this.Direction = direction;
			this.startPoint = point;
			direction.Normalize();
		}

		private Line()
		{
		}

		public Vector3 Direction { get; private set; }

		/** tolerance value to test equalities */
		//----------------------------------CONSTRUCTORS---------------------------------//

		/**
     * Constructor for a line. The line created is the intersection between two planes
     *
     * @param face1 face representing one of the planes
     * @param face2 face representing one of the planes
     */
		/**
     * Constructor for a ray
     *
     * @param direction direction ray
     * @param point beginning of the ray
     */
		//---------------------------------OVERRIDES------------------------------------//

		/**
     * Clones the Line object
     *
     * @return cloned Line object
     */

		public Line Clone()
		{
			Line clone = new Line();
			clone.Direction = Direction;
			clone.startPoint = startPoint;
			return clone;
		}

		/**
     * Makes a string definition for the Line object
     *
     * @return the string definition
     */

		public Vector3 ComputeLineIntersection(Line otherLine)
		{
			//x = x1 + a1*t = x2 + b1*s
			//y = y1 + a2*t = y2 + b2*s
			//z = z1 + a3*t = z2 + b3*s

			Vector3 linePoint = otherLine.GetPoint();
			Vector3 lineDirection = otherLine.Direction;

			double t;
			if (Math.Abs(Direction.y * lineDirection.x - Direction.x * lineDirection.y) > EqualityTolerance)
			{
				t = (-startPoint.y * lineDirection.x + linePoint.y * lineDirection.x + lineDirection.y * startPoint.x - lineDirection.y * linePoint.x) / (Direction.y * lineDirection.x - Direction.x * lineDirection.y);
			}
			else if (Math.Abs(-Direction.x * lineDirection.z + Direction.z * lineDirection.x) > EqualityTolerance)
			{
				t = -(-lineDirection.z * startPoint.x + lineDirection.z * linePoint.x + lineDirection.x * startPoint.z - lineDirection.x * linePoint.z) / (-Direction.x * lineDirection.z + Direction.z * lineDirection.x);
			}
			else if (Math.Abs(-Direction.z * lineDirection.y + Direction.y * lineDirection.z) > EqualityTolerance)
			{
				t = (startPoint.z * lineDirection.y - linePoint.z * lineDirection.y - lineDirection.z * startPoint.y + lineDirection.z * linePoint.y) / (-Direction.z * lineDirection.y + Direction.y * lineDirection.z);
			}
			else
			{
				#if DEBUG
				throw new InvalidOperationException();
				#else
				return Vector3.Zero;
				#endif
			}

			double x = startPoint.x + Direction.x * t;
			double y = startPoint.y + Direction.y * t;
			double z = startPoint.z + Direction.z * t;

			return new Vector3(x, y, z);
		}

		public Vector3 ComputePlaneIntersection(Vector3 normal, Vector3 planePoint)
		{
			//Ax + By + Cz + D = 0
			//x = x0 + t(x1 � x0)
			//y = y0 + t(y1 � y0)
			//z = z0 + t(z1 � z0)
			//(x1 - x0) = dx, (y1 - y0) = dy, (z1 - z0) = dz
			//t = -(A*x0 + B*y0 + C*z0 )/(A*dx + B*dy + C*dz)

			double A = normal.x;
			double B = normal.y;
			double C = normal.z;
			double D = -(normal.x * planePoint.x + normal.y * planePoint.y + normal.z * planePoint.z);

			double numerator = A * startPoint.x + B * startPoint.y + C * startPoint.z + D;
			double denominator = A * Direction.x + B * Direction.y + C * Direction.z;

			//if line is paralel to the plane...
			if (Math.Abs(denominator) < EqualityTolerance)
			{
				//if line is contained in the plane...
				if (Math.Abs(numerator) < EqualityTolerance)
				{
					return startPoint;
				}
				else
				{
					return Vector3.PositiveInfinity;
				}
			}
			//if line intercepts the plane...
			else
			{
				double t = -numerator / denominator;
				Vector3 resultPoint = new Vector3();
				resultPoint.x = startPoint.x + t * Direction.x;
				resultPoint.y = startPoint.y + t * Direction.y;
				resultPoint.z = startPoint.z + t * Direction.z;

				return resultPoint;
			}
		}

		public double ComputePointToPointDistance(Vector3 otherPoint)
		{
			double distance = (otherPoint - startPoint).Length;
			Vector3 vec = new Vector3(otherPoint.x - startPoint.x, otherPoint.y - startPoint.y, otherPoint.z - startPoint.z);
			vec.Normalize();
			if (Vector3.Dot(vec, Direction) < 0)
			{
				return -distance;
			}
			else
			{
				return distance;
			}
		}

		public Vector3 GetPoint()
		{
			return startPoint;
		}

		public void PerturbDirection()
		{
			Vector3 perturbedDirection = Direction;
			perturbedDirection.x += 1e-5 * Random();
			perturbedDirection.y += 1e-5 * Random();
			perturbedDirection.z += 1e-5 * Random();

			Direction = perturbedDirection;
		}

		public void SetDirection(Vector3 direction)
		{
			this.Direction = direction;
		}

		public void SetPoint(Vector3 point)
		{
			this.startPoint = point;
		}

		public String toString()
		{
			return "Direction: " + Direction.ToString() + "\nPoint: " + startPoint.ToString();
		}

		//-----------------------------------GETS---------------------------------------//

		/**
     * Gets the point used to represent the line
     *
     * @return point used to represent the line
     */
		/**
     * Gets the line direction
     *
     * @return line direction
     */

		//-----------------------------------SETS---------------------------------------//

		/**
     * Sets a new point
     *
     * @param point new point
     */
		/**
     * Sets a new direction
     *
     * @param direction new direction
     */
		//--------------------------------OTHERS----------------------------------------//

		/**
     * Computes the distance from the line point to another point
     *
     * @param otherPoint the point to compute the distance from the line point. The point
     * is supposed to be on the same line.
     * @return points distance. If the point submitted is behind the direction, the
     * distance is negative
     */
		/**
     * Computes the point resulting from the intersection with another line
     *
     * @param otherLine the other line to apply the intersection. The lines are supposed
     * to intersect
     * @return point resulting from the intersection. If the point coundn't be obtained, return null
     */
		/**
     * Compute the point resulting from the intersection with a plane
     *
     * @param normal the plane normal
     * @param planePoint a plane point.
     * @return intersection point. If they don't intersect, return null
     */
		/** Changes slightly the line direction */

		private static double Random()
		{
			return rnd.NextDouble();
		}
	}
}