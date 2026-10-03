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

namespace MatterHackers.RenderGl.OpenGl
{
    /// <summary>
    /// The push/pop bookkeeping behind <see cref="GL.PushMatrix"/>, <see cref="GL.PopMatrix"/>,
    /// <see cref="GL.PushAttrib"/> and <see cref="GL.PopAttrib"/>: counts the outstanding pushes per
    /// matrix mode and throws when a caller runs away with them, which the classic GL stack (32 deep on
    /// most drivers) would have done with a GL_STACK_OVERFLOW.
    /// </summary>
    /// <remarks>
    /// The counts are per frame, not per process. A widget draw that throws between a push and its pop
    /// leaves the pushes outstanding, and the host abandons only that frame - so without
    /// <see cref="ResetForFrame"/> at the top of the next one, a draw that throws every frame (a scene
    /// renderer that cannot submit, say) carries a few leaked pushes forward each time until the counter
    /// trips here at the first push of a frame, before any widget has drawn. From then on every frame
    /// dies at that same push - including the one that would have drawn the dialog asking whether to
    /// save - and the window is black until the process is killed. One bad frame has to cost one frame.
    /// </remarks>
    public class GlStackBalance
    {
        /// <summary>How deep a matrix stack may go: the classic GL minimum for the model-view stack.</summary>
        public const int MaxMatrixDepth = 32;

        /// <summary>How many attribute pushes may be outstanding at once.</summary>
        public const int MaxAttribDepth = 100;

        private readonly Dictionary<MatrixMode, int> pushMatrixCount = new Dictionary<MatrixMode, int>()
        {
            [OpenGl.MatrixMode.Modelview] = 0,
            [OpenGl.MatrixMode.Projection] = 0,
        };

        private int pushAttribCount;

        /// <summary>The stack the next push or pop acts on.</summary>
        public MatrixMode MatrixMode { get; set; } = OpenGl.MatrixMode.Modelview;

        /// <summary>Outstanding pushes on <paramref name="mode"/>'s stack.</summary>
        public int MatrixDepth(MatrixMode mode) => this.pushMatrixCount.TryGetValue(mode, out int depth) ? depth : 0;

        /// <summary>Outstanding attribute pushes.</summary>
        public int AttribDepth => this.pushAttribCount;

        /// <summary>Counts a push on the current matrix stack.</summary>
        public void PushMatrix()
        {
            this.pushMatrixCount[this.MatrixMode] = this.MatrixDepth(this.MatrixMode) + 1;
            if (this.pushMatrixCount[this.MatrixMode] > MaxMatrixDepth)
            {
                throw new Exception("PushMatrix being called without matching PopMatrix");
            }
        }

        /// <summary>Counts a pop on the current matrix stack.</summary>
        public void PopMatrix()
        {
            this.pushMatrixCount[this.MatrixMode] = this.MatrixDepth(this.MatrixMode) - 1;
            if (this.pushMatrixCount[this.MatrixMode] < 0)
            {
                throw new Exception("popMatrib called too many times.");
            }
        }

        /// <summary>Counts an attribute push.</summary>
        public void PushAttrib()
        {
            this.pushAttribCount++;
            if (this.pushAttribCount > MaxAttribDepth)
            {
                throw new Exception("pushAttrib being called without matching PopAttrib");
            }
        }

        /// <summary>Counts an attribute pop.</summary>
        public void PopAttrib() => this.pushAttribCount--;

        /// <summary>
        /// Forgets every outstanding push: the frame that made them is over, whether it finished or was
        /// abandoned by a throw. See the remarks on this class for why this is not optional.
        /// </summary>
        public void ResetForFrame()
        {
            foreach (var mode in new List<MatrixMode>(this.pushMatrixCount.Keys))
            {
                this.pushMatrixCount[mode] = 0;
            }

            this.pushAttribCount = 0;
            this.MatrixMode = OpenGl.MatrixMode.Modelview;
        }
    }
}
