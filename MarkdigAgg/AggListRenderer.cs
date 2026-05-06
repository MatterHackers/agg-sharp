// Copyright (c) 2026, Lars Brubaker, Nicolas Musset. All rights reserved.
// This file is licensed under the MIT license.
// See the LICENSE.md file in the project root for more information.

using System;
using Markdig.Agg;
using Markdig.Syntax;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;

namespace Markdig.Renderers.Agg
{
	public class ListX : FlowLayoutWidget
	{
		public ListX(int depth)
			: base(FlowDirection.TopToBottom)
		{
			this.VAnchor = VAnchor.Fit;
			this.HAnchor = HAnchor.Stretch;
			this.Margin = new BorderDouble(left: (depth + 1) * 14);
		}
	}

	public class ListItemX : FlowLayoutWidget
	{
		private readonly FlowLayoutWidget content;

		public ListItemX(ThemeConfig theme, string markerText)
		{
			this.VAnchor = VAnchor.Fit;
			this.HAnchor = HAnchor.Stretch;
			this.Margin = new BorderDouble(bottom: 3);

			base.AddChild(new MarkdownTextWidget(markerText, pointSize: 11, textColor: theme.TextColor)
			{
				Margin = new BorderDouble(right: 2),
				VAnchor = VAnchor.Top,
				MinimumSize = new Vector2(7, 0)
			});

			base.AddChild(content = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch
			});
		}

		public override GuiWidget AddChild(GuiWidget childToAdd, int indexInChildrenList = -1)
		{
			// TODOD: Anything else required for list children?
			return content.AddChild(childToAdd, indexInChildrenList);
		}
	}

	public class AggListRenderer : AggObjectRenderer<ListBlock>
	{
		private ThemeConfig theme;

		public AggListRenderer(ThemeConfig theme)
		{
			this.theme = theme;
		}

		protected override void Write(AggRenderer renderer, ListBlock listBlock)
		{
			var depth = GetListDepth(listBlock);
			renderer.Push(new ListX(depth)); // list);

			int orderedIndex = 1;
			if (listBlock.IsOrdered && !string.IsNullOrWhiteSpace(listBlock.OrderedStart))
			{
				orderedIndex = Math.Max(1, int.Parse(listBlock.OrderedStart));
			}

			foreach (var item in listBlock)
			{
				var markerText = listBlock.IsOrdered ? $"{orderedIndex}." : "-";
				renderer.Push(new ListItemX(theme, markerText));
				renderer.WriteChildren(item as ListItemBlock);
				renderer.Pop();
				orderedIndex++;
			}

			renderer.Pop();
		}

		private static int GetListDepth(ListBlock listBlock)
		{
			int depth = 0;
			var parent = listBlock.Parent;
			while (parent != null)
			{
				if (parent is ListBlock)
				{
					depth++;
				}

				parent = parent.Parent;
			}

			return depth;
		}
	}
}
