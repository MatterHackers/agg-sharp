using MatterHackers.Agg;
using MatterHackers.Agg.PlatformAbstract;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace MatterHackers.Localizations
{
	public class TranslationMap
	{
		protected const string engishTag = "English:";
		protected const string translatedTag = "Translated:";

		protected Dictionary<string, string> translationDictionary = new Dictionary<string, string>();

		public string TwoLetterIsoLanguageName { get; private set; }

		public TranslationMap(string pathToTranslationsFolder, string twoLetterIsoLanguageName = "")
		{
			// Select either the user supplied language name or the current thread language name
			this.TwoLetterIsoLanguageName = string.IsNullOrEmpty(twoLetterIsoLanguageName) ?
				Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName.ToLower():
				twoLetterIsoLanguageName.ToLower();

			string translationFilePath = Path.Combine(pathToTranslationsFolder, TwoLetterIsoLanguageName, "Translation.txt");

			// In English no translation file exists and no dictionary will be initialized or loaded
			if (StaticData.Instance.FileExists(translationFilePath))
			{
				translationDictionary = ReadIntoDictionary(translationFilePath);
			}
		}

		public virtual string Translate(string englishString)
		{
			// Skip dictionary lookups for English
			if (TwoLetterIsoLanguageName == "en")
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

			string[] lines = StaticData.Instance.ReadAllLines(pathAndFilename);
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
			string relativePath = Path.Combine(pathToTranslationsFolder, "Master.txt");
			this.masterFilePath = StaticData.Instance.MapPath(relativePath);

			// Override the default logic and load master.txt in English debug builds
			if (this.TwoLetterIsoLanguageName == "en")
			{
				translationDictionary = ReadIntoDictionary(relativePath);
			}
		}

		public override string Translate(string englishString)
		{
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
			if (OsInformation.OperatingSystem == OSType.Windows)
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