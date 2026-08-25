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

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using MatterHackers.Agg.UI;

namespace MatterHackers.Agg.Platform
{
	/// <summary>
	/// File dialogs on Linux, over whichever of <c>zenity</c> (GTK) or <c>kdialog</c> (KDE) is installed.
	/// </summary>
	/// <remarks>
	/// <para>
	/// There is no file dialog in X11 and no dialog API in this assembly's dependency set - PlatformLinux
	/// takes no NuGet or native dependency of its own, so binding GTK or the xdg-desktop-portal D-Bus
	/// interface directly is off the table. What every desktop does ship is a helper that puts up its own
	/// native chooser and prints the chosen paths on stdout, and that is what this drives. zenity is tried
	/// first because it is the one present on GNOME, Xfce, Cinnamon, MATE and most bare X sessions;
	/// kdialog covers KDE, where zenity often is not installed.
	/// </para>
	/// <para>
	/// <b>This differs from mac and Windows in one visible way: the call does not block.</b>
	/// <c>NSOpenPanel.runModal</c> and WinForms' <c>ShowDialog</c> both spin a nested native modal loop, so
	/// the caller sits inside the dialog call until the user answers and the provider can return the answer
	/// as its return value. X11 has no such nested loop to borrow, and running one here by pumping
	/// <c>X11SystemWindow</c>'s event loop from inside a dialog call would re-enter the loop from an idle
	/// action - the exact re-entrancy the window host guards against. So the helper runs on a thread-pool
	/// thread and the result comes back through <c>UiThread.RunOnIdle</c>, which is the same place the
	/// callback lands on the other two hosts. The return value therefore means <i>the dialog was shown</i>,
	/// not <i>the user picked something</i>, and callers must use the callback for the answer. Every caller
	/// in this repo already does - the mac host's own callers had to, because RunOnIdle keeps ticking under
	/// runModal there too.
	/// </para>
	/// <para>
	/// The consequence to know about: the agg window underneath stays interactive while the chooser is up,
	/// where on mac and Windows it is blocked. Nothing here breaks from a second click, but a user can
	/// start a second dialog on top of the first. A modal scrim (or an X11 grab) is the follow-up if that
	/// turns out to matter in practice.
	/// </para>
	/// <para>
	/// Cancel is silent, matching <c>MacFileDialogProvider</c>: the helper exits 1, no callback is invoked,
	/// and the caller's params are left exactly as they were on the way in. Only <see cref="OpenFileDialog"/>
	/// clears anything, and it does that up front rather than on cancel - again mirroring mac - so a params
	/// object reused across calls cannot hand back last time's answer. A helper that <i>fails</i> rather
	/// than being cancelled is not silent; see <see cref="DescribeFailure"/>.
	/// </para>
	/// </remarks>
	public class LinuxFileDialogProvider : IFileDialogProvider
	{
		/// <summary>Which helper this machine has. Probed once - PATH does not change under a running app.</summary>
		private static readonly Lazy<DialogTool> InstalledTool = new Lazy<DialogTool>(ProbeForTool);

		private enum DialogTool
		{
			/// <summary>Neither helper is installed; dialogs cannot be shown at all.</summary>
			None,

			Zenity,

			KDialog,
		}

		public string LastDirectoryUsed { get; private set; }

		/// <summary>Linux paths need no translation; this exists for platforms whose paths do.</summary>
		public string ResolveFilePath(string path) => path;

		public bool OpenFileDialog(OpenFileDialogParams openParams, Action<OpenFileDialogParams> callback)
		{
			// Reset first, exactly as the mac provider does, so a caller that keeps a params object around
			// never reads last time's answer as this time's.
			openParams.FileName = string.Empty;
			openParams.FileNames = null;

			return this.ShowDialog(
				openParams.MultiSelect,
				tool => tool == DialogTool.Zenity ? BuildZenityArguments(openParams) : BuildKdialogArguments(openParams),
				paths =>
				{
					openParams.FileNames = paths;
					openParams.FileName = paths[0];
					this.LastDirectoryUsed = Path.GetDirectoryName(paths[0]);

					callback?.Invoke(openParams);
				});
		}

