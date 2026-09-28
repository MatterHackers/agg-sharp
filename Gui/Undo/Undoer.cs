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

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// A snapshot undo history (agg-gui's undo::Undoer, itself egui's Undoer&lt;State&gt;). Unlike
	/// <see cref="UndoBuffer"/>, which records commands, this is fed the whole current state over and over
	/// (<see cref="FeedState"/>) and decides for itself when to keep a snapshot: once the state has held still
	/// for <see cref="StableTime"/> seconds, or every <see cref="AutoSaveInterval"/> seconds while it keeps
	/// changing, so a burst of edits becomes one undo point. States compare with <see cref="object.Equals(object)"/>,
	/// so <typeparamref name="TState"/> should be a value type or a record.
	/// </summary>
	public class Undoer<TState>
	{
		private readonly LinkedList<TState> undos = new LinkedList<TState>();
		private readonly List<TState> redos = new List<TState>();
		private readonly EqualityComparer<TState> comparer = EqualityComparer<TState>.Default;

		// The change in progress: when it began, when the state last differed, and what it was then.
		private bool inFlux;
		private double fluxStart;
		private double latestChange;
		private TState latestState;

		/// <summary>The most undo points kept; the oldest go first.</summary>
		public int MaxUndos { get; set; } = 100;

		/// <summary>Seconds a changed state must hold still before it becomes an undo point.</summary>
		public double StableTime { get; set; } = 1;

		/// <summary>Seconds of continuous change after which the state is kept anyway.</summary>
		public double AutoSaveInterval { get; set; } = 30;

		/// <summary>True while the state differs from the latest undo point and has not settled yet. Keep feeding
		/// the state until this goes false, or the change never becomes an undo point.</summary>
		public bool IsInFlux => this.inFlux;

		/// <summary>True when <see cref="Undo"/> would change <paramref name="currentState"/>.</summary>
		public bool HasUndo(TState currentState)
		{
			return this.undos.Count switch
			{
				0 => false,
				1 => !this.IsLatest(currentState),
				_ => true,
			};
		}

		/// <summary>True when an undo has been made and the state has not changed since.</summary>
		public bool HasRedo(TState currentState) => this.redos.Count > 0 && this.IsLatest(currentState);

		/// <summary>
		/// Steps back: returns the state to show, or false when there is nothing to undo. An unsaved change is
		/// itself kept for redo, so undoing it returns to the latest undo point.
		/// </summary>
		public bool Undo(TState currentState, out TState previous)
		{
			previous = default;
			if (!this.HasUndo(currentState))
			{
				return false;
			}

			this.inFlux = false;
			if (this.IsLatest(currentState))
			{
				this.redos.Add(this.undos.Last.Value);
				this.undos.RemoveLast();
			}
			else
			{
				this.redos.Add(currentState);
			}

			previous = this.undos.Last.Value;
			return true;
		}

		/// <summary>
		/// Steps forward again: returns the state to show, or false when there is nothing to redo. A state edited
		/// since the undo drops the redo history.
		/// </summary>
		public bool Redo(TState currentState, out TState next)
		{
			next = default;
			if (this.undos.Count > 0 && !this.IsLatest(currentState))
			{
				this.redos.Clear();
				return false;
			}

			if (this.redos.Count == 0)
			{
				return false;
			}

			next = this.redos[this.redos.Count - 1];
			this.redos.RemoveAt(this.redos.Count - 1);
			this.undos.AddLast(next);
			return true;
		}

		/// <summary>Keeps <paramref name="currentState"/> as an undo point now, unless it already is the latest.</summary>
		public void AddUndo(TState currentState)
		{
			if (!this.IsLatest(currentState))
			{
				this.undos.AddLast(currentState);
			}

			while (this.undos.Count > this.MaxUndos)
			{
				this.undos.RemoveFirst();
			}

			this.inFlux = false;
		}

		/// <summary>
		/// Tells the undoer the state at <paramref name="currentTime"/> (seconds, any steady clock). Call it after
		/// every change and again while <see cref="IsInFlux"/>; the first call sets the baseline.
		/// </summary>
		public void FeedState(double currentTime, TState currentState)
		{
			if (this.undos.Count == 0)
			{
				this.AddUndo(currentState);
				return;
			}

			if (this.IsLatest(currentState))
			{
				this.inFlux = false;
				return;
			}

			this.redos.Clear();
			if (!this.inFlux)
			{
				this.inFlux = true;
				this.fluxStart = currentTime;
				this.latestChange = currentTime;
				this.latestState = currentState;
			}
			else if (this.comparer.Equals(this.latestState, currentState))
			{
				if (currentTime - this.latestChange >= this.StableTime)
				{
					this.AddUndo(currentState);
				}
			}
			else if (currentTime - this.fluxStart >= this.AutoSaveInterval)
			{
				this.AddUndo(currentState);
			}
			else
			{
				this.latestChange = currentTime;
				this.latestState = currentState;
			}
		}

		private bool IsLatest(TState state) => this.undos.Count > 0 && this.comparer.Equals(this.undos.Last.Value, state);
	}
}
