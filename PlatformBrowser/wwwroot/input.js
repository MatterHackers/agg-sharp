// Copyright (c) 2026 Lars Brubaker, MatterHackers Inc.
//
// The canvas host module: everything BrowserSystemWindow needs the DOM for. Listeners, focus, cursor and
// the page title all live here rather than being split across modules, because they are all writes to (or
// reads of) the one element the listeners already hold, and two modules is the shortest import list a host
// page has to await.
//
// The managed side is handed the events already picked apart into a plain object, because a marshalled
// delegate takes at most three arguments and a pointer event has ten. Every field of that object is filled
// on every event - including the ones the event has no use for - so managed code never has to ask whether a
// property is there. That is the contract with BrowserInputEvents.cs; keep the two in step.

const canvasStates = new Map();

function resolveCanvas(selector) {
	const canvas = document.querySelector(selector);
	if (!canvas) {
		throw new Error(`agg canvas '${selector}' was not found in the document.`);
	}

	return canvas;
}

// The one place a fractional CSS layout becomes a whole number of device pixels. devicePixelContentBoxSize
// is the browser's own exact integer answer and is preferred wherever it exists; clientWidth * dpr is the
// fallback for engines that do not report it yet (Safari, historically), and is rounded here so that the
// canvas backing store this sets and the size managed code is told can never disagree.
function measureDevicePixels(canvas, entry) {
	const dpr = window.devicePixelRatio || 1;

	if (entry && entry.devicePixelContentBoxSize && entry.devicePixelContentBoxSize.length > 0) {
		const box = entry.devicePixelContentBoxSize[0];
		return [box.inlineSize, box.blockSize, dpr];
	}

	return [Math.round(canvas.clientWidth * dpr), Math.round(canvas.clientHeight * dpr), dpr];
}

// The backing store has to be sized here, next to the measurement, for the same reason the rounding is:
// managed code is told these exact numbers and lays the whole UI out against them.
function applyBackingSize(state, entry) {
	const [width, height, dpr] = measureDevicePixels(state.canvas, entry);

	const pixelWidth = Math.max(1, width);
	const pixelHeight = Math.max(1, height);

	if (state.canvas.width !== pixelWidth || state.canvas.height !== pixelHeight) {
		state.canvas.width = pixelWidth;
		state.canvas.height = pixelHeight;
	}

	if (state.onResize) {
		state.onResize(pixelWidth, pixelHeight, dpr);
	}
}

// A devicePixelRatio change with no size change - dragging the window to a display with a different scale,
// or the user zooming the page - resizes nothing and so notifies no ResizeObserver. A media query on the
// current resolution does fire, once, and then has to be replaced because the resolution it asks about has
// moved. Same trick the browser's own documentation gives for watching dpr.
function watchDevicePixelRatio(state) {
	if (state.dprQuery) {
		state.dprQuery.removeEventListener('change', state.dprListener);
		state.dprQuery = null;
	}

	const dpr = window.devicePixelRatio || 1;

	state.dprQuery = window.matchMedia(`(resolution: ${dpr}dppx)`);
	state.dprListener = () => {
		applyBackingSize(state, null);
		watchDevicePixelRatio(state);
	};

	state.dprQuery.addEventListener('change', state.dprListener, { once: true });
}

