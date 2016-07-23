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
	public enum TimeBlock { Future, Today, Yesterday, SameWeek, SameMonth, SameYear, PastYear };

	public static class RelativeTime
	{
		public static Dictionary<TimeBlock, string> BlockDescriptions { get; private set; } = new Dictionary<TimeBlock, string>
		{
			[TimeBlock.Future] = "Future",
			[TimeBlock.Today] = "Today",
			[TimeBlock.Yesterday] = "Yesterday",
			[TimeBlock.SameWeek] = "This Week",
			[TimeBlock.SameMonth] = "This Month",
			[TimeBlock.SameYear] = "This Year",
			[TimeBlock.PastYear] = "Previous Year",
		};

		public static TimeBlock GetTimeBlock(DateTime now, DateTime timeToDescribe)
		{
			if (timeToDescribe < now)
			{
				// in the past
				if (timeToDescribe.Year == now.Year)
				{
					// same year
					if (timeToDescribe.Month == now.Month)
					{
						// same month
						if ((now - timeToDescribe).Days < 7)
						{
							if ((now - timeToDescribe).Days < 3)
							{
								if ((now - timeToDescribe).Days < 1)
								{
									return TimeBlock.Today;
								}
								else
								{
									return TimeBlock.Yesterday;
								}
							}
							else
							{
								return TimeBlock.SameWeek;
							}
						}
						else
						{
							return TimeBlock.SameMonth;
						}
					}
					else
					{
						// same year but different month
						return TimeBlock.SameYear;
					}
				}
				else
				{
					return TimeBlock.PastYear;
				}
			}
			else
			{
				// in the future
				return TimeBlock.Future;
			}
		}

		public static string GetDetail(DateTime now, DateTime timeToDescribe)
		{
			TimeBlock timeBlock = GetTimeBlock(now, timeToDescribe);

			return GetDetail(timeBlock, timeToDescribe);
		}

		public static string GetDetail(TimeBlock timeBlock, DateTime timeToDescribe)
		{
			string time = timeToDescribe.ToString("h:mm tt");
			switch (timeBlock)
			{
				case TimeBlock.Future:
					return "Future Date";
				case TimeBlock.Today:
					return time; // just the time 5:00 pm
				case TimeBlock.Yesterday:
					return time; // just the time 5:00 pm
				case TimeBlock.SameWeek:
					return timeToDescribe.ToString("dddd ") + time; // day of the week and time, Monday 5:00 pm
				case TimeBlock.SameMonth:
					return timeToDescribe.ToString("MMMM d, ") + time; // month, day of the month and time, May 25, 5:00 pm
				case TimeBlock.SameYear:
					return timeToDescribe.ToString("MMMM d, ") + time; // month, day of the month and time, May 25, 5:00 pm
				case TimeBlock.PastYear:
					return timeToDescribe.ToString("yyyy MMMM d, ") + time;// Year, month, day of the month and time, 2009 May 25, 5:00 pm
				default:
					return "Unknown Data";
			}
		}

		/// <summary>
		/// Get all the times sorted for display into TimeBlock groups
		/// </summary>
		/// <param name="nowTime"></param>
		/// <param name="allTimes"></param>
		/// <returns>A dictionary of TimeBlocks containing Dictionaries of the original index and the string to display.</returns>
		public static Dictionary<TimeBlock, Dictionary<int, string>> GroupTimes(DateTime nowTime, List<DateTime> allTimes)
		{
			Dictionary<TimeBlock, Dictionary<int, string>> groupedTimes = new Dictionary<TimeBlock, Dictionary<int, string>>();
			for (int i=0; i<allTimes.Count; i++)
			{
				TimeBlock timeBlock = GetTimeBlock(nowTime, allTimes[i]);
				string displayString = GetDetail(timeBlock, allTimes[i]);

				if(!groupedTimes.ContainsKey(timeBlock))
				{
					groupedTimes.Add(timeBlock, new Dictionary<int, string>());
				}

				groupedTimes[timeBlock].Add(i, displayString);
			}

			return groupedTimes;
		}
	}
}
