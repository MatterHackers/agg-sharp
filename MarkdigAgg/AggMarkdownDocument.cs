/*
Copyright(c) 2018, Lars Brubaker, John Lewin
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
using System.Net.Http;
using Markdig.Renderers;
using Markdig.Renderers.Agg;
using MatterHackers.Agg.UI;

namespace Markdig.Agg
{
	public class AggMarkdownDocument
	{
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

        public string GetRelativePath(string fullPath)
        {
            return Path.GetRelativePath(currentDirectory, fullPath);
        }
    }
}
