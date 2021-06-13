using Gaming.Core;
using MatterHackers.Agg;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;

namespace Gaming.Game
{
	public class DataViewGraph
	{
		private ColorF SentDataLineColor = new ColorF(200, 200, 0);
		private ColorF ReceivedDataLineColor = new ColorF(0, 200, 20);
		private ColorF BoxColor = new ColorF(10, 25, 240);

		private double m_DataViewMinY;
		private double m_DataViewMaxY;
		private bool m_DynamiclyScaleYRange;
		private Vector2 m_Position;
		private uint m_Width;
		private uint m_Height;
		private Dictionary<string, HistoryData> m_DataHistoryArray;
		private int m_ColorIndex;
		private VertexStorage m_LinesToDraw;

		internal class HistoryData
		{
			private int m_Capacity;
			private TwoSidedStack<double> m_Data;

			internal double m_TotalValue;
			internal Color m_Color;

			internal HistoryData(int capacity, IColorType color)
			{
				m_Color = color.ToColor();
				m_Capacity = capacity;
				m_Data = new TwoSidedStack<double>();
				Reset();
			}

			public int Count
			{
				get
				{
					return m_Data.Count;
				}
			}

			internal void Add(double value)
			{
				if (m_Data.Count == m_Capacity)
				{
					m_TotalValue -= m_Data.PopHead();
				}

				m_Data.PushTail(value);

				m_TotalValue += value;
			}

			internal void Reset()
			{
				m_TotalValue = 0;
				m_Data.Zero();
			}

			internal double GetItem(int itemIndex)
			{
				if (itemIndex < m_Data.Count)
				{
					return m_Data[itemIndex];
				}
				else
				{
					return 0;
				}
			}

			internal double GetMaxValue()
			{
				double max = -9999999999;
				for (int i = 0; i < m_Data.Count; i++)
				{
					if (m_Data[i] > max)
					{
						max = m_Data[i];
					}
				}

				return max;
			}

			internal double GetMinValue()
			{
				double min = 9999999999;
				for (int i = 0; i < m_Data.Count; i++)
				{
					if (m_Data[i] < min)
					{
						min = m_Data[i];
					}
				}

				return min;
			}

			internal double GetAverageValue()
			{
				return m_TotalValue / m_Data.Count;
			}
		}

		public DataViewGraph(Vector2 renderPosition)
			: this(renderPosition, 80, 50, 0, 0)
		{
			m_DynamiclyScaleYRange = true;
		}

		public DataViewGraph(Vector2 renderPosition, uint width, uint Height)
			: this(renderPosition, width, Height, 0, 0)
		{
			m_DynamiclyScaleYRange = true;
		}

		public DataViewGraph(Vector2 renderPosition, uint width, uint Height, double StartMin, double StartMax)
		{
			m_LinesToDraw = new VertexStorage();
			m_DataHistoryArray = new Dictionary<string, HistoryData>();

			m_Width = width;
			m_Height = Height;
			m_DataViewMinY = StartMin;
			m_DataViewMaxY = StartMax;
			if (StartMin == 0 && StartMax == 0)
			{
				m_DataViewMaxY = -999999;
				m_DataViewMinY = 999999;
			}

			m_Position = renderPosition;
			m_DynamiclyScaleYRange = false;
		}

		public double GetAverageValue(string dataType)
		{
			m_DataHistoryArray.TryGetValue(dataType, out HistoryData trendLine);
			if (trendLine != null)
			{
				return trendLine.GetAverageValue();
			}

			return 0;
		}

