/*
Copyright (c) 2014, Lars Brubaker
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

using MatterHackers.Csg.Solids;
using MatterHackers.Csg.Operations;
using MatterHackers.Csg.Transform;

namespace MatterHackers.Csg.Processors
{
    public class CopyAndFlatten
    {
        static public CsgObject ProcessObject(CsgObject objectToCopyAndFlatten)
        {
            CopyAndFlatten instance = new CopyAndFlatten();
            return instance.DoCopyAndFlatten((dynamic)objectToCopyAndFlatten);
        }

        public CopyAndFlatten()
        {
        }

        #region Visitor Patern Functions
        public CsgObject DoCopyAndFlatten(CsgObject objectToProcess)
        {
            throw new Exception("You must write the specialized function for this type.");
        }

        #region PrimitiveWrapper
        public CsgObject DoCopyAndFlatten(CsgObjectWrapper objectToProcess)
        {
            return DoCopyAndFlatten((dynamic)objectToProcess.root);
        }
        #endregion

        #region Box
        public CsgObject DoCopyAndFlatten(Box.BoxPrimitive objectToProcess)
        {
            return new Box.BoxPrimitive(objectToProcess);
        }
        #endregion

        #region Cylinder
        public CsgObject DoCopyAndFlatten(Cylinder.CylinderPrimitive objectToProcess)
        {
            return new Cylinder.CylinderPrimitive(objectToProcess);
        }
        #endregion

        #region Sphere
        public CsgObject DoCopyAndFlatten(Sphere objectToProcess)
        {
            return new Sphere(objectToProcess);
        }
        #endregion

        #region Transform
        public CsgObject DoCopyAndFlatten(TransformBase objectToProcess)
        {
            return new TransformBase(DoCopyAndFlatten((dynamic)objectToProcess.objectToTransform), objectToProcess.transform, objectToProcess.Name);
        }
        #endregion

        #region Union
        public CsgObject DoCopyAndFlatten(Union objectToProcess)
        {
            Union unionCopy = new Union(objectToProcess.Name);
            foreach (CsgObject copiedObject in objectToProcess.allObjects)
            {
                unionCopy += DoCopyAndFlatten((dynamic)copiedObject);
            }
            return unionCopy;
        }
        #endregion

        #region Difference
        public CsgObject DoCopyAndFlatten(Difference objectToProcess)
        {
            Difference differenceCopy = new Difference(DoCopyAndFlatten((dynamic)objectToProcess.primary), objectToProcess.primary.Name);
            foreach (CsgObject copiedObject in objectToProcess.allSubtracts)
            {
                differenceCopy -= DoCopyAndFlatten((dynamic)copiedObject);
            }
            return differenceCopy;
        }
        #endregion

        #region Intersection
        public CsgObject DoCopyAndFlatten(Intersection objectToProcess)
        {
            return new Intersection(objectToProcess);
        }
        #endregion
        #endregion
    }
}
