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

using System.Collections.Generic;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests
{
	// SafeList's whole job is to let one thread swap a new list into place while other threads are
	// reading the old one. That only holds if nobody but Modify can touch the list that is live.
	//
	// It did not hold: Modify published the very list it had just handed to the modifier. Anything that
	// kept a reference to that list - most importantly an `async` lambda bound to Action<List<T>>, which
	// is async void and so lets Modify return (and publish) at its first await while the continuation
	// keeps calling Add - was then mutating the LIVE list from outside Modify. Because List<T>.Add
	// publishes the incremented Count before it stores the element, a concurrent reader (the next
	// Modify's `new List<T>(items)`, which copies via ICollection.CopyTo and so skips the enumerator's
	// version check) could capture a null element. In an Object3D tree that null child then threw a bare
	// NullReferenceException from Object3DExtensions.DescendantsAndSelf on whatever background rebuild
	// thread walked it next - an unhandled exception on a thread pool thread, i.e. a dead process.
	public class SafeListTests
	{
		[Test]
		public async Task ModifyPublishesAListTheModifierCannotStillReach()
		{
			var safeList = new SafeList<string>();

			// Stand in for the leaked reference an async modifier's continuation holds.
			List<string> modifierList = null;

			safeList.Modify(list =>
			{
				list.Add("published");
				modifierList = list;
			});

			modifierList.Add("after Modify returned");

			await Assert.That(safeList.Count).IsEqualTo(1)
				.Because("the published list must be private to SafeList, so a leaked reference cannot mutate it");
		}

		[Test]
		public async Task ModifyStillPublishesEverythingTheModifierDidDuringTheCall()
		{
			var safeList = new SafeList<string>();
			safeList.Add("keep");

			safeList.Modify(list =>
			{
				list.Add("added");
				list.Remove("keep");
			});

			await Assert.That(safeList.Count).IsEqualTo(1);
			await Assert.That(safeList.Contains("added")).IsTrue();
			await Assert.That(safeList.Contains("keep")).IsFalse();
		}

		[Test]
		public async Task ReadersEnumeratingWhileModifyRunsSeeTheOldListIntact()
		{
			var safeList = new SafeList<string>();
			safeList.Add("first");
			safeList.Add("second");

			var seen = new List<string>();

			// Start enumerating, swap the list out mid-walk, then finish the walk. The in-flight reader
			// must keep walking the snapshot it started on rather than observing a torn list.
			using (var enumerator = safeList.GetEnumerator())
			{
				enumerator.MoveNext();
				seen.Add(enumerator.Current);

				safeList.Modify(list => list.Clear());

				while (enumerator.MoveNext())
				{
					seen.Add(enumerator.Current);
				}
			}

			await Assert.That(seen.Count).IsEqualTo(2);
			await Assert.That(safeList.Count).IsEqualTo(0);
		}
	}
}