		public void Draw(MatterHackers.Agg.Transform.ITransform position, Graphics2D renderer)
		{
			double textHeight = m_Position.Y - 20;
			double Range = (m_DataViewMaxY - m_DataViewMinY);
			VertexSourceApplyTransform TransformedLinesToDraw;
			Stroke StrockedTransformedLinesToDraw;

			var BackGround = new RoundedRect(m_Position.X, m_Position.Y - 1, m_Position.X + m_Width, m_Position.Y - 1 + m_Height + 2, 5);
			var TransformedBackGround = new VertexSourceApplyTransform(BackGround, position);
			renderer.Render(TransformedBackGround, new Color(0, 0, 0, .5));

			// if the 0 line is within the window than draw it.
			if (m_DataViewMinY < 0 && m_DataViewMaxY > 0)
			{
				m_LinesToDraw.remove_all();
				m_LinesToDraw.MoveTo(m_Position.X,
					m_Position.Y + ((0 - m_DataViewMinY) * m_Height / Range));
				m_LinesToDraw.LineTo(m_Position.X + m_Width,
					m_Position.Y + ((0 - m_DataViewMinY) * m_Height / Range));
				TransformedLinesToDraw = new VertexSourceApplyTransform(m_LinesToDraw, position);
				StrockedTransformedLinesToDraw = new Stroke(TransformedLinesToDraw);
				renderer.Render(StrockedTransformedLinesToDraw, new Color(0, 0, 0, 1));
			}

			double MaxMax = -999999999;
			double MinMin = 999999999;
			double MaxAverage = 0;
			foreach (KeyValuePair<string, HistoryData> historyKeyValue in m_DataHistoryArray)
			{
				HistoryData history = historyKeyValue.Value;
				m_LinesToDraw.remove_all();
				MaxMax = System.Math.Max(MaxMax, history.GetMaxValue());
				MinMin = System.Math.Min(MinMin, history.GetMinValue());
				MaxAverage = System.Math.Max(MaxAverage, history.GetAverageValue());
				for (int i = 0; i < m_Width - 1; i++)
				{
					if (i == 0)
					{
						m_LinesToDraw.MoveTo(m_Position.X + i,
							m_Position.Y + ((history.GetItem(i) - m_DataViewMinY) * m_Height / Range));
					}
					else
					{
						m_LinesToDraw.LineTo(m_Position.X + i,
							m_Position.Y + ((history.GetItem(i) - m_DataViewMinY) * m_Height / Range));
					}
				}

				TransformedLinesToDraw = new VertexSourceApplyTransform(m_LinesToDraw, position);
				StrockedTransformedLinesToDraw = new Stroke(TransformedLinesToDraw);
				renderer.Render(StrockedTransformedLinesToDraw, history.m_Color);

				string Text = historyKeyValue.Key + ": Min:" + MinMin.ToString("0.0") + " Max:" + MaxMax.ToString("0.0");
				renderer.DrawString(Text, m_Position.X, textHeight - m_Height);
				textHeight -= 20;
			}

			var backGround2 = new RoundedRect(m_Position.X, m_Position.Y - 1, m_Position.X + m_Width, m_Position.Y - 1 + m_Height + 2, 5);
			var TransformedBackGround2 = new VertexSourceApplyTransform(backGround2, position);
			var strockedTransformedBackGround = new Stroke(TransformedBackGround2);
			renderer.Render(strockedTransformedBackGround, new Color(0.0, 0, 0, 1));

			//renderer.Color = BoxColor;
			//renderer.DrawRect(m_Position.x, m_Position.y - 1, m_Width, m_Height + 2);
		}

		public void AddData(string dataType, double newData)
		{
			if (m_DynamiclyScaleYRange)
			{
				m_DataViewMaxY = System.Math.Max(m_DataViewMaxY, newData);
				m_DataViewMinY = System.Math.Min(m_DataViewMinY, newData);
			}

			if (!m_DataHistoryArray.ContainsKey(dataType))
			{
				var lineColor = new Color(255, 255, 255);
				switch (m_ColorIndex++ % 3)
				{
					case 0:
						lineColor = new Color(255, 55, 55);
						break;

					case 1:
						lineColor = new Color(55, 255, 55);
						break;

					case 2:
						lineColor = new Color(55, 55, 255);
						break;
				}

				m_DataHistoryArray.Add(dataType, new HistoryData((int)m_Width, lineColor));
			}

			m_DataHistoryArray[dataType].Add(newData);
		}

		public void Reset()
		{
			m_DataViewMaxY = 1;
			m_DataViewMinY = 99999;
			foreach (KeyValuePair<string, HistoryData> historyKeyValue in m_DataHistoryArray)
			{
				historyKeyValue.Value.Reset();
			}
		}
	}

}