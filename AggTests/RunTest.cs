using System;

namespace MatterHackers.Agg.Tests
{
	public class RunTest
	{
		public static void Main(string[] args)
		{
			Console.WriteLine("This is the test running exe that you can use when \nthere is a problem with a test.\n");

			// Run any test that needs debuging
			Vector3Tests test1 = new Vector3Tests();
			test1.DotProduct();

			LionRenderTest lionTest = new LionRenderTest();
			lionTest.CompareToLionTGA();

			Console.Write("There were no unhandled exceptions.\n\nPress any key to continue . . . ");
			Console.ReadKey(true);
		}
	}
}