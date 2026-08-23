//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.4
// Copyright (C) 2002-2005 Maxim Shemanarev (http://www.antigrain.com)
//
// C# port by: Lars Brubaker
//                  larsbrubaker@gmail.com
// Copyright (C) 2026 Lars Brubaker
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
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.LcdCoverage;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;
using static System.Math;
using static MatterHackers.Agg.Color;

namespace MatterHackers.Agg.UI
{
	public class LayoutLock : IDisposable
	{
		private readonly GuiWidget item;

		public LayoutLock(GuiWidget item)
		{
			this.item = item;
			item.LayoutLockCount++;
		}

		public void Dispose()
		{
			item.LayoutLockCount--;
		}
	}

	[Flags]
	public enum SizeLimitsToSet
	{
		None = 0,
		Minimum = 1,
		Maximum = 2
	}

	[Flags]
	/// <summary>
	/// Sets Horizontal alignment used for a widget, respecting widget margin and parent padding.
	/// </summary>
	public enum HAnchor
	{
		/// <summary>
		/// The widget will not change width automatically and will be positions at the OriginRelative to parent in x.
		/// </summary>
		Absolute = 0,

		/// <summary>
		/// Hold the widget to the parents left edge, respecting widget margin and parent padding.
		/// </summary>
		Left = 1,
		Center = 2,
		Right = 4,

		/// <summary>
		/// Maintain a size that horizontally encloses all of its visible children.
		/// </summary>
		Fit = 8,

		/// <summary>
		/// Maintain a width that is the same width as its parent.
		/// </summary>
		Stretch = Left | Right,

		/// <summary>
		/// Take the larger of Fit or Stretch.
		/// </summary>
		MaxFitOrStretch = Fit | Stretch,

		/// <summary>
		/// Take the lesser of the Fit or Stretch calculation
		/// </summary>
		MinFitOrStretch = 16,
	}

	/// <summary>
	/// Sets Vertical alignment used for a widget, respecting widget margin and parent padding.
	/// </summary>
	[Flags]
	public enum VAnchor
	{
		Absolute = 0,
		Bottom = 1,
		Center = 2,
		Top = 4,

		/// <summary>
		/// Maintain a size that vertically encloses all of its visible children.
		/// </summary>
		Fit = 8,
		Stretch = Bottom | Top,

		/// <summary>
		/// Take the larger of FitToChildren or Stretch.
		/// </summary>
		MaxFitOrStretch = Fit | Stretch,

		/// <summary>
		/// Take the lesser of the Fit or Stretch calculation
		/// </summary>
		MinFitOrStretch = 16,
	}

	public enum Cursors
	{
		Arrow,
		Cross,
		Default,
		Hand,
		Help,
		HSplit,
		IBeam,
		No,
		NoMove2D,
		NoMoveHoriz,
		NoMoveVert,
		PanEast,
		PanNE,
		PanNorth,
		PanNW,
		PanSE,
		PanSouth,
		PanSW,
		PanWest,
		SizeAll,
		SizeNESW,
		SizeNS,
		SizeNWSE,
		SizeWE,
		UpArrow,
		VSplit,
		WaitCursor
	}

	public enum UnderMouseState
	{
		NotUnderMouse,
		UnderMouseNotFirst,
		FirstUnderMouse
	}

	/// <summary>
	/// The base of every widget. The double-buffering concern - the backbuffer itself, its modes and the
	/// raster and composite that use them - lives in <see cref="WidgetBackbuffer"/>, which this class owns
	/// while <see cref="DoubleBuffer"/> is on.
	/// </summary>
	public class GuiWidget : IAscendable<GuiWidget>, IEquatable<GuiWidget>
	{
		public static double DeviceScale { get; set; } = 1;

		private const double DumpIfLongerThanTime = 1;
		private static readonly bool DebugShowSize = false;

		private readonly ScreenClipping screenClipping;

		// this should probably some type of dirty rects with the current invalid set stored.
		private bool isCurrentlyInvalid = true;

		public static bool DebugBoundsUnderMouse = false;

		public bool HasBeenClosed { get; private set; }

		private bool debugShowBounds = false;

		public bool DebugShowBounds
		{
			get
			{
				if (DebugBoundsUnderMouse)
				{
					if (UnderMouseState != UI.UnderMouseState.NotUnderMouse)
					{
						return true;
					}
				}

				return debugShowBounds;
			}

			set
			{
				if (debugShowBounds != value)
				{
					debugShowBounds = value;
					Invalidate();
				}
			}
		}

		private bool drawOnTopOfSiblings = false;

		/// <summary>
		/// Draw this child after all of its siblings that do not have this set, so it paints above them.
		/// This exists for widgets that must keep their slot in the parent's Children list (layout engines
		/// such as flow layout assign position from list order) while still rendering on top, for example a
		/// tab being dragged to a new position. It changes only render order, not child order or hit testing.
		/// </summary>
		public bool DrawOnTopOfSiblings
		{
			get => drawOnTopOfSiblings;

			set
			{
				if (drawOnTopOfSiblings != value)
				{
					drawOnTopOfSiblings = value;
					Invalidate();
				}
			}
		}

		private bool doubleBuffer;

		/// <summary>
		/// This widget's pixel cache, or null while <see cref="DoubleBuffer"/> is off - which is the great
		/// majority of widgets, and why it is built on demand rather than per widget.
		/// </summary>
		private WidgetBackbuffer backbuffer;

		/// <summary>
		/// Gets the backBuffer object for widgets that are double buffered.  It will return null if they are not.
		/// </summary>
		/// <remarks>
		/// Also null while the widget's pixels are in <see cref="BackbufferMode.LcdCoverage"/>: those live in
		/// two coverage planes, not in an <see cref="ImageBuffer"/>, and there is no lossless
		/// <see cref="ImageBuffer"/> to hand back. Returning the last RGBA buffer instead would serve pixels
		/// from whenever the widget was last painted the other way, which is worse than nothing. A caller that
		/// wants pixels regardless has to either keep LCD rendering off (the default) or collapse the planes
		/// itself through <see cref="LcdBuffer.ToImageBufferCollapsed"/>.
		/// </remarks>
		public ImageBuffer BackBuffer => this.backbuffer?.RgbaBuffer;

		public bool DoubleBuffer
		{
			get => doubleBuffer;
			set
			{
				if (this.DoubleBuffer != value)
				{
					doubleBuffer = value;
					if (doubleBuffer)
					{
						backbuffer = new WidgetBackbuffer(this);
						backbuffer.AllocateRgbaBuffer();
					}
					else
					{
						// Dropping the whole cache also drops the recorded mode with the pixels it describes:
						// keeping LcdCoverage would let a later paint that resolves the same mode skip the
						// re-raster and composite a buffer that is no longer there.
						backbuffer = null;
					}

					Invalidate();
				}
			}
		}

		private double backbufferOpacity = 1;

		/// <summary>
		/// How opaque this whole widget - itself and everything under it - is when its cached pixels are
		/// composited onto its parent. 1 (the default) is today's behaviour, byte for byte; 0 is invisible.
		/// </summary>
		/// <remarks>
		/// Only takes effect while <see cref="DoubleBuffer"/> is true, because the whole point is that the
		/// widget paints itself normally into its back buffer and then that <i>one</i> finished image is faded
		/// onto the parent. That is what makes an overlay window read as a single translucent pane rather than
		/// as a stack of individually see-through children, and it is why this is not the same thing as giving
		/// every child a transparent colour.
		/// </remarks>
		public double BackbufferOpacity
		{
			get => backbufferOpacity;

			set
			{
				double clamped = Max(0, Min(1, value));
				if (backbufferOpacity != clamped)
				{
					backbufferOpacity = clamped;
					Invalidate();
				}
			}
		}

		/// <summary>
		/// Which backbuffer representation this widget should be painted into, given the surface it will be
		/// composited onto. See <see cref="WidgetBackbuffer.ResolveMode"/> for the gates and why each one is
		/// separate.
		/// </summary>
		/// <param name="destination">The graphics the backbuffer will be composited onto, with the transform
		/// the composite will happen under already set.</param>
		public BackbufferMode ResolveBackbufferMode(Graphics2D destination)
		{
			// A faded widget has to be RGBA: LcdCoverage keeps its pixels as three per-channel coverages that
			// composite straight into the destination's own planes, and there is no single alpha in that
			// representation for a whole-widget opacity to scale. Subpixel coverage and alpha compositing are
			// alternatives, not layers.
			if (this.BackbufferOpacity < 1)
			{
				return BackbufferMode.Rgba;
			}

			return WidgetBackbuffer.ResolveMode(destination);
		}

		public LayoutEngine LayoutEngine { get; protected set; }

		public UnderMouseState UnderMouseState { get; private set; }

		public bool ContainsFirstUnderMouseRecursive()
		{
			if (UnderMouseState == UnderMouseState.FirstUnderMouse)
			{
				return true;
			}

			if (UnderMouseState == UnderMouseState.NotUnderMouse)
			{
				return false;
			}

			foreach (var child in Children)
			{
				if (child.ContainsFirstUnderMouseRecursive())
				{
					return true;
				}
			}

			return false;
		}

		public static bool DefaultEnforceIntegerBounds
		{
			get;
			set;
		}

		private bool enforceIntegerBounds = DefaultEnforceIntegerBounds;

		public bool EnforceIntegerBounds
		{
			get => enforceIntegerBounds;
			set => enforceIntegerBounds = value;
		}

		public bool FirstWidgetUnderMouse
		{
			get { return this.UnderMouseState == UnderMouseState.FirstUnderMouse; }
		}

		private RectangleDouble localBounds;

		private bool visible = true;
		private bool enabled = true;

		public bool Selectable { get; set; } = true;

		private enum MouseCapturedState
		{
			NotCaptured,
			ChildHasMouseCaptured,
			ThisHasMouseCaptured
		}

		private MouseCapturedState mouseCapturedState;

		public bool TabStop { get; set; }

		public virtual int TabIndex { get; set; }

		/// <summary>
		/// The radius to use on the corners of the background and Background Outline (if enabled).
		/// </summary>
		public RadiusCorners BackgroundRadius { get; set; } = default(RadiusCorners);

		/// <summary>
		/// Draw an outline around the background fill, this will use the OutlineColor and BackgroundRadius if set (in device units, scalled when rendered).
		/// </summary>
		public double BackgroundOutlineWidth { get; set; } = 0;

		private Color _backgroundColor = default(Color);

		public virtual Color BackgroundColor
		{
			get => _backgroundColor;
			set
			{
				if (_backgroundColor != value)
				{
					_backgroundColor = value;
					OnBackgroundColorChanged(null);
					Invalidate();
				}
			}
		}

		/// <summary>
		/// Constructor-safe initialization of BackgroundColor. Writes the backing field without
		/// virtual dispatch so derived overrides cannot run before their constructor body has
		/// executed. The change notification and Invalidate the BackgroundColor setter performs
		/// are no-ops for a widget still under construction (no subscribers, no parent, and the
		/// widget is created already invalid), so only the field write is replicated.
		/// </summary>
		protected void SetBackgroundColorWithoutDispatch(Color value)
		{
			_backgroundColor = value;
		}

		public event EventHandler BackgroundColorChanged;

		public virtual void OnBackgroundColorChanged(EventArgs e)
		{
			BackgroundColorChanged?.Invoke(this, e);
		}

		/// <summary>
		/// Gets the border and padding scaled by the DeviceScale
		/// </summary>
		public BorderDouble DevicePadding
		{
			get;
			private set;
		}

		/// <summary>
		/// Called when the padding has changed
		/// </summary>
		public event EventHandler PaddingChanged;

		private BorderDouble _padding;

		/// <summary>
		/// Gets or sets the space between the Widget and it's contents (the inside border).
		/// </summary>
		public virtual BorderDouble Padding
		{
			get => _padding;
			set
			{
				// using (new PerformanceTimer("Draw Timer", "On Layout"))
				{
					if (_padding != value)
					{
						_padding = value;
						DevicePadding = _padding * GuiWidget.DeviceScale;
						if (EnforceIntegerBounds)
						{
							DevicePadding.Round();
						}

						// the padding affects the children so make sure they are laid out
						OnLayout(new LayoutEventArgs(this, null));
						OnPaddingChanged();
					}
				}
			}
		}

		public virtual void OnPaddingChanged()
		{
			PaddingChanged?.Invoke(this, null);
		}

		private Color _borderColor = Color.Transparent;

		public virtual Color BorderColor
		{
			get => _borderColor;
			set
			{
				if (_borderColor != value)
				{
					_borderColor = value;
					OnBorderColorChanged(null);
					Invalidate();
				}
			}
		}

		public event EventHandler BorderColorChanged;

		public virtual void OnBorderColorChanged(EventArgs e)
		{
			BorderColorChanged?.Invoke(this, e);
		}

		public event EventHandler BorderChanged;

		private BorderDouble deviceBorder;
		private BorderDouble _border;

		/// <summary>
		/// Gets or sets the space between the Widget and its border. If BorderColor is set this will render as BorderColor and be rectangular.
		/// </summary>
		public BorderDouble Border
		{
			get => _border;
			set
			{
				// using (new PerformanceTimer("Draw Timer", "On Layout"))
				{
					if (_border != value)
					{
						_border = value;
						deviceBorder = _border * GuiWidget.DeviceScale;
						if (EnforceIntegerBounds)
						{
							deviceBorder.Round();
						}

						// the border affects the children so make sure they are laid out
						OnLayout(new LayoutEventArgs(this, null));
						OnBorderChanged();
					}
				}
			}
		}

		public virtual void OnBorderChanged()
		{
			BorderChanged?.Invoke(this, null);
		}

		public event EventHandler MarginChanged;

		private BorderDouble margin;

		public long LastMouseDownMs { get; private set; }

		/// <summary>
		/// The Clicks value of the mouse-down currently being processed or most recently pressed on
		/// this widget, cleared again when the press is released. Kept because the platform only
		/// reports Clicks == 2 on the second DOWN of a double click - the matching up arrives with
		/// Clicks == 1 - so an up-time <see cref="IsDoubleClick"/> query has to remember the down.
		/// </summary>
		private int lastMouseDownClicks;

		private BorderDouble deviceMargin;

		/// <summary>
		/// Gets the Margin scaled by the DeviceScale
		/// </summary>
		public BorderDouble DeviceMarginAndBorder
		{
			get { return deviceMargin + deviceBorder; }
		}

		/// <summary>
		/// Gets or sets the space between the Widget and it's parent (the outside border).
		/// </summary>
		public BorderDouble Margin
		{
			get => margin;
			set
			{
				if (margin != value)
				{
					margin = value;
					deviceMargin = Margin * GuiWidget.DeviceScale;

					if (EnforceIntegerBounds)
					{
						deviceMargin.Round();
					}

					this.Parent?.OnLayout(new LayoutEventArgs(this.Parent, this));
					OnLayout(new LayoutEventArgs(this, null));
					OnMarginChanged();
				}
			}
		}

		public virtual void OnMarginChanged()
		{
			MarginChanged?.Invoke(this, null);
		}

		/// <summary>
		/// Gets or sets the cursor that will be used when the mouse is over this control
		/// </summary>
		public virtual Cursors Cursor { get; set; }

		[Conditional("DEBUG")]
		public static void BreakInDebugger(string description = "")
		{
			Debug.WriteLine(description);
#if DEBUG && false
			Debugger.Break();
#endif
		}

		public bool HAnchorIsSet(HAnchor testFlags)
		{
			return (HAnchor & testFlags) == testFlags;
		}

		public bool HAnchorIsFloating
		{
			get
			{
				int numSet = 0;
				if (HAnchorIsSet(UI.HAnchor.Left))
				{
					numSet++;
				}

				if (HAnchorIsSet(UI.HAnchor.Center))
				{
					numSet++;
				}

				if (HAnchorIsSet(UI.HAnchor.Right))
				{
					numSet++;
				}

				return numSet == 1;
			}
		}

		private HAnchor hAnchor;

