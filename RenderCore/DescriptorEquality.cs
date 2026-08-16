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
using System.Collections.Generic;

namespace MatterHackers.RenderCore
{
	/// <summary>
	/// Element-wise equality and hashing for the arrays inside descriptors. Descriptors are cache keys
	/// (pipeline permutations, bind groups), so their arrays have to compare by content - the default
	/// struct equality would compare the array references and miss every cache hit.
	/// </summary>
	internal static class DescriptorEquality
	{
		/// <summary>True when both arrays hold the same elements in the same order. Null counts as empty.</summary>
		public static bool ArrayEquals<T>(T[] left, T[] right)
			where T : IEquatable<T>
		{
			if (ReferenceEquals(left, right))
			{
				return true;
			}

			int leftLength = left?.Length ?? 0;
			int rightLength = right?.Length ?? 0;
			if (leftLength != rightLength)
			{
				return false;
			}

			for (int i = 0; i < leftLength; i++)
			{
				if (!EqualityComparer<T>.Default.Equals(left[i], right[i]))
				{
					return false;
				}
			}

			return true;
		}

		/// <summary>A content hash matching <see cref="ArrayEquals"/>. Null and empty hash alike.</summary>
		public static int ArrayHash<T>(T[] values)
			where T : IEquatable<T>
		{
			var hash = default(HashCode);
			if (values != null)
			{
				foreach (var value in values)
				{
					hash.Add(value);
				}
			}

			return hash.ToHashCode();
		}
	}
}
