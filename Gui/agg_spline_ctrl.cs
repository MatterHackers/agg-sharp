using System.Collections.Generic;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;

//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.4
// Copyright (C) 2002-2005 Maxim Shemanarev (http://www.antigrain.com)
//
// C# port by: Lars Brubaker
//                  larsbrubaker@gmail.com
// Copyright (C) 2007-2026
//
// Permission to copy, use, modify, sell and distribute this software
// is granted provided this copyright notice appears in all copies.
// This software is provided "as is" without express or implied
// warranty, and with no claim as to its suitability for any purpose.
//
//----------------------------------------------------------------------------
// Contact: mcseem@antigrain.com
//          mcseemagg@yahoo.com
//          http://www.antigrain.com
//----------------------------------------------------------------------------
//
// classes spline_ctrl_impl, spline_ctrl
//
//----------------------------------------------------------------------------

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// The GuiWidget face of C++ AGG's <c>spline_ctrl</c>: a thin adapter over <see cref="SplineCtrl"/>, laid out
	/// in the widget's local coordinates. It hands itself to the renderer once per path (background, border,
	/// curve, inactive points, active point) through <see cref="SimpleVertexSourceWidget"/>.
	/// </summary>
	public class spline_ctrl : SimpleVertexSourceWidget
	{
		private readonly SplineCtrl ctrl;
		private IEnumerator<VertexData> currentPath;

		public spline_ctrl(Vector2 location, Vector2 size, int num_pnt)
			: base(location, false)
		{
			LocalBounds = new RectangleDouble(0, 0, size.X, size.Y);
			this.ctrl = new SplineCtrl(0, 0, size.X, size.Y, num_pnt);
		}

		/// <summary>The control this widget adapts.</summary>
		public SplineCtrl Ctrl => this.ctrl;

		// Set other parameters
		public void border_width(double t) => border_width(t, 0);

		public void border_width(double t, double extra)
		{
			this.ctrl.SetBorderWidth(t, extra);

			// The background reaches extra past the control's box; grow the widget so it is not clipped.
			LocalBounds = new RectangleDouble(-extra, -extra, this.ctrl.X2 + extra, this.ctrl.Y2 + extra);
		}

		public void curve_width(double t) => this.ctrl.CurveWidth = t;

		public void point_size(double s) => this.ctrl.PointSize = s;

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			if (this.ctrl.OnMouseButtonDown(mouseEvent.X, mouseEvent.Y))
			{
				Invalidate();
			}

			base.OnMouseDown(mouseEvent);
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			this.ctrl.OnMouseButtonUp(mouseEvent.X, mouseEvent.Y);
			base.OnMouseUp(mouseEvent);
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			// Only a point grabbed by OnMouseDown moves, and the grab ends on mouse up, so the button state
			// C++ checks here is already implied.
			if (this.ctrl.OnMouseMove(mouseEvent.X, mouseEvent.Y, true))
			{
				Invalidate();
			}

			base.OnMouseMove(mouseEvent);
		}

		public override void OnKeyDown(KeyEventArgs keyEvent)
		{
			// this must be called first to ensure we get the correct Handled state
			base.OnKeyDown(keyEvent);

			if (!keyEvent.Handled
				&& this.ctrl.OnArrowKeys(keyEvent.KeyCode == Keys.Left, keyEvent.KeyCode == Keys.Right, keyEvent.KeyCode == Keys.Down, keyEvent.KeyCode == Keys.Up))
			{
				keyEvent.Handled = true;
				Invalidate();
			}
		}

		public void active_point(int i) => this.ctrl.ActivePoint = i;

		public double[] spline() => this.ctrl.Spline;

		public byte[] spline8() => this.ctrl.Spline8;

		public double value(double x) => this.ctrl.Value(x);

		public void value(int idx, double y) => this.ctrl.SetValue(idx, y);

		public void point(int idx, double x, double y) => this.ctrl.SetPoint(idx, x, y);

		public void x(int idx, double x) => this.ctrl.SetX(idx, x);

		public void y(int idx, double y) => this.ctrl.SetY(idx, y);

		public double x(int idx) => this.ctrl.GetX(idx);

		public double y(int idx) => this.ctrl.GetY(idx);

		public void update_spline() => this.ctrl.UpdateSpline();

		// There is deliberately no OnDraw override: SimpleVertexSourceWidget.OnDraw already draws all five
		// paths in the colors color(i) hands back, and it is the only place that can select a path.

		// Vertex source interface
		public override int num_paths() => 5;

		public override void Rewind(int idx)
		{
			// C++ rewind's switch falls through its default into the background.
			if (idx < 0 || idx > 4)
			{
				idx = 0;
			}

			this.currentPath = this.ctrl.PathSource(idx).Vertices().GetEnumerator();
		}

		public override FlagsAndCommand Vertex(out double x, out double y)
		{
			x = 0;
			y = 0;
			if (this.currentPath == null || !this.currentPath.MoveNext() || this.currentPath.Current.IsStop)
			{
				return FlagsAndCommand.Stop;
			}

			x = this.currentPath.Current.Position.X;
			y = this.currentPath.Current.Position.Y;
			return this.currentPath.Current.Command;
		}

		// Set colors
		public void background_color(Color c) => this.ctrl.BackgroundColor = c;

		public void border_color(Color c) => this.ctrl.BorderColor = c;

		public void curve_color(Color c) => this.ctrl.CurveColor = c;

		public void inactive_pnt_color(Color c) => this.ctrl.InactivePointColor = c;

		public void active_pnt_color(Color c) => this.ctrl.ActivePointColor = c;

		public override IColorType color(int i)
		{
			if (i < 0 || i > 4)
			{
				throw new System.IndexOutOfRangeException("You asked for a color out of range.");
			}

			return this.ctrl.PathColor(i);
		}
	}
}
