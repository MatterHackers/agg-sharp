using MatterHackers.Agg.UI;
using MatterHackers.RayTracer;
using MatterHackers.VectorMath;
using System;
using System.Diagnostics;

namespace MatterHackers.Agg
{
	public class RayTracerWindow : SystemWindow
	{
		private RayTraceWidget previewWindowRayTrace;

		public RayTracerWindow(int width, int height)
			: base(width, height)
		{
			//CalculateIntersectCostsAndSaveToFile();

			FlowLayoutWidget leftToRight = new FlowLayoutWidget();
			leftToRight.HAnchor |= HAnchor.Stretch;
			leftToRight.VAnchor |= VAnchor.Stretch;

			previewWindowRayTrace = new RayTraceWidget();
			AnchorAll();
			previewWindowRayTrace.AnchorAll();

			leftToRight.AddChild(previewWindowRayTrace);

			GuiWidget zBuffer = new GuiWidget()
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch
			};
			zBuffer.BackgroundColor = Color.Blue;
			leftToRight.AddChild(zBuffer);

			AddChild(leftToRight);

			BackgroundColor = Color.Black;

			ShowAsSystemWindow();
		}

		private string GetStringForFile(string name, long timeMs, long overheadMs)
		{
			return "\r\n" + name + ": " + timeMs.ToString() + " : minus overhead = " + (timeMs - overheadMs).ToString();
		}

		private Random rayOriginRand = new Random();

		private Ray GetRandomIntersectingRay()
		{
			double maxDist = 1000000;
			Vector3 origin = new Vector3(
				(rayOriginRand.NextDouble() * 2 - 1) * maxDist,
				(rayOriginRand.NextDouble() * 2 - 1) * maxDist,
				(rayOriginRand.NextDouble() * 2 - 1) * maxDist);
			Vector3 direction = Vector3.Normalize(-origin);
			Ray randomRay = new Ray(origin, direction, 0, double.MaxValue);
			return randomRay;
		}

		private long CalculateIntersectCostsForItem(IPrimitive item, int numInterations)
		{
			Stopwatch timer = new Stopwatch();
			timer.Start();
			for (int i = 0; i < numInterations; i++)
			{
				item.GetClosestIntersection(GetRandomIntersectingRay());
			}
			return timer.ElapsedMilliseconds;
		}

		private void CalculateIntersectCostsAndSaveToFile()
		{
			int numInterations = 5000000;
			AxisAlignedBoundingBox referenceCostObject = new AxisAlignedBoundingBox(new Vector3(-.5, -.5, -.5), new Vector3(.5, .5, .5));

			Stopwatch timer = new Stopwatch();
			Vector3 accumulation = new Vector3();
			timer.Start();
			for (int i = 0; i < numInterations; i++)
			{
				accumulation += GetRandomIntersectingRay().directionNormal;
			}
			long notIntersectStuff = timer.ElapsedMilliseconds;
			timer.Restart();
			for (int i = 0; i < numInterations; i++)
			{
				GetRandomIntersectingRay().Intersection(referenceCostObject);
			}
			long referenceMiliseconds = timer.ElapsedMilliseconds;

			SolidMaterial material = new SolidMaterial(ColorF.Black, 0, 0, 1);
			long sphereMiliseconds = CalculateIntersectCostsForItem(new SphereShape(new Vector3(), .5, material), numInterations);
			long cylinderMiliseconds = CalculateIntersectCostsForItem(new CylinderShape(.5, 1, material), numInterations);
			long boxMiliseconds = CalculateIntersectCostsForItem(new BoxShape(new Vector3(-.5, -.5, -.5), new Vector3(.5, .5, .5), material), numInterations);
			long planeMiliseconds = CalculateIntersectCostsForItem(new PlaneShape(new Vector3(0, 0, 1), 0, material), numInterations);
			BaseShape triangleTest = new TriangleShape(new Vector3(-1, 0, 0), new Vector3(0, 1, 0), new Vector3(0, 0, 0), material);
			long triangleMiliseconds = CalculateIntersectCostsForItem(triangleTest, numInterations);

			System.IO.File.WriteAllText("Cost Of Primitive.txt",
				"Cost of Primitives"
				+ "\r\n" + numInterations.ToString("N0") + " intersections per primitive."
				+ "\r\nTest Overhead: " + notIntersectStuff.ToString()
				+ GetStringForFile("AABB", referenceMiliseconds, notIntersectStuff)
				+ GetStringForFile("Sphere", sphereMiliseconds, notIntersectStuff)
				+ GetStringForFile("Cylider", cylinderMiliseconds, notIntersectStuff)
				+ GetStringForFile("Box", boxMiliseconds, notIntersectStuff)
				+ GetStringForFile("Plane", planeMiliseconds, notIntersectStuff)
				+ GetStringForFile("Triangle", triangleMiliseconds, notIntersectStuff)
				);
		}

		[STAThread]
		public static void Main(string[] args)
		{
			new RayTracerWindow(300, 100);
		}
	}
}