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

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using MatterHackers.Agg.LcdCoverage;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.Agg
{
    /// <summary>
    /// How <see cref="Graphics2D"/>'s cached LCD fill path places a fill: finding the source that names its
    /// own geometry and the full transform to device pixels, and normalizing the sub-pixel phase it keys
    /// masks by. Pure functions of their arguments.
    /// </summary>
    internal static class IdentifiedFillPlacement
    {
        /// <summary>
        /// Collapses a sub-pixel phase of -0.0 onto +0.0, leaving every other value exactly as it is.
        /// </summary>
        /// <remarks>
        /// The two are the same placement, but <see cref="LcdMaskKey"/> compares its doubles by bit pattern,
        /// so an unnormalized -0.0 files a second entry holding bytes identical to the first one's. It takes a
        /// transform whose translation is already -0.0 to get here - subtracting the whole part from any other
        /// value yields +0.0 - and the affine multiply in <see cref="TryUnwrap"/> turns most
        /// of those into +0.0 on the way past, so this is a guard rather than a fix for an observed duplicate.
        /// It is one comparison on a path that is about to rasterize, which is a fair price for not having to
        /// reason about which mirrored or sheared transform survives that multiply with its sign intact.
        /// <para>
        /// Written as a comparison rather than <c>+ 0.0</c> because that trick is only a no-op for every value
        /// <i>other</i> than -0.0, and relying on a compiler not to fold away an addition it is entitled to
        /// consider redundant is the kind of thing that stops being true silently.
        /// </para>
        /// </remarks>
        internal static double NormalizeZeroPhase(double phase)
        {
            // -0.0 == 0 is true, so this catches both zeros and hands back the positive one.
            return phase == 0 ? 0.0 : phase;
        }

        /// <summary>
        /// Finds the source that names its own geometry inside <paramref name="vertexSource"/> and the full
        /// path-space-to-device transform that applies to it, or answers false when there is no such source.
        /// </summary>
        /// <param name="vertexSource">The source handed to <see cref="Graphics2D.Render(IVertexSource, IColorType)"/>, possibly wrapped.</param>
        /// <param name="identifiedSource">The source that names itself.</param>
        /// <param name="currentTransform">The surface's current transform (<see cref="Graphics2D.GetTransform"/>).</param>
        /// <param name="transform">Everything between that source's own vertices and device pixels: the
        /// wrappers' transforms followed by the current transform.</param>
        /// <remarks>
        /// <b>How identity and wrappers compose.</b> A <see cref="VertexSourceApplyTransform"/> holding an
        /// <see cref="Affine"/> contributes placement, not shape - it moves vertices without changing which
        /// vertices they are - so it joins the transform and leaves the identity underneath it intact. That
        /// is what makes the split work for text: <see cref="Font.TypeFacePrinter"/> hands
        /// <see cref="Graphics2D.Render(IVertexSource, IColorType)"/> either itself or itself wrapped in the whole-device-pixel baseline nudge, and
        /// the two are the same run at two placements rather than two runs.
        /// <para>
        /// Every other proxy - a <see cref="Stroke"/>, a curve flattener - produces different vertices than
        /// the source it wraps, and cannot claim that source's identity. The walk stops at the first one, and
        /// the fill is rendered the ordinary way. So does a non-affine <see cref="ITransform"/>, whose effect
        /// is not a matrix that can be folded into the current one.
        /// </para>
        /// </remarks>
        internal static bool TryUnwrap(IVertexSource vertexSource, Affine currentTransform, out IVertexSourceRenderIdentity identifiedSource, out Affine transform)
        {
            identifiedSource = null;
            transform = default;

            IVertexSource source = vertexSource;
            Affine wrappers = Affine.NewIdentity();
            while (source is VertexSourceApplyTransform applyTransform
                && applyTransform.TransformToApply is Affine affine
                && applyTransform.VertexSource != null)
            {
                // agg-sharp's operator * is a post-multiply ("a then b"), and a wrapper found further in is
                // applied before every wrapper already collected outside it.
                wrappers = affine * wrappers;
                source = applyTransform.VertexSource;
            }

            if (!(source is IVertexSourceRenderIdentity identifiable))
            {
                return false;
            }

            identifiedSource = identifiable;
            transform = wrappers * currentTransform;
            return true;
        }
    }
}
