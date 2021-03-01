/*
Copyright (c) 2015, Lars Brubaker
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
using System.Linq;

namespace MatterHackers.Agg
{
    public class StatisticsTracker
    {
        private List<double> data = new List<double>();

        #region Median
        #endregion
        public double Median
        {
            get
            {
                if (data.Count > 0)
                {
                    if ((data.Count % 2) == 0)
                    {
                        return data[data.Count / 2 - 1] + data[data.Count / 2];
                    }
                    else
                    {
                        return data[data.Count / 2];
                    }
                }

                return 0;
            }
        }

        #region Mode

            private double cachedMode = 0;
        private int countModeCalculatedOn = 0;

        public double Mode
        {
            get
            {
                if (countModeCalculatedOn != data.Count)
                {
                    var groups = data.GroupBy(v => v);
                    int maxCount = groups.Max(g => g.Count());

                    countModeCalculatedOn = data.Count;
                    cachedMode = groups.First(g => g.Count() == maxCount).Key;
                }

                return cachedMode;
            }
        }

        #endregion Mode

        #region Mean Function and cache data

        private double cachedMean = 0;
        private int countMeanCalculatedOn = 0;

        public double Mean
        {
            get
            {
                if (countMeanCalculatedOn != data.Count)
                {
                    double total = 0;
                    foreach (double value in data)
                    {
                        total += value;
                    }

                    countMeanCalculatedOn = data.Count;
                    cachedMean = total / data.Count;
                }

                return cachedMean;
            }
        }

        #endregion Mean Function and cache data

        #region Variance Function and cache data

        private double cachedVariance = 0;
        private int countVarianceCalculatedOn = 0;

		public string Name
		{
			get; private set;
		}

		public StatisticsTracker(string name)
		{
			this.Name = name;
		}

        public double Variance
        {
            get
            {
                if (data.Count > 1
                    && countVarianceCalculatedOn != data.Count)
                {
                    List<double> dataMinusMean_Squared = new List<double>();
                    foreach (double value in data)
                    {
                        double diffFromMean = value - Mean;
                        dataMinusMean_Squared.Add(diffFromMean * diffFromMean);
                    }

                    double total = 0;
                    foreach (double value in dataMinusMean_Squared)
                    {
                        total += value;
                    }

                    countVarianceCalculatedOn = data.Count;
                    cachedVariance = total / (data.Count-1);
                }

                return cachedVariance;
            }
        }

        #endregion Variance Function and cache data

        public double StandardDeviation
        {
            get
            {
                if (data.Count > 0)
                {
                    return Math.Sqrt(Variance);
                }
                return 0;
            }
        }

        public int Count { get { return data.Count; } }

        public void AddValue(double value)
        {
            data.Add(value);
        }
    }
}