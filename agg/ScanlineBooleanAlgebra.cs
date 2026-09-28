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
using MatterHackers.Agg.Image;

namespace MatterHackers.Agg
{
	/// <summary>C++ AGG's sbool_op_e, in its order (the scanline_boolean demo casts its radio box item to it).</summary>
	public enum SboolOp
	{
		Or,
		And,
		Xor,
		XorSaddle,
		XorAbsDiff,
		AMinusB,
		BMinusA,
	}

	/// <summary>
	/// Where a boolean operation sends its result: C++'s Renderer template argument (a scanline renderer, or a
	/// scanline storage), called once before the first scanline and once per non-empty result scanline.
	/// </summary>
	public interface IScanlineSink
	{
		void prepare();

		void render(IScanlineCache scanline);
	}

	/// <summary>
	/// C++ renderer_scanline_aa_solid as an <see cref="IScanlineSink"/>: blends each scanline into an image in
	/// one color, its covers as coverage.
	/// </summary>
	public class SolidScanlineSink : ScanlineRenderer, IScanlineSink
	{
		private readonly IImageByte destination;

		public SolidScanlineSink(IImageByte destination, Color color)
		{
			this.destination = destination;
			this.Color = color;
		}

		public Color Color { get; set; }

		public void prepare()
		{
		}

		public void render(IScanlineCache scanline)
		{
			this.RenderSolidSingleScanLine(this.destination, scanline, this.Color);
		}
	}

	/// <summary>
	/// C++ renderer_scanline_bin_solid as an <see cref="IScanlineSink"/>: fills every span of each scanline in
	/// one color at full cover, whatever covers the scanline holds.
	/// </summary>
	public class BinSolidScanlineSink : IScanlineSink
	{
		private readonly IImageByte destination;

		public BinSolidScanlineSink(IImageByte destination, Color color)
		{
			this.destination = destination;
			this.Color = color;
		}

		public Color Color { get; set; }

		public void prepare()
		{
		}

		public void render(IScanlineCache scanline)
		{
			ScanlineSpan span = scanline.begin();
			for (int i = 0; i < scanline.num_spans(); i++)
			{
				if (i > 0)
				{
					span = scanline.GetNextScanlineSpan();
				}

				this.destination.blend_hline(span.x, scanline.y(), span.x - 1 + Math.Abs(span.len), this.Color, 255);
			}
		}
	}

	/// <summary>
	/// C++ AGG's agg_scanline_boolean_algebra.h: union, intersection, xor and difference of two shapes, done
	/// scanline by scanline on the cover values of two scanline generators.
	/// </summary>
	/// <remarks>
	/// A span with a negative len is solid: one cover for its whole length at its cover_index. C++ adds whole
	/// runs of AA covers with add_cells; <see cref="IScanlineCache"/> has no add_cells, so they go in one
	/// add_cell at a time, which builds the same spans.
	/// <para>
	/// The generators are rasterizers or scanline storages (<see cref="ScanlineStorageAa8"/>,
	/// <see cref="ScanlineStorageBin"/>), which are sinks too, so a result can be stored and combined again.
	/// </para>
	/// </remarks>
	public static class ScanlineBooleanAlgebra
	{
		private const int CoverShift = 8;
		private const int CoverMask = (1 << CoverShift) - 1;
		private const int CoverFull = CoverMask;

		private delegate void AddSpan(IScanlineCache source, ScanlineSpan span, int x, int len, IScanlineCache result);

		private delegate void CombineSpans(IScanlineCache sl1, ScanlineSpan span1, IScanlineCache sl2, ScanlineSpan span2, int x, int len, IScanlineCache result);

