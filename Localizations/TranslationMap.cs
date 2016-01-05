using MatterHackers.Agg;
using MatterHackers.Agg.PlatformAbstract;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace MatterHackers.Localizations
{
	public class TranslationMap
	{
		private const string engishTag = "English:";
		private const string translatedTag = "Translated:";

		private Dictionary<string, string> translationDictionary = new Dictionary<string, string>();
		private string translationFilePath;

		public string TwoLetterIsoLanguageName { get; private set; }

		public TranslationMap(string pathToTranslationsFolder, string twoLetterIsoLanguageName = "")
		{
			if (twoLetterIsoLanguageName == "")
			{
				twoLetterIsoLanguageName = Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName;
			}

			LoadTranslation(pathToTranslationsFolder, twoLetterIsoLanguageName);
		}

		private void ReadIntoDictonary(Dictionary<string, string> dictionary, string pathAndFilename)
		{
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
		}

		public void LoadTranslation(string pathToTranslationsFolder, string twoLetterIsoLanguageName)
		{
			this.TwoLetterIsoLanguageName = twoLetterIsoLanguageName.ToLower();

			this.translationFilePath = Path.Combine(pathToTranslationsFolder, TwoLetterIsoLanguageName, "Translation.txt");
			if (StaticData.Instance.FileExists(translationFilePath))
			{
				ReadIntoDictonary(translationDictionary, translationFilePath);
			}
		}

		/// <summary>
		/// Encodes for saving, escaping newlines
		/// </summary>
		private string EncodeForSaving(string stringToEncode)
		{
			return stringToEncode.Replace("\n", "\\n");
		}

		/// <summary>
		/// Decodes the while reading, unescaping newlines
		/// </summary>
		private string DecodeWhileReading(string stringToDecode)
		{
			return stringToDecode.Replace("\\n", "\n");
		}

		private void AddNewString(Dictionary<string, string> dictionary, string pathAndFilename, string englishString)
		{
			// We only ship release and this could cause a write to the ProgramFiles directory which is not allowed.
			// So we only write translation text while in debug (another solution in the future could be implemented). LBB
#if DEBUG
			if (OsInformation.OperatingSystem == OSType.Windows)
			{
				// TODO: make sure we don't throw an assertion when running from the ProgramFiles directory.
				// Don't do saving when we are.
				if (!dictionary.ContainsKey(englishString))
				{
					dictionary.Add(englishString, englishString);

					using (TimedLock.Lock(this, "TranslationMap"))
					{
						string staticDataPath = StaticData.Instance.MapPath(pathAndFilename);
						string pathName = Path.GetDirectoryName(staticDataPath);
						if (!Directory.Exists(pathName))
						{
							Directory.CreateDirectory(pathName);
						}

						using (StreamWriter masterFileStream = File.AppendText(staticDataPath))
						{
							masterFileStream.WriteLine("{0}{1}", engishTag, EncodeForSaving(englishString));
							masterFileStream.WriteLine("{0}{1}", translatedTag, EncodeForSaving(englishString));
							masterFileStream.WriteLine("");
						}
					}
				}
			}
#endif
		}

		public string Translate(string englishString)
		{
			string tranlatedString;
			if (!translationDictionary.TryGetValue(englishString, out tranlatedString))
			{
#if DEBUG
				if (TwoLetterIsoLanguageName == "en")
				{
					AddNewString(translationDictionary, translationFilePath, englishString);
				}
#endif
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
	}
}