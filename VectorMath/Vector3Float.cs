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
	/// Represents a 3D vector using three float-precision floating-point numbers.
	/// </summary>
	[Serializable]
	[StructLayout(LayoutKind.Sequential)]
	public struct Vector3Float : IEquatable<Vector3Float>
	{
		#region Fields

		/// <summary>
		/// The X component of the Vector3Float.
		/// </summary>
		public float X;

		/// <summary>
		/// The Y component of the Vector3Float.
		/// </summary>
		public float Y;

		/// <summary>
		/// The Z component of the Vector3Float.
		/// </summary>
		public float Z;

		#endregion Fields

		#region Constants

		/// <summary>
		/// Defines a unit-length Vector3Floatd that points towards the X-axis.
		/// </summary>
		public static readonly Vector3Float UnitX = new Vector3Float(1, 0, 0);

		/// <summary>
		/// Defines a unit-length Vector3Floatd that points towards the Y-axis.
		/// </summary>
		public static readonly Vector3Float UnitY = new Vector3Float(0, 1, 0);

		/// <summary>
		/// /// Defines a unit-length Vector3Floatd that points towards the Z-axis.
		/// </summary>
		public static readonly Vector3Float UnitZ = new Vector3Float(0, 0, 1);

		/// <summary>
		/// Defines a zero-length Vector3Float.
		/// </summary>
		public static readonly Vector3Float Zero = new Vector3Float(0, 0, 0);

		/// <summary>
		/// Defines an instance with all components set to 1.
		/// </summary>
		public static readonly Vector3Float One = new Vector3Float(1, 1, 1);

		/// <summary>
		/// Defines an instance with all components set to positive infinity.
		/// </summary>
		public static readonly Vector3Float PositiveInfinity = new Vector3Float(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);

		/// <summary>
		/// Defines an instance with all components set to negative infinity.
		/// </summary>
		public static readonly Vector3Float NegativeInfinity = new Vector3Float(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

		/// <summary>
		/// Defines the size of the Vector3Floatd struct in bytes.
		/// </summary>
		public static readonly int SizeInBytes = Marshal.SizeOf(new Vector3Float());

		#endregion Constants

		#region Constructors

		/// <summary>
		/// Constructs a new Vector3Float.
		/// </summary>
		/// <param name="x">The x component of the Vector3Float.</param>
		/// <param name="y">The y component of the Vector3Float.</param>
		/// <param name="z">The z component of the Vector3Float.</param>
		public Vector3Float(double x, double y, double z)
			: this((float)x, (float)y, (float)z)
		{
		}

		public Vector3Float(float x, float y, float z)
		{
			this.X = x;
			this.Y = y;
			this.Z = z;
		}

		/// <summary>
		/// Constructs a new instance from the given Vector2d.
		/// </summary>
		/// <param name="v">The Vector2d to copy components from.</param>
		public Vector3Float(Vector2 v, double z = 0)
		{
			X = (float)v.X;
			Y = (float)v.Y;
			this.Z = (float)z;
		}

		/// <summary>
		/// Constructs a new instance from the given Vector3Floatd.
		/// </summary>
		/// <param name="v">The Vector3Floatd to copy components from.</param>
		public Vector3Float(Vector3Float v)
		{
			X = v.X;
			Y = v.Y;
			Z = v.Z;
		}

		public Vector3Float(float[] floatArray)
		{
			X = floatArray[0];
			Y = floatArray[1];
			Z = floatArray[2];
		}

		/// <summary>
		/// Constructs a new instance from the given Vector4d.
		/// </summary>
		/// <param name="v">The Vector4d to copy components from.</param>
		public Vector3Float(Vector4 v)
		{
			X = (float)v.X;
			Y = (float)v.Y;
			Z = (float)v.Z;
		}

		public Vector3Float(Vector3 position)
		{
			this.X = (float)position.X;
			this.Y = (float)position.Y;
			this.Z = (float)position.Z;
		}

		#endregion Constructors

		#region Properties

		public float this[int index]
		{
			get
			{
				switch (index)
				{
					case 0:
						return X;

					case 1:
						return Y;

					case 2:
						return Z;

					default:
						return 0;
				}
			}

			set
			{
				switch (index)
				{
					case 0:
						X = value;
						break;

					case 1:
						Y = value;
						break;

					case 2:
						Z = value;
						break;

					default:
						throw new Exception();
				}
			}
		}

		#endregion Properties

		public Vector3 AsVector3()
		{
			return new Vector3(this);
		}

		#region Public Members

		#region Instance

		#region public float Length

		/// <summary>
		/// Gets the length (magnitude) of the vector.
		/// </summary>
		/// <see cref="LengthFast"/>
		/// <seealso cref="LengthSquared"/>
		public float Length
		{
			get
			{
				return (float)Math.Sqrt(X * X + Y * Y + Z * Z);
			}
		}

		#endregion public float Length

		#region public float LengthSquared

		/// <summary>
		/// Gets the square of the vector length (magnitude).
		/// </summary>
		/// <remarks>
		/// This property avoids the costly square root operation required by the Length property. This makes it more suitable
		/// for comparisons.
		/// </remarks>
		/// <see cref="Length"/>
		/// <seealso cref="LengthFast"/>
		public float LengthSquared
		{
			get
			{
				return X * X + Y * Y + Z * Z;
			}
		}

		#endregion public float LengthSquared

		#region public void Normalize()

		/// <summary>
		/// Returns a normalized Vector of this.
		/// </summary>
		/// <returns></returns>
		public Vector3Float GetNormal()
		{
			Vector3Float temp = this;
			temp.Normalize();
			return temp;
		}

		/// <summary>
		/// Scales the Vector3Floatd to unit length.
		/// </summary>
		public void Normalize()
		{
			float scale = 1.0f / this.Length;
			X *= scale;
			Y *= scale;
			Z *= scale;
		}

		#endregion public void Normalize()

		#region public float[] ToArray()

		public float[] ToArray()
		{
			return new float[] { X, Y, Z };
		}

		#endregion public float[] ToArray()

		#endregion Instance

		#region Swizzle

		/// <summary>
		/// Gets or sets an OpenTK.Vector2d with the X and Y components of this instance.
		/// </summary>
		public Vector2 Xy
		{
			get
			{
				return new Vector2(X, Y);
			}
			set
			{
#if true
				throw new NotImplementedException();
#else
                x = value.x; y = value.y;
#endif
			}
		}

		#endregion Swizzle

		#region Operators

		/// <summary>
		/// Adds two instances.
		/// </summary>
		/// <param name="left">The first instance.</param>
		/// <param name="right">The second instance.</param>
		/// <returns>The result of the calculation.</returns>
		public static Vector3Float operator +(Vector3Float left, Vector3Float right)
		{
			left.X += right.X;
			left.Y += right.Y;
			left.Z += right.Z;
			return left;
		}

		/// <summary>
		/// Subtracts two instances.
		/// </summary>
		/// <param name="left">The first instance.</param>
		/// <param name="right">The second instance.</param>
		/// <returns>The result of the calculation.</returns>
		public static Vector3Float operator -(Vector3Float left, Vector3Float right)
		{
			left.X -= right.X;
			left.Y -= right.Y;
			left.Z -= right.Z;
			return left;
		}

		/// <summary>
		/// Negates an instance.
		/// </summary>
		/// <param name="vec">The instance.</param>
		/// <returns>The result of the calculation.</returns>
		public static Vector3Float operator -(Vector3Float vec)
		{
			vec.X = -vec.X;
			vec.Y = -vec.Y;
			vec.Z = -vec.Z;
			return vec;
		}

		/// <summary>
		/// Component wise multiply two vectors together, x*x, y*y, z*z.
		/// </summary>
		/// <param name="vecA"></param>
		/// <param name="vecB"></param>
		/// <returns></returns>
		public static Vector3Float operator *(Vector3Float vecA, Vector3Float vecB)
		{
			vecA.X *= vecB.X;
			vecA.Y *= vecB.Y;
			vecA.Z *= vecB.Z;
			return vecA;
		}

		/// <summary>
		/// Multiplies an instance by a scalar.
		/// </summary>
		/// <param name="vec">The instance.</param>
		/// <param name="scale">The scalar.</param>
		/// <returns>The result of the calculation.</returns>
		public static Vector3Float operator *(Vector3Float vec, double scale)
		{
			vec.X *= (float)scale;
			vec.Y *= (float)scale;
			vec.Z *= (float)scale;
			return vec;
		}

		/// <summary>
		/// Multiplies an instance by a scalar.
		/// </summary>
		/// <param name="scale">The scalar.</param>
		/// <param name="vec">The instance.</param>
		/// <returns>The result of the calculation.</returns>
		public static Vector3Float operator *(float scale, Vector3Float vec)
		{
			vec.X *= scale;
			vec.Y *= scale;
			vec.Z *= scale;
			return vec;
		}

		/// <summary>
		/// Creates a new vector which is the numerator divide by each component of the vector.
		/// </summary>
		/// <param name="numerator"></param>
		/// <param name="vec"></param>
		/// <returns>The result of the calculation.</returns>
		public static Vector3Float operator /(float numerator, Vector3Float vec)
		{
			return new Vector3Float((numerator / vec.X), (numerator / vec.Y), (numerator / vec.Z));
		}

		/// <summary>
		/// Divides an instance by a scalar.
		/// </summary>
		/// <param name="vec">The instance.</param>
		/// <param name="scale">The scalar.</param>
		/// <returns>The result of the calculation.</returns>
		public static Vector3Float operator /(Vector3Float vec, double scale)
		{
			float mult = 1 / (float)scale;
			vec.X *= mult;
			vec.Y *= mult;
			vec.Z *= mult;
			return vec;
		}

		/// <summary>
		/// Compares two instances for equality.
		/// </summary>
		/// <param name="left">The first instance.</param>
		/// <param name="right">The second instance.</param>
		/// <returns>True, if left equals right; false otherwise.</returns>
		public static bool operator ==(Vector3Float left, Vector3Float right)
		{
			return left.Equals(right);
		}

		/// <summary>
		/// Compares two instances for inequality.
		/// </summary>
		/// <param name="left">The first instance.</param>
		/// <param name="right">The second instance.</param>
		/// <returns>True, if left does not equa lright; false otherwise.</returns>
		public static bool operator !=(Vector3Float left, Vector3Float right)
		{
			return !left.Equals(right);
		}

		#endregion Operators

		#region Overrides

		#region public override string ToString()

		/// <summary>
		/// Returns a System.String that represents the current Vector3Float.
		/// </summary>
		/// <returns></returns>
		public override string ToString()
		{
			return $"[{X}, {Y}, {Z}]";
		}

		#endregion public override string ToString()

		#region public override int GetHashCode()

		/// <summary>
		/// return a 64 bit hash code proposed by Jon Skeet
		// http://stackoverflow.com/questions/8094867/good-gethashcode-override-for-list-of-foo-objects-respecting-the-order
		/// </summary>
		/// <returns></returns>
		public long GetLongHashCode()
		{
			long hash = 19;

			unsafe
			{
				hash = hash * 31 + BitConverter.DoubleToInt64Bits(X);
				hash = hash * 31 + BitConverter.DoubleToInt64Bits(Y);
				hash = hash * 31 + BitConverter.DoubleToInt64Bits(Z);
			}

			return hash;
		}

		/// <summary>
		/// Returns the hashcode for this instance.
		/// </summary>
		/// <returns>A System.Int32 containing the unique hashcode for this instance.</returns>
		public override int GetHashCode()
		{
			return new { X, Y, Z }.GetHashCode();
		}

		#endregion public override int GetHashCode()

		#region public override bool Equals(object obj)

		/// <summary>
		/// Indicates whether this instance and a specified object are equal.
		/// </summary>
		/// <param name="obj">The object to compare to.</param>
		/// <returns>True if the instances are equal; false otherwise.</returns>
		public override bool Equals(object obj)
		{
			if (!(obj is Vector3Float))
				return false;

			return this.Equals((Vector3Float)obj);
		}

		/// <summary>
		/// Indicates whether this instance and a specified object are equal within an error range.
		/// </summary>
		/// <param name="OtherVector"></param>
		/// <param name="ErrorValue"></param>
		/// <returns>True if the instances are equal; false otherwise.</returns>
		public bool Equals(Vector3Float OtherVector, double ErrorValue)
		{
			if ((X < OtherVector.X + ErrorValue && X > OtherVector.X - ErrorValue) &&
				(Y < OtherVector.Y + ErrorValue && Y > OtherVector.Y - ErrorValue) &&
				(Z < OtherVector.Z + ErrorValue && Z > OtherVector.Z - ErrorValue))
			{
				return true;
			}

			return false;
		}

		#endregion public override bool Equals(object obj)

		#endregion Overrides

		#endregion Public Members

		#region IEquatable<Vector3Float> Members

		/// <summary>Indicates whether the current vector is equal to another vector.</summary>
		/// <param name="other">A vector to compare with this vector.</param>
		/// <returns>true if the current vector is equal to the vector parameter; otherwise, false.</returns>
		public bool Equals(Vector3Float other)
		{
			return
				X == other.X &&
				Y == other.Y &&
				Z == other.Z;
		}

		#endregion IEquatable<Vector3Float> Members
	}

	public static class Vector3FloatEx
	{
		#region Static

		#region Add

		/// <summary>
		/// Adds two vectors.
		/// </summary>
		/// <param name="a">Left operand.</param>
		/// <param name="b">Right operand.</param>
		/// <returns>Result of operation.</returns>
		public static Vector3Float Add(Vector3Float a, Vector3Float b)
		{
			Add(ref a, ref b, out a);
			return a;
		}

		/// <summary>
		/// Adds two vectors.
		/// </summary>
		/// <param name="a">Left operand.</param>
		/// <param name="b">Right operand.</param>
		/// <param name="result">Result of operation.</param>
		public static void Add(ref Vector3Float a, ref Vector3Float b, out Vector3Float result)
		{
			result = new Vector3Float(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
		}

		#endregion Add

		#region Subtract

		/// <summary>
		/// Subtract one Vector from another
		/// </summary>
		/// <param name="a">First operand</param>
		/// <param name="b">Second operand</param>
		/// <returns>Result of subtraction</returns>
		public static Vector3Float Subtract(Vector3Float a, Vector3Float b)
		{
			Subtract(ref a, ref b, out a);
			return a;
		}

		/// <summary>
		/// Subtract one Vector from another
		/// </summary>
		/// <param name="a">First operand</param>
		/// <param name="b">Second operand</param>
		/// <param name="result">Result of subtraction</param>
		public static void Subtract(ref Vector3Float a, ref Vector3Float b, out Vector3Float result)
		{
			result = new Vector3Float(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
		}

		#endregion Subtract

		#region Multiply

		/// <summary>
		/// Multiplies a vector by a scalar.
		/// </summary>
		/// <param name="vector">Left operand.</param>
		/// <param name="scale">Right operand.</param>
		/// <returns>Result of the operation.</returns>
		public static Vector3Float Multiply(Vector3Float vector, float scale)
		{
			Multiply(ref vector, scale, out vector);
			return vector;
		}

		/// <summary>
		/// Multiplies a vector by a scalar.
		/// </summary>
		/// <param name="vector">Left operand.</param>
		/// <param name="scale">Right operand.</param>
		/// <param name="result">Result of the operation.</param>
		public static void Multiply(ref Vector3Float vector, float scale, out Vector3Float result)
		{
			result = new Vector3Float(vector.X * scale, vector.Y * scale, vector.Z * scale);
		}

		/// <summary>
		/// Multiplies a vector by the components a vector (scale).
		/// </summary>
		/// <param name="vector">Left operand.</param>
		/// <param name="scale">Right operand.</param>
		/// <returns>Result of the operation.</returns>
		public static Vector3Float Multiply(Vector3Float vector, Vector3Float scale)
		{
			Multiply(ref vector, ref scale, out vector);
			return vector;
		}

		/// <summary>
		/// Multiplies a vector by the components of a vector (scale).
		/// </summary>
		/// <param name="vector">Left operand.</param>
		/// <param name="scale">Right operand.</param>
		/// <param name="result">Result of the operation.</param>
		public static void Multiply(ref Vector3Float vector, ref Vector3Float scale, out Vector3Float result)
		{
			result = new Vector3Float(vector.X * scale.X, vector.Y * scale.Y, vector.Z * scale.Z);
		}

		#endregion Multiply

		#region Divide

		/// <summary>
		/// Divides a vector by a scalar.
		/// </summary>
		/// <param name="vector">Left operand.</param>
		/// <param name="scale">Right operand.</param>
		/// <returns>Result of the operation.</returns>
		public static Vector3Float Divide(Vector3Float vector, float scale)
		{
			Divide(ref vector, scale, out vector);
			return vector;
		}

		/// <summary>
		/// Divides a vector by a scalar.
		/// </summary>
		/// <param name="vector">Left operand.</param>
		/// <param name="scale">Right operand.</param>
		/// <param name="result">Result of the operation.</param>
		public static void Divide(ref Vector3Float vector, float scale, out Vector3Float result)
		{
			Multiply(ref vector, 1 / scale, out result);
		}

		/// <summary>
		/// Divides a vector by the components of a vector (scale).
		/// </summary>
		/// <param name="vector">Left operand.</param>
		/// <param name="scale">Right operand.</param>
		/// <returns>Result of the operation.</returns>
		public static Vector3Float Divide(Vector3Float vector, Vector3Float scale)
		{
			Divide(ref vector, ref scale, out vector);
			return vector;
		}

		/// <summary>
		/// Divide a vector by the components of a vector (scale).
		/// </summary>
		/// <param name="vector">Left operand.</param>
		/// <param name="scale">Right operand.</param>
		/// <param name="result">Result of the operation.</param>
		public static void Divide(ref Vector3Float vector, ref Vector3Float scale, out Vector3Float result)
		{
			result = new Vector3Float(vector.X / scale.X, vector.Y / scale.Y, vector.Z / scale.Z);
		}

		#endregion Divide

		#region ComponentMin

		public static float ComponentMin(this Vector3Float vector3)
		{
			return Math.Min(vector3.X, Math.Min(vector3.Y, vector3.Z));
		}

		/// <summary>
		/// Calculate the component-wise minimum of two vectors
		/// </summary>
		/// <param name="a">First operand</param>
		/// <param name="b">Second operand</param>
		/// <returns>The component-wise minimum</returns>
		public static Vector3Float ComponentMin(this Vector3Float a, Vector3Float b)
		{
			a.X = a.X < b.X ? a.X : b.X;
			a.Y = a.Y < b.Y ? a.Y : b.Y;
			a.Z = a.Z < b.Z ? a.Z : b.Z;
			return a;
		}

		/// <summary>
		/// Calculate the component-wise minimum of two vectors
		/// </summary>
		/// <param name="a">First operand</param>
		/// <param name="b">Second operand</param>
		/// <param name="result">The component-wise minimum</param>
		public static void ComponentMin(this Vector3Float a, ref Vector3Float b, out Vector3Float result)
		{
			result.X = a.X < b.X ? a.X : b.X;
			result.Y = a.Y < b.Y ? a.Y : b.Y;
			result.Z = a.Z < b.Z ? a.Z : b.Z;
		}

		#endregion ComponentMin

		#region ComponentMax

		public static float ComponentMax(this Vector3Float vector3)
		{
			return Math.Max(vector3.X, Math.Max(vector3.Y, vector3.Z));
		}

		/// <summary>
		/// Calculate the component-wise maximum of two vectors
		/// </summary>
		/// <param name="a">First operand</param>
		/// <param name="b">Second operand</param>
		/// <returns>The component-wise maximum</returns>
		public static Vector3Float ComponentMax(this Vector3Float a, Vector3Float b)
		{
			a.X = a.X > b.X ? a.X : b.X;
			a.Y = a.Y > b.Y ? a.Y : b.Y;
			a.Z = a.Z > b.Z ? a.Z : b.Z;
			return a;
		}

		/// <summary>
		/// Calculate the component-wise maximum of two vectors
		/// </summary>
		/// <param name="a">First operand</param>
		/// <param name="b">Second operand</param>
		/// <param name="result">The component-wise maximum</param>
		public static void ComponentMax(this Vector3Float a, ref Vector3Float b, out Vector3Float result)
		{
			result.X = a.X > b.X ? a.X : b.X;
			result.Y = a.Y > b.Y ? a.Y : b.Y;
			result.Z = a.Z > b.Z ? a.Z : b.Z;
		}

		#endregion ComponentMax

		#region Min

		/// <summary>
		/// Returns the Vector3d with the minimum magnitude
		/// </summary>
		/// <param name="left">Left operand</param>
		/// <param name="right">Right operand</param>
		/// <returns>The minimum Vector3Float</returns>
		public static Vector3Float Min(Vector3Float left, Vector3Float right)
		{
			return left.LengthSquared < right.LengthSquared ? left : right;
		}

		#endregion Min

		#region Max

		/// <summary>
		/// Returns the Vector3d with the minimum magnitude
		/// </summary>
		/// <param name="left">Left operand</param>
		/// <param name="right">Right operand</param>
		/// <returns>The minimum Vector3Float</returns>
		public static Vector3Float Max(Vector3Float left, Vector3Float right)
		{
			return left.LengthSquared >= right.LengthSquared ? left : right;
		}

		#endregion Max

		#region Clamp

		/// <summary>
		/// Clamp a vector to the given minimum and maximum vectors
		/// </summary>
		/// <param name="vec">Input vector</param>
		/// <param name="min">Minimum vector</param>
		/// <param name="max">Maximum vector</param>
		/// <returns>The clamped vector</returns>
		public static Vector3Float Clamp(Vector3Float vec, Vector3Float min, Vector3Float max)
		{
			vec.X = vec.X < min.X ? min.X : vec.X > max.X ? max.X : vec.X;
			vec.Y = vec.Y < min.Y ? min.Y : vec.Y > max.Y ? max.Y : vec.Y;
			vec.Z = vec.Z < min.Z ? min.Z : vec.Z > max.Z ? max.Z : vec.Z;
			return vec;
		}

		/// <summary>
		/// Clamp a vector to the given minimum and maximum vectors
		/// </summary>
		/// <param name="vec">Input vector</param>
		/// <param name="min">Minimum vector</param>
		/// <param name="max">Maximum vector</param>
		/// <param name="result">The clamped vector</param>
		public static void Clamp(ref Vector3Float vec, ref Vector3Float min, ref Vector3Float max, out Vector3Float result)
		{
			result.X = vec.X < min.X ? min.X : vec.X > max.X ? max.X : vec.X;
			result.Y = vec.Y < min.Y ? min.Y : vec.Y > max.Y ? max.Y : vec.Y;
			result.Z = vec.Z < min.Z ? min.Z : vec.Z > max.Z ? max.Z : vec.Z;
		}

		#endregion Clamp

		#region Normalize

		/// <summary>
		/// Scale a vector to unit length
		/// </summary>
		/// <param name="vec">The input vector</param>
		/// <returns>The normalized vector</returns>
		public static Vector3Float Normalize(Vector3Float vec)
		{
			float scale = 1.0f / vec.Length;
			vec.X *= scale;
			vec.Y *= scale;
			vec.Z *= scale;
			return vec;
		}

		/// <summary>
		/// Scale a vector to unit length
		/// </summary>
		/// <param name="vec">The input vector</param>
		/// <param name="result">The normalized vector</param>
		public static void Normalize(ref Vector3Float vec, out Vector3Float result)
		{
			float scale = 1.0f / vec.Length;
			result.X = vec.X * scale;
			result.Y = vec.Y * scale;
			result.Z = vec.Z * scale;
		}

		#endregion Normalize

		#region Dot

		/// <summary>
		/// Calculate the dot (scalar) product of two vectors
		/// </summary>
		/// <param name="left">First operand</param>
		/// <param name="right">Second operand</param>
		/// <returns>The dot product of the two inputs</returns>
		public static float Dot(this Vector3Float left, Vector3Float right)
		{
			return left.X * right.X + left.Y * right.Y + left.Z * right.Z;
		}

		/// <summary>
		/// Calculate the dot (scalar) product of two vectors
		/// </summary>
		/// <param name="left">First operand</param>
		/// <param name="right">Second operand</param>
		/// <param name="result">The dot product of the two inputs</param>
		public static void Dot(this Vector3Float left, ref Vector3Float right, out float result)
		{
			result = left.X * right.X + left.Y * right.Y + left.Z * right.Z;
		}

		#endregion Dot

		#region Cross

		/// <summary>
		/// Calculate the cross (vector) product of two vectors
		/// </summary>
		/// <param name="left">First operand</param>
		/// <param name="right">Second operand</param>
		/// <returns>The cross product of the two inputs</returns>
		public static Vector3Float Cross(this Vector3Float left, Vector3Float right)
		{
			Vector3Float result;
			left.Cross(ref right, out result);
			return result;
		}

		/// <summary>
		/// Calculate the cross (vector) product of two vectors
		/// </summary>
		/// <param name="left">First operand</param>
		/// <param name="right">Second operand</param>
		/// <returns>The cross product of the two inputs</returns>
		/// <param name="result">The cross product of the two inputs</param>
		public static void Cross(this Vector3Float left, ref Vector3Float right, out Vector3Float result)
		{
			result = new Vector3Float(left.Y * right.Z - left.Z * right.Y,
				left.Z * right.X - left.X * right.Z,
				left.X * right.Y - left.Y * right.X);
		}

		#endregion Cross

		#region Utility

		/// <summary>
		/// Checks if 3 points are collinear (all lie on the same line).
		/// </summary>
		/// <param name="a"></param>
		/// <param name="b"></param>
		/// <param name="c"></param>
		/// <param name="epsilon"></param>
		/// <returns></returns>
		public static bool Collinear(Vector3Float a, Vector3Float b, Vector3Float c, float epsilon = .000001f)
		{
			// Return true if a, b, and c all lie on the same line.
			return Math.Abs(Cross(b - a, c - a).Length) < epsilon;
		}

		#endregion Utility

		#region Lerp

		/// <summary>
		/// Returns a new Vector that is the linear blend of the 2 given Vectors
		/// </summary>
		/// <param name="a">First input vector</param>
		/// <param name="b">Second input vector</param>
		/// <param name="blend">The blend factor. a when blend=0, b when blend=1.</param>
		/// <returns>a when blend=0, b when blend=1, and a linear combination otherwise</returns>
		public static Vector3Float Lerp(Vector3Float a, Vector3Float b, float blend)
		{
			a.X = blend * (b.X - a.X) + a.X;
			a.Y = blend * (b.Y - a.Y) + a.Y;
			a.Z = blend * (b.Z - a.Z) + a.Z;
			return a;
		}

		/// <summary>
		/// Returns a new Vector that is the linear blend of the 2 given Vectors
		/// </summary>
		/// <param name="a">First input vector</param>
		/// <param name="b">Second input vector</param>
		/// <param name="blend">The blend factor. a when blend=0, b when blend=1.</param>
		/// <param name="result">a when blend=0, b when blend=1, and a linear combination otherwise</param>
		public static void Lerp(ref Vector3Float a, ref Vector3Float b, float blend, out Vector3Float result)
		{
			result.X = blend * (b.X - a.X) + a.X;
			result.Y = blend * (b.Y - a.Y) + a.Y;
			result.Z = blend * (b.Z - a.Z) + a.Z;
		}

		#endregion Lerp

		#region Barycentric

		/// <summary>
		/// Interpolate 3 Vectors using Barycentric coordinates
		/// </summary>
		/// <param name="a">First input Vector</param>
		/// <param name="b">Second input Vector</param>
		/// <param name="c">Third input Vector</param>
		/// <param name="u">First Barycentric Coordinate</param>
		/// <param name="v">Second Barycentric Coordinate</param>
		/// <returns>a when u=v=0, b when u=1,v=0, c when u=0,v=1, and a linear combination of a,b,c otherwise</returns>
		public static Vector3Float BaryCentric(Vector3Float a, Vector3Float b, Vector3Float c, float u, float v)
		{
			return a + u * (b - a) + v * (c - a);
		}

		/// <summary>Interpolate 3 Vectors using Barycentric coordinates</summary>
		/// <param name="a">First input Vector.</param>
		/// <param name="b">Second input Vector.</param>
		/// <param name="c">Third input Vector.</param>
		/// <param name="u">First Barycentric Coordinate.</param>
		/// <param name="v">Second Barycentric Coordinate.</param>
		/// <param name="result">Output Vector. a when u=v=0, b when u=1,v=0, c when u=0,v=1, and a linear combination of a,b,c otherwise</param>
		public static void BaryCentric(ref Vector3Float a, ref Vector3Float b, ref Vector3Float c, float u, float v, out Vector3Float result)
		{
			result = a; // copy

			Vector3Float temp = b; // copy
			Subtract(ref temp, ref a, out temp);
			Multiply(ref temp, u, out temp);
			Add(ref result, ref temp, out result);

			temp = c; // copy
			Subtract(ref temp, ref a, out temp);
			Multiply(ref temp, v, out temp);
			Add(ref result, ref temp, out result);
		}

		#endregion Barycentric

		#region Transform

		/// <summary>Transform a direction vector by the given Matrix
		/// Assumes the matrix has a bottom row of (0,0,0,1), that is the translation part is ignored.
		/// </summary>
		/// <param name="vec">The vector to transform</param>
		/// <param name="mat">The desired transformation</param>
		/// <returns>The transformed vector</returns>
		public static Vector3Float TransformVector(this Vector3Float vec, Matrix4X4 mat)
		{
			return new Vector3Float(
				vec.Dot(new Vector3Float(mat.Column0)),
				vec.Dot(new Vector3Float(mat.Column1)),
				vec.Dot(new Vector3Float(mat.Column2)));
		}

		/// <summary>Transform a direction vector by the given Matrix
		/// Assumes the matrix has a bottom row of (0,0,0,1), that is the translation part is ignored.
		/// </summary>
		/// <param name="vec">The vector to transform</param>
		/// <param name="mat">The desired transformation</param>
		/// <param name="result">The transformed vector</param>
		public static void TransformVector(this Vector3Float vec, ref Matrix4X4 mat, out Vector3Float result)
		{
#if true
			throw new NotImplementedException();
#else
            result.x = vec.x * mat.Row0.x +
                       vec.y * mat.Row1.x +
                       vec.z * mat.Row2.x;

            result.y = vec.x * mat.Row0.y +
                       vec.y * mat.Row1.y +
                       vec.z * mat.Row2.y;

            result.z = vec.x * mat.Row0.z +
                       vec.y * mat.Row1.z +
                       vec.z * mat.Row2.z;
#endif
		}

		/// <summary>Transform a Normal by the given Matrix</summary>
		/// <remarks>
		/// This calculates the inverse of the given matrix, use TransformNormalInverse if you
		/// already have the inverse to avoid this extra calculation
		/// </remarks>
		/// <param name="norm">The normal to transform</param>
		/// <param name="mat">The desired transformation</param>
		/// <returns>The transformed normal</returns>
		public static Vector3Float TransformNormal(this Vector3Float norm, Matrix4X4 mat)
		{
			mat.Invert();
			return norm.TransformNormalInverse(mat);
		}

		/// <summary>Transform a Normal by the given Matrix</summary>
		/// <remarks>
		/// This calculates the inverse of the given matrix, use TransformNormalInverse if you
		/// already have the inverse to avoid this extra calculation
		/// </remarks>
		/// <param name="norm">The normal to transform</param>
		/// <param name="mat">The desired transformation</param>
		/// <param name="result">The transformed normal</param>
		public static void TransformNormal(this Vector3Float norm, ref Matrix4X4 mat, out Vector3Float result)
		{
			Matrix4X4 Inverse = Matrix4X4.Invert(mat);
			norm.TransformNormalInverse(ref Inverse, out result);
		}

		/// <summary>Transform a Normal by the (transpose of the) given Matrix</summary>
		/// <remarks>
		/// This version doesn't calculate the inverse matrix.
		/// Use this version if you already have the inverse of the desired transform to hand
		/// </remarks>
		/// <param name="norm">The normal to transform</param>
		/// <param name="invMat">The inverse of the desired transformation</param>
		/// <returns>The transformed normal</returns>
		public static Vector3Float TransformNormalInverse(this Vector3Float norm, Matrix4X4 invMat)
		{
			return new Vector3Float(
				norm.Dot(new Vector3Float(invMat.Row0)),
				norm.Dot(new Vector3Float(invMat.Row1)),
				norm.Dot(new Vector3Float(invMat.Row2)));
		}

		/// <summary>Transform a Normal by the (transpose of the) given Matrix</summary>
		/// <remarks>
		/// This version doesn't calculate the inverse matrix.
		/// Use this version if you already have the inverse of the desired transform to hand
		/// </remarks>
		/// <param name="norm">The normal to transform</param>
		/// <param name="invMat">The inverse of the desired transformation</param>
		/// <param name="result">The transformed normal</param>
		public static void TransformNormalInverse(this Vector3Float norm, ref Matrix4X4 invMat, out Vector3Float result)
		{
#if true
			throw new NotImplementedException();
#else
            result.x = norm.x * invMat.Row0.x +
                       norm.y * invMat.Row0.y +
                       norm.z * invMat.Row0.z;

            result.y = norm.x * invMat.Row1.x +
                       norm.y * invMat.Row1.y +
                       norm.z * invMat.Row1.z;

            result.z = norm.x * invMat.Row2.x +
                       norm.y * invMat.Row2.y +
                       norm.z * invMat.Row2.z;
#endif
		}

		/// <summary>Transform a Position by the given Matrix</summary>
		/// <param name="pos">The position to transform</param>
		/// <param name="mat">The desired transformation</param>
		/// <returns>The transformed position</returns>
		public static Vector3Float TransformPosition(this Vector3Float pos, Matrix4X4 mat)
		{
#if true
			throw new NotImplementedException();
#else
            return new Vector3Float(
                Vector3Float.Dot(pos, new Vector3Float((float)mat.Column0)) + mat.Row3.x,
                Vector3Float.Dot(pos, new Vector3Float((float)mat.Column1)) + mat.Row3.y,
                Vector3Float.Dot(pos, new Vector3Float((float)mat.Column2)) + mat.Row3.z);
#endif
		}

		/// <summary>Transform a Position by the given Matrix</summary>
		/// <param name="pos">The position to transform</param>
		/// <param name="mat">The desired transformation</param>
		/// <param name="result">The transformed position</param>
		public static void TransformPosition(this Vector3Float pos, ref Matrix4X4 mat, out Vector3Float result)
		{
#if true
			throw new NotImplementedException();
#else
            result.x = pos.x * mat.Row0.x +
                       pos.y * mat.Row1.x +
                       pos.z * mat.Row2.x +
                       mat.Row3.x;

            result.y = pos.x * mat.Row0.y +
                       pos.y * mat.Row1.y +
                       pos.z * mat.Row2.y +
                       mat.Row3.y;

            result.z = pos.x * mat.Row0.z +
                       pos.y * mat.Row1.z +
                       pos.z * mat.Row2.z +
                       mat.Row3.z;
#endif
		}

		/// <summary>
		/// Transform all the vectors in the array by the given Matrix.
		/// </summary>
		/// <param name="boundsVerts"></param>
		/// <param name="rotationQuaternion"></param>
		public static void Transform(this Vector3Float[] vecArray, Matrix4X4 mat)
		{
			for (int i = 0; i < vecArray.Length; i++)
			{
				vecArray[i] = Transform(vecArray[i], mat);
			}
		}

		/// <summary>Transform a Vector by the given Matrix</summary>
		/// <param name="vec">The vector to transform</param>
		/// <param name="mat">The desired transformation</param>
		/// <returns>The transformed vector</returns>
		public static Vector3Float Transform(this Vector3Float vec, Matrix4X4 mat)
		{
			Vector3Float result;
			Transform(vec, ref mat, out result);
			return result;
		}

		/// <summary>Transform a Vector by the given Matrix</summary>
		/// <param name="vec">The vector to transform</param>
		/// <param name="mat">The desired transformation</param>
		/// <param name="result">The transformed vector</param>
		public static void Transform(this Vector3Float inVec, ref Matrix4X4 mat, out Vector3Float result)
		{
			Vector3 vec = new Vector3(inVec);
			Vector4 v4 = new Vector4(vec.X, vec.Y, vec.Z, 1.0);
			Vector4.Transform(ref v4, ref mat, out v4);
			result = new Vector3Float(v4.Xyz);
		}

		/// <summary>
		/// Transforms a vector by a quaternion rotation.
		/// </summary>
		/// <param name="vec">The vector to transform.</param>
		/// <param name="quat">The quaternion to rotate the vector by.</param>
		/// <returns>The result of the operation.</returns>
		public static Vector3Float Transform(this Vector3Float vec, Quaternion quat)
		{
#if true
			throw new NotImplementedException();
#else
            Vector3Float result;
            Transform(ref vec, ref quat, out result);
            return result;
#endif
		}

		/// <summary>
		/// Transforms a vector by a quaternion rotation.
		/// </summary>
		/// <param name="vec">The vector to transform.</param>
		/// <param name="quat">The quaternion to rotate the vector by.</param>
		/// <param name="result">The result of the operation.</param>
		public static void Transform(this Vector3Float vec, ref Quaternion quat, out Vector3Float result)
		{
#if true
			throw new NotImplementedException();
#else
            // Since vec.W == 0, we can optimize quat * vec * quat^-1 as follows:
            // vec + 2.0 * cross(quat.xyz, cross(quat.xyz, vec) + quat.w * vec)
            Vector3Float xyz = quat.Xyz, temp, temp2;
            Vector3Float.Cross(ref xyz, ref vec, out temp);
            Vector3Float.Multiply(ref vec, quat.W, out temp2);
            Vector3Float.Add(ref temp, ref temp2, out temp);
            Vector3Float.Cross(ref xyz, ref temp, out temp);
            Vector3Float.Multiply(ref temp, 2, out temp);
            Vector3Float.Add(ref vec, ref temp, out result);
#endif
		}

		/// <summary>
		/// Transform all the vectors in the array by the quaternion rotation.
		/// </summary>
		/// <param name="boundsVerts"></param>
		/// <param name="rotationQuaternion"></param>
		public static void Transform(this Vector3Float[] vecArray, Quaternion rotationQuaternion)
		{
			for (int i = 0; i < vecArray.Length; i++)
			{
				vecArray[i] = Transform(vecArray[i], rotationQuaternion);
			}
		}

		/// <summary>
		/// Transform a Vector3d by the given Matrix, and project the resulting Vector4 back to a Vector3Float
		/// </summary>
		/// <param name="vec">The vector to transform</param>
		/// <param name="mat">The desired transformation</param>
		/// <returns>The transformed vector</returns>
		public static Vector3Float TransformPerspective(this Vector3Float vec, Matrix4X4 mat)
		{
#if true
			throw new NotImplementedException();
#else
            Vector3Float result;
            TransformPerspective(ref vec, ref mat, out result);
            return result;
#endif
		}

		/// <summary>Transform a Vector3d by the given Matrix, and project the resulting Vector4d back to a Vector3d</summary>
		/// <param name="vec">The vector to transform</param>
		/// <param name="mat">The desired transformation</param>
		/// <param name="result">The transformed vector</param>
		public static void TransformPerspective(this Vector3Float vec, ref Matrix4X4 mat, out Vector3Float result)
		{
#if true
			throw new NotImplementedException();
#else
            Vector4 v = new Vector4(vec);
            Vector4.Transform(ref v, ref mat, out v);
            result.x = v.x / v.w;
            result.y = v.y / v.w;
            result.z = v.z / v.w;
#endif
		}

		#endregion Transform

		#region CalculateAngle

		/// <summary>
		/// Calculates the angle (in radians) between two vectors.
		/// </summary>
		/// <param name="first">The first vector.</param>
		/// <param name="second">The second vector.</param>
		/// <returns>Angle (in radians) between the vectors.</returns>
		/// <remarks>Note that the returned angle is never bigger than the constant Pi.</remarks>
		public static float CalculateAngle(this Vector3Float first, Vector3Float second)
		{
			return (float)Math.Acos((first.Dot(second)) / (first.Length * second.Length));
		}

		/// <summary>Calculates the angle (in radians) between two vectors.</summary>
		/// <param name="first">The first vector.</param>
		/// <param name="second">The second vector.</param>
		/// <param name="result">Angle (in radians) between the vectors.</param>
		/// <remarks>Note that the returned angle is never bigger than the constant Pi.</remarks>
		public static void CalculateAngle(this Vector3Float first, ref Vector3Float second, out float result)
		{
			float temp;
			first.Dot(ref second, out temp);
			result = (float)Math.Acos(temp / (first.Length * second.Length));
		}

		#endregion CalculateAngle

		#endregion Static
	}
}