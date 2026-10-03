/*
Copyright (c) 2026, Lars Brubaker
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
*/

using System.Collections.Generic;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;

namespace MatterHackers.RenderGl
{
    /// <summary>
    /// The 256 premultiplied-white alpha ramp images <see cref="Graphics2DGpu.PreRender"/> binds, one per alpha.
    /// </summary>
    internal static class GpuAlphaRampImages
    {
        // The anti-aliasing alpha ramp textures are cpu side ImageBuffers with no gl affinity, so they
        // are built once and never invalidated - only the gl textures made from them are context bound.
        // Volatile plus publish-when-complete so a racing thread can never see a half filled list.
        private static volatile List<ImageBuffer> images;
        private static readonly object imagesLock = new object();

        /// <summary>
        /// Builds (once) and returns the anti-aliasing alpha ramp images. Returns the list rather than
        /// leaving callers to read the field, so a caller can never index a field that changed between
        /// the check and the read.
        /// </summary>
        internal static List<ImageBuffer> Get()
        {
            var existing = images;
            if (existing != null) return existing;

            lock (imagesLock)
            {
                if (images != null) return images;

                // Fill a local list and publish it only when it is complete - a thumbnail worker and
                // the ui thread can both land here, and a partially filled list would index-fault.
                var textureImages = new List<ImageBuffer>();
                for (int i = 0; i < 256; i++)
                {
                    // Premultiplied white (PreRender blends One / OneMinusSrcAlpha): the texture holds these bytes as they are.
                    var texture = new ImageBuffer(1024, 4, 32, new BlenderPreMultBGRA());
                    textureImages.Add(texture);
                    var hardwarePixelBuffer = texture.GetBuffer();
                    for (int y = 0; y < 4; y++)
                    {
                        byte alpha = 0;
                        for (int x = 0; x < 1024; x++)
                        {
                            var index = (y * 1024 + x) * 4;
                            hardwarePixelBuffer[index + 0] = alpha;
                            hardwarePixelBuffer[index + 1] = alpha;
                            hardwarePixelBuffer[index + 2] = alpha;
                            hardwarePixelBuffer[index + 3] = alpha;
                            alpha = (byte)i;
                        }
                    }
                }

                images = textureImages;
                return textureImages;
            }
        }
    }
}
