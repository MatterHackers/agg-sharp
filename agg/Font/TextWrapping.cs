using MatterHackers.Agg.Platform;
using System;
using System.Collections.Generic;
using System.Text;

namespace MatterHackers.Agg.Font
{
	abstract public class TextWrapping
	{
		protected StyledTypeFace styledTypeFace;

		// you can't
		public TextWrapping(StyledTypeFace styledTypeFace)
		{
			this.styledTypeFace = styledTypeFace;
		}

		public string InsertCRs(string textToWrap, double maxPixelWidth)
		{
			StringBuilder textWithCRs = new StringBuilder();
			List<string> lines = WrapText(textToWrap, maxPixelWidth);
			for (int i = 0; i < lines.Count; i++)
			{
				string line = lines[i];
				if (i > 0)
				{
					textWithCRs.Append("\n");
				}

				textWithCRs.Append(line);
			}

			return textWithCRs.ToString();
		}

		public List<string> WrapText(string textToWrap, double maxPixelWidth)
		{
			List<string> finalLines = new List<string>();
			string[] splitOnNL = textToWrap.Split('\n');
			foreach (string line in splitOnNL)
			{
				List<string> linesFromWidth = WrapSingleLineOnWidth(line, maxPixelWidth);
				if (linesFromWidth.Count > 0)
				{
					finalLines.AddRange(linesFromWidth);
				}
			}

			return finalLines;
		}

		abstract public List<string> WrapSingleLineOnWidth(string originalTextToWrap, double maxPixelWidth);
	}

	public class EnglishTextWrapping : TextWrapping
	{
		public EnglishTextWrapping(StyledTypeFace styledTypeFace)
			: base(styledTypeFace)
		{
		}

		public EnglishTextWrapping(double pointSize)
			: base(new StyledTypeFace(AggContext.DefaultFont, pointSize))
		{
		}

		bool HasSpaceBeforeIndex(string stringToCheck, int endOfChecking)
		{
			for (int i = Math.Min(endOfChecking, stringToCheck.Length - 1); i >= 0; i--)
			{
				if (stringToCheck[i] == ' ')
				{
					return true;
				}
			}

			return false;
		}

		public override List<string> WrapSingleLineOnWidth(string originalTextToWrap, double maxPixelWidth)
		{
			List<string> lines = new List<string>();

			if (maxPixelWidth > 0
				&& originalTextToWrap.Length > 0)
			{
				string textToWrap = originalTextToWrap;
				TypeFacePrinter printer = new TypeFacePrinter(textToWrap, styledTypeFace);
				while (textToWrap.Length > 0)
				{
					printer.Text = textToWrap;
					int countBeforeWrap;

					double currentLength = 0;
					for (countBeforeWrap = 0; countBeforeWrap < printer.Text.Length; countBeforeWrap++)
					{
						if (currentLength > maxPixelWidth)
						{
							break;
						}
						currentLength += printer.TypeFaceStyle.GetAdvanceForCharacter(textToWrap, countBeforeWrap);
					}

					while (printer.GetOffsetLeftOfCharacterIndex(countBeforeWrap).X > maxPixelWidth
						&& countBeforeWrap > 1)
					{
						// now trim back to the last break
						countBeforeWrap--;
						while (countBeforeWrap > 1
							&& HasSpaceBeforeIndex(textToWrap, countBeforeWrap)
							&& textToWrap[countBeforeWrap] != ' ')
						{
							countBeforeWrap--;
						}
					}

					if (countBeforeWrap >= 0)
					{
						lines.Add(textToWrap.Substring(0, countBeforeWrap));
					}

					// check if we wrapped because of to long or a '\n'. If '\n' we only trim a leading space if to long.
					if (countBeforeWrap > 1 // we have more than 2 characters left
						&& textToWrap.Length > countBeforeWrap // we are longer than the remaining text
						&& textToWrap[countBeforeWrap] == ' ' // the first new character is a space
						&& textToWrap[countBeforeWrap - 1] != '\n') // the character before the space was not a cr (wrapped because of length)
					{
						textToWrap = textToWrap.Substring(countBeforeWrap + 1);
					}
					else
					{
						textToWrap = textToWrap.Substring(countBeforeWrap);
					}
				}
			}
			else
			{
				lines.Add(originalTextToWrap);
			}

			return lines;
		}
	}
}