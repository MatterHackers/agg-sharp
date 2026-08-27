/*
Copyright (c) 2026, Lars Brubaker, John Lewin
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
using System.Reflection;

namespace MatterHackers.Agg.Platform
{
	/// <summary>
	/// The disk-backed <see cref="IStaticData"/>: every path is resolved against <see cref="RootPath"/>
	/// and read straight from the filesystem.
	/// </summary>
	public class StaticData : StaticDataBase
	{
		private StaticData()
		{
			string appPathAndFile = Assembly.GetExecutingAssembly().Location;
			string pathToAppFolder = Path.GetDirectoryName(appPathAndFile);

			if (string.IsNullOrEmpty(RootPath))
			{
				RootPath = Path.Combine(pathToAppFolder, "StaticData");
			}
		}

		// Guards singleton creation and RootPath so the constructor's default RootPath write
		// cannot race another thread's read or write of RootPath.
		private static readonly object instanceLocker = new object();

		private static IStaticData _instance = null;

		private static string rootPath;

		/// <summary>
		/// Gets or sets the process-wide asset provider. Defaults to the disk-backed implementation;
		/// a host with no filesystem (WASM, for one) assigns its own provider during startup, before
		/// anything reads an asset. Assigning after the default has been created is allowed but the
		/// icons already handed out will have come from disk.
		/// </summary>
		public static IStaticData Instance
		{
			get
			{
				lock (instanceLocker)
				{
					if (_instance == null)
					{
						_instance = new StaticData();
					}

					return _instance;
				}
			}

			set
			{
				// Refuse null loudly. Clearing the instance would silently fall back to lazily building
				// the disk-backed provider, so a host with no filesystem would not fail here - it would
				// fail much later with "Bad icon load", nowhere near the assignment that caused it.
				if (value == null)
				{
					throw new ArgumentNullException(nameof(value), "StaticData.Instance cannot be set to null.");
				}

				lock (instanceLocker)
				{
					_instance = value;
				}
			}
		}

		public static string RootPath
		{
			get
			{
				lock (instanceLocker)
				{
					return rootPath;
				}
			}

			set
			{
				lock (instanceLocker)
				{
					rootPath = value;
				}
			}
		}

		public static void OverrideRootPath(string overridePath)
		{
			Console.WriteLine("   Overriding StaticData: " + Path.GetFullPath(overridePath));
			RootPath = overridePath;
		}

		public override bool DirectoryExists(string path)
		{
			return Directory.Exists(MapPath(path));
		}

		public override bool FileExists(string path)
		{
			return File.Exists(MapPath(path));
		}

		/// <summary>
		/// Gets the sub-directories of the given directory as full paths, which map back to themselves.
		/// </summary>
		/// <remarks>
		/// Order is whatever the filesystem hands back - see <see cref="IStaticData.GetDirectories"/>.
		/// </remarks>
		public override IEnumerable<string> GetDirectories(string path)
		{
			return Directory.GetDirectories(MapPath(path));
		}

		/// <summary>
		/// Gets the files of the given directory as paths relative to <see cref="RootPath"/>, so that
		/// they can be passed straight back to the other members (which all map through RootPath).
		/// </summary>
		/// <remarks>
		/// This used to chop the string at the first literal "StaticData" it found, which produced
		/// garbage whenever "StaticData" appeared earlier in the root (a user named StaticData, a
		/// build folder, a temp root) and whenever the root folder was not called "StaticData" at all.
		/// </remarks>
		public override IEnumerable<string> GetFiles(string path)
		{
			// Map "" rather than using RootPath directly - RootPath may be relative, and the paths
			// coming back from Directory.GetFiles are always full.
			var fullRoot = MapPath("");
			return Directory.GetFiles(MapPath(path)).Select(p => Path.GetRelativePath(fullRoot, p));
		}

		public override Stream OpenStream(string path)
		{
			return File.OpenRead(MapPath(path));
		}

		public override string[] ReadAllLines(string path)
		{
			return File.ReadLines(MapPath(path)).ToArray();
		}

		public override string ReadAllText(string path)
		{
			var allText = File.ReadAllText(MapPath(path));
			return allText;
		}

		public override string MapPath(string path)
		{
			return Path.GetFullPath(Path.Combine(RootPath, path));
		}
	}
}