		public virtual HAnchor HAnchor
		{
			get => hAnchor;
			set
			{
				if (hAnchor != value)
				{
					if (value == (HAnchor.Left | HAnchor.Center | HAnchor.Right))
					{
						BreakInDebugger("You cannot be anchored to all three positions.");
					}

					if(value != HAnchor.MinFitOrStretch && value.HasFlag(HAnchor.MinFitOrStretch))
					{
						BreakInDebugger("You cannot have anything else set if you set MinFitOrStretch.");
					}
					hAnchor = value;
					this.Parent?.OnLayout(new LayoutEventArgs(this.Parent, this));

					if (HAnchorIsSet(HAnchor.Fit))
					{
						OnLayout(new LayoutEventArgs(this, null));
					}

					HAnchorChanged?.Invoke(this, null);
				}
			}
		}

		public bool VAnchorIsSet(VAnchor testFlags)
		{
			return (VAnchor & testFlags) == testFlags;
		}

		public bool VAnchorIsFloating
		{
			get
			{
				int numSet = 0;
				if (VAnchorIsSet(UI.VAnchor.Bottom))
				{
					numSet++;
				}

				if (VAnchorIsSet(UI.VAnchor.Center))
				{
					numSet++;
				}

				if (VAnchorIsSet(UI.VAnchor.Top))
				{
					numSet++;
				}

				return numSet == 1;
			}
		}

		private VAnchor vAnchor;

		public VAnchor VAnchor
		{
			get => vAnchor;
			set
			{
				if (vAnchor != value)
				{
					if (value == (VAnchor.Bottom | VAnchor.Center | VAnchor.Top))
					{
						BreakInDebugger("You cannot be anchored to all three positions.");
					}

					vAnchor = value;

					if (this.Visible)
					{
						this.Parent?.OnLayout(new LayoutEventArgs(this.Parent, this));

						if (VAnchorIsSet(VAnchor.Fit))
						{
							OnLayout(new LayoutEventArgs(this, null));
						}
					}

					VAnchorChanged?.Invoke(this, null);
				}
			}
		}

		public void AnchorAll()
		{
			VAnchor = VAnchor.Bottom | VAnchor.Top;
			HAnchor = HAnchor.Left | HAnchor.Right;
		}

		public void AnchorCenter()
		{
			VAnchor = VAnchor.Center;
			HAnchor = HAnchor.Center;
		}

		private Transform.Affine parentToChildTransform = Affine.NewIdentity();
		private bool containsFocus = false;

		internal int LayoutLockCount { get; set; }

		public LayoutLock LayoutLock()
		{
			return new LayoutLock(this);
		}

		public bool LayoutLocked
		{
			get
			{
				return LayoutLockCount > 0;
			}
		}

		public event EventHandler Layout;

		// the event args will be a DrawEventArgs
		public event EventHandler<DrawEventArgs> BeforeDraw;

		public event EventHandler<DrawEventArgs> AfterDraw;

		public event EventHandler<KeyPressEventArgs> KeyPressed;

		public event EventHandler Invalidated;

		public event EventHandler<KeyEventArgs> KeyDown;

		public event EventHandler<KeyEventArgs> KeyUp;

        public event EventHandler<object> ObjectSent;

        #region close events
        /// <summary>
		/// This is called when the user clicks the close button on the window.
		/// </summary>
        public event EventHandler<ShouldCloseEventArgs> ShouldClose;

        /// <summary>
		/// This is called before calling Closed and before any children are removed 
		/// </summary>
        public event EventHandler Closing2;
        
		/// <summary>
		/// This is called after children have been removed for any last minute cleanup
		/// </summary>
		public event EventHandler Closed;
        #endregion

        public event EventHandler ParentChanged;

		public event EventHandler FocusChanged;

		public event EventHandler<FocusChangedArgs> ContainsFocusChanged;

		/// <summary>
		/// The mouse has gone down on this widget. This will not trigger if a child of this widget gets the down message.
		/// </summary>
		public event EventHandler<MouseEventArgs> MouseDownCaptured;

		public event EventHandler<MouseEventArgs> MouseUpCaptured;

		public class FocusChangedArgs : EventArgs
		{
			public FocusChangedArgs(GuiWidget sourceWidget, bool focused)
			{
				this.Focused = focused;
				this.SourceWidget = sourceWidget;
			}

			public bool Focused { get; }

			public GuiWidget SourceWidget { get; }
		}

		/// <summary>
		/// The mouse has gone down while in the bounds of this widget
		/// </summary>
		public event EventHandler<MouseEventArgs> MouseDown;

		public event EventHandler<MouseEventArgs> MouseUp;

		public event EventHandler<MouseEventArgs> Click;

		public event EventHandler<MouseEventArgs> MouseWheel;

		public event EventHandler<MouseEventArgs> MouseMove;

		public event EventHandler<FlingEventArgs> GestureFling;

		/// <summary>
		/// The mouse has entered the bounds of this widget.  It may also be over a child.
		/// </summary>
		public event EventHandler<MouseEventArgs> MouseEnterBounds;

		/// <summary>
		/// The mouse has left the bounds of this widget.
		/// </summary>
		public event EventHandler<MouseEventArgs> MouseLeaveBounds;

		/// <summary>
		/// The mouse has entered the bounds of this widget and is also not over a child widget.
		/// </summary>
		public event EventHandler<MouseEventArgs> MouseEnter;

		/// <summary>
		/// The mouse has left this widget but may still be over the bounds, it could be above a child.
		/// </summary>
		public event EventHandler<MouseEventArgs> MouseLeave;

		public event EventHandler BoundsChanged;

		public event EventHandler MinimumSizeChanged;

		public event EventHandler MaximumSizeChanged;

		public event EventHandler TextChanged;

		public event EventHandler VisibleChanged;

		public event EventHandler EnabledChanged;

		public event EventHandler VAnchorChanged;

		public event EventHandler HAnchorChanged;

		public event EventHandler ChildAdded;

		public event EventHandler ChildRemoved;

		private static readonly RectangleDouble LargestValidBounds = new RectangleDouble(-1000000, -1000000, 1000000, 1000000);

		public GuiWidget(double width, double height, SizeLimitsToSet sizeLimits = SizeLimitsToSet.Minimum)
			: this()
		{
			screenClipping = new ScreenClipping(this);

			// Direct backing-field initialization. The MinimumSize/MaximumSize/LocalBounds
			// property setters must not be used here: they are (or call) virtual members, and a
			// derived override would run before the derived constructor body has executed. The
			// writes below replicate the setters' effects for a freshly constructed widget
			// (no parent, no children, no event subscribers, default anchors).
			if ((sizeLimits & SizeLimitsToSet.Minimum) == SizeLimitsToSet.Minimum)
			{
				if (width < 0 || height < 0)
				{
					BreakInDebugger("These have to be 0 or greater.");
				}

				minimumSize = new Vector2(width, height);
				maximumSize.X = Max(minimumSize.X, maximumSize.X);
				maximumSize.Y = Max(minimumSize.Y, maximumSize.Y);
			}

			if ((sizeLimits & SizeLimitsToSet.Maximum) == SizeLimitsToSet.Maximum)
			{
				if (width < 0 || height < 0)
				{
					BreakInDebugger("These have to be 0 or greater.");
				}

				maximumSize = new Vector2(width, height);
				minimumSize.X = Min(minimumSize.X, maximumSize.X);
				minimumSize.Y = Min(minimumSize.Y, maximumSize.Y);
			}

			// same normalization the LocalBounds setter performs
			var bounds = new RectangleDouble(0, 0, width, height);
			if (bounds.Width < minimumSize.X)
			{
				bounds.Right = bounds.Left + minimumSize.X;
			}
			else if (bounds.Width > maximumSize.X)
			{
				bounds.Right = bounds.Left + maximumSize.X;
			}

			if (bounds.Height < minimumSize.Y)
			{
				bounds.Top = bounds.Bottom + minimumSize.Y;
			}
			else if (bounds.Height > maximumSize.Y)
			{
				bounds.Top = bounds.Bottom + maximumSize.Y;
			}

			if (EnforceIntegerBounds)
			{
				bounds.Left = Floor(bounds.Left);
				bounds.Bottom = Floor(bounds.Bottom);
				bounds.Right = Ceiling(bounds.Right);
				bounds.Top = Ceiling(bounds.Top);
			}

			if (!LargestValidBounds.Contains(bounds))
			{
				BreakInDebugger("The bounds you are passing seems like they are probably wrong.  Check it.");
			}

			localBounds = bounds;
			screenClipping.MarkRecalculate();
		}

		public GuiWidget()
		{
			Children = new AscendableSafeList<GuiWidget>(this);
			screenClipping = new ScreenClipping(this);
			LayoutEngine = new LayoutEngineSimpleAlign();
		}

		public override string ToString()
		{
			return $"Name = {Name}, Bounds = {LocalBounds} - {GetType().Name}";
		}

		public static event Action<GuiWidget, string, MouseEventArgs> InteractionObserved;

		public AscendableSafeList<GuiWidget> Children { get; }

		public void ClearRemovedFlag()
		{
			hasBeenRemoved = false;
		}

		public Affine ParentToChildTransform
		{
			get => parentToChildTransform;
			set
			{
				// Compared component by component because Affine is a struct with no equality operators (the
				// ones in agg's original are commented out), which is why this guard used to be commented out
				// too. Exact comparison, not the epsilon one agg proposed: a write of the same transform is
				// common during layout and costs a clipping invalidation, but a genuinely tiny move must not
				// be swallowed.
				if (parentToChildTransform.sx != value.sx
					|| parentToChildTransform.shy != value.shy
					|| parentToChildTransform.shx != value.shx
					|| parentToChildTransform.sy != value.sy
					|| parentToChildTransform.tx != value.tx
					|| parentToChildTransform.ty != value.ty)
				{
					parentToChildTransform = value;
					screenClipping.MarkRecalculate();
				}
			}
		}

		public int CountVisibleChildren()
		{
			int count = 0;
			foreach (GuiWidget child in this.Children)
			{
				if (child.Visible == true)
				{
					count++;
				}
			}

			return count;
		}

		public virtual void OnFocusChanged(EventArgs e)
		{
			FocusChanged?.Invoke(this, e);
		}

		public virtual void OnContainsFocusChanged(FocusChangedArgs e)
		{
			ContainsFocusChanged?.Invoke(this, e);
		}

		public virtual Keys ModifierKeys
		{
			get
			{
				if (Parent != null)
				{
					return Parent.ModifierKeys;
				}

				return Keys.None;
			}
		}

		private Vector2 minimumSize = default(Vector2);

		public virtual Vector2 MinimumSize
		{
			get => minimumSize;
			set
			{
				if (value != minimumSize)
				{
					if (value.X < 0 || value.Y < 0)
					{
						BreakInDebugger("These have to be 0 or greater.");
					}

					minimumSize = value;

					maximumSize.X = Max(minimumSize.X, maximumSize.X);
					maximumSize.Y = Max(minimumSize.Y, maximumSize.Y);

					GrowBoundsToMinimumSize();

					OnMinimumSizeChanged(null);
				}
			}
		}

		/// <summary>
		/// Pushes <see cref="LocalBounds"/> up to a <see cref="MinimumSize"/> that has just been raised past it,
		/// so a widget is never left smaller than the minimum it was given.
		/// </summary>
		/// <remarks>
		/// Its own seam only because <see cref="SystemWindow"/> has to opt out: its bounds are the size of a real
		/// drawing surface, and growing past that draws off the surface rather than making the window bigger.
		/// </remarks>
		protected virtual void GrowBoundsToMinimumSize()
		{
			RectangleDouble grownBounds = LocalBounds;
			if (grownBounds.Width < MinimumSize.X)
			{
				grownBounds.Right = grownBounds.Left + MinimumSize.X;
			}

			if (grownBounds.Height < MinimumSize.Y)
			{
				grownBounds.Top = grownBounds.Bottom + MinimumSize.Y;
			}

			LocalBounds = grownBounds;
		}

		public virtual void OnMinimumSizeChanged(EventArgs e)
		{
			MinimumSizeChanged?.Invoke(this, e);
		}

		public virtual void OnMaximumSizeChanged(EventArgs e)
		{
			MaximumSizeChanged?.Invoke(this, e);
		}

		private Vector2 maximumSize = new Vector2(double.MaxValue, double.MaxValue);

		public Vector2 MaximumSize
		{
			get => maximumSize;
			set
			{
				if (value != maximumSize)
				{
					if (value.X < 0 || value.Y < 0)
					{
						BreakInDebugger("These have to be 0 or greater.");
					}

					maximumSize = value;

					minimumSize.X = Min(minimumSize.X, maximumSize.X);
					minimumSize.Y = Min(minimumSize.Y, maximumSize.Y);

					RectangleDouble localBounds = LocalBounds;
					if (localBounds.Width > MaximumSize.X)
					{
						localBounds.Right = localBounds.Left + MaximumSize.X;
					}

					if (localBounds.Height > MaximumSize.Y)
					{
						localBounds.Top = localBounds.Bottom + MaximumSize.Y;
					}

					LocalBounds = localBounds;

					OnMaximumSizeChanged(null);
				}
			}
		}

		/// <summary>
		/// The name of the shared size group this widget belongs to.
		/// All visible widgets with the same group name within a SharedSizeScope
		/// ancestor will be sized to the maximum width among them during layout.
		/// </summary>
		public string SharedSizeGroupName { get; set; }

		/// <summary>
		/// When true, this widget acts as the boundary for shared size groups.
		/// During layout, all descendant widgets with SharedSizeGroupName set
		/// are collected and equalized in width per group.
		/// </summary>
		public bool IsSharedSizeScope { get; set; }

		public event EventHandler PositionChanged;

		public virtual void OnPositionChanged(EventArgs e)
		{
			PositionChanged?.Invoke(this, e);
		}

		/// <summary>
		/// Gets or sets the bottom left position of the widget in its parent space (or the logical/intuitive position).
		/// </summary>
		public Vector2 Position
		{
			get
			{
				var bounds = BoundsRelativeToParent;
				return new Vector2(bounds.Left, bounds.Bottom);
			}

			set
			{
				if (value != Position)
				{
					var delta = value - Position;
					OriginRelativeParent += delta;
					OnPositionChanged(null);
				}
			}
		}

		public event EventHandler SizeChanged;

		public virtual void OnSizeChanged(EventArgs e)
		{
			SizeChanged?.Invoke(this, e);
		}

		/// <summary>
		/// Gets or sets the width height of the control (its size!)
		/// </summary>
		public Vector2 Size
		{
			get => new Vector2(LocalBounds.Width, LocalBounds.Height);
			set
			{
				Width = value.X;
				Height = value.Y;
			}
		}

		public virtual Vector2 OriginRelativeParent
		{
			get
			{
				Affine tempLocalToParentTransform = ParentToChildTransform;
				var originRelParent = new Vector2(tempLocalToParentTransform.tx, tempLocalToParentTransform.ty);
				return originRelParent;
			}

			set
			{
				var tempLocalToParentTransform = ParentToChildTransform;
				if (EnforceIntegerBounds)
				{
					value.X = Math.Round(value.X);
					value.Y = Math.Round(value.Y);
				}

				if (tempLocalToParentTransform.tx != value.X || tempLocalToParentTransform.ty != value.Y)
				{
					tempLocalToParentTransform.tx = value.X;
					tempLocalToParentTransform.ty = value.Y;
					ParentToChildTransform = tempLocalToParentTransform;
					Invalidate();
					if (this.Parent != null)
					{
						// when this object moves it requires that the parent re-layout this object (and maybe others)
						if (!this.Parent.LayoutLocked)
						{
							this.Parent.OnLayout(new LayoutEventArgs(this.Parent, this));
						}
					}

					OnPositionChanged(null);
				}
			}
		}

		/// <summary>
		/// Holds bounds about to be assigned to <see cref="LocalBounds"/> inside <see cref="MinimumSize"/> and
		/// <see cref="MaximumSize"/>, anchoring the rectangle at its left and bottom edges.
		/// </summary>
		/// <remarks>
		/// Its own seam only because <see cref="SystemWindow"/> has to opt out for sizes a platform host reports:
		/// those are the measured size of a real drawing surface, and a widget tree laid out larger than the
		/// surface does not enlarge it, it draws off the edge of it.
		/// </remarks>
		protected virtual RectangleDouble ClampToSizeLimits(RectangleDouble value)
		{
			if (value.Width < MinimumSize.X)
			{
				value.Right = value.Left + MinimumSize.X;
			}
			else if (value.Width > MaximumSize.X)
			{
				value.Right = value.Left + MaximumSize.X;
			}

			if (value.Height < MinimumSize.Y)
			{
				value.Top = value.Bottom + MinimumSize.Y;
			}
			else if (value.Height > MaximumSize.Y)
			{
				value.Top = value.Bottom + MaximumSize.Y;
			}

			return value;
		}