		/// <summary>C++ sbool_combine_shapes_aa: <paramref name="op"/> of the two generators' anti-aliased shapes.</summary>
		public static void CombineShapesAa(SboolOp op, IScanlineGenerator sg1, IScanlineGenerator sg2, IScanlineCache sl1, IScanlineCache sl2, IScanlineCache sl, IScanlineSink ren)
		{
			switch (op)
			{
				case SboolOp.Or: UniteShapes(sg1, sg2, sl1, sl2, sl, ren, AddSpanAa, AddSpanAa, UniteSpansAa); break;
				case SboolOp.And: IntersectShapes(sg1, sg2, sl1, sl2, sl, ren, IntersectSpansAa); break;
				case SboolOp.Xor: UniteShapes(sg1, sg2, sl1, sl2, sl, ren, AddSpanAa, AddSpanAa, XorSpansAa(XorLinear)); break;
				case SboolOp.XorSaddle: UniteShapes(sg1, sg2, sl1, sl2, sl, ren, AddSpanAa, AddSpanAa, XorSpansAa(XorSaddle)); break;
				case SboolOp.XorAbsDiff: UniteShapes(sg1, sg2, sl1, sl2, sl, ren, AddSpanAa, AddSpanAa, XorSpansAa(XorAbsDiff)); break;
				case SboolOp.AMinusB: SubtractShapes(sg1, sg2, sl1, sl2, sl, ren, AddSpanAa, SubtractSpansAa); break;
				case SboolOp.BMinusA: SubtractShapes(sg2, sg1, sl2, sl1, sl, ren, AddSpanAa, SubtractSpansAa); break;
			}
		}

		/// <summary>C++ sbool_combine_shapes_bin: <paramref name="op"/> of the two shapes, every cover full (all xors are one).</summary>
		public static void CombineShapesBin(SboolOp op, IScanlineGenerator sg1, IScanlineGenerator sg2, IScanlineCache sl1, IScanlineCache sl2, IScanlineCache sl, IScanlineSink ren)
		{
			switch (op)
			{
				case SboolOp.Or: UniteShapes(sg1, sg2, sl1, sl2, sl, ren, AddSpanBin, AddSpanBin, CombineSpansBin); break;
				case SboolOp.And: IntersectShapes(sg1, sg2, sl1, sl2, sl, ren, CombineSpansBin); break;
				case SboolOp.Xor:
				case SboolOp.XorSaddle:
				case SboolOp.XorAbsDiff: UniteShapes(sg1, sg2, sl1, sl2, sl, ren, AddSpanBin, AddSpanBin, CombineSpansEmpty); break;
				case SboolOp.AMinusB: SubtractShapes(sg1, sg2, sl1, sl2, sl, ren, AddSpanBin, CombineSpansEmpty); break;
				case SboolOp.BMinusA: SubtractShapes(sg2, sg1, sl2, sl1, sl, ren, AddSpanBin, CombineSpansEmpty); break;
			}
		}

		/// <summary>C++ sbool_xor_formula_linear.</summary>
		public static int XorLinear(int a, int b)
		{
			int cover = a + b;
			if (cover > CoverMask)
			{
				cover = CoverMask + CoverMask - cover;
			}

			return cover;
		}

		/// <summary>C++ sbool_xor_formula_saddle.</summary>
		public static int XorSaddle(int a, int b)
		{
			int k = a * b;
			if (k == CoverMask * CoverMask)
			{
				return 0;
			}

			a = ((CoverMask * CoverMask) - (a << CoverShift) + k) >> CoverShift;
			b = ((CoverMask * CoverMask) - (b << CoverShift) + k) >> CoverShift;
			return CoverMask - ((a * b) >> CoverShift);
		}

		/// <summary>C++ sbool_xor_formula_abs_diff.</summary>
		public static int XorAbsDiff(int a, int b) => Math.Abs(a - b);

		// A product of two covers back to one cover: full stays full, anything else drops the low byte.
		private static int ScaleProduct(int cover) => cover == CoverFull * CoverFull ? CoverFull : cover >> CoverShift;

		private static int Cover(IScanlineCache sl, ScanlineSpan span, int x)
		{
			// A solid span keeps one cover; an AA span has one per pixel from span.x.
			return span.len < 0 ? sl.GetCovers()[span.cover_index] : sl.GetCovers()[span.cover_index + (x - span.x)];
		}

