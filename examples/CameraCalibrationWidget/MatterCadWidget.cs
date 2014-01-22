/*
Copyright (c) 2012, Lars Brubaker
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

using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Csg;
using MatterHackers.PolygonMesh;
using MatterHackers.RenderOpenGl;
using MatterHackers.VectorMath;
using RayTracer;

namespace MatterHackers.MatterCad
{

    public class MatterCadWidget : RectangleWidget
    {
        CsgObject objectToRender = null;

        PreviewWindowRayTrace previewWindowRayTrace;
        PreviewWindowGL previewWindowGL;
        Button runMatterScript;
        Button outputScad;
        Splitter verticleSpliter;

        TextEditWidget matterScriptEditor;
        FlowLayoutWidget textSide;

        public MatterCadWidget()
        {
            SuspendLayout();
            verticleSpliter = new Splitter();
            {
                // pannel 1 stuff
                textSide = new FlowLayoutWidget(FlowDirection.TopToBottom);
                {
                    matterScriptEditor = new TextEditWidget("", pixelWidth: 300, pixelHeight: 500, multiLine: true);
                    matterScriptEditor.ShowBounds = true;
                    //matterScriptEditor.LocalBounds = new rect_d(0, 0, 200, 300);
                    textSide.AddChild(matterScriptEditor);
                    textSide.Resize += new ResizeEventHandler(textSide_Resize);

                    FlowLayoutWidget buttonBar = new FlowLayoutWidget();
                    {
                        Button loadFile = new Button("Load Matter Script");
                        loadFile.Click += new ButtonBase.ButtonEventHandler(loadFile_Click);
                        buttonBar.AddChild(loadFile);

                        runMatterScript = new Button("Run Matter Script");
                        runMatterScript.Click += new ButtonBase.ButtonEventHandler(runMatterScript_Click);
                        buttonBar.AddChild(runMatterScript);

                        outputScad = new Button("Output SCAD");
                        outputScad.Click += new ButtonBase.ButtonEventHandler(outputScad_Click);
                        buttonBar.AddChild(outputScad);
                    }
                    textSide.AddChild(buttonBar);
                }

                // pannel 2 stuff
                FlowLayoutWidget rightStuff = new FlowLayoutWidget(FlowDirection.TopToBottom);
                {
                    previewWindowRayTrace = new PreviewWindowRayTrace();
                    rightStuff.AddChild(previewWindowRayTrace);
                    previewWindowGL = new PreviewWindowGL();
                    previewWindowGL.DrawGlContent += new PreviewWindowGL.DrawGlContentEventHandler(glLightedView_DrawGlContent);
                    rightStuff.AddChild(previewWindowGL);

                    FlowLayoutWidget radioButtons = new FlowLayoutWidget();
                    {
                        RadioButton rayTrace = new RadioButton("Ray Trace");
                        radioButtons.AddChild(rayTrace);
                        RadioButton openGL = new RadioButton("OpenGL");
                        radioButtons.AddChild(openGL);

                        rayTrace.CheckedStateChanged += new RadioButton.CheckedStateChangedEventHandler(rayTrace_CheckedStateChanged);
                        openGL.CheckedStateChanged += new RadioButton.CheckedStateChangedEventHandler(openGL_CheckedStateChanged);

                        //rayTrace.Checked = true;
                        openGL.Checked = true;
                    }
                    rightStuff.AddChild(radioButtons);
                }
                verticleSpliter.Panel2.AddChild(rightStuff);

                verticleSpliter.Panel1.AddChild(textSide);
            }
            ResumeLayout();

            AddChild(verticleSpliter);
        }

        void openGL_CheckedStateChanged(object sender, EventArgs e)
        {
            previewWindowRayTrace.Visible = false;
            previewWindowGL.Visible = true;
        }

        void rayTrace_CheckedStateChanged(object sender, EventArgs e)
        {
            previewWindowRayTrace.Visible = true;
            previewWindowGL.Visible = false;
        }

        void textSide_Resize(object sender)
        {
            matterScriptEditor.LocalBounds = new rect_d(0, 0, ((GUIWidget)sender).Width-10, matterScriptEditor.Height);
        }

        void glLightedView_DrawGlContent(object sender, EventArgs e)
        {
            if (objectToRender != null)
            {
                RenderCsgToGl.Render(objectToRender);
            }
        }

        public override void OnLayout()
        {
            SetAnchor(AnchorFlags.All);

            verticleSpliter.SetAnchor(AnchorFlags.All);
            verticleSpliter.SplitterDistance = Width / 2;

            int size = 150;
            //previewWindowRayTrace.OriginRelativeParent = new Vector2(0, previewWindowRayTrace.Parrent.LocalBounds.Top - size);
            //previewWindowRayTrace.BoundsRelativeToParent = new rect_d(0, previewWindowRayTrace.Parrent.LocalBounds.Top - size, size, previewWindowRayTrace.Parrent.LocalBounds.Top);
            //previewWindowRayTrace.SetAnchor(AnchorFlags.Top);

            textSide.SetAnchor(AnchorFlags.All);

            previewWindowGL.SetAnchor(AnchorFlags.All);

            base.OnLayout();
        }

        public override void OnIdle()
        {
            Invalidate();
            base.OnIdle();
        }

        string loadedFileName;
        SourceFileType loadedSourceFileType = SourceFileType.CSharp;
        void loadFile_Click(object sender, MouseEventArgs mouseEvent)
        {
            GuiHalWidget.OpenFileDialogParams openParams = new GuiHalWidget.OpenFileDialogParams("MatterScript Files,c-sharp code", "*.part,*.cs");

            Stream streamToLoadFrom = GuiHalFactory.PrimaryHalWidget.OpenFileDialog(openParams);
            if (streamToLoadFrom != null)
            {
                loadedFileName = openParams.FileName;
                string extension = System.IO.Path.GetExtension(openParams.FileName).ToUpper(CultureInfo.InvariantCulture);
                if (extension == ".CS")
                {
                    loadedSourceFileType = SourceFileType.CSharp;
                }
                else if (extension == ".VB")
                {
                    loadedSourceFileType = SourceFileType.VisualBasic;
                }

                //string text = System.IO.File.ReadAllText(loadedFileName);

                StreamReader streamReader = new StreamReader(streamToLoadFrom);
                matterScriptEditor.Text = streamReader.ReadToEnd();
                streamToLoadFrom.Close();

                verticleSpliter.SplitterDistance = verticleSpliter.SplitterDistance - 1;
                verticleSpliter.SplitterDistance = verticleSpliter.SplitterDistance + 1;
            }
        }

        void runMatterScript_Click(object sender, MouseEventArgs mouseEvent)
        {
            CsgObject testObject = ParseScript();
            if (testObject != null)
            {
                objectToRender = testObject;
            }
        }

        System.AppDomain compilerAppDomain = null;
        void CreateAppDomain()
        {
            System.AppDomainSetup pythonAppDomainSetup = new AppDomainSetup();
            pythonAppDomainSetup.ApplicationBase = ".";

            bool usePermissionTest = false;
            if (usePermissionTest)
            {
                System.Security.PermissionSet permissionSet = new System.Security.PermissionSet(System.Security.Permissions.PermissionState.None);
                permissionSet.AddPermission(new System.Security.Permissions.SecurityPermission(System.Security.Permissions.SecurityPermissionFlag.Execution));
                //permissionSet.AddPermission(new System.Security.Permissions.FileIOPermission(System.Security.Permissions.PermissionState.Unrestricted));

                compilerAppDomain = System.AppDomain.CreateDomain("Python App Domain", null, pythonAppDomainSetup, permissionSet, null);
            }
            else
            {
                compilerAppDomain = System.AppDomain.CreateDomain("Python App Domain");
            }

            Dictionary<string, object> options = new Dictionary<string, object>();
            options["Debug"] = true;
        }

        void ShutdownAppDomain()
        {
            System.AppDomain.Unload(compilerAppDomain);
        }

        public enum SourceFileType { CSharp, VisualBasic };
        public bool CompileExecutable(String fileContents, String fileName, SourceFileType fileType, out Assembly compiledAssemply)
        {
            compiledAssemply = null;
            // TODO: look into Managed Extensibility Framework

            CodeDomProvider provider = null;
            bool compileOk = false;

            // Select the code provider based on the input file extension.
            if (fileType == SourceFileType.CSharp)
            {
                provider = CodeDomProvider.CreateProvider("CSharp");
            }
            else if (fileType == SourceFileType.VisualBasic)
            {
                provider = CodeDomProvider.CreateProvider("VisualBasic");
            }
            else
            {
                Debug.WriteLine("Source file must have a .cs or .vb extension");
            }

            if (provider != null)
            {
                // Format the executable file name.
                // Build the output assembly path using the current directory
                // and <source>_cs.exe or <source>_vb.exe.

                CompilerParameters cp = new CompilerParameters();

                cp.ReferencedAssemblies.Add("MatterHackers.Csg.dll");
                cp.ReferencedAssemblies.Add("MatterHackers.VectorMath.dll");

                // Generate an executable instead of 
                // a class library.
                cp.GenerateExecutable = false;

                // Save the assembly as a physical file.
                cp.GenerateInMemory = true;

                // Set whether to treat all warnings as errors.
                cp.TreatWarningsAsErrors = false;

                // Invoke compilation of the source file.
                //CompilerResults cr = provider.CompileAssemblyFromFile(cp, fileName);

                CompilerResults cr = provider.CompileAssemblyFromSource(cp, fileContents);

                if (cr.Errors.Count > 0)
                {
                    // Display compilation errors.
                    foreach (CompilerError ce in cr.Errors)
                    {
                        Debug.WriteLine("  {0}", ce.ToString());
                        Debug.WriteLine("");
                    }
                }
                else
                {
                    // Display a successful compilation message.
                    Debug.WriteLine("Source built successfully.");
                }

                // Return the results of the compilation.
                if (cr.Errors.Count > 0)
                {
                    compileOk = false;
                }
                else
                {
                    compileOk = true;
                }

                compiledAssemply = cr.CompiledAssembly;
            }

            return compileOk;
        }

        static string preString = @"using System;

using MatterHackers.Csg;
using MatterHackers.VectorMath;

namespace SimplePartScripting
{
    public class SimplePartWrapper
    {
        public static Primitive SimplePartFunction()
        {
";

        static string postString = @"        }
    }
}
";

        CsgObject ParseScript()
        {
            CreateAppDomain();

            Assembly generatedAssembly;

            if (System.IO.Path.GetExtension(loadedFileName).ToUpper(CultureInfo.InvariantCulture) == ".PART")
            {
                CompileExecutable(preString + matterScriptEditor.Text + postString, loadedFileName, loadedSourceFileType, out generatedAssembly);
            }
            else
            {
                CompileExecutable(matterScriptEditor.Text, loadedFileName, loadedSourceFileType, out generatedAssembly);
            }

            CsgObject testObject = null;
            Type[] types = generatedAssembly.GetTypes();
            if (types.Length == 1)
            {
                // Grabbing the type that has the static generic method
                Type typeofClassWithGenericStaticMethod = types[0];

                // Grabbing the specific static method
                MethodInfo methodInfo = typeofClassWithGenericStaticMethod.GetMethod("SimplePartFunction", System.Reflection.BindingFlags.Static | BindingFlags.Public);

                testObject = (CsgObject)methodInfo.Invoke(null, null);
            }

            ShutdownAppDomain();

            return testObject;
        }

        void outputScad_Click(object sender, MouseEventArgs mouseEvent)
        {
            if (matterScriptEditor.Text == "")
            {
                loadFile_Click(sender, mouseEvent);
            }

            CsgObject testObject = ParseScript();
            if (testObject != null)
            {
                GuiHalWidget.SaveFileDialogParams saveParams = new GuiHalWidget.SaveFileDialogParams("Scad part file (*.scad)", "*.scad");
                Stream streamToSaveTo = GuiHalFactory.PrimaryHalWidget.SaveFileDialog(saveParams);

                if (streamToSaveTo != null)
                {
                    OpenSCadOutput.Save(Utilities.PutOnPlatformAndCenter(testObject), streamToSaveTo);
                }
            }
        }

        static NamedExecutionTimer MatterCadWidget_OnDraw = new NamedExecutionTimer("MatterCadWidget_OnDraw");
        public override void OnDraw(Graphics2D graphics2D)
        {
            MatterCadWidget_OnDraw.Start();
            graphics2D.Clear(RGBA_Bytes.White);
            rect_d rect = new rect_d(Width - 40, 10, Width - 10, 40);
            graphics2D.FillRectangle(rect, previewWindowRayTrace.mouseOverColor);
            graphics2D.Rectangle(rect, RGBA_Bytes.Black);
            Invalidate(rect);

            base.OnDraw(graphics2D);
            MatterCadWidget_OnDraw.Stop();
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

            long sphereMiliseconds = CalculateIntersectCostsForItem(new SphereShape(new Vector3(), .5, new SolidMaterial(RGBA_Floats.Black, 0, 0, 1)), numInterations);
            long boxMiliseconds = CalculateIntersectCostsForItem(new BoxShape(new Vector3(-.5, -.5, -.5), new Vector3(.5, .5, .5), new SolidMaterial(RGBA_Floats.Black, 0, 0, 1)), numInterations);
            long planeMiliseconds = CalculateIntersectCostsForItem(new PlaneShape(new Vector3(0, 0, 1), 0, new SolidMaterial(RGBA_Floats.Black, 0, 0, 1)), numInterations);

            System.IO.File.WriteAllText("Cost Of Primitive.txt",
                "Cost of Primitives"
                + "\r\n" + numInterations.ToString("N0") + " intersections per primitive."
                + "\r\nTest Overhead: " + notIntersectStuff.ToString()
                + GetStringForFile("AABB", referenceMiliseconds, notIntersectStuff)
                + GetStringForFile("Sphere", sphereMiliseconds, notIntersectStuff)
                + GetStringForFile("Box", boxMiliseconds, notIntersectStuff)
                + GetStringForFile("Plane", planeMiliseconds, notIntersectStuff)
                );
        }

        string GetStringForFile(string name, long timeMs, long overheadMs)
        {
            return "\r\n" + name + ": " + timeMs.ToString() + " : minus overhead = " + (timeMs - overheadMs).ToString();
        }
    }
    
    public class MatterCadWidgetFactory : IAppWidgetFactory
    {
        public GUIWidget NewWidget()
        {
            return new MatterCadWidget();
        }

        public AppWidgetInfo GetAppParameters()
        {
            AppWidgetInfo appWidgetInfo = new AppWidgetInfo(
            "Other",
            "Matter CAD",
            "The 3D modeling system for MatterHackers' BuildBot.",
            800,
            600);

            return appWidgetInfo;
        }
    }
}
