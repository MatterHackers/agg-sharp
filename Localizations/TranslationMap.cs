/*
Copyright (c) 2018, Lars Brubaker, John Lewin
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
using System.Threading;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;

namespace MatterHackers.Localizations
{
	[DebuggerStepThrough]
	public static class TranslationMapExtensions
	{
		public static string Localize(this string englishString)
		{
			if (TranslationMap.ActiveTranslationMap != null)
			{
				return TranslationMap.ActiveTranslationMap.Translate(englishString);
			}

			return englishString;
		}
	}

	[DebuggerStepThrough]
	public class TranslationMap
	{
		protected const string engishTag = "English:";
		protected const string translatedTag = "Translated:";

		protected Dictionary<string, string> translationDictionary = new Dictionary<string, string>();

		public string TwoLetterIsoLanguageName { get; private set; }

		public static TranslationMap ActiveTranslationMap { get; set; }

		public TranslationMap(string pathToTranslationsFolder, string twoLetterIsoLanguageName = "")
		{
			// Select either the user supplied language name or the current thread language name
			this.TwoLetterIsoLanguageName = string.IsNullOrEmpty(twoLetterIsoLanguageName) ?
				Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName.ToLower():
				twoLetterIsoLanguageName.ToLower();

			string translationFilePath = Path.Combine(pathToTranslationsFolder, TwoLetterIsoLanguageName, "Translation.txt");

			// In English no translation file exists and no dictionary will be initialized or loaded
			if (AggContext.StaticData.FileExists(translationFilePath))
			{
				translationDictionary = ReadIntoDictionary(translationFilePath);
			}
		}

		public virtual string Translate(string englishString)
		{
			// Skip dictionary lookups for English
			if (TwoLetterIsoLanguageName == "en"
				|| englishString == null)
			{
				return englishString;
			}

			// Perform the lookup to the translation table
			string tranlatedString;
			if (!translationDictionary.TryGetValue(englishString, out tranlatedString))
			{
				return englishString;
			}

			return tranlatedString;
		}

		public static void AssertDebugNotDefined()
		{
#if DEBUG
			throw new Exception("DEBUG is defined and should not be!");
#endif
		}

		protected Dictionary<string, string> ReadIntoDictionary(string pathAndFilename)
		{
			var dictionary = new Dictionary<string, string>();

			string[] lines = AggContext.StaticData.ReadAllLines(pathAndFilename);
			bool lookingForEnglish = true;
			string englishString = "";
			for (int i = 0; i < lines.Length; i++)
			{
				string line = lines[i].Trim();
				if (line.Length == 0)
				{
					// we are happy to skip blank lines
					continue;
				}

				if (lookingForEnglish)
				{
					if (line.Length < engishTag.Length || !line.StartsWith(engishTag))
					{
						throw new Exception("Found unknown string at line {0}. Looking for {1}.".FormatWith(i, engishTag));
					}
					else
					{
						englishString = lines[i].Substring(engishTag.Length);
						lookingForEnglish = false;
					}
				}
				else
				{
					if (line.Length < translatedTag.Length || !line.StartsWith(translatedTag))
					{
						throw new Exception("Found unknown string at line {0}. Looking for {1}.".FormatWith(i, translatedTag));
					}
					else
					{
						string translatedString = lines[i].Substring(translatedTag.Length);
						// store the string
						if (!dictionary.ContainsKey(DecodeWhileReading(englishString)))
						{
							dictionary.Add(DecodeWhileReading(englishString), DecodeWhileReading(translatedString));
						}
						// go back to looking for English
						lookingForEnglish = true;
					}
				}
			}

			return dictionary;
		}

		/// <summary>
		/// Decodes while reading, unescaping newlines
		/// </summary>
		private string DecodeWhileReading(string stringToDecode)
		{
			return stringToDecode.Replace("\\n", "\n");
		}
	}

#if DEBUG
	/// <summary>
	/// An auto generating translation map that dumps missing localization strings to master.txt in debug builds
	/// </summary>
	/// <seealso cref="MatterHackers.Localizations.TranslationMap" />
	public class AutoGeneratingTranslationMap : TranslationMap
	{
		private static object locker = new object();
		private string masterFilePath;

		public AutoGeneratingTranslationMap(string pathToTranslationsFolder, string twoLetterIsoLanguageName = "") : base(pathToTranslationsFolder, twoLetterIsoLanguageName)
		{
			masterFilePath = Path.Combine(pathToTranslationsFolder, "Master.txt");

			// Override the default logic and load master.txt in English debug builds
			if (this.TwoLetterIsoLanguageName == "en")
			{
				translationDictionary = ReadIntoDictionary(masterFilePath);
			}
		}

		public override string Translate(string englishString)
		{
			if (string.IsNullOrEmpty(englishString))
			{
				return englishString;
			}

			string tranlatedString;
			if (!translationDictionary.TryGetValue(englishString, out tranlatedString))
			{
				if (TwoLetterIsoLanguageName == "en")
				{
					AddNewString(englishString);
				}
				return englishString;
			}

			return tranlatedString;
		}

		/// <summary>
		/// Encodes for saving, escaping newlines
		/// </summary>
		private string EncodeForSaving(string stringToEncode)
		{
			return stringToEncode.Replace("\n", "\\n");
		}

		private void AddNewString(string englishString)
		{
			// We only ship release and this could cause a write to the ProgramFiles directory which is not allowed.
			// So we only write translation text while in debug (another solution in the future could be implemented). LBB
			if (AggContext.OperatingSystem == OSType.Windows)
			{
				// TODO: make sure we don't throw an assertion when running from the ProgramFiles directory.
				// Don't do saving when we are.
				if (!translationDictionary.ContainsKey(englishString))
				{
					translationDictionary.Add(englishString, englishString);

					lock (locker)
					{
						string pathName = Path.GetDirectoryName(masterFilePath);
						if (!Directory.Exists(pathName))
						{
							Directory.CreateDirectory(pathName);
						}

						using (StreamWriter masterFileStream = File.AppendText(masterFilePath))
						{
							masterFileStream.WriteLine("{0}{1}", engishTag, EncodeForSaving(englishString));
							masterFileStream.WriteLine("{0}{1}", translatedTag, EncodeForSaving(englishString));
							masterFileStream.WriteLine("");
						}
					}
				}
			}
		}
	}
#endif
}
