namespace ObjParser
{
	public class Extent
	{
		public double XMax { get; set; }
		public double XMin { get; set; }
		public double YMax { get; set; }
		public double YMin { get; set; }
		public double ZMax { get; set; }
		public double ZMin { get; set; }

		public double XSize { get { return XMax - XMin; } }
		public double YSize { get { return YMax - YMin; } }
		public double ZSize { get { return ZMax - ZMin; } }
	}
}
