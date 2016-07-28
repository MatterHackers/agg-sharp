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
			: base(new StyledTypeFace(LiberationSansFont.Instance, pointSize))
		{
		}

		bool HasSpaceBeforeIndex(string stringToCheck, int endOfChecking)
		{
			for (int i = stringToCheck.Length - 1; i >= 0; i--)
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

			if (maxPixelWidth > 0)
			{
				string textToWrap = originalTextToWrap;
				TypeFacePrinter printer = new TypeFacePrinter(textToWrap, styledTypeFace);
				while (textToWrap.Length > 0)
				{
					printer.Text = textToWrap;
					int remainingLength = 1;
					while (printer.GetOffsetLeftOfCharacterIndex(remainingLength).x < maxPixelWidth
						&& remainingLength < printer.Text.Length)
					{
						// get all the characters we can up to the end of the wrap
						remainingLength++;
					}

					while (printer.GetOffsetLeftOfCharacterIndex(remainingLength).x > maxPixelWidth
						&& remainingLength > 1)
					{
						// now trim back to the last break
						remainingLength--;
						while (remainingLength > 1
							&& HasSpaceBeforeIndex(textToWrap, remainingLength)
							&& textToWrap[remainingLength] != ' ')
						{
							remainingLength--;
						}
					}

					if (remainingLength >= 0)
					{
						lines.Add(textToWrap.Substring(0, remainingLength));
					}

					// check if we wrapped because of to long or a '\n'. If '\n' we only trim a leading space if to long.
					if (remainingLength > 1 // we have more than 2 characters left
						&& textToWrap.Length > remainingLength // we are longer than the remaining text
						&& textToWrap[remainingLength] == ' ' // the first new character is a space
						&& textToWrap[remainingLength - 1] != '\n') // the character before the space was not a cr (wrapped because of length)
					{
						textToWrap = textToWrap.Substring(remainingLength + 1);
					}
					else
					{
						textToWrap = textToWrap.Substring(remainingLength);
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