		public bool SaveFileDialog(SaveFileDialogParams saveParams, Action<SaveFileDialogParams> callback)
		{
			return this.ShowDialog(
				multipleSelection: false,
				tool => tool == DialogTool.Zenity ? BuildZenityArguments(saveParams) : BuildKdialogArguments(saveParams),
				paths =>
				{
					saveParams.FileName = paths[0];
					saveParams.FileNames = new[] { paths[0] };
					this.LastDirectoryUsed = Path.GetDirectoryName(paths[0]);

					callback?.Invoke(saveParams);
				});
		}

		public bool SelectFolderDialog(SelectFolderDialogParams folderParams, Action<SelectFolderDialogParams> callback)
		{
			return this.ShowDialog(
				multipleSelection: false,
				tool => tool == DialogTool.Zenity ? BuildZenityArguments(folderParams) : BuildKdialogArguments(folderParams),
				paths =>
				{
					folderParams.FolderPath = paths[0];
					this.LastDirectoryUsed = paths[0];

					callback?.Invoke(folderParams);
				});
		}

		/// <summary>
		/// Opens a file manager on the file's folder with the file selected, falling back to just opening
		/// the folder.
		/// </summary>
		/// <remarks>
		/// The same two-step MatterCAD's <c>LinuxShellIntegration</c> uses, and deliberately a copy of it:
		/// that class lives in the application and this assembly is a library underneath it, so there is no
		/// reference to share. There is no "reveal" command on Linux the way <c>open -R</c> is on macOS;
		/// what there is is <c>org.freedesktop.FileManager1.ShowItems</c>, which Nautilus, Dolphin, Nemo,
		/// Thunar and PCManFM all implement. Where nothing owns that bus name the call fails and
		/// <c>xdg-open</c> on the containing folder is the honest degradation - right folder, file not
		/// preselected.
		/// </remarks>
		public void ShowFileInFolder(string fileName)
		{
			if (string.IsNullOrEmpty(fileName))
			{
				return;
			}

			var showItems = new ProcessStartInfo("dbus-send")
			{
				UseShellExecute = false,
			};

			showItems.ArgumentList.Add("--session");

			// Without --print-reply dbus-send fires the call off and exits 0 whether or not anything is
			// listening, which would make the exit code meaningless and the xdg-open fallback below dead
			// code. With it, dbus-send waits for the reply and exits non-zero when the name is unowned or
			// the method failed - which is exactly the "no file manager here" signal the fallback needs.
			showItems.ArgumentList.Add("--print-reply");
			showItems.ArgumentList.Add("--dest=org.freedesktop.FileManager1");
			showItems.ArgumentList.Add("--type=method_call");
			showItems.ArgumentList.Add("/org/freedesktop/FileManager1");
			showItems.ArgumentList.Add("org.freedesktop.FileManager1.ShowItems");
			showItems.ArgumentList.Add(BuildShowItemsArgument(fileName));
			showItems.ArgumentList.Add("string:");

			if (TryRunToCompletion(showItems))
			{
				return;
			}

			string directory = Path.GetDirectoryName(Path.GetFullPath(fileName));
			if (string.IsNullOrEmpty(directory))
			{
				return;
			}

			var openFolder = new ProcessStartInfo("xdg-open")
			{
				UseShellExecute = false,
			};

			openFolder.ArgumentList.Add(directory);

			TryStart(openFolder);
		}

		/// <summary>
		/// Builds the <c>array:string:</c> argument naming <paramref name="filePath"/> for
		/// <c>dbus-send</c>'s <c>org.freedesktop.FileManager1.ShowItems</c> call.
		/// </summary>
		/// <remarks>
		/// Two escapings, both load-bearing. ShowItems takes file:// URIs, so the path is percent-encoded
		/// (<see cref="Uri.AbsoluteUri"/> does it) - without that a <c>#</c> in a name truncates the path at
		/// the fragment. On top of that dbus-send splits an <c>array:</c> value on commas, so a file named
		/// <c>a,b.stl</c> would arrive as two paths that do not exist and the reveal would silently do
		/// nothing; a comma is legal unencoded in a URI path so AbsoluteUri leaves it, and this encodes it.
		/// <c>%2C</c> is right on both sides - the file manager decodes it back to a comma. None of this is
		/// shell quoting: the value is one argv entry and there is no shell in this path.
		/// </remarks>
		internal static string BuildShowItemsArgument(string filePath)
		{
			string uri = new Uri(Path.GetFullPath(filePath)).AbsoluteUri;

			return "array:string:" + uri.Replace(",", "%2C");
		}