// Browser chords that stay the browser's. Everything else a keydown carries is swallowed, the way the
// desktop hosts swallow keys, so that Space does not scroll the page, Backspace does not navigate back and
// Tab does not walk out of the canvas - agg owns the keyboard while the app has focus.
//
// The carve-outs are the ones a user would rightly be angry to lose, and the ones the browser reserves at a
// level preventDefault cannot reach anyway (calling preventDefault on those is not harmful, it is simply
// ignored - they are listed so the policy is written down rather than discovered):
//
//   reload            F5, Ctrl/Cmd+R
//   fullscreen        F11
//   devtools          F12, Ctrl+Shift+I, Cmd+Alt+I
//   tab and window    Ctrl/Cmd+T, Ctrl/Cmd+W, Ctrl/Cmd+N
//   address bar       Ctrl/Cmd+L
//   quit              Cmd+Q
//   tab switching     Ctrl+Tab, Ctrl+Shift+Tab
//
// Anything an application wants that collides with one of these has to be re-bound; the app cannot win that
// argument with the browser. Note what is NOT carved out: Ctrl/Cmd+P, +S, +O, +F, +Z and the rest of the
// editing chords are the application's, which is the whole reason a canvas app takes the keyboard at all.
function isBrowserReservedChord(e) {
	// An IME is mid-composition; the keystroke belongs to it and preventing it breaks composition outright.
	if (e.isComposing) {
		return true;
	}

	const accel = e.ctrlKey || e.metaKey;

	switch (e.code) {
		case 'F5':
		case 'F11':
		case 'F12':
			return true;
		case 'KeyR':
		case 'KeyT':
		case 'KeyW':
		case 'KeyN':
		case 'KeyL':
			return accel;
		case 'KeyQ':
			return e.metaKey;
		case 'KeyI':
			return (e.ctrlKey && e.shiftKey) || (e.metaKey && e.altKey);
		case 'Tab':
			return e.ctrlKey;
		default:
			return false;
	}
}

// Every field managed code reads, on every event. See the header.
function packPointerEvent(type, e) {
	return {
		type: type,
		offsetX: e.offsetX,
		offsetY: e.offsetY,
		button: typeof e.button === 'number' ? e.button : -1,
		buttons: e.buttons | 0,
		detail: e.detail | 0,
		deltaX: 0,
		deltaY: 0,
		deltaMode: 0,
		code: '',
		key: '',
		ctrlKey: !!e.ctrlKey,
		shiftKey: !!e.shiftKey,
		altKey: !!e.altKey,
		metaKey: !!e.metaKey,
	};
}

function packWheelEvent(e) {
	return {
		type: 'wheel',
		offsetX: e.offsetX,
		offsetY: e.offsetY,
		button: -1,
		buttons: e.buttons | 0,
		detail: 0,
		deltaX: e.deltaX,
		deltaY: e.deltaY,
		deltaMode: e.deltaMode | 0,
		code: '',
		key: '',
		ctrlKey: !!e.ctrlKey,
		shiftKey: !!e.shiftKey,
		altKey: !!e.altKey,
		metaKey: !!e.metaKey,
	};
}

function packKeyEvent(type, e) {
	return {
		type: type,
		offsetX: 0,
		offsetY: 0,
		button: -1,
		buttons: 0,
		detail: 0,
		deltaX: 0,
		deltaY: 0,
		deltaMode: 0,
		code: e.code || '',
		key: e.key || '',
		ctrlKey: !!e.ctrlKey,
		shiftKey: !!e.shiftKey,
		altKey: !!e.altKey,
		metaKey: !!e.metaKey,
	};
}

function packBlurEvent() {
	return {
		type: 'blur',
		offsetX: 0,
		offsetY: 0,
		button: -1,
		buttons: 0,
		detail: 0,
		deltaX: 0,
		deltaY: 0,
		deltaMode: 0,
		code: '',
		key: '',
		ctrlKey: false,
		shiftKey: false,
		altKey: false,
		metaKey: false,
	};
}

/**
 * Prepares the canvas to be an agg window and reports [width, height, devicePixelRatio] in device pixels.
 *
 * tabIndex is what makes a canvas able to hold keyboard focus at all; touch-action none stops a touch drag
 * scrolling the page out from under a gesture agg is tracking; user-select none stops a double click
 * selecting the page's text; outline none hides the focus ring the app draws itself.
 */
export function bindCanvas(selector) {
	const canvas = resolveCanvas(selector);

	if (!canvas.hasAttribute('tabindex')) {
		canvas.tabIndex = 0;
	}

	canvas.style.touchAction = 'none';
	canvas.style.userSelect = 'none';
	canvas.style.outline = 'none';

	const [width, height, dpr] = measureDevicePixels(canvas, null);

	canvas.width = Math.max(1, width);
	canvas.height = Math.max(1, height);

	return [canvas.width, canvas.height, dpr];
}

/**
 * Subscribes every listener the host needs. onInputEvent takes one packed object; onResize takes the
 * canvas's device-pixel width, height and devicePixelRatio.
 */
