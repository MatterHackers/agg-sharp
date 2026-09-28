namespace MatterHackers.Agg
{
	public struct ScanlineSpan
	{
		public int x;
		public int len;
		public int cover_index;
	};

	public interface IScanlineCache
	{
		void finalize(int y);

		void reset(int min_x, int max_x);

		void ResetSpans();

		int num_spans();

		ScanlineSpan begin();

		ScanlineSpan GetNextScanlineSpan();

		int y();

		byte[] GetCovers();

		void add_cell(int x, int cover);

		void add_span(int x, int len, int cover);
	};

	/// <summary>
	/// C++ AGG's scanline generator concept: anything that hands out its shape one scanline at a time - a
	/// rasterizer, or a scanline storage replaying what was rendered into it.
	/// </summary>
	public interface IScanlineGenerator
	{
		/// <summary>Starts the sweep over; false when there is nothing to sweep.</summary>
		bool rewind_scanlines();

		int min_x();

		int min_y();

		int max_x();

		int max_y();

		/// <summary>Fills <paramref name="sl"/> with the next non-empty scanline; false when there are no more.</summary>
		bool sweep_scanline(IScanlineCache sl);
	}
}
