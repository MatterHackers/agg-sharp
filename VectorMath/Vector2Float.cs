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
using Newtonsoft.Json;

namespace MatterHackers.VectorMath
{
	/// <summary>Represents a 2D vector using two double-precision floating-point numbers.</summary>
	[JsonObject]
	[Serializable]
	[StructLayout(LayoutKind.Sequential)]
	public struct Vector2Float : IEquatable<Vector2Float> , IConvertible
	{
		#region Fields

		/// <summary>The X coordinate of this instance.</summary>
		public float X;

		/// <summary>The Y coordinate of this instance.</summary>
		public float Y;

		/// <summary>
		/// Defines a unit-length Vector2d that points towards the X-axis.
		/// </summary>
		public static Vector2Float UnitX = new Vector2Float(1, 0);

		/// <summary>
		/// Defines a unit-length Vector2d that points towards the Y-axis.
		/// </summary>
		public static Vector2Float UnitY = new Vector2Float(0, 1);

		/// <summary>
		/// Defines a zero-length Vector2d.
		/// </summary>
		public static Vector2Float Zero = new Vector2Float(0, 0);

		/// <summary>
		/// Defines an instance with all components set to 1.
		/// </summary>
		public static readonly Vector2Float One = new Vector2Float(1, 1);

		/// <summary>
		/// Defines the size of the Vector2d struct in bytes.
		/// </summary>
		public static readonly int SizeInBytes = Marshal.SizeOf(new Vector2Float());

		#endregion Fields

		#region Constructors

		/// <summary>Constructs left vector with the given coordinates.</summary>
		/// <param name="x">The X coordinate.</param>
		/// <param name="y">The Y coordinate.</param>
		public Vector2Float(double x, double y)
		{
			this.X = (float)x;
			this.Y = (float)y;
		}

		public Vector2Float(Vector3 vector)
		{
			this.X = (float)vector.X;
			this.Y = (float)vector.Y;
		}

