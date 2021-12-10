﻿/*
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

		public static string Stars(this string englishString)
		{
			return "*" + englishString + "*";
		}
	}

	public class TranslationMap
	{
		private const string englishTag = "English:";
		private const string translatedTag = "Translated:";

		private Dictionary<string, string> translationDictionary = new Dictionary<string, string>();

		public static TranslationMap ActiveTranslationMap { get; set; }

		public TranslationMap(StreamReader streamReader, string twoLetterIsoLanguageName)
		{
			translationDictionary = ReadIntoDictionary(streamReader);
		}

		public virtual string Translate(string englishString)
		{
			// Skip dictionary lookups for English
#if DEBUG
			if (englishString == null)
			{
				return englishString;
			}
#else
			if (twoLetterIsoLanguageName == "en"
				|| englishString == null)
			{
				return englishString;
			}
#endif

			// Perform the lookup to the translation table
			if (!translationDictionary.TryGetValue(englishString, out string translatedString))
			{
#if DEBUG
				AddNewString(englishString);
#endif

				// Use English string if no mapping found
				return englishString;
			}

			return translatedString;
		}

		/// <summary>
		/// Encodes for saving, escaping newlines
		/// </summary>
		private string EncodeForSaving(string stringToEncode)
		{
			return stringToEncode.Replace("\n", "\\n");
		}

		private object locker = new object();

		private void AddNewString(string englishString)
		{
			lock (locker)
			{
				if (!translationDictionary.ContainsKey(englishString))
				{
					translationDictionary.Add(englishString, englishString);

					string mastFilePath = "C:\\" + Path.Combine("Development", "MCCentral", "MatterControl", "StaticData", "Translations", "Master.txt");

					string pathName = Path.GetDirectoryName(mastFilePath);
					if (!Directory.Exists(pathName))
					{
						Directory.CreateDirectory(pathName);
					}

					using (StreamWriter masterFileStream = File.AppendText(mastFilePath))
					{
						masterFileStream.WriteLine("{0}{1}", englishTag, EncodeForSaving(englishString));
						masterFileStream.WriteLine("{0}{1}", translatedTag, EncodeForSaving(englishString));
						masterFileStream.WriteLine("");
					}
				}
			}
		}

		public static void AssertDebugNotDefined()
		{
#if DEBUG
			throw new Exception("DEBUG is defined and should not be!");
#endif
		}

		protected Dictionary<string, string> ReadIntoDictionary(StreamReader streamReader)
		{
			var dictionary = new Dictionary<string, string>();

			bool lookingForEnglish = true;
			string englishString = "";

			string line;

			int i = 0;
			while ((line = streamReader.ReadLine()?.Trim()) != null)
			{
				if (line.Length == 0)
				{
					// we are happy to skip blank lines
					continue;
				}

				if (lookingForEnglish)
				{
					if (line.Length < englishTag.Length || !line.StartsWith(englishTag))
					{
						throw new Exception(string.Format("Found unknown string at line {0}. Looking for {1}.", i, englishTag));
					}
					else
					{
						englishString = line.Substring(englishTag.Length);
						lookingForEnglish = false;
					}
				}
				else
				{
					if (line.Length < translatedTag.Length || !line.StartsWith(translatedTag))
					{
						throw new Exception(string.Format("Found unknown string at line {0}. Looking for {1}.", i, translatedTag));
					}
					else
					{
						string translatedString = line.Substring(translatedTag.Length);
						// store the string
						if (!dictionary.ContainsKey(DecodeWhileReading(englishString)))
						{
							dictionary.Add(
								DecodeWhileReading(englishString),
								DecodeWhileReading(translatedString));
						}
						// go back to looking for English
						lookingForEnglish = true;
					}
				}

				i += 1;
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
