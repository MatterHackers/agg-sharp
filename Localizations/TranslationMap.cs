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
		private const string englishTag = "English:";
		private const string translatedTag = "Translated:";

		private Dictionary<string, string> translationDictionary = new Dictionary<string, string>();

		private string twoLetterIsoLanguageName;

		public static TranslationMap ActiveTranslationMap { get; set; }

		public TranslationMap(string pathToTranslationsFolder, string twoLetterIsoLanguageName = "")
		{
			// Select either the user supplied language name or the current thread language name
			this.twoLetterIsoLanguageName = string.IsNullOrEmpty(twoLetterIsoLanguageName) ?
				Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName.ToLower():
				twoLetterIsoLanguageName.ToLower();

			string translationFilePath = Path.Combine(pathToTranslationsFolder, this.twoLetterIsoLanguageName, "Translation.txt");

			// In English no translation file exists and no dictionary will be initialized or loaded
			if (AggContext.StaticData.FileExists(translationFilePath))
			{
				translationDictionary = ReadIntoDictionary(translationFilePath);
			}
		}

		public virtual string Translate(string englishString)
		{
			// Skip dictionary lookups for English
			if (twoLetterIsoLanguageName == "en"
				|| englishString == null)
			{
				return englishString;
			}

			// Perform the lookup to the translation table
			if (!translationDictionary.TryGetValue(englishString, out string translatedString))
			{
				// Use English string if no mapping found
				return englishString;
			}

			return translatedString;
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
					if (line.Length < englishTag.Length || !line.StartsWith(englishTag))
					{
						throw new Exception("Found unknown string at line {0}. Looking for {1}.".FormatWith(i, englishTag));
					}
					else
					{
						englishString = lines[i].Substring(englishTag.Length);
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
}