		public Vector2Float(Vector2 vector)
		{
			this.X = (float)vector.X;
			this.Y = (float)vector.Y;
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

					default:
						throw new Exception();
				}
			}
		}

		/// <summary>
		/// Get the delta angle from start to end relative to this
		/// </summary>
		/// <param name="startPosition"></param>
		/// <param name="endPosition"></param>
		public double GetDeltaAngle(Vector2Float startPosition, Vector2Float endPosition)
		{
			startPosition -= this;
			var startAngle = Math.Atan2(startPosition.Y, startPosition.X);
			startAngle = startAngle < 0 ? startAngle + MathHelper.Tau : startAngle;

			endPosition -= this;
			var endAngle = Math.Atan2(endPosition.Y, endPosition.X);
			endAngle = endAngle < 0 ? endAngle + MathHelper.Tau : endAngle;

			return endAngle - startAngle;
		}

		#endregion Properties

		#region Public Members

		#region Instance

		#region public double Length

		/// <summary>
		/// Gets the length (magnitude) of the vector.
		/// </summary>
		/// <seealso cref="LengthSquared"/>
		public float Length
		{
			get
			{
				return (float)System.Math.Sqrt(X * X + Y * Y);
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
				return X * X + Y * Y;
			}
		}

		#endregion public double LengthSquared

		public void Rotate(double radians)
		{
			this = Vector2Float.Rotate(this, radians);
		}

		public double GetAngle()
		{
			return System.Math.Atan2(Y, X);
		}

		public double GetAngle0To2PI()
		{
			return MathHelper.Range0ToTau(GetAngle());
		}

		#region public Vector2d PerpendicularRight

		/// <summary>
		/// Gets the perpendicular vector on the right side of this vector.
		/// </summary>
		public Vector2Float GetPerpendicularRight()
		{
			return new Vector2Float(Y, -X);
		}

		#endregion public Vector2d PerpendicularRight

		#region public Vector2d PerpendicularLeft

		/// <summary>
		/// Gets the perpendicular vector on the left side of this vector.
		/// </summary>
		public Vector2Float GetPerpendicularLeft()
		{
			return new Vector2Float(-Y, X);
		}

		#endregion public Vector2d PerpendicularLeft

		#region public void Normalize()

		/// <summary>
		/// Returns a normalized Vector of this.
		/// </summary>
		/// <returns></returns>
		public Vector2Float GetNormal()
		{
			Vector2Float temp = this;
			temp.Normalize();
			return temp;
		}

		/// <summary>
		/// Scales the Vector2 to unit length.
		/// </summary>
		public void Normalize()
		{
			float scale = 1.0f / Length;
			X *= scale;
			Y *= scale;
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
		public static Vector2Float Add(Vector2Float a, Vector2Float b)
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
		public static void Add(ref Vector2Float a, ref Vector2Float b, out Vector2Float result)
		{
			result = new Vector2Float(a.X + b.X, a.Y + b.Y);
		}

		#endregion Add

		#region Subtract

		/// <summary>
		/// Subtract one Vector from another
		/// </summary>
		/// <param name="a">First operand</param>
		/// <param name="b">Second operand</param>
		/// <returns>Result of subtraction</returns>
		public static Vector2Float Subtract(Vector2Float a, Vector2Float b)
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
		public static void Subtract(ref Vector2Float a, ref Vector2Float b, out Vector2Float result)
		{
			result = new Vector2Float(a.X - b.X, a.Y - b.Y);
		}

		#endregion Subtract

		#region Multiply

		/// <summary>
		/// Multiplies a vector by a scalar.
		/// </summary>
		/// <param name="vector">Left operand.</param>
		/// <param name="scale">Right operand.</param>
		/// <returns>Result of the operation.</returns>
		public static Vector2Float Multiply(Vector2Float vector, double scale)
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
		public static void Multiply(ref Vector2Float vector, double scale, out Vector2Float result)
		{
			result = new Vector2Float(vector.X * scale, vector.Y * scale);
		}

		/// <summary>
		/// Multiplies a vector by the components a vector (scale).
		/// </summary>
		/// <param name="vector">Left operand.</param>
		/// <param name="scale">Right operand.</param>
		/// <returns>Result of the operation.</returns>
		public static Vector2Float Multiply(Vector2Float vector, Vector2Float scale)
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
		public static void Multiply(ref Vector2Float vector, ref Vector2Float scale, out Vector2Float result)
		{
			result = new Vector2Float(vector.X * scale.X, vector.Y * scale.Y);
		}

		#endregion Multiply

		#region Divide

		/// <summary>
		/// Divides a vector by a scalar.
		/// </summary>
		/// <param name="vector">Left operand.</param>
		/// <param name="scale">Right operand.</param>
		/// <returns>Result of the operation.</returns>
		public static Vector2Float Divide(Vector2Float vector, double scale)
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
		public static void Divide(ref Vector2Float vector, double scale, out Vector2Float result)
		{
			Multiply(ref vector, 1 / scale, out result);
		}

		/// <summary>
		/// Divides a vector by the components of a vector (scale).
		/// </summary>
		/// <param name="vector">Left operand.</param>
		/// <param name="scale">Right operand.</param>
		/// <returns>Result of the operation.</returns>
		public static Vector2Float Divide(Vector2Float vector, Vector2Float scale)
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
		public static void Divide(ref Vector2Float vector, ref Vector2Float scale, out Vector2Float result)
		{
			result = new Vector2Float(vector.X / scale.X, vector.Y / scale.Y);
		}

		#endregion Divide

		#region Min

		/// <summary>
		/// Calculate the component-wise minimum of two vectors
		/// </summary>
		/// <param name="a">First operand</param>
		/// <param name="b">Second operand</param>
		/// <returns>The component-wise minimum</returns>
		public static Vector2Float Min(Vector2Float a, Vector2Float b)
		{
			a.X = a.X < b.X ? a.X : b.X;
			a.Y = a.Y < b.Y ? a.Y : b.Y;
			return a;
		}

		/// <summary>
		/// Calculate the component-wise minimum of two vectors
		/// </summary>
		/// <param name="a">First operand</param>
		/// <param name="b">Second operand</param>
		/// <param name="result">The component-wise minimum</param>
		public static void Min(ref Vector2Float a, ref Vector2Float b, out Vector2Float result)
		{
			result.X = a.X < b.X ? a.X : b.X;
			result.Y = a.Y < b.Y ? a.Y : b.Y;
		}

		#endregion Min

		#region Max

		/// <summary>
		/// Calculate the component-wise maximum of two vectors
		/// </summary>
		/// <param name="a">First operand</param>
		/// <param name="b">Second operand</param>
		/// <returns>The component-wise maximum</returns>
		public static Vector2Float Max(Vector2Float a, Vector2Float b)
		{
			a.X = a.X > b.X ? a.X : b.X;
			a.Y = a.Y > b.Y ? a.Y : b.Y;
			return a;
		}

		/// <summary>
		/// Calculate the component-wise maximum of two vectors
		/// </summary>
		/// <param name="a">First operand</param>
		/// <param name="b">Second operand</param>
		/// <param name="result">The component-wise maximum</param>
		public static void Max(ref Vector2Float a, ref Vector2Float b, out Vector2Float result)
		{
			result.X = a.X > b.X ? a.X : b.X;
			result.Y = a.Y > b.Y ? a.Y : b.Y;
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
		public static Vector2Float Clamp(Vector2Float vec, Vector2Float min, Vector2Float max)
		{
			vec.X = vec.X < min.X ? min.X : vec.X > max.X ? max.X : vec.X;
			vec.Y = vec.Y < min.Y ? min.Y : vec.Y > max.Y ? max.Y : vec.Y;
			return vec;
		}

		/// <summary>
		/// Clamp a vector to the given minimum and maximum vectors
		/// </summary>
		/// <param name="vec">Input vector</param>
		/// <param name="min">Minimum vector</param>
		/// <param name="max">Maximum vector</param>
		/// <param name="result">The clamped vector</param>
		public static void Clamp(ref Vector2Float vec, ref Vector2Float min, ref Vector2Float max, out Vector2Float result)
		{
			result.X = vec.X < min.X ? min.X : vec.X > max.X ? max.X : vec.X;
			result.Y = vec.Y < min.Y ? min.Y : vec.Y > max.Y ? max.Y : vec.Y;
		}

		#endregion Clamp

		#region Normalize

		/// <summary>
		/// Scale a vector to unit length
		/// </summary>
		/// <param name="vec">The input vector</param>
		/// <returns>The normalized vector</returns>
		public static Vector2Float Normalize(Vector2Float vec)
		{
			float scale = 1.0f / vec.Length;
			vec.X *= scale;
			vec.Y *= scale;
			return vec;
		}

		/// <summary>
		/// Scale a vector to unit length
		/// </summary>
		/// <param name="vec">The input vector</param>
		/// <param name="result">The normalized vector</param>
		public static void Normalize(ref Vector2Float vec, out Vector2Float result)
		{
			float scale = 1.0f / vec.Length;
			result.X = vec.X * scale;
			result.Y = vec.Y * scale;
		}

		#endregion Normalize

		#region Dot

		/// <summary>
		/// Calculate the dot (scalar) product of two vectors
		/// </summary>
		/// <param name="left">First operand</param>
		/// <param name="right">Second operand</param>
		/// <returns>The dot product of the two inputs</returns>
		public static double Dot(Vector2Float left, Vector2Float right)
		{
			return left.X * right.X + left.Y * right.Y;
		}

		/// <summary>
		/// Calculate the dot (scalar) product of two vectors
		/// </summary>
		/// <param name="left">First operand</param>
		/// <param name="right">Second operand</param>
		/// <param name="result">The dot product of the two inputs</param>
		public static void Dot(ref Vector2Float left, ref Vector2Float right, out double result)
		{
			result = left.X * right.X + left.Y * right.Y;
		}

		#endregion Dot

		#region Cross

		/// <summary>
		/// Calculate the cross product of two vectors
		/// </summary>
		/// <param name="left">First operand</param>
		/// <param name="right">Second operand</param>
		/// <returns>The cross product of the two inputs</returns>
		public static double Cross(Vector2Float left, Vector2Float right)
		{
			return left.X * right.Y - left.Y * right.X;
		}

		#endregion Cross

		#region Rotate

		public static Vector2Float Rotate(Vector2Float toRotate, double radians)
		{
			Vector2Float temp;
			Rotate(ref toRotate, radians, out temp);
			return temp;
		}

		public static void Rotate(ref Vector2Float input, double radians, out Vector2Float output)
		{
			float Cos, Sin;

			Cos = (float)System.Math.Cos(radians);
			Sin = (float)System.Math.Sin(radians);

			output.X = input.X * Cos - input.Y * Sin;
			output.Y = input.Y * Cos + input.X * Sin;
		}

		#endregion Rotate

		#region Lerp

		/// <summary>
		/// Returns a new Vector that is the linear blend of the 2 given Vectors
		/// </summary>
		/// <param name="a">First input vector</param>
		/// <param name="b">Second input vector</param>
		/// <param name="blend">The blend factor. a when blend=0, b when blend=1.</param>
		/// <returns>a when blend=0, b when blend=1, and a linear combination otherwise</returns>
		public static Vector2Float Lerp(Vector2Float a, Vector2Float b, float blend)
		{
			a.X = blend * (b.X - a.X) + a.X;
			a.Y = blend * (b.Y - a.Y) + a.Y;
			return a;
		}

		/// <summary>
		/// Returns a new Vector that is the linear blend of the 2 given Vectors
		/// </summary>
		/// <param name="a">First input vector</param>
		/// <param name="b">Second input vector</param>
		/// <param name="blend">The blend factor. a when blend=0, b when blend=1.</param>
		/// <param name="result">a when blend=0, b when blend=1, and a linear combination otherwise</param>
		public static void Lerp(ref Vector2Float a, ref Vector2Float b, float blend, out Vector2Float result)
		{
			result.X = blend * (b.X - a.X) + a.X;
			result.Y = blend * (b.Y - a.Y) + a.Y;
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
		public static Vector2Float BaryCentric(Vector2Float a, Vector2Float b, Vector2Float c, float u, float v)
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
		public static void BaryCentric(ref Vector2Float a, ref Vector2Float b, ref Vector2Float c, double u, double v, out Vector2Float result)
		{
			result = a; // copy

			Vector2Float temp = b; // copy
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

		/// <summary>
		/// Transforms a vector by a quaternion rotation.
		/// </summary>
		/// <param name="vec">The vector to transform.</param>
		/// <param name="quat">The quaternion to rotate the vector by.</param>
		/// <returns>The result of the operation.</returns>
		public static Vector2Float Transform(Vector2Float vec, Quaternion quat)
		{
			Vector2Float result;
			Transform(ref vec, ref quat, out result);
			return result;
		}

		/// <summary>
		/// Transforms a vector by a quaternion rotation.
		/// </summary>
		/// <param name="vec">The vector to transform.</param>
		/// <param name="quat">The quaternion to rotate the vector by.</param>
		/// <param name="result">The result of the operation.</param>
		public static void Transform(ref Vector2Float vec, ref Quaternion quat, out Vector2Float result)
		{
			Quaternion v = new Quaternion(vec.X, vec.Y, 0, 0), i, t;
			Quaternion.Invert(ref quat, out i);
			Quaternion.Multiply(ref quat, ref v, out t);
			Quaternion.Multiply(ref t, ref i, out v);

			result = new Vector2Float(v.X, v.Y);
		}

		#endregion Transform

		#region ComponentMin

		/// <summary>
		/// Calculate the component-wise minimum of two vectors
		/// </summary>
		/// <param name="a">First operand</param>
		/// <param name="b">Second operand</param>
		/// <returns>The component-wise minimum</returns>
		public static Vector2Float ComponentMin(Vector2Float a, Vector2Float b)
		{
			a.X = a.X < b.X ? a.X : b.X;
			a.Y = a.Y < b.Y ? a.Y : b.Y;
			return a;
		}

		/// <summary>
		/// Calculate the component-wise minimum of two vectors
		/// </summary>
		/// <param name="a">First operand</param>
		/// <param name="b">Second operand</param>
		/// <param name="result">The component-wise minimum</param>
		public static void ComponentMin(ref Vector2Float a, ref Vector2Float b, out Vector2Float result)
		{
			result.X = a.X < b.X ? a.X : b.X;
			result.Y = a.Y < b.Y ? a.Y : b.Y;
		}

		#endregion ComponentMin

		#region ComponentMax

		/// <summary>
		/// Calculate the component-wise maximum of two vectors
		/// </summary>
		/// <param name="a">First operand</param>
		/// <param name="b">Second operand</param>
		/// <returns>The component-wise maximum</returns>
		public static Vector2Float ComponentMax(Vector2Float a, Vector2Float b)
		{
			a.X = a.X > b.X ? a.X : b.X;
			a.Y = a.Y > b.Y ? a.Y : b.Y;
			return a;
		}

		/// <summary>
		/// Calculate the component-wise maximum of two vectors
		/// </summary>
		/// <param name="a">First operand</param>
		/// <param name="b">Second operand</param>
		/// <param name="result">The component-wise maximum</param>
		public static void ComponentMax(ref Vector2Float a, ref Vector2Float b, out Vector2Float result)
		{
			result.X = a.X > b.X ? a.X : b.X;
			result.Y = a.Y > b.Y ? a.Y : b.Y;
		}

		#endregion ComponentMax

		#endregion Static

		#region Operators

		/// <summary>
		/// Adds two instances.
		/// </summary>
		/// <param name="left">The left instance.</param>
		/// <param name="right">The right instance.</param>
		/// <returns>The result of the operation.</returns>
		public static Vector2Float operator +(Vector2Float left, Vector2Float right)
		{
			left.X += right.X;
			left.Y += right.Y;
			return left;
		}

		/// <summary>
		/// Subtracts two instances.
		/// </summary>
		/// <param name="left">The left instance.</param>
		/// <param name="right">The right instance.</param>
		/// <returns>The result of the operation.</returns>
		public static Vector2Float operator -(Vector2Float left, Vector2Float right)
		{
			left.X -= right.X;
			left.Y -= right.Y;
			return left;
		}

		/// <summary>
		/// Negates an instance.
		/// </summary>
		/// <param name="vec">The instance.</param>
		/// <returns>The result of the operation.</returns>
		public static Vector2Float operator -(Vector2Float vec)
		{
			vec.X = -vec.X;
			vec.Y = -vec.Y;
			return vec;
		}

		/// <summary>
		/// Multiplies an instance by a scalar.
		/// </summary>
		/// <param name="vec">The instance.</param>
		/// <param name="f">The scalar.</param>
		/// <returns>The result of the operation.</returns>
		public static Vector2Float operator *(Vector2Float vec, float f)
		{
			vec.X *= f;
			vec.Y *= f;
			return vec;
		}

		/// <summary>
		/// Multiply an instance by a scalar.
		/// </summary>
		/// <param name="f">The scalar.</param>
		/// <param name="vec">The instance.</param>
		/// <returns>The result of the operation.</returns>
		public static Vector2Float operator *(float f, Vector2Float vec)
		{
			vec.X *= f;
			vec.Y *= f;
			return vec;
		}

		/// <summary>
		/// Divides an instance by a scalar.
		/// </summary>
		/// <param name="vec">The instance.</param>
		/// <param name="f">The scalar.</param>
		/// <returns>The result of the operation.</returns>
		public static Vector2Float operator /(Vector2Float vec, float f)
		{
			float mult = 1.0f / f;
			vec.X *= mult;
			vec.Y *= mult;
			return vec;
		}

		/// <summary>
		/// Divides a scaler by an instance components wise.
		/// </summary>
		/// <param name="vec">The scalar.</param>
		/// <param name="f">The instance.</param>
		/// <returns>The result of the operation.</returns>
		public static Vector2Float operator /(float f, Vector2Float vec)
		{
			vec.X = f / vec.X;
			vec.Y = f / vec.Y;
			return vec;
		}

		/// <summary>
		/// Compares two instances for equality.
		/// </summary>
		/// <param name="left">The left instance.</param>
		/// <param name="right">The right instance.</param>
		/// <returns>True, if both instances are equal; false otherwise.</returns>
		public static bool operator ==(Vector2Float left, Vector2Float right)
		{
			return left.Equals(right);
		}

		/// <summary>
		/// Compares two instances for ienquality.
		/// </summary>
		/// <param name="left">The left instance.</param>
		/// <param name="right">The right instance.</param>
		/// <returns>True, if the instances are not equal; false otherwise.</returns>
		public static bool operator !=(Vector2Float left, Vector2Float right)
		{
			return !left.Equals(right);
		}

		#endregion Operators

		#region Overrides

		#region public override string ToString()

		/// <summary>
		/// Returns a System.String that represents the current instance.
		/// </summary>
		/// <returns></returns>
		public override string ToString()
		{
			return String.Format("({0}, {1})", X, Y);
		}

		#endregion public override string ToString()

		#region public override int GetHashCode()

		/// <summary>
		/// Returns the hashcode for this instance.
		/// </summary>
		/// <returns>A System.Int32 containing the unique hashcode for this instance.</returns>
		public override int GetHashCode()
		{
			return new { X, Y }.GetHashCode();
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
			if (!(obj is Vector2Float))
				return false;

			return this.Equals((Vector2Float)obj);
		}

		#endregion public override bool Equals(object obj)

		#endregion Overrides

		#endregion Public Members

		#region IEquatable<Vector2Floatd> Members

		/// <summary>Indicates whether the current vector is equal to another vector.</summary>
		/// <param name="other">A vector to compare with this vector.</param>
		/// <returns>true if the current vector is equal to the vector parameter; otherwise, false.</returns>
		public bool Equals(Vector2Float other)
		{
			return
				X == other.X &&
				Y == other.Y;
		}

		/// <summary>Indicates whether the current vector is equal to another vector.</summary>
		/// <param name="other">A vector to compare with this vector.</param>
		/// <returns>true if the current vector is equal to the vector parameter; otherwise, false.</returns>
		public bool Equals(Vector2Float other, double errorRange)
		{
			if ((X < other.X + errorRange && X > other.X - errorRange) &&
				(Y < other.Y + errorRange && Y > other.Y - errorRange))
			{
				return true;
			}

			return false;
		}

		#endregion IEquatable<Vector2Floatd> Members

		#region IConvertable
		public TypeCode GetTypeCode()
		{
			throw new NotImplementedException();
		}

		public bool ToBoolean(IFormatProvider provider)
		{
			throw new NotImplementedException();
		}

		public char ToChar(IFormatProvider provider)
		{
			throw new NotImplementedException();
		}

		public sbyte ToSByte(IFormatProvider provider)
		{
			throw new NotImplementedException();
		}

		public byte ToByte(IFormatProvider provider)
		{
			throw new NotImplementedException();
		}

		public short ToInt16(IFormatProvider provider)
		{
			throw new NotImplementedException();
		}

		public ushort ToUInt16(IFormatProvider provider)
		{
			throw new NotImplementedException();
		}

		public int ToInt32(IFormatProvider provider)
		{
			throw new NotImplementedException();
		}

		public uint ToUInt32(IFormatProvider provider)
		{
			throw new NotImplementedException();
		}

		public long ToInt64(IFormatProvider provider)
		{
			throw new NotImplementedException();
		}

		public ulong ToUInt64(IFormatProvider provider)
		{
			throw new NotImplementedException();
		}

		public float ToSingle(IFormatProvider provider)
		{
			throw new NotImplementedException();
		}

		public double ToDouble(IFormatProvider provider)
		{
			throw new NotImplementedException();
		}

		public decimal ToDecimal(IFormatProvider provider)
		{
			throw new NotImplementedException();
		}

		public DateTime ToDateTime(IFormatProvider provider)
		{
			throw new NotImplementedException();
		}

		public string ToString(IFormatProvider provider)
		{
			throw new NotImplementedException();
		}

		public object ToType(Type conversionType, IFormatProvider provider)
		{
			throw new NotImplementedException();
		}

		#endregion IConvertable
	}
}