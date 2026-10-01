/*
Copyright(c) 2026, Lars Brubaker
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

using MatterHackers.Agg.UI;

namespace Markdig.Agg.Editing
{
	/// <summary>
	/// The WYSIWYG markdown editor as a host places it: the formatting toolbar above the editing widget, with the
	/// toolbar acting on the widget through <see cref="IRichEditCommands"/> and the widget's Cmd/Ctrl+K opening the
	/// toolbar's link row. A host with its own link dialog handles <see cref="MarkdownFormatToolbar.LinkRequested"/>
	/// on <see cref="Toolbar"/>; both the button and the shortcut then go to it.
	/// </summary>
	public class RichMarkdownEditor : FlowLayoutWidget
	{
		public RichMarkdownEditor(ThemeConfig theme)
			: base(FlowDirection.TopToBottom)
		{
			HAnchor = HAnchor.Stretch;
			VAnchor = VAnchor.Stretch;

			Editor = new RichMarkdownEditWidget(theme)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
			};
			Toolbar = new MarkdownFormatToolbar(theme, Editor);

			AddChild(Toolbar);
			AddChild(Editor);

			Editor.LinkRequested += (s, e) => Toolbar.ShowLinkEditor();
		}

		public MarkdownFormatToolbar Toolbar { get; }

		public RichMarkdownEditWidget Editor { get; }

		/// <summary>
		/// The document as markdown; setting it loads a new document (and clears the undo history).
		/// </summary>
		public string Markdown
		{
			get => Editor.Markdown;
			set
			{
				Editor.Markdown = value;

				// Loading is not an edit and raises no event, so the toolbar is told directly.
				Toolbar.Refresh();
			}
		}

		public string EmptyHint
		{
			get => Editor.EmptyHint;
			set => Editor.EmptyHint = value;
		}

		/// <summary>
		/// The editing area's theme. The toolbar keeps the theme it was built with: its buttons bake their colours
		/// in, so a theme change rebuilds the whole editor.
		/// </summary>
		public ThemeConfig Theme
		{
			get => Editor.Theme;
			set => Editor.Theme = value;
		}
	}
}
