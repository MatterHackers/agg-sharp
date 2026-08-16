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

using System;

namespace MatterHackers.RenderGl.Compat
{
	/// <summary>
	/// One <c>gl*Pointer</c> binding: where the client array lives and how to walk it.
	/// </summary>
	public readonly struct GlClientArrayPointer
	{
		/// <summary>Creates a client array binding.</summary>
		/// <param name="size">Components per element, as GL counts them.</param>
		/// <param name="stride">Bytes between elements; 0 means tightly packed.</param>
		/// <param name="pointer">Address of the first element.</param>
		public GlClientArrayPointer(int size, int stride, IntPtr pointer)
		{
			this.Size = size;
			this.Stride = stride;
			this.Pointer = pointer;
		}

		/// <summary>Components per element.</summary>
		public int Size { get; }

		/// <summary>Bytes between elements; 0 means tightly packed.</summary>
		public int Stride { get; }

		/// <summary>Address of the first element.</summary>
		public IntPtr Pointer { get; }

		/// <summary>True when an array has actually been bound.</summary>
		public bool IsSet => this.Pointer != IntPtr.Zero;
	}

	/// <summary>
	/// The four client array bindings GL's fixed function pipeline knows about. They are shadowed
	/// because they are state a caller can read back, but nothing consumes them yet - the draw calls
	/// that would are the legacy mesh fallback the port plan closes in the native scene renderer rather
	/// than reimplementing here.
	/// </summary>
	public class GlClientArrayPointers
	{
		/// <summary>The vertex position array.</summary>
		public GlClientArrayPointer Vertex { get; set; }

		/// <summary>The vertex color array.</summary>
		public GlClientArrayPointer Color { get; set; }

		/// <summary>The texture coordinate array.</summary>
		public GlClientArrayPointer TexCoord { get; set; }

		/// <summary>The normal array.</summary>
		public GlClientArrayPointer Normal { get; set; }
	}
}