		private static void CombineSpansBin(IScanlineCache sl1, ScanlineSpan span1, IScanlineCache sl2, ScanlineSpan span2, int x, int len, IScanlineCache result)
		{
			result.add_span(x, len, CoverFull);
		}

		private static void CombineSpansEmpty(IScanlineCache sl1, ScanlineSpan span1, IScanlineCache sl2, ScanlineSpan span2, int x, int len, IScanlineCache result)
		{
		}

		private static void AddSpanEmpty(IScanlineCache source, ScanlineSpan span, int x, int len, IScanlineCache result)
		{
		}

		private static void AddSpanBin(IScanlineCache source, ScanlineSpan span, int x, int len, IScanlineCache result)
		{
			result.add_span(x, len, CoverFull);
		}

		private static void AddSpanAa(IScanlineCache source, ScanlineSpan span, int x, int len, IScanlineCache result)
		{
			if (span.len < 0)
			{
				result.add_span(x, len, source.GetCovers()[span.cover_index]);
			}
			else if (span.len > 0)
			{
				for (int i = 0; i < len; i++)
				{
					result.add_cell(x + i, Cover(source, span, x + i));
				}
			}
		}

		// The C++ combine functors switch on which of the two spans are solid. Per pixel each applies its formula
		// to the two covers and two solid spans give one solid span; the cases are folded together here where
		// that gives the same spans and covers, and kept apart where C++ special-cases a full solid cover.
		private static void IntersectSpansAa(IScanlineCache sl1, ScanlineSpan span1, IScanlineCache sl2, ScanlineSpan span2, int x, int len, IScanlineCache result)
		{
			bool solid1 = span1.len < 0;
			bool solid2 = span2.len < 0;
			if (solid1 && solid2)
			{
				result.add_span(x, len, ScaleProduct(Cover(sl1, span1, x) * Cover(sl2, span2, x)));
				return;
			}

			// A full solid span passes the other span's covers through untouched (C++ add_cells) - the formula
			// would take one off each partial cover.
			if (solid1 && Cover(sl1, span1, x) == CoverFull)
			{
				AddSpanAa(sl2, span2, x, len, result);
				return;
			}

			if (solid2 && Cover(sl2, span2, x) == CoverFull)
			{
				AddSpanAa(sl1, span1, x, len, result);
				return;
			}

			for (int end = x + len; x < end; x++)
			{
				result.add_cell(x, ScaleProduct(Cover(sl1, span1, x) * Cover(sl2, span2, x)));
			}
		}

		private static void UniteSpansAa(IScanlineCache sl1, ScanlineSpan span1, IScanlineCache sl2, ScanlineSpan span2, int x, int len, IScanlineCache result)
		{
			bool solid1 = span1.len < 0;
			bool solid2 = span2.len < 0;
			if ((solid1 && solid2)
				|| (solid1 && Cover(sl1, span1, x) == CoverFull)
				|| (solid2 && Cover(sl2, span2, x) == CoverFull))
			{
				// C++ adds a full solid span when either side is solid and full, and the formula's solid span when both are solid.
				result.add_span(x, len, ScaleProduct((CoverMask * CoverMask) - ((CoverMask - Cover(sl1, span1, x)) * (CoverMask - Cover(sl2, span2, x)))));
				return;
			}

			for (int end = x + len; x < end; x++)
			{
				int cover = (CoverMask * CoverMask) - ((CoverMask - Cover(sl1, span1, x)) * (CoverMask - Cover(sl2, span2, x)));
				result.add_cell(x, ScaleProduct(cover));
			}
		}

		private static CombineSpans XorSpansAa(Func<int, int, int> formula)
		{
			return (sl1, span1, sl2, span2, x, len, result) =>
			{
				if (span1.len < 0 && span2.len < 0)
				{
					int solid = formula(Cover(sl1, span1, x), Cover(sl2, span2, x));
					if (solid != 0)
					{
						result.add_span(x, len, solid);
					}

					return;
				}

				for (int end = x + len; x < end; x++)
				{
					int cover = formula(Cover(sl1, span1, x), Cover(sl2, span2, x));
					if (cover != 0)
					{
						result.add_cell(x, cover);
					}
				}
			};
		}

