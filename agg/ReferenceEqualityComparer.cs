using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace MatterHackers.Agg
{
	public sealed class ReferenceEqualityComparer
		: IEqualityComparer, IEqualityComparer<object>
	{
		public static readonly ReferenceEqualityComparer Default
			= new ReferenceEqualityComparer(); // JIT-lazy is sufficiently lazy imo.

		private ReferenceEqualityComparer()
		{
		}

		public bool Equals(object left, object right)
		{
			return left == right; // Reference identity comparison
		}

		public int GetHashCode(object obj)
		{
			return RuntimeHelpers.GetHashCode(obj);
		}
	}
}