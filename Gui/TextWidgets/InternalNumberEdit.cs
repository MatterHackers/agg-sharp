/*
Copyright (c) 2014, Lars Brubaker
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

namespace MatterHackers.Agg.UI
{
	public class InternalNumberEdit : InternalTextEditWidget
	{
		private HashSet<char> allowedChars = null;
		private double minValue;
		private double maxValue;
		private double increment;
		private bool allowNegatives;
		private bool allowDecimals;

		public InternalNumberEdit(double startingValue,
			double pointSize,
			double pixelWidth,
			double pixelHeight,
			bool allowNegatives,
			bool allowDecimals,
			double minValue,
			double maxValue,
			double increment,
			int tabIndex)
			: base(startingValue.ToString(), pointSize, false, tabIndex)
		{
			this.allowDecimals = allowDecimals;
			this.allowNegatives = allowNegatives;
			this.minValue = minValue;
			this.maxValue = maxValue;
			this.increment = increment;

			MergeTypingDuringUndo = false;

			allowedChars = new HashSet<char>();
			allowedChars.Add('0');
			allowedChars.Add('1');
			allowedChars.Add('2');
			allowedChars.Add('3');
			allowedChars.Add('4');
			allowedChars.Add('5');
			allowedChars.Add('6');
			allowedChars.Add('7');
			allowedChars.Add('8');
			allowedChars.Add('9');
			allowedChars.Add('0');
			if (allowNegatives)
			{
				allowedChars.Add('-');
			}
			else
			{
				if (startingValue < 0 || increment < 0 || minValue < 0 || maxValue < 0)
				{
					throw new Exception("To have a startingValue, min, max, or increment be negative, you need to set 'allowNegatives' to true.");
				}
			}

			if (allowDecimals)
			{
				allowedChars.Add('.');
			}
			else
			{
				if (startingValue != (int)startingValue || increment != (int)increment || minValue != (int)minValue || maxValue != (int)maxValue)
				{
					throw new Exception("To have a fractional startingValue, min, max, or increment you need to set 'allowDecimals' to true.");
				}
			}
		}

		public double MinValue
		{
			get
			{
				return minValue;
			}

			set
			{
				if (!allowNegatives && value < 0)
				{
					throw new Exception("This has to be positive.");
				}

				if (!allowDecimals && value != (int)value)
				{
					throw new Exception("This can't be a decimal number.");
				}

				minValue = value;
			}
		}

		public double MaxValue
		{
			get
			{
				return maxValue;
			}

			set
			{
				if (!allowNegatives && value < 0)
				{
					throw new Exception("This has to be positive.");
				}

				if (!allowDecimals && value != (int)value)
				{
					throw new Exception("This can't be a decimal number.");
				}

				maxValue = value;
			}
		}

		/// <summary>
		/// Gets or sets the number of decimal places to show with this number. Max is currently 10
		/// any number below 0 will render without formating (string default behavior).
		/// </summary>
		public int MaxDecimalsPlaces { get; set; } = -1;

		public double Value
		{
			get
			{
				double errorReturn = Math.Max(minValue, Math.Min(0, maxValue));
				if (Text == "" || Text == "." || Text == "-" || Text == "-.")
				{
					return errorReturn;
				}

				double value = minValue;
				if (double.TryParse(Text, out value))
				{
					return value;
				}

				return errorReturn;
			}

			set
			{
				string format = "";
				if (MaxDecimalsPlaces > 0)
				{
					format = "0." + new string('#', Math.Min(10, MaxDecimalsPlaces));
				}

				double newValue = ValidateRange(value);
				if (newValue != Value)
				{
					Text = newValue.ToString(format);
				}
				else // lets make sure it has the same text as the value
				{
					double currentValue;
					if (double.TryParse(Text, out currentValue))
					{
						if (currentValue != newValue)
						{
							// the text does not match the value so set it
							Text = newValue.ToString(format);
						}
					}
					else // the text cannot be parsed so set it
					{
						Text = newValue.ToString(format);
						CharIndexToInsertBefore = Text.Length;
					}
				}
			}
		}

		private double ValidateRange(double valueToValidate)
		{
			if (valueToValidate < minValue)
			{
				valueToValidate = minValue;
			}

			if (valueToValidate > maxValue)
			{
				valueToValidate = maxValue;
			}

			return valueToValidate;
		}

		public override void OnKeyDown(KeyEventArgs keyEvent)
		{
			// this must be called first to ensure we get the correct Handled state
			base.OnKeyDown(keyEvent);

			if (!keyEvent.Handled)
			{
				switch (keyEvent.KeyCode)
				{
					case Keys.Up:
						keyEvent.SuppressKeyPress = true;
						keyEvent.Handled = true;
						Value = Value + increment;
						OnEditComplete(keyEvent);
						break;

					case Keys.Down:
						keyEvent.SuppressKeyPress = true;
						keyEvent.Handled = true;
						Value = Value - increment;
						OnEditComplete(keyEvent);
						break;
				}
			}
		}

		public override void OnEditComplete(EventArgs e)
		{
			Value = Value;
			base.OnEditComplete(e);
		}

		public override void OnKeyPress(KeyPressEventArgs keyPressEvent)
		{
			// this must be called first to ensure we get the correct Handled state
			base.OnKeyPress(keyPressEvent);

			if (!keyPressEvent.Handled)
			{
				if (allowedChars.Contains(keyPressEvent.KeyChar))
				{
					bool hadSelection = Selecting;

					int prevCharIndexToInsertBefore = CharIndexToInsertBefore;
					// let's check and see if the new string is a valid number
					double number;
					if (Text == "." && allowDecimals)
					{
						return;
					}

					if (Text == "-" && allowNegatives)
					{
						return;
					}

					if (Text == "-." && allowDecimals && allowNegatives)
					{
						return;
					}

					if (!double.TryParse(Text, out number))
					{
						if (hadSelection)
						{
							// we have to undo twice, once for the delete selection and once for the bad character.
							Undo();
						}

						Undo();
						CharIndexToInsertBefore = prevCharIndexToInsertBefore;
						FixBarPosition(DesiredXPositionOnLine.Set);
					}
				}
			}
		}
	}
}