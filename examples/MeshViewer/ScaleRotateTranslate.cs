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
using System.ComponentModel;
using System.IO;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.OpenGlGui;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.PolygonMesh;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.RenderOpenGl;
using MatterHackers.VectorMath;

namespace MatterHackers.MeshVisualizer
{
    public struct ScaleRotateTranslate
    {
        public Matrix4X4 centering;
        public Matrix4X4 scale;
        public Matrix4X4 rotation;
        public Matrix4X4 translation;

        public ScaleRotateTranslate(Matrix4X4 scale, Matrix4X4 rotation, Matrix4X4 translation)
        {
            centering = Matrix4X4.Identity;
            this.scale = scale;
            this.rotation = rotation;
            this.translation = translation;
        }

        public Matrix4X4 TotalTransform
        {
            get
            {
                return centering * scale * rotation * translation;
            }
        }

        public static ScaleRotateTranslate Identity()
        {
            ScaleRotateTranslate identity = new ScaleRotateTranslate();
            identity.centering = Matrix4X4.Identity;
            identity.scale = Matrix4X4.Identity;
            identity.rotation = Matrix4X4.Identity;
            identity.translation = Matrix4X4.Identity;
            return identity;
        }

        public static ScaleRotateTranslate CreateTranslation(double x, double y, double z)
        {
            ScaleRotateTranslate translation = ScaleRotateTranslate.Identity();
            translation.translation = Matrix4X4.CreateTranslation(x, y, z);
            return translation;
        }

        public void SetCenteringForMeshGroup(MeshGroup meshGroup)
        {
            AxisAlignedBoundingBox bounds = meshGroup.GetAxisAlignedBoundingBox();
            Vector3 boundsCenter = (bounds.maxXYZ + bounds.minXYZ) / 2;
            centering = Matrix4X4.CreateTranslation(-boundsCenter);
        }
    }
}

