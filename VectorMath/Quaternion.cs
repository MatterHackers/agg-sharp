#region --- License ---

/*
Copyright (c) 2006 - 2008 The Open Toolkit library.

Permission is hereby granted, free of charge, to any person obtaining a copy of
this software and associated documentation files (the "Software"), to deal in
the Software without restriction, including without limitation the rights to
use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies
of the Software, and to permit persons to whom the Software is furnished to do
so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

#endregion --- License ---

using System;
using System.Runtime.InteropServices;

namespace MatterHackers.VectorMath
{
	/// <summary>
	/// Represents a double-precision Quaternion.
	/// </summary>
	[Serializable]
	[StructLayout(LayoutKind.Sequential)]
	public struct Quaternion : IEquatable<Quaternion>
	{
		#region Fields

		private Vector3 xyz;
		private double w;

		#endregion Fields

		#region Constructors

		/// <summary>
		/// Construct a new Quaternion from vector and w components
		/// </summary>
		/// <param name="v">The vector part</param>
		/// <param name="w">The w part</param>
		public Quaternion(Vector3 v, double w)
		{
			this.xyz = v;
			this.w = w;
		}

		/// <summary>
		/// Construct a new Quaternion
		/// </summary>
		/// <param name="x">The x component</param>
		/// <param name="y">The y component</param>
		/// <param name="z">The z component</param>
		/// <param name="w">The w component</param>
		public Quaternion(double x, double y, double z, double w)
			: this(new Vector3(x, y, z), w)
		{ }

		/// <summary>
		/// Construct a new Quaternion
		/// </summary>
		/// <param name="matrix">The matrix to discover the rotation of</param>
		public Quaternion(Matrix4X4 m)
			: this()
		{
			this = m.GetRotation();
		}

		private double CopySign(double value, double sign)
		{
			if (sign < 0)
			{
				// return a negative number
				return value < 0 ? value : -value;
			}
			else
			{
				// return a positive number
				return value < 0 ? -value : value;
			}
		}

		/// <summary>
		/// Construct a quaternion that rotates from one direction to another
		/// </summary>
		/// <param name="startingDirection"></param>
		/// <param name="endingDirection"></param>
		public Quaternion(Vector3 startingDirection, Vector3 endingDirection)
		{
			if ((endingDirection + startingDirection).LengthSquared == 0)
			{
				endingDirection += new Vector3(.0000001, 0, 0);
			}
			this.xyz = Vector3.Cross(endingDirection, startingDirection);
			this.w = Math.Sqrt(Math.Pow(endingDirection.Length, 2) * Math.Pow(startingDirection.Length, 2)) + Vector3.Dot(endingDirection, startingDirection);
			Normalize();
		}

		#endregion Constructors

		#region Public Members

		#region Properties

		/// <summary>
		/// Gets or sets an OpenTK.Vector3d with the X, Y and Z components of this instance.
		/// </summary>
		public Vector3 Xyz { get { return xyz; } set { xyz = value; } }

		/// <summary>
		/// Gets or sets the X component of this instance.
		/// </summary>
		public double X { get { return xyz.X; } set { xyz.X = value; } }

		/// <summary>
		/// Gets or sets the Y component of this instance.
		/// </summary>
		public double Y { get { return xyz.Y; } set { xyz.Y = value; } }

		/// <summary>
		/// Gets or sets the Z component of this instance.
		/// </summary>
		public double Z { get { return xyz.Z; } set { xyz.Z = value; } }

		/// <summary>
		/// Gets or sets the W component of this instance.
		/// </summary>
		public double W { get { return w; } set { w = value; } }

		#endregion Properties

		#region Instance

		#region ToAxisAngle

		/// <summary>
		/// Convert the current quaternion to axis angle representation
		/// </summary>
		/// <param name="axis">The resultant axis</param>
		/// <param name="angle">The resultant angle</param>
		public void ToAxisAngle(out Vector3 axis, out double angle)
		{
			Vector4 result = ToAxisAngle();
			axis = result.Xyz;
			angle = result.W;
		}

		/// <summary>
		/// Convert this instance to an axis-angle representation.
		/// </summary>
		/// <returns>A Vector4 that is the axis-angle representation of this quaternion.</returns>
		public Vector4 ToAxisAngle()
		{
#if true
			Vector4 axisAngle = new Vector4();
			Quaternion q1 = this;
			if (q1.w > 1) q1.Normalize(); // if w>1 acos and sqrt will produce errors, this cant happen if quaternion is normalized
			axisAngle.W = 2 * Math.Acos(q1.w);
			double s = Math.Sqrt(1 - q1.w * q1.w); // assuming quaternion normalized then w is less than 1, so term always positive.
			if (s < 0.001)
			{ // test to avoid divide by zero, s is always positive due to sqrt
			  // if s close to zero then direction of axis not important
				axisAngle.X = q1.X; // if it is important that axis is normalized then replace with x=1; y=z=0;
				axisAngle.Y = q1.Y;
				axisAngle.Z = q1.Z;
			}
			else
			{
				axisAngle.X = q1.X / s; // normalize axis
				axisAngle.Y = q1.Y / s;
				axisAngle.Z = q1.Z / s;
			}

			return axisAngle;
#else
			Quaternion q = this;
			if (q.W > 1.0)
				q.Normalize();

			Vector4 result = new Vector4();

			result.W = 2.0 * (float)System.Math.Acos(q.W); // angle
			float den = (float)System.Math.Sqrt(1.0 - q.W * q.W);
			if (den > 0.0001f)
			{
				result.Xyz = q.Xyz / den;
			}
			else
			{
				// This occurs when the angle is zero.
				// Not a problem: just set an arbitrary normalized axis.
				result.Xyz = Vector3.UnitX;
			}

			return result;
#endif
		}

		#endregion ToAxisAngle

		#region public double Length

		/// <summary>
		/// Gets the length (magnitude) of the Quaternion.
		/// </summary>
		/// <seealso cref="LengthSquared"/>
		public double Length
		{
			get
			{
				return (double)System.Math.Sqrt(W * W + Xyz.LengthSquared);
			}
		}

		#endregion public double Length

		#region public double LengthSquared

		/// <summary>
		/// Gets the square of the Quaternion length (magnitude).
		/// </summary>
		public double LengthSquared
		{
			get
			{
				return W * W + Xyz.LengthSquared;
			}
		}

		#endregion public double LengthSquared

		#region public void Normalize()

		/// <summary>
		/// Scales the Quaternion to unit length.
		/// </summary>
		public void Normalize()
		{
			double length = this.Length;
			if (length != 0)
			{
				double scale = 1.0 / length;
				Xyz *= scale;
				W *= scale;
			}
		}

		#endregion public void Normalize()

		#region public void Conjugate()

		/// <summary>
		/// Convert this Quaternion to its conjugate
		/// </summary>
		public void Conjugate()
		{
			Xyz = -Xyz;
		}

		#endregion public void Conjugate()

		#endregion Instance

		#region Static

		#region Fields

		/// <summary>
		/// Defines the identity quaternion.
		/// </summary>
		public readonly static Quaternion Identity = new Quaternion(0, 0, 0, 1);

		#endregion Fields

		#region Add

		/// <summary>
		/// Add two quaternions
		/// </summary>
		/// <param name="left">The first operand</param>
		/// <param name="right">The second operand</param>
		/// <returns>The result of the addition</returns>
		public static Quaternion Add(Quaternion left, Quaternion right)
		{
			return new Quaternion(
				left.Xyz + right.Xyz,
				left.W + right.W);
		}

		/// <summary>
		/// Add two quaternions
		/// </summary>
		/// <param name="left">The first operand</param>
		/// <param name="right">The second operand</param>
		/// <param name="result">The result of the addition</param>
		public static void Add(ref Quaternion left, ref Quaternion right, out Quaternion result)
		{
			result = new Quaternion(
				left.Xyz + right.Xyz,
				left.W + right.W);
		}

		#endregion Add

		#region Sub

		/// <summary>
		/// Subtracts two instances.
		/// </summary>
		/// <param name="left">The left instance.</param>
		/// <param name="right">The right instance.</param>
		/// <returns>The result of the operation.</returns>
		public static Quaternion Sub(Quaternion left, Quaternion right)
		{
			return new Quaternion(
				left.Xyz - right.Xyz,
				left.W - right.W);
		}

		/// <summary>
		/// Subtracts two instances.
		/// </summary>
		/// <param name="left">The left instance.</param>
		/// <param name="right">The right instance.</param>
		/// <param name="result">The result of the operation.</param>
		public static void Sub(ref Quaternion left, ref Quaternion right, out Quaternion result)
		{
			result = new Quaternion(
				left.Xyz - right.Xyz,
				left.W - right.W);
		}

		#endregion Sub

		#region Mult

		/// <summary>
		/// Multiplies two instances.
		/// </summary>
		/// <param name="left">The first instance.</param>
		/// <param name="right">The second instance.</param>
		/// <returns>A new instance containing the result of the calculation.</returns>
		[Obsolete("Use Multiply instead.")]
		public static Quaternion Mult(Quaternion left, Quaternion right)
		{
			return new Quaternion(
				right.W * left.Xyz + left.W * right.Xyz + Vector3.Cross(left.Xyz, right.Xyz),
				left.W * right.W - Vector3.Dot(left.Xyz, right.Xyz));
		}

		/// <summary>
		/// Multiplies two instances.
		/// </summary>
		/// <param name="left">The first instance.</param>
		/// <param name="right">The second instance.</param>
		/// <param name="result">A new instance containing the result of the calculation.</param>
		[Obsolete("Use Multiply instead.")]
		public static void Mult(ref Quaternion left, ref Quaternion right, out Quaternion result)
		{
			result = new Quaternion(
				right.W * left.Xyz + left.W * right.Xyz + Vector3.Cross(left.Xyz, right.Xyz),
				left.W * right.W - Vector3.Dot(left.Xyz, right.Xyz));
		}

		/// <summary>
		/// Multiplies two instances.
		/// </summary>
		/// <param name="left">The first instance.</param>
		/// <param name="right">The second instance.</param>
		/// <returns>A new instance containing the result of the calculation.</returns>
		public static Quaternion Multiply(Quaternion left, Quaternion right)
		{
			Quaternion result;
			Multiply(ref left, ref right, out result);
			return result;
		}

		/// <summary>
		/// Multiplies two instances.
		/// </summary>
		/// <param name="left">The first instance.</param>
		/// <param name="right">The second instance.</param>
		/// <param name="result">A new instance containing the result of the calculation.</param>
		public static void Multiply(ref Quaternion left, ref Quaternion right, out Quaternion result)
		{
			result = new Quaternion(
				right.W * left.Xyz + left.W * right.Xyz + Vector3.Cross(left.Xyz, right.Xyz),
				left.W * right.W - Vector3.Dot(left.Xyz, right.Xyz));
		}

		/// <summary>
		/// Multiplies an instance by a scalar.
		/// </summary>
		/// <param name="quaternion">The instance.</param>
		/// <param name="scale">The scalar.</param>
		/// <param name="result">A new instance containing the result of the calculation.</param>
		public static void Multiply(ref Quaternion quaternion, double scale, out Quaternion result)
		{
			result = new Quaternion(quaternion.X * scale, quaternion.Y * scale, quaternion.Z * scale, quaternion.W * scale);
		}

		/// <summary>
		/// Multiplies an instance by a scalar.
		/// </summary>
		/// <param name="quaternion">The instance.</param>
		/// <param name="scale">The scalar.</param>
		/// <returns>A new instance containing the result of the calculation.</returns>
		public static Quaternion Multiply(Quaternion quaternion, double scale)
		{
			return new Quaternion(quaternion.X * scale, quaternion.Y * scale, quaternion.Z * scale, quaternion.W * scale);
		}

		#endregion Mult

		#region Conjugate

		/// <summary>
		/// Get the conjugate of the given Quaternion
		/// </summary>
		/// <param name="q">The Quaternion</param>
		/// <returns>The conjugate of the given Quaternion</returns>
		public static Quaternion Conjugate(Quaternion q)
		{
			return new Quaternion(-q.Xyz, q.W);
		}

		/// <summary>
		/// Get the conjugate of the given Quaternion
		/// </summary>
		/// <param name="q">The Quaternion</param>
		/// <param name="result">The conjugate of the given Quaternion</param>
		public static void Conjugate(ref Quaternion q, out Quaternion result)
		{
			result = new Quaternion(-q.Xyz, q.W);
		}

		#endregion Conjugate

		#region Invert

		/// <summary>
		/// Get the inverse of the given Quaternion
		/// </summary>
		/// <param name="q">The Quaternion to invert</param>
		/// <returns>The inverse of the given Quaternion</returns>
		public static Quaternion Invert(Quaternion q)
		{
			Quaternion result;
			Invert(ref q, out result);
			return result;
		}

		/// <summary>
		/// Get the inverse of the given Quaternion
		/// </summary>
		/// <param name="q">The Quaternion to invert</param>
		/// <param name="result">The inverse of the given Quaternion</param>
		public static void Invert(ref Quaternion q, out Quaternion result)
		{
			double lengthSq = q.LengthSquared;
			if (lengthSq != 0.0)
			{
				double i = 1.0 / lengthSq;
				result = new Quaternion(q.Xyz * -i, q.W * i);
			}
			else
			{
				result = q;
			}
		}

		#endregion Invert

		#region Normalize

		/// <summary>
		/// Scale the given Quaternion to unit length
		/// </summary>
		/// <param name="q">The Quaternion to normalize</param>
		/// <returns>The normalized Quaternion</returns>
		public static Quaternion Normalize(Quaternion q)
		{
			Quaternion result;
			Normalize(ref q, out result);
			return result;
		}

		/// <summary>
		/// Scale the given Quaternion to unit length
		/// </summary>
		/// <param name="q">The Quaternion to normalize</param>
		/// <param name="result">The normalized Quaternion</param>
		public static void Normalize(ref Quaternion q, out Quaternion result)
		{
			double scale = 1.0 / q.Length;
			result = new Quaternion(q.Xyz * scale, q.W * scale);
		}

		#endregion Normalize

		#region FromEulerAngles

		public static Quaternion FromEulerAngles(Vector3 rotation)
		{
			Quaternion xRotation = FromAxisAngle(Vector3.UnitX, rotation.X);
			Quaternion yRotation = FromAxisAngle(Vector3.UnitY, rotation.Y);
			Quaternion zRotation = FromAxisAngle(Vector3.UnitZ, rotation.Z);

			//return xRotation * yRotation * zRotation;
			return zRotation * yRotation * xRotation;
		}

		#endregion FromEulerAngles

		#region FromAxisAngle

		/// <summary>
		/// Build a Quaternion from the given axis and angle
		/// </summary>
		/// <param name="axis">The axis to rotate about</param>
		/// <param name="angle">The rotation angle in radians</param>
		/// <returns></returns>
		public static Quaternion FromAxisAngle(Vector3 axis, double angle)
		{
			if (axis.LengthSquared == 0.0)
			{
				return Identity;
			}

			Quaternion result = Identity;

			angle *= 0.5f;
			axis.Normalize();
			result.Xyz = axis * (double)System.Math.Sin(angle);
			result.W = (double)System.Math.Cos(angle);

			return Normalize(result);
		}

		#endregion FromAxisAngle

		#region Slerp

		/// <summary>
		/// Do Spherical linear interpolation between two quaternions
		/// </summary>
		/// <param name="q1">The first Quaternion</param>
		/// <param name="q2">The second Quaternion</param>
		/// <param name="blend">The blend factor</param>
		/// <returns>A smooth blend between the given quaternions</returns>
		public static Quaternion Slerp(Quaternion q1, Quaternion q2, double blend)
		{
			// if either input is zero, return the other.
			if (q1.LengthSquared == 0.0)
			{
				if (q2.LengthSquared == 0.0)
				{
					return Identity;
				}
				return q2;
			}
			else if (q2.LengthSquared == 0.0)
			{
				return q1;
			}

			double cosHalfAngle = q1.W * q2.W + Vector3.Dot(q1.Xyz, q2.Xyz);

			if (cosHalfAngle >= 1.0 || cosHalfAngle <= -1.0)
			{
				// angle = 0.0, so just return one input.
				return q1;
			}
			else if (cosHalfAngle < 0.0)
			{
				q2.Xyz = -q2.Xyz;
				q2.W = -q2.W;
				cosHalfAngle = -cosHalfAngle;
			}

			double blendA;
			double blendB;
			if (cosHalfAngle < 0.99f)
			{
				// do proper slerp for big angles
				double halfAngle = (double)System.Math.Acos(cosHalfAngle);
				double sinHalfAngle = (double)System.Math.Sin(halfAngle);
				double oneOverSinHalfAngle = 1.0 / sinHalfAngle;
				blendA = (double)System.Math.Sin(halfAngle * (1.0 - blend)) * oneOverSinHalfAngle;
				blendB = (double)System.Math.Sin(halfAngle * blend) * oneOverSinHalfAngle;
			}
			else
			{
				// do lerp if angle is really small.
				blendA = 1.0 - blend;
				blendB = blend;
			}

			Quaternion result = new Quaternion(blendA * q1.Xyz + blendB * q2.Xyz, blendA * q1.W + blendB * q2.W);
			if (result.LengthSquared > 0.0)
				return Normalize(result);
			else
				return Identity;
		}

		#endregion Slerp

		#endregion Static

		#region Operators

		/// <summary>
		/// Adds two instances.
		/// </summary>
		/// <param name="left">The first instance.</param>
		/// <param name="right">The second instance.</param>
		/// <returns>The result of the calculation.</returns>
		public static Quaternion operator +(Quaternion left, Quaternion right)
		{
			left.Xyz += right.Xyz;
			left.W += right.W;
			return left;
		}

		/// <summary>
		/// Subtracts two instances.
		/// </summary>
		/// <param name="left">The first instance.</param>
		/// <param name="right">The second instance.</param>
		/// <returns>The result of the calculation.</returns>
		public static Quaternion operator -(Quaternion left, Quaternion right)
		{
			left.Xyz -= right.Xyz;
			left.W -= right.W;
			return left;
		}

		/// <summary>
		/// Multiplies two instances.
		/// </summary>
		/// <param name="left">The first instance.</param>
		/// <param name="right">The second instance.</param>
		/// <returns>The result of the calculation.</returns>
		public static Quaternion operator *(Quaternion left, Quaternion right)
		{
			Multiply(ref left, ref right, out left);
			return left;
		}

		/// <summary>
		/// Multiplies an instance by a scalar.
		/// </summary>
		/// <param name="quaternion">The instance.</param>
		/// <param name="scale">The scalar.</param>
		/// <returns>A new instance containing the result of the calculation.</returns>
		public static Quaternion operator *(Quaternion quaternion, double scale)
		{
			Multiply(ref quaternion, scale, out quaternion);
			return quaternion;
		}

		/// <summary>
		/// Multiplies an instance by a scalar.
		/// </summary>
		/// <param name="quaternion">The instance.</param>
		/// <param name="scale">The scalar.</param>
		/// <returns>A new instance containing the result of the calculation.</returns>
		public static Quaternion operator *(double scale, Quaternion quaternion)
		{
			return new Quaternion(quaternion.X * scale, quaternion.Y * scale, quaternion.Z * scale, quaternion.W * scale);
		}

		/// <summary>
		/// Compares two instances for equality.
		/// </summary>
		/// <param name="left">The first instance.</param>
		/// <param name="right">The second instance.</param>
		/// <returns>True, if left equals right; false otherwise.</returns>
		public static bool operator ==(Quaternion left, Quaternion right)
		{
			return left.Equals(right);
		}

		/// <summary>
		/// Compares two instances for inequality.
		/// </summary>
		/// <param name="left">The first instance.</param>
		/// <param name="right">The second instance.</param>
		/// <returns>True, if left does not equal right; false otherwise.</returns>
		public static bool operator !=(Quaternion left, Quaternion right)
		{
			return !left.Equals(right);
		}

		#endregion Operators

		#region Overrides

		#region public override string ToString()

		/// <summary>
		/// Returns a System.String that represents the current Quaternion.
		/// </summary>
		/// <returns></returns>
		public override string ToString()
		{
			return String.Format("V: {0}, W: {1}", Xyz, W);
		}

		#endregion public override string ToString()

		#region public override bool Equals (object o)

		/// <summary>
		/// Compares this object instance to another object for equality.
		/// </summary>
		/// <param name="other">The other object to be used in the comparison.</param>
		/// <returns>True if both objects are Quaternions of equal value. Otherwise it returns false.</returns>
		public override bool Equals(object other)
		{
			if (other is Quaternion == false) return false;
			return this == (Quaternion)other;
		}

		#endregion public override bool Equals (object o)

		#region public override int GetHashCode ()

		/// <summary>
		/// Provides the hash code for this object.
		/// </summary>
		/// <returns>A hash code formed from the bitwise XOR of this objects members.</returns>
		public override int GetHashCode()
		{
			return new { Xyz.X, Xyz.Y, Xyz.Z, W }.GetHashCode();
		}

		#endregion public override int GetHashCode ()

		#endregion Overrides

		#endregion Public Members

		#region IEquatable<Quaterniond> Members

		/// <summary>
		/// Compares this Quaternion instance to another Quaternion for equality.
		/// </summary>
		/// <param name="other">The other Quaternion to be used in the comparison.</param>
		/// <returns>True if both instances are equal; false otherwise.</returns>
		public bool Equals(Quaternion other)
		{
			return Xyz == other.Xyz && W == other.W;
		}

		#endregion IEquatable<Quaterniond> Members
	}
}