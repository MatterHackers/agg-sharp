using System;
using System.Collections.Generic;
using System.IO;

using AGG;
using AGG.Transform;
using AGG.Image;
using AGG.VertexSource;
using AGG.UI;
using AGG.Font;

using RayTracer;

namespace BigAtom
{
    public class BigAtom_application : MatchParentBoundsWidget
    {
        ImageBuffer marbleTexture;
        ImageBuffer woodTexture;
        ImageBuffer wallTexture;
        GuiHalSurface guiSurface;
        
        Scene scene;
		
        public BigAtom_application(GuiHalSurface guiSurface)
        {
            this.guiSurface = guiSurface;

            renderScene();
        }

        // marble balls scene
        private void SetupScene0()
        {
            TextureMaterial texture = new TextureMaterial(marbleTexture, 0.0, 0.0, 2, .5);

            scene = new Scene();
            scene.Camera = new Camera(new Vector(0, 0, -15), new Vector(-.2, 0, 5), new Vector(0, 1, 0));

            // setup a solid reflecting sphere
            scene.Shapes.Add(new SphereShape(new Vector(-1.5, 0.5, 0), .5,
                               new SolidMaterial(new RGBA_Doubles(0, .5, .5), 0.2, 0.0, 2.0)));

            // setup sphere with a marble texture from an image
            scene.Shapes.Add(new SphereShape(new Vector(0, 0, 0), 1, texture));

            scene.Shapes.Add(new BoxShape(new Vector(0, 0, -15), new Vector(.5, .5, .5), new SolidMaterial(new RGBA_Doubles(0, 0, 1), .1, 0, .2)));

            // setup the chessboard floor
            scene.Shapes.Add(new PlaneShape(new Vector(0.1, 0.9, -0.5).Normalize(), 1.2,
                               new ChessboardMaterial(new RGBA_Doubles(1, 1, 1), new RGBA_Doubles(0, 0, 0), 0.2, 0, 1, 0.7)));

            //add two lights for better lighting effects
            scene.Lights.Add(new Light(new Vector(5, 10, -1), new RGBA_Doubles(0.8, 0.8, 0.8)));
            scene.Lights.Add(new Light(new Vector(-3, 5, -15), new RGBA_Doubles(0.8, 0.8, 0.8)));

        }

        private void SetupScene1()
        {

            TextureMaterial woodMaterial = new TextureMaterial(woodTexture, 0.2, 0.0, 2, .5);
            TextureMaterial marbleMaterial = new TextureMaterial(marbleTexture, 0.0, 0.0, 2, .5);
            TextureMaterial wallMaterial = new TextureMaterial(wallTexture, 0.0, 0.0, 2, .4);


            scene = new Scene();
            scene.Background = new Background(new RGBA_Doubles(.8, .8, .8), 0.8);
            Vector campos = new Vector(5, 1.8, -15);
            scene.Camera = new Camera(campos, campos / -3, new Vector(0, 1, 0).Normalize());

            // marble
            scene.Shapes.Add(new SphereShape(new Vector(1, 1, -5), 1,
                               marbleMaterial));

            //floor
            scene.Shapes.Add(new PlaneShape(new Vector(0, 1, 0).Normalize(), 0, woodMaterial));
            //wall
            scene.Shapes.Add(new PlaneShape(new Vector(0, 0, 1).Normalize(), 0, wallMaterial));

            scene.Lights.Add(new Light(new Vector(25, 20, -20), new RGBA_Doubles(0.5, 0.5, 0.5)));
            scene.Lights.Add(new Light(new Vector(-3, 5, -15), new RGBA_Doubles(0.5, 0.5, 0.5)));
        }

        private void SetupScene2()
        {
            scene = new Scene();
            scene.Background = new Background(new RGBA_Doubles(.2, .3, .4), 0.5);
            Vector campos = new Vector(0, 0, -5);
            scene.Camera = new Camera(campos, campos / -2, new Vector(0, 1, 0).Normalize());

            Random rnd = new Random();
            for (int i = 0; i < 40; i++)
            {

                // setup a solid reflecting sphere
                scene.Shapes.Add(new SphereShape(new Vector(rnd.Next(-100, 100) / 50.0, rnd.Next(-100, 100) / 50.0, rnd.Next(0, 200) / 50.0), .2,
                                   new SolidMaterial(new RGBA_Doubles(rnd.Next(0, 100) / 100.0, rnd.Next(0, 100) / 100.0, rnd.Next(0, 100) / 100.0), 0.4, 0.0, 2.0)));

            }

            scene.Lights.Add(new Light(new Vector(5, 10, -1), new RGBA_Doubles(0.8, 0.8, 0.8)));
            scene.Lights.Add(new Light(new Vector(-3, 5, -15), new RGBA_Doubles(0.8, 0.8, 0.8)));

        }

