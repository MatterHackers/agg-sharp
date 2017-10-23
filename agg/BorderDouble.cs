using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;

namespace MatterHackers.Agg
{
	/// <summary>
	/// BorderDouble is used to represent the border around (Margin) on inside (Padding) of a rectangular area.
	/// </summary>
	[TypeConverter(typeof(BorderDoubleConverter))]
	public struct BorderDouble
	{
		public double Left, Bottom, Right, Top;

		public BorderDouble(double valueForAll)
			: this(valueForAll, valueForAll, valueForAll, valueForAll)
		{
		}

		public BorderDouble(double leftRight, double bottomTop)
			: this(leftRight, bottomTop, leftRight, bottomTop)
		{
		}

		public BorderDouble(double left = 0, double bottom = 0, double right = 0, double top = 0)
		{
			this.Left = left;
			this.Bottom = bottom;
			this.Right = right;
			this.Top = top;
		}

		public static implicit operator BorderDouble(int valueForAll)  // explicit byte to digit conversion operator
		{
			return new BorderDouble(valueForAll);
		}

		public static bool operator ==(BorderDouble a, BorderDouble b)
		{
			if (a.Left == b.Left && a.Bottom == b.Bottom && a.Right == b.Right && a.Top == b.Top)
			{
				return true;
			}

			return false;
		}

		public static bool operator !=(BorderDouble a, BorderDouble b)
		{
			if (a.Left != b.Left || a.Bottom != b.Bottom || a.Right != b.Right || a.Top != b.Top)
			{
				return true;
			}

			return false;
		}

		static public BorderDouble operator *(BorderDouble a, double b)
		{
			return new BorderDouble(a.Left * b, a.Bottom * b, a.Right * b, a.Top * b);
		}

		static public BorderDouble operator *(double b, BorderDouble a)
		{
			return new BorderDouble(a.Left * b, a.Bottom * b, a.Right * b, a.Top * b);
		}

		public static BorderDouble operator +(BorderDouble left, BorderDouble right)
		{
			left.Left += right.Left;
			left.Bottom += right.Bottom;
			left.Right += right.Right;
			left.Top += right.Top;
			return left;
		}

		public override int GetHashCode()
		{
			return new { x1 = Left, x2 = Right, y1 = Bottom, y2 = Top }.GetHashCode();
		}

		public override bool Equals(object obj)
		{
			if (obj.GetType() == typeof(BorderDouble))
			{
				return this == (BorderDouble)obj;
			}
			return false;
		}

		public double Width
		{
			get
			{
				return Left + Right;
			}
		}

		// This function assumes the rect is normalized
		public double Height
		{
			get
			{
				return Bottom + Top;
			}
		}

		public override string ToString()
		{
			return $"{Left}, {Bottom}, {Right}, {Top}";
		}

		public void Round()
		{
			this.Left = Math.Round(this.Left);
			this.Bottom = Math.Round(this.Bottom);
			this.Right = Math.Round(this.Right);
			this.Top = Math.Round(this.Top);
		}
	}

	public class BorderDoubleConverter : TypeConverter
	{
		public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
		{
			return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
		}

		public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
		{
			string stringValue = value as string;

			if (!string.IsNullOrEmpty(stringValue))
			{
				var values = stringValue.Split(',').Select(s =>
				{
					double result;
					double.TryParse(s, out result);
					return result;
				}).ToArray();

				switch (values.Length)
				{
					case 1:
						return new BorderDouble(values[0]);
					case 2:
						return new BorderDouble(values[0], values[1]);
					case 4:
						return new BorderDouble(values[0], values[1], values[2], values[3]);
					default:
						return 0;
				}
			}

			return base.ConvertFrom(context, culture, value);
		}
	}
}