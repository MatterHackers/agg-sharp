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
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using Newtonsoft.Json;

namespace MatterHackers.VectorMath
{
	/// <summary>Represents a 2D vector using two double-precision floating-point numbers.</summary>
	[JsonObject]
	[Serializable]
	[StructLayout(LayoutKind.Sequential)]
	[TypeConverter(typeof(Vector2Converter))]
	public struct Vector2 : IEquatable<Vector2>
	{
		/// <summary>
		/// Defines an instance with all components set to positive infinity.
		/// </summary>
		public static readonly Vector2 PositiveInfinity = new Vector2(double.PositiveInfinity, double.PositiveInfinity);

		/// <summary>
		/// Defines an instance with all components set to negative infinity.
		/// </summary>
		public static readonly Vector2 NegativeInfinity = new Vector2(double.NegativeInfinity, double.NegativeInfinity);

		#region Fields

		/// <summary>The X coordinate of this instance.</summary>
		public double X;

		/// <summary>The Y coordinate of this instance.</summary>
		public double Y;

		/// <summary>
		/// Defines a unit-length Vector2d that points towards the X-axis.
		/// </summary>
		public static Vector2 UnitX = new Vector2(1, 0);

		/// <summary>
		/// Defines a unit-length Vector2d that points towards the Y-axis.
		/// </summary>
		public static Vector2 UnitY = new Vector2(0, 1);

		/// <summary>
		/// Defines a zero-length Vector2d.
		/// </summary>
		public static Vector2 Zero = new Vector2(0, 0);

		/// <summary>
		/// Defines an instance with all components set to 1.
		/// </summary>
		public static readonly Vector2 One = new Vector2(1, 1);

		/// <summary>
		/// Defines the size of the Vector2d struct in bytes.
		/// </summary>
		public static readonly int SizeInBytes = Marshal.SizeOf(new Vector2());

		#endregion Fields

		#region Constructors

		/// <summary>Constructs left vector with the given coordinates.</summary>
		/// <param name="x">The X coordinate.</param>
		/// <param name="y">The Y coordinate.</param>
		public Vector2(double x, double y)
		{
			this.X = x;
			this.Y = y;
		}

		public Vector2(Vector3 vector)
		{
			this.X = vector.X;
			this.Y = vector.Y;
		}

		public Vector2(Vector3Float vector)
		{
			this.X = vector.X;
			this.Y = vector.Y;
		}

		#endregion Constructors

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
		/// return a 64 bit hash code proposed by Jon Skeet
		// http://stackoverflow.com/questions/8094867/good-gethashcode-override-for-list-of-foo-objects-respecting-the-order
		/// </summary>
		/// <returns></returns>
		public ulong GetLongHashCode(ulong hash = 14695981039346656037)
		{
			hash = Vector4.GetLongHashCode(X, hash);
			hash = Vector4.GetLongHashCode(Y, hash);

			return hash;
		}

		/// <summary>
		/// Get the delta angle from start to end relative to this
		/// </summary>
		/// <param name="startPosition"></param>
		/// <param name="endPosition"></param>
		public double GetDeltaAngle(Vector2 startPosition, Vector2 endPosition)
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
		[JsonIgnore]
		public double Length
		{
			get
			{
				return System.Math.Sqrt(X * X + Y * Y);
			}
		}

		public double Distance(Vector2 p)
		{
			return (this - p).Length;
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
		[JsonIgnore]
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
			this = Vector2.Rotate(this, radians);
		}

