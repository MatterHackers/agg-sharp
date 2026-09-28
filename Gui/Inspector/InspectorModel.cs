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
	/// The widget inspector's state, after agg-gui's inspector (widget/tree_inspector.rs and widgets/inspector): a flat,
	/// depth-first snapshot of a widget tree, which rows are expanded, and which node is selected and which hovered.
	/// </summary>
	/// <remarks>
	/// Expansion, selection and hover are kept by widget, not by row, so they survive a <see cref="Refresh"/> that
	/// adds, removes or moves widgets. Every node starts expanded, as agg-gui's (a widget it has not seen before takes
	/// is_expanded = true), so the model keeps the rows the user collapsed. The model never changes the tree it inspects.
	/// </remarks>
	public class InspectorModel
	{
		private readonly HashSet<GuiWidget> collapsed = new HashSet<GuiWidget>();

		private List<InspectorNode> nodes = new List<InspectorNode>();

		private GuiWidget root;

		public InspectorModel(GuiWidget root = null)
		{
			this.Root = root;
		}

		/// <summary>The snapshot, selection, hover or expansion changed.</summary>
		public event EventHandler Changed;

		/// <summary>The widget the tree starts at. Setting it takes a new snapshot, with every node expanded.</summary>
		public GuiWidget Root
		{
			get => this.root;
			set
			{
				if (value == this.root)
				{
					return;
				}

				this.root = value;
				this.collapsed.Clear();
				this.Selected = null;
				this.Hovered = null;

				this.nodes = new List<InspectorNode>();
				this.Refresh();
				this.Changed?.Invoke(this, EventArgs.Empty);
			}
		}

		/// <summary>Every widget under <see cref="Root"/> (and the root), depth first in child order.</summary>
		public IReadOnlyList<InspectorNode> Nodes => this.nodes;

		/// <summary>The node shown in the properties pane and outlined on the app; null for none.</summary>
		public InspectorNode Selected { get; private set; }

		/// <summary>The node whose tree row is under the mouse, highlighted on the app; null for none.</summary>
		public InspectorNode Hovered { get; private set; }

		/// <summary>The depth-first snapshot of <paramref name="root"/>'s tree, in its coordinates.</summary>
		public static List<InspectorNode> Collect(GuiWidget root)
		{
			var list = new List<InspectorNode>();
			if (root != null)
			{
				Collect(root, root, 0, Array.Empty<int>(), list);
			}

			return list;
		}

		/// <summary>
		/// Takes a new snapshot of the tree (agg-gui re-collects every frame; the panel calls this on a timer). Selection
		/// and hover move to the new node of the same widget, or clear when it has gone. Returns whether anything shown
		/// changed, and raises <see cref="Changed"/> if so.
		/// </summary>
		public bool Refresh()
		{
			List<InspectorNode> fresh = Collect(this.root);
			bool changed = fresh.Count != this.nodes.Count;
			for (int i = 0; !changed && i < fresh.Count; i++)
			{
				changed = !fresh[i].SameAs(this.nodes[i]);
			}

			if (!changed)
			{
				return false;
			}

			this.nodes = fresh;
			var live = new HashSet<GuiWidget>(fresh.Select(node => node.Widget));
			this.collapsed.RemoveWhere(widget => !live.Contains(widget));
			this.Selected = this.NodeOf(this.Selected?.Widget);
			this.Hovered = this.NodeOf(this.Hovered?.Widget);
			this.Changed?.Invoke(this, EventArgs.Empty);
			return true;
		}

		/// <summary>The node of <paramref name="node"/>'s parent widget, or null for the root.</summary>
		public InspectorNode ParentOf(InspectorNode node) => node == null || node.Depth == 0 ? null : this.NodeOf(node.Widget.Parent);

		/// <summary>The current node of <paramref name="widget"/>, or null when it is not in the tree.</summary>
		public InspectorNode NodeOf(GuiWidget widget)
		{
			return widget == null ? null : this.nodes.FirstOrDefault(node => node.Widget == widget);
		}

		/// <summary>Whether <paramref name="node"/> shows its children: true unless the user collapsed it, so a leaf reads
		/// true, as agg-gui saves it.</summary>
		public bool IsExpanded(InspectorNode node) => node != null && !this.collapsed.Contains(node.Widget);

		/// <summary>Shows or hides <paramref name="node"/>'s children in the tree. A node without children stays as it is.</summary>
		public void SetExpanded(InspectorNode node, bool expand)
		{
			if (node == null || node.ChildCount == 0 || expand == this.IsExpanded(node))
			{
				return;
			}

			if (expand)
			{
				this.collapsed.Remove(node.Widget);
			}
			else
			{
				this.collapsed.Add(node.Widget);
			}

			this.Changed?.Invoke(this, EventArgs.Empty);
		}

		/// <summary>Selects <paramref name="node"/> (null clears), expanding its ancestors so its row can be seen.</summary>
		public void Select(InspectorNode node)
		{
			if (node == this.Selected)
			{
				return;
			}

			this.Selected = node;
			if (node != null)
			{
				for (GuiWidget parent = node.Widget.Parent; parent != null && parent != this.root.Parent; parent = parent.Parent)
				{
					this.collapsed.Remove(parent);
				}
			}

			this.Changed?.Invoke(this, EventArgs.Empty);
		}

		/// <summary>Sets the node whose row the mouse is over (null when none).</summary>
		public void SetHovered(InspectorNode node)
		{
			if (node != this.Hovered)
			{
				this.Hovered = node;
				this.Changed?.Invoke(this, EventArgs.Empty);
			}
		}

		/// <summary>Each node's expansion, in <see cref="Nodes"/> order (agg-gui saves the tree this way).</summary>
		public List<bool> ExpansionByPosition() => this.nodes.Select(this.IsExpanded).ToList();

		/// <summary>The selected node's position in <see cref="Nodes"/>, or -1 when nothing is selected.</summary>
		public int SelectedPosition => this.Selected == null ? -1 : this.nodes.IndexOf(this.Selected);

		/// <summary>
		/// Restores expansion and selection saved by position (<see cref="ExpansionByPosition"/>,
		/// <see cref="SelectedPosition"/>), as agg-gui's apply_saved_state: flags past the end are ignored, nodes past
		/// the flags keep their expansion, and a selection out of range selects nothing. Unlike <see cref="Select"/>
		/// it expands nothing but what the flags say.
		/// </summary>
		public void RestoreByPosition(IReadOnlyList<bool> expandedFlags, int selectedPosition)
		{
			for (int i = 0; i < Math.Min(expandedFlags?.Count ?? 0, this.nodes.Count); i++)
			{
				if (expandedFlags[i])
				{
					this.collapsed.Remove(this.nodes[i].Widget);
				}
				else
				{
					this.collapsed.Add(this.nodes[i].Widget);
				}
			}

			this.Selected = selectedPosition >= 0 && selectedPosition < this.nodes.Count ? this.nodes[selectedPosition] : null;
			this.Changed?.Invoke(this, EventArgs.Empty);
		}

		/// <summary>The rows the tree shows, top to bottom: every node whose ancestors are all expanded.</summary>
		public List<InspectorNode> VisibleRows()
		{
			var rows = new List<InspectorNode>();
			int hideDeeperThan = int.MaxValue;
			foreach (InspectorNode node in this.nodes)
			{
				if (node.Depth > hideDeeperThan)
				{
					continue;
				}

				hideDeeperThan = int.MaxValue;
				rows.Add(node);
				if (node.ChildCount > 0 && !this.IsExpanded(node))
				{
					hideDeeperThan = node.Depth;
				}
			}

			return rows;
		}

		private static void Collect(GuiWidget widget, GuiWidget root, int depth, int[] path, List<InspectorNode> list)
		{
			list.Add(new InspectorNode(widget, root, depth, path));
			int index = 0;
			foreach (GuiWidget child in widget.Children)
			{
				Collect(child, root, depth + 1, path.Append(index++).ToArray(), list);
			}
		}
	}
}
