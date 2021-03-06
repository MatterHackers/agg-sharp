using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;

namespace MatterHackers.Agg
{
	[TypeConverter(typeof(RadiusCornersConverter))]
	public struct RadiusCorners
	{
		public double NE, NW, SW, SE;

		public RadiusCorners(double valueForAll)
			: this(valueForAll, valueForAll, valueForAll, valueForAll)
		{
		}

		public RadiusCorners(double leftRight, double bottomTop)
			: this(leftRight, bottomTop, leftRight, bottomTop)
		{
		}

		public RadiusCorners(double ne = 0, double nw = 0, double sw = 0, double se = 0)
		{
			this.NE = ne;
			this.NW = nw;
			this.SW = sw;
			this.SE = se;
		}

		public static implicit operator RadiusCorners(int valueForAll) // explicit byte to digit conversion operator
		{
			return new RadiusCorners(valueForAll);
		}

		public static implicit operator RadiusCorners(double valueForAll)
		{
			return new RadiusCorners(valueForAll);
		}

		public static bool operator ==(RadiusCorners a, RadiusCorners b)
		{
			if (a.NE == b.NE && a.NW == b.NW && a.SW == b.SW && a.SE == b.SE)
			{
				return true;
			}

			return false;
		}

		public static bool operator !=(RadiusCorners a, RadiusCorners b)
		{
			if (a.NE != b.NE || a.NW != b.NW || a.SW != b.SW || a.SE != b.SE)
			{
				return true;
			}

			return false;
		}

		public override int GetHashCode()
		{
			return new { x1 = NE, x2 = SW, y1 = NW, y2 = SE }.GetHashCode();
		}

		public override bool Equals(object obj)
		{
			if (obj.GetType() == typeof(RadiusCorners))
			{
				return this == (RadiusCorners)obj;
			}

			return false;
		}

		public override string ToString()
		{
			return $"{NE}, {NW}, {SW}, {SE}";
		}
	}

	public class RadiusCornersConverter : TypeConverter
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
					double.TryParse(s, out double result);
					return result;
				}).ToArray();

				switch (values.Length)
				{
					case 1:
						return new RadiusCorners(values[0]);
					case 2:
						return new RadiusCorners(values[0], values[1]);
					case 4:
						return new RadiusCorners(values[0], values[1], values[2], values[3]);
					default:
						return 0;
				}
			}

			return base.ConvertFrom(context, culture, value);
		}
	}
}