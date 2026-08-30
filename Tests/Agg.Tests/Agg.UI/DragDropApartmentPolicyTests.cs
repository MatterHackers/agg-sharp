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

using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg.UI;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// When a window is allowed to turn drag-drop on.
	/// </summary>
	/// <remarks>
	/// The rule is a Windows one - WinForms throws <c>ThreadStateException</c> out of
	/// <c>Control.SetAcceptDrops</c> unless the thread is STA, from inside handle creation where no caller
	/// can catch it - but the decision is a pure function and runs everywhere, which is the point of it
	/// taking the apartment as an argument. A CI shard was lost to the modal dialog that throw turns into;
	/// the leg that matters most here is the first one, because a regression that quietly answered false for
	/// STA would take drag-drop away from every real user and no Windows-only test of the MTA path would
	/// notice.
	/// </remarks>
	public class DragDropApartmentPolicyTests
	{
		[Test]
		public async Task AnStaThreadKeepsDragDrop()
		{
			// Every shipping head is [STAThread] - this is the path a user is on.
			await Assert.That(SystemWindow.ShouldEnableAllowDrop(true, ApartmentState.STA)).IsTrue();
		}

		[Test]
		public async Task AnMtaThreadGoesWithout()
		{
			// A window shown from a thread-pool thread: the case that wedged the suite.
			await Assert.That(SystemWindow.ShouldEnableAllowDrop(true, ApartmentState.MTA)).IsFalse()
				.Because("registering drag-drop from an MTA thread throws inside handle creation");
		}

		[Test]
		public async Task AnUnknownApartmentGoesWithout()
		{
			// What GetApartmentState answers off Windows, and what a thread that has not been initialized
			// answers on it. Neither is a promise of STA, and only a promise will do.
			await Assert.That(SystemWindow.ShouldEnableAllowDrop(true, ApartmentState.Unknown)).IsFalse();
		}

		[Test]
		public async Task TheApplicationsOwnSwitchStillWins()
		{
			// AutomationRunner turns EnableAllowDrop off around every run; an STA thread must not override it.
			await Assert.That(SystemWindow.ShouldEnableAllowDrop(false, ApartmentState.STA)).IsFalse();
			await Assert.That(SystemWindow.ShouldEnableAllowDrop(false, ApartmentState.MTA)).IsFalse();
		}
	}
}