		/// <summary>
		/// Spawns the installed helper with <paramref name="buildArguments"/>'s argument list and hands the
		/// chosen paths to <paramref name="onAccepted"/> on the UI thread.
		/// </summary>
		/// <returns>
		/// True if a dialog was launched. False only when neither helper is installed - which is reported
		/// once to stderr and never thrown, because a missing optional package must not take the
		/// application down over a menu item.
		/// </returns>
		private bool ShowDialog(bool multipleSelection, Func<DialogTool, List<string>> buildArguments, Action<string[]> onAccepted)
		{
			DialogTool tool = InstalledTool.Value;
			if (tool == DialogTool.None)
			{
				Console.Error.WriteLine("File dialogs on Linux need zenity or kdialog installed; neither was found on PATH.");
				return false;
			}

			string executable = tool == DialogTool.Zenity ? "zenity" : "kdialog";
			List<string> arguments = buildArguments(tool);

			// Off the UI thread deliberately - see the class remarks. Blocking here would stop the X11
			// event loop, and with it painting and the RunOnIdle pump that delivers this very callback.
			_ = Task.Run(async () =>
			{
				string[] paths = await RunDialogAsync(executable, arguments, multipleSelection).ConfigureAwait(false);

				// Empty means cancelled, or a failure that RunDialogAsync has already reported. Either way
				// there is no answer to deliver, and the mac provider is equally silent on a cancel.
				if (paths.Length == 0)
				{
					return;
				}

				UiThread.RunOnIdle(() => onAccepted(paths));
			});

			return true;
		}

		/// <summary>
		/// Runs the helper to completion and returns what the user chose - an empty array for both cancel
		/// and failure, with failure additionally reported to <see cref="UiThread.UnhandledException"/>.
		/// </summary>
		private static async Task<string[]> RunDialogAsync(string executable, List<string> arguments, bool multipleSelection)
		{
			try
			{
				var startInfo = new ProcessStartInfo(executable)
				{
					UseShellExecute = false,
					RedirectStandardOutput = true,
					RedirectStandardError = true,
				};

				foreach (string argument in arguments)
				{
					startInfo.ArgumentList.Add(argument);
				}

				using (var process = Process.Start(startInfo))
				{
					if (process == null)
					{
						// Documented as possible when an existing process is reused; there is no helper to
						// wait on and no answer coming, so the caller would otherwise wait forever.
						Report($"{executable} could not be started.");
						return Array.Empty<string>();
					}

					// Both pipes are drained concurrently, and before the wait. GTK is chatty on stderr
					// (dconf and theme warnings), and a full stderr pipe would wedge the helper forever
					// while we sat waiting for it to exit.
					Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
					Task<string> standardError = process.StandardError.ReadToEndAsync();

					await Task.WhenAll(standardOutput, standardError).ConfigureAwait(false);
					await process.WaitForExitAsync().ConfigureAwait(false);

					string failure = DescribeFailure(executable, process.ExitCode, standardError.Result);
					if (failure != null)
					{
						Report(failure);
						return Array.Empty<string>();
					}

					return ParseDialogOutput(process.ExitCode, standardOutput.Result, multipleSelection);
				}
			}
			catch (Exception exception)
			{
				// The helper vanished between the PATH probe and here, or could not be executed at all.
				// This has to be reported and not merely traced: Debug.WriteLine compiles out of a Release
				// build, and the symptom without it is a menu item that does nothing at all.
				Report($"Failed to run {executable}: {exception.Message}");
				return Array.Empty<string>();
			}
		}

		/// <summary>
		/// Routes a dialog failure to the channel the crash reporter and the automation tests both watch.
		/// </summary>
		/// <remarks>
		/// Raised from the thread-pool thread the helper ran on. <c>UiThread.ReportUnhandledException</c>
		/// swallows anything a subscriber throws, so this can never take down the dialog path itself.
		/// </remarks>
		private static void Report(string message)
		{
			UiThread.ReportUnhandledException(new InvalidOperationException(message));
		}

