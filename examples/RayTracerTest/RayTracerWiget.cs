/*
Copyright (c) 2013, Lars Brubaker
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.RayTracer.Traceable;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace MatterHackers.RayTracer
{
	using System.Threading;
	using MatterHackers.Agg.OpenGlGui;
	using MatterHackers.RayTracer.Light;

	public class RayTraceWidget : GuiWidget
	{
		private ImageBuffer destImage;

		private RayTracer raytracer = new RayTracer()
		{
			//AntiAliasing = AntiAliasing.None,
			//AntiAliasing = AntiAliasing.Low,
			AntiAliasing = AntiAliasing.Medium,
			//MultiThreaded = false,
		};

		private IPrimitive focusedObject = null;
		private Stopwatch renderTime = new Stopwatch();
		private Scene scene;

		private Transform allObjectsHolder;
		private IPrimitive allObjects;
		private List<IPrimitive> renderCollection = new List<IPrimitive>();

		private TrackballTumbleWidget trackballTumbleWidget;

		private List<string> timingStrings = new List<string>();
		private Stopwatch totalTime = new Stopwatch();

		private WorldView world;

		public RayTraceWidget(int width = 200, int height = 200)
		{
			world = new WorldView(width, height);

			trackballTumbleWidget = new TrackballTumbleWidget(world);
			trackballTumbleWidget.DoOpenGlDrawing = false;
			trackballTumbleWidget.DrawRotationHelperCircle = false;
			//trackballTumbleWidget.DrawGlContent += trackballTumbleWidget_DrawGlContent;
			trackballTumbleWidget.TransformState = TrackBallController.MouseDownType.Rotation;

			AddChild(trackballTumbleWidget);

			totalTime.Start();

			CreateScene();
			LocalBounds = new RectangleDouble(0, 0, width, height);

			world.Scale = .03;

			world.Rotate(Quaternion.FromEulerAngles(new Vector3(0, 0, MathHelper.Tau / 16)));
			world.Rotate(Quaternion.FromEulerAngles(new Vector3(-MathHelper.Tau * .19, 0, 0)));

			trackballTumbleWidget.AnchorAll();
		}

		public class WorldCamera : ICamera
		{
			private WorldView world;

			public WorldCamera(WorldView world)
			{
				this.world = world;
			}

			public Ray GetRay(double screenX, double screenY)
			{
				return world.GetRayForLocalBounds(new Vector2(screenX, screenY));
			}
		}

		private Scene Scene
		{
			get
			{
				return Scene;
			}
		}

		private void rayTraceScene()
		{
			RectangleInt rect = new RectangleInt(0, 0, (int)Width, (int)Height);
			if (destImage == null || destImage.Width != rect.Width || destImage.Height != rect.Height)
			{
				destImage = new ImageBuffer(rect.Width, rect.Height, 32, new BlenderBGRA());
			}

			//rect_i rect = new rect_i(0, 0, (int)32, (int)32);

			renderTime.Restart();
			raytracer.RayTraceScene(rect, scene);
			raytracer.CopyColorBufferToImage(destImage, rect);
			//raytracer.CopyDepthBufferToImage(destImage, rect);
			//raytracer.CopyNoramlBufferToImage(destImage, rect);
			//raytracer.AntiAliasScene(graphics2D, rect, scene, raytracer.RayTraceColorBuffer, 13);
			renderTime.Stop();

			//graphics2D.Rect(new rect_d(bitmap.GetBoundingRect()), RGBA_Bytes.Black);
		}

		private void CreateScene()
		{
			scene = new Scene();
			scene.camera = new WorldCamera(world);
			scene.background = new Background(new RGBA_Floats(0.5, .5, .5), 0.4);

			//AddBoxAndSheresBooleanTest();
			//AddBoxAndBoxBooleanTest();
#if false
            renderCollection.Add(new BoxShape(new Vector3(), new Vector3(1, 1, 1),
                               new SolidMaterial(new RGBA_Floats(.9, .2, .1), .01, 0.0, 2.0)));
            renderCollection.Add(new BoxShape(new Vector3(.5,.5,.5), new Vector3(1.5, 1.5, 1.5),
                               new SolidMaterial(new RGBA_Floats(.9, .2, .1), .01, 0.0, 2.0)));
#endif
			//renderCollection.Add(new CylinderShape(.25, 1, new SolidMaterial(RGBA_Floats.Cyan, 0, 0, 0)));

			AddTestStl();
			//AddPolygonTest();
			//AddSphereAndBox();
			//AddAxisMarker();
			//AddCubeOfShperes();

			//renderCollection.Add(MakerGearXCariage());

			allObjects = BoundingVolumeHierarchy.CreateNewHierachy(renderCollection);
			allObjectsHolder = new Transform(allObjects);
			//allObjects = root;
			scene.shapes.Add(allObjectsHolder);

			//AddAFloor();

			//add two lights for better lighting effects
			scene.lights.Add(new PointLight(new Vector3(5000, 5000, 5000), new RGBA_Floats(0.8, 0.8, 0.8)));
			scene.lights.Add(new PointLight(new Vector3(-5000, -5000, 3000), new RGBA_Floats(0.5, 0.5, 0.5)));
		}

		private MatterHackers.Csg.CsgObject BooleanBoxBallThing()
		{
			return null;
		}

		private IPrimitive MakerGearXCariage()
		{
#if false
            MatterHackers.Csg.ObjectCSG total = new MatterHackers.Csg.Box(30, 32, 65);
            MatterHackers.Csg.ObjectCSG minusBox = new MatterHackers.Csg.Box(20, 22, 55);
            minusBox = new MatterHackers.Csg.Translate(minusBox, 5, -5, 5);
            total -= minusBox;
#else
			//MatterHackers.Csg.ObjectCSG frontRodHolder = new MatterHackers.Csg.Cylinder(11, 70, name: "front rod holder");
			MatterHackers.Csg.CsgObject frontRodHolder = new MatterHackers.Csg.Solids.Box(22, 22, 70, createCentered: false, name: "front rod holder");
			frontRodHolder = new MatterHackers.Csg.Transform.SetCenter(frontRodHolder, Vector3.Zero);
			MatterHackers.Csg.CsgObject total = frontRodHolder;

#if true
			MatterHackers.Csg.CsgObject backRodHolder = new MatterHackers.Csg.Solids.Cylinder(11, 70, name: "back rod holder");
			backRodHolder = new MatterHackers.Csg.Transform.Translate(backRodHolder, 0, 50, 0);
			//total += backRodHolder;

			MatterHackers.Csg.CsgObject plate = new MatterHackers.Csg.Solids.Box(7, 32, 65, createCentered: false);
			plate = new MatterHackers.Csg.Transform.SetCenter(plate, total.GetCenter());
			plate = new MatterHackers.Csg.Transform.Align(plate, Csg.Face.Bottom, frontRodHolder, Csg.Face.Bottom);
			//total += plate;

			MatterHackers.Csg.CsgObject beltMount = new MatterHackers.Csg.Solids.Box(7, 32, 30, createCentered: false);
			beltMount = new MatterHackers.Csg.Transform.SetCenter(beltMount, frontRodHolder.GetCenter() + new Vector3(6, -19, 0));

			// belt mount screw holes
			MatterHackers.Csg.CsgObject screwHole = new MatterHackers.Csg.Solids.Cylinder(2, beltMount.YSize + 1, MatterHackers.Csg.Alignment.x);
			screwHole = new MatterHackers.Csg.Transform.SetCenter(screwHole, beltMount.GetCenter());
			//beltMount -= new MatterHackers.Csg.Align(screwHole, Face.Front | Face.Top, beltMount, Face.Front | Face.Top, 0, 3, -4);
			//beltMount -= new MatterHackers.Csg.Align(screwHole, Face.Front | Face.Top, beltMount, Face.Front | Face.Top, 0, 18, -4);
			//beltMount -= new MatterHackers.Csg.Align(screwHole, Face.Front | Face.Bottom, beltMount, Face.Front | Face.Bottom, 0, 3, 4);
			//beltMount -= new MatterHackers.Csg.Align(screwHole, Face.Front | Face.Bottom, beltMount, Face.Front | Face.Bottom, 0, 18, 4);

			total += beltMount;
#endif

			// smooth rod holes
			//total -= new MatterHackers.Csg.Cylinder(8, 70, name: "front rod bearing hole");
			//total -= new MatterHackers.Csg.Cylinder(8, 70, name: "front rod bearing hole");
			total -= new MatterHackers.Csg.Transform.SetCenter(new MatterHackers.Csg.Solids.Box(16, 16, 70, createCentered: false, name: "front rod hole"), Vector3.Zero);
			//total -= new MatterHackers.Csg.SetCenter(new MatterHackers.Csg.Cylinder(8, 70, name: "back rod bearing hole"), backRodHolder.GetCenter());
#endif

			total = MatterHackers.Csg.CsgObject.Flatten(total);
			MatterHackers.Csg.Processors.OpenSCadOutput.Save(total, "MakerGearXCariage.scad");

			CsgToRayTraceable visitor = new CsgToRayTraceable();

			return visitor.GetIPrimitiveRecursive((dynamic)total);
		}

		private void AddAFloor()
		{
			ImageBuffer testImage = new ImageBuffer(200, 200, 32, new BlenderBGRA());
			Graphics2D graphics = testImage.NewGraphics2D();
			Random rand = new Random(0);
			for (int i = 0; i < 100; i++)
			{
				RGBA_Bytes color = new RGBA_Bytes(rand.NextDouble(), rand.NextDouble(), rand.NextDouble());
				graphics.Circle(new Vector2(rand.NextDouble() * testImage.Width, rand.NextDouble() * testImage.Height), rand.NextDouble() * 40 + 10, color);
			}
			scene.shapes.Add(new PlaneShape(new Vector3(0, 0, 1), 0, new TextureMaterial(testImage, 0, 0, .2, 1)));
			//scene.shapes.Add(new PlaneShape(new Vector3(0, 0, 1), 0, new ChessboardMaterial(new RGBA_Floats(1, 1, 1), new RGBA_Floats(0, 0, 0), 0, 0, 1, 0.7)));
		}

		private void AddCubeOfShperes()
		{
			List<IPrimitive> scanData1 = new List<IPrimitive>();
			Random rand = new Random(0);
			double dist = 2;
			for (int i = 0; i < 4000; i++)
			{
				BaseShape littleShpere = new SphereShape(new Vector3(rand.NextDouble() * dist - dist / 2, rand.NextDouble() * dist - dist / 2, rand.NextDouble() * dist - dist / 2),
					rand.NextDouble() * .1 + .01,
					new SolidMaterial(new RGBA_Floats(rand.NextDouble() * .5 + .5, rand.NextDouble() * .5 + .5, rand.NextDouble() * .5 + .5), 0, 0.0, 2.0));
				scanData1.Add(littleShpere);
			}

			renderCollection.Add(BoundingVolumeHierarchy.CreateNewHierachy(scanData1));
		}

		private void AddBoxAndSheresBooleanTest()
		{
			BoxShape box1 = new BoxShape(new Vector3(.5, .5, .5), new Vector3(1.5, 1.5, 1.5),
							   new SolidMaterial(RGBA_Floats.Green, 0, 0, 0));//.01, 0.0, 2.0));

			List<IPrimitive> subtractShapes = new List<IPrimitive>();
			SolidMaterial material = new SolidMaterial(RGBA_Floats.Red, 0, 0, 0);

#if false
			// two big spheres.  Looks good.
			subtractShapes.Add(new SphereShape(new Vector3(.5, .5, 1), .6, material));
			subtractShapes.Add(new SphereShape(new Vector3(1.5, .5, 1), .6, material));

			Transform cylinder = new Transform(new CylinderShape(.1, 3, material));
			cylinder.MoveToAbsolute(1, 1, 1);
			cylinder.RotateRelative(.1, .6, .6);
			//subtractShapes.Add(cylinder);
			//renderCollection.Add(cylinder);
#else
            for (int z = 0; z < 6; z++)
            {
                for (int y = 0; y < 6; y++)
                {
                    for (int x = 0; x < 6; x++)
                    {
                        subtractShapes.Add(new SphereShape(new Vector3(x * .2 + .5, y * .2 + .5, z * .2 + .5), .1, material));
                        //subtractShapes.Add(new SphereShape(new Vector3(x * .2 + .5, y * .2 + .5, z * .2 + .5), .13, material));
                    }
                }
            }
#endif

			IPrimitive subtractGroup = BoundingVolumeHierarchy.CreateNewHierachy(subtractShapes);
			Difference merge = new Difference(box1, subtractGroup);

			renderCollection.Add(merge);
		}

		private void AddBoxAndBoxBooleanTest()
		{
			BoxShape box1 = new BoxShape(new Vector3(.5, .5, .5), new Vector3(1.5, 1.5, 1.5),
							   new SolidMaterial(RGBA_Floats.Green, .01, 0.0, 2.0));

			List<IPrimitive> subtractShapes = new List<IPrimitive>();
			SolidMaterial material = new SolidMaterial(RGBA_Floats.Red, 0, 0, 0);
			subtractShapes.Add(new BoxShape(new Vector3(), new Vector3(1, 1, 1), material));

			IPrimitive subtractGroup = BoundingVolumeHierarchy.CreateNewHierachy(subtractShapes);
			Difference merge = new Difference(box1, subtractGroup);

			renderCollection.Add(merge);
		}

		private void AddPolygonTest()
		{
			double[][] points = new double[][]
            {
                new double[] {0.0f, 1.0f, 0.0f},
                new double[] {0.943f, -0.333f, 0.0f},
                new double[] {-0.471f, -0.333f, 0.8165f},
                new double[] {-0.471f, -0.333f, -0.8165f}
            };

			SolidMaterial redStuff = new SolidMaterial(new RGBA_Floats(.9, .2, .1), .01, 0.0, 2.0);
			// setup a solid reflecting sphere
			renderCollection.Add(new TriangleShape(new Vector3(points[0]), new Vector3(points[1]), new Vector3(points[2]), redStuff));
			renderCollection.Add(new TriangleShape(new Vector3(points[0]), new Vector3(points[3]), new Vector3(points[1]), redStuff));
			renderCollection.Add(new TriangleShape(new Vector3(points[0]), new Vector3(points[2]), new Vector3(points[3]), redStuff));
			renderCollection.Add(new TriangleShape(new Vector3(points[1]), new Vector3(points[3]), new Vector3(points[2]), redStuff));
		}

		private void AddTestStl()
		{
			Stopwatch loadTime = new Stopwatch();
			loadTime.Start();

			PolygonMesh.Mesh simpleMesh = StlProcessing.Load("circle_100x100_centered.stl", CancellationToken.None);
			//PolygonMesh.Mesh simpleMesh = StlProcessing.Load("Simple.stl");
			//PolygonMesh.Mesh simpleMesh = StlProcessing.Load("Complex.stl");
			//PolygonMesh.Mesh simpleMesh = StlProcessing.Load("bunny.stl");
			//PolygonMesh.Mesh simpleMesh = StlProcessing.Load("bunny_binary.stl");

			loadTime.Stop();

			timingStrings.Add("Time to load STL {0:0.0}s".FormatWith(loadTime.Elapsed.TotalSeconds));

			Stopwatch bvhTime = new Stopwatch();
			bvhTime.Start();
			IPrimitive bvhCollection = MeshToBVH.Convert(simpleMesh);
			bvhTime.Stop();

			timingStrings.Add("Time to create BVH {0:0.0}s".FormatWith(bvhTime.Elapsed.TotalSeconds));

			renderCollection.Add(bvhCollection);
		}

		private void AddSphereAndBox()
		{
			// setup a solid reflecting sphere
			double radius = 1.5;
			BaseShape bigSphere = new SphereShape(new Vector3(1, 4, -3), radius, new SolidMaterial(new RGBA_Floats(0, .5, .5), 0.2, 0.0, 2.0));
			renderCollection.Add(bigSphere);

			BoxShape box = new BoxShape(new Vector3(-1, 0, -4), new Vector3(1, 2, -2),
							   new SolidMaterial(new RGBA_Floats(.9, .2, .1), .01, 0.0, 2.0));
			renderCollection.Add(box);
		}

		private void AddAxisMarker()
		{
			int count = 10;
			double size = .1;
			for (int i = 1; i < count + 1; i++)
			{
				RGBA_Floats xColor = new RGBA_Floats(1, i / (double)count, i / (double)count);
				SolidMaterial xMaterial = new SolidMaterial(xColor, 0, 0.0, 2.0);
				BoxShape xBox = new BoxShape(new Vector3(i * size, 0, 0), new Vector3(i * size + size, size, size), xMaterial);
				renderCollection.Add(xBox);

				RGBA_Floats yColor = new RGBA_Floats(i / (double)count, 1, i / (double)count);
				SolidMaterial yMaterial = new SolidMaterial(yColor, 0, 0.0, 2.0);
				BoxShape yBox = new BoxShape(new Vector3(0, i * size, 0), new Vector3(size, i * size + size, size), yMaterial);
				//yBox.Transform.Position += new Vector3D(1, 1, 1);
				renderCollection.Add(yBox);

				RGBA_Floats zColor = new RGBA_Floats(i / (double)count, i / (double)count, 1);
				SolidMaterial zMaterial = new SolidMaterial(zColor, 0, 0.0, 2.0);
				BoxShape zBox = new BoxShape(new Vector3(0, 0, i * size), new Vector3(size, size, i * size + size), zMaterial);
				renderCollection.Add(zBox);
			}
		}

		public IPrimitive FocusedObject
		{
			get
			{
				if (focusedObject == null)
				{
					focusedObject = scene.shapes[0];
				}
				return focusedObject;
			}
		}

		private Matrix4X4 lastRenderedMatrix = new Matrix4X4();
		public RGBA_Floats mouseOverColor = new RGBA_Floats();
		private bool SavedTimes = false;

		public override void OnDraw(Graphics2D graphics2D)
		{
			Matrix4X4 currentRenderMatrix = world.ModelviewMatrix;
			if (lastRenderedMatrix != currentRenderMatrix)
			{
				Stopwatch traceTime = new Stopwatch();
				traceTime.Start();
				rayTraceScene();
				traceTime.Stop();

				timingStrings.Add("Time to trace BVH {0:0.0}s".FormatWith(traceTime.Elapsed.TotalSeconds));
			}
			//allObjectsHolder.AxisToWorld = trackBallController.GetTransform4X4();

			graphics2D.FillRectangle(new RectangleDouble(0, 0, 1000, 1000), RGBA_Bytes.Red);
			graphics2D.Render(destImage, 0, 0);
			//trackBallController.DrawRadius(graphics2D);
			totalTime.Stop();
			timingStrings.Add("Total Time {0:0.0}s".FormatWith(totalTime.Elapsed.TotalSeconds));

			if (!SavedTimes)
			{
				SavedTimes = true;
				File.WriteAllLines("timing.txt", timingStrings.ToArray());
			}

			graphics2D.DrawString("Ray Trace: " + renderTime.ElapsedMilliseconds.ToString(), 20, 10);

			base.OnDraw(graphics2D);
			currentRenderMatrix = lastRenderedMatrix;
		}
	}
}