		private static void SubtractSpansAa(IScanlineCache sl1, ScanlineSpan span1, IScanlineCache sl2, ScanlineSpan span2, int x, int len, IScanlineCache result)
		{
			if (span1.len < 0 && span2.len < 0)
			{
				int solid = Cover(sl1, span1, x) * (CoverMask - Cover(sl2, span2, x));
				if (solid != 0)
				{
					result.add_span(x, len, ScaleProduct(solid));
				}

				return;
			}

			for (int end = x + len; x < end; x++)
			{
				int cover = Cover(sl1, span1, x) * (CoverMask - Cover(sl2, span2, x));
				if (cover != 0)
				{
					result.add_cell(x, ScaleProduct(cover));
				}
			}
		}

		private static void AddSpansAndRender(IScanlineCache sl1, IScanlineCache sl, IScanlineSink ren, AddSpan addSpan)
		{
			sl.ResetSpans();
			ScanlineSpan span = sl1.begin();
			int numSpans = sl1.num_spans();
			for (; ; )
			{
				addSpan(sl1, span, span.x, Math.Abs(span.len), sl);
				if (--numSpans == 0)
				{
					break;
				}

				span = sl1.GetNextScanlineSpan();
			}

			sl.finalize(sl1.y());
			ren.render(sl);
		}

		private static void IntersectScanlines(IScanlineCache sl1, IScanlineCache sl2, IScanlineCache sl, CombineSpans combineSpans)
		{
			sl.ResetSpans();
			int num1 = sl1.num_spans();
			if (num1 == 0)
			{
				return;
			}

			int num2 = sl2.num_spans();
			if (num2 == 0)
			{
				return;
			}

			ScanlineSpan span1 = sl1.begin();
			ScanlineSpan span2 = sl2.begin();
			while (num1 != 0 && num2 != 0)
			{
				int xb1 = span1.x;
				int xb2 = span2.x;
				int xe1 = xb1 + Math.Abs(span1.len) - 1;
				int xe2 = xb2 + Math.Abs(span2.len) - 1;

				bool advanceSpan1 = xe1 < xe2;
				bool advanceBoth = xe1 == xe2;

				if (xb1 < xb2)
				{
					xb1 = xb2;
				}

				if (xe1 > xe2)
				{
					xe1 = xe2;
				}

				if (xb1 <= xe1)
				{
					combineSpans(sl1, span1, sl2, span2, xb1, xe1 - xb1 + 1, sl);
				}

				if (advanceBoth)
				{
					--num1;
					--num2;
					if (num1 != 0)
					{
						span1 = sl1.GetNextScanlineSpan();
					}

					if (num2 != 0)
					{
						span2 = sl2.GetNextScanlineSpan();
					}
				}
				else if (advanceSpan1)
				{
					--num1;
					if (num1 != 0)
					{
						span1 = sl1.GetNextScanlineSpan();
					}
				}
				else
				{
					--num2;
					if (num2 != 0)
					{
						span2 = sl2.GetNextScanlineSpan();
					}
				}
			}
		}