		/// <summary>
		/// Decides whether a finished helper failed, and with what message - or null for the two normal
		/// outcomes, accept (exit 0) and cancel (exit 1).
		/// </summary>
		/// <remarks>
		/// <para>
		/// Any exit code outside {0, 1} is a failure by itself: zenity answers 255 for an option it does not
		/// understand, which is what a filter or flag this provider builds wrongly would look like, and
		/// silently treating that as "the user cancelled" is how such a bug survives to a release.
		/// </para>
		/// <para>
		/// Exit 1 needs a second look, because zenity also exits 1 when it cannot open the display - a real
		/// failure wearing the cancel code. What it cannot do is use stderr alone to tell them apart: GTK
		/// prints a paragraph of dconf, DRI3 and theme warnings to stderr during a <i>perfectly ordinary</i>
		/// cancel, and treating any stderr output as failure would file a crash report (Program.cs feeds
		/// this channel straight to the crash reporter) every time a user backs out of an Open dialog. So
		/// the GLib-formatted diagnostic lines are filtered out first, and only what is left - a plain
		/// message, the shape a tool uses for its own errors - counts as evidence.
		/// </para>
		/// </remarks>
		internal static string DescribeFailure(string executable, int exitCode, string standardError)
		{
			if (exitCode == 0)
			{
				return null;
			}

			string complaint = ToolComplaint(standardError);

			if (exitCode == 1 && complaint == null)
			{
				// The ordinary cancel.
				return null;
			}

			return complaint == null
				? $"{executable} exited with code {exitCode}."
				: $"{executable} exited with code {exitCode}: {complaint}";
		}

		/// <summary>
		/// Strips GLib's own diagnostic lines out of stderr and returns what a tool said in its own voice,
		/// or null if it said nothing.
		/// </summary>
		/// <remarks>
		/// GLib formats every warning it emits as either <c>(name:pid): DOMAIN-LEVEL **: time: text</c> or
		/// <c>DOMAIN-Message: text</c>, and Mesa's EGL loader prefixes its own with <c>libEGL warning:</c>.
		/// A tool reporting its own failure - <c>This option is not available...</c> from zenity - has none
		/// of that structure, which is the only durable way to tell the two apart without matching on
		/// message text that changes with the locale.
		/// </remarks>
		private static string ToolComplaint(string standardError)
		{
			if (string.IsNullOrWhiteSpace(standardError))
			{
				return null;
			}

			foreach (string line in standardError.Split('\n'))
			{
				string trimmed = line.Trim();

				if (trimmed.Length == 0
					|| trimmed.StartsWith("(", StringComparison.Ordinal)

					// GLib drops the domain when a message has none, and then leads with the "** " marker
					// instead: "** (zenity:123): WARNING **: ...", "** Message: ...". Both forms lose the
					// hyphen the domain-ed checks below key on, so without this a cancel on any desktop
					// lacking at-spi ("Couldn't connect to accessibility bus") files a crash report.
					|| trimmed.StartsWith("** ", StringComparison.Ordinal)
					|| trimmed.StartsWith("libEGL warning:", StringComparison.Ordinal)
					|| trimmed.Contains("-WARNING **:", StringComparison.Ordinal)
					|| trimmed.Contains("-CRITICAL **:", StringComparison.Ordinal)
					|| trimmed.Contains("-Message:", StringComparison.Ordinal))
				{
					continue;
				}

				return trimmed;
			}

			return null;
		}

		/// <summary>
		/// Turns a helper's exit code and stdout into the chosen paths.
		/// </summary>
		/// <remarks>
		/// <para>
		/// Both helpers agree on the shape: exit 0 and the chosen path on stdout, with a trailing newline.
		/// </para>
		/// <para>
		/// Splitting on newlines is correct <i>only</i> for a multi-select, where the arguments asked for a
		/// newline separator. A newline is perfectly legal in a Unix filename - nothing but <c>/</c> and NUL
		/// is illegal - so newline is not a safe separator, only the least bad one on offer: zenity's
		/// default is <c>|</c> and kdialog's is a space, and both of those turn up in real filenames
		/// constantly where a newline essentially never does. For a single selection there is no separator
		/// problem to have, so nothing is split and the whole of stdout is the path, minus the one trailing
		/// newline the helper added - which is what makes a single file named with a newline work.
		/// </para>
		/// </remarks>
		internal static string[] ParseDialogOutput(int exitCode, string standardOutput, bool multipleSelection)
		{
			if (exitCode != 0 || string.IsNullOrEmpty(standardOutput))
			{
				return Array.Empty<string>();
			}

			if (!multipleSelection)
			{
				string only = TrimOneTrailingNewline(standardOutput);

				return only.Length == 0 ? Array.Empty<string>() : new[] { only };
			}

			var paths = new List<string>();
			foreach (string line in standardOutput.Split('\n'))
			{
				// Only the carriage return of a CRLF comes off. Spaces do not: a trailing space is a legal
				// and unremarkable part of a filename, and trimming it yields a path that does not exist.
				string path = line.Trim('\r');
				if (path.Length > 0)
				{
					paths.Add(path);
				}
			}

			return paths.ToArray();
		}