        private void SetupScene3()
        {

            scene = new Scene();
            scene.Camera = new Camera(new Vector(0, 0, -15), new Vector(-.2, 0, 5), new Vector(0, 1, 0));
            scene.Background = new Background(new RGBA_Doubles(0.5, .5, .5), 0.4);

            // setup a solid reflecting sphere
            scene.Shapes.Add(new SphereShape(new Vector(-0.5, 0.5, -2), 1.5,
                               new SolidMaterial(new RGBA_Doubles(0, .5, .5), 0.3, 0.0, 2.0)));

            // setup the chessboard floor
            scene.Shapes.Add(new PlaneShape(new Vector(0.1, 0.9, -0.5).Normalize(), 1.2,
                               new ChessboardMaterial(new RGBA_Doubles(1, 1, 1), new RGBA_Doubles(0, 0, 0), 0.2, 0, 1, 0.7)));

            //add two lights for better lighting effects
            scene.Lights.Add(new Light(new Vector(5, 10, -1), new RGBA_Doubles(0.8, 0.8, 0.8)));
            scene.Lights.Add(new Light(new Vector(-3, 5, -15), new RGBA_Doubles(0.8, 0.8, 0.8)));
        }

        // metallic box with marble on stone floor
        private void SetupScene4()
        {

            TextureMaterial woodMaterial = new TextureMaterial(woodTexture, 0.0, 0.0, 2, .5);
            TextureMaterial marbleMaterial = new TextureMaterial(marbleTexture, 0.3, 0.0, 2, .5);
            TextureMaterial wallMaterial = new TextureMaterial(wallTexture, 0.0, 0.0, 2, .4);


            scene = new Scene();
            scene.Background = new Background(new RGBA_Doubles(.3, .8, .8), 0.8);
            Vector campos = new Vector(14, 2, -6);
            scene.Camera = new Camera(campos, campos / -2.5, new Vector(-0, 1, 0.1).Normalize());

            // marble
            scene.Shapes.Add(new SphereShape(new Vector(-3, 1, 5), 2,
                               marbleMaterial));

            // box
            scene.Shapes.Add(new BoxShape(new Vector(0, 1, -1), new Vector(1, 0, 0),
                               woodMaterial));

            //floor
            scene.Shapes.Add(new PlaneShape(new Vector(0, 1, 0).Normalize(), 0, wallMaterial));

            //wall
            //scene.Shapes.Add(new PlaneShape(new Vector(0, 0, 1).Normalize(), 0, wallMaterial));

            scene.Lights.Add(new Light(new Vector(25, 20, -20), new RGBA_Doubles(0.5, 0.5, 0.5)));
            scene.Lights.Add(new Light(new Vector(-23, 25, -15), new RGBA_Doubles(0.5, 0.5, 0.5)));
        }

        ImageBuffer bitmap;
        private void renderScene()
        {
            SetupScene3();

            RayTracer.RayTracer raytracer = new RayTracer.RayTracer(AntiAliasing.Medium, true, true, true, true, true);
            raytracer.RenderUpdate += new RenderUpdateDelegate(raytracer_RenderUpdate);

            rect_i rect = new rect_i(0, 0, 512, 512);
            bitmap = new ImageBuffer(rect.Width, rect.Height, 32, new BlenderBGRA());

            Graphics2D graphics2D = bitmap.NewGraphics2D();

            raytracer.RayTraceScene(graphics2D, rect, scene);

            graphics2D.Rect(new rect_d(bitmap.GetBoundingRect()), RGBA_Bytes.Black);
        }

        void raytracer_RenderUpdate(int progress, double duration, double ETA, int scanline)
        {
            Invalidate();
        }
        
        public override void OnDraw(Graphics2D graphics2D)
        {
            this.NewGraphics2D().Clear(new RGBA_Bytes(255, 255, 255));

            graphics2D.Render(bitmap, 0, 0);

            base.OnDraw(graphics2D);
        }

        public static void StartDemo(bool useHWAcceleration)
        {
#if true
            GuiHalFactory.SetGuiBackend(new WindowsFormsBitmapBackendGuiFactory());
            GuiHalSurface primaryWindow = GuiHalFactory.CreatePrimarySurface(512, 512, GuiHalSurface.CreateFlags.Resizable, GuiHalSurface.PixelFormat.PixelFormatBgr24);
#else
            GuiHalFactory.SetGuiBackend(new WindowsFormsOpenGLBackendGuiFactory());
            GuiHalSurface primaryWindow = GuiHalFactory.CreatePrimarySurface(512, 400, GuiHalSurface.CreateFlags.Resizable, GuiHalSurface.PixelFormat.PixelFormatBgra32);
#endif
            primaryWindow.Caption = "BigAtom - Voxel CSG Magic";

            primaryWindow.AddChild(new BigAtom_application(primaryWindow));
            primaryWindow.Run();
        }

        [STAThread]
        public static void Main(string[] args)
        {
        	StartDemo(false);
        }
    }
}
