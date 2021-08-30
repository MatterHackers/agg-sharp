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

namespace MatterHackers.VectorMath
{
	/*
	 * Functions taken from Tween.js - Licensed under the MIT license
	 * at https://github.com/sole/tween.js
	 */
	public class Easing
	{
		public enum EaseType
		{
			Linear,
			Quadratic,
			Cubic,
			Quartic,
			Quintic,
			Sinusoidal,
		}

		public enum EaseOption
		{
			In,
			Out,
			InOut
		}

		/// <summary>
		/// Iteretively solve for the required input given a known output
		/// </summary>
		/// <param name="function">The easing function (must be monotonic)</param>
		/// <param name="output">The output value needing an input (0 to 1)</param>
		/// <param name="errorTolerance">When this error is achived evaluation will stop</param>
		/// <returns></returns>
		public static double CalculateInverse(Func<double, double> function, double output, double errorTolerance = .000000001)
		{
			if (output >= 0 && output <= 1)
			{
				var start = 0.0;
				var end = 1.0;
				var mid = .5;
				var iterations = 0;
				var error = 1.0;
				while (error > errorTolerance && iterations < 128)
				{
					iterations++;
					var delta = end - start;
					mid = start + delta / 2;
					var testOutput = function(mid);

					error = Math.Abs(output - testOutput);
					if (error <= errorTolerance)
					{
						return mid;
					}

					if (testOutput > output)
					{
						end = mid;
					}
					else
					{
						start = mid;
					}
				}

				return mid;
			}

			return 0;
		}

		public static Func<double, double> GetEasingFunction(EaseType easeType, EaseOption easeOption)
		{
			switch (easeType)
			{
				case EaseType.Quadratic:
					switch (easeOption)
					{
						case EaseOption.In:
							return Quadratic.In;
						case EaseOption.Out:
							return Quadratic.Out;
						case EaseOption.InOut:
							return Quadratic.InOut;
					}

					break;

				case EaseType.Cubic:
					switch (easeOption)
					{
						case EaseOption.In:
							return Cubic.In;
						case EaseOption.Out:
							return Cubic.Out;
						case EaseOption.InOut:
							return Cubic.InOut;
					}

					break;

				case EaseType.Quartic:
					switch (easeOption)
					{
						case EaseOption.In:
							return Quartic.In;
						case EaseOption.Out:
							return Quartic.Out;
						case EaseOption.InOut:
							return Quartic.InOut;
					}

					break;

				case EaseType.Quintic:
					switch (easeOption)
					{
						case EaseOption.In:
							return Quintic.In;
						case EaseOption.Out:
							return Quintic.Out;
						case EaseOption.InOut:
							return Quintic.InOut;
					}

					break;

				case EaseType.Sinusoidal:
					switch (easeOption)
					{
						case EaseOption.In:
							return Sinusoidal.In;
						case EaseOption.Out:
							return Sinusoidal.Out;
						case EaseOption.InOut:
							return Sinusoidal.InOut;
					}

					break;
			}

			return Linear;
		}

		public static double Calculate(EaseType easeType, EaseOption easeOption, double k)
		{
			return GetEasingFunction(easeType, easeOption)(k);
		}

		/// <summary>
		/// Given the easing functions output, find the approximate input that would generate it.
		/// </summary>
		/// <param name="easeType">The easing type</param>
		/// <param name="easeOption">The easing option</param>
		/// <param name="k">The output value to find the input for</param>
		/// <returns></returns>
		public static double CalculateInverse(EaseType easeType, EaseOption easeOption, double k, double errorTolerance = .000000001)
		{
			if (easeType == EaseType.Linear)
			{
				return k;
			}

			return CalculateInverse(GetEasingFunction(easeType, easeOption), k, errorTolerance);
		}

		public static double Linear(double k)
		{
			return k;
		}

		public class Quadratic
		{
			public static double In(double k)
			{
				return k * k;
			}

			public static double Out(double k)
			{
				return k * (2f - k);
			}

			public static double InOut(double k)
			{
				if ((k *= 2f) < 1f)
				{
					return 0.5f * k * k;
				}

				return -0.5f * ((k -= 1f) * (k - 2f) - 1f);
			}
		}

		public class Cubic
		{
			public static double In(double k)
			{
				return k * k * k;
			}

			public static double Out(double k)
			{
				return 1f + ((k -= 1f) * k * k);
			}

			public static double InOut(double k)
			{
				if ((k *= 2f) < 1f)
				{
					return 0.5f * k * k * k;
				}

				return 0.5f * ((k -= 2f) * k * k + 2f);
			}
		}

		public class Quartic
		{
			public static double In(double k)
			{
				return k * k * k * k;
			}

			public static double Out(double k)
			{
				return 1f - ((k -= 1f) * k * k * k);
			}