		/// <summary>Removes the single line terminator a helper prints after the path, and nothing else.</summary>
		private static string TrimOneTrailingNewline(string output)
		{
			if (output.EndsWith("\n", StringComparison.Ordinal))
			{
				output = output.Substring(0, output.Length - 1);
			}

			if (output.EndsWith("\r", StringComparison.Ordinal))
			{
				output = output.Substring(0, output.Length - 1);
			}

			return output;
		}

		internal static List<string> BuildZenityArguments(OpenFileDialogParams openParams)
		{
			var arguments = new List<string> { "--file-selection" };

			AddZenityTitle(arguments, openParams.Title);

			if (openParams.MultiSelect)
			{
				arguments.Add("--multiple");
				arguments.Add("--separator=\n");
			}

			AddZenityFilename(arguments, DirectoryArgument(openParams.InitialDirectory));
			arguments.AddRange(ZenityFilters(openParams.Filter));

			return arguments;
		}

		internal static List<string> BuildZenityArguments(SaveFileDialogParams saveParams)
		{
			var arguments = new List<string> { "--file-selection", "--save" };

			// Deprecated as of zenity 4 (where it warns and does nothing) but still what makes zenity 3
			// confirm before clobbering a file, and zenity 3 is what most current LTS distributions ship.
			arguments.Add("--confirm-overwrite");

			AddZenityTitle(arguments, saveParams.Title);
			AddZenityFilename(arguments, SuggestedSavePath(saveParams));
			arguments.AddRange(ZenityFilters(saveParams.Filter));

			return arguments;
		}

		internal static List<string> BuildZenityArguments(SelectFolderDialogParams folderParams)
		{
			var arguments = new List<string> { "--file-selection", "--directory" };

			// Folder params carry a Description where the file ones carry only a Title, and callers fill
			// the Description far more often - it is the one required constructor argument. The mac panel
			// has a message line to put it on; a zenity chooser has only its title bar.
			AddZenityTitle(arguments, string.IsNullOrEmpty(folderParams.Title) ? folderParams.Description : folderParams.Title);
			AddZenityFilename(arguments, DirectoryArgument(folderParams.FolderPath));

			return arguments;
		}

		internal static List<string> BuildKdialogArguments(OpenFileDialogParams openParams)
		{
			var arguments = new List<string>();

			AddKdialogTitle(arguments, openParams.Title);

			if (openParams.MultiSelect)
			{
				arguments.Add("--multiple");
				arguments.Add("--separate-output");
			}

			// kdialog takes the start directory and the filter as positionals after the command, so the
			// command has to be last and the start directory has to be present whenever a filter is.
			arguments.Add("--getopenfilename");
			AddKdialogPositionals(arguments, StartDirectoryArgument(openParams.InitialDirectory), openParams.Filter);

			return arguments;
		}

		internal static List<string> BuildKdialogArguments(SaveFileDialogParams saveParams)
		{
			var arguments = new List<string>();

			AddKdialogTitle(arguments, saveParams.Title);

			// No overwrite flag: kdialog's save chooser confirms on its own.
			arguments.Add("--getsavefilename");
			AddKdialogPositionals(arguments, StartDirectoryArgument(SuggestedSavePath(saveParams)), saveParams.Filter);

			return arguments;
		}

		internal static List<string> BuildKdialogArguments(SelectFolderDialogParams folderParams)
		{
			var arguments = new List<string>();

			AddKdialogTitle(arguments, string.IsNullOrEmpty(folderParams.Title) ? folderParams.Description : folderParams.Title);

			arguments.Add("--getexistingdirectory");

			// Through the same guard as the other two: an unset FolderPath used to append a bare null here,
			// which ProcessStartInfo.ArgumentList rejects with an ArgumentNullException out of a menu click.
			AddKdialogPositionals(arguments, StartDirectoryArgument(DirectoryArgument(folderParams.FolderPath)), filter: null);

			return arguments;
		}

		/// <summary>
		/// Translates a <see cref="FileDialogParams.Filter"/> string into zenity's <c>--file-filter</c>
		/// arguments - one per group, in the <c>NAME | PATTERN PATTERN</c> form zenity parses.
		/// </summary>
		internal static IEnumerable<string> ZenityFilters(string filter)
		{
			foreach (var group in ParseFilter(filter))
			{
				yield return "--file-filter=" + group.Description + " | " + string.Join(" ", group.Patterns);
			}
		}

