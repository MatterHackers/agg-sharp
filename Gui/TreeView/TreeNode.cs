﻿/*
Copyright (c) 2016, Lars Brubaker, John Lewin
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

using MatterHackers.Agg.Font;
using MatterHackers.Agg.Image;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;

namespace MatterHackers.Agg.UI.TreeView
{
	public class TreeNode : FlowLayoutWidget, ICheckbox
	{
		FlowLayoutWidget titleBar;
		GuiWidget content;

		public TreeNode()
			: base(FlowDirection.TopToBottom)
		{
			HAnchor = HAnchor.Fit | HAnchor.Left;
			VAnchor = VAnchor.Fit;

			titleBar = new FlowLayoutWidget();
			AddChild(titleBar);
			RebuildTitleBar();

			content = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Fit | HAnchor.Left,
				Visible = false, // content starts out not visible
				Name = "content",
				Margin = new BorderDouble(25, 3),
			};
			AddChild(content);

			Nodes.CollectionChanged += Nodes_CollectionChanged;
		}

		public override void OnTextChanged(EventArgs e)
		{
			RebuildTitleBar();
			base.OnTextChanged(e);
		}

		private void RebuildTitleBar()
		{
			titleBar.RemoveAllChildren();
			if(content != null
				&& GetNodeCount(false) > 0)
			{
				// add a check box
				var expandCheckBox = new CheckBox("")
				{
					Checked = Expanded,
					VAnchor = VAnchor.Center
				};
				ExpandedChanged += (s, e) =>
				{
					expandCheckBox.Checked = Expanded;
				};
				expandCheckBox.CheckedStateChanged += (s, e) =>
				{
					Expanded = expandCheckBox.Checked;
				};
				titleBar.AddChild(expandCheckBox);
			}
			titleBar.AddChild(new TextWidget(Text));
		}

		private void Nodes_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			RebuildContentSection();
		}

		private void RebuildContentSection()
		{
			// If the node count is starting at 0 we are adding content and need to rebuild the title bar so it will have a + in it
			bool needToRebuildTitleBar = GetNodeCount(false) == 0;

			// Remove but don't close all the current nodes
			content.RemoveAllChildren();

			// Then add them back in (after the change)
			foreach (var node in Nodes)
			{
				node.ClearRemovedFlag();
				content.AddChild(node);
			}

			// If the node count is ending at 0 we removed content and need to rebuild the title bar so it will net have a + in it
			needToRebuildTitleBar |= GetNodeCount(false) == 0;
			if(needToRebuildTitleBar)
			{
				RebuildTitleBar();
			}
		}

		#region Properties
		//
		// Summary:
		//     Gets or sets a value indicating whether the tree node is in a checked state.
		//
		// Returns:
		//     true if the tree node is in a checked state; otherwise, false.
		public bool Checked { get; set; }

		//
		// Summary:
		//     Gets the first child tree node in the tree node collection.
		//
		// Returns:
		//     The first child TreeNode in the TreeNode.Nodes
		//     collection.
		public TreeNode FirstNode { get; }

		//
		// Summary:
		//     Gets or sets the image list index value of the image displayed when the tree
		//     node is in the unselected state.
		//
		// Returns:
		//     A zero-based index value that represents the image position in the assigned ImageList.
		public ImageBuffer Image { get; set; }

		//
		// Summary:
		//     Gets a value indicating whether the tree node is in an editable state.
		//
		// Returns:
		//     true if the tree node is in editable state; otherwise, false.
		public bool Editing { get; }

		//
		// Summary:
		//     Gets a value indicating whether the tree node is in the expanded state.
		//
		// Returns:
		//     true if the tree node is in the expanded state; otherwise, false.
		public bool Expanded
		{
			get
			{
				return content.Visible;
			}
			set
			{
				if (content.Visible != value)
				{
					content.Visible = value;
					ExpandedChanged?.Invoke(this, null);
				}
			}
		}

		//
		// Summary:
		//     Gets a value indicating whether the tree node is in the selected state.
		//
		// Returns:
		//     true if the tree node is in the selected state; otherwise, false.
		public bool Selected { get; }

		//
		// Summary:
		//     Gets the last child tree node.
		//
		// Returns:
		//     A TreeNode that represents the last child tree node.
		public TreeNode LastNode { get; }

		//
		// Summary:
		//     Gets the zero-based depth of the tree node in the TreeView
		//     control.
		//
		// Returns:
		//     The zero-based depth of the tree node in the TreeView control.
		public int Level { get; }

		//
		// Summary:
		//     Gets the next sibling tree node.
		//
		// Returns:
		//     A TreeNode that represents the next sibling tree node.
		public TreeNode NextNode { get; }

		//
		// Summary:
		//     Gets the next visible tree node.
		//
		// Returns:
		//     A TreeNode that represents the next visible tree node.
		public TreeNode NextVisibleNode { get; }

		//
		// Summary:
		//     Gets or sets the font that is used to display the text on the tree node label.
		//
		// Returns:
		//     The StyledTypeFace that is used to display the text on the tree node label.
		public StyledTypeFace NodeFont { get; set; }

		public ObservableCollection<TreeNode> Nodes { get; } = new ObservableCollection<TreeNode>();

		//
		// Summary:
		//     Gets the parent tree node of the current tree node.
		//
		// Returns:
		//     A TreeNode that represents the parent of the current tree
		//     node.
		public TreeNode NodeParent { get; }

		public int PointSize { get; set; }

		//
		// Summary:
		//     Gets the previous sibling tree node.
		//
		// Returns:
		//     A TreeNode that represents the previous sibling tree node.
		public TreeNode PrevNode { get; }

		//
		// Summary:
		//     Gets the previous visible tree node.
		//
		// Returns:
		//     A TreeNode that represents the previous visible tree node.
		public TreeNode PrevVisibleNode { get; }

		//
		// Summary:
		//     Gets or sets the image list index value of the image that is displayed when the
		//     tree node is in the selected state.
		//
		// Returns:
		//     A zero-based index value that represents the image position in an ImageList.
		public ImageBuffer SelectedImage { get; set; }

		//
		// Summary:
		//     Gets or sets the index of the image that is used to indicate the state of the
		//     TreeNode when the parent TreeView has
		//     its TreeView.CheckBoxes property set to false.
		//
		// Returns:
		//     The index of the image that is used to indicate the state of the TreeNode.
		//
		// Exceptions:
		//   T:System.ArgumentOutOfRangeException:
		//     The specified index is less than -1 or greater than 14.
		public ImageBuffer StateImage { get; set; }

		//
		// Summary:
		//     Gets or sets the object that contains data about the tree node.
		//
		// Returns:
		//     An System.Object that contains data about the tree node. The default is null.
		public object Tag { get; set; }

		public Color TextColor { get; set; }

		//
		// Summary:
		//     Gets the parent tree view that the tree node is assigned to.
		//
		// Returns:
		//     A TreeView that represents the parent tree view that the
		//     tree node is assigned to, or null if the node has not been assigned to a tree
		//     view.
		public TreeView TreeView { get; }

		#endregion

		#region Events
		public event EventHandler ExpandedChanged;
		public event EventHandler CheckedStateChanged;
		#endregion

		//
		// Summary:
		//     Initiates the editing of the tree node label.
		//
		// Exceptions:
		//   T:System.InvalidOperationException:
		//     TreeView.LabelEdit is set to false.
		public void BeginEdit()
		{
			throw new NotImplementedException();
		}

		public void Collapse(bool collapseChildren)
		{
			throw new NotImplementedException();
		}

		//
		// Summary:
		//     Collapses the tree node.
		public void Collapse()
		{
			throw new NotImplementedException();
		}

		//
		// Summary:
		//     Ends the editing of the tree node label.
		//
		// Parameters:
		//   cancel:
		//     true if the editing of the tree node label text was canceled without being saved;
		//     otherwise, false.
		public void EndEdit(bool cancel)
		{
			throw new NotImplementedException();
		}

		//
		// Summary:
		//     Ensures that the tree node is visible, expanding tree nodes and scrolling the
		//     tree view control as necessary.
		public void EnsureVisible()
		{
			throw new NotImplementedException();
		}

		//
		// Summary:
		//     Expands all the child tree nodes.
		public void ExpandAll()
		{
			throw new NotImplementedException();
		}

		//
		// Summary:
		//     Returns the number of child tree nodes.
		//
		// Parameters:
		//   includeSubTrees:
		//     true if the resulting count includes all tree nodes indirectly rooted at this
		//     tree node; otherwise, false.
		//
		// Returns:
		//     The number of child tree nodes assigned to the TreeNode.Nodes
		//     collection.
		public int GetNodeCount(bool includeSubTrees)
		{
			if (includeSubTrees)
			{
				return this.Descendants<TreeNode>().Count();
			}

			return content.Children.Where((c) => c is TreeNode).Count();
		}

		//
		// Summary:
		//     Removes the current tree node from the tree view control.
		public void Remove()
		{
			throw new NotImplementedException();
		}

		//
		// Summary:
		//     Toggles the tree node to either the expanded or collapsed state.
		public void Toggle()
		{
			content.Visible = !content.Visible;
		}
	}
}