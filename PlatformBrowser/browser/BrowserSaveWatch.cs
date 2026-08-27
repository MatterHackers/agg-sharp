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

namespace MatterHackers.Agg.Platform.Browser
{
	/// <summary>What one poll of a save dialog's staging path concludes.</summary>
	public enum SaveWatchDecision
	{
		/// <summary>Nothing yet, or still being written. Poll again.</summary>
		KeepWaiting,

		/// <summary>The file is there and has stopped changing; hand it to the browser.</summary>
		Download,

		/// <summary>Long enough. Stop polling and clean up.</summary>
		GiveUp,
	}

	/// <summary>
	/// Decides when a file an application is writing to a save dialog's staging path is finished, so it can
	/// be handed to the browser as a download.
	/// </summary>
	/// <remarks>
	/// <para><b>Why there is anything to decide.</b> Saving in a page is a download, and a download needs the
	/// bytes - but <see cref="IFileDialogProvider.SaveFileDialog"/> hands the caller a <em>path</em> and then
	/// has no further part in the transaction. Every desktop host is done at that point, because the path is
	/// a real file and the OS owns what happens next. Here nothing happens next: the file sits in the wasm
	/// virtual file system, invisible to the user, until something notices it is complete.</para>
	/// <para><b>The v1 mechanism, and it is a choice among poor options.</b> The provider polls the staging
	/// path from <c>UiThread</c>'s idle queue and calls the file finished when it exists and has stopped
	/// growing. The alternative considered was a completion hook owned by the head - the application telling
	/// the provider "I have finished writing that one" - which is exact rather than heuristic, but needs a
	/// call added at every save site in an application that has many, and would silently never download for
	/// any caller that had not been updated. Polling works for every existing caller unchanged, which is
	/// what makes it the v1 answer. It is written down here as the thing to revisit if a save ever needs to
	/// be exact - see the wasm plan's open decisions.</para>
	/// <para><b>Why "stopped growing" and not "the handle closed".</b> There is no way to ask the wasm file
	/// system whether a file is still open. A writer that stalls for longer than the settle window - a slow
	/// serializer between two flushes - would be called finished early, which is the known hole in this. The
	/// settle window is deliberately several polls rather than one so an ordinary buffered write cannot fall
	/// through it.</para>
	/// </remarks>
	public sealed class BrowserSaveWatch
	{
		/// <summary>
		/// How long between polls. Idle work runs on the animation frame tick, so this is a lower bound;
		/// a quarter second is short enough that a save feels immediate and long enough that a writer
		/// between two buffer flushes is very unlikely to look idle.
		/// </summary>
		public const double PollIntervalSeconds = 0.25;

		/// <summary>
		/// How many consecutive polls must see the same size before the file is called finished. Two - so
		/// the file has to be unchanged across half a second - rather than one, which any pause between
		/// writes would satisfy.
		/// </summary>
		public const int StablePollsBeforeDownload = 2;

		/// <summary>
		/// How long to keep watching a staging path before giving up on it, in seconds.
		/// </summary>
		/// <remarks>
		/// Generous on purpose: the thing being waited for is an application writing a file, and a large
		/// mesh export legitimately takes minutes. The cost of waiting too long is one queued idle action
		/// per quarter second; the cost of giving up too early is a save that silently never downloads.
		/// </remarks>
		public const double GiveUpSeconds = 300;

		/// <summary>The size seen last poll. Negative until the file has been seen at all, so that the first
		/// sighting can never be counted as a stable one.</summary>
		private long lastLength = -1;

		private int stablePolls;

		/// <summary>How many polls have concluded the size was unchanged. Diagnostics and tests.</summary>
		public int StablePolls => this.stablePolls;

		/// <summary>
		/// Folds one look at the staging path into a decision.
		/// </summary>
		/// <param name="exists">Whether the file is there yet.</param>
		/// <param name="length">Its size in bytes; ignored when it does not exist.</param>
		/// <param name="elapsedSeconds">How long the watch has been running.</param>
		public SaveWatchDecision Observe(bool exists, long length, double elapsedSeconds)
		{
			if (exists)
			{
				if (length == this.lastLength)
				{
					this.stablePolls++;

					if (this.stablePolls >= StablePollsBeforeDownload)
					{
						// Checked before the give-up below: a file that settled on the very poll that ran out
						// of time is still a finished file, and throwing it away would be perverse.
						return SaveWatchDecision.Download;
					}
				}
				else
				{
					this.lastLength = length;
					this.stablePolls = 0;
				}
			}

			return elapsedSeconds >= GiveUpSeconds ? SaveWatchDecision.GiveUp : SaveWatchDecision.KeepWaiting;
		}
	}
}
