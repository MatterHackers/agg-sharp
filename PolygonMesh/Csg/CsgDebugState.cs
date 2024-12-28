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
        public List<IntPoint> CurrentFacePolygon { get; set; }
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
        public Dictionary<int, List<IntPoint>> OriginalFacePolygons { get; set; } = new Dictionary<int, List<IntPoint>>();
        public int CurrentProcessingFaceIndex { get; set; }
        public ClipType? CurrentClipOperation { get; set; }

        public CsgDebugState DeepCopy()
        {
            var copy = new CsgDebugState
            {
                // Copy original properties
                CurrentMeshIndex = this.CurrentMeshIndex,
                CurrentFaceIndex = this.CurrentFaceIndex,
                CurrentResultMesh = this.CurrentResultMesh?.Copy(CancellationToken.None),
                CurrentSlicePolygons = this.CurrentSlicePolygons?.Select(p => new List<IntPoint>(p)).ToList(),
                CurrentPlane = this.CurrentPlane,
                ProcessedFaces = new List<(int meshIndex, int faceIndex)>(this.ProcessedFaces),
                Operation = this.Operation,
                CurrentFacePolygon = this.CurrentFacePolygon != null ? new List<IntPoint>(this.CurrentFacePolygon) : null,
                ClippingResult = this.ClippingResult?.Select(p => new List<IntPoint>(p)).ToList(),
                ProcessingAction = this.ProcessingAction,
                WasProcessed = this.WasProcessed,
                SkipReason = this.SkipReason,

                // Copy coplanar processing properties
                CoplanarProcessingPhase = this.CoplanarProcessingPhase,
                CurrentMeshIndices = this.CurrentMeshIndices != null ? new List<int>(this.CurrentMeshIndices) : null,
                FacesToRemove = this.FacesToRemove != null ? new HashSet<int>(this.FacesToRemove) : null,
                TotalCoplanarFacesProcessed = this.TotalCoplanarFacesProcessed,
                KeepPolygons = this.KeepPolygons?.Select(p => new List<IntPoint>(p)).ToList(),
                RemovePolygons = this.RemovePolygons?.Select(p => new List<IntPoint>(p)).ToList(),
                PolygonOperationDescription = this.PolygonOperationDescription,
                CurrentProcessingFaceIndex = this.CurrentProcessingFaceIndex,
                CurrentClipOperation = this.CurrentClipOperation
            };

            // Deep copy the CoplanarFaceGroups dictionary
            if (this.CoplanarFaceGroups != null)
            {
                copy.CoplanarFaceGroups = new Dictionary<Plane, List<(int meshIndex, int faceIndex)>>();
                foreach (var kvp in this.CoplanarFaceGroups)
                {
                    copy.CoplanarFaceGroups[kvp.Key] = new List<(int meshIndex, int faceIndex)>(kvp.Value);
                }
            }

            // Deep copy the OriginalFacePolygons dictionary
            if (this.OriginalFacePolygons != null)
            {
                copy.OriginalFacePolygons = new Dictionary<int, List<IntPoint>>();
                foreach (var kvp in this.OriginalFacePolygons)
                {
                    copy.OriginalFacePolygons[kvp.Key] = new List<IntPoint>(kvp.Value);
                }
            }

            return copy;
        }
    }
}