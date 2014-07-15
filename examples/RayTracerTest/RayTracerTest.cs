using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;

using MatterHackers.Agg;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.VertexSource;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.Font;
using MatterHackers.RayTracer;
using MatterHackers.Csg;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg
{
    public class RayTracerTestWidget : GuiWidget
    {
        PreviewWindowRayTrace previewWindowRayTrace;

        public RayTracerTestWidget()
        {
            //CalculateIntersectCostsAndSaveToFile();

            FlowLayoutWidget leftToRight = new FlowLayoutWidget();
            leftToRight.HAnchor |= HAnchor.ParentLeftRight;
            leftToRight.VAnchor |= VAnchor.ParentBottomTop;
            
            SuspendLayout();
            previewWindowRayTrace = new PreviewWindowRayTrace();
            AnchorAll();
            previewWindowRayTrace.AnchorAll();

            leftToRight.AddChild(previewWindowRayTrace);

            GuiWidget zBuffer = new GuiWidget(HAnchor.ParentLeftRight, VAnchor.ParentBottomTop);
            zBuffer.BackgroundColor = RGBA_Bytes.Blue;
            leftToRight.AddChild(zBuffer);

            AddChild(leftToRight);

            ResumeLayout();
        }

        public override void OnDraw(Graphics2D graphics2D)
        {
            this.NewGraphics2D().Clear(new RGBA_Bytes(255, 255, 255));

            base.OnDraw(graphics2D);
        }

        string GetStringForFile(string name, long timeMs, long overheadMs)
        {
            return "\r\n" + name + ": " + timeMs.ToString() + " : minus overhead = " + (timeMs - overheadMs).ToString();
        }

        Random rayOriginRand = new Random();
        Ray GetRandomIntersectingRay()
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
        
        long CalculateIntersectCostsForItem(IRayTraceable item, int numInterations)
        {
            Stopwatch timer = new Stopwatch();
            timer.Start();
            for (int i = 0; i < numInterations; i++)
            {
                item.GetClosestIntersection(GetRandomIntersectingRay());
            }
            return timer.ElapsedMilliseconds;
        }
         
        void CalculateIntersectCostsAndSaveToFile()
        {
            int numInterations = 5000000;
            AxisAlignedBoundingBox referenceCostObject = new AxisAlignedBoundingBox(new Vector3(-.5, -.5, -.5), new Vector3(.5, .5, .5));

            Stopwatch timer = new Stopwatch();
            Vector3 accumulation = new Vector3();
            timer.Start();
            for (int i = 0; i < numInterations; i++)
            {
                accumulation += GetRandomIntersectingRay().direction;
            }
            long notIntersectStuff = timer.ElapsedMilliseconds;
            timer.Restart();
            for (int i = 0; i < numInterations; i++)
            {
                GetRandomIntersectingRay().Intersection(referenceCostObject);
            }
            long referenceMiliseconds = timer.ElapsedMilliseconds;

            SolidMaterial material = new SolidMaterial(RGBA_Floats.Black, 0, 0, 1);
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
            PolygonMesh.UnitTests.UnitTests.Run();

            AppWidgetFactory appWidget = new RayTracerTestFactory();
            appWidget.CreateWidgetAndRunInWindow();
        }
    }

    public class RayTracerTestFactory : AppWidgetFactory
    {
		public override GuiWidget NewWidget()
        {
            return new RayTracerTestWidget();
        }

		public override AppWidgetInfo GetAppParameters()
        {
            AppWidgetInfo appWidgetInfo = new AppWidgetInfo(
            "Other",
            "A Simple Ray Tracer test",
            "A sample application to show the current capabilities of the RayTracer.",
            300,
            100);

            return appWidgetInfo;
        }
    }
}
