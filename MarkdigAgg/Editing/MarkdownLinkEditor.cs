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

using System;
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;

namespace Markdig.Agg.Editing
{
	/// <summary>
	/// The toolbar's built-in link row: "Link" [url] OK Remove Cancel, shown under the strip. A row rather than a
	/// popup keeps it in the toolbar's own layout, so it needs no window or popup positioning and a host with its
	/// own dialog can skip it (<see cref="MarkdownFormatToolbar.LinkRequested"/>). Remove shows only when editing an
	/// existing link. Escape cancels.
	/// </summary>
	public class MarkdownLinkEditor : FlowLayoutWidget
	{
		private readonly ThemedTextButton removeButton;

		public MarkdownLinkEditor(ThemeConfig theme)
		{
			HAnchor = HAnchor.Stretch;
			VAnchor = VAnchor.Fit;
			Padding = new BorderDouble(3, 2);

			AddChild(new TextWidget("Link:", pointSize: theme.DefaultFontSize, textColor: theme.TextColor)
			{
				VAnchor = VAnchor.Center,
				Margin = new BorderDouble(0, 0, 4, 0),
			});

			AddChild(UrlField = new ThemedTextEditWidget("", theme, messageWhenEmptyAndNotSelected: "https://")
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Center,
				ToolTipText = "The web address the link opens",
			});
			UrlField.ActualTextEditWidget.EnterPressed += (s, e) => Accept();

			var okButton = theme.CreateDialogButton("OK");
			okButton.ToolTipText = "Link the selected text to this address";
			okButton.Click += (s, e) => Accept();
			AddChild(okButton);

			removeButton = theme.CreateDialogButton("Remove");
			removeButton.ToolTipText = "Remove the link and keep its text";
			removeButton.Click += (s, e) => Remove();
			AddChild(removeButton);

			var cancelButton = theme.CreateDialogButton("Cancel");
			cancelButton.ToolTipText = "Close without changing the link";
			cancelButton.Click += (s, e) => Cancel();
			AddChild(cancelButton);
		}

		/// <summary>
		/// Raised with the typed URL when OK (or Enter) is pressed.
		/// </summary>
		public event EventHandler<string> Accepted;

		public event EventHandler Removed;

		/// <summary>
		/// Raised when Cancel or Escape closes the row without a change, so the toolbar can give the text its focus back.
		/// </summary>
		public event EventHandler Cancelled;

		public ThemedTextEditWidget UrlField { get; }

		public string Url
		{
			get => UrlField.Text;
			set => UrlField.Text = value ?? "";
		}

		/// <summary>
		/// Shows the row holding <paramref name="currentUrl"/> (empty for a new link) and focuses the box.
		/// </summary>
		public void Open(string currentUrl)
		{
			Url = currentUrl;
			removeButton.Visible = currentUrl != null;
			Visible = true;
			UrlField.Focus();
		}

		/// <summary>
		/// <paramref name="url"/> trimmed, with "https://" added when it has no scheme and reads as a web address:
		/// a novice types "example.com" and expects the link to open that site, not a file of that name. It reads
		/// as one when the part before any '/' holds a dot and no spaces, and does not end in a file type a
		/// document links to (page.md, image.png stay relative). Paths, anchors and anything with a scheme
		/// (mailto:, http://) are left alone.
		/// </summary>
		public static string WithScheme(string url)
		{
			url = url?.Trim() ?? "";
			if (url.Length == 0 || url.Any(char.IsWhiteSpace) || url[0] == '/' || url[0] == '.' || url[0] == '#')
			{
				return url;
			}

			string host = url.Split('/', '?', '#')[0];
			if (host.Contains(':') || !host.Contains('.') || host.EndsWith("."))
			{
				return url;
			}

			string ending = host.Substring(host.LastIndexOf('.') + 1).ToLowerInvariant();
			return FileEndings.Contains(ending) ? url : "https://" + url;
		}

		private static readonly string[] FileEndings =
		{
			"md", "markdown", "html", "htm", "txt", "pdf", "png", "jpg", "jpeg", "gif", "svg", "webp", "mcx", "stl",
		};

		/// <summary>
		/// Hides the row with no event: the caret moved elsewhere, or the toolbar now edits another document.
		/// </summary>
		public void Close() => Visible = false;

		public void Cancel()
		{
			Visible = false;
			Cancelled?.Invoke(this, EventArgs.Empty);
		}

		public override void OnKeyDown(KeyEventArgs keyEvent)
		{
			if (keyEvent.KeyCode == Keys.Escape && Visible)
			{
				keyEvent.Handled = true;
				keyEvent.SuppressKeyPress = true;
				Cancel();
				return;
			}

			base.OnKeyDown(keyEvent);
		}

		public void Accept()
		{
			Visible = false;
			Accepted?.Invoke(this, Url);
		}

		public void Remove()
		{
			Visible = false;
			Removed?.Invoke(this, EventArgs.Empty);
		}
	}
}
