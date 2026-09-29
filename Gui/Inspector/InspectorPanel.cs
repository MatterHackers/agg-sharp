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
using System.Linq;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// agg-gui's widget inspector panel (widgets/inspector): a tinted header bar titled "Widget Inspector" with the
	/// widget count at its right, the widget tree
	/// (<see cref="InspectorTreeView"/>) and below it the selected widget's properties
	/// (<see cref="InspectorPropertiesView"/>). Hovering a tree row highlights that widget on top of the app and
	/// clicking selects it (<see cref="InspectorOverlay"/>). The split between tree and properties drags
	/// (<see cref="InspectorSplitBar"/>), and <see cref="SavedState"/> / <see cref="ApplySavedState"/> carry the tree's
	/// expansion, the selection and the split across runs.
	/// </summary>
	/// <remarks>
	/// The tree is re-collected on a short timer while the panel is in a window, which keeps it live (agg-gui
	/// re-collects every frame) without a full walk of the app on every frame drawn; the tree and the highlight only
	/// repaint when the refresh found a change. Automation names start "Inspector ".
	/// </remarks>
	public class InspectorPanel : FlowLayoutWidget
	{
		/// <summary>How often the tree is re-collected, in seconds.</summary>
		public const double RefreshSeconds = .25;

		/// <summary>agg-gui's HEADER_H, in logical units.</summary>
		public const double HeaderHeight = 30;

		/// <summary>agg-gui's DEFAULT_PROPS_H, in logical units.</summary>
		public const double DefaultPropertiesHeight = 180;

		/// <summary>agg-gui's MIN_PROPS_H and MIN_TREE_H: the least either side of the split keeps, in logical units.</summary>
		public const double MinPaneHeight = 60;

		private double propertiesHeight = DefaultPropertiesHeight;

		private InspectorSavedState pendingState;

		private InspectorSavedState lastState;

		private readonly GuiWidget header;

		private readonly TextWidget headerTitle;

		private readonly TextWidget headerCount;

		private readonly InspectorOverlay overlay;

		private readonly List<InspectorEdit> pendingEdits = new List<InspectorEdit>();

		private RunningInterval refreshInterval;

		private InspectorStyle style = new InspectorStyle();

		/// <param name="inspectedRoot">The tree to show; null shows the SystemWindow the panel is in.</param>
		public InspectorPanel(GuiWidget inspectedRoot = null)
			: base(FlowDirection.TopToBottom)
		{
			this.InspectedRoot = inspectedRoot;
			this.Name = "Inspector Panel";
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Stretch;

			this.Model = new InspectorModel();
			this.overlay = new InspectorOverlay(this.Model);

			// agg-gui's HEADER_H bar: the 13px title 12 in from the left, the 11px dim count 10 in from the right
			// (9.75 and 8.25 points), over a line in the separator colour.
			this.header = new GuiWidget()
			{
				Name = "Inspector Header",
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Absolute,
				Height = HeaderHeight * DeviceScale,
			};
			this.header.AfterDraw += (s, e) => e.Graphics2D.FillRectangle(0, 0, this.header.Width, DeviceScale, this.style.Separator);
			this.headerTitle = new TextWidget("Widget Inspector", pointSize: 9.75)
			{
				Name = "Inspector Title",
				HAnchor = HAnchor.Left,
				VAnchor = VAnchor.Center,
				Margin = new BorderDouble(left: 12),
			};
			this.header.AddChild(this.headerTitle);
			this.headerCount = new TextWidget("", pointSize: 8.25)
			{
				Name = "Inspector Widget Count",
				HAnchor = HAnchor.Right,
				VAnchor = VAnchor.Center,

				// the count arrives once the tree is collected, so the label grows with it
				AutoExpandBoundsToText = true,
				Margin = new BorderDouble(right: 10),
			};
			this.header.AddChild(this.headerCount);
			this.AddChild(this.header);

			this.Tree = new InspectorTreeView(this.Model, this.style);
			this.AddChild(this.Tree);

			this.SplitBar = new InspectorSplitBar(this);
			this.AddChild(this.SplitBar);

			this.Properties = new InspectorPropertiesView(this.Model, this.style);
			this.Properties.EditRequested += this.QueueEdit;
			this.AddChild(this.Properties);

			this.Model.Changed += this.Model_Changed;
			this.ApplyStyle();
			this.SizeProperties();
			this.refreshInterval = UiThread.SetInterval(this.RefreshNow, RefreshSeconds);
		}

		/// <summary>The tree to show; null follows the SystemWindow the panel is in (none while it is not in one).</summary>
		public GuiWidget InspectedRoot { get; set; }

		public InspectorModel Model { get; }

		public InspectorTreeView Tree { get; }

		public InspectorPropertiesView Properties { get; }

		public InspectorSplitBar SplitBar { get; }

		/// <summary>The tree's expansion, the selection or the split moved: what <see cref="SavedState"/> returns changed.</summary>
		public event EventHandler SavedStateChanged;

		/// <summary>
		/// agg-gui's props_h: the split line's height above the panel's bottom, in logical units. The pane shows it
		/// clamped so the tree and the properties each keep <see cref="MinPaneHeight"/>; the value itself is kept, so
		/// a panel made taller again gets its split back.
		/// </summary>
		public double PropertiesHeight
		{
			get => this.propertiesHeight;
			set
			{
				if (value != this.propertiesHeight)
				{
					this.propertiesHeight = value;
					this.SizeProperties();
					this.RaiseIfStateChanged();
				}
			}
		}

		/// <summary>The height the tree and properties share below the header, in logical units.</summary>
		public double ListAreaHeight => (this.Height - this.Padding.Height - this.header.Height - this.header.Margin.Height) / DeviceScale;

		/// <summary>agg-gui's split clamp: at least <see cref="MinPaneHeight"/> for the properties and, room allowing, for the tree.</summary>
		public double ClampPropertiesHeight(double height) => Math.Max(MinPaneHeight, Math.Min(height, this.ListAreaHeight - MinPaneHeight));

		/// <summary>The inspector's state to save, as agg-gui's saved_state.</summary>
		public InspectorSavedState SavedState => new InspectorSavedState
		{
			Expanded = this.Model.ExpansionByPosition(),
			Selected = this.Model.SelectedPosition,
			PropertiesHeight = this.propertiesHeight,
		};

		/// <summary>
		/// Restores a saved state, as agg-gui's apply_saved_state: the split at once, the expansion and selection as
		/// soon as the tree has been collected (now, if it has).
		/// </summary>
		public void ApplySavedState(InspectorSavedState state)
		{
			if (state == null)
			{
				return;
			}

			this.PropertiesHeight = Math.Max(MinPaneHeight, Math.Min(state.PropertiesHeight, 1024));
			this.pendingState = state;
			if (this.Model.Nodes.Count > 0)
			{
				this.ApplyPendingState();
			}
		}

		/// <summary>The colours the panel draws with; setting it repaints.</summary>
		public InspectorStyle Style
		{
			get => this.style;
			set
			{
				this.style = value ?? new InspectorStyle();
				this.ApplyStyle();
			}
		}

		/// <summary>
		/// Points the model and the highlight at the tree the panel shows and re-collects it. The timer calls this; it
		/// can also be called directly (a test does, rather than waiting on the timer). A panel that is hidden or out
		/// of its window lets go of the tree, so a closed inspector leaves no highlight behind.
		/// </summary>
		public void RefreshNow()
		{
			GuiWidget root = this.Visible ? this.InspectedRoot ?? this.FindDefaultRoot() : null;
			this.overlay.Root = root;
			if (root != this.Model.Root)
			{
				this.Model.Root = root;
			}
			else
			{
				this.Model.Refresh();
			}
		}

		/// <summary>The tree shown when <see cref="InspectedRoot"/> is null, looked up on every refresh so it follows the
		/// panel as it is moved or taken out: the SystemWindow the panel is in. An app that hosts its own page inside
		/// a bigger window can return its page's root instead.</summary>
		protected virtual GuiWidget FindDefaultRoot() => this.Parents<SystemWindow>().FirstOrDefault();

		/// <summary>The edits waiting for the next idle, oldest first.</summary>
		public IReadOnlyList<InspectorEdit> PendingEdits => this.pendingEdits;

		/// <summary>
		/// Queues <paramref name="edit"/> to be applied on the UI thread's next idle, as agg-gui's host frame loop drains
		/// its edit queue: the click that made it is still being dispatched, and changing the tree under that dispatch
		/// could re-lay-out widgets it is walking.
		/// </summary>
		public void QueueEdit(InspectorEdit edit)
		{
			if (edit == null)
			{
				return;
			}

			this.pendingEdits.Add(edit);
			if (this.pendingEdits.Count == 1)
			{
				UiThread.RunOnIdle(this.ApplyPendingEdits);
			}
		}

		/// <summary>Applies every queued edit in order, then re-collects the tree so the pane shows the new values.
		/// The idle queued by <see cref="QueueEdit"/> calls this; a test can call it directly.</summary>
		public void ApplyPendingEdits()
		{
			if (this.pendingEdits.Count == 0)
			{
				return;
			}

			var edits = this.pendingEdits.ToList();
			this.pendingEdits.Clear();
			foreach (InspectorEdit edit in edits)
			{
				edit.Apply();
			}

			this.RefreshNow();
		}

		public override void OnClosed(EventArgs e)
		{
			this.pendingEdits.Clear();
			UiThread.ClearInterval(this.refreshInterval);
			this.refreshInterval = null;
			this.Model.Changed -= this.Model_Changed;
			this.overlay.Detach();
			base.OnClosed(e);
		}

		public override void OnBoundsChanged(EventArgs e)
		{
			this.SizeProperties();
			base.OnBoundsChanged(e);
		}

		/// <summary>Sizes the properties pane so the split's line sits at the clamped <see cref="PropertiesHeight"/>.</summary>
		private void SizeProperties()
		{
			double splitY = this.ClampPropertiesHeight(this.propertiesHeight);
			double height = Math.Max(0, (splitY - InspectorSplitBar.LogicalLine) * DeviceScale);
			if (this.Properties != null && height != this.Properties.Height)
			{
				this.Properties.Height = height;
			}
		}

		private void ApplyPendingState()
		{
			InspectorSavedState state = this.pendingState;
			this.pendingState = null;
			this.Model.RestoreByPosition(state.Expanded, state.Selected);
		}

		private void RaiseIfStateChanged()
		{
			InspectorSavedState state = this.SavedState;
			if (this.lastState == null
				|| state.Selected != this.lastState.Selected
				|| state.PropertiesHeight != this.lastState.PropertiesHeight
				|| !state.Expanded.SequenceEqual(this.lastState.Expanded))
			{
				this.lastState = state;
				this.SavedStateChanged?.Invoke(this, EventArgs.Empty);
			}
		}

		private void ApplyStyle()
		{
			this.BackgroundColor = this.style.Background;
			this.header.BackgroundColor = this.style.HeaderBackground;
			this.headerTitle.TextColor = this.style.Text;
			this.headerCount.TextColor = this.style.DimText;
			this.Tree.Style = this.style;
			this.Properties.Style = this.style;
			this.Tree.Invalidate();
			this.Properties.Invalidate();
			this.SplitBar.Invalidate();
			this.Invalidate();
		}

		private void Model_Changed(object sender, EventArgs e)
		{
			int count = this.Model.Nodes.Count;
			this.headerCount.Text = $"{count} widgets";
			if (this.pendingState != null && count > 0)
			{
				// restoring raises Changed again, which lands back here with nothing pending
				this.ApplyPendingState();
				return;
			}

			this.RaiseIfStateChanged();
		}
	}
}