		public Vector2 GetRotated(double radians)
		{
			return Vector2.Rotate(this, radians);
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
		public Vector2 GetPerpendicularRight()
		{
			return new Vector2(Y, -X);
		}

		#endregion public Vector2d PerpendicularRight

		#region public Vector2d PerpendicularLeft

		/// <summary>
		/// Gets the perpendicular vector on the left side of this vector.
		/// </summary>
		public Vector2 GetPerpendicularLeft()
		{
			return new Vector2(-Y, X);
		}

		#endregion public Vector2d PerpendicularLeft

		#region public void Normalize()

		/// <summary>
		/// Returns a normalized Vector of this.
		/// </summary>
		/// <returns></returns>
		public Vector2 GetNormal()
		{
			Vector2 temp = this;
			temp.Normalize();
			return temp;
		}

		/// <summary>
		/// Scales the Vector2 to unit length.
		/// </summary>
		public void Normalize()
		{
			double scale = 1.0 / Length;
			X *= scale;
			Y *= scale;
		}

		#endregion public void Normalize()

		public bool IsValid()
		{
			if (double.IsNaN(X) || double.IsInfinity(X)
				|| double.IsNaN(Y) || double.IsInfinity(Y))
			{
				return false;
			}

			return true;
		}

		#endregion Instance

		#region Static

		#region Add

		/// <summary>
		/// Adds two vectors.
		/// </summary>
		/// <param name="a">Left operand.</param>
		/// <param name="b">Right operand.</param>
		/// <returns>Result of operation.</returns>
		public static Vector2 Add(Vector2 a, Vector2 b)
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
		public static void Add(ref Vector2 a, ref Vector2 b, out Vector2 result)
		{
			result = new Vector2(a.X + b.X, a.Y + b.Y);
		}

		#endregion Add

		#region Subtract

		/// <summary>
		/// Subtract one Vector from another
		/// </summary>
		/// <param name="a">First operand</param>
		/// <param name="b">Second operand</param>
		/// <returns>Result of subtraction</returns>
		public static Vector2 Subtract(Vector2 a, Vector2 b)
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
		public static void Subtract(ref Vector2 a, ref Vector2 b, out Vector2 result)
		{
			result = new Vector2(a.X - b.X, a.Y - b.Y);
		}

		#endregion Subtract

		#region Multiply

		/// <summary>
		/// Multiplies a vector by a scalar.
		/// </summary>
		/// <param name="vector">Left operand.</param>
		/// <param name="scale">Right operand.</param>
		/// <returns>Result of the operation.</returns>
		public static Vector2 Multiply(Vector2 vector, double scale)
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
		public static void Multiply(ref Vector2 vector, double scale, out Vector2 result)
		{
			result = new Vector2(vector.X * scale, vector.Y * scale);
		}

		/// <summary>
		/// Multiplies a vector by the components a vector (scale).
		/// </summary>
		/// <param name="vector">Left operand.</param>
		/// <param name="scale">Right operand.</param>
		/// <returns>Result of the operation.</returns>
		public static Vector2 Multiply(Vector2 vector, Vector2 scale)
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
		public static void Multiply(ref Vector2 vector, ref Vector2 scale, out Vector2 result)
		{
			result = new Vector2(vector.X * scale.X, vector.Y * scale.Y);
		}

		#endregion Multiply

		#region Divide

		/// <summary>
		/// Divides a vector by a scalar.
		/// </summary>
		/// <param name="vector">Left operand.</param>
		/// <param name="scale">Right operand.</param>
		/// <returns>Result of the operation.</returns>
		public static Vector2 Divide(Vector2 vector, double scale)
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
		public static void Divide(ref Vector2 vector, double scale, out Vector2 result)
		{
			Multiply(ref vector, 1 / scale, out result);
		}

		/// <summary>
		/// Divides a vector by the components of a vector (scale).
		/// </summary>
		/// <param name="vector">Left operand.</param>
		/// <param name="scale">Right operand.</param>
		/// <returns>Result of the operation.</returns>
		public static Vector2 Divide(Vector2 vector, Vector2 scale)
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
		public static void Divide(ref Vector2 vector, ref Vector2 scale, out Vector2 result)
		{
			result = new Vector2(vector.X / scale.X, vector.Y / scale.Y);
		}

		#endregion Divide

		#region Min

		/// <summary>
		/// Calculate the component-wise minimum of two vectors
		/// </summary>
		/// <param name="a">First operand</param>
		/// <param name="b">Second operand</param>
		/// <returns>The component-wise minimum</returns>
		public static Vector2 Min(Vector2 a, Vector2 b)
		{
			a.X = a.X < b.X ? a.X : b.X;
			a.Y = a.Y < b.Y ? a.Y : b.Y;
			return a;
		}

		public static Vector2 Parse(string s)
		{
			var result = Vector2.Zero;
			var values = s.Split(',').Select(sValue =>
			{
				double number = 0;
				if (double.TryParse(sValue, out number))
				{
					return double.Parse(sValue);
				}
				return 0;
			}).ToArray();

			for (int i = 0; i < Math.Min(2, values.Length); i++)
			{
				result[i] = values[i];
			}

			return result;
		}

		/// <summary>
		/// Calculate the component-wise minimum of two vectors
		/// </summary>
		/// <param name="a">First operand</param>
		/// <param name="b">Second operand</param>
		/// <param name="result">The component-wise minimum</param>
		public static void Min(ref Vector2 a, ref Vector2 b, out Vector2 result)
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
		public static Vector2 Max(Vector2 a, Vector2 b)
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
		public static void Max(ref Vector2 a, ref Vector2 b, out Vector2 result)
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
		public static Vector2 Clamp(Vector2 vec, Vector2 min, Vector2 max)
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
		public static void Clamp(ref Vector2 vec, ref Vector2 min, ref Vector2 max, out Vector2 result)
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
		public static Vector2 Normalize(Vector2 vec)
		{
			double scale = 1.0 / vec.Length;
			vec.X *= scale;
			vec.Y *= scale;
			return vec;
		}

		/// <summary>
		/// Scale a vector to unit length
		/// </summary>
		/// <param name="vec">The input vector</param>
		/// <param name="result">The normalized vector</param>
		public static void Normalize(ref Vector2 vec, out Vector2 result)
		{
			double scale = 1.0 / vec.Length;
			result.X = vec.X * scale;
			result.Y = vec.Y * scale;
		}

		#endregion Normalize

		/// <summary>
		/// Get the distance from a point to a line. Calculate the distance to both ends and to the
		/// line legment and return the smallest value.
		/// </summary>
		/// <param name="point">The point to consider</param>
		/// <param name="lineStart">The start of the line segment to consider</param>
		/// <param name="lineEnd">The end of the line segment to consider</param>
		/// <returns>The distance from the point to the line</returns>
		public static double DistancePointToLine(Vector2 point, Vector2 lineStart, Vector2 lineEnd)
		{
			if (point == lineStart || point == lineEnd)
			{
				return 0;
			}

			if (lineStart == lineEnd)
			{
				return (point - lineStart).Length;
			}

			var lineDelta = lineEnd - lineStart;
			var lineLength = lineDelta.Length;
			var lineNormal = lineDelta.GetNormal();

			var deltaFromStart = point - lineStart;
			var distanceFromStart = lineNormal.Dot(deltaFromStart);
			// if we are within the cone of the line
			if (distanceFromStart >= 0 && distanceFromStart <= lineLength)
            {
				var perpendicularNormal = lineNormal.GetPerpendicularLeft();
				var distanceToLine = perpendicularNormal.Dot(deltaFromStart);

				return Math.Abs(distanceToLine);
			}

			if (distanceFromStart < 0)
            {
				return deltaFromStart.Length;
			}

			var deltaFromEnd = point - lineEnd;
			return deltaFromEnd.Length;
		}

		#region Dot

		/// <summary>
		/// Calculate the dot (scalar) product of two vectors
		/// </summary>
		/// <param name="left">First operand</param>
		/// <param name="right">Second operand</param>
		/// <returns>The dot product of the two inputs</returns>
		public static double Dot(Vector2 left, Vector2 right)
		{
			return left.X * right.X + left.Y * right.Y;
		}

		public double Dot(Vector2 b)
        {
			return X * b.X + Y * b.Y;
        }

		/// <summary>
		/// Calculate the dot (scalar) product of two vectors
		/// </summary>
		/// <param name="left">First operand</param>
		/// <param name="right">Second operand</param>
		/// <param name="result">The dot product of the two inputs</param>
		public static void Dot(ref Vector2 left, ref Vector2 right, out double result)
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
		public static double Cross(Vector2 left, Vector2 right)
		{
			return left.X * right.Y - left.Y * right.X;
		}

		public double Cross(Vector2 right)
		{
			return this.X * right.Y - this.Y * right.X;
		}

		#endregion Cross

		#region Rotate

		public static Vector2 Rotate(Vector2 toRotate, double radians)
		{
			Vector2 temp;
			Rotate(ref toRotate, radians, out temp);
			return temp;
		}

		public static void Rotate(ref Vector2 input, double radians, out Vector2 output)
		{
			double Cos, Sin;

			Cos = (double)System.Math.Cos(radians);
			Sin = (double)System.Math.Sin(radians);

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
		public static Vector2 Lerp(Vector2 a, Vector2 b, double blend)
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
		public static void Lerp(ref Vector2 a, ref Vector2 b, double blend, out Vector2 result)
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
		public static Vector2 BaryCentric(Vector2 a, Vector2 b, Vector2 c, double u, double v)
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
		public static void BaryCentric(ref Vector2 a, ref Vector2 b, ref Vector2 c, double u, double v, out Vector2 result)
		{
			result = a; // copy

			Vector2 temp = b; // copy
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
		public static Vector2 Transform(Vector2 vec, Quaternion quat)
		{
			Vector2 result;
			Transform(ref vec, ref quat, out result);
			return result;
		}

		/// <summary>
		/// Transforms a vector by a quaternion rotation.
		/// </summary>
		/// <param name="vec">The vector to transform.</param>
		/// <param name="quat">The quaternion to rotate the vector by.</param>
		/// <param name="result">The result of the operation.</param>
		public static void Transform(ref Vector2 vec, ref Quaternion quat, out Vector2 result)
		{
			Quaternion v = new Quaternion(vec.X, vec.Y, 0, 0), i, t;
			Quaternion.Invert(ref quat, out i);
			Quaternion.Multiply(ref quat, ref v, out t);
			Quaternion.Multiply(ref t, ref i, out v);

			result = new Vector2(v.X, v.Y);
		}

		#endregion Transform

		#region ComponentMin

		/// <summary>
		/// Calculate the component-wise minimum of two vectors
		/// </summary>
		/// <param name="a">First operand</param>
		/// <param name="b">Second operand</param>
		/// <returns>The component-wise minimum</returns>
		public static Vector2 ComponentMin(Vector2 a, Vector2 b)
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
		public static void ComponentMin(ref Vector2 a, ref Vector2 b, out Vector2 result)
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
		public static Vector2 ComponentMax(Vector2 a, Vector2 b)
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
		public static void ComponentMax(ref Vector2 a, ref Vector2 b, out Vector2 result)
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
		public static Vector2 operator +(Vector2 left, Vector2 right)
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
		public static Vector2 operator -(Vector2 left, Vector2 right)
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
		public static Vector2 operator -(Vector2 vec)
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
		public static Vector2 operator *(Vector2 vec, double f)
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
		public static Vector2 operator *(double f, Vector2 vec)
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
		public static Vector2 operator /(Vector2 vec, double f)
		{
			double mult = 1.0 / f;
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
		public static Vector2 operator /(double f, Vector2 vec)
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
		public static bool operator ==(Vector2 left, Vector2 right)
		{
			return left.Equals(right);
		}

		/// <summary>
		/// Compares two instances for ienquality.
		/// </summary>
		/// <param name="left">The left instance.</param>
		/// <param name="right">The right instance.</param>
		/// <returns>True, if the instances are not equal; false otherwise.</returns>
		public static bool operator !=(Vector2 left, Vector2 right)
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
			if (!(obj is Vector2))
				return false;

			return this.Equals((Vector2)obj);
		}

		#endregion public override bool Equals(object obj)

		#endregion Overrides

		#endregion Public Members

		#region IEquatable<Vector2d> Members

		/// <summary>Indicates whether the current vector is equal to another vector.</summary>
		/// <param name="other">A vector to compare with this vector.</param>
		/// <returns>true if the current vector is equal to the vector parameter; otherwise, false.</returns>
		public bool Equals(Vector2 other)
		{
			return
				X == other.X &&
				Y == other.Y;
		}

		/// <summary>Indicates whether the current vector is equal to another vector.</summary>
		/// <param name="other">A vector to compare with this vector.</param>
		/// <returns>true if the current vector is equal to the vector parameter; otherwise, false.</returns>
		public bool Equals(Vector2 other, double errorRange)
		{
			if ((X < other.X + errorRange && X > other.X - errorRange) &&
				(Y < other.Y + errorRange && Y > other.Y - errorRange))
			{
				return true;
			}

			return false;
		}

		#endregion IEquatable<Vector2d> Members
	}

	public static class Vector2Ex
	{
		public static double PolygonLength(this List<Vector2> polygon, bool isClosed = true)
		{
			var length = 0.0;
			if (polygon.Count > 1)
			{
				var previousPoint = polygon[0];
				if (isClosed)
				{
					previousPoint = polygon[polygon.Count - 1];
				}

				for (int i = isClosed ? 0 : 1; i < polygon.Count; i++)
				{
					var currentPoint = polygon[i];
					length += (previousPoint - currentPoint).Length;
					previousPoint = currentPoint;
				}
			}

			return length;
		}

		public static double LengthTo(this List<Vector2> polygon, int index, bool isClosed = true)
		{
			var length = 0.0;
			index = Math.Max(0, Math.Min(polygon.Count - 1, index));
			for (int i = 1; i <= index; i++)
			{
				length += (polygon[i] - polygon[i - 1]).Length;
			}

			return length;
		}

		/// <summary>
		/// Get the position of a point that is lengthFromStart distance around the perimeter.
		/// </summary>
		/// <param name="polygon">The polygon to find the position on</param>
		/// <param name="lengthFromStart">The distance around the perimeter form the start</param>
		/// <param name="closed">The polygon loops back on itself. There is a segment between the
		/// last and the first point, if they are not the same</param>
		/// <returns>The position on the perimeter</returns>
		public static Vector2 GetPositionAt(this List<Vector2> polygon, double lengthFromStart, bool closed = true)
		{
			var totalLength = polygon.PolygonLength();
			if (lengthFromStart > totalLength)
			{
				if (closed)
				{
					var ratio = lengthFromStart / totalLength;
					var times = (int)ratio;
					var remainder = ratio - times;
					lengthFromStart = remainder * totalLength;
				}
				else
				{
					return polygon[polygon.Count - 1];
				}
			}
			else if (lengthFromStart <= 0)
			{
				if (closed)
				{
					var ratio = lengthFromStart / totalLength;
					var times = (int)ratio;
					var remainder = ratio - times;
					lengthFromStart = (1 + remainder) * totalLength;
				}
				else
				{
					return polygon[0];
				}
			}
			
			var position = new Vector2();
			var length = 0.0;
			if (polygon.Count > 1)
			{
				position = polygon[0];
				var currentPoint = polygon[0];

				int polygonCount = polygon.Count;
				for (int i = 1; i < (closed ? polygonCount + 1 : polygonCount); i++)
				{
					var nextPoint = polygon[(polygonCount + i) % polygonCount];
					var segmentLength = (nextPoint - currentPoint).Length;
					if (length + segmentLength > lengthFromStart)
					{
						// return the distance along this segment
						var distanceAlongThisSegment = lengthFromStart - length;
						var delteFromCurrent = (nextPoint - currentPoint) * distanceAlongThisSegment / segmentLength;
						return currentPoint + delteFromCurrent;
					}
					
					position = nextPoint;

					length += segmentLength;
					currentPoint = nextPoint;
				}
			}

			return position;
		}

		public static double GetTurnAmount(this Vector2 currentPoint, Vector2 prevPoint, Vector2 nextPoint)
		{
			if (prevPoint != currentPoint
				&& currentPoint != nextPoint
				&& nextPoint != prevPoint)
			{
				prevPoint = currentPoint - prevPoint;
				nextPoint -= currentPoint;

				double prevAngle = Math.Atan2(prevPoint.Y, prevPoint.X);
				Vector2 rotatedPrev = prevPoint.GetRotated(-prevAngle);

				// undo the rotation
				nextPoint = nextPoint.GetRotated(-prevAngle);
				double angle = Math.Atan2(nextPoint.Y, nextPoint.X);

				return angle;
			}

			return 0;
		}

	}

	public class Vector2Converter : TypeConverter
	{
		public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
		{
			return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
		}

		public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
		{
			string stringValue = value as string;

			if (!string.IsNullOrEmpty(stringValue)
				&& stringValue.Length > 3)
			{
				stringValue = stringValue.Substring(1, stringValue.Length - 2);
				var values = stringValue.Split(',').Select(s =>
				{
					double.TryParse(s, out double result);
					return result;
				}).ToArray();

				switch (values.Length)
				{
					case 1:
						return new Vector2(values[0], values[0]);
					case 2:
						return new Vector2(values[0], values[1]);
					default:
						return 0;
				}
			}

			return base.ConvertFrom(context, culture, value);
		}
	}
}