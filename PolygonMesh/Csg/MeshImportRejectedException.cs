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

using System;

using RustStatus = ManifoldSharp.Error;

namespace MatterHackers.PolygonMesh.Csg
{
	/// <summary>
	/// The kernel looked at one operand's geometry and would not take it, with
	/// <see cref="Status"/> saying what it objected to.
	/// </summary>
	/// <remarks>
	/// Its own type so the union's degrade path can skip exactly this and nothing else. The
	/// alternative - catching <see cref="InvalidOperationException"/> around the import - also
	/// caught a library load or handle failure and reported it as geometry the user should run
	/// Repair on, which is a lie about what went wrong and sends them to fix a part that is fine.
	/// <para>
	/// Every status the kernel refuses an import with is a judgement about that one operand's
	/// geometry - not closed, a coordinate that is not a number, indices out of range - so they
	/// are all per-operand skippable, and there is no need to enumerate which ones.
	/// </para>
	/// <para>
	/// It derives from <see cref="InvalidOperationException"/> because that is what a refused
	/// operand has always thrown, so callers written against the old behaviour keep catching it.
	/// </para>
	/// </remarks>
	internal class MeshImportRejectedException : InvalidOperationException
	{
		public MeshImportRejectedException(string message, RustStatus status)
			: base(message)
		{
			this.Status = status;
		}

		/// <summary>
		/// What the kernel objected to.
		/// </summary>
		public RustStatus Status { get; }
	}
}