		private static void IntersectShapes(IScanlineGenerator sg1, IScanlineGenerator sg2, IScanlineCache sl1, IScanlineCache sl2, IScanlineCache sl, IScanlineSink ren, CombineSpans combineSpans)
		{
			if (!sg1.rewind_scanlines() || !sg2.rewind_scanlines())
			{
				return;
			}

			int x1 = Math.Max(sg1.min_x(), sg2.min_x());
			int y1 = Math.Max(sg1.min_y(), sg2.min_y());
			int x2 = Math.Min(sg1.max_x(), sg2.max_x());
			int y2 = Math.Min(sg1.max_y(), sg2.max_y());
			if (x1 > x2 || y1 > y2)
			{
				return;
			}

			sl.reset(x1, x2);
			sl1.reset(sg1.min_x(), sg1.max_x());
			sl2.reset(sg2.min_x(), sg2.max_x());
			if (!sg1.sweep_scanline(sl1) || !sg2.sweep_scanline(sl2))
			{
				return;
			}

			ren.prepare();

			for (; ; )
			{
				while (sl1.y() < sl2.y())
				{
					if (!sg1.sweep_scanline(sl1))
					{
						return;
					}
				}

				while (sl2.y() < sl1.y())
				{
					if (!sg2.sweep_scanline(sl2))
					{
						return;
					}
				}

				if (sl1.y() == sl2.y())
				{
					IntersectScanlines(sl1, sl2, sl, combineSpans);
					if (sl.num_spans() != 0)
					{
						sl.finalize(sl1.y());
						ren.render(sl);
					}

					if (!sg1.sweep_scanline(sl1) || !sg2.sweep_scanline(sl2))
					{
						return;
					}
				}
			}
		}

		private static void UniteScanlines(IScanlineCache sl1, IScanlineCache sl2, IScanlineCache sl, AddSpan addSpan1, AddSpan addSpan2, CombineSpans combineSpans)
		{
			sl.ResetSpans();

			int num1 = sl1.num_spans();
			int num2 = sl2.num_spans();

			ScanlineSpan span1 = default;
			ScanlineSpan span2 = default;

			// An invalidated span has begin > end, so it is past and the next one is read.
			const int InvalidB = 0xFFFFFFF;
			const int InvalidE = InvalidB - 1;

			int xb1 = InvalidB;
			int xb2 = InvalidB;
			int xe1 = InvalidE;
			int xe2 = InvalidE;

			if (num1 != 0)
			{
				span1 = sl1.begin();
				xb1 = span1.x;
				xe1 = xb1 + Math.Abs(span1.len) - 1;
				--num1;
			}

			if (num2 != 0)
			{
				span2 = sl2.begin();
				xb2 = span2.x;
				xe2 = xb2 + Math.Abs(span2.len) - 1;
				--num2;
			}

			for (; ; )
			{
				if (num1 != 0 && xb1 > xe1)
				{
					--num1;
					span1 = sl1.GetNextScanlineSpan();
					xb1 = span1.x;
					xe1 = xb1 + Math.Abs(span1.len) - 1;
				}

				if (num2 != 0 && xb2 > xe2)
				{
					--num2;
					span2 = sl2.GetNextScanlineSpan();
					xb2 = span2.x;
					xe2 = xb2 + Math.Abs(span2.len) - 1;
				}

				if (xb1 > xe1 && xb2 > xe2)
				{
					break;
				}

				int xb = Math.Max(xb1, xb2);
				int xe = Math.Min(xe1, xe2);
				int len = xe - xb + 1;
				if (len > 0)
				{
					if (xb1 < xb2)
					{
						addSpan1(sl1, span1, xb1, xb2 - xb1, sl);
						xb1 = xb2;
					}
					else if (xb2 < xb1)
					{
						addSpan2(sl2, span2, xb2, xb1 - xb2, sl);
						xb2 = xb1;
					}

					combineSpans(sl1, span1, sl2, span2, xb, len, sl);

					if (xe1 < xe2)
					{
						xb1 = InvalidB;
						xe1 = InvalidE;
						xb2 += len;
					}
					else if (xe2 < xe1)
					{
						xb2 = InvalidB;
						xe2 = InvalidE;
						xb1 += len;
					}
					else
					{
						xb1 = InvalidB;
						xb2 = InvalidB;
						xe1 = InvalidE;
						xe2 = InvalidE;
					}
				}
				else if (xb1 < xb2)
				{
					if (xb1 <= xe1)
					{
						addSpan1(sl1, span1, xb1, xe1 - xb1 + 1, sl);
					}

					xb1 = InvalidB;
					xe1 = InvalidE;
				}
				else
				{
					if (xb2 <= xe2)
					{
						addSpan2(sl2, span2, xb2, xe2 - xb2 + 1, sl);
					}

					xb2 = InvalidB;
					xe2 = InvalidE;
				}
			}
		}

