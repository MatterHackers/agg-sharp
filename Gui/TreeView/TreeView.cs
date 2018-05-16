/*
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

using MatterHackers.VectorMath;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;

namespace MatterHackers.Agg.UI.TreeView
{
	public class TreeView : ScrollableWidget
	{
		public ObservableCollection<TreeNode> Nodes { get; } = new ObservableCollection<TreeNode>();
		FlowLayoutWidget content;
		public Color TextColor { get; set; } = Color.Black;
		public double PointSize { get; set; } = 12;

		public TreeView()
			: this(0, 0)
		{

		}

		public TreeView(int width, int height)
			: base(width, height)
		{
			content = new FlowLayoutWidget()
			{
				VAnchor = VAnchor.Fit | VAnchor.Top
			};
			this.AddChild(content);
			Nodes.CollectionChanged += Nodes_CollectionChanged;
		}

		private void Nodes_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			content.CloseAllChildren();
			var currentNodes = Nodes.ToArray();
			AddNodes(currentNodes);
		}

		private void AddNodes(TreeNode[] currentNodes)
		{
			foreach (var node in currentNodes)
			{
				var childNodes = node.Nodes.ToArray();
				if (childNodes.Length > 0)
				{
					AddNodes(childNodes);
				}
				else
				{
					content.AddChild(new TextWidget(node.Text, pointSize: PointSize, textColor: TextColor));
				}
			}
		}
	}
}