/*
Copyright(c) 2025, Lars Brubaker, John Lewin
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
DISCLAIMED.IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using HtmlAgilityPack;
using Markdig.Renderers;
using Markdig.Renderers.Agg;
using Markdig.Renderers.Html;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;

namespace Markdig.Agg
{
	public class AggMarkdownDocument
	{
		private const int TableBorderAlpha = 150;
		private const int ZebraStripeAlpha = 12;

		private string _markDownText = null;
		private MarkdownPipeline _pipeLine = null;
		private static readonly MarkdownPipeline DefaultPipeline = new MarkdownPipelineBuilder().UseSupportedExtensions().Build();
        public string BasePath { get; private set; }

        public AggMarkdownDocument()
		{
		}

		public AggMarkdownDocument(string basePath)
		{
			this.BasePath = basePath;
		}

		public string MatchingText { get; set; }

        public List<MarkdownDocumentLink> Children { get; private set; } = new List<MarkdownDocumentLink>();

        public static AggMarkdownDocument Load(string uri)
        {
            using (var httpClient = new HttpClient())
            {
                string rawText = httpClient.GetStringAsync(uri).Result;

                return new AggMarkdownDocument(uri)
                {
                    Markdown = rawText,
                };
            }
        }

		/// <summary>
		/// Gets or sets the Markdown to display.
		/// </summary>
		public string Markdown
		{
			get => _markDownText;
			set
			{
				if (_markDownText != value)
				{
					_markDownText = value;
				}
			}
		}

		/// <summary>
		/// Gets or sets the Markdown pipeline to use.
		/// </summary>
		public MarkdownPipeline Pipeline
		{
			get => _pipeLine ?? DefaultPipeline;
			set
			{
				if (_pipeLine != value)
				{
					_pipeLine = value;
				}
			}
		}

		public void Parse(ThemeConfig theme, GuiWidget guiWidget = null)
		{
			if (!string.IsNullOrEmpty(this.Markdown))
			{
				MarkdownPipeline pipeline;

				if (!string.IsNullOrWhiteSpace(MatchingText))
				{
					var builder = new MarkdownPipelineBuilder().UseSupportedExtensions();
					builder.InlineParsers.Add(new MatchingTextParser(MatchingText));

					pipeline = builder.Build();
				}
				else
				{
					pipeline = Pipeline;
				}

				var rootWidget = guiWidget ?? new GuiWidget();

				var renderer = new AggRenderer(rootWidget, theme)
				{
					ChildLinks = new List<MarkdownDocumentLink>()
				};

				pipeline.Setup(renderer);

				var document = Markdig.Markdown.Parse(this.Markdown, pipeline);

				renderer.Render(document);

				this.Children = renderer.ChildLinks;
			}
		}

		public string ToHtml()
		{
			return ToHtml(this.Markdown, this.Pipeline);
		}

		public string ToStyledHtml(ThemeConfig theme)
		{
			return ToStyledHtml(this.Markdown, theme, this.Pipeline);
		}

		public static string ToHtml(string markdown, MarkdownPipeline pipeline = null)
		{
			if (string.IsNullOrEmpty(markdown))
			{
				return string.Empty;
			}

			var activePipeline = pipeline ?? DefaultPipeline;
			var writer = new StringWriter(new StringBuilder());
			var renderer = new HtmlRenderer(writer);
			activePipeline.Setup(renderer);
			var document = Markdig.Markdown.Parse(markdown, activePipeline);
			renderer.Render(document);
			writer.Flush();

			return writer.ToString();
		}

		public static string ToStyledHtml(string markdown, ThemeConfig theme, MarkdownPipeline pipeline = null)
		{
			return StyleHtmlFragment(ToHtml(markdown, pipeline), theme);
		}

		public static string StyleHtmlFragment(string htmlFragment, ThemeConfig theme)
		{
			if (string.IsNullOrWhiteSpace(htmlFragment))
			{
				return string.Empty;
			}

			var activeTheme = theme ?? new ThemeConfig();
			var document = new HtmlDocument();
			document.LoadHtml($"<div>{htmlFragment}</div>");
			var root = document.DocumentNode.FirstChild;
			if (root == null)
			{
				return htmlFragment;
			}

			AppendStyle(root,
				$"font-family:Arial, Helvetica, sans-serif;" +
				$"font-size:{activeTheme.DefaultFontSize}pt;" +
				$"line-height:1.45;" +
				$"color:{CssColor(activeTheme.TextColor, activeTheme.BackgroundColor)};" +
				$"background-color:{CssColor(activeTheme.BackgroundColor)};");

			foreach (var node in root.Descendants().Where(node => node.NodeType == HtmlNodeType.Element))
			{
				var nodeName = node.Name.ToLowerInvariant();
				if (nodeName == "h1")
				{
					AppendStyle(node, "margin:0 0 12px 0;font-size:20pt;font-weight:700;line-height:1.25;");
				}
				else if (nodeName == "h2")
				{
					AppendStyle(node, "margin:0 0 12px 0;font-size:17pt;font-weight:700;line-height:1.3;");
				}
				else if (nodeName == "h3")
				{
					AppendStyle(node, "margin:0 0 12px 0;font-size:15pt;font-weight:700;line-height:1.3;");
				}
				else if (nodeName == "h4")
				{
					AppendStyle(node, "margin:0 0 12px 0;font-size:13pt;font-weight:700;line-height:1.35;");
				}
				else if (nodeName == "h5")
				{
					AppendStyle(node, "margin:0 0 12px 0;font-size:12pt;font-weight:700;line-height:1.35;");
				}
				else if (nodeName == "h6")
				{
					AppendStyle(node, "margin:0 0 12px 0;font-size:11pt;font-weight:700;line-height:1.35;");
				}
				else if (nodeName == "p")
				{
					AppendStyle(node, "margin:0 0 12px 0;");
				}
				else if (nodeName == "ul" || nodeName == "ol")
				{
					AppendStyle(node, "margin:0 0 12px 0;padding-left:24px;");
				}
				else if (nodeName == "li")
				{
					AppendStyle(node, "margin:0 0 3px 0;");
				}
				else if (nodeName == "blockquote")
				{
					AppendStyle(node,
						$"margin:12px 0;padding:0 0 0 12px;border-left:4px solid {CssColor(activeTheme.PrimaryAccentColor, activeTheme.BackgroundColor)};");
				}
				else if (nodeName == "pre")
				{
					AppendStyle(node,
						$"margin:12px 0;padding:6px;background-color:{CssColor(activeTheme.MinimalShade, activeTheme.BackgroundColor)};" +
						"font-family:Consolas, \"Liberation Mono\", Menlo, monospace;font-size:10pt;white-space:pre-wrap;");
				}
				else if (nodeName == "code")
				{
					if (node.ParentNode?.Name.Equals("pre", StringComparison.OrdinalIgnoreCase) == true)
					{
						AppendStyle(node, "font-family:Consolas, \"Liberation Mono\", Menlo, monospace;font-size:10pt;background-color:transparent;padding:0;");
					}
					else
					{
						AppendStyle(node,
							$"font-family:Consolas, \"Liberation Mono\", Menlo, monospace;background-color:{CssColor(activeTheme.MinimalShade, activeTheme.BackgroundColor)};padding:0 2px;");
					}
				}
				else if (nodeName == "hr")
				{
					AppendStyle(node,
						$"border:none;border-top:1px solid {CssColor(activeTheme.TabBarBackground, activeTheme.BackgroundColor)};margin:12px 0;");
				}
				else if (nodeName == "table")
				{
					AppendStyle(node, "margin:12px 0;border-collapse:collapse;border-spacing:0;");
				}
				else if (nodeName == "tr")
				{
					if (IsStripedBodyRow(node))
					{
						AppendStyle(node, $"background-color:{CssColor(new Color(activeTheme.TextColor, ZebraStripeAlpha), activeTheme.BackgroundColor)};");
					}
				}
				else if (nodeName == "th")
				{
					AppendStyle(node,
						$"padding:6px 8px;border:1px solid {CssColor(new Color(activeTheme.TextColor, TableBorderAlpha), activeTheme.BackgroundColor)};" +
						$"font-weight:700;{TableCellTextAlign(node, "left")}");
				}
				else if (nodeName == "td")
				{
					AppendStyle(node,
						$"padding:6px 8px;border:1px solid {CssColor(new Color(activeTheme.TextColor, TableBorderAlpha), activeTheme.BackgroundColor)};" +
						TableCellTextAlign(node));
				}
				else if (nodeName == "a")
				{
					AppendStyle(node, $"color:{CssColor(activeTheme.PrimaryAccentColor, activeTheme.BackgroundColor)};text-decoration:underline;");
				}
				else if (nodeName == "img")
				{
					AppendStyle(node, "max-width:100%;height:auto;");
				}
				else if (nodeName == "strong")
				{
					AppendStyle(node, "font-weight:700;");
				}
				else if (nodeName == "em")
				{
					AppendStyle(node, "font-style:italic;");
				}
				else if (nodeName == "del")
				{
					AppendStyle(node, "text-decoration:line-through;");
				}
			}

			return root.OuterHtml;
		}

		private static void AppendStyle(HtmlNode node, string style)
		{
			if (node == null || string.IsNullOrWhiteSpace(style))
			{
				return;
			}

			var existing = node.GetAttributeValue("style", string.Empty);
			if (!string.IsNullOrWhiteSpace(existing) && !existing.TrimEnd().EndsWith(";", StringComparison.Ordinal))
			{
				existing += ";";
			}

			node.SetAttributeValue("style", existing + style);
		}

		private static bool IsStripedBodyRow(HtmlNode node)
		{
			if (!node.Name.Equals("tr", StringComparison.OrdinalIgnoreCase)
				|| !node.ParentNode?.Name.Equals("tbody", StringComparison.OrdinalIgnoreCase) == true)
			{
				return false;
			}

			var rowIndex = node.ParentNode.ChildNodes
				.Where(child => child.NodeType == HtmlNodeType.Element && child.Name.Equals("tr", StringComparison.OrdinalIgnoreCase))
				.TakeWhile(child => child != node)
				.Count();

			return rowIndex % 2 == 1;
		}

		private static string TableCellTextAlign(HtmlNode node, string defaultAlign = null)
		{
			var align = node.GetAttributeValue("align", defaultAlign);
			return string.IsNullOrWhiteSpace(align)
				? string.Empty
				: $"text-align:{align.Trim().ToLowerInvariant()};";
		}

		private static string CssColor(Color color, Color? backgroundOverride = null)
		{
			var background = backgroundOverride ?? Color.White;
			var opaqueColor = color.Alpha0To255 < 255
				? ThemeConfig.ResolveColor2(background, color)
				: color;

			return $"#{opaqueColor.Red0To255:X2}{opaqueColor.Green0To255:X2}{opaqueColor.Blue0To255:X2}";
		}
	}

    public class MarkdownPathHandler
    {
        private string basePath;
        private string currentDirectory;

        public MarkdownPathHandler(string initialBasePath)
        {
            // Normalize the base path to use platform-specific directory separators
            basePath = Path.GetFullPath(initialBasePath.Replace('/', Path.DirectorySeparatorChar));
            currentDirectory = basePath;
        }

        public string ResolvePath(string relativePath)
        {
            // Handle absolute paths
            if (Path.IsPathRooted(relativePath))
            {
                return Path.GetFullPath(relativePath);
            }

            // Normalize slashes to platform-specific separator
            relativePath = relativePath.Replace('/', Path.DirectorySeparatorChar);

            // Combine the current directory with the relative path
            string fullPath = Path.Combine(currentDirectory, relativePath);

            // Normalize the path (resolve .. and . segments)
            fullPath = Path.GetFullPath(fullPath);

            // Verify the resolved path is still under the base path for security
            if (!fullPath.StartsWith(basePath))
            {
                throw new InvalidOperationException("Resolved path is outside the base directory");
            }

            return fullPath;
        }

        public void UpdateCurrentDirectory(string newPath)
        {
            // Get the directory of the new path
            string newDir = Path.GetDirectoryName(newPath);
            if (newDir != null)
            {
                // Update the current directory while maintaining the base path constraint
                string fullPath = Path.GetFullPath(newDir);
                if (fullPath.StartsWith(basePath))
                {
                    currentDirectory = fullPath;
                }
            }
        }

        /// <summary>
        /// Directly sets the working directory for relative link resolution without modifying basePath.
        /// Used when navigating between articles within a known-safe document root.
        /// </summary>
        public void SetCurrentDirectory(string directory)
        {
            directory = directory.Replace('/', Path.DirectorySeparatorChar).TrimEnd(Path.DirectorySeparatorChar);
            currentDirectory = Path.GetFullPath(directory);
        }

        public string GetRelativePath(string fullPath)
        {
            return Path.GetRelativePath(currentDirectory, fullPath);
        }
    }
}
