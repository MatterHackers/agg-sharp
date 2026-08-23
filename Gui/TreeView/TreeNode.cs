/*
Copyright (c) 2026, Lars Brubaker, John Lewin
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
using MatterHackers.Agg.Platform;
using MatterHackers.ImageProcessing;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;

namespace MatterHackers.Agg.UI
{
    public class TreeNode : FlowLayoutWidget, ICheckbox
    {
        private readonly GuiWidget content;
        private readonly TreeExpandWidget expandWidget;
        private readonly ImageWidget imageWidget;
        private readonly TextWidget textWidget;
        private bool _expanded;
        private TreeView _treeView;
        private ThemeConfig theme;

        /// <summary>
        /// How many nodes anywhere are holding rows that have not been parented into the widget tree yet.
        /// </summary>
        /// <remarks>
        /// A whole-process count rather than one per TreeView because a node does not reliably know which
        /// TreeView it belongs to until it is parented (<see cref="TreeView"/> walks up through NodeParent).
        /// It only gates a walk that is otherwise skipped, so the cost of the coarseness is that one tree
        /// filling in makes the other trees on screen check themselves once, which is nothing.
        /// </remarks>
        private static int nodesAwaitingContent;

        // The dirty flag and whether this node is counted in nodesAwaitingContent live in one field, so
        // that the two can only ever move together (see IsDirty).
        private const int Clean = 0;
        private const int DirtyCounted = 1;
        private const int DirtyOrphaned = 2;

        private int dirtyState;

        /// <summary>
        /// Whether any node that can still be drawn is waiting to have its rows materialized, so a TreeView
        /// can skip the walk in <see cref="UI.TreeView.OnDraw"/> on the frames where none is.
        /// </summary>
        /// <remarks>
        /// Best effort, and deliberately biased towards saying yes: an overcount only costs the walk it
        /// gates, while an undercount brings the mid-frame glitch back. So it stays hot for as long as any
        /// node is dirty, including nodes sitting inside a collapsed parent that will not be drawn until
        /// someone opens it - a tree built up front and mostly collapsed (a help index, say) can hold it
        /// true for the life of the window. Nodes dropped from the tree stop counting where that is visible
        /// (see <see cref="RebuildContentSection"/>), but a node pulled out of the widget tree by other
        /// means keeps its count until it is closed.
        /// </remarks>
        internal static bool AnyNodeAwaitingContent => Volatile.Read(ref nodesAwaitingContent) > 0;

        /// <summary>
        /// True while this node's <see cref="Nodes"/> have changed but the rows for them are not in the
        /// widget tree yet. Kept behind a property so the process-wide count stays in step with it.
        /// </summary>
        private bool IsDirty
        {
            get => Volatile.Read(ref dirtyState) != Clean;

            set
            {
                // Nodes.Add runs off the UI thread while a library panel fills in, and can land while the UI
                // thread is clearing the flag in RebuildContentSection. So the flag and the count have to
                // change as one step: a read-compare-write of the flag with a separate Interlocked on the
                // count can interleave into "dirty, but nothing counted", which switches the pre-pass off in
                // exactly the case it exists for. One Exchange hands this thread the state it really
                // replaced, which is the only thing that says what this thread owes the count.
                int previous = Interlocked.Exchange(ref dirtyState, value ? DirtyCounted : Clean);

                int delta = (value ? 1 : 0) - (previous == DirtyCounted ? 1 : 0);
                if (delta != 0)
                {
                    Interlocked.Add(ref nodesAwaitingContent, delta);
                }
            }
        }

        /// <summary>
        /// Stop counting towards <see cref="AnyNodeAwaitingContent"/> while staying dirty, for a node that
        /// has left the widget tree and so cannot be reached by the pre-pass to be built.
        /// </summary>
        /// <remarks>
        /// The flag itself must survive: the rows really are unbuilt, and if the node is parented again it
        /// still has to build them. <see cref="OnParentChanged"/> is what puts it back on the count.
        /// </remarks>
        private void StopAwaitingContent()
        {
            if (Interlocked.CompareExchange(ref dirtyState, DirtyOrphaned, DirtyCounted) == DirtyCounted)
            {
                Interlocked.Decrement(ref nodesAwaitingContent);
            }
        }

        public TreeNode(ThemeConfig theme, bool useIcon = true, TreeNode nodeParent = null)
            : base(FlowDirection.TopToBottom)
        {
            this.theme = theme;

            this.HAnchor = HAnchor.Fit | HAnchor.Left;
            this.VAnchor = VAnchor.Fit;

            this.NodeParent = nodeParent;

            this.TitleBar = new FlowLayoutWidget();
            this.TitleBar.Click += (s, e) =>
            {
                if (TreeView != null)
                {
                    // Keep right-click non-destructive so callers can show a context menu
                    // without changing tree selection or triggering navigation.
                    if (e.Button == MouseButtons.Left)
                    {
                        TreeView.SelectedNode = this;
                    }

                    TreeView.NotifyItemClicked(TitleBar, e);
                }
            };

            TreeNode hitNode = null;
            var firstClickHandled = false;
            this.TitleBar.MouseDown += (s, e) =>
            {
                if (TreeView != null && e.Button == MouseButtons.Left)
                {
                    if (e.Clicks == 1)
                    {
                        firstClickHandled = e.Handled;
                        hitNode = this;
                    }
                    else if (e.Clicks == 2)
                    {
                        // Nodes can move around in the tree between clicks.
                        // Make sure we're hitting the same node twice, and the double click wasn't handled
                        if (this != hitNode
                            || firstClickHandled)
                        {
                            return;
                        }

                        // find the child that is a TreeExpandWidget
                        TreeExpandWidget treeExpandWidget = this.Descendants<TreeExpandWidget>(tew => tew.ContainsFirstUnderMouseRecursive()).FirstOrDefault();

                        // if there was a tree expand widget that got clicked (under the mouse)
                        if (treeExpandWidget != null)
                        {
                            // already selected and will have open / close processing done by the treeExpandWidget. Return without doing any double clicking.
                            return;
                        }

                        TreeView.SelectedNode = this;

                        if (this.Nodes.Count > 0
                            && TreeView.DoubleClickTogglesExpansion)
                        {
                            this.Expanded = !this.Expanded;
                        }
                        else
                        {
                            this.TreeView.NotifyItemDoubleClicked(TitleBar, e);
                        }
                    }
                }
            };

            this.AddChild(this.TitleBar);

            // add a check box
            expandWidget = new TreeExpandWidget(theme)
            {
                Expandable = GetNodeCount(false) != 0,
                VAnchor = VAnchor.Fit | VAnchor.Center,
                Height = 16 * DeviceScale,
                Width = 16 * DeviceScale,
                Name = "Expand Widget"
            };

            expandWidget.Click += (s, e) =>
            {
                this.Expanded = !this.Expanded;
                expandWidget.Expanded = this.Expanded;
            };

            this.TitleBar.AddChild(expandWidget);

            this.HighlightRegion = new FlowLayoutWidget()
            {
                VAnchor = VAnchor.Fit,
                HAnchor = HAnchor.Fit,
                Padding = useIcon ? new BorderDouble(2) : new BorderDouble(4, 2),
                Selectable = false,
                Name = "Content Region"
            };
            this.TitleBar.AddChild(this.HighlightRegion);

            // add a check box
            if (useIcon)
            {
                var image = new ImageBuffer(16, 16);

                this.HighlightRegion.AddChild(imageWidget = new ImageWidget(image, listenForImageChanged: false)
                {
                    VAnchor = VAnchor.Center,
                    Margin = new BorderDouble(right: 4),
                    Selectable = false,
                    Name = "ImageIconWidget"
                });
            }

            this.HighlightRegion.AddChild(textWidget = new TextWidget(this.Text, pointSize: theme.DefaultFontSize, textColor: theme.TextColor)
            {
                Selectable = false,
                AutoExpandBoundsToText = true,
                VAnchor = VAnchor.Center
            });

            content = new FlowLayoutWidget(FlowDirection.TopToBottom)
            {
                HAnchor = HAnchor.Fit | HAnchor.Left,
                Visible = false, // content starts out not visible
                Name = "content",
                //Margin = new BorderDouble(12, 3),
                Padding = new BorderDouble(12, 3),
            };
            content.AfterDraw += Content_AfterDraw;
            this.AddChild(content);

            // Register listeners
            this.Nodes.CollectionChanged += this.Nodes_CollectionChanged;
        }

        private void Content_AfterDraw(object sender, DrawEventArgs e)
        {
            if (TreeView?.ShowLines == true)
            {
                var xOffset = -.5;
                var shortLine = 5 * DeviceScale;
                var longLine = 25 * DeviceScale;

                foreach (var treeNodeWidget in content.Children.OfType<TreeNode>())
                {
                    var firstChildOfLast = treeNodeWidget.Children.OfType<FlowLayoutWidget>().FirstOrDefault();
                    var yOffset = 0.5;
                    if (firstChildOfLast != null)
                    {
                        var firstChildOfLastBounds = firstChildOfLast.TransformToParentSpace(this, firstChildOfLast.LocalBounds);
                        yOffset = Math.Round(firstChildOfLastBounds.Center.Y) + .5;
                        if (treeNodeWidget.Nodes.Count > 0 || treeNodeWidget.Expandable)
                        {
                            // short line for nodes that have children or are expandable (e.g. lazy-loaded containers)
                            e.Graphics2D.Line(xOffset, yOffset, xOffset + shortLine, yOffset, theme.TextColor.WithAlpha(100));
                        }
                        else
                        {
                            e.Graphics2D.Line(xOffset, yOffset, xOffset + longLine, yOffset, theme.TextColor.WithAlpha(100));
                        }
                    }

                    if (treeNodeWidget == content.Children.OfType<TreeNode>().LastOrDefault())
                    {
                        // draw a line from the center of the height of the last child to the top
                        e.Graphics2D.Line(xOffset, yOffset, xOffset, Height, theme.TextColor.WithAlpha(100));
                    }
                }
            }
        }

        public event EventHandler CheckedStateChanged;

        public event EventHandler ExpandedChanged;

        public event EventHandler ImageChanged;

        public bool AlwaysExpandable
        {
            get => expandWidget.AlwaysExpandable;
            set => expandWidget.AlwaysExpandable = value;
        }

        public bool Checked { get; set; }

        public bool Editing { get; }

        public bool Expandable
        {
            get => expandWidget.Expandable;
            set => expandWidget.Expandable = value;
        }

        public bool Expanded
        {
            get => _expanded;
            set
            {
                if (_expanded != value || content.Visible != value)
                {
                    _expanded = value;
                    expandWidget.Expanded = _expanded;

                    content.Visible = _expanded && this.Nodes.Count > 0;
                    ExpandedChanged?.Invoke(this, null);
                }
            }
        }

        public TreeNode FirstNode { get; }

        public FlowLayoutWidget HighlightRegion { get; }

        public ImageBuffer Image
        {
            get
            {
                return imageWidget.Image;
            }

            set
            {
                if (Image != value)
                {
                    imageWidget.Image = value;

                    OnImageChanged(null);
                }
            }
        }

        public TreeNode LastNode { get; }

        /// <summary>
        /// Gets the zero-based depth of the tree node in the TreeView control.
        /// </summary>
        public int Level { get; }

        // Summary:
        //     Gets the next sibling tree node.
        //
        // Returns:
        //     A TreeNode that represents the next sibling tree node.
        public TreeNode NextNode { get; }

        // Summary:
        //     Gets the next visible tree node.
        //
        // Returns:
        //     A TreeNode that represents the next visible tree node.
        public TreeNode NextVisibleNode { get; }

        // Summary:
        //     Gets or sets the font that is used to display the text on the tree node label.
        //
        // Returns:
        //     The StyledTypeFace that is used to display the text on the tree node label.
        public StyledTypeFace NodeFont { get; set; }

        // Summary:
        //     Gets the parent tree node of the current tree node.
        //
        // Returns:
        //     A TreeNode that represents the parent of the current tree
        //     node.
        public TreeNode NodeParent { get; protected set; }

        public ObservableCollection<TreeNode> Nodes { get; } = new ObservableCollection<TreeNode>();

        public int PointSize { get; set; }

        // Summary:
        //     Gets the previous sibling tree node.
        //
        // Returns:
        //     A TreeNode that represents the previous sibling tree node.
        public TreeNode PrevNode { get; }

        // Summary:
        //     Gets the previous visible tree node.
        //
        // Returns:
        //     A TreeNode that represents the previous visible tree node.
        public TreeNode PrevVisibleNode { get; }

        public bool ReserveIconSpace
        {
            get => imageWidget.Visible;
            set => imageWidget.Visible = value;
        }

        // Summary:
        //     Gets a value indicating whether the tree node is in the selected state.
        //
        // Returns:
        //     true if the tree node is in the selected state; otherwise, false.
        public bool Selected
        {
            get
            {
                if (TreeView != null)
                {
                    return TreeView.SelectedNode == this;
                }

                return false;
            }
        }

        // Summary:
        //     Gets or sets the image list index value of the image that is displayed when the
        //     tree node is in the selected state.
        //
        // Returns:
        //     A zero-based index value that represents the image position in an ImageList.
        public ImageBuffer SelectedImage { get; set; }

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

        // Summary:
        //     Gets or sets the object that contains data about the tree node.
        //
        // Returns:
        //     An System.Object that contains data about the tree node. The default is null.
        public object Tag { get; set; }

        public Color TextColor { get; set; }

        public FlowLayoutWidget TitleBar { get; }

        public virtual TreeView TreeView
        {
            get => _treeView ?? NodeParent.TreeView;
            set => _treeView = value;
        }

        public IEnumerable<TreeNode> DescendantsAndSelf()
        {
            var treeNodes = new Stack<TreeNode>();
            treeNodes.Push(this);

            while (treeNodes.Any())
            {
                TreeNode treeNode = treeNodes.Pop();
                
                foreach (var childNode in treeNode.Nodes)
                {
                    treeNodes.Push(childNode);
                }

                yield return treeNode;
            }
        }

        public IEnumerable<TreeNode> Ancestors()
        {
            var context = this.NodeParent;
            while (context != null)
            {
                yield return context;

                context = context.NodeParent;
            }
        }

        public TreeNode FindNodeKey(string nodeKey)
        {
            if (this.GetNodeKey() == nodeKey)
            {
                return this;
            }
            
            foreach (var node in this.Nodes)
            {
                var foundNode = node.FindNodeKey(nodeKey);
                if (foundNode != null)
                {
                    return foundNode;
                }
            }

            return null;
        }

        // **** Not implemented ****
        public void BeginEdit() => throw new NotImplementedException();

        public void Collapse(bool collapseChildren) => throw new NotImplementedException();

        public void Collapse() => throw new NotImplementedException();

        public void DescendantsAndSelf(Action<TreeNode> action)
        {
            action(this);
            foreach (var node in Nodes)
            {
                node.DescendantsAndSelf(action);
            }
        }

        public void EndEdit(bool cancel) => throw new NotImplementedException();

        public void EnsureVisible() => throw new NotImplementedException();

        public void ExpandAll()
        {
            // expand everything recursively
            foreach (var node in Nodes)
            {
                node.Expanded = true;
                node.ExpandAll();
            }
        }

        public Dictionary<string, bool> GetExpandedStates()
        {
            var expandedStates = new Dictionary<string, bool>();
            foreach (var node in Nodes)
            {
                expandedStates[node.GetNodeKey()] = node.Expanded;
                expandedStates = expandedStates.Concat(node.GetExpandedStates()).ToDictionary(x => x.Key, x => x.Value);
            }

            return expandedStates;
        }

        public int GetNodeCount(bool includeSubTrees)
        {
            if (includeSubTrees)
            {
                return this.Descendants<TreeNode>().Count();
            }

            return content?.Children.Where((c) => c is TreeNode).Count() ?? 0;
        }

        public string GetNodeKey()
        {
            var parentNames = new List<string>();
            var parent = this;
            while (parent != null)
            {
                // check if there is a Name before using the Text
                if (!string.IsNullOrEmpty(parent.Name))
                {
                    parentNames.Add(parent.Name);
                }
                else
                {
                    parentNames.Add(parent.Text);
                }
                parent = parent.NodeParent;
            }

            parentNames.Reverse();

            return string.Join("/", parentNames);
        }

        /// <summary>
        /// Parent this node's child rows into the widget tree now, instead of waiting for the next paint.
        /// </summary>
        /// <remarks>
        /// Rows are normally materialized lazily in <see cref="OnDraw"/>, which is fine for a tree that is
        /// already on screen but wrong for one that is still being built. A node handed to a live container
        /// while it is still dirty grows in the middle of a frame, and everything that was already painted
        /// that frame - the rows above it, and the container itself - was drawn against the smaller size, so
        /// the tree paints stacked and spilling out over its own top edge until some later layout happens to
        /// cascade through. Building while the node is still detached costs nothing and makes the first frame
        /// the right frame.
        /// </remarks>
        public void EnsureContentBuilt()
        {
            if (IsDirty)
            {
                RebuildContentSection();
            }
        }

        public override void OnClosed(EventArgs e)
        {
            // Unregister listeners
            this.Nodes.CollectionChanged -= this.Nodes_CollectionChanged;

            // A closed node will never build its rows, so it must stop counting towards the work every
            // TreeView checks for before it paints.
            this.IsDirty = false;

            base.OnClosed(e);
        }

        public override void OnDraw(Graphics2D graphics2D)
        {
            // A last resort for a node that is not under a TreeView, which therefore gets no chance to
            // build its rows before the frame starts (see TreeView.OnDraw). Building here is what produced
            // the one-frame glitch this guards against: the containers above have already baked their
            // transforms into the graphics stack for this frame, so rows parented now paint - and are
            // clipped - against a layout that no longer exists.
            EnsureContentBuilt();

            base.OnDraw(graphics2D);
        }

        public override void OnParentChanged(EventArgs e)
        {
            // Back in the widget tree, so the pre-pass can reach this node again and it owes the gate its
            // count back (see StopAwaitingContent).
            if (Parent != null
                && Interlocked.CompareExchange(ref dirtyState, DirtyCounted, DirtyOrphaned) == DirtyOrphaned)
            {
                Interlocked.Increment(ref nodesAwaitingContent);
            }

            base.OnParentChanged(e);
        }

        public override void OnKeyDown(KeyEventArgs keyEvent)
        {
            base.OnKeyDown(keyEvent);

            var restoreFocus = Focused;

            if (!keyEvent.Handled)
            {
                switch (keyEvent.KeyCode)
                {
                    case Keys.Right:
                        this.Expanded = true;
                        keyEvent.Handled = true;
                        break;

                    case Keys.Left:
                        if (!this.Expanded)
                        {
                            // One key opens and closes the row - for trees that asked for it, and only where
                            // there is something to open. A childless node still walks up to its parent.
                            if (TreeView.LeftArrowTogglesExpansion
                                && this.Nodes.Count > 0)
                            {
                                this.Expanded = true;
                            }
                            else
                            {
                                if (this.NodeParent != null)
                                {
                                    // navigate back up to the parent of this node
                                    TreeView.SelectedNode = this.NodeParent;
                                    TreeView.NotifyItemClicked(TreeView, new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0));
                                }

                                // the selection left this node, so it must not take the focus back
                                restoreFocus = false;
                            }
                        }
                        else
                        {
                            this.Expanded = false;
                        }

                        keyEvent.Handled = true;
                        break;
                }
            }

            if (restoreFocus && !Focused)
            {
                Focus();
            }
        }

        public override void OnTextChanged(EventArgs e)
        {
            if (textWidget != null)
            {
                textWidget.Text = this.Text;
            }

            base.OnTextChanged(e);
        }

        public void Remove() => throw new NotImplementedException();

        public void Toggle()
        {
            content.Visible = !content.Visible;
        }

        public override string ToString()
        {
            return textWidget?.Text ?? "";
        }

        private void Nodes_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
            {
                // Assign NodeParent when items are added
                foreach (var item in e.NewItems)
                {
                    if (item is TreeNode treeNode)
                    {
                        treeNode.NodeParent = this;
                    }
                }
            }

            IsDirty = true;
        }

        // Summary:
        //     Gets the parent tree view that the tree node is assigned to.
        //
        // Returns:
        //     A TreeView that represents the parent tree view that the
        //     tree node is assigned to, or null if the node has not been assigned to a tree
        //     view.
        private void OnImageChanged(EventArgs args)
        {
            ImageChanged?.Invoke(this, null);
        }

        private void RebuildContentSection()
        {
            // Remove but don't close all the current nodes
            var previousRows = content.Children.OfType<TreeNode>().ToList();
            content.RemoveChildren();

            using (content.LayoutLock())
            {
                // Then add them back in (after the change)
                foreach (var node in Nodes)
                {
                    node.NodeParent = this;
                    node.ClearRemovedFlag();
                    content.AddChild(node);
                }
            }

            content.PerformLayout();

            // If the node count is ending at 0 we removed content and need to rebuild the title bar so it will net have a + in it
            expandWidget.Expandable = GetNodeCount(false) != 0;

            // Rows that were not added back are gone from the tree - dropped by a Nodes.Remove or a
            // Nodes.Clear - and are never closed, so without this they would hold their claim on the
            // pre-pass gate for the rest of the session. Their descendants go with them: the whole subtree
            // is unreachable from any TreeView now.
            foreach (var previousRow in previousRows)
            {
                if (previousRow.Parent == null)
                {
                    foreach (var orphan in previousRow.DescendantsAndSelf())
                    {
                        // Only the ones that are out of the widget tree themselves. A descendant whose row
                        // was already built is still parented to its own parent's content, and would never
                        // see the OnParentChanged that puts it back on the count if the subtree came back.
                        if (orphan.Parent == null)
                        {
                            orphan.StopAwaitingContent();
                        }
                    }
                }
            }

            IsDirty = false;
        }

        private class TreeExpandWidget : FlowLayoutWidget
        {
            // Loaded in OnLoad rather than the constructor (disk I/O + recolor per node),
            // matching the PopupMenu.RadioMenuItem pattern.
            private ImageBuffer arrowDown;
            private ImageBuffer arrowRight;
            private readonly ThemedIconButton imageButton = null;
            private readonly ImageBuffer placeholder;
            private readonly ThemeConfig theme;
            private bool _alwaysExpandable;

            private bool? _expandable = null;

            private bool _expanded;

            public TreeExpandWidget(ThemeConfig theme)
            {
                this.theme = theme;
                placeholder = new ImageBuffer(16, 16);

                this.Margin = new BorderDouble(right: 4);

                imageButton = new ThemedIconButton(placeholder, theme)
                {
                    MinimumSize = new Vector2(16 * DeviceScale, 16 * DeviceScale),
                    VAnchor = VAnchor.Center,
                    Selectable = false,
                    Width = 16 * DeviceScale,
                    Height = 16 * DeviceScale
                };

                this.AddChild(imageButton);
            }

            public override void OnLoad(EventArgs args)
            {
                if (arrowRight == null)
                {
                    arrowRight = StaticData.Instance.LoadIcon("fa-angle-right_12.png", 12, 12).GrayToColor(theme.TextColor);
                    arrowDown = StaticData.Instance.LoadIcon("fa-angle-down_12.png", 12, 12).GrayToColor(theme.TextColor);

                    // Catch up on any expansion-state changes made before load
                    this.EnsureExpansionState();
                }

                base.OnLoad(args);
            }

            public bool AlwaysExpandable
            {
                get => _alwaysExpandable;
                set
                {
                    if (arrowRight != null)
                    {
                        imageButton.SetIcon(_expanded ? arrowDown : arrowRight);
                    }

                    _alwaysExpandable = value;
                }
            }

            public bool Expandable
            {
                get => _expandable == true || this.AlwaysExpandable;
                set
                {
                    if (_expandable != value)
                    {
                        _expandable = value;
                    }

                    this.EnsureExpansionState();
                }
            }

            public bool Expanded
            {
                get => _expanded;
                set
                {
                    if (_expanded != value)
                    {
                        _expanded = value;

                        this.EnsureExpansionState();
                    }
                }
            }

            private void EnsureExpansionState()
            {
                if (!this.Expandable)
                {
                    imageButton.SetIcon(placeholder);
                }
                else if (arrowRight != null)
                {
                    imageButton.Visible = true;
                    imageButton.SetIcon(_expanded ? arrowDown : arrowRight);
                }
            }
        }
    }
}