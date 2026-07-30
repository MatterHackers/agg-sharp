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
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THE
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

namespace MatterHackers.Agg.VertexSource
{
	/// <summary>
	/// Implemented by an <see cref="IVertexSource"/> that can name its own geometry: "the vertices I emit are
	/// a deterministic function of this key". Renderers that cache rasterized geometry use the key to
	/// recognise work they have already done.
	/// </summary>
	/// <remarks>
	/// Optional and additive - a source that cannot say anything stable about itself simply does not
	/// implement this, and is rasterized every time.
	/// <para>
	/// <b>The contract, which is a promise about the future and not just about now:</b> two sources whose
	/// identities are <see cref="object.Equals(object)"/>-equal must emit <b>identical vertices</b>, and a
	/// source that changes what it emits must return an identity that is no longer equal to the one it
	/// returned before. Everything that moves, adds or reshapes a vertex belongs in the key - including a
	/// position baked into the vertices, and including process-wide state the source consults while emitting
	/// them.
	/// </para>
	/// <para>
	/// <b>The hazard is silent.</b> An identity that misses an input does not fail, it serves the previous
	/// raster: text that keeps its old string, a glyph that keeps its old size. This is the same stale-key
	/// hazard <c>LcdMaskCache</c>'s <c>pathIdentity</c> carries, and the same rule answers it - a mutable
	/// path object is not an identity, a value describing what the source was asked to draw is.
	/// </para>
	/// <para>
	/// The identity says nothing about <b>where</b> the geometry is drawn from the renderer's point of view:
	/// a transform applied by the renderer, or by a wrapper around this source, is placement rather than
	/// shape and is accounted for separately. A translation the source bakes into its own vertices is not -
	/// that one is shape, because it is part of what this source emits.
	/// </para>
	/// <para>
	/// Return null to opt out for this call - a source that is currently in a state it cannot describe (an
	/// unset style, a path still being built) is better off unnamed than misnamed.
	/// </para>
	/// </remarks>
	public interface IVertexSourceRenderIdentity : IVertexSource
	{
		/// <summary>
		/// A value that is <see cref="object.Equals(object)"/>-equal exactly when this source's vertices are
		/// identical, or null to decline being identified. Must be immutable and a sensible
		/// <see cref="object.GetHashCode"/> implementor, since a cache holds on to it as a key.
		/// </summary>
		object RenderIdentity { get; }
	}
}