		/// <summary>
		/// Translates a <see cref="FileDialogParams.Filter"/> string into kdialog's single filter argument.
		/// </summary>
		/// <remarks>
		/// kdialog inverts agg's ordering: it wants <c>PATTERN PATTERN|Description</c>, one group per line,
		/// where agg's string is <c>Description|PATTERN;PATTERN</c>. Returns null for no filter, which is
		/// the signal not to pass a filter positional at all.
		/// </remarks>
		internal static string KdialogFilter(string filter)
		{
			var groups = new List<string>();
			foreach (var group in ParseFilter(filter))
			{
				groups.Add(string.Join(" ", group.Patterns) + "|" + group.Description);
			}

			return groups.Count == 0 ? null : string.Join("\n", groups);
		}

		/// <summary>
		/// Splits agg's <c>"Meshes|*.stl;*.amf|All Files|*.*"</c> filter into description/pattern groups.
		/// </summary>
		/// <remarks>
		/// The format is positional pairs with no escaping, so a trailing unpaired element (a description
		/// with no patterns) is simply dropped rather than treated as an error - the alternative is
		/// throwing out of a menu click over a typo in a filter string.
		/// </remarks>
		private static IEnumerable<(string Description, string[] Patterns)> ParseFilter(string filter)
		{
			if (string.IsNullOrWhiteSpace(filter))
			{
				yield break;
			}

			string[] fields = filter.Split('|');

			// Step by two and stop before an unpaired tail.
			for (int i = 0; i + 1 < fields.Length; i += 2)
			{
				string description = fields[i].Trim();
				var patterns = new List<string>();

				foreach (string pattern in fields[i + 1].Split(';'))
				{
					string trimmed = pattern.Trim();
					if (trimmed.Length > 0)
					{
						patterns.Add(trimmed);
					}
				}

				if (patterns.Count > 0)
				{
					yield return (description, patterns.ToArray());
				}
			}
		}

		private static void AddZenityTitle(List<string> arguments, string title)
		{
			if (!string.IsNullOrEmpty(title))
			{
				arguments.Add("--title=" + title);
			}
		}

		private static void AddZenityFilename(List<string> arguments, string path)
		{
			if (!string.IsNullOrEmpty(path))
			{
				arguments.Add("--filename=" + path);
			}
		}

		private static void AddKdialogTitle(List<string> arguments, string title)
		{
			if (!string.IsNullOrEmpty(title))
			{
				arguments.Add("--title");
				arguments.Add(title);
			}
		}

		private static void AddKdialogPositionals(List<string> arguments, string startDirectory, string filter)
		{
			string kdialogFilter = KdialogFilter(filter);

			if (string.IsNullOrEmpty(startDirectory) && kdialogFilter == null)
			{
				return;
			}

			// Everything after "--" is a positional, whatever it starts with. Without it a start directory
			// beginning with "-" - which a directory legitimately may - is read as an unknown option and
			// kdialog exits instead of opening.
			arguments.Add("--");

			// The filter is the second positional, so it needs a start directory ahead of it even when the
			// caller gave none. "." is kdialog's own default.
			arguments.Add(string.IsNullOrEmpty(startDirectory) ? "." : startDirectory);

			if (kdialogFilter != null)
			{
				arguments.Add(kdialogFilter);
			}
		}

		/// <summary>
		/// Normalizes a directory into the trailing-slash form both helpers read as "start here" rather
		/// than "preselect a file with this name".
		/// </summary>
		private static string DirectoryArgument(string directory)
		{
			if (string.IsNullOrEmpty(directory))
			{
				return null;
			}

			return directory.EndsWith("/", StringComparison.Ordinal) ? directory : directory + "/";
		}

		/// <summary>
		/// The path a save dialog should open on: the caller's directory with the caller's suggested file
		/// name inside it, either half optional.
		/// </summary>
		private static string SuggestedSavePath(SaveFileDialogParams saveParams)
		{
			string directory = DirectoryArgument(saveParams.InitialDirectory);
			string fileName = string.IsNullOrEmpty(saveParams.FileName) ? null : Path.GetFileName(saveParams.FileName);

			if (fileName == null)
			{
				return directory;
			}

			return directory == null ? fileName : directory + fileName;
		}

