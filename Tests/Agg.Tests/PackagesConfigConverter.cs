/*
Copyright (c) 2025, Lars Brubaker
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
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
namespace MatterHackers.Agg.Tests
{
	// Utility class for converting packages.config to PackageReference format
	public class PackagesConfigConverter
	{
		private static XNamespace ns = "http://schemas.microsoft.com/developer/msbuild/2003";

		private static Regex quotedStrings = new Regex("[^\"]*");

		private static string solutionFilePath
		{
			get
			{
				throw new NotImplementedException();
				//Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "agg-sharp.sln");
			}
		}

		// [Test]
		public async Task ConvertWinExeProjectsToPackageReference()
		{
			foreach(var csproj in allCSProjFiles)
			{
				var outputType = csproj.Root.Descendants(ns + "OutputType");
				if (outputType.FirstOrDefault() is XElement elem)
				{
					bool shouldPerformUpgrade = false;

					if (false)
					{
						// Upgrade Exe projects
						shouldPerformUpgrade = elem.Value.IndexOf("exe", StringComparison.OrdinalIgnoreCase) >= 0;
					}
					else
					{
						// Upgrade test projects
						shouldPerformUpgrade = csproj.Name.IndexOf("test", StringComparison.OrdinalIgnoreCase) >= 0;
					}

					if (!shouldPerformUpgrade)
					{
						continue;
					}

					string projectDirectory = Path.GetDirectoryName(csproj.FilePath);

					var xml = csproj.Root.ToString();

					// Add required config for PackageReference/automatic binding redirects/bind redirects for tests
					elem.AddAfterSelf(
						new XComment("See the following for details on netstandard2 binding workaround: https://github.com/dotnet/standard/issues/481"),
						new XElement(ns + "AutoGenerateBindingRedirects",
							new XText("true")),
						new XElement(ns + "RestoreProjectStyle",
							new XText("PackageReference")),
						new XElement(ns + "GenerateBindingRedirectsOutputType",
							new XText("true")));

					var references = csproj.Root.Descendants(ns + "Reference");

					if (references.FirstOrDefault() is XElement firstReference)
					{
						var parent = firstReference.Parent;
						references.Remove();

						string packagesConfig = Path.Combine(projectDirectory, "packages.config");
						if (File.Exists(packagesConfig))
						{
							var root = XElement.Load(packagesConfig);

							parent.Add(
								root.Descendants("package").Select(p =>
								{
									return new XElement(
										ns + "PackageReference",
										new XAttribute("Include", (string)p.Attribute("id")),
										new XAttribute("Version", (string)p.Attribute("version")));
								}));

							File.Delete(packagesConfig);
						}

						csproj.Root.Descendants(ns + "None").Where(e => (string)e.Attribute("Include") == "packages.config").Remove();
					}

					if (xml != csproj.Root.ToString())
					{
						csproj.Root.Save(csproj.FilePath);
					}
				}
			}
		}

		private static List<CSProjFile> allCSProjFiles = GetProjectPathsFromSolutionFile(solutionFilePath).Select(f => new CSProjFile(f)).ToList();
		private static List<string> GetProjectPathsFromSolutionFile(string solutionFile)
		{
			var processed = new List<string>();
			foreach (var line in File.ReadAllLines(solutionFile))
			{
				if (line.StartsWith("Project("))
				{
					var matches = quotedStrings.Matches(line);

					//var path = Path.Combine(Path.GetDirectoryName(solutionFile), matches[6].Value);
					var path = Path.GetDirectoryName(solutionFile);

					path = Path.Combine(path, matches[10].Value);

					if (File.Exists(path))
					{
						processed.Add(path);
					}
					else
					{
						Console.WriteLine("    Skipping: {0}", path);
					}
				}
			}

			return processed;
		}

		/// <summary>
		/// Wrapper class for csproj files that loads them with XElement and exposes filtered PropertyGroup elements for release builds
		/// </summary>
		public class CSProjFile
		{
			private static XNamespace ns = "http://schemas.microsoft.com/developer/msbuild/2003";

			public CSProjFile(string filePath)
			{
				this.Root = XElement.Load(filePath);
				this.FilePath = filePath;
				this.Name = Path.GetFileName(filePath);

				this.PropertyGroups = Root.Elements(ns + "PropertyGroup");

				this.AllConfigs = from elem in this.PropertyGroups
								  let conditionText = (string)elem.Attribute("Condition")
								  where conditionText != null && conditionText.Contains("(Configuration)|$(Platform)")
								  orderby conditionText.Length descending
								  select elem;

				// Find PropertyGroup elements having 'Release' or 'Release|AnyCPU' and select the first, ordered by length to grab 'Release|AnyCPU' or fall back to 'Release'
				this.ReleaseConfigs = from elem in this.PropertyGroups
									  let conditionText = (string)elem.Attribute("Condition")
									  where conditionText != null && conditionText.Contains("Release|AnyCPU")
									  orderby conditionText.Length descending
									  select elem;

				this.RootPropertyGroup = this.PropertyGroups.Where(e => e.Attribute("Condition") == null).FirstOrDefault();
			}

			public string FilePath { get; }
			public IEnumerable<XElement> ReleaseConfigs { get; }
			public IEnumerable<XElement> PropertyGroups { get; }
			public IEnumerable<XElement> AllConfigs { get; }
			public XElement RootPropertyGroup { get; }
			public XElement Root { get; }
			public string Name { get; internal set; }
		}
	}
}