		public virtual RectangleDouble LocalBounds
		{
			get => localBounds;
			set
			{
				value = ClampToSizeLimits(value);

				if (EnforceIntegerBounds)
				{
					value.Left = Floor(value.Left);
					value.Bottom = Floor(value.Bottom);
					value.Right = Ceiling(value.Right);
					value.Top = Ceiling(value.Top);
				}

				if (localBounds != value)
				{
					if (!LargestValidBounds.Contains(value))
					{
						BreakInDebugger("The bounds you are passing seems like they are probably wrong.  Check it.");
					}

					localBounds = value;

					OnLayout(new LayoutEventArgs(this, null));
					if (this.Parent != null
						&& !this.Parent.LayoutLocked)
					{
						this.Parent.OnLayout(new LayoutEventArgs(this.Parent, this));
					}

					Invalidate();

					backbuffer?.AllocateRgbaBuffer();

					OnBoundsChanged(null);

					screenClipping.MarkRecalculate();
				}
			}
		}

		public RectangleDouble BoundsRelativeToParent
		{
			get
			{
				RectangleDouble boundsRelParent = LocalBounds;
				boundsRelParent.Offset(OriginRelativeParent.X, OriginRelativeParent.Y);
				return boundsRelParent;
			}

			set
			{
				// constrain this to MinimumSize
				if (value.Width < MinimumSize.X)
				{
					value.Right = value.Left + MinimumSize.X;
				}

				if (value.Height < MinimumSize.Y)
				{
					value.Top = value.Bottom + MinimumSize.Y;
				}

				if (value != BoundsRelativeToParent)
				{
					value.Offset(-OriginRelativeParent.X, -OriginRelativeParent.Y);
					LocalBounds = value;
				}
			}
		}

		public RectangleDouble GetChildrenBoundsIncludingMargins(bool considerChildAnchor = false, Func<GuiWidget, GuiWidget, bool> considerChild = null)
		{
			var boundsOfAllChildrenIncludingMargin = new RectangleDouble();

			if (this.CountVisibleChildren() > 0)
			{
				Vector2 minSize = Vector2.Zero;
				boundsOfAllChildrenIncludingMargin = RectangleDouble.ZeroIntersection;
				bool foundHBounds = false;
				bool foundVBounds = false;
				foreach (GuiWidget child in Children)
				{
					if (child.Visible == false
					|| (considerChild != null && !considerChild(this, child)))
					{
						continue;
					}

					if (considerChildAnchor)
					{
						var childSize = child.MinimumSize;
						minSize.X = Max((child.HAnchor == HAnchor.Stretch) ? 0 : child.Width
							+ child.DeviceMarginAndBorder.Width, minSize.X);
						minSize.Y = Max((child.VAnchor == VAnchor.Stretch) ? 0 : child.Height
							+ child.DeviceMarginAndBorder.Height, minSize.Y);

						RectangleDouble childBoundsWithMargin = child.BoundsRelativeToParent;
						childBoundsWithMargin.Inflate(child.DeviceMarginAndBorder);

						var flowLayout = this as FlowLayoutWidget;
						bool childHSizeHasBeenAdjusted = flowLayout != null && (flowLayout.FlowDirection == FlowDirection.LeftToRight || flowLayout.FlowDirection == FlowDirection.RightToLeft);
						if (!child.HAnchorIsFloating
							&& (child.HAnchor != HAnchor.Stretch || childHSizeHasBeenAdjusted))
						{
							foundHBounds = true;
							// it can't move so make sure our horizontal bounds enclose it
							if (boundsOfAllChildrenIncludingMargin.Right < childBoundsWithMargin.Right)
							{
								boundsOfAllChildrenIncludingMargin.Right = childBoundsWithMargin.Right;
							}

							if (boundsOfAllChildrenIncludingMargin.Left > childBoundsWithMargin.Left)
							{
								boundsOfAllChildrenIncludingMargin.Left = childBoundsWithMargin.Left;
							}
						}

						bool childVSizeHasBeenAdjusted = flowLayout != null && (flowLayout.FlowDirection == FlowDirection.BottomToTop || flowLayout.FlowDirection == FlowDirection.TopToBottom);
						if (!child.VAnchorIsFloating
							&& (child.VAnchor != VAnchor.Stretch || childVSizeHasBeenAdjusted))
						{
							foundVBounds = true;
							// it can't move so make sure our vertical bounds enclose it
							if (boundsOfAllChildrenIncludingMargin.Top < childBoundsWithMargin.Top)
							{
								boundsOfAllChildrenIncludingMargin.Top = childBoundsWithMargin.Top;
							}

							if (boundsOfAllChildrenIncludingMargin.Bottom > childBoundsWithMargin.Bottom)
							{
								boundsOfAllChildrenIncludingMargin.Bottom = childBoundsWithMargin.Bottom;
							}
						}
					}
					else
					{
						RectangleDouble childBoundsWithMargin = child.BoundsRelativeToParent;
						childBoundsWithMargin.Inflate(child.Margin);
						boundsOfAllChildrenIncludingMargin.ExpandToInclude(childBoundsWithMargin);
					}
				}

				if (considerChildAnchor)
				{
					if (foundHBounds)
					{
						boundsOfAllChildrenIncludingMargin.Right = boundsOfAllChildrenIncludingMargin.Left + Max(boundsOfAllChildrenIncludingMargin.Width, minSize.X);
					}
					else
					{
						boundsOfAllChildrenIncludingMargin.Left = 0;
						boundsOfAllChildrenIncludingMargin.Right = minSize.X;
					}

					if (foundVBounds)
					{
						boundsOfAllChildrenIncludingMargin.Top = boundsOfAllChildrenIncludingMargin.Bottom + Max(boundsOfAllChildrenIncludingMargin.Height, minSize.Y);
					}
					else
					{
						boundsOfAllChildrenIncludingMargin.Bottom = 0;
						boundsOfAllChildrenIncludingMargin.Top = minSize.Y;
					}
				}
			}

			return boundsOfAllChildrenIncludingMargin;
		}

		/// <summary>
		/// The smallest rect that covers every visible child (plus their margins and our padding). This is what
		/// HAnchor/VAnchor Fit sizes to, so a widget whose children are not a fair measure of its content (a
		/// <see cref="ScrollableWidget"/>, whose scrolling area is displaced by the scroll position) overrides it.
		/// </summary>
		public virtual RectangleDouble GetMinimumBoundsToEncloseChildren(bool considerChildAnchor = false)
		{
			RectangleDouble minimumSizeToEncloseChildren = GetChildrenBoundsIncludingMargins(considerChildAnchor);
			minimumSizeToEncloseChildren.Inflate(DevicePadding);
			return minimumSizeToEncloseChildren;
		}

		public void SetBoundsToEncloseChildren()
		{
			RectangleDouble childrenBounds = GetMinimumBoundsToEncloseChildren();
			LocalBounds = childrenBounds;
		}

		public virtual void OnBoundsChanged(EventArgs e)
		{
			BoundsChanged?.Invoke(this, e);

			// make sure we call size changed (we are planning to deprecate bounds changed at some point)
			OnSizeChanged(e);
		}

		public string Name { get; set; }

		private string _text = "";

		public virtual string Text
		{
			get => _text;
			set
			{
				// make sure value is set to empty string rather than null
				value = value ?? "";
				if (_text != value)
				{
					_text = value;
					OnTextChanged(null);
					Invalidate();
				}
			}
		}

		/// <summary>
		/// Gets or sets if this is set the control will show tool tips on hover, if the platform specific SystemWindow implements tool tips.
		/// You can change the settings for the tool tip delays in the containing SystemWindow. Shows a hint or help text.
		/// </summary>
		public virtual string ToolTipText { get; set; }

		public virtual void OnTextChanged(EventArgs e)
		{
			TextChanged?.Invoke(this, e);
		}

		public void SetBoundsRelativeToParent(RectangleInt newBounds)
		{
			var bounds = new RectangleDouble(newBounds.Left, newBounds.Bottom, newBounds.Right, newBounds.Top);

			BoundsRelativeToParent = bounds;
		}

		public bool MouseCaptured => mouseCapturedState == MouseCapturedState.ThisHasMouseCaptured;

		public bool ChildHasMouseCaptured => mouseCapturedState == MouseCapturedState.ChildHasMouseCaptured;

		public virtual bool Visible
		{
			get => visible;
			set
			{
				if (visible != value)
				{
					visible = value;
					if (visible == false)
					{
						Unfocus();
					}

					OnVisibleChanged(null);

					OnLayout(new LayoutEventArgs(this, null));
					this.Parent?.OnLayout(new LayoutEventArgs(this.Parent, this));

					Invalidate();
					screenClipping.MarkRecalculate();
				}
			}
		}

		public virtual bool Enabled
		{
			get => this.enabled && this.Parent?.Enabled != false;
			set
			{
				if (enabled != value)
				{
					enabled = value;
					if (enabled == false)
					{
						ClearCapturedState();
						Unfocus();
					}

					this.Invalidate();

					OnEnabledChanged(null);
				}
			}
		}

		public virtual void OnVisibleChanged(EventArgs e)
		{
			VisibleChanged?.Invoke(this, e);
		}

		private void ClearMouseOverWidget()
		{
			bool needToCallLeaveBounds = UnderMouseState != UI.UnderMouseState.NotUnderMouse;
			if (needToCallLeaveBounds)
			{
				OnMouseLeaveBounds(null);
			}

			foreach (GuiWidget child in Children)
			{
				child.ClearMouseOverWidget();
			}

			UnderMouseState = UI.UnderMouseState.NotUnderMouse;
		}

		public virtual void OnEnabledChanged(EventArgs e)
		{
			if (Enabled == false)
			{
				if (FirstWidgetUnderMouse)
				{
					ClearMouseOverWidget();
					OnMouseLeave(null);
				}
			}

			Invalidate();
			EnabledChanged?.Invoke(this, null);

			foreach (GuiWidget child in Children)
			{
				child.OnEnabledChanged(e);
			}
		}

		private GuiWidget _parent = null;

		public GuiWidget Parent
		{
			get => _parent;
			set
			{
				if (value == null && _parent != null)
				{
					if (_parent.Children.Contains(this))
					{
						throw new Exception("Take this out of the parent before setting this to null.");
					}
				}

				_parent = value;

				// A move to a new parent replaces this widget's whole ancestor chain, so its cached screen
				// clipping (and every descendant's, which is validated through this one) is worthless.
				screenClipping.MarkRecalculate();
			}
		}

		private bool _resizable = true;

		public bool Resizable
		{
			get => _resizable;

			set
			{
				if (_resizable != value)
				{
					_resizable = value;
					OnResizeableChanged(null);
					Invalidate();
				}
			}
		}

		public event EventHandler ResizeableChanged;

		public virtual void OnResizeableChanged(EventArgs e)
		{
			ResizeableChanged?.Invoke(this, e);
		}

		// Place holder, this is not really implemented.

		public double Width
		{
			get => LocalBounds.Width;
			set
			{
				if (value != Width)
				{
					RectangleDouble localBounds = LocalBounds;
					localBounds.Right = localBounds.Left + value;
					LocalBounds = localBounds;
				}
			}
		}

		public double Height
		{
			get => LocalBounds.Height;
			set
			{
				if (value != Height)
				{
					RectangleDouble localBounds = LocalBounds;
					localBounds.Top = localBounds.Bottom + value;
					LocalBounds = localBounds;
				}
			}
		}

		/// <summary>
		/// Add a child to this widget. It will layout right away.
		/// </summary>
		/// <param name="childToAdd">The child to add</param>
		/// <param name="indexInChildrenList">The index in the child list to add the child (defaults to the end).</param>
		/// <returns>The child that was added</returns>
		public virtual GuiWidget AddChild(GuiWidget childToAdd, int indexInChildrenList = -1)
		{
#if DEBUG
			if (childToAdd.hasBeenRemoved)
			{
				throw new Exception("You are adding a child that has previously been removed. You should probably be creating a new widget, or calling ClearRemovedFlag() before adding.");
			}
#endif

			// first thing we do is make sure the child has been initialized
			childToAdd.Initialize();


			if (indexInChildrenList == -1)
			{
				indexInChildrenList = Children.Count;
			}

			if (childToAdd == this)
			{
				BreakInDebugger("A GuiWidget cannot be a child of itself.");
			}

			if (indexInChildrenList > Children.Count)
			{
				throw new IndexOutOfRangeException();
			}

			if (Children.Contains(childToAdd))
			{
				throw new Exception("You cannot add the same child twice.");
			}

			if (childToAdd.Parent != null)
			{
				throw new Exception("This is already the child of another widget.");
			}

			childToAdd.Parent = this;
			childToAdd.HasBeenClosed = false;
			Children.Modify((list) =>
			{
				list.Insert(indexInChildrenList, childToAdd);
			});

			OnChildAdded(new GuiWidgetEventArgs(childToAdd));
			childToAdd.OnParentChanged(null);

			childToAdd.InitLayout();
			OnLayout(new LayoutEventArgs(this, childToAdd));

			return childToAdd;
		}

		/// <summary>
		/// Override this to create child controls and other
		/// </summary>
		public virtual void Initialize()
		{
			Initialized = true;
		}

		public void SendToBack()
		{
			if (Parent == null)
			{
				return;
			}

			Parent.Children.Modify((list) =>
			{
				list.Remove(this);
				list.Insert(0, this);
			});
		}

		/// <summary>
		/// Moves this widget to the end of its parent's children, so it draws over - and is hit tested before -
		/// its siblings.
		/// </summary>
		/// <remarks>
		/// Deliberately a reorder of the parent's list rather than a <see cref="RemoveChild(GuiWidget)"/> and
		/// <see cref="AddChild"/> pair, which is how callers used to raise a widget themselves (WindowWidget did,
		/// to come to the front when it took focus). RemoveChild calls ClearCapturedState, which drops the mouse
		/// capture the current press has set all the way up the parent chain, so raising a widget from inside a
		/// click (a floating window coming to the front as the user presses a control on it) would swallow the
		/// release and the click with it. Nothing about the widget's parentage, focus or capture changes here -
		/// only where it sits in the list.
		/// </remarks>
		public virtual void BringToFront()
		{
			var parent = Parent;
			if (parent == null
				|| parent.Children.Count == 0
				|| parent.Children[parent.Children.Count - 1] == this)
			{
				return;
			}

			// one Modify, so the widget is never missing from the published list - anything enumerating the
			// children while this runs sees either the old order or the new one, never a gap
			parent.Children.Modify(list =>
			{
				if (list.Remove(this))
				{
					list.Add(this);
				}
			});

			// only this widget's own area changes when it moves to the front of the list, and invalidating the
			// whole parent is a full screen repaint per raise when that parent is the root window
			this.Invalidate();
		}

		public virtual void OnChildAdded(EventArgs e)
		{
			ChildAdded?.Invoke(this, e);
		}

		/// <summary>
		/// Remove all children and call close on each of them
		/// </summary>
		public void CloseChildren()
		{
			Children.Modify(list =>
			{
				foreach (var child in list)
				{
					using (child.LayoutLock())
					{
						child.Close();
					}
				}

				list.Clear();
			});
		}
		/// <summary>
		/// Remove all the children of the widget but do not explicitly call close on them
		/// </summary>
		public void RemoveChildren()
		{
			foreach (var child in Children)
			{
				RemoveChild(child);
			}
		}

		public virtual GuiWidget RemoveChild(int index)
		{
			GuiWidget childThatWasRemove = null;
			Children.Modify((list) =>
			{
				if (index < list.Count)
				{
					childThatWasRemove = list[index];
					list.RemoveAt(index);
				}
			});

			return childThatWasRemove;
		}

