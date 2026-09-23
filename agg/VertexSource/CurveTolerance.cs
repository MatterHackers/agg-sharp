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

namespace MatterHackers.Agg.VertexSource
{
	/// <summary>
	/// How coarse a curve may be cut, by the size of the shape it belongs to: the absolute tolerance a caller
	/// always used while the shape is at or under <see cref="DeskSize"/>, and that tolerance grown in proportion
	/// to the shape above it.
	/// </summary>
	/// <remarks>
	/// <para>Every flattening and offset tolerance in agg is an ABSOLUTE distance, and the segments it puts in a
	/// curve go as <c>sqrt(size / tolerance)</c>. Held fixed while a design grows from millimetres to feet, the
	/// same shape is cut about <c>sqrt(304.8)</c> = 17.5x finer - huge meshes and slow rebuilds for a part that
	/// has not asked for any more detail. Growing the tolerance with the size makes the cut scale-free above the
	/// threshold: a 20 ft circle gets the segments a 300 mm one does.</para>
	/// <para>At or under the threshold the scale is EXACTLY 1, so <c>base * scale</c> and <c>base / scale</c>
	/// are bit-identical to the old absolute value and no stored desk-size mesh moves.</para>
	/// </remarks>
	public static class CurveTolerance
	{
		/// <summary>
		/// The size at which a shape stops being "desk size" - a printer bed, in the shape's own units
		/// (millimetres in MatterCAD).
		/// </summary>
		/// <remarks>
		/// 300 because it is the bed of the largest common desktop printers, so every part a user can print in
		/// one piece keeps its shipped resolution; and because MatterCAD's path offsets (ScaleAwareOffset)
		/// already anchor on it, so one shape is cut at one quality whether its round comes from a bezier or an
		/// offset join.
		/// </remarks>
		public const double DeskSize = 300;

		/// <summary>
		/// How many times coarser than the absolute default a shape <paramref name="size"/> across may be cut:
		/// exactly 1 at or under <see cref="DeskSize"/>, <c>size / DeskSize</c> above it.
		/// </summary>
		/// <remarks>A size that is not a finite number (no shape, broken bounds) reads as desk size.</remarks>
		public static double ScaleFor(double size)
		{
			return double.IsFinite(size) && size > DeskSize ? size / DeskSize : 1;
		}

		/// <summary>
		/// The tolerance to use for a shape <paramref name="size"/> across whose desk-size tolerance is
		/// <paramref name="baseTolerance"/>.
		/// </summary>
		public static double ForSize(double baseTolerance, double size)
		{
			return baseTolerance * ScaleFor(size);
		}
	}
}