			public static double InOut(double k)
			{
				if ((k *= 2f) < 1f)
				{
					return 0.5f * k * k * k * k;
				}

				return -0.5f * ((k -= 2f) * k * k * k - 2f);
			}
		}

		public class Quintic
		{
			public static double In(double k)
			{
				return k * k * k * k * k;
			}

			public static double Out(double k)
			{
				return 1f + ((k -= 1f) * k * k * k * k);
			}

			public static double InOut(double k)
			{
				if ((k *= 2f) < 1f)
				{
					return 0.5f * k * k * k * k * k;
				}

				return 0.5f * ((k -= 2f) * k * k * k * k + 2f);
			}
		}

		public class Sinusoidal
		{
			public static double In(double k)
			{
				return 1f - Math.Cos(k * Math.PI / 2f);
			}

			public static double Out(double k)
			{
				return Math.Sin(k * Math.PI / 2f);
			}

			public static double InOut(double k)
			{
				return 0.5f * (1f - Math.Cos(Math.PI * k));
			}
		}

		public class Exponential
		{
			public static double In(double k)
			{
				return k == 0f ? 0f : Math.Pow(1024f, k - 1f);
			}

			public static double Out(double k)
			{
				return k == 1f ? 1f : 1f - Math.Pow(2f, -10f * k);
			}

			public static double InOut(double k)
			{
				if (k == 0f)
				{
					return 0f;
				}

				if (k == 1f)
				{
					return 1f;
				}

				if ((k *= 2f) < 1f)
				{
					return 0.5f * Math.Pow(1024f, k - 1f);
				}

				return 0.5f * (-Math.Pow(2f, -10f * (k - 1f)) + 2f);
			}
		}

		public class Circular
		{
			public static double In(double k)
			{
				return 1f - Math.Sqrt(1f - k * k);
			}

			public static double Out(double k)
			{
				return Math.Sqrt(1f - ((k -= 1f) * k));
			}

			public static double InOut(double k)
			{
				if ((k *= 2f) < 1f)
				{
					return -0.5f * (Math.Sqrt(1f - k * k) - 1);
				}

				return 0.5f * (Math.Sqrt(1f - (k -= 2f) * k) + 1f);
			}
		}

		public class Elastic
		{
			public static double In(double k)
			{
				if (k == 0)
				{
					return 0;
				}

				if (k == 1)
				{
					return 1;
				}

				return -Math.Pow(2f, 10f * (k -= 1f)) * Math.Sin((k - 0.1f) * (2f * Math.PI) / 0.4f);
			}

			public static double Out(double k)
			{
				if (k == 0)
				{
					return 0;
				}

				if (k == 1)
				{
					return 1;
				}

				return Math.Pow(2f, -10f * k) * Math.Sin((k - 0.1f) * (2f * Math.PI) / 0.4f) + 1f;
			}

			public static double InOut(double k)
			{
				if ((k *= 2f) < 1f)
				{
					return -0.5f * Math.Pow(2f, 10f * (k -= 1f)) * Math.Sin((k - 0.1f) * (2f * Math.PI) / 0.4f);
				}

				return Math.Pow(2f, -10f * (k -= 1f)) * Math.Sin((k - 0.1f) * (2f * Math.PI) / 0.4f) * 0.5f + 1f;
			}
		}

		public class Back
		{
			private static readonly double S = 1.70158f;
			private static readonly double S2 = 2.5949095f;

			public static double In(double k)
			{
				return k * k * ((S + 1f) * k - S);
			}

			public static double Out(double k)
			{
				return (k -= 1f) * k * ((S + 1f) * k + S) + 1f;
			}

			public static double InOut(double k)
			{
				if ((k *= 2f) < 1f)
				{
					return 0.5f * (k * k * ((S2 + 1f) * k - S2));
				}

				return 0.5f * ((k -= 2f) * k * ((S2 + 1f) * k + S2) + 2f);
			}
		}

		public class Bounce
		{
			public static double In(double k)
			{
				return 1f - Out(1f - k);
			}

			public static double Out(double k)
			{
				if (k < (1f / 2.75f))
				{
					return 7.5625f * k * k;
				}
				else if (k < (2f / 2.75f))
				{
					return 7.5625f * (k -= 1.5f / 2.75f) * k + 0.75f;
				}
				else if (k < (2.5f / 2.75f))
				{
					return 7.5625f * (k -= 2.25f / 2.75f) * k + 0.9375f;
				}
				else
				{
					return 7.5625f * (k -= 2.625f / 2.75f) * k + 0.984375f;
				}
			}

			public static double InOut(double k)
			{
				if (k < 0.5f)
				{
					return In(k * 2f) * 0.5f;
				}

				return Out(k * 2f - 1f) * 0.5f + 0.5f;
			}
		}
	}
}
