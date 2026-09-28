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
// class gamma_ctrl
//
//----------------------------------------------------------------------------

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// The GuiWidget face of C++ AGG's <c>gamma_ctrl</c>: a thin adapter over <see cref="GammaCtrl"/>, laid out
	/// in the widget's local coordinates. It draws the control's background and border itself, then hands
	/// itself to the renderer once per path for the curve, grid and both points; the four values are a
	/// child <see cref="TextWidget"/> rather than the control's stroked gsv_text.
	/// </summary>
	public class gamma_ctrl : SimpleVertexSourceWidget
	{
		// Widget path i is control path i + 2: curve, grid, inactive point, active point, text.
		private const int FirstControlPath = 2;

		private readonly GammaCtrl ctrl;
		private readonly gamma_spline spline;
		private readonly TextWidget gammaText;
		private IEnumerator<VertexData> currentPath;

		public gamma_ctrl(Vector2 position, Vector2 size)
			: base(position, false)
		{
			LocalBounds = new RectangleDouble(0, 0, size.X, size.Y);
			this.ctrl = new GammaCtrl(0, 0, size.X, size.Y);
			this.spline = new gamma_spline(this.ctrl.Spline);

			gammaText = new TextWidget("", pointSize: 11)
			{
				VAnchor = VAnchor.Top,
				HAnchor = HAnchor.Stretch,
				Margin = new BorderDouble(8, 4)
			};
			this.AddChild(gammaText);
		}

		/// <summary>The control this widget adapts.</summary>
		public GammaCtrl Ctrl => this.ctrl;

		// Set colors
		public void background_color(Color c) => this.ctrl.BackgroundColor = c;

		public void border_color(Color c) => this.ctrl.BorderColor = c;

		public void curve_color(Color c) => this.ctrl.CurveColor = c;

		public void grid_color(Color c) => this.ctrl.GridColor = c;

		public void inactive_pnt_color(Color c) => this.ctrl.InactivePointColor = c;

		public void active_pnt_color(Color c) => this.ctrl.ActivePointColor = c;

		public void text_color(Color c) => this.ctrl.TextColor = c;

		/// <summary>
		/// The color of path <paramref name="i"/>, in the order OnDraw walks the paths: curve, grid, inactive
		/// point, active point, text.
		/// </summary>
		public override IColorType color(int i)
		{
			if (i < 0 || i > 4)
			{
				throw new System.IndexOutOfRangeException("You asked for a color out of range.");
			}

			return this.ctrl.PathColor(i + FirstControlPath);
		}

		// Set other parameters
		public void border_width(double t) => this.ctrl.SetBorderWidth(t);

		public void border_width(double t, double extra) => this.ctrl.SetBorderWidth(t, extra);

		public void curve_width(double t) => this.ctrl.CurveWidth = t;

		public void grid_width(double t) => this.ctrl.GridWidth = t;

		public void text_thickness(double t) => this.ctrl.TextThickness = t;

		public void text_size(double h) => this.ctrl.SetTextSize(h);

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

		public void change_active_point() => this.ctrl.ChangeActivePoint();

		// A copy of agg::gamma_spline interface
		public void values(double kx1, double ky1, double kx2, double ky2) => this.ctrl.SetValues(kx1, ky1, kx2, ky2);

		public void values(out double kx1, out double ky1, out double kx2, out double ky2) => this.ctrl.GetValues(out kx1, out ky1, out kx2, out ky2);

		public byte[] gamma() => this.ctrl.Gamma;

		public double y(double x) => this.ctrl.Y(x);

		public gamma_spline get_gamma_spline() => this.spline;

		// Vertex source interface
		public override int num_paths() => 5;

		public override void OnDraw(Graphics2D graphics2D)
		{
			// The control's own background and border paths.
			graphics2D.Render(this.ctrl.PathSource(0), this.ctrl.PathColor(0));
			graphics2D.Render(this.ctrl.PathSource(1), this.ctrl.PathColor(1));

			UpdateGammaText();

			// The curve, grid and handles are drawn by base.OnDraw - it is the only place that can pick a
			// path, so drawing them here as well would just paint path 0 over and over (see
			// SimpleVertexSourceWidget.OnDraw).
			base.OnDraw(graphics2D);
		}

		/// <summary>
		/// Selects widget path <paramref name="idx"/>: 0 curve, 1 grid, 2 inactive point, 3 active point. Path 4
		/// (the control's text) is empty; <see cref="gammaText"/> shows the values instead.
		/// </summary>
		public override void Rewind(int idx)
		{
			this.currentPath = idx >= 0 && idx < 4
				? this.ctrl.PathSource(idx + FirstControlPath).Vertices().GetEnumerator()
				: null;
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

		private void UpdateGammaText()
		{
			this.ctrl.GetValues(out double kx1, out double ky1, out double kx2, out double ky2);

			// Setter already optimizes for change
			gammaText.Text = string.Format("{0:F3} {1:F3} {2:F3} {3:F3}", kx1, ky1, kx2, ky2);
		}
	}
}
