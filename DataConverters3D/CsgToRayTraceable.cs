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

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;

using MatterHackers.Agg;

using MatterHackers.Csg;
using MatterHackers.Csg.Solids;
using MatterHackers.Csg.Transform;
using MatterHackers.Csg.Operations;
using MatterHackers.Csg.Processors;
using MatterHackers.VectorMath;
using MatterHackers.RayTracer;
using MatterHackers.RayTracer.Traceable;

namespace MatterHackers.DataConverters3D
{
    public class CsgToRayTraceable
    {
        public static MaterialAbstract DefaultMaterial = new SolidMaterial(RGBA_Floats.Green, 0, 0, 0);

        public CsgToRayTraceable()
        {
        }

        #region Visitor Patern Functions
        public IRayTraceable GetIRayTraceableRecursive(CsgObject objectToProcess)
        {
            throw new Exception("You must wirte the specialized function for this type.");
        }

        #region PrimitiveWrapper
        public IRayTraceable GetIRayTraceableRecursive(CsgObjectWrapper objectToProcess)
        {
            return GetIRayTraceableRecursive((dynamic)objectToProcess.Root);
        }
        #endregion

        #region Box
        public IRayTraceable GetIRayTraceableRecursive(BoxPrimitive objectToProcess)
        {
            return new BoxShape(Vector3.Zero, objectToProcess.Size, DefaultMaterial);
        }
        #endregion

        #region Cylinder
        public IRayTraceable GetIRayTraceableRecursive(Cylinder.CylinderPrimitive objectToProcess)
        {
            return new CylinderShape(objectToProcess.Radius1, objectToProcess.Height, DefaultMaterial);
        }
        #endregion

        #region Sphere
        public IRayTraceable GetIRayTraceableRecursive(Sphere objectToProcess)
        {
            throw new NotImplementedException();
#if false
            string info = "";
            if (objectToProcess.Name.Length > 0 && (objectToProcess.Name[0] == '#' || objectToProcess.Name[0] == '%'))
            {
                info = objectToProcess.Name[0] + " ";
            }

            info += "sphere(" + objectToProcess.Radius.ToString() + ");" + AddNameAsComment(objectToProcess);
            return ApplyIndent(info, level);
#endif
        }
        #endregion

        #region Transform
        public IRayTraceable GetIRayTraceableRecursive(TransformBase objectToProcess)
        {
            return new Transform(GetIRayTraceableRecursive((dynamic)objectToProcess.ObjectToTransform), objectToProcess.ActiveTransform);
        }
        #endregion

        #region Union
        public IRayTraceable GetIRayTraceableRecursive(Union objectToProcess)
        {
            List<IRayTraceable> items = new List<IRayTraceable>();
            foreach (CsgObject copiedObject in objectToProcess.AllObjects)
            {
                items.Add(GetIRayTraceableRecursive((dynamic)copiedObject));
            }

            return BoundingVolumeHierarchy.CreateNewHierachy(items);
        }
        #endregion

        #region Difference
        public IRayTraceable GetIRayTraceableRecursive(MatterHackers.Csg.Operations.Difference objectToProcess)
        {
            List<IRayTraceable> subtractItems = new List<IRayTraceable>();
            foreach (CsgObject copiedObject in objectToProcess.AllSubtracts)
            {
                subtractItems.Add(GetIRayTraceableRecursive((dynamic)copiedObject));
            }

            return new MatterHackers.RayTracer.Traceable.Difference(GetIRayTraceableRecursive((dynamic)objectToProcess.Primary), BoundingVolumeHierarchy.CreateNewHierachy(subtractItems));
        }
        #endregion

        #region Intersection
        public IRayTraceable GetIRayTraceableRecursive(Intersection objectToProcess)
        {
            throw new NotImplementedException();
            //return ApplyIndent("intersection()" + AddNameAsComment(objectToProcess) + "\n{\n" + GetRayTraceableRecursive((dynamic)objectToProcess.a, level + 1) + "\n" + GetRayTraceableRecursive((dynamic)objectToProcess.b, level + 1) + "\n}", level);
        }
        #endregion
        #endregion

        #region SCAD Formating Functions
        protected string AddNameAsComment(CsgObject objectToProcess)
        {
            if (objectToProcess.Name != "")
            {
                return " // " + objectToProcess.Name;
            }

            return "";
        }

        protected string ApplyIndent(string source, int level)
        {
            if (level > 0)
            {
                StringBuilder final = new StringBuilder();

                string[] splitOnReturn = source.Split('\n');
                for (int i = 0; i < splitOnReturn.Length; i++)
                {
                    final.Append(Spaces(4));
                    final.Append(splitOnReturn[i]);
                    if (i < splitOnReturn.Length - 1)
                    {
                        final.Append('\n');
                    }
                }

                return final.ToString();
            }

            return source;
        }

        string Spaces(int num)
        {
            StringBuilder spaces = new StringBuilder();
            for (int i = 0; i < num; i++)
            {
                spaces.Append(" ");
            }

            return spaces.ToString();
        }
        #endregion
    }
}
