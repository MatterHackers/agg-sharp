//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.4
// Copyright (C) 2002-2005 Maxim Shemanarev (http://www.antigrain.com)
//
// C# port by: Lars Brubaker
// Copyright (C) 2026
//
// Permission to copy, use, modify, sell and distribute this software
// is granted provided this copyright notice appears in all copies.
// This software is provided "as is" without express or implied
// warranty, and with no claim as to its suitability for any purpose.
//
//----------------------------------------------------------------------------
using System;
using System.Collections.Generic;

namespace MatterHackers.Agg
{
	/// <summary>
	/// C++ AGG's scanline_storage_aa8: keeps every scanline rendered into it (as an <see cref="IScanlineSink"/>)
	/// with its covers, and replays them (as an <see cref="IScanlineGenerator"/>) into any scanline - so a shape
	/// can be rasterized once and swept many times, or a boolean result combined again.
	/// </summary>
	public class ScanlineStorageAa8 : IScanlineSink, IScanlineGenerator
	{
		private readonly List<byte> covers = new List<byte>();

		private readonly List<StoredSpan> spans = new List<StoredSpan>();

		private readonly List<StoredScanline> scanlines = new List<StoredScanline>();

		private int currentScanline;

		private int minX = int.MaxValue;

		private int minY = int.MaxValue;

		private int maxX = int.MinValue;

		private int maxY = int.MinValue;

		/// <summary>Empties the storage (C++ calls it before the first scanline is rendered into it).</summary>
		public void prepare()
		{
			this.covers.Clear();
			this.spans.Clear();
			this.scanlines.Clear();
			this.minX = int.MaxValue;
			this.minY = int.MaxValue;
			this.maxX = int.MinValue;
			this.maxY = int.MinValue;
			this.currentScanline = 0;
		}

		public void render(IScanlineCache scanline)
		{
			int y = scanline.y();
			this.minY = Math.Min(this.minY, y);
			this.maxY = Math.Max(this.maxY, y);

			byte[] scanlineCovers = scanline.GetCovers();
			int numSpans = scanline.num_spans();
			this.scanlines.Add(new StoredScanline(y, numSpans, this.spans.Count));
			ScanlineSpan span = scanline.begin();
			for (int i = 0; i < numSpans; i++)
			{
				if (i > 0)
				{
					span = scanline.GetNextScanlineSpan();
				}

				// A solid span (negative len) needs only its one cover; C++ copies len of them and reads the first.
				int len = Math.Abs(span.len);
				int coversId = this.covers.Count;
				for (int c = 0; c < (span.len < 0 ? 1 : len); c++)
				{
					this.covers.Add(scanlineCovers[span.cover_index + c]);
				}

				this.spans.Add(new StoredSpan(span.x, span.len, coversId));
				this.minX = Math.Min(this.minX, span.x);
				this.maxX = Math.Max(this.maxX, span.x + len - 1);
			}
		}

		public int min_x() => this.minX;

		public int min_y() => this.minY;

		public int max_x() => this.maxX;

		public int max_y() => this.maxY;

		public bool rewind_scanlines()
		{
			this.currentScanline = 0;
			return this.scanlines.Count > 0;
		}

		public bool sweep_scanline(IScanlineCache sl)
		{
			sl.ResetSpans();
			for (; ; )
			{
				if (this.currentScanline >= this.scanlines.Count)
				{
					return false;
				}

				StoredScanline stored = this.scanlines[this.currentScanline++];
				for (int i = 0; i < stored.NumSpans; i++)
				{
					StoredSpan span = this.spans[stored.StartSpan + i];
					if (span.Len < 0)
					{
						sl.add_span(span.X, -span.Len, this.covers[span.CoversId]);
					}
					else
					{
						// C++ add_cells; one cell at a time builds the same spans in every scanline type.
						for (int c = 0; c < span.Len; c++)
						{
							sl.add_cell(span.X + c, this.covers[span.CoversId + c]);
						}
					}
				}

				if (sl.num_spans() != 0)
				{
					sl.finalize(stored.Y);
					return true;
				}
			}
		}

		private readonly record struct StoredSpan(int X, int Len, int CoversId);

		private readonly record struct StoredScanline(int Y, int NumSpans, int StartSpan);
	}

	/// <summary>
	/// C++ AGG's scanline_storage_bin: <see cref="ScanlineStorageAa8"/> without covers - it keeps only where
	/// each span is and replays every span at full cover.
	/// </summary>
	public class ScanlineStorageBin : IScanlineSink, IScanlineGenerator
	{
		private readonly List<(int X, int Len)> spans = new List<(int X, int Len)>();

		private readonly List<(int Y, int NumSpans, int StartSpan)> scanlines = new List<(int Y, int NumSpans, int StartSpan)>();

		private int currentScanline;

		private int minX = int.MaxValue;

		private int minY = int.MaxValue;

		private int maxX = int.MinValue;

		private int maxY = int.MinValue;

		/// <summary>Empties the storage (C++ calls it before the first scanline is rendered into it).</summary>
		public void prepare()
		{
			this.spans.Clear();
			this.scanlines.Clear();
			this.minX = int.MaxValue;
			this.minY = int.MaxValue;
			this.maxX = int.MinValue;
			this.maxY = int.MinValue;
			this.currentScanline = 0;
		}

		public void render(IScanlineCache scanline)
		{
			int y = scanline.y();
			this.minY = Math.Min(this.minY, y);
			this.maxY = Math.Max(this.maxY, y);

			int numSpans = scanline.num_spans();
			this.scanlines.Add((y, numSpans, this.spans.Count));
			ScanlineSpan span = scanline.begin();
			for (int i = 0; i < numSpans; i++)
			{
				if (i > 0)
				{
					span = scanline.GetNextScanlineSpan();
				}

				int len = Math.Abs(span.len);
				this.spans.Add((span.x, len));
				this.minX = Math.Min(this.minX, span.x);
				this.maxX = Math.Max(this.maxX, span.x + len - 1);
			}
		}

		public int min_x() => this.minX;

		public int min_y() => this.minY;

		public int max_x() => this.maxX;

		public int max_y() => this.maxY;

		public bool rewind_scanlines()
		{
			this.currentScanline = 0;
			return this.scanlines.Count > 0;
		}

		public bool sweep_scanline(IScanlineCache sl)
		{
			sl.ResetSpans();
			for (; ; )
			{
				if (this.currentScanline >= this.scanlines.Count)
				{
					return false;
				}

				var stored = this.scanlines[this.currentScanline++];
				for (int i = 0; i < stored.NumSpans; i++)
				{
					var span = this.spans[stored.StartSpan + i];
					sl.add_span(span.X, span.Len, 255);
				}

				if (sl.num_spans() != 0)
				{
					sl.finalize(stored.Y);
					return true;
				}
			}
		}
	}
}
