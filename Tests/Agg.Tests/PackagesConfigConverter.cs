
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using NUnit.Framework;

namespace MatterHackers.Agg.Tests
{
	// [TestFixture]
	public class PackagesConfigConverter
	{
		private static XNamespace ns = "http://schemas.microsoft.com/developer/msbuild/2003";

		private static Regex quotedStrings = new Regex("[^\"]*");

		private static string solutionFilePath = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "agg-sharp.sln");

		// [Test]
		public void ConvertWinExeProjectsToPackageReference()
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
						new XComment("See the following for details on netstandard2 binding workround: https://github.com/dotnet/standard/issues/481"),
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