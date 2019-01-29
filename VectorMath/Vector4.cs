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
	/// <summary>Represents a 4D vector using four double-precision floating-point numbers.</summary>
	[Serializable]
	[StructLayout(LayoutKind.Sequential)]
	public struct Vector4 : IEquatable<Vector4>
	{
		#region Fields

		/// <summary>
		/// The X component of the Vector4d.
		/// </summary>
		public double X;

		/// <summary>
		/// The Y component of the Vector4d.
		/// </summary>
		public double Y;

		/// <summary>
		/// The Z component of the Vector4d.
		/// </summary>
		public double Z;

		/// <summary>
		/// The W component of the Vector4d.
		/// </summary>
		public double W;

		/// <summary>
		/// Defines a unit-length Vector4d that points towards the X-axis.
		/// </summary>
		public static Vector4 UnitX = new Vector4(1, 0, 0, 0);

		/// <summary>
		/// Defines a unit-length Vector4d that points towards the Y-axis.
		/// </summary>
		public static Vector4 UnitY = new Vector4(0, 1, 0, 0);

		/// <summary>
		/// Defines a unit-length Vector4d that points towards the Z-axis.
		/// </summary>
		public static Vector4 UnitZ = new Vector4(0, 0, 1, 0);

		/// <summary>
		/// Defines a unit-length Vector4d that points towards the W-axis.
		/// </summary>
		public static Vector4 UnitW = new Vector4(0, 0, 0, 1);

		/// <summary>
		/// Defines a zero-length Vector4d.
		/// </summary>
		public static Vector4 Zero = new Vector4(0, 0, 0, 0);

		/// <summary>
		/// Defines an instance with all components set to 1.
		/// </summary>
		public static readonly Vector4 One = new Vector4(1, 1, 1, 1);

		/// <summary>
		/// Defines the size of the Vector4d struct in bytes.
		/// </summary>
		public static readonly int SizeInBytes = Marshal.SizeOf(new Vector4());

		#endregion Fields

		#region Constructors

		/// <summary>
		/// Constructs a new Vector4d.
		/// </summary>
		/// <param name="x">The x component of the Vector4d.</param>
		/// <param name="y">The y component of the Vector4d.</param>
		/// <param name="z">The z component of the Vector4d.</param>
		/// <param name="w">The w component of the Vector4d.</param>
		public Vector4(double x, double y, double z, double w)
		{
			this.X = x;
			this.Y = y;
			this.Z = z;
			this.W = w;
		}

		/// <summary>
		/// Constructs a new Vector4d from the given Vector2d.
		/// </summary>
		/// <param name="v">The Vector2d to copy components from.</param>
		public Vector4(Vector2 v)
		{
			X = v.X;
			Y = v.Y;
			Z = 0.0f;
			W = 0.0f;
		}

		/// <summary>
		/// Constructs a new Vector4d from the given Vector3d.
		/// </summary>
		/// <param name="v">The Vector3d to copy components from.</param>
		public Vector4(Vector3 v)
		{
			X = v.X;
			Y = v.Y;
			Z = v.Z;
			W = 0.0f;
		}

		/// <summary>
		/// Constructs a new Vector4d from the specified Vector3d and w component.
		/// </summary>
		/// <param name="v">The Vector3d to copy components from.</param>
		/// <param name="w">The w component of the new Vector4.</param>
		public Vector4(Vector3 v, double w)
		{
			X = v.X;
			Y = v.Y;
			Z = v.Z;
			this.W = w;
		}

		/// <summary>
		/// Constructs a new Vector4d from the given Vector4d.
		/// </summary>
		/// <param name="v">The Vector4d to copy components from.</param>
		public Vector4(Vector4 v)
		{
			X = v.X;
			Y = v.Y;
			Z = v.Z;
			W = v.W;
		}

		#endregion Constructors

		#region Public Members

		#region Properties

		public double this[int index]
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

					case 3:
						return W;

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

					case 3:
						W = value;
						break;

					default:
						throw new Exception();
				}
			}
		}

		#endregion Properties

		#region Instance

		#region public double Length

		/// <summary>
		/// Gets the length (magnitude) of the vector.
		/// </summary>
		/// <see cref="LengthFast"/>
		/// <seealso cref="LengthSquared"/>
		public double Length
		{
			get
			{
				return System.Math.Sqrt(X * X + Y * Y + Z * Z + W * W);
			}
		}

		#endregion public double Length

		#region public double LengthSquared

		/// <summary>
		/// Gets the square of the vector length (magnitude).
		/// </summary>
		/// <remarks>
		/// This property avoids the costly square root operation required by the Length property. This makes it more suitable
		/// for comparisons.
		/// </remarks>
		/// <see cref="Length"/>
		public double LengthSquared
		{
			get
			{
				return X * X + Y * Y + Z * Z + W * W;
			}
		}

		#endregion public double LengthSquared

		#region public void Normalize()

		/// <summary>
		/// Scales the Vector4d to unit length.
		/// </summary>
		public void Normalize()
		{
			double scale = 1.0 / this.Length;
			X *= scale;
			Y *= scale;
			Z *= scale;
			W *= scale;
		}

		#endregion public void Normalize()

		#endregion Instance

		#region Static

		#region Add

		/// <summary>
		/// Adds two vectors.
		/// </summary>
		/// <param name="a">Left operand.</param>
		/// <param name="b">Right operand.</param>
		/// <returns>Result of operation.</returns>
		public static Vector4 Add(Vector4 a, Vector4 b)
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
		public static void Add(ref Vector4 a, ref Vector4 b, out Vector4 result)
		{
			result = new Vector4(a.X + b.X, a.Y + b.Y, a.Z + b.Z, a.W + b.W);
		}

		#endregion Add

		#region Subtract

		/// <summary>
		/// Subtract one Vector from another
		/// </summary>
		/// <param name="a">First operand</param>
		/// <param name="b">Second operand</param>
		/// <returns>Result of subtraction</returns>
		public static Vector4 Subtract(Vector4 a, Vector4 b)
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
		public static void Subtract(ref Vector4 a, ref Vector4 b, out Vector4 result)
		{
			result = new Vector4(a.X - b.X, a.Y - b.Y, a.Z - b.Z, a.W - b.W);
		}

		#endregion Subtract

		#region Multiply

		/// <summary>
		/// Multiplies a vector by a scalar.
		/// </summary>
		/// <param name="vector">Left operand.</param>
		/// <param name="scale">Right operand.</param>
		/// <returns>Result of the operation.</returns>
		public static Vector4 Multiply(Vector4 vector, double scale)
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
		public static void Multiply(ref Vector4 vector, double scale, out Vector4 result)
		{
			result = new Vector4(vector.X * scale, vector.Y * scale, vector.Z * scale, vector.W * scale);
		}

		/// <summary>
		/// Multiplies a vector by the components a vector (scale).
		/// </summary>
		/// <param name="vector">Left operand.</param>
		/// <param name="scale">Right operand.</param>
		/// <returns>Result of the operation.</returns>
		public static Vector4 Multiply(Vector4 vector, Vector4 scale)
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
		public static void Multiply(ref Vector4 vector, ref Vector4 scale, out Vector4 result)
		{
			result = new Vector4(vector.X * scale.X, vector.Y * scale.Y, vector.Z * scale.Z, vector.W * scale.W);
		}

		#endregion Multiply

		#region Divide

		/// <summary>
		/// Divides a vector by a scalar.
		/// </summary>
		/// <param name="vector">Left operand.</param>
		/// <param name="scale">Right operand.</param>
		/// <returns>Result of the operation.</returns>
		public static Vector4 Divide(Vector4 vector, double scale)
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
		public static void Divide(ref Vector4 vector, double scale, out Vector4 result)
		{
			Multiply(ref vector, 1 / scale, out result);
		}

		/// <summary>
		/// Divides a vector by the components of a vector (scale).
		/// </summary>
		/// <param name="vector">Left operand.</param>
		/// <param name="scale">Right operand.</param>
		/// <returns>Result of the operation.</returns>
		public static Vector4 Divide(Vector4 vector, Vector4 scale)
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
		public static void Divide(ref Vector4 vector, ref Vector4 scale, out Vector4 result)
		{
			result = new Vector4(vector.X / scale.X, vector.Y / scale.Y, vector.Z / scale.Z, vector.W / scale.W);
		}

		#endregion Divide

		#region Min

		/// <summary>
		/// Calculate the component-wise minimum of two vectors
		/// </summary>
		/// <param name="a">First operand</param>
		/// <param name="b">Second operand</param>
		/// <returns>The component-wise minimum</returns>
		public static Vector4 Min(Vector4 a, Vector4 b)
		{
			a.X = a.X < b.X ? a.X : b.X;
			a.Y = a.Y < b.Y ? a.Y : b.Y;
			a.Z = a.Z < b.Z ? a.Z : b.Z;
			a.W = a.W < b.W ? a.W : b.W;
			return a;
		}

		/// <summary>
		/// Calculate the component-wise minimum of two vectors
		/// </summary>
		/// <param name="a">First operand</param>
		/// <param name="b">Second operand</param>
		/// <param name="result">The component-wise minimum</param>
		public static void Min(ref Vector4 a, ref Vector4 b, out Vector4 result)
		{
			result.X = a.X < b.X ? a.X : b.X;
			result.Y = a.Y < b.Y ? a.Y : b.Y;
			result.Z = a.Z < b.Z ? a.Z : b.Z;
			result.W = a.W < b.W ? a.W : b.W;
		}

		#endregion Min

		#region Max

		/// <summary>
		/// Calculate the component-wise maximum of two vectors
		/// </summary>
		/// <param name="a">First operand</param>
		/// <param name="b">Second operand</param>
		/// <returns>The component-wise maximum</returns>
		public static Vector4 Max(Vector4 a, Vector4 b)
		{
			a.X = a.X > b.X ? a.X : b.X;
			a.Y = a.Y > b.Y ? a.Y : b.Y;
			a.Z = a.Z > b.Z ? a.Z : b.Z;
			a.W = a.W > b.W ? a.W : b.W;
			return a;
		}

		/// <summary>
		/// Calculate the component-wise maximum of two vectors
		/// </summary>
		/// <param name="a">First operand</param>
		/// <param name="b">Second operand</param>
		/// <param name="result">The component-wise maximum</param>
		public static void Max(ref Vector4 a, ref Vector4 b, out Vector4 result)
		{
			result.X = a.X > b.X ? a.X : b.X;
			result.Y = a.Y > b.Y ? a.Y : b.Y;
			result.Z = a.Z > b.Z ? a.Z : b.Z;
			result.W = a.W > b.W ? a.W : b.W;
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
		public static Vector4 Clamp(Vector4 vec, Vector4 min, Vector4 max)
		{
			vec.X = vec.X < min.X ? min.X : vec.X > max.X ? max.X : vec.X;
			vec.Y = vec.Y < min.Y ? min.Y : vec.Y > max.Y ? max.Y : vec.Y;
			vec.Z = vec.X < min.Z ? min.Z : vec.Z > max.Z ? max.Z : vec.Z;
			vec.W = vec.Y < min.W ? min.W : vec.W > max.W ? max.W : vec.W;
			return vec;
		}

		/// <summary>
		/// Clamp a vector to the given minimum and maximum vectors
		/// </summary>
		/// <param name="vec">Input vector</param>
		/// <param name="min">Minimum vector</param>
		/// <param name="max">Maximum vector</param>
		/// <param name="result">The clamped vector</param>
		public static void Clamp(ref Vector4 vec, ref Vector4 min, ref Vector4 max, out Vector4 result)
		{
			result.X = vec.X < min.X ? min.X : vec.X > max.X ? max.X : vec.X;
			result.Y = vec.Y < min.Y ? min.Y : vec.Y > max.Y ? max.Y : vec.Y;
			result.Z = vec.X < min.Z ? min.Z : vec.Z > max.Z ? max.Z : vec.Z;
			result.W = vec.Y < min.W ? min.W : vec.W > max.W ? max.W : vec.W;
		}

		#endregion Clamp

		#region Normalize

		/// <summary>
		/// Scale a vector to unit length
		/// </summary>
		/// <param name="vec">The input vector</param>
		/// <returns>The normalized vector</returns>
		public static Vector4 Normalize(Vector4 vec)
		{
			double scale = 1.0 / vec.Length;
			vec.X *= scale;
			vec.Y *= scale;
			vec.Z *= scale;
			vec.W *= scale;
			return vec;
		}

		/// <summary>
		/// Scale a vector to unit length
		/// </summary>
		/// <param name="vec">The input vector</param>
		/// <param name="result">The normalized vector</param>
		public static void Normalize(ref Vector4 vec, out Vector4 result)
		{
			double scale = 1.0 / vec.Length;
			result.X = vec.X * scale;
			result.Y = vec.Y * scale;
			result.Z = vec.Z * scale;
			result.W = vec.W * scale;
		}

		#endregion Normalize

		#region Dot

		/// <summary>
		/// Calculate the dot product of two vectors
		/// </summary>
		/// <param name="left">First operand</param>
		/// <param name="right">Second operand</param>
		/// <returns>The dot product of the two inputs</returns>
		public static double Dot(Vector4 left, Vector4 right)
		{
			return left.X * right.X + left.Y * right.Y + left.Z * right.Z + left.W * right.W;
		}

		/// <summary>
		/// Calculate the dot product of two vectors
		/// </summary>
		/// <param name="left">First operand</param>
		/// <param name="right">Second operand</param>
		/// <param name="result">The dot product of the two inputs</param>
		public static void Dot(ref Vector4 left, ref Vector4 right, out double result)
		{
			result = left.X * right.X + left.Y * right.Y + left.Z * right.Z + left.W * right.W;
		}

		#endregion Dot

		#region Lerp

		/// <summary>
		/// Returns a new Vector that is the linear blend of the 2 given Vectors
		/// </summary>
		/// <param name="a">First input vector</param>
		/// <param name="b">Second input vector</param>
		/// <param name="blend">The blend factor. a when blend=0, b when blend=1.</param>
		/// <returns>a when blend=0, b when blend=1, and a linear combination otherwise</returns>
		public static Vector4 Lerp(Vector4 a, Vector4 b, double blend)
		{
			a.X = blend * (b.X - a.X) + a.X;
			a.Y = blend * (b.Y - a.Y) + a.Y;
			a.Z = blend * (b.Z - a.Z) + a.Z;
			a.W = blend * (b.W - a.W) + a.W;
			return a;
		}

		/// <summary>
		/// Returns a new Vector that is the linear blend of the 2 given Vectors
		/// </summary>
		/// <param name="a">First input vector</param>
		/// <param name="b">Second input vector</param>
		/// <param name="blend">The blend factor. a when blend=0, b when blend=1.</param>
		/// <param name="result">a when blend=0, b when blend=1, and a linear combination otherwise</param>
		public static void Lerp(ref Vector4 a, ref Vector4 b, double blend, out Vector4 result)
		{
			result.X = blend * (b.X - a.X) + a.X;
			result.Y = blend * (b.Y - a.Y) + a.Y;
			result.Z = blend * (b.Z - a.Z) + a.Z;
			result.W = blend * (b.W - a.W) + a.W;
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
		public static Vector4 BaryCentric(Vector4 a, Vector4 b, Vector4 c, double u, double v)
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
		public static void BaryCentric(ref Vector4 a, ref Vector4 b, ref Vector4 c, double u, double v, out Vector4 result)
		{
			result = a; // copy

			Vector4 temp = b; // copy
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

		/// <summary>Transform a Vector by the given Matrix</summary>
		/// <param name="vec">The vector to transform</param>
		/// <param name="mat">The desired transformation</param>
		/// <returns>The transformed vector</returns>
		public static Vector4 Transform(Vector4 vec, Matrix4X4 mat)
		{
			Vector4 result;
			Transform(vec, ref mat, out result);
			return result;
		}

		/// <summary>Transform a Vector by the given Matrix</summary>
		/// <param name="vec">The vector to transform</param>
		/// <param name="mat">The desired transformation</param>
		/// <param name="result">The transformed vector</param>
		public static void Transform(Vector4 vec, ref Matrix4X4 mat, out Vector4 result)
		{
			result = new Vector4(
				vec.X * mat.Row0.X + vec.Y * mat.Row1.X + vec.Z * mat.Row2.X + vec.W * mat.Row3.X,
				vec.X * mat.Row0.Y + vec.Y * mat.Row1.Y + vec.Z * mat.Row2.Y + vec.W * mat.Row3.Y,
				vec.X * mat.Row0.Z + vec.Y * mat.Row1.Z + vec.Z * mat.Row2.Z + vec.W * mat.Row3.Z,
				vec.X * mat.Row0.W + vec.Y * mat.Row1.W + vec.Z * mat.Row2.W + vec.W * mat.Row3.W);
		}

		/// <summary>
		/// Transforms a vector by a quaternion rotation.
		/// </summary>
		/// <param name="vec">The vector to transform.</param>
		/// <param name="quat">The quaternion to rotate the vector by.</param>
		/// <returns>The result of the operation.</returns>
		public static Vector4 Transform(Vector4 vec, Quaternion quat)
		{
			Vector4 result;
			Transform(ref vec, ref quat, out result);
			return result;
		}

		/// <summary>
		/// Transforms a vector by a quaternion rotation.
		/// </summary>
		/// <param name="vec">The vector to transform.</param>
		/// <param name="quat">The quaternion to rotate the vector by.</param>
		/// <param name="result">The result of the operation.</param>
		public static void Transform(ref Vector4 vec, ref Quaternion quat, out Vector4 result)
		{
			Quaternion v = new Quaternion(vec.X, vec.Y, vec.Z, vec.W), i, t;
			Quaternion.Invert(ref quat, out i);
			Quaternion.Multiply(ref quat, ref v, out t);
			Quaternion.Multiply(ref t, ref i, out v);

			result = new Vector4(v.X, v.Y, v.Z, v.W);
		}

		#endregion Transform

		#endregion Static

		#region Swizzle

		/// <summary>
		/// Gets or sets an OpenTK.Vector2d with the X and Y components of this instance.
		/// </summary>
		public Vector2 Xy { get { return new Vector2(X, Y); } set { X = value.X; Y = value.Y; } }

		/// <summary>
		/// Gets or sets an OpenTK.Vector3d with the X, Y and Z components of this instance.
		/// </summary>
		public Vector3 Xyz { get { return new Vector3(X, Y, Z); } set { X = value.X; Y = value.Y; Z = value.Z; } }

		#endregion Swizzle

		#region Operators

		/// <summary>
		/// Adds two instances.
		/// </summary>
		/// <param name="left">The first instance.</param>
		/// <param name="right">The second instance.</param>
		/// <returns>The result of the calculation.</returns>
		public static Vector4 operator +(Vector4 left, Vector4 right)
		{
			left.X += right.X;
			left.Y += right.Y;
			left.Z += right.Z;
			left.W += right.W;
			return left;
		}

		/// <summary>
		/// Subtracts two instances.
		/// </summary>
		/// <param name="left">The first instance.</param>
		/// <param name="right">The second instance.</param>
		/// <returns>The result of the calculation.</returns>
		public static Vector4 operator -(Vector4 left, Vector4 right)
		{
			left.X -= right.X;
			left.Y -= right.Y;
			left.Z -= right.Z;
			left.W -= right.W;
			return left;
		}

		/// <summary>
		/// Negates an instance.
		/// </summary>
		/// <param name="vec">The instance.</param>
		/// <returns>The result of the calculation.</returns>
		public static Vector4 operator -(Vector4 vec)
		{
			vec.X = -vec.X;
			vec.Y = -vec.Y;
			vec.Z = -vec.Z;
			vec.W = -vec.W;
			return vec;
		}

		/// <summary>
		/// Multiplies an instance by a scalar.
		/// </summary>
		/// <param name="vec">The instance.</param>
		/// <param name="scale">The scalar.</param>
		/// <returns>The result of the calculation.</returns>
		public static Vector4 operator *(Vector4 vec, double scale)
		{
			vec.X *= scale;
			vec.Y *= scale;
			vec.Z *= scale;
			vec.W *= scale;
			return vec;
		}

		/// <summary>
		/// Multiplies an instance by a scalar.
		/// </summary>
		/// <param name="scale">The scalar.</param>
		/// <param name="vec">The instance.</param>
		/// <returns>The result of the calculation.</returns>
		public static Vector4 operator *(double scale, Vector4 vec)
		{
			vec.X *= scale;
			vec.Y *= scale;
			vec.Z *= scale;
			vec.W *= scale;
			return vec;
		}

		/// <summary>
		/// Divides an instance by a scalar.
		/// </summary>
		/// <param name="vec">The instance.</param>
		/// <param name="scale">The scalar.</param>
		/// <returns>The result of the calculation.</returns>
		public static Vector4 operator /(Vector4 vec, double scale)
		{
			double mult = 1 / scale;
			vec.X *= mult;
			vec.Y *= mult;
			vec.Z *= mult;
			vec.W *= mult;
			return vec;
		}

		/// <summary>
		/// Compares two instances for equality.
		/// </summary>
		/// <param name="left">The first instance.</param>
		/// <param name="right">The second instance.</param>
		/// <returns>True, if left equals right; false otherwise.</returns>
		public static bool operator ==(Vector4 left, Vector4 right)
		{
			return left.Equals(right);
		}

		/// <summary>
		/// Compares two instances for inequality.
		/// </summary>
		/// <param name="left">The first instance.</param>
		/// <param name="right">The second instance.</param>
		/// <returns>True, if left does not equa lright; false otherwise.</returns>
		public static bool operator !=(Vector4 left, Vector4 right)
		{
			return !left.Equals(right);
		}

		#endregion Operators

		#region Overrides

		#region public override string ToString()

		/// <summary>
		/// Returns a System.String that represents the current Vector4d.
		/// </summary>
		/// <returns></returns>
		public override string ToString()
		{
			return String.Format("{0}, {1}, {2}, {3}", X, Y, Z, W);
		}

		/// <summary>
		/// Returns a System.String that represents the current Vector4d, formatting each element with format.
		/// </summary>
		/// <param name="format"></param>
		/// <returns></returns>
		public string ToString(string format = "")
		{
			return X.ToString(format) + ", " + Y.ToString(format) + ", " + Z.ToString(format) + ", " + W.ToString(format);
		}

		#endregion public override string ToString()

		#region public override int GetHashCode()

		/// <summary>
		/// Returns the hashcode for this instance.
		/// </summary>
		/// <returns>A System.Int32 containing the unique hashcode for this instance.</returns>
		public override int GetHashCode()
		{
			return new { X, Y, Z, W }.GetHashCode();
		}

		public static ulong GetLongHashCode(double data, ulong hash = 14695981039346656037)
		{
			return ComputeHash(BitConverter.GetBytes(data), hash);
		}

		// FNV-1a (64-bit) non-cryptographic hash function.
		// Adapted from: http://github.com/jakedouglas/fnv-java
		public static ulong ComputeHash(byte[] bytes, ulong hash = 14695981039346656037)
		{
			const ulong fnv64Prime = 0x100000001b3;

			for (var i = 0; i < bytes.Length; i++)
			{
				hash = hash ^ bytes[i];
				hash *= fnv64Prime;
			}

			return hash;
		}

		/// <summary>
		/// return a 64 bit hash code proposed by Jon Skeet
		// http://stackoverflow.com/questions/8094867/good-gethashcode-override-for-list-of-foo-objects-respecting-the-order
		/// </summary>
		/// <returns></returns>
		public ulong GetLongHashCode(ulong hash = 14695981039346656037)
		{
			hash = GetLongHashCode(X, hash);
			hash = GetLongHashCode(Y, hash);
			hash = GetLongHashCode(Z, hash);
			hash = GetLongHashCode(W, hash);

			return hash;
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
			if (!(obj is Vector4))
				return false;

			return this.Equals((Vector4)obj);
		}

		/// <summary>
		/// Indicates whether this instance and a specified object are equal within an error range.
		/// </summary>
		/// <param name="OtherVector"></param>
		/// <param name="ErrorValue"></param>
		/// <returns>True if the instances are equal; false otherwise.</returns>
		public bool Equals(Vector4 OtherVector, double ErrorValue)
		{
			if ((X < OtherVector.X + ErrorValue && X > OtherVector.X - ErrorValue) &&
				(Y < OtherVector.Y + ErrorValue && Y > OtherVector.Y - ErrorValue) &&
				(Z < OtherVector.Z + ErrorValue && Z > OtherVector.Z - ErrorValue) &&
				(W < OtherVector.W + ErrorValue && W > OtherVector.W - ErrorValue))
			{
				return true;
			}

			return false;
		}

		#endregion public override bool Equals(object obj)

		#endregion Overrides

		#endregion Public Members

		#region IEquatable<Vector4d> Members

		/// <summary>Indicates whether the current vector is equal to another vector.</summary>
		/// <param name="other">A vector to compare with this vector.</param>
		/// <returns>true if the current vector is equal to the vector parameter; otherwise, false.</returns>
		public bool Equals(Vector4 other)
		{
			return
				X == other.X &&
				Y == other.Y &&
				Z == other.Z &&
				W == other.W;
		}

		#endregion IEquatable<Vector4d> Members
	}
}