		private static void UniteShapes(IScanlineGenerator sg1, IScanlineGenerator sg2, IScanlineCache sl1, IScanlineCache sl2, IScanlineCache sl, IScanlineSink ren, AddSpan addSpan1, AddSpan addSpan2, CombineSpans combineSpans)
		{
			bool flag1 = sg1.rewind_scanlines();
			bool flag2 = sg2.rewind_scanlines();
			if (!flag1 && !flag2)
			{
				return;
			}

			int x1, x2;
			if (flag1 && flag2)
			{
				x1 = Math.Min(sg1.min_x(), sg2.min_x());
				x2 = Math.Max(sg1.max_x(), sg2.max_x());
			}
			else if (flag1)
			{
				x1 = sg1.min_x();
				x2 = sg1.max_x();
			}
			else
			{
				x1 = sg2.min_x();
				x2 = sg2.max_x();
			}

			// A generator with cells has a valid box, so the union is valid too.
			ren.prepare();
			sl.reset(x1, x2);
			if (flag1)
			{
				sl1.reset(sg1.min_x(), sg1.max_x());
				flag1 = sg1.sweep_scanline(sl1);
			}

			if (flag2)
			{
				sl2.reset(sg2.min_x(), sg2.max_x());
				flag2 = sg2.sweep_scanline(sl2);
			}

			while (flag1 || flag2)
			{
				if (flag1 && flag2)
				{
					if (sl1.y() == sl2.y())
					{
						UniteScanlines(sl1, sl2, sl, addSpan1, addSpan2, combineSpans);
						if (sl.num_spans() != 0)
						{
							sl.finalize(sl1.y());
							ren.render(sl);
						}

						flag1 = sg1.sweep_scanline(sl1);
						flag2 = sg2.sweep_scanline(sl2);
					}
					else if (sl1.y() < sl2.y())
					{
						AddSpansAndRender(sl1, sl, ren, addSpan1);
						flag1 = sg1.sweep_scanline(sl1);
					}
					else
					{
						AddSpansAndRender(sl2, sl, ren, addSpan2);
						flag2 = sg2.sweep_scanline(sl2);
					}
				}
				else
				{
					if (flag1)
					{
						AddSpansAndRender(sl1, sl, ren, addSpan1);
						flag1 = sg1.sweep_scanline(sl1);
					}

					if (flag2)
					{
						AddSpansAndRender(sl2, sl, ren, addSpan2);
						flag2 = sg2.sweep_scanline(sl2);
					}
				}
			}
		}

		private static void SubtractShapes(IScanlineGenerator sg1, IScanlineGenerator sg2, IScanlineCache sl1, IScanlineCache sl2, IScanlineCache sl, IScanlineSink ren, AddSpan addSpan1, CombineSpans combineSpans)
		{
			if (!sg1.rewind_scanlines())
			{
				return;
			}

			bool flag2 = sg2.rewind_scanlines();

			sl.reset(sg1.min_x(), sg1.max_x());
			sl1.reset(sg1.min_x(), sg1.max_x());
			sl2.reset(sg2.min_x(), sg2.max_x());
			if (!sg1.sweep_scanline(sl1))
			{
				return;
			}

			if (flag2)
			{
				flag2 = sg2.sweep_scanline(sl2);
			}

			ren.prepare();

			bool flag1;
			do
			{
				while (flag2 && sl2.y() < sl1.y())
				{
					flag2 = sg2.sweep_scanline(sl2);
				}

				if (flag2 && sl2.y() == sl1.y())
				{
					UniteScanlines(sl1, sl2, sl, addSpan1, AddSpanEmpty, combineSpans);
					if (sl.num_spans() != 0)
					{
						sl.finalize(sl1.y());
						ren.render(sl);
					}
				}
				else
				{
					AddSpansAndRender(sl1, sl, ren, addSpan1);
				}

				flag1 = sg1.sweep_scanline(sl1);
			}
			while (flag1);
		}
	}
}
