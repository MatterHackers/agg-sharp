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

using System.Collections.Generic;
using System.Linq;
using MatterHackers.Agg.UI;

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Widgets
{
	/// <summary>A node of the Misc Demos tree: just its children, like egui's Tree(Vec&lt;Self&gt;).</summary>
	public class MiscTreeNode
	{
		public List<MiscTreeNode> Children { get; } = new List<MiscTreeNode>();

		/// <summary>The shape egui seeds its demo tree with: two children holding four leaves and three pairs.</summary>
		public static MiscTreeNode Demo()
		{
			var root = new MiscTreeNode();
			root.Children.Add(WithChildren(Enumerable.Range(0, 4).Select(_ => new MiscTreeNode())));
			root.Children.Add(WithChildren(Enumerable.Range(0, 3).Select(_ => WithChildren(new[] { new MiscTreeNode(), new MiscTreeNode() }))));
			return root;
		}

		/// <summary>The node at <paramref name="path"/> (child indices from this node), or null if there is none.</summary>
		public MiscTreeNode At(IReadOnlyList<int> path)
		{
			MiscTreeNode node = this;
			foreach (int index in path)
			{
				if (index < 0 || index >= node.Children.Count)
				{
					return null;
				}

				node = node.Children[index];
			}

			return node;
		}

		/// <summary>Adds a leaf under the node at <paramref name="path"/>.</summary>
		public void AddChild(IReadOnlyList<int> path) => this.At(path)?.Children.Add(new MiscTreeNode());

		/// <summary>Removes the node at <paramref name="path"/> from its parent; the root (an empty path) stays.</summary>
		public void Delete(IReadOnlyList<int> path)
		{
			if (path.Count == 0)
			{
				return;
			}

			MiscTreeNode parent = this.At(path.Take(path.Count - 1).ToList());
			int last = path[path.Count - 1];
			if (parent != null && last < parent.Children.Count)
			{
				parent.Children.RemoveAt(last);
			}
		}

		private static MiscTreeNode WithChildren(IEnumerable<MiscTreeNode> children)
		{
			var node = new MiscTreeNode();
			node.Children.AddRange(children);
			return node;
		}
	}

	/// <summary>
	/// agg-gui's tree_section.rs (egui's Tree demo): nested collapsing headers where every node has a "+" that
	/// adds a child and every node below the root a "delete". A change rebuilds the tree from the model, so
	/// open/closed state resets to the defaults (root open, the rest closed), as agg-gui's does.
	/// </summary>
	public sealed class MiscTreeSection : GuiWidget
	{
		private readonly MiscDemoKit kit;

		internal MiscTreeSection(MiscDemoKit kit)
		{
			this.kit = kit;
			this.Name = "Misc Tree";
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Fit;
			this.Rebuild();
		}

		public MiscTreeNode Root { get; } = MiscTreeNode.Demo();

		/// <summary>The automation name part for a path: "root", or its indices joined by dots.</summary>
		public static string PathName(IReadOnlyList<int> path) => path.Count == 0 ? "root" : string.Join(".", path);

		private void Rebuild()
		{
			this.CloseChildren();
			this.AddChild(this.BuildNode(this.Root, new List<int>(), "root"));
		}

		private GuiWidget BuildNode(MiscTreeNode node, List<int> path, string title)
		{
			string pathName = PathName(path);
			var header = new CollapsingHeader(title, this.kit.Theme, expanded: path.Count < 1);
			header.Header.Name = $"Misc Tree Node {pathName}";

			if (path.Count > 0)
			{
				ThemedTextButton delete = this.kit.Button($"Misc Tree Delete {pathName}", "delete");
				delete.Click += (s, e) => this.Change(() => this.Root.Delete(path));
				header.Body.AddChild(delete);
			}

			for (int i = 0; i < node.Children.Count; i++)
			{
				var childPath = new List<int>(path) { i };
				header.Body.AddChild(this.BuildNode(node.Children[i], childPath, $"child #{i}"));
			}

			ThemedTextButton add = this.kit.Button($"Misc Tree Add {pathName}", "+");
			add.Click += (s, e) => this.Change(() => this.Root.AddChild(path));
			header.Body.AddChild(add);
			return header;
		}

		/// <summary>Applies a model change after the click that asked for it, since the rebuild closes that button.</summary>
		private void Change(System.Action change)
		{
			change();
			UiThread.RunOnIdle(this.Rebuild);
		}
	}
}