		/// <summary>
		/// kdialog's start-directory positional wants a path, not the trailing-slash directory form zenity
		/// wants, and it is happy with a file path (it starts in that file's folder).
		/// </summary>
		private static string StartDirectoryArgument(string path)
		{
			if (string.IsNullOrEmpty(path) || path == "/")
			{
				return path;
			}

			return path.TrimEnd('/');
		}

		private static DialogTool ProbeForTool()
		{
			if (FindOnPath("zenity") != null)
			{
				return DialogTool.Zenity;
			}

			if (FindOnPath("kdialog") != null)
			{
				return DialogTool.KDialog;
			}

			return DialogTool.None;
		}

		/// <summary>
		/// Finds <paramref name="executable"/> on PATH, or null. This is what <c>which</c> does, without
		/// spawning a shell to ask.
		/// </summary>
		/// <param name="executable">The bare command name to look for.</param>
		/// <param name="searchPath">
		/// The colon-separated list to search, defaulting to the process's own PATH. Passed explicitly only
		/// by the tests, which would otherwise have to mutate PATH for the whole process to cover it.
		/// </param>
		internal static string FindOnPath(string executable, string searchPath = null)
		{
			searchPath ??= Environment.GetEnvironmentVariable("PATH");
			if (string.IsNullOrEmpty(searchPath))
			{
				return null;
			}

			foreach (string directory in searchPath.Split(Path.PathSeparator))
			{
				if (string.IsNullOrEmpty(directory))
				{
					continue;
				}

				try
				{
					string candidate = Path.Combine(directory, executable);

					// Existence is not enough. A same-named data file, or a script whose execute bit was
					// lost to an unzip, would be "found" here and then fail at Process.Start - which is a
					// launch failure reported to the user rather than the quiet fallback to the other
					// helper that the situation actually calls for.
					if (File.Exists(candidate) && IsExecutable(candidate))
					{
						return candidate;
					}
				}
				catch (ArgumentException)
				{
					// A PATH entry with invalid path characters in it. Skip and keep looking.
				}
			}

			return null;
		}

		/// <summary>Whether any of the three execute bits is set on <paramref name="path"/>.</summary>
		private static bool IsExecutable(string path)
		{
			try
			{
				UnixFileMode mode = File.GetUnixFileMode(path);

				return (mode & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) != 0;
			}
			catch (Exception)
			{
				// Unreadable, gone between the two calls, or a host with no Unix modes at all (this
				// assembly targets plain net10.0 and compiles anywhere). None of those is an executable.
				return false;
			}
		}

		/// <summary>
		/// Runs a helper and reports whether it succeeded. Used for the D-Bus reveal, whose failure is the
		/// signal to fall back.
		/// </summary>
		private static bool TryRunToCompletion(ProcessStartInfo startInfo)
		{
			try
			{
				startInfo.RedirectStandardOutput = true;
				startInfo.RedirectStandardError = true;

				using (var process = Process.Start(startInfo))
				{
					if (process == null)
					{
						return false;
					}

					// Start draining both pipes before waiting. --print-reply makes dbus-send write the
					// reply to stdout, and a wait on a process whose output nobody is reading deadlocks the
					// moment that output fills the pipe buffer. These are deliberately not awaited: this
					// call is synchronous by design (see below) and the reads only need to be *running*.
					_ = process.StandardOutput.ReadToEndAsync();
					_ = process.StandardError.ReadToEndAsync();

					// This can run on the UI thread from a context menu, so a wedged bus must not hang the
					// app. Two seconds is far more than a local method call needs.
					if (!process.WaitForExit(2000))
					{
						process.Kill();
						return false;
					}

					return process.ExitCode == 0;
				}
			}
			catch (Exception)
			{
				// dbus-send missing entirely, or no session bus. Both mean "fall back".
				return false;
			}
		}

		/// <summary>
		/// Fire-and-forget launch. A missing helper must not take the application down over a menu item.
		/// </summary>
		private static void TryStart(ProcessStartInfo startInfo)
		{
			try
			{
				// Disposed immediately: the handle is all that is being released, the child keeps running,
				// and holding one Process object per reveal leaks a file descriptor for the app's lifetime.
				using (Process.Start(startInfo))
				{
				}
			}
			catch (Exception exception)
			{
				Debug.WriteLine($"Failed to start {startInfo.FileName}: {exception.Message}");
			}
		}
	}
}