		public void ReplaceChild(GuiWidget existing, GuiWidget replacement)
		{
			Children.Modify((list) =>
			{
				var index = list.IndexOf(existing);
				if (index >= 0)
				{
					list.Remove(existing);
					list.Insert(index, replacement);
				}
			});
		}

		private bool hasBeenRemoved = false;

		public virtual void RemoveChild(GuiWidget childToRemove)
		{
			if (Children.Contains(childToRemove))
			{
				childToRemove.ClearCapturedState();
				childToRemove.hasBeenRemoved = true;
				Children.Remove(childToRemove);
				childToRemove.Parent = null;
				OnChildRemoved(new GuiWidgetEventArgs(childToRemove));
				OnLayout(new LayoutEventArgs(this, null));
				Invalidate();
			}
		}

		public virtual void OnChildRemoved(EventArgs e)
		{
			ChildRemoved?.Invoke(this, e);
		}

		/// <summary>
		/// A <see cref="Graphics2D"/> that draws onto this widget, outside of a paint.
		/// </summary>
		/// <returns>Graphics for this widget's own backbuffer when it is double buffered and that buffer
		/// exists, otherwise one derived from the nearest ancestor that can supply a surface, transformed and
		/// clipped to this widget. Null when no ancestor can supply one, or when this widget is clipped
		/// away.</returns>
		/// <remarks>
		/// The buffer is checked for rather than inferred from <see cref="DoubleBuffer"/>, because while the
		/// widget's pixels are in <see cref="BackbufferMode.LcdCoverage"/> the RGBA buffer genuinely does not
		/// exist and <see cref="BackBuffer"/> answers null (see its remarks). A double-buffered widget in that
		/// state behaves here as an un-buffered one does and derives its graphics from the parent chain.
		/// <para>
		/// Handing back an <see cref="LcdBufferGraphics2D"/> over the coverage planes was possible - that is a
		/// real <see cref="Graphics2D"/>, and <see cref="WidgetBackbuffer.Rasterize"/> constructs one - but was
		/// deliberately chosen against. It is a paint-time surface, built for the duration of a raster and only
		/// partially supported outside that pipeline (its <see cref="IImageFloat"/> <c>Render</c> overload
		/// throws, for one), so giving it to arbitrary out-of-paint callers carries hazards of its own. The
		/// parent-derived surface is the answer an un-buffered widget has always given, which is the behaviour
		/// callers here already cope with.
		/// </para>
		/// <para>
		/// <b>That surface is transient.</b> Ink drawn through it lands on the ancestor's pixels, so the next
		/// time the parent composites this widget's coverage planes over that rect
		/// (<see cref="WidgetBackbuffer.CompositeOnto"/>) it is painted over - unlike the RGBA arm above, where
		/// drawing goes into the widget's own cache and survives until the widget re-rasters.
		/// </para>
		/// </remarks>
		public virtual Graphics2D NewGraphics2D()
		{
			// Read once: BackBuffer is computed, and the mode it keys on is not this method's to re-check.
			ImageBuffer rgbaBuffer = BackBuffer;
			if (rgbaBuffer != null)
			{
				return rgbaBuffer.NewGraphics2D();
			}

			if (Parent != null)
			{
				// call recursively to get the first parent that can return a Graphics2D
				Graphics2D parentGraphics2D = Parent.NewGraphics2D();
				if (parentGraphics2D != null)
				{
					Affine parentToLocalTransform = parentGraphics2D.GetTransform();
					parentToLocalTransform *= ParentToChildTransform;
					parentGraphics2D.SetTransform(parentToLocalTransform);

					if (CurrentScreenClipping(out RectangleDouble currentScreenClipping))
					{
						parentGraphics2D.SetClippingRect(currentScreenClipping);
						return parentGraphics2D;
					}
				}
			}

			return null;
		}

		public bool PositionWithinLocalBounds(Vector2 position)
		{
			return PositionWithinLocalBounds(position.X, position.Y);
		}

		public virtual bool PositionWithinLocalBounds(double x, double y)
		{
			if (LocalBounds.Contains(x, y))
			{
				return true;
			}

			return false;
		}

		public void Invalidate()
		{
			Invalidate(LocalBounds);
		}

		public virtual void Invalidate(RectangleDouble rectToInvalidate)
		{
			isCurrentlyInvalid = true;

			var threadSafeParent = Parent;
			if (threadSafeParent != null && threadSafeParent.Visible)
			{
				rectToInvalidate.Offset(OriginRelativeParent);

				// This code may be a good idea but it needs to be tested to make sure there are no subtle consequences
				if (rectToInvalidate.Width > 0 && rectToInvalidate.Height > 0
					&& this.ActuallyVisibleOnParent())
				{
					threadSafeParent.Invalidate(rectToInvalidate);
				}
			}

			Invalidated?.Invoke(this, new InvalidateEventArgs(rectToInvalidate));
		}

		public virtual void Focus()
		{
			if (CanFocus && CanSelect && !Focused)
			{
				var allWidgetsThatWillContainFocus = new List<GuiWidget>();
				var allWidgetsThatCurrentlyHaveFocus = new List<GuiWidget>();

				GuiWidget widgetNeedingFocus = this;
				while (widgetNeedingFocus != null)
				{
					allWidgetsThatWillContainFocus.Add(widgetNeedingFocus);
					widgetNeedingFocus = widgetNeedingFocus.Parent;
				}

				GuiWidget currentWithFocus = allWidgetsThatWillContainFocus[allWidgetsThatWillContainFocus.Count - 1];
				while (currentWithFocus != null)
				{
					allWidgetsThatCurrentlyHaveFocus.Add(currentWithFocus);
					GuiWidget childWithFocus = null;
					foreach (GuiWidget child in currentWithFocus.Children)
					{
						if (child.ContainsFocus)
						{
							if (childWithFocus != null)
							{
								BreakInDebugger("Two children should never have focus.");
							}

							childWithFocus = child;
						}
					}

					currentWithFocus = childWithFocus;
				}

				// Try to remove all the widgets we are giving focus to from all the ones that have it.
				// This will leave us with a list of all the widgets that need to lose focus.
				foreach (GuiWidget childThatWillNeedFocus in allWidgetsThatWillContainFocus)
				{
					if (allWidgetsThatCurrentlyHaveFocus.Contains(childThatWillNeedFocus))
					{
						allWidgetsThatCurrentlyHaveFocus.Remove(childThatWillNeedFocus);
					}
				}

				// take the focus away from all the widgets that will not have it after this focus.
				foreach (GuiWidget childThatIsLosingFocus in allWidgetsThatCurrentlyHaveFocus)
				{
					childThatIsLosingFocus.Unfocus();
				}

				// and give focus to everything in our direct parent chain (including this).
				GuiWidget curWidget = this;
				do
				{
					curWidget.containsFocus = true;
					curWidget = curWidget.Parent;
				}
				while (curWidget != null);

				// finally call any delegates
				OnFocusChanged(null);
			}
		}

		public void Unfocus()
		{
			if (containsFocus == true)
			{
				if (Focused)
				{
					containsFocus = false;
					OnContainsFocusChanged(new FocusChangedArgs(this, false));
					OnFocusChanged(null);
					return;
				}

				// If it is still focused it was not the primary widget one of its children was
				if (containsFocus)
				{
					containsFocus = false;
					OnContainsFocusChanged(new FocusChangedArgs(this, false));
					foreach (GuiWidget child in Children.ToArray())
					{
						child.Unfocus();
					}
				}
			}
		}

		public bool CanSelect
		{
			get
			{
				if (Selectable && Parent != null && AllParentsVisibleAndEnabled())
				{
					return true;
				}

				return false;
			}
		}

		private bool AllParentsVisibleAndEnabled()
		{
			GuiWidget curGUIWidget = this;
			RectangleDouble visibleBounds = this.LocalBounds;
			while (curGUIWidget != null)
			{
				if (!curGUIWidget.Visible || !curGUIWidget.Enabled
					|| visibleBounds.Width <= 0
					|| visibleBounds.Height <= 0)
				{
					return false;
				}

				var parent = curGUIWidget.Parent;
				if (parent != null)
				{
					// offset our bounds to the parent bounds
					visibleBounds.Offset(curGUIWidget.OriginRelativeParent.X, curGUIWidget.OriginRelativeParent.Y);
					visibleBounds.IntersectWithRectangle(parent.LocalBounds);
				}

				curGUIWidget = parent;
			}

			return true;
		}

		public bool ActuallyVisibleOnParent()
		{
			RectangleDouble visibleBounds = this.LocalBounds;
			if (!this.Visible
				|| visibleBounds.Width <= 0
				|| visibleBounds.Height <= 0)
			{
				return false;
			}

			// hold this to prevent threading issues
            var parent = this.Parent; 
			if (parent != null)
			{
				// offset our bounds to the parent bounds
				visibleBounds.Offset(this.OriginRelativeParent.X, this.OriginRelativeParent.Y);
				visibleBounds.IntersectWithRectangle(parent.LocalBounds);
			}

			if (visibleBounds.Width <= 0
				|| visibleBounds.Height <= 0)
			{
				return false;
			}

			return true;
		}

		public RectangleDouble ClippedOnScreenBounds()
		{
			GuiWidget curGUIWidget = this;
			var clippedBounds = this.LocalBounds;
			while (curGUIWidget != null)
			{
				if (!curGUIWidget.Visible
					|| clippedBounds.Width <= 0
					|| clippedBounds.Height <= 0)
				{
					return default(RectangleDouble);
				}

				if (curGUIWidget.Parent != null)
				{
					// offset our bounds to the parent bounds
					clippedBounds.Offset(curGUIWidget.OriginRelativeParent.X, curGUIWidget.OriginRelativeParent.Y);
					clippedBounds.IntersectWithRectangle(curGUIWidget.Parent.LocalBounds);
				}

				curGUIWidget = curGUIWidget.Parent;
			}

			return clippedBounds;
		}

		public bool ActuallyVisibleOnScreen()
		{
			GuiWidget curGUIWidget = this;
			RectangleDouble visibleBounds = this.LocalBounds;
			bool sawSystemWindow = false;
			while (curGUIWidget != null)
			{
				if (curGUIWidget is SystemWindow)
				{
					sawSystemWindow = true;
				}

				if (!curGUIWidget.Visible
					|| visibleBounds.Width <= 0
					|| visibleBounds.Height <= 0)
				{
					return false;
				}

				if (curGUIWidget.Parent != null)
				{
					// offset our bounds to the parent bounds
					visibleBounds.Offset(curGUIWidget.OriginRelativeParent.X, curGUIWidget.OriginRelativeParent.Y);
					visibleBounds.IntersectWithRectangle(curGUIWidget.Parent.LocalBounds);
				}

				curGUIWidget = curGUIWidget.Parent;
			}

			return sawSystemWindow;
		}

		public virtual bool CanFocus => this.Visible && this.Enabled;

		public bool Focused
		{
			get
			{
				if (ContainsFocus && CanFocus)
				{
					foreach (GuiWidget child in Children)
					{
						if (child.ContainsFocus)
						{
							return false;
						}
					}

					// we contain focus and none of our children do so we are focused.
					return true;
				}

				return false;
			}
		}

		public bool ContainsFocus => containsFocus;

		public bool Initialized { get; private set; } = false;

		public void PerformLayout()
		{
			OnLayout(new LayoutEventArgs(this, null));
		}

		public virtual void InitLayout()
		{
		}

		public virtual void OnLayout(LayoutEventArgs layoutEventArgs)
		{
			if (this.HasBeenClosed)
			{
				return;
			}

			if (Visible && !LayoutLocked)
			{
				LayoutCount++;

				if (LayoutEngine != null)
				{
					using (LayoutLock())
					{
						LayoutEngine.Layout(layoutEventArgs);
					}
				}

				Layout?.Invoke(this, layoutEventArgs);
			}
		}

		public virtual void OnParentChanged(EventArgs e)
		{
			ParentChanged?.Invoke(this, e);
		}

        /// <summary>
        /// Builds a rounded rect inset from <paramref name="bounds"/> on every side, with the corner
        /// radii pulled in by the same amount so the arcs still fit the box they are drawn in.
        /// </summary>
        /// <remarks>
        /// Keeping the full radius on an inset rect is what makes a "stadium" (corner radius exactly half
        /// the height) render as a malformed blob. Inset a 24 tall stadium by 1 and its 12 radius corners
        /// have only 11 of half-height left, so the top and bottom arcs sweep past each other.
        /// <see cref="RoundedRect.Vertices"/> emits the arcs as given - it never normalizes them - so the
        /// result is a self-intersecting path that fills and strokes as garbage at both end caps.
        /// </remarks>
        internal static RoundedRect InsetRoundedRect(RectangleDouble bounds, RadiusCorners cornerRadius, double inset)
        {
            var rect = new RoundedRect(bounds.Left + inset, bounds.Bottom + inset, bounds.Right - inset, bounds.Top - inset);
            rect.radius(Max(cornerRadius.SW - inset, 0),
                Max(cornerRadius.SE - inset, 0),
                Max(cornerRadius.NE - inset, 0),
                Max(cornerRadius.NW - inset, 0));

            return rect;
        }

        /// <summary>
        /// True when insetting <paramref name="bounds"/> by <paramref name="inset"/> would turn it inside
        /// out - the inset eats more than half the width or more than half the height.
        /// </summary>
        /// <remarks>
        /// Such a piece cannot simply be drawn and left to disappear: <see cref="RoundedRect"/> puts
        /// inverted bounds back in order, so an over-inset rect comes back as a thin normal one and paints a
        /// band across the middle of the widget. A stroke width wider than the widget it outlines is the
        /// usual way to get here. Skip the piece instead.
        /// </remarks>
        private static bool InsetInvertsBounds(RectangleDouble bounds, double inset)
        {
            return inset * 2 > bounds.Width || inset * 2 > bounds.Height;
        }

        public static void RenderBackground(Graphics2D graphics2D,
			RectangleDouble bounds,
			Color backgroundColor,
			RadiusCorners cornerRadius,
			double outlineWidth,
			Color outlineColor)
		{
            if (outlineColor.Alpha0To255 > 0 && outlineWidth > 0)
            {
                var stroke = outlineWidth * GuiWidget.DeviceScale;

                if (backgroundColor.Alpha0To255 > 0 && !InsetInvertsBounds(bounds, stroke))
                {
                    // the background sits entirely inside the border, so it insets by the full stroke width
                    graphics2D.Render(InsetRoundedRect(bounds, cornerRadius, stroke), backgroundColor);
                }

                // and draw the border, on a centerline half a stroke in from the bounds so the stroke's
                // outer edge lands on them
                if (!InsetInvertsBounds(bounds, stroke / 2))
                {
                    var rectOutline = new Stroke(InsetRoundedRect(bounds, cornerRadius, stroke / 2), stroke);

                    graphics2D.Render(rectOutline, outlineColor);
                }
            }
            else if (backgroundColor.Alpha0To255 > 0)
            {
                // only draw the background color
                graphics2D.Render(InsetRoundedRect(bounds, cornerRadius, 0), backgroundColor);
            }
        }

        /// <summary>
        /// This is called before the OnDraw method.
        /// When overriding OnPaintBackground in a derived class it is not necessary to call the base class's OnPaintBackground.
        /// </summary>
        /// <param name="graphics2D">The graphics 2D this is being drawn onto.</param>
        public virtual void OnDrawBackground(Graphics2D graphics2D)
		{
            RenderBackground(graphics2D, this.LocalBounds, BackgroundColor, BackgroundRadius, BackgroundOutlineWidth, BorderColor);
        }

		public static int DrawCount;
		public static int LayoutCount;

		protected bool onloadInvoked = false;

		/// <summary>
		/// Called before the very first draw of this widget
		/// </summary>
		public event EventHandler Load;

		/// <summary>
		/// Called before the very first draw of this widget
		/// </summary>
		/// <param name="args">The args to pass on.</param>
		public virtual void OnLoad(EventArgs args)
		{
			this.Load?.Invoke(this, args);
		}

		public static ConcurrentDictionary<int, int> DrawsByDepth { get; private set; } = new ConcurrentDictionary<int, int>();

		[ThreadStatic]
		private static int drawDepth;

		public virtual void OnDraw(Graphics2D graphics2D)
		{
			drawDepth++;
			DrawsByDepth.AddOrUpdate(drawDepth, 1, (key, oldValue) => oldValue + 1);

			if (!onloadInvoked)
			{
				// Set onloadInvoked before invoking OnLoad to ensure we only fire once
				onloadInvoked = true;

				this.OnLoad(null);
			}

			DrawCount++;

			OnBeforeDraw(graphics2D);

			bool haveDrawOnTopChildren = false;

			foreach (var child in Children)
			{
				if (child.DrawOnTopOfSiblings)
				{
					// hold these back so they paint above the rest of their siblings
					haveDrawOnTopChildren = true;
					continue;
				}

				DrawChild(child, graphics2D);
			}

			// only walk the children a second time if there is actually something to draw on top
			if (haveDrawOnTopChildren)
			{
				foreach (var child in Children)
				{
					if (child.DrawOnTopOfSiblings)
					{
						DrawChild(child, graphics2D);
					}
				}
			}

			OnAfterDraw(graphics2D);

			if (DebugShowBounds)
            {
                ShowDebugBounds(graphics2D);
            }

            if (DebugShowSize)
			{
				graphics2D.DrawString(string.Format("{4} {0}, {1} : {2}, {3}", (int)MinimumSize.X, (int)MinimumSize.Y, (int)LocalBounds.Width, (int)LocalBounds.Height, Name),
					Width / 2, Max(Height - 16, Height / 2 - 16 * graphics2D.TransformStackCount), color: Magenta, justification: Font.Justification.Center);
			}

			drawDepth--;
		}

		/// <summary>
		/// Render a single child of this widget, applying its transform, screen clipping and back buffer.
		/// </summary>
		/// <param name="child">The child to draw. It is skipped if it is not visible.</param>
		/// <param name="graphics2D">The graphics 2D this widget is being drawn onto.</param>
		private void DrawChild(GuiWidget child, Graphics2D graphics2D)
		{
			if (child.Visible)
			{
				if (child.DebugShowBounds)
				{
					// draw the margin
					BorderDouble invertedMargin = child.DeviceMarginAndBorder;
					invertedMargin.Left = -invertedMargin.Left;
					invertedMargin.Bottom = -invertedMargin.Bottom;
					invertedMargin.Right = -invertedMargin.Right;
					invertedMargin.Top = -invertedMargin.Top;
					DrawBorderAndPaddingBounds(graphics2D, child.BoundsRelativeToParent, invertedMargin, new Color(Red, 128));
				}

				RectangleDouble oldClippingRect = graphics2D.GetClippingRect();
				graphics2D.PushTransform();
				{
					Affine currentGraphics2DTransform = graphics2D.GetTransform();
					Affine accumulatedTransform = child.ParentToChildTransform * currentGraphics2DTransform;
					graphics2D.SetTransform(accumulatedTransform);

					bool childHasSomethingToPaint = child.CurrentScreenClipping(out RectangleDouble currentScreenClipping);
					if (childHasSomethingToPaint)
					{
						// The clipping is worked out in screen coordinates, and the surface being painted is not
						// always the screen: while this widget is double buffered its children are painted into a
						// buffer whose bottom left pixel is this widget's own origin, not the screen's. Shifting
						// the rectangle by the difference between where that origin sits on the destination and
						// where it sits on the screen puts the clip in the destination's own coordinates.
						//
						// The shift is zero whenever the two are the same surface, so painting straight to the
						// screen is byte for byte what it always was. Without it a double buffered widget away
						// from the screen origin clips its children off the far side of its own buffer and
						// composites as an empty pane - which is exactly what a popped out editor window, sat in
						// the middle of the 3D view, did.
						currentScreenClipping.Offset(currentGraphics2DTransform.Transform(Vector2.Zero) - this.ScreenSpaceOrigin());

						currentScreenClipping.Left = Floor(currentScreenClipping.Left);
						currentScreenClipping.Right = Ceiling(currentScreenClipping.Right);
						currentScreenClipping.Bottom = Floor(currentScreenClipping.Bottom);
						currentScreenClipping.Top = Ceiling(currentScreenClipping.Top);
						if (currentScreenClipping.Right < currentScreenClipping.Left || currentScreenClipping.Top < currentScreenClipping.Bottom)
						{
							BreakInDebugger("Right is less than Left or Top is less than Bottom");
						}

						// The offset above reconciles surfaces, but it describes where the child sat when the
						// clip was computed and cannot be trusted once layout has mutated mid-frame: a widget
						// that grows during a paint pass shifts or widens this rectangle past the clip its
						// parent is painting under. The parent's clip is the outer bound no child may escape,
						// so bound the child by it here rather than replacing it below. An empty result means
						// the child lies entirely outside the visible region and has nothing to paint.
						// Note the skip suppresses more than the child's ink: its OnDraw side effects (lazy
						// content building, backbuffer rasterization) do not run either. A dirty child that
						// is fully clipped out defers those to the next frame, which the mutation's own
						// Invalidate has already scheduled.
						childHasSomethingToPaint = currentScreenClipping.IntersectWithRectangle(oldClippingRect);
					}

					if (childHasSomethingToPaint)
					{
						graphics2D.SetClippingRect(currentScreenClipping);

						if (child.DoubleBuffer
							&& accumulatedTransform.sx < 1.05
							&& accumulatedTransform.sx > .95)
						{
							var offsetToRenderSurface = new Vector2(currentGraphics2DTransform.tx, currentGraphics2DTransform.ty);
							offsetToRenderSurface += new Vector2(child.OriginRelativeParent.X * currentGraphics2DTransform.sx, child.OriginRelativeParent.Y * currentGraphics2DTransform.sy);

							double yFraction = offsetToRenderSurface.Y - (int)offsetToRenderSurface.Y;
							double xFraction = offsetToRenderSurface.X - (int)offsetToRenderSurface.X;
							int xOffset = (int)Floor(child.LocalBounds.Left);
							int yOffset = (int)Floor(child.LocalBounds.Bottom);

							// Re-decided every paint, so the LCD setting takes effect on the next frame. Both a
							// mode flip and a change to the filter's style parameters force a re-raster: the
							// pixels already in the buffer are in the wrong representation in the first case and
							// rastered under superseded settings in the second.
							BackbufferMode mode = child.ResolveBackbufferMode(graphics2D);

							// Read once, before the raster, and used for both the compare and the store: read
							// again afterwards it would stamp pixels rastered under the old settings with an
							// epoch that says they are current, and a settings change that landed mid-raster
							// would never be re-rastered.
							long lcdEpoch = LcdRenderSettings.Epoch;
							if (mode != child.backbuffer.Mode
								|| (mode == BackbufferMode.LcdCoverage && child.backbuffer.LcdEpoch != lcdEpoch))
							{
								child.isCurrentlyInvalid = true;
							}

							if (child.isCurrentlyInvalid)
							{
								int extraW = xFraction > 0 ? 1 : 0;
								int extraH = yFraction > 0 ? 1 : 0;

								child.backbuffer.Rasterize(
									mode,
									extraW,
									extraH,
									Affine.NewTranslation(-xOffset + xFraction, -yOffset + yFraction));

								child.backbuffer.Mode = mode;
								child.backbuffer.LcdEpoch = lcdEpoch;
								child.isCurrentlyInvalid = false;
							}

							offsetToRenderSurface.X = (int)offsetToRenderSurface.X + xOffset;
							offsetToRenderSurface.Y = (int)offsetToRenderSurface.Y + yOffset;
							// The transform to draw the back-buffer to the graphics2D must not have a factional amount
							// or we will get aliasing in the image and we want our back buffer pixels to map 1:1 to the next buffer
							if (offsetToRenderSurface.X - (int)offsetToRenderSurface.X != 0
								|| offsetToRenderSurface.Y - (int)offsetToRenderSurface.Y != 0)
							{
								BreakInDebugger("The transform for a back buffer must be integer to avoid aliasing.");
							}

							graphics2D.SetTransform(Affine.NewTranslation(offsetToRenderSurface));

							child.backbuffer.CompositeOnto(
								graphics2D,
								offsetToRenderSurface,
								currentGraphics2DTransform.sx,
								currentGraphics2DTransform.sy,
								child.BackbufferOpacity);
						}
						else
						{
							child.OnDrawBackground(graphics2D);
							child.OnDraw(graphics2D);
						}
					}
				}

				graphics2D.PopTransform();
				graphics2D.SetClippingRect(oldClippingRect);

				DrawBorder(graphics2D, child);
			}
		}

		virtual public void OnBeforeDraw(Graphics2D graphics2D)
		{
			BeforeDraw?.Invoke(this, new DrawEventArgs(graphics2D));
		}

		virtual public void OnAfterDraw(Graphics2D graphics2D)
        {
			AfterDraw?.Invoke(this, new DrawEventArgs(graphics2D));
		}

		protected void ShowDebugBounds(Graphics2D graphics2D)
        {
            // draw the padding
            DrawBorderAndPaddingBounds(graphics2D, LocalBounds, DevicePadding, new Color(Cyan, 128));

            // show the bounds and inside with an x
            graphics2D.Line(LocalBounds.Left, LocalBounds.Bottom, LocalBounds.Right, LocalBounds.Top, new Color(Green, 100), 3);
            graphics2D.Line(LocalBounds.Left, LocalBounds.Top, LocalBounds.Right, LocalBounds.Bottom, new Color(Green, 100), 3);
            graphics2D.Rectangle(LocalBounds, Red);

            RenderAnchoreInfo(graphics2D);
        }

        private void RenderAnchoreInfo(Graphics2D graphics2D)
		{
			var color = Color.Cyan;
			double size = 10;

			// an arrow pointing right
			var rightArrow = new VertexStorage();
			rightArrow.MoveTo(new Vector2(size * 2, 0));
			rightArrow.LineTo(new Vector2(size * 1, size * .6));
			rightArrow.LineTo(new Vector2(size * 1, -size * .6));

			if (HAnchor == HAnchor.Absolute)
			{
				// graphics2D.Line(LocalBounds.Center + new Vector2(0, size * .8),
				// LocalBounds.Center + new Vector2(0, -size * .8),
				// color, size * .5);
			}
			else // figure out what it is
			{
				if (HAnchor.HasFlag(HAnchor.Left))
				{
					graphics2D.Render(new VertexSourceApplyTransform(rightArrow, Affine.NewRotation(MathHelper.DegreesToRadians(180))), LocalBounds.Center, color);
				}

				if (HAnchor.HasFlag(HAnchor.Center))
				{
					graphics2D.Circle(LocalBounds.Center, size / 2, color);
				}

				if (HAnchor.HasFlag(HAnchor.Right))
				{
					graphics2D.Render(rightArrow, LocalBounds.Center, color);
				}

				if (HAnchor.HasFlag(HAnchor.Fit))
				{
					// draw the right arrow offset
					var offsetArrow = new VertexSourceApplyTransform(rightArrow, Affine.NewTranslation(-size * 3, 0));
					graphics2D.Render(offsetArrow, LocalBounds.Center, color);
					graphics2D.Render(new VertexSourceApplyTransform(offsetArrow,
						Affine.NewRotation(MathHelper.DegreesToRadians(180))),
						LocalBounds.Center,
						color);
				}
			}

			if (VAnchor == VAnchor.Absolute)
			{
				// graphics2D.Line(LocalBounds.Center + new Vector2(size * .8, 0),
				// LocalBounds.Center + new Vector2(-size * .8, 0),
				// color, size * .5);
			}
			else // figure out what it is
			{
				var upArrow = new VertexSourceApplyTransform(rightArrow, Affine.NewRotation(MathHelper.DegreesToRadians(90)));
				if (VAnchor.HasFlag(VAnchor.Bottom))
				{
					graphics2D.Render(new VertexSourceApplyTransform(upArrow, Affine.NewRotation(MathHelper.DegreesToRadians(180))), LocalBounds.Center, color);
				}

				if (VAnchor.HasFlag(VAnchor.Center))
				{
					graphics2D.Circle(LocalBounds.Center, size / 2, color);
				}

				if (VAnchor.HasFlag(VAnchor.Top))
				{
					graphics2D.Render(upArrow, LocalBounds.Center, color);
				}

				if (VAnchor.HasFlag(VAnchor.Fit))
				{
					// draw the right arrow offset
					var offsetArrow = new VertexSourceApplyTransform(upArrow, Affine.NewTranslation(0, -size * 3));
					graphics2D.Render(offsetArrow, LocalBounds.Center, color);
					graphics2D.Render(new VertexSourceApplyTransform(offsetArrow,
						Affine.NewRotation(MathHelper.DegreesToRadians(180))),
						LocalBounds.Center,
						color);
				}
			}
		}

		private static void DrawBorderAndPaddingBounds(Graphics2D graphics2D, RectangleDouble bounds, BorderDouble border, Color color)
		{
			if (border.Width != 0
				|| border.Height != 0)
			{
				var borderPath = new VertexStorage();
				// put in the bounds
				borderPath.MoveTo(bounds.Left, bounds.Bottom);
				borderPath.LineTo(bounds.Left, bounds.Top);
				borderPath.LineTo(bounds.Right, bounds.Top);
				borderPath.LineTo(bounds.Right, bounds.Bottom);
				borderPath.LineTo(bounds.Left, bounds.Bottom);

				// take out inside the border
				borderPath.MoveTo(bounds.Left + border.Left, bounds.Bottom + border.Bottom);
				borderPath.LineTo(bounds.Right - border.Right, bounds.Bottom + border.Bottom);
				borderPath.LineTo(bounds.Right - border.Right, bounds.Top - border.Top);
				borderPath.LineTo(bounds.Left + border.Left, bounds.Top - border.Top);
				borderPath.LineTo(bounds.Left + border.Left, bounds.Bottom + border.Bottom);
				graphics2D.Render(borderPath, color);
			}
		}

		protected void DrawBorder(Graphics2D graphics2D, GuiWidget child)
		{
			var childDeviceBorder = child.deviceBorder;
			var childBorderColor = child.BorderColor;

			if (childBorderColor == Color.Transparent
				|| (childDeviceBorder.Left == 0
					&& childDeviceBorder.Right == 0
					&& childDeviceBorder.Bottom == 0
					&& childDeviceBorder.Top == 0))
			{
				return;
			}

			var childBounds = child.TransformToParentSpace(this, child.localBounds);
			// bounds = this.localBounds;
			// graphics2D.FillRectangle(bounds, new Color(Color.Cyan, 100));
			// var expand = bounds;
			// expand.Inflate(1);
			// graphics2D.Rectangle(expand, new Color(Color.Magenta, 100));

			if (childDeviceBorder.Left > 0)
			{
				// do a fill rect that does not include the top or bottom
				graphics2D.FillRectangle(childBounds.Left,
					childBounds.Bottom,
					childBounds.Left - childDeviceBorder.Left,
					childBounds.Top,
					childBorderColor);
			}

			if (childDeviceBorder.Bottom > 0)
			{
				// do a fill rect
				graphics2D.FillRectangle(childBounds.Left - childDeviceBorder.Left,
					childBounds.Bottom,
					childBounds.Right + childDeviceBorder.Right,
					childBounds.Bottom - childDeviceBorder.Bottom,
					childBorderColor);
			}

			if (childDeviceBorder.Right > 0)
			{
				// do a fill rect that does not include the top or bottom
				graphics2D.FillRectangle(childBounds.Right + childDeviceBorder.Right,
					childBounds.Bottom,
					childBounds.Right,
					childBounds.Top,
					childBorderColor);
			}

			if (childDeviceBorder.Top > 0)
			{
				// do a fill rect
				graphics2D.FillRectangle(childBounds.Left - childDeviceBorder.Left,
					childBounds.Top + childDeviceBorder.Top,
					childBounds.Right + childDeviceBorder.Right,
					childBounds.Top,
					childBorderColor);
			}
		}

		/// <summary>
		/// A widget's clipping rectangle in screen space, cached and invalidated lazily.
		/// </summary>
		/// <remarks>
		/// The cached rectangle depends on this widget's bounds and transform and on every ancestor's bounds
		/// and transform - never on its children. Invalidation used to be pushed down: writing LocalBounds or
		/// ParentToChildTransform walked the widget's whole subtree setting a dirty flag. On a large tree that
		/// is ruinous (moving the 100 sibling rows of a flow container re-walked 18,000 widgets per row).
		/// <para>
		/// So the push is gone and validity is decided on read instead. Every change stamps only the changed
		/// widget with a value from a process wide counter (<see cref="NextVersion"/>), which is O(1). A read
		/// walks UP to the root - depth, not subtree - and the cache is good only if the largest stamp on that
		/// chain is the one it was built against. Because the counter only ever increases, any change to any
		/// widget on the chain (including a re-parent, which stamps the moved widget) raises that maximum past
		/// anything a cache could have recorded, so a stale cache can never look current.
		/// </para>
		/// <para>
		/// The up-walk is skipped entirely while nothing anywhere has changed - the common case inside a single
		/// paint - by remembering the counter value the cache was last validated at.
		/// </para>
		/// <para>
		/// One thing the stamps cannot see is a widget whose LocalBounds is *derived* rather than stored (a
		/// Slider sizes itself to its track, thumb and value text). Nothing writes those bounds, so nothing
		/// stamps the widget; such widgets must call <see cref="GuiWidget.InvalidateScreenClipping"/> when
		/// whatever they derive from changes.
		/// </para>
		/// <para>
		/// Reads happen on the paint thread and writes during layout, as they always have here, so this is a
		/// single threaded design just like the dirty flag it replaces. The stamps are still written and read
		/// through <see cref="Volatile"/>, so a reader that sees the raised global counter cannot go on to read
		/// a stale (or, on a 32 bit runtime, torn) local stamp and latch a rectangle nothing will ever rebuild.
		/// That is hardening, not a guarantee: nothing here orders a change made on one thread against a paint
		/// already under way on another.
		/// </para>
		/// </remarks>
		internal class ScreenClipping
		{
			private readonly GuiWidget attachedTo;

			/// <summary>
			/// Counts every change to any widget that can move a clipping rectangle. Only its ordering
			/// matters: a fresh value is larger than every value handed out before it.
			/// </summary>
			private static long globalVersion;

			private static long NextVersion() => Interlocked.Increment(ref globalVersion);

			/// <summary>
			/// How many widgets have had their cached clipping stamped stale (plus one per widget ever
			/// constructed, which starts life stamped). Exposed for tests and diagnostics through
			/// <see cref="GuiWidget.ScreenClippingInvalidationCount"/>.
			/// </summary>
			internal static long InvalidationCount => Volatile.Read(ref globalVersion);

			/// <summary>
			/// Stamped afresh whenever this widget alone changes. Never 0, so a zeroed
			/// <see cref="chainVersion"/> reliably means "never built".
			/// </summary>
			private long localVersion = NextVersion();

			/// <summary>
			/// The largest <see cref="localVersion"/> on the chain from this widget to the root at the moment
			/// the cached rectangle was worked out.
			/// </summary>
			private long chainVersion;

			/// <summary>
			/// The <see cref="globalVersion"/> at which this cache was last confirmed current, so that repeated
			/// reads with no intervening change cost one comparison rather than a walk to the root.
			/// </summary>
			private long validatedAtGlobalVersion;

			internal void MarkRecalculate()
			{
				Volatile.Write(ref localVersion, NextVersion());
			}

			internal bool VisibleAfterClipping = true;
			internal RectangleDouble ScreenClippingRect;

			/// <summary>
			/// Where the widget's own local origin lands in screen coordinates, rebuilt beside
			/// <see cref="ScreenClippingRect"/> because it is worked out from the same walk up the parents and
			/// goes stale at exactly the same moments. See <see cref="GuiWidget.ScreenSpaceOrigin"/>.
			/// </summary>
			internal Vector2 ScreenOrigin;

			internal ScreenClipping(GuiWidget attachedTo)
			{
				this.attachedTo = attachedTo;
			}

			/// <summary>
			/// Brings <see cref="ScreenClippingRect"/>, <see cref="ScreenOrigin"/> and
			/// <see cref="VisibleAfterClipping"/> up to date, and answers the version of the chain they now
			/// stand for (which is what this widget's children compare themselves against).
			/// </summary>
			internal long Validate()
			{
				// Read the global first: anything stamped after this point raises it again, so the worst a
				// concurrent change can do is leave this validation standing for a version it no longer
				// describes, which the next read corrects.
				long globalNow = Volatile.Read(ref globalVersion);
				if (validatedAtGlobalVersion == globalNow)
				{
					return chainVersion;
				}

				GuiWidget parent = attachedTo.Parent;
				long parentChainVersion = parent?.screenClipping.Validate() ?? 0;

				long currentChainVersion = Max(parentChainVersion, Volatile.Read(ref localVersion));
				if (currentChainVersion != chainVersion)
				{
					Rebuild(parent);
					chainVersion = currentChainVersion;
				}

				validatedAtGlobalVersion = globalNow;
				return currentChainVersion;
			}

			/// <summary>
			/// Works out the screen rectangle from scratch. The parent's cache is up to date by the time this
			/// runs - <see cref="Validate"/> sees to that - so it can be read directly.
			/// </summary>
			private void Rebuild(GuiWidget parent)
			{
				DrawCount++;

				ScreenClippingRect = attachedTo.TransformToScreenSpace(attachedTo.LocalBounds);
				ScreenOrigin = attachedTo.TransformToScreenSpace(Vector2.Zero);
				VisibleAfterClipping = true;

				if (parent != null)
				{
					ScreenClipping parentClipping = parent.screenClipping;
					if (parentClipping.VisibleAfterClipping)
					{
						var intersectionRect = new RectangleDouble();
						if (intersectionRect.IntersectRectangles(ScreenClippingRect, parentClipping.ScreenClippingRect))
						{
							ScreenClippingRect = intersectionRect;
						}
						else
						{
							// this rect is clipped away by the parent rect
							VisibleAfterClipping = false;
						}
					}
					else
					{
						// the parent is completely clipped away, so this is too.
						VisibleAfterClipping = false;
					}
				}
			}
		}

		/// <summary>
		/// How many times any widget's cached screen clipping has been stamped stale since the process
		/// started. Counted the way <see cref="DrawCount"/> and <see cref="LayoutCount"/> are, for tests and
		/// diagnostics: invalidation is O(1) per changed widget, so this must track the widgets that actually
		/// moved and not the size of the tree they sit in.
		/// </summary>
		public static long ScreenClippingInvalidationCount => ScreenClipping.InvalidationCount;

		/// <summary>
		/// Drops this widget's cached screen clipping rectangle (and, because clipping is validated against
		/// the chain up to the root, every descendant's with it).
		/// </summary>
		/// <remarks>
		/// Writing LocalBounds or ParentToChildTransform does this for you. This exists for the widgets that
		/// never write their bounds because they *derive* them - Slider returns the union of its track, thumb
		/// and value text - since nothing can tell that a derived rectangle has grown. Such a widget must call
		/// this itself whenever one of the things it derives from changes, or it will go on being clipped to
		/// the size it used to be until some ancestor happens to change.
		/// </remarks>
		protected void InvalidateScreenClipping()
		{
			screenClipping.MarkRecalculate();
		}

		protected bool CurrentScreenClipping(out RectangleDouble screenClippingRect)
		{
			screenClipping.Validate();

			screenClippingRect = screenClipping.ScreenClippingRect;
			return screenClipping.VisibleAfterClipping;
		}

		/// <summary>
		/// This widget's local origin in screen coordinates, which is what turns a screen space clipping
		/// rectangle into one the surface being painted can use. See <see cref="DrawChild"/>.
		/// </summary>
		/// <remarks>
		/// Answered from the screen clipping cache rather than by walking the parents on every call: this is
		/// asked once per child per paint, and the walk it replaces is the reason that cache exists.
		/// </remarks>
		private Vector2 ScreenSpaceOrigin()
		{
			// Purely to bring the cache up to date - the rectangle it hands back is not wanted here.
			this.CurrentScreenClipping(out _);

			return screenClipping.ScreenOrigin;
		}

		public void CloseOnIdle()
		{
			if (!HasBeenClosed)
			{
				UiThread.RunOnIdle(() => this.Close());
			}
		}

		public void Close()
		{
			Close(force: false);
		}

		/// <summary>
		/// Closes this widget even if a <see cref="ShouldClose"/> handler would cancel the close.
		/// This exists for the automation watchdog: a window whose close is vetoed (typically to show a
		/// "do you want to save?" dialog) blocks the message pump forever and hangs the entire test run,
		/// so the runner needs a way out. Application code should call <see cref="Close()"/> so the veto is honored.
		/// <para>
		/// Skipping the OnShouldClose path also skips whatever the application normally does there - saving
		/// window size and position, prompting to save changes, and so on. That loss is acceptable on the
		/// failure path this serves (the alternative is a hung test run), but it is why this is for the
		/// automation watchdog only.
		/// </para>
		/// </summary>
		public void ForceClose()
		{
			Close(force: true);
		}

		private void Close(bool force)
		{
			if (childrenLockedInMouseUpCount != 0)
			{
				BreakInDebugger("You should put this close onto the UiThread.RunOnIdle so it can happen after the child list is unlocked.");
			}

			if (HasBeenClosed)
			{
				// already closed don't need to do anything more
				return;
			}

			if (!force)
			{
				// Validate via OnClosing if this should close
				var shouldCloseArgs = new ShouldCloseEventArgs();
				OnShouldClose(shouldCloseArgs);

				if (shouldCloseArgs.Cancel)
				{
					// exit without doing anything
					return;
				}
			}

			// we are closed, there is no turning back
            HasBeenClosed = true;
            
			// let any listeners know we are closed before we remove our children
            OnClosing2(null);

			// close all the children
			this.CloseChildren();

			// let listeners know we are done closing
			OnClosed(null);
			if (Parent != null)
			{
				// This code will only execute if this is the actual widget we called close on (not a child of the widget we called close on).
				Parent.RemoveChild(this);
				this.Parent = null;
			}
		}

		public virtual void OnShouldClose(ShouldCloseEventArgs e)
		{
			ShouldClose?.Invoke(this, e);
		}

        public virtual void OnClosing2(EventArgs eventArgs)
        {
            Closing2?.Invoke(this, eventArgs);
        }
        
		public virtual void OnClosed(EventArgs e)
		{
			Closed?.Invoke(this, e);
		}

		public Vector2 TransformFromParentSpace(GuiWidget parentToGetRelativeTo, Vector2 position)
		{
			GuiWidget parent = Parent;
			while (parent != null
				&& parent != parentToGetRelativeTo)
			{
				position -= new Vector2(parent.BoundsRelativeToParent.Left, parent.BoundsRelativeToParent.Bottom);
				parent = parent.Parent;
			}

			return position;
		}

		public Vector2 TransformToParentSpace(GuiWidget parentToGetRelativeTo, Vector2 inPosition)
		{
			var bPosition = inPosition;
			GuiWidget widgetToTransformBy = this;
			while (widgetToTransformBy != null
				&& widgetToTransformBy != parentToGetRelativeTo)
			{
				bPosition += new Vector2(widgetToTransformBy.BoundsRelativeToParent.Left, widgetToTransformBy.BoundsRelativeToParent.Bottom);
				widgetToTransformBy = widgetToTransformBy.Parent;
			}

			var mPosition = inPosition;
			widgetToTransformBy = this;
			while (widgetToTransformBy != null
				&& widgetToTransformBy != parentToGetRelativeTo)
			{
				mPosition.X += widgetToTransformBy.parentToChildTransform.tx;
				mPosition.Y += widgetToTransformBy.parentToChildTransform.ty;
				widgetToTransformBy = widgetToTransformBy.Parent;
			}

			if (bPosition != mPosition)
			{
			}

			return mPosition;
		}

		public RectangleDouble TransformFromParentSpace(GuiWidget parentToGetRelativeTo, RectangleDouble rectangleToTransform)
		{
			GuiWidget parent = Parent;
			while (parent != null
				&& parent != parentToGetRelativeTo)
			{
				rectangleToTransform.Offset(-parent.BoundsRelativeToParent.Left, -parent.BoundsRelativeToParent.Bottom);
				parent = parent.Parent;
			}

			return rectangleToTransform;
		}

		public RectangleDouble TransformToParentSpace(GuiWidget parentToGetRelativeTo, RectangleDouble rectangleToTransform)
		{
			GuiWidget widgetToTransformBy = this;
			while (widgetToTransformBy != null
				&& widgetToTransformBy != parentToGetRelativeTo)
			{
                widgetToTransformBy.ParentToChildTransform.transform(ref rectangleToTransform);
                widgetToTransformBy = widgetToTransformBy.Parent;
			}

			return rectangleToTransform;
		}

        public Vector2 TransformToScreenSpace(Vector2 vectorToTransform)
        {
            GuiWidget prevGUIWidget = this;

			// Walk until we find a SystemWindow with a null parent or until the topmost GuiWidget
            while (prevGUIWidget != null
                && !(prevGUIWidget is SystemWindow && prevGUIWidget.Parent == null))
            {
				vectorToTransform = prevGUIWidget.ParentToChildTransform.Transform(vectorToTransform);
                prevGUIWidget = prevGUIWidget.Parent;
            }

            return vectorToTransform;
        }

        public GuiWidget TopmostParent()
		{
			if (this.Parent == null)
			{
				return this;
			}
			return this.Parents<SystemWindow>().FirstOrDefault() ?? this.Parents<GuiWidget>().Last();
		}

		public Vector2 TransformFromScreenSpace(Vector2 vectorToTransform)
		{
			return this.TransformFromParentSpace(TopmostParent(), vectorToTransform);
		}

		public RectangleDouble TransformToScreenSpace(RectangleDouble rectangleToTransform)
		{
            return TransformToParentSpace(null, rectangleToTransform);            
		}

		public RectangleDouble TransformFromScreenSpace(RectangleDouble rectangleToTransform)
		{
			return this.TransformFromParentSpace(TopmostParent(), rectangleToTransform);
		}

		protected GuiWidget GetChildContainingFocus()
		{
			foreach (GuiWidget child in Children)
			{
				if (child.ContainsFocus)
				{
					return child;
				}
			}

			return null;
		}

		private void DoMouseMovedOffWidgetRecursive(MouseEventArgs mouseEvent)
		{
			bool needToCallLeaveBounds = UnderMouseState != UI.UnderMouseState.NotUnderMouse;
			bool needToCallLeave = UnderMouseState == UI.UnderMouseState.FirstUnderMouse;

			UnderMouseState = UI.UnderMouseState.NotUnderMouse;

			if (needToCallLeave)
			{
				OnMouseLeave(mouseEvent);
			}

			if (needToCallLeaveBounds)
			{
				OnMouseLeaveBounds(mouseEvent);
			}

			foreach (GuiWidget child in Children)
			{
				double childX = mouseEvent.X;
				double childY = mouseEvent.Y;
				child.ParentToChildTransform.inverse_transform(ref childX, ref childY);
				var childMouseEvent = new MouseEventArgs(mouseEvent, childX, childY);
				child.DoMouseMovedOffWidgetRecursive(childMouseEvent);
			}
		}

		public virtual void OnGestureFling(FlingEventArgs flingEvent)
		{
			if (PositionWithinLocalBounds(flingEvent.X, flingEvent.Y))
			{
				// bool childHasAcceptedThisEvent = false;
				foreach (var child in Children.Reverse())
				{
					if (child.Visible & child.Enabled)
					{
						double childX = flingEvent.X;
						double childY = flingEvent.Y;
						child.ParentToChildTransform.inverse_transform(ref childX, ref childY);
						var childFlingEvent = new FlingEventArgs(childX, childY, flingEvent.Direction);

						if (child.PositionWithinLocalBounds(childFlingEvent.X, childFlingEvent.Y))
						{
							// recurse in
							child.OnGestureFling(childFlingEvent);
						}
					}
				}

				GestureFling?.Invoke(this, flingEvent);
			}
		}

		public virtual void OnMouseDown(MouseEventArgs mouseEvent)
		{
			bool focusStateBeforeProcessing = containsFocus;
			if (PositionWithinLocalBounds(mouseEvent.X, mouseEvent.Y))
			{
				bool willBeInChild = false;

				// figure out what state we will be in when done
				foreach (var child in Children.Reverse())
				{
					double childX = mouseEvent.X;
					double childY = mouseEvent.Y;
					child.ParentToChildTransform.inverse_transform(ref childX, ref childY);
					if (child.Visible
						&& child.Enabled
						&& child.CanSelect
						&& child.PositionWithinLocalBounds(childX, childY))
					{
						willBeInChild = true;
						break;
					}
				}

				if (willBeInChild)
				{
					if (UnderMouseState == UnderMouseState.FirstUnderMouse)
					{
						// set it before we call the function to have the state right to the callee
						UnderMouseState = UI.UnderMouseState.UnderMouseNotFirst;
						OnMouseLeave(mouseEvent);
					}
					else if (UnderMouseState == UnderMouseState.NotUnderMouse)
					{
						UnderMouseState = UI.UnderMouseState.UnderMouseNotFirst;
						OnMouseEnterBounds(mouseEvent);
					}

					UnderMouseState = UI.UnderMouseState.UnderMouseNotFirst;
				}
				else // It is in this but not children. It will be the first under mouse
				{
					if (UnderMouseState == UnderMouseState.NotUnderMouse)
					{
						UnderMouseState = UI.UnderMouseState.FirstUnderMouse;
						OnMouseEnterBounds(mouseEvent);
						OnMouseEnter(mouseEvent);
					}
					else if (UnderMouseState == UnderMouseState.UnderMouseNotFirst)
					{
						UnderMouseState = UI.UnderMouseState.FirstUnderMouse;
						OnMouseEnter(mouseEvent);
					}
				}

				bool childHasAcceptedThisEvent = false;
				bool childHasTakenFocus = false;
				foreach (var child in Children.Reverse())
				{
					double childX = mouseEvent.X;
					double childY = mouseEvent.Y;
					child.ParentToChildTransform.inverse_transform(ref childX, ref childY);

					var childMouseEvent = new MouseEventArgs(mouseEvent, childX, childY);

					// If any previous child has accepted the MouseDown, then we won't continue propagating the event and
					// will attempt to fire MovedOffWidget logic
					if (childHasAcceptedThisEvent)
					{
						// another child already took the down so no one else can.
						child.DoMouseMovedOffWidgetRecursive(childMouseEvent);
					}
					else
					{
						if (child.Visible && child.Enabled && child.CanSelect)
						{
							if (child.PositionWithinLocalBounds(childX, childY))
							{
								childHasAcceptedThisEvent = true;
								child.OnMouseDown(childMouseEvent);
								if (child.ContainsFocus)
								{
									childHasTakenFocus = true;
								}
							}
							else
							{
								child.DoMouseMovedOffWidgetRecursive(childMouseEvent);
								child.Unfocus();
							}
						}
					}
				}

				if (childHasAcceptedThisEvent)
				{
					mouseCapturedState = MouseCapturedState.ChildHasMouseCaptured;
				}
				else
				{
					mouseCapturedState = MouseCapturedState.ThisHasMouseCaptured;

					MouseDownCaptured?.Invoke(this, mouseEvent);
				}

				if (!childHasTakenFocus)
				{
					if (CanFocus)
					{
						Focus();
					}
				}

				try
				{
					InteractionObserved?.Invoke(this, "mouse-down", mouseEvent);
				}
				catch (Exception ex)
				{
					Debug.WriteLine($"Interaction observer failed during mouse down: {ex}");
				}

				MouseDown?.Invoke(this, mouseEvent);
			}

			// not under the mouse
			else if (UnderMouseState != UI.UnderMouseState.NotUnderMouse)
			{
				Unfocus();
				mouseCapturedState = MouseCapturedState.NotCaptured;
				UnderMouseState = UnderMouseState.NotUnderMouse;

				OnMouseLeaveBounds(mouseEvent);
				if (UnderMouseState == UI.UnderMouseState.FirstUnderMouse)
				{
					OnMouseLeave(mouseEvent);
				}

				DoMouseMovedOffWidgetRecursive(mouseEvent);
			}

			LastMouseDownMs = UiThread.CurrentTimerMs;
			lastMouseDownClicks = mouseEvent.Clicks;

			if (focusStateBeforeProcessing != containsFocus)
			{
				OnContainsFocusChanged(new FocusChangedArgs(this, containsFocus));
			}
		}

		/// <summary>
		/// Whether the event being processed belongs to a double click on this widget. Works from
		/// both halves of the second click: the DOWN carries Clicks == 2 from the platform, and for
		/// the matching UP - which the platform reports with Clicks == 1 - the click count recorded
		/// at the down is consulted instead. The time window keeps a press held longer than a real
		/// double click from counting, and confirms the click landed on this control.
		/// </summary>
		public bool IsDoubleClick(MouseEventArgs mouseEvent)
		{
			if ((mouseEvent.Clicks == 2 || lastMouseDownClicks == 2)
				&& LastMouseDownMs > UiThread.CurrentTimerMs - 550)
			{
				return true;
			}

			return false;
		}

		public bool MouseDownOnWidget
		{
			get
			{
				return mouseCapturedState == MouseCapturedState.ThisHasMouseCaptured;
			}
		}

		public static bool TouchScreenMode { get; protected set; }

		internal bool mouseMoveEventHasBeenAcceptedByOther = false;

		public virtual void OnMouseMove(MouseEventArgs mouseEvent)
		{
			mouseMoveEventHasBeenAcceptedByOther = false;

			if (mouseCapturedState == MouseCapturedState.NotCaptured)
			{
				OnMouseMoveNotCaptured(mouseEvent);
			}
			else // either this or a child has the mouse captured
			{
				OnMouseMoveWhenCaptured(mouseEvent);
			}
		}

		public void ValidateMouseCaptureRecursive(GuiWidget lastUpdatedParent = null)
		{
			int countOfChildernThatThinkTheyHaveTheMouseCaptured = 0;
			foreach (GuiWidget child in Children)
			{
				if (child.mouseCapturedState != MouseCapturedState.NotCaptured)
				{
					// keep a count
					countOfChildernThatThinkTheyHaveTheMouseCaptured++;

					// validate that every parent is marked as containing mouse capture
					GuiWidget parent = this.Parent;
					while (parent != null
						&& parent != lastUpdatedParent
						&& this != lastUpdatedParent)
					{
						if (parent.mouseCapturedState != MouseCapturedState.ChildHasMouseCaptured)
						{
							BreakInDebugger("All parents must know a child has the mouse captured.");
						}

						parent = parent.Parent;
					}
				}

				child.ValidateMouseCaptureRecursive(lastUpdatedParent);
			}

			switch (mouseCapturedState)
			{
				case MouseCapturedState.NotCaptured:
				case MouseCapturedState.ThisHasMouseCaptured:
					if (countOfChildernThatThinkTheyHaveTheMouseCaptured != 0)
					{
						BreakInDebugger("No child should have the mouse captured.");
					}

					break;

				case MouseCapturedState.ChildHasMouseCaptured:
					if (countOfChildernThatThinkTheyHaveTheMouseCaptured < 1 || countOfChildernThatThinkTheyHaveTheMouseCaptured > 1)
					{
						BreakInDebugger("One and only one child should ever have the mouse captured.");
					}

					break;

				default:
					throw new NotImplementedException();
			}
		}

		private void OnMouseMoveWhenCaptured(MouseEventArgs mouseEvent)
		{
			if (mouseCapturedState == MouseCapturedState.ChildHasMouseCaptured)
			{
				int countOfChildernThatThinkTheyHaveTheMouseCaptured = 0;
				foreach (GuiWidget child in Children)
				{
					double childX = mouseEvent.X;
					double childY = mouseEvent.Y;
					child.ParentToChildTransform.inverse_transform(ref childX, ref childY);
					var childMouseEvent = new MouseEventArgs(mouseEvent, childX, childY);
					if (child.mouseCapturedState != MouseCapturedState.NotCaptured)
					{
						child.OnMouseMove(childMouseEvent);
						countOfChildernThatThinkTheyHaveTheMouseCaptured++;
					}
				}

				if (countOfChildernThatThinkTheyHaveTheMouseCaptured < 1 || countOfChildernThatThinkTheyHaveTheMouseCaptured > 1)
				{
					BreakInDebugger("One and only one child should ever have the mouse captured.");
				}
			}
			else
			{
				if (mouseCapturedState != MouseCapturedState.ThisHasMouseCaptured)
				{
					BreakInDebugger("You should only ever get here if you have the mouse captured.");
				}

				if (PositionWithinLocalBounds(mouseEvent.X, mouseEvent.Y))
				{
					if (!FirstWidgetUnderMouse)
					{
						UnderMouseState = UI.UnderMouseState.FirstUnderMouse;
						OnMouseEnter(mouseEvent);
						OnMouseEnterBounds(mouseEvent);
					}
					else if (UnderMouseState == UI.UnderMouseState.NotUnderMouse)
					{
						UnderMouseState = UI.UnderMouseState.FirstUnderMouse;
						OnMouseEnterBounds(mouseEvent);
					}

					UnderMouseState = UI.UnderMouseState.FirstUnderMouse;
				}
				else
				{
					if (FirstWidgetUnderMouse)
					{
						UnderMouseState = UI.UnderMouseState.NotUnderMouse;
						OnMouseLeave(mouseEvent);
						OnMouseLeaveBounds(mouseEvent);
					}
					else if (UnderMouseState != UI.UnderMouseState.NotUnderMouse)
					{
						UnderMouseState = UI.UnderMouseState.NotUnderMouse;
						OnMouseLeaveBounds(mouseEvent);
					}

					UnderMouseState = UI.UnderMouseState.NotUnderMouse;
				}

				MouseMove?.Invoke(this, mouseEvent);
			}
		}

		private void OnMouseMoveNotCaptured(MouseEventArgs mouseEvent)
		{
			if (Parent != null && Parent.mouseMoveEventHasBeenAcceptedByOther)
			{
				mouseMoveEventHasBeenAcceptedByOther = true;
			}

			if (PositionWithinLocalBounds(mouseEvent.X, mouseEvent.Y))
			{
				if (mouseMoveEventHasBeenAcceptedByOther)
				{
					if (UnderMouseState == UnderMouseState.FirstUnderMouse)
					{
						// set it before we call the function to have the state right to the callee
						UnderMouseState = UI.UnderMouseState.UnderMouseNotFirst;
						OnMouseLeave(mouseEvent);
					}
					else if (UnderMouseState == UnderMouseState.NotUnderMouse)
					{
						UnderMouseState = UI.UnderMouseState.UnderMouseNotFirst;
						OnMouseEnterBounds(mouseEvent);
					}
				}
				else
				{
					bool willBeInChild = false;

					// figure out what state we will be in when done
					foreach (var child in Children.Reverse())
					{
						double childX = mouseEvent.X;
						double childY = mouseEvent.Y;
						child.ParentToChildTransform.inverse_transform(ref childX, ref childY);
						if (child.Visible
							&& child.Enabled
							&& child.CanSelect
							&& child.PositionWithinLocalBounds(childX, childY))
						{
							willBeInChild = true;
							break;
						}
					}

					if (willBeInChild)
					{
						if (UnderMouseState == UnderMouseState.FirstUnderMouse)
						{
							// set it before we call the function to have the state right to the callee
							UnderMouseState = UI.UnderMouseState.UnderMouseNotFirst;
							OnMouseLeave(mouseEvent);
						}
						else if (UnderMouseState == UnderMouseState.NotUnderMouse)
						{
							UnderMouseState = UI.UnderMouseState.UnderMouseNotFirst;
							OnMouseEnterBounds(mouseEvent);
						}

						UnderMouseState = UI.UnderMouseState.UnderMouseNotFirst;
					}
					else // It is in this but not children. It will be the first under mouse
					{
						if (UnderMouseState == UnderMouseState.NotUnderMouse)
						{
							UnderMouseState = UI.UnderMouseState.FirstUnderMouse;
							OnMouseEnterBounds(mouseEvent);
							OnMouseEnter(mouseEvent);
						}
						else if (UnderMouseState == UnderMouseState.UnderMouseNotFirst)
						{
							UnderMouseState = UI.UnderMouseState.FirstUnderMouse;
							OnMouseEnter(mouseEvent);
						}
					}
				}
			}
			else // mouse is not in this bounds
			{
				if (UnderMouseState != UI.UnderMouseState.NotUnderMouse)
				{
					if (FirstWidgetUnderMouse)
					{
						UnderMouseState = UI.UnderMouseState.NotUnderMouse;
						OnMouseLeave(mouseEvent);
					}

					UnderMouseState = UI.UnderMouseState.NotUnderMouse;
					OnMouseLeaveBounds(mouseEvent);
				}
			}

			MouseMove?.Invoke(this, mouseEvent);

			foreach (var child in Children.Reverse())
			{
				double childX = mouseEvent.X;
				double childY = mouseEvent.Y;
				child.ParentToChildTransform.inverse_transform(ref childX, ref childY);
				var childMouseEvent = new MouseEventArgs(mouseEvent, childX, childY);
				if (child.Visible && child.Enabled && child.CanSelect)
				{
					child.OnMouseMove(childMouseEvent);
					mouseEvent.AcceptDrop |= childMouseEvent.AcceptDrop;
					if (child.UnderMouseState != UnderMouseState.NotUnderMouse)
					{
						mouseMoveEventHasBeenAcceptedByOther = true;
					}
				}
			}
		}

		private int childrenLockedInMouseUpCount = 0;

		public virtual void OnMouseUp(MouseEventArgs mouseEvent)
		{
			if (childrenLockedInMouseUpCount != 0)
			{
				BreakInDebugger("This should not be locked.");
			}

			childrenLockedInMouseUpCount++;

			bool mouseUpOnWidget = PositionWithinLocalBounds(mouseEvent.X, mouseEvent.Y);
			bool childHasAcceptedThisEvent = false;

			if (mouseCapturedState == MouseCapturedState.NotCaptured)
			{
				if (mouseUpOnWidget)
				{
					foreach (var child in Children.Reverse())
					{
						double childX = mouseEvent.X;
						double childY = mouseEvent.Y;
						child.ParentToChildTransform.inverse_transform(ref childX, ref childY);
						var childMouseEvent = new MouseEventArgs(mouseEvent, childX, childY);
						if (child.Visible && child.Enabled && child.CanSelect)
						{
							if (child.PositionWithinLocalBounds(childX, childY))
							{
								childHasAcceptedThisEvent = true;
								child.OnMouseUp(childMouseEvent);
								break;
							}
							else
							{
								if (UnderMouseState != UI.UnderMouseState.NotUnderMouse)
								{
									if (FirstWidgetUnderMouse)
									{
										OnMouseLeave(mouseEvent);
									}

									DoMouseMovedOffWidgetRecursive(mouseEvent);
									UnderMouseState = UI.UnderMouseState.NotUnderMouse;
								}
							}
						}
					}

					if (!childHasAcceptedThisEvent)
					{
						MouseUpCaptured?.Invoke(this, mouseEvent);
					}
				}
			}
			else // either this or a child has the mouse captured
			{
				if (mouseCapturedState == MouseCapturedState.ChildHasMouseCaptured)
				{
					if (childrenLockedInMouseUpCount != 1)
					{
						BreakInDebugger("The mouse should always be locked while in mouse up.");
					}

					int countOfChildernThatThinkTheyHaveTheMouseCaptured = 0;
					foreach (var child in Children)
					{
						if (childrenLockedInMouseUpCount != 1)
						{
							BreakInDebugger("The mouse should always be locked while in mouse up.");
						}

						double childX = mouseEvent.X;
						double childY = mouseEvent.Y;
						child.ParentToChildTransform.inverse_transform(ref childX, ref childY);
						var childMouseEvent = new MouseEventArgs(mouseEvent, childX, childY);
						if (child.mouseCapturedState != MouseCapturedState.NotCaptured)
						{
							if (countOfChildernThatThinkTheyHaveTheMouseCaptured > 0)
							{
								BreakInDebugger("One and only one child should ever have the mouse captured.");
							}

							child.OnMouseUp(childMouseEvent);
							countOfChildernThatThinkTheyHaveTheMouseCaptured++;
						}
					}
				}
				else
				{
					if (mouseCapturedState != MouseCapturedState.ThisHasMouseCaptured)
					{
						BreakInDebugger("You should only ever get here if you have the mouse captured.");
					}

					bool upHappenedAboveChild = false;
					foreach (var child in Children.Reverse())
					{
						double childX = mouseEvent.X;
						double childY = mouseEvent.Y;
						child.ParentToChildTransform.inverse_transform(ref childX, ref childY);
						var childMouseEvent = new MouseEventArgs(mouseEvent, childX, childY);
						if (child.Visible && child.Enabled && child.CanSelect)
						{
							if (child.PositionWithinLocalBounds(childX, childY))
							{
								upHappenedAboveChild = true;
								break;
							}
						}
					}

					if (!upHappenedAboveChild)
					{
						MouseUpCaptured?.Invoke(this, mouseEvent);

						if (mouseUpOnWidget)
						{
							OnClick(mouseEvent);
						}
					}
				}

				if (!mouseUpOnWidget)
				{
					if (UnderMouseState != UI.UnderMouseState.NotUnderMouse)
					{
						if (FirstWidgetUnderMouse)
						{
							UnderMouseState = UI.UnderMouseState.NotUnderMouse;
							OnMouseLeave(mouseEvent);
							OnMouseLeaveBounds(mouseEvent);
						}
						else
						{
							UnderMouseState = UI.UnderMouseState.NotUnderMouse;
							OnMouseLeaveBounds(mouseEvent);
						}

						DoMouseMovedOffWidgetRecursive(mouseEvent);
					}
				}

				ClearCapturedState();
			}

			MouseUp?.Invoke(this, mouseEvent);

			// The press is over, so the remembered down no longer describes a live click. Without
			// this, a single click arriving shortly after a double click could still see the stale
			// 2 through IsDoubleClick (widgets like ListViewItemBase ask during their own
			// OnMouseDown, before the base records the new event's clicks).
			lastMouseDownClicks = 0;

			childrenLockedInMouseUpCount--;

			if (childrenLockedInMouseUpCount != 0)
			{
				BreakInDebugger("This should not be locked.");
			}
		}

		/// <summary>
		/// Fire a mouse click within the bounds of the control
		/// </summary>
		public void InvokeClick()
		{
			this.OnClick(new MouseEventArgs(MouseButtons.Left, 1, new[] { this.Position + Vector2.One }, 0, null));
		}

		protected virtual void OnClick(MouseEventArgs mouseEvent)
		{
			try
			{
				InteractionObserved?.Invoke(this, "click", mouseEvent);
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Interaction observer failed during click: {ex}");
			}

			Click?.Invoke(this, mouseEvent);
		}

		protected virtual void SetCursor(Cursors cursorToSet)
		{
			Parent?.SetCursor(cursorToSet);
		}

		/// <summary>
		/// The mouse has entered the bounds of this widget and is also not over a child widget.
		/// </summary>
		/// <param name="mouseEvent">The mouse event that triggered this event</param>
		public virtual void OnMouseEnter(MouseEventArgs mouseEvent)
		{
			SetCursor(Cursor);

			try
			{
				InteractionObserved?.Invoke(this, "hover", mouseEvent);
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Interaction observer failed during hover: {ex}");
			}

			MouseEnter?.Invoke(this, mouseEvent);
		}

		/// <summary>
		/// The mouse has left the bounds of this widget but it may still be over a child widget.
		/// </summary>
		/// <param name="mouseEvent">The mouse event that triggered this event</param>
		public virtual void OnMouseLeave(MouseEventArgs mouseEvent)
		{
			MouseLeave?.Invoke(this, mouseEvent);
		}

		public void SendToChildren(object objectToRoute)
		{
			foreach (GuiWidget child in Children)
			{
				child.SendToChildren(objectToRoute);
			}

            ObjectSent?.Invoke(this, objectToRoute);
        }

		public class WidgetAndPosition
		{
			public Point2D Position { get; private set; }

			public GuiWidget Widget { get; private set; }

			public string Name { get; private set; }

			public object NamedObject { get; private set; }

			public WidgetAndPosition(GuiWidget widget, Point2D position, string name, object namedObject = null)
			{
				this.Name = name;
				this.Widget = widget;
				this.Position = position;
				if (namedObject == null)
				{
					this.NamedObject = widget;
				}
				else
				{
					this.NamedObject = namedObject;
				}
			}
		}

		public enum SearchType
		{
			Exact,
			Partial
		}

		public List<WidgetAndPosition> FindDescendants(string widgetName)
		{
			return FindDescendants(new string[] { widgetName });
		}

		public List<WidgetAndPosition> FindDescendants(IEnumerable<string> widgetNames)
		{
			return FindDescendants(
				widgetNames,
				new List<WidgetAndPosition>(),
				new RectangleDouble(double.MinValue, double.MinValue, double.MaxValue, double.MaxValue),
				SearchType.Exact);
		}

		// allowDisabledOrHidden - automation tests use this function and may need to find disabled or non-visible items to validate their state
		public virtual List<WidgetAndPosition> FindDescendants(IEnumerable<string> widgetNames, List<WidgetAndPosition> foundChildren, RectangleDouble touchingBounds, SearchType searchType, bool allowDisabledOrHidden = true)
		{
			bool nameFound = false;

			// Loop over name filters, checking for exact or partial matches
			foreach (var widgetName in widgetNames)
			{
				if (searchType == SearchType.Exact)
				{
					if (this.Name == widgetName)
					{
						nameFound = true;
						break;
					}
				}
				else
				{
					if (widgetName == ""
						|| this.Name.Contains(widgetName))
					{
						nameFound = true;
						break;
					}
				}
			}

			if (nameFound)
			{
				if (touchingBounds.IntersectWithRectangle(this.LocalBounds))
				{
					foundChildren.Add(new WidgetAndPosition(this, new Point2D(Width / 2, Height / 2), Name));
				}
			}

			var searchChildren = new List<GuiWidget>(Children);
			foreach (GuiWidget child in searchChildren.Where(child => allowDisabledOrHidden || (child.Visible && child.Enabled)))
			{
				RectangleDouble touchingBoundsRelChild = touchingBounds;
				touchingBoundsRelChild.Offset(-child.OriginRelativeParent);
				child.FindDescendants(widgetNames, foundChildren, touchingBoundsRelChild, searchType, allowDisabledOrHidden);
			}

			return foundChildren;
		}

		public GuiWidget FindDescendant(string nameToSearchFor)
		{
			if (Name == nameToSearchFor)
			{
				return this;
			}

			var searchChildren = new List<GuiWidget>(Children);

			foreach (GuiWidget child in searchChildren)
			{
				GuiWidget namedChild = child.FindDescendant(nameToSearchFor);
				if (namedChild != null)
				{
					return namedChild;
				}
			}

			return null;
		}

		/// <summary>
		/// The mouse has entered the bounds of this widget.  It may also be over a child.
		/// </summary>
		/// <param name="mouseEvent">The mouse event that triggered the enter</param>
		public virtual void OnMouseEnterBounds(MouseEventArgs mouseEvent)
		{
			MouseEnterBounds?.Invoke(this, mouseEvent);
		}

		/// <summary>
		/// The mouse has left the bounds of this widget.
		/// </summary>
		/// <param name="mouseEvent">The mouse event that triggered the leave</param>
		public virtual void OnMouseLeaveBounds(MouseEventArgs mouseEvent)
		{
			MouseLeaveBounds?.Invoke(this, mouseEvent);
		}

		private void ClearCapturedState()
		{
			if (MouseCaptured || ChildHasMouseCaptured)
			{
				foreach (GuiWidget child in Children)
				{
					child.ClearCapturedState();
				}

				mouseCapturedState = MouseCapturedState.NotCaptured;

				GuiWidget parent = this;
				while (parent != null)
				{
					parent.mouseCapturedState = MouseCapturedState.NotCaptured;
					parent = parent.Parent;
				}
			}
		}

		public virtual void OnMouseWheel(MouseEventArgs mouseEvent)
		{
			if (PositionWithinLocalBounds(mouseEvent.X, mouseEvent.Y))
			{
				foreach (var child in Children.Reverse())
				{
					if (child.Visible & child.Enabled)
					{
						double childX = mouseEvent.X;
						double childY = mouseEvent.Y;
						child.ParentToChildTransform.inverse_transform(ref childX, ref childY);
						var childMouseEvent = new MouseEventArgs(mouseEvent, childX, childY);

						if (child.PositionWithinLocalBounds(childMouseEvent.X, childMouseEvent.Y))
						{
							// recurse in
							child.OnMouseWheel(childMouseEvent);
							mouseEvent.WheelDelta = childMouseEvent.WheelDelta;
							// Both axes are consumed by zeroing, so both have to come back out of the child's
							// copy of the event or a widget that ate the sideways scroll would see it acted on
							// again by an ancestor.
							mouseEvent.WheelDeltaX = childMouseEvent.WheelDeltaX;
						}
					}
				}

				MouseWheel?.Invoke(this, mouseEvent);
			}
		}

		/// <summary>
		/// Occurs when a character. space or backspace key is pressed while the control has focus.
		/// base.OnKeyPress should always be called first during override to ensure we get the correct Handled state
		/// </summary>
		/// <param name="keyPressEvent">The key event we are receiving.</param>
		public virtual void OnKeyPress(KeyPressEventArgs keyPressEvent)
		{
			GuiWidget childWithFocus = GetChildContainingFocus();
			if (childWithFocus != null && childWithFocus.Visible && childWithFocus.Enabled)
			{
				childWithFocus.OnKeyPress(keyPressEvent);
			}

			KeyPressed?.Invoke(this, keyPressEvent);
		}

		/// <summary>
		/// Gets all active descendants marked as TabStops
		/// </summary>
		/// <returns>A populated list of active TabStop descendants</returns>
		protected List<GuiWidget> ActiveTabStops()
		{
			var tabStops = new List<GuiWidget>();
			this.ActiveTabStops(tabStops);

			return tabStops;
		}

		private void ActiveTabStops(List<GuiWidget> tabStops)
		{
			foreach (GuiWidget child in Children)
			{
				if (child.Visible
					&& child.Selectable
					&& child.Enabled)
				{
					child.ActiveTabStops(tabStops);
				}
			}

			if (this.TabStop)
			{
				tabStops.Add(this);
			}
		}

		protected void AdvanceFocus(int andvanceAmount)
		{
			if (Parent != null)
			{
				GuiWidget topParent = Parent;
				while (topParent != null && topParent.Parent != null)
				{
					topParent = topParent.Parent;
				}

				var tabStops = topParent.ActiveTabStops();

				if (tabStops.Count > 0)
				{
					// Order by TabIndex
					tabStops = tabStops.OrderBy(t => t.TabIndex).ToList();

					int currentIndex = tabStops.IndexOf(this);
					int nextIndex = (currentIndex + andvanceAmount) % tabStops.Count;
					if (nextIndex < 0)
					{
						nextIndex += tabStops.Count;
					}

					if (currentIndex != nextIndex)
					{
						tabStops[nextIndex].Focus();
						tabStops[nextIndex].OnKeyDown(new KeyEventArgs(Keys.A | Keys.Control));
					}
				}
			}
		}

		protected void FocusNext()
		{
			AdvanceFocus(1);
		}

		protected void FocusPrevious()
		{
			AdvanceFocus(-1);
		}

		/// <summary>
		/// Occurs when a character. space or backspace key is pressed while the control has focus.
		/// base.OnKeyDown should always be called first during override to ensure we get the correct Handled state
		/// </summary>
		/// <param name="keyEvent">The key event being received.</param>
		public virtual void OnKeyDown(KeyEventArgs keyEvent)
		{
			GuiWidget childWithFocus = GetChildContainingFocus();

			if (childWithFocus != null && childWithFocus.Visible && childWithFocus.Enabled)
			{
				childWithFocus.OnKeyDown(keyEvent);
			}

			if (!keyEvent.Handled && keyEvent.KeyCode == Keys.Tab && ContainsFocus)
			{
				if (keyEvent.Shift)
				{
					FocusPrevious();
				}
				else
				{
					FocusNext();
				}

				keyEvent.Handled = true;
				keyEvent.SuppressKeyPress = true;
			}

			KeyDown?.Invoke(this, keyEvent);
		}

		/// <summary>
		/// Occurs when a character. space or backspace key is released while the control has focus.
		/// base.OnKeyUp should always be called first during override to ensure we get the correct Handled state
		/// </summary>
		/// <param name="keyEvent">The key event being received.</param>
		public virtual void OnKeyUp(KeyEventArgs keyEvent)
		{
			GuiWidget childWithFocus = GetChildContainingFocus();
			if (childWithFocus != null && childWithFocus.Visible && childWithFocus.Enabled)
			{
				childWithFocus.OnKeyUp(keyEvent);
			}

			KeyUp?.Invoke(this, keyEvent);
		}

		public bool Equals(GuiWidget other)
		{
			return base.Equals(other);
		}
	}

	public static class ExtensionMethods
	{
		/// <summary>
		/// Returns all children of the current GuiWiget matching the given type
		/// </summary>
		/// <typeparam name="T">The type filter</typeparam>
		/// <param name="widget">The context widget</param>
		/// <returns>All matching child widgets</returns>
		public static IEnumerable<T> Children<T>(this GuiWidget widget) where T : GuiWidget
		{
			return widget.Children.OfType<T>();
		}

		public static IEnumerable<GuiWidget> DescendantsAndSelf(this GuiWidget widget)
		{
			return DescendantsAndSelf<GuiWidget>(widget);
		}

		/// <summary>
		/// Returns all descendants and this of the current GuiWiget matching the given type
		/// </summary>
		/// <typeparam name="T">The type filter</typeparam>
		/// <param name="widget">The context widget</param>
		/// <returns>All matching child widgets</returns>
		public static IEnumerable<T> DescendantsAndSelf<T>(this GuiWidget widget) where T : GuiWidget
		{
			var items = new Stack<GuiWidget>();
			items.Push(widget);

			while (items.Any())
			{
				GuiWidget item = items.Pop();

				foreach (var child in item.Children)
				{
					items.Push(child);
				}

				if (item is T itemIsType)
				{
					yield return itemIsType;
				}
			}
		}

		public static IEnumerable<GuiWidget> Descendants(this GuiWidget widget)
		{
			return Descendants<GuiWidget>(widget);
		}

		public enum ReturnOrder
		{
			BredthFirst,
			DepthFirst
		}

		/// <summary>
		/// Returns all descendants of the current GuiWiget matching the given type
		/// </summary>
		/// <typeparam name="T">The type filter</typeparam>
		/// <param name="widget">The context widget</param>
		/// <param name="evaluate">Determines if a given child widget should be added or descended.</param>
		/// <returns>All matching child widgets</returns>
		public static IEnumerable<T> Descendants<T>(this GuiWidget widget,
			Func<GuiWidget, bool> evaluate = null) where T : GuiWidget
		{
			var items = new Stack<GuiWidget>(widget.Children);

			while (items.Any())
			{
				GuiWidget item = items.Pop();

				foreach (var child in item.Children.Reverse())
				{
					if (evaluate == null
						|| evaluate(child))
					{
						items.Push(child);
					}
				}

				if (item is T itemIsType)
				{
					yield return itemIsType;
				}
			}
		}

		/// <summary>
		/// Returns all ancestors of the current GuiWidget matching the given type
		/// </summary>
		/// <typeparam name="T">The type filter</typeparam>
		/// <param name="widget">The context widget</param>
		/// <returns>The matching ancestor widgets</returns>
		public static IEnumerable<T> Parents<T>(this GuiWidget widget) where T : GuiWidget
		{
			GuiWidget context = widget.Parent;
			while (context != null)
			{
				if (context is T)
				{
					yield return (T)context;
				}

				context = context.Parent;
			}
		}
	}

    public class GuiWidgetEventArgs : EventArgs
    {
        public GuiWidget Child { get; private set; }

        public GuiWidgetEventArgs(GuiWidget child)
        {
            Child = child;
        }
    }
}