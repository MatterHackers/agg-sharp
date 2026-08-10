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
using System.Collections.Generic;

namespace MatterHackers.PolygonMesh.Csg
{
	/// <summary>
	/// One operand the kernel would not take, and why.
	/// </summary>
	public class SkippedBooleanOperand
	{
		public SkippedBooleanOperand(int index, string reason)
		{
			this.Index = index;
			this.Reason = reason;
		}

		/// <summary>
		/// The operand's position in the list handed to
		/// <see cref="BooleanProcessing.DoArray"/>, so the caller can name the part it came from.
		/// </summary>
		public int Index { get; }

		/// <summary>
		/// The kernel's complaint, including its status.
		/// </summary>
		public string Reason { get; }
	}

	/// <summary>
	/// A union that ran without some of its operands: the rest combined, and
	/// <see cref="PartialResult"/> is what they came to.
	/// </summary>
	/// <remarks>
	/// A failure rather than a return value on purpose. The rule the boolean layer has always
	/// kept is that an operand the kernel refused must never simply go missing - a boolean
	/// treats an error operand as empty geometry and still reports success, which reads as a
	/// part vanishing from the model with nothing logged. So the default outcome stays a throw,
	/// and every existing caller keeps today's behaviour by doing nothing. A caller that is
	/// prepared to show the user what was left out - and to keep those parts visible itself -
	/// catches this and takes <see cref="PartialResult"/>.
	/// <para>
	/// It derives from <see cref="InvalidOperationException"/> because that is what a refused
	/// operand has always thrown, so a handler written against the old behaviour still catches
	/// it, and <see cref="Exception.Message"/> still names every skipped operand.
	/// </para>
	/// </remarks>
	public class PartialBooleanException : InvalidOperationException
	{
		public PartialBooleanException(string message, Mesh partialResult, IReadOnlyList<SkippedBooleanOperand> skippedOperands)
			: base(message)
		{
			this.PartialResult = partialResult;
			this.SkippedOperands = skippedOperands;
		}

		/// <summary>
		/// The boolean over the operands that did import. Never null, so a caller can always copy
		/// the skipped parts into it - but empty when every operand was refused, which is the one
		/// case where the operands that "worked" are none of them.
		/// </summary>
		public Mesh PartialResult { get; }

		/// <summary>
		/// The operands that did not, in the order they were handed in.
		/// </summary>
		public IReadOnlyList<SkippedBooleanOperand> SkippedOperands { get; }
	}
}
