/*
Copyright (c) 2019, Lars Brubaker, John Lewin
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

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ClipperLib;
using MatterHackers.VectorMath;

namespace MatterHackers.PolygonMesh.Csg
{
    using Polygon = List<IntPoint>;
    using Polygons = List<List<IntPoint>>;

    public class CsgDebugState
    {
        // Original properties
        public int CurrentMeshIndex { get; set; }
        public int CurrentFaceIndex { get; set; }
        public Mesh CurrentResultMesh { get; set; }
        public Polygons CurrentSlicePolygons { get; set; }
        public Plane? CurrentPlane { get; set; }
        public List<(int meshIndex, int faceIndex)> ProcessedFaces { get; set; } = new List<(int meshIndex, int faceIndex)>();
        public CsgModes Operation { get; set; }
        public Polygon CurrentFacePolygon { get; set; }
        public Polygons ClippingResult { get; set; }
        public string ProcessingAction { get; set; }
        public bool WasProcessed { get; set; }
        public string SkipReason { get; set; }

        // Coplanar face processing properties
        public string CoplanarProcessingPhase { get; set; }
        public List<int> CurrentMeshIndices { get; set; }
        public HashSet<int> FacesToRemove { get; set; }
        public int TotalCoplanarFacesProcessed { get; set; }
        public Dictionary<Plane, List<(int meshIndex, int faceIndex)>> CoplanarFaceGroups { get; set; }
        public Polygons KeepPolygons { get; set; }
        public Polygons RemovePolygons { get; set; }
        public string PolygonOperationDescription { get; set; }
        public Dictionary<int, Polygon> OriginalFacePolygons { get; set; } = new Dictionary<int, Polygon>();
        public int CurrentProcessingFaceIndex { get; set; }
        public ClipType? CurrentClipOperation { get; set; }
    }
}