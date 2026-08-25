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

using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg.Platform;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// The parts of <see cref="LinuxFileDialogProvider"/> that can be checked without a desktop: the
	/// translation of agg's dialog params into a zenity or kdialog command line, the reading of what the
	/// helper prints back, and the failure-versus-cancel decision. Spawning the helper itself is a smoke
	/// test, not a unit test - these cover everything that decides <i>what</i> gets spawned and how its
	/// answer is read.
	/// </summary>
	public class LinuxFileDialogProviderTests
	{
		[Test]
		public async Task ASingleFilterGroupBecomesOneFilterArgumentPerHelper()
		{
			await Assert.That(LinuxFileDialogProvider.ZenityFilters("Meshes|*.stl;*.amf").ToArray())
				.IsEquivalentTo(new[] { "--file-filter=Meshes | *.stl *.amf" });

			// kdialog inverts the pair: patterns first, then the description.
			await Assert.That(LinuxFileDialogProvider.KdialogFilter("Meshes|*.stl;*.amf"))
				.IsEqualTo("*.stl *.amf|Meshes");
		}

		[Test]
		public async Task EveryGroupInTheFilterSurvivesTheTranslation()
		{
			await Assert.That(LinuxFileDialogProvider.ZenityFilters("Meshes|*.stl;*.amf|All Files|*.*").ToArray())
				.IsEquivalentTo(new[] { "--file-filter=Meshes | *.stl *.amf", "--file-filter=All Files | *.*" });

			// One argument, one line per group - kdialog takes the whole set as a single positional.
			await Assert.That(LinuxFileDialogProvider.KdialogFilter("Meshes|*.stl;*.amf|All Files|*.*"))
				.IsEqualTo("*.stl *.amf|Meshes\n*.*|All Files");
		}

		/// <summary>
		/// No filter has to mean no flag rather than an empty one: zenity treats an empty
		/// <c>--file-filter</c> as a filter matching nothing, which hides every file in the chooser.
		/// </summary>
		[Test]
		public async Task NoFilterProducesNoFilterArgument()
		{
			await Assert.That(LinuxFileDialogProvider.ZenityFilters(null).ToArray()).IsEmpty();
			await Assert.That(LinuxFileDialogProvider.ZenityFilters(string.Empty).ToArray()).IsEmpty();
			await Assert.That(LinuxFileDialogProvider.KdialogFilter(null)).IsNull();
			await Assert.That(LinuxFileDialogProvider.KdialogFilter(string.Empty)).IsNull();
		}

		/// <summary>
		/// The filter format is positional pairs with no escaping, so a description with no patterns after
		/// it is a typo in the caller's string. Dropping the tail keeps the groups that did parse instead
		/// of throwing out of a menu click.
		/// </summary>
		[Test]
		public async Task AnUnpairedTailIsDroppedRatherThanThrowing()
		{
			await Assert.That(LinuxFileDialogProvider.ZenityFilters("Meshes|*.stl|All Files").ToArray())
				.IsEquivalentTo(new[] { "--file-filter=Meshes | *.stl" });

			await Assert.That(LinuxFileDialogProvider.KdialogFilter("Meshes|*.stl|All Files"))
				.IsEqualTo("*.stl|Meshes");

			// A lone description is a group with nothing in it, and yields nothing at all.
			await Assert.That(LinuxFileDialogProvider.ZenityFilters("All Files").ToArray()).IsEmpty();
			await Assert.That(LinuxFileDialogProvider.KdialogFilter("All Files")).IsNull();
		}

		[Test]
		public async Task AnOpenDialogCarriesItsTitleStartDirectoryAndFilters()
		{
			var openParams = new OpenFileDialogParams("Meshes|*.stl", initialDirectory: "/home/user/parts", title: "Pick a part");

			await Assert.That(LinuxFileDialogProvider.BuildZenityArguments(openParams))
				.IsEquivalentTo(new[]
				{
					"--file-selection",
					"--title=Pick a part",
					// Trailing slash: without it zenity reads the path as a file to preselect and opens on
					// the parent directory instead.
					"--filename=/home/user/parts/",
					"--file-filter=Meshes | *.stl",
				});
		}

		/// <summary>
		/// The separator has to be set explicitly for multi-select. Every candidate is legal in a Unix
		/// filename - only <c>/</c> and NUL are not - so this is a choice of the least bad one rather than
		/// a safe one, and a newline beats zenity's default pipe and kdialog's default space by a wide
		/// margin in practice.
		/// </summary>
		[Test]
		public async Task MultiSelectAsksForOnePathPerLine()
		{
			var openParams = new OpenFileDialogParams("Meshes|*.stl", multiSelect: true);

			await Assert.That(LinuxFileDialogProvider.BuildZenityArguments(openParams))
				.IsEquivalentTo(new[]
				{
					"--file-selection",
					"--multiple",
					"--separator=\n",
					"--file-filter=Meshes | *.stl",
				});

			await Assert.That(LinuxFileDialogProvider.BuildKdialogArguments(openParams))
				.IsEquivalentTo(new[]
				{
					"--multiple",
					"--separate-output",
					"--getopenfilename",
					"--",
					".",
					"*.stl|Meshes",
				});
		}

		[Test]
		public async Task ASaveDialogOpensOnTheSuggestedNameInsideTheStartDirectory()
		{
			var saveParams = new SaveFileDialogParams("Meshes|*.stl", initialDirectory: "/home/user/parts", title: "Save part")
			{
				FileName = "bracket.stl",
			};

			await Assert.That(LinuxFileDialogProvider.BuildZenityArguments(saveParams))
				.IsEquivalentTo(new[]
				{
					"--file-selection",
					"--save",
					"--confirm-overwrite",
					"--title=Save part",
					"--filename=/home/user/parts/bracket.stl",
					"--file-filter=Meshes | *.stl",
				});

			await Assert.That(LinuxFileDialogProvider.BuildKdialogArguments(saveParams))
				.IsEquivalentTo(new[]
				{
					"--title",
					"Save part",
					"--getsavefilename",
					"--",
					"/home/user/parts/bracket.stl",
					"*.stl|Meshes",
				});
		}

		/// <summary>
		/// A caller that passes a full path as the suggested name (which several do, reusing the path they
		/// last saved to) must not end up with the directory named twice.
		/// </summary>
		[Test]
		public async Task ASuggestedNameIsReducedToItsFileNameBeforeBeingJoined()
		{
			var saveParams = new SaveFileDialogParams("Meshes|*.stl", initialDirectory: "/home/user/parts")
			{
				FileName = "/somewhere/else/bracket.stl",
			};

			await Assert.That(LinuxFileDialogProvider.BuildZenityArguments(saveParams))
				.Contains("--filename=/home/user/parts/bracket.stl");
		}

		/// <summary>
		/// Folder params carry a Description and usually no Title, and a zenity chooser has only a title
		/// bar to put text on - so the Description is what the user reads.
		/// </summary>
		[Test]
		public async Task AFolderDialogFallsBackToItsDescriptionForATitle()
		{
			var folderParams = new SelectFolderDialogParams("Choose an output folder")
			{
				FolderPath = "/home/user/exports",
			};

			await Assert.That(LinuxFileDialogProvider.BuildZenityArguments(folderParams))
				.IsEquivalentTo(new[]
				{
					"--file-selection",
					"--directory",
					"--title=Choose an output folder",
					"--filename=/home/user/exports/",
				});

			await Assert.That(LinuxFileDialogProvider.BuildKdialogArguments(folderParams))
				.IsEquivalentTo(new[]
				{
					"--title",
					"Choose an output folder",
					"--getexistingdirectory",
					"--",
					"/home/user/exports",
				});

			// An explicit Title wins over the Description when the caller set one.
			folderParams.Title = "Export to";
			await Assert.That(LinuxFileDialogProvider.BuildZenityArguments(folderParams)).Contains("--title=Export to");
		}

		/// <summary>
		/// FolderPath is optional and frequently unset - <see cref="SelectFolderDialogParams"/>'s
		/// constructor does not take one. An unset one used to append a null argument, which
		/// <c>ProcessStartInfo.ArgumentList</c> rejects with an ArgumentNullException, so every folder
		/// dialog opened without a starting point threw out of the menu click instead of opening.
		/// </summary>
		[Test]
		public async Task AFolderDialogWithNoStartingPointAddsNoPositionalAtAll()
		{
			var folderParams = new SelectFolderDialogParams("Choose an output folder");

			await Assert.That(LinuxFileDialogProvider.BuildKdialogArguments(folderParams))
				.IsEquivalentTo(new[]
				{
					"--title",
					"Choose an output folder",
					"--getexistingdirectory",
				});

			await Assert.That(LinuxFileDialogProvider.BuildKdialogArguments(folderParams).Any(argument => argument == null))
				.IsFalse();

			await Assert.That(LinuxFileDialogProvider.BuildZenityArguments(folderParams))
				.IsEquivalentTo(new[]
				{
					"--file-selection",
					"--directory",
					"--title=Choose an output folder",
				});
		}

		/// <summary>
		/// kdialog takes the start directory and the filter as positionals after the command word, so the
		/// command has to come last and a filter cannot be passed without a directory ahead of it. The
		/// <c>--</c> is what keeps a directory whose name starts with a hyphen from being read as a flag.
		/// </summary>
		[Test]
		public async Task KdialogPositionalsFollowTheCommandBehindADoubleDash()
		{
			var openParams = new OpenFileDialogParams("Meshes|*.stl", initialDirectory: "/home/user/parts", title: "Pick a part");

			await Assert.That(LinuxFileDialogProvider.BuildKdialogArguments(openParams))
				.IsEquivalentTo(new[]
				{
					"--title",
					"Pick a part",
					"--getopenfilename",
					"--",
					"/home/user/parts",
					"*.stl|Meshes",
				});

			// No filter and no directory: the command stands alone rather than gaining a bare "--" and ".".
			await Assert.That(LinuxFileDialogProvider.BuildKdialogArguments(new OpenFileDialogParams(null)))
				.IsEquivalentTo(new[] { "--getopenfilename" });

			// A directory that looks like an option is exactly what the "--" is for.
			var hyphenated = new OpenFileDialogParams(null, initialDirectory: "/home/user/-scratch");
			var arguments = LinuxFileDialogProvider.BuildKdialogArguments(hyphenated);
			await Assert.That(arguments.IndexOf("--")).IsLessThan(arguments.IndexOf("/home/user/-scratch"));
		}

		/// <summary>
		/// Cancel is the exit-1 case, and it has to read as "no paths" rather than as a path list with a
		/// blank in it - the provider treats an empty result as "do not call the callback", which is how
		/// the mac provider behaves on a cancelled panel.
		/// </summary>
		[Test]
		public async Task ANonZeroExitIsACancelAndYieldsNoPaths()
		{
			await Assert.That(LinuxFileDialogProvider.ParseDialogOutput(1, string.Empty, multipleSelection: false)).IsEmpty();

			// Some helpers echo a partial line before being dismissed; the exit code is what decides.
			await Assert.That(LinuxFileDialogProvider.ParseDialogOutput(1, "/etc/hostname\n", multipleSelection: false)).IsEmpty();

			// A clean exit with nothing chosen is equally "no answer".
			await Assert.That(LinuxFileDialogProvider.ParseDialogOutput(0, "\n", multipleSelection: false)).IsEmpty();
			await Assert.That(LinuxFileDialogProvider.ParseDialogOutput(0, "\n", multipleSelection: true)).IsEmpty();
		}

		[Test]
		public async Task AcceptedMultiSelectPathsComeBackOnePerLine()
		{
			await Assert.That(LinuxFileDialogProvider.ParseDialogOutput(0, "/a/one.stl\n/a/two.stl\n", multipleSelection: true))
				.IsEquivalentTo(new[] { "/a/one.stl", "/a/two.stl" });
		}

		/// <summary>
		/// A single selection is never split. A newline is a legal character in a Unix filename, so a file
		/// genuinely named with one comes back as a single path - splitting it unconditionally would turn
		/// that one real file into two paths that do not exist.
		/// </summary>
		[Test]
		public async Task ASingleSelectionIsTakenWholeAndOnlyLosesItsTrailingNewline()
		{
			await Assert.That(LinuxFileDialogProvider.ParseDialogOutput(0, "/etc/hostname\n", multipleSelection: false))
				.IsEquivalentTo(new[] { "/etc/hostname" });

			await Assert.That(LinuxFileDialogProvider.ParseDialogOutput(0, "/a/two\nline.stl\n", multipleSelection: false))
				.IsEquivalentTo(new[] { "/a/two\nline.stl" });

			// Exactly one terminator comes off, so a name ending in a blank line keeps it.
			await Assert.That(LinuxFileDialogProvider.ParseDialogOutput(0, "/a/trailing\n\n", multipleSelection: false))
				.IsEquivalentTo(new[] { "/a/trailing\n" });
		}

		/// <summary>
		/// A trailing space is a legal and unremarkable part of a filename. Trimming it produces a path
		/// that does not exist, and the caller has no way to tell that happened.
		/// </summary>
		[Test]
		public async Task SpacesAreNeverTrimmedOffAPath()
		{
			await Assert.That(LinuxFileDialogProvider.ParseDialogOutput(0, "/a/trailing space \n", multipleSelection: false))
				.IsEquivalentTo(new[] { "/a/trailing space " });

			await Assert.That(LinuxFileDialogProvider.ParseDialogOutput(0, "/a/trailing space \n/a/ leading.stl\n", multipleSelection: true))
				.IsEquivalentTo(new[] { "/a/trailing space ", "/a/ leading.stl" });

			// A CRLF helper still gets its carriage return removed - that is not part of the name.
			await Assert.That(LinuxFileDialogProvider.ParseDialogOutput(0, "/a/one.stl\r\n/a/two.stl\r\n", multipleSelection: true))
				.IsEquivalentTo(new[] { "/a/one.stl", "/a/two.stl" });

			await Assert.That(LinuxFileDialogProvider.ParseDialogOutput(0, "/a/one.stl\r\n", multipleSelection: false))
				.IsEquivalentTo(new[] { "/a/one.stl" });
		}

		/// <summary>
		/// Exit 0 is an answer and exit 1 is a cancel; anything else is a bug in what this provider built,
		/// and has to reach the crash reporter rather than looking like the user changed their mind.
		/// </summary>
		[Test]
		public async Task AnUnexpectedExitCodeIsAFailureAndACancelIsNot()
		{
			await Assert.That(LinuxFileDialogProvider.DescribeFailure("zenity", 0, string.Empty)).IsNull();
			await Assert.That(LinuxFileDialogProvider.DescribeFailure("zenity", 1, string.Empty)).IsNull();

			// zenity's answer to an option it does not understand.
			await Assert.That(LinuxFileDialogProvider.DescribeFailure("zenity", 255, "This option is not available.\n"))
				.IsEqualTo("zenity exited with code 255: This option is not available.");

			// Still a failure when the helper died without explaining itself.
			await Assert.That(LinuxFileDialogProvider.DescribeFailure("kdialog", 139, string.Empty))
				.IsEqualTo("kdialog exited with code 139.");
		}

		/// <summary>
		/// The hard case. GTK writes a paragraph of warnings to stderr during a perfectly ordinary cancel,
		/// and Program.cs feeds this channel straight to the crash reporter - so treating any stderr output
		/// as evidence of failure would file a crash report every time a user backs out of an Open dialog.
		/// Only text that is not a GLib-formatted diagnostic counts.
		/// </summary>
		[Test]
		public async Task GlibWarningsOnACancelAreNotMistakenForAFailure()
		{
			// Captured verbatim from a cancelled zenity 4.0.1 chooser under Xvfb, plus the two domain-less
			// forms GLib uses when a message carries no log domain. Those lead with "** " and have no
			// hyphen before WARNING/Message, so they match none of the domain-ed patterns - and the
			// accessibility-bus one appears on every desktop without at-spi running, which is most
			// headless and minimal sessions.
			string noise =
				"libEGL warning: DRI3 error: Could not get DRI3 device\n"
				+ "\n"
				+ "(zenity:10958): Gtk-WARNING **: 17:06:13.661: Unable to acquire session bus: Failed to execute child process\n"
				+ "Gtk-Message: 17:06:14.828: GtkDialog mapped without a transient parent. This is discouraged.\n"
				+ "\n"
				+ "** (zenity:10958): WARNING **: 17:06:14.850: Couldn't connect to accessibility bus: Failed to connect to socket\n"
				+ "** Message: 17:06:14.851: Failed to load module \"canberra-gtk-module\"\n"
				+ "\n"
				+ "(zenity:10958): dconf-WARNING **: 17:06:14.849: failed to commit changes to dconf\n";

			await Assert.That(LinuxFileDialogProvider.DescribeFailure("zenity", 1, noise)).IsNull();

			// The same noise around a real complaint still surfaces the complaint - the filter must not
			// have become a blanket "exit 1 is always a cancel".
			await Assert.That(LinuxFileDialogProvider.DescribeFailure("zenity", 1, noise + "This option is not available.\n"))
				.IsEqualTo("zenity exited with code 1: This option is not available.");

			// And a complaint buried in the middle of the noise, not just after it.
			string buried =
				"** (zenity:10958): WARNING **: 17:06:14.850: Couldn't connect to accessibility bus\n"
				+ "This option is not available.\n"
				+ "** Message: 17:06:14.851: Failed to load module \"canberra-gtk-module\"\n";

			await Assert.That(LinuxFileDialogProvider.DescribeFailure("zenity", 255, buried))
				.IsEqualTo("zenity exited with code 255: This option is not available.");
		}

		/// <summary>
		/// The reveal argument is a copy of MatterCAD's LinuxShellIntegration.BuildShowItemsArgument - this
		/// assembly sits underneath that one and cannot reference it. The comma is the case worth guarding:
		/// dbus-send splits an array: value on commas, so an unencoded one silently turns one real path
		/// into two paths that do not exist.
		/// </summary>
		[Test]
		public async Task TheRevealArgumentEncodesCommasAsWellAsTheUri()
		{
			await Assert.That(LinuxFileDialogProvider.BuildShowItemsArgument("/home/user/a,b.stl"))
				.IsEqualTo("array:string:file:///home/user/a%2Cb.stl");

			await Assert.That(LinuxFileDialogProvider.BuildShowItemsArgument("/home/user/my parts/bracket.stl"))
				.IsEqualTo("array:string:file:///home/user/my%20parts/bracket.stl");
		}

		/// <summary>
		/// The PATH probe is what decides whether dialogs work at all, so it has to agree with what the
		/// shell would resolve - which means the execute bit and not merely the name.
		/// </summary>
		[Test]
		public async Task ThePathProbeFindsExecutablesAndSkipsNonExecutableMatches()
		{
			await Assert.That(LinuxFileDialogProvider.FindOnPath("sh")).IsNotNull();
			await Assert.That(LinuxFileDialogProvider.FindOnPath("no-such-helper-anywhere-on-path")).IsNull();

			// A readable, non-executable file of the right name is not a helper. /etc/hostname is mode 644
			// and /etc is on nobody's real PATH, so it only reaches the probe because this passes it in -
			// which is why the search path is a parameter rather than PATH being mutated process-wide.
			await Assert.That(LinuxFileDialogProvider.FindOnPath("hostname", "/etc")).IsNull();

			// The same probe over a directory that does hold an executable of that name still finds it.
			await Assert.That(LinuxFileDialogProvider.FindOnPath("sh", "/nonexistent:/bin:/usr/bin")).IsNotNull();
		}
	}
}
