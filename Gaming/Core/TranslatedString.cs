using System;
using System.Collections.Generic;
using System.Text;

namespace Gaming.Core
{
    public class TranslatedString
    {
        private String m_EnglishString;
        private String m_LocalizedString;

        public TranslatedString(String EnglishString)
        {
            // look it up and get the translation
            m_EnglishString = EnglishString;
            m_LocalizedString = EnglishString;
        }

        public String LocalizedString
        {
            get
            {
                return m_LocalizedString;
            }
        }

        public String EnglishString
        {
            get
            {
                return m_EnglishString;
            }
        }
    }
}
