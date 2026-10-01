// Copyright (c) 2016-2017 Nicolas Musset. All rights reserved.
// This file is licensed under the MIT license. 
// See the LICENSE.md file in the project root for more information.

using System;
using System.IO;
using Markdig.Agg;
using Markdig.Helpers;
using Markdig.Syntax;
using MatterHackers.Agg;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;

namespace Markdig.Renderers.Agg
{
	public class CodeBlockX : FlowLayoutWidget
	{
		private static TypeFace monoTypeFace;
		private readonly ThemeConfig theme;

		public CodeBlockX(ThemeConfig theme)
			: base(FlowDirection.TopToBottom)
		{
			this.theme = theme;
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Fit;
			this.Margin = 12;
			this.Padding = 6;
			this.BackgroundColor = theme.MinimalShade;
		}

		public void AddLine(StringSlice slice)
		{
			var text = slice.Text == null || slice.Start > slice.End
				? string.Empty
				: slice.Text.Substring(slice.Start, slice.Length);

			var textWidget = new MarkdownTextWidget(text, pointSize: 10, textColor: theme.TextColor, ellipsisIfClipped: false, typeFace: GetMonoTypeFace())
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
				AutoExpandBoundsToText = true,
				Padding = new BorderDouble(bottom: 3)
			};

			textWidget.DoExpandBoundsToText();
			base.AddChild(textWidget);
		}

		// Internal so the rich editor's layout (Editing/RichLayoutStyle) draws code in the same face as the viewer.
		internal static TypeFace GetMonoTypeFace()
		{
			if (monoTypeFace == null)
			{
				var monoFontPath = ResolveMonoFontPath();
				monoTypeFace = monoFontPath != null
					? TypeFace.LoadFrom(File.ReadAllText(monoFontPath))
					: AggContext.DefaultFont;
			}

			return monoTypeFace;
		}

		private static string ResolveMonoFontPath()
		{
			string[] rootCandidates =
			{
				StaticData.RootPath,
				AppContext.BaseDirectory
			};

			foreach (var root in rootCandidates)
			{
				if (string.IsNullOrWhiteSpace(root))
				{
					continue;
				}

				foreach (var relativePath in new[]
				{
					Path.Combine("Fonts", "LiberationMono.svg"),
					Path.Combine("fonts", "LiberationMono.svg"),
					Path.Combine("liberation-fonts-ttf-1.07.0", "LiberationMono.svg")
				})
				{
					var candidate = Path.Combine(root, relativePath);
					if (File.Exists(candidate))
					{
						return candidate;
					}
				}
			}

			var currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);
			for (int i = 0; i < 8 && currentDirectory != null; i++)
			{
				foreach (var relativePath in new[]
				{
					Path.Combine("StaticData", "Fonts", "LiberationMono.svg"),
					Path.Combine("liberation-fonts-ttf-1.07.0", "LiberationMono.svg"),
					Path.Combine("Submodules", "agg-sharp", "liberation-fonts-ttf-1.07.0", "LiberationMono.svg")
				})
				{
					var candidate = Path.Combine(currentDirectory.FullName, relativePath);
					if (File.Exists(candidate))
					{
						return candidate;
					}
				}

				currentDirectory = currentDirectory.Parent;
			}

			return null;
		}
	}

	public class AggCodeBlockRenderer : AggObjectRenderer<CodeBlock>
    {
		private ThemeConfig theme;

		public AggCodeBlockRenderer(ThemeConfig theme)
		{
			this.theme = theme;
		}

        protected override void Write(AggRenderer renderer, CodeBlock obj)
        {
			var codeBlock = new CodeBlockX(theme);

			if (obj?.Lines.Lines != null)
			{
				var lines = obj.Lines;
				var slices = lines.Lines;
				for (var i = 0; i < lines.Count; i++)
				{
					codeBlock.AddLine(slices[i].Slice);
				}
			}

			renderer.WriteBlock(codeBlock);
        }
    }
}
