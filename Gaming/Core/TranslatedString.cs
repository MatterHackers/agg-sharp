using System;

namespace Gaming.Core
{
	public class TranslatedString
	{
		private string m_EnglishString;
		private string m_LocalizedString;

		public TranslatedString(string englishString)
		{
			// look it up and get the translation
			m_EnglishString = englishString;
			m_LocalizedString = englishString;
		}

		public string LocalizedString
		{
			get
			{
				return m_LocalizedString;
			}
		}

		public string EnglishString
		{
			get
			{
				return m_EnglishString;
			}
		}
	}
}