export function attachInput(selector, onInputEvent, onResize) {
	detachInput(selector);

	const canvas = resolveCanvas(selector);

	const state = {
		canvas: canvas,
		onResize: onResize,
		listeners: [],
		observer: null,
		dprQuery: null,
		dprListener: null,
	};

	const on = (target, type, handler, options) => {
		target.addEventListener(type, handler, options);
		state.listeners.push({ target, type, handler, options });
	};

	const sendPointer = (type) => (e) => {
		onInputEvent(packPointerEvent(type, e));
	};

	on(canvas, 'pointerdown', (e) => {
		// The native capture, so a drag that leaves the canvas keeps delivering - most importantly its
		// pointerup, without which a widget stays convinced its button is still held. (W3 S4 adds agg's own
		// arbiter on top as the Safari hedge; the two agree because a button only becomes agg's through a
		// down inside the canvas.)
		if (canvas.setPointerCapture) {
			try {
				canvas.setPointerCapture(e.pointerId);
			} catch {
				// Safari drops the capture on some gestures and throws on others; the arbiter covers it.
			}
		}

		// Where keyboard focus actually comes from in a page: clicking the canvas.
		canvas.focus();

		// Stops the press starting a text selection or a native element drag.
		e.preventDefault();

		onInputEvent(packPointerEvent('pointerdown', e));
	});

	on(canvas, 'pointerup', sendPointer('pointerup'));
	on(canvas, 'pointercancel', sendPointer('pointercancel'));
	on(canvas, 'pointermove', sendPointer('pointermove'));
	on(canvas, 'pointerleave', sendPointer('pointerleave'));

	// passive:false, or preventDefault is ignored and the page scrolls (and pinches) underneath the app.
	on(canvas, 'wheel', (e) => {
		e.preventDefault();
		onInputEvent(packWheelEvent(e));
	}, { passive: false });

	// The context menu is agg's to draw, not the browser's to pop.
	on(canvas, 'contextmenu', (e) => e.preventDefault());

	// On the canvas rather than on window: an app that puts a real DOM input beside the canvas must keep
	// its typing, and the canvas has focus whenever agg is what the user is using.
	on(canvas, 'keydown', (e) => {
		if (!isBrowserReservedChord(e)) {
			e.preventDefault();
		}

		onInputEvent(packKeyEvent('keydown', e));
	});

	on(canvas, 'keyup', (e) => {
		onInputEvent(packKeyEvent('keyup', e));
	});

	// A modifier released while the page was not looking sends no event at all, so what was held has to be
	// let go of here or it is reported as held forever.
	on(canvas, 'blur', () => {
		onInputEvent(packBlurEvent());
	});

	if (typeof ResizeObserver !== 'undefined') {
		state.observer = new ResizeObserver((entries) => {
			applyBackingSize(state, entries && entries.length > 0 ? entries[0] : null);
		});

		try {
			// The exact box, which is the whole point; not every engine accepts the option, and the ones that
			// do not throw rather than ignoring it.
			state.observer.observe(canvas, { box: 'device-pixel-content-box' });
		} catch {
			state.observer.observe(canvas);
		}
	} else {
		on(window, 'resize', () => applyBackingSize(state, null));
	}

	watchDevicePixelRatio(state);

	canvasStates.set(selector, state);
}

/** Removes everything attachInput added. A closed window must stop swallowing the page's keystrokes. */
export function detachInput(selector) {
	const state = canvasStates.get(selector);
	if (!state) {
		return;
	}

	for (const { target, type, handler, options } of state.listeners) {
		target.removeEventListener(type, handler, options);
	}

	if (state.observer) {
		state.observer.disconnect();
	}

	if (state.dprQuery) {
		state.dprQuery.removeEventListener('change', state.dprListener);
	}

	canvasStates.delete(selector);
}

export function setCanvasCursor(selector, cssCursor) {
	const canvas = document.querySelector(selector);
	if (canvas) {
		canvas.style.cursor = cssCursor;
	}
}

export function setDocumentTitle(title) {
	document.title = title;
}

export function focusCanvas(selector) {
	const canvas = document.querySelector(selector);
	if (canvas) {
		canvas.focus();
	}
}
