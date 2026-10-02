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

// What the browser says about the canvas's size, both ways it can say it: devicePixelContentBoxSize (NaN
// where the engine has none, or there is no observer entry) and the CSS content box, with the ratio between
// them. Which one to believe, and the rounding of a fractional layout into whole pixels, is decided by
// BrowserBacking.FromDeviceMetrics on the managed side - Chrome's device-scale emulation reports a device box
// at the CSS size while devicePixelRatio is 2, so the device box cannot simply be trusted. The managed side
// then sizes the backing store through setCanvasBackingSize, so what agg is told and the canvas agree.
function measureCanvas(canvas, entry) {
	let devicePixelWidth = NaN;
	let devicePixelHeight = NaN;
	if (entry && entry.devicePixelContentBoxSize && entry.devicePixelContentBoxSize.length > 0) {
		const box = entry.devicePixelContentBoxSize[0];
		devicePixelWidth = box.inlineSize;
		devicePixelHeight = box.blockSize;
	}

	// The observer's content box is fractional, which is what lets the device box be checked against it to
	// within the browser's own rounding; clientWidth is the integer fallback.
	let cssWidth = canvas.clientWidth;
	let cssHeight = canvas.clientHeight;
	if (entry && entry.contentBoxSize && entry.contentBoxSize.length > 0) {
		cssWidth = entry.contentBoxSize[0].inlineSize;
		cssHeight = entry.contentBoxSize[0].blockSize;
	}

	return {
		devicePixelWidth: devicePixelWidth,
		devicePixelHeight: devicePixelHeight,
		cssWidth: cssWidth,
		cssHeight: cssHeight,
		devicePixelRatio: window.devicePixelRatio || 1,
	};
}

function applyBackingSize(state, entry) {
	if (state.onResize) {
		state.onResize(measureCanvas(state.canvas, entry));
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
		pointerId: e.pointerId | 0,
		pointerType: e.pointerType || '',
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
		pointerId: 0,
		pointerType: '',
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
		pointerId: 0,
		pointerType: '',
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
		pointerId: 0,
		pointerType: '',
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
 * Prepares the canvas to be an agg window and reports what measureCanvas does, as
 * [devicePixelWidth, devicePixelHeight, cssWidth, cssHeight, devicePixelRatio]. The managed side decides the
 * backing size from that and sets it with setCanvasBackingSize.
 *
 * tabIndex is what makes a canvas able to hold keyboard focus at all; touch-action none stops a touch drag
 * scrolling the page out from under a gesture agg is tracking; user-select none stops a double click
 * selecting the page's text (the -webkit- copy is for older WebKit, which ignores the unprefixed property);
 * outline none hides the focus ring the app draws itself. A transparent -webkit-tap-highlight-color stops mobile
 * Chrome flashing its blue tap highlight on every tap, which covers the whole app because the app is one canvas;
 * -webkit-touch-callout none stops a long press popping the browser's save-image menu over the canvas.
 */
export function bindCanvas(selector) {
	const canvas = resolveCanvas(selector);

	if (!canvas.hasAttribute('tabindex')) {
		canvas.tabIndex = 0;
	}

	canvas.style.touchAction = 'none';
	canvas.style.userSelect = 'none';
	canvas.style.webkitUserSelect = 'none';
	canvas.style.outline = 'none';
	canvas.style.webkitTapHighlightColor = 'transparent';
	canvas.style.webkitTouchCallout = 'none';

	const m = measureCanvas(canvas, null);
	return [m.devicePixelWidth, m.devicePixelHeight, m.cssWidth, m.cssHeight, m.devicePixelRatio];
}

/**
 * Sizes the canvas's backing store to the device pixels the managed side decided on. Only when it changes:
 * assigning width or height clears the canvas even to the same value.
 */
export function setCanvasBackingSize(selector, pixelWidth, pixelHeight) {
	const state = canvasStates.get(selector);
	const canvas = state ? state.canvas : resolveCanvas(selector);

	if (canvas.width !== pixelWidth || canvas.height !== pixelHeight) {
		canvas.width = pixelWidth;
		canvas.height = pixelHeight;
	}
}

/**
 * Subscribes every listener the host needs. onInputEvent takes one packed object; onResize takes
 * measureCanvas's object.
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
		// pointerup, without which a widget stays convinced its button is still held. agg's own arbiter sits
		// on top of it as the Safari hedge (BrowserSystemWindow's mouseCapture); the two agree, because a
		// button only becomes agg's through a down inside the canvas, which is also the only place this
		// takes a capture.
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

// ---------------------------------------------------------------------------------------------------
// File drag-and-drop
// ---------------------------------------------------------------------------------------------------

// How much of a dropped file is read per turn of the event loop. Small enough that no one read stalls the
// page, large enough that a video of hundreds of megabytes is not thousands of round trips into managed code.
const DROP_CHUNK_BYTES = 4 * 1024 * 1024;

// Every field BrowserFileDropEvents reads, on every event - the same contract the input events keep.
function packFileDragEvent(type, fields) {
	return Object.assign({
		type: type,
		offsetX: 0,
		offsetY: 0,
		names: '',
		types: '',
		name: '',
		size: 0,
		bytes: null,
	}, fields);
}

// Only a drag that carries files is agg's; a text or link drag from elsewhere on the page is left alone.
function carriesFiles(e) {
	return !!(e.dataTransfer && Array.from(e.dataTransfer.types || []).includes('Files'));
}

// Newline-joined because a marshalled object has no string-array getter; a newline inside a name becomes a
// space first so the managed split is exact.
function joinLines(values) {
	return values.map((v) => String(v || '').replace(/[\r\n]/g, ' ')).join('\n');
}

// What a hover can know: per dragged file, its name (hidden by every engine for a real drag - only a
// synthetic one or a drop reveals it) and its MIME type (usually there). See BrowserFileDrop's remarks.
function describeDraggedFiles(dataTransfer) {
	const names = [];
	const types = [];

	const files = Array.from(dataTransfer.files || []);
	if (files.length > 0) {
		for (const file of files) {
			names.push(file.name);
			types.push(file.type);
		}
	} else {
		for (const item of Array.from(dataTransfer.items || [])) {
			if (item.kind === 'file') {
				names.push('');
				types.push(item.type);
			}
		}
	}

	return { names: joinLines(names), types: joinLines(types) };
}

// Reads one dropped file a slice at a time, handing each slice over as it arrives. Each byte is read from
// the File exactly once and the managed side appends it straight to the staged file, so a large video is
// never held whole anywhere but the browser's own File.
async function streamDroppedFile(file, onFileDrag) {
	onFileDrag(packFileDragEvent('dropfile', { name: file.name, size: file.size }));

	try {
		for (let start = 0; start < file.size; start += DROP_CHUNK_BYTES) {
			const slice = await file.slice(start, start + DROP_CHUNK_BYTES).arrayBuffer();
			onFileDrag(packFileDragEvent('dropchunk', { bytes: new Uint8Array(slice) }));
		}
	} catch (error) {
		onFileDrag(packFileDragEvent('dropfileerror', { name: error && error.message ? error.message : String(error) }));
	}
}

/**
 * Subscribes the canvas's drag-and-drop listeners; call after attachInput, whose state they join so
 * detachInput removes them. onFileDrag takes packFileDragEvent's object and answers a dragover with true
 * for "copy".
 *
 * Every file drag's dragover and drop are preventDefault'ed, whatever agg answers: a drop the page does not
 * cancel is a navigation to the file, which throws the whole app away. The same goes for a file dropped
 * beside the canvas - on the page's margin or a status line - so the window gets a guard that refuses it
 * (dropEffect none) rather than letting the browser open it.
 */
export function attachFileDrop(selector, onFileDrag) {
	const state = canvasStates.get(selector);
	if (!state) {
		throw new Error(`attachFileDrop('${selector}') needs attachInput first.`);
	}

	// Drops are staged strictly one after another, so a second drop landing while a large first one is still
	// streaming cannot interleave its slices into the first one's files.
	state.dropChain = Promise.resolve();

	const on = (type, handler) => {
		state.canvas.addEventListener(type, handler);
		state.listeners.push({ target: state.canvas, type, handler, options: undefined });
	};

	const hover = (e) => {
		if (!carriesFiles(e)) {
			return;
		}

		e.preventDefault();

		const described = describeDraggedFiles(e.dataTransfer);
		const accepted = onFileDrag(packFileDragEvent('dragover', {
			offsetX: e.offsetX,
			offsetY: e.offsetY,
			names: described.names,
			types: described.types,
		}));

		e.dataTransfer.dropEffect = accepted ? 'copy' : 'none';
	};

	// The window-level guard. Canvas events are the canvas's own listeners' business; everything else that
	// carries files is refused, not opened.
	const guard = (e) => {
		if (e.target === state.canvas || !carriesFiles(e)) {
			return;
		}

		e.preventDefault();
		e.dataTransfer.dropEffect = 'none';
	};

	for (const type of ['dragover', 'drop']) {
		window.addEventListener(type, guard);
		state.listeners.push({ target: window, type, handler: guard, options: undefined });
	}

	on('dragenter', hover);
	on('dragover', hover);

	on('dragleave', (e) => {
		if (carriesFiles(e)) {
			onFileDrag(packFileDragEvent('dragleave', {}));
		}
	});

	on('drop', (e) => {
		if (!carriesFiles(e)) {
			return;
		}

		e.preventDefault();

		// Taken now: the DataTransfer is emptied once this listener returns.
		const files = Array.from(e.dataTransfer.files || []);
		const offsetX = e.offsetX;
		const offsetY = e.offsetY;

		state.dropChain = state.dropChain.then(async () => {
			onFileDrag(packFileDragEvent('dropstart', { offsetX, offsetY }));

			try {
				for (const file of files) {
					await streamDroppedFile(file, onFileDrag);
				}
			} finally {
				onFileDrag(packFileDragEvent('dropend', {}));
			}
		}).catch((error) => {
			// Every link ends here, so one drop that threw cannot leave the chain rejected - which would
			// silently skip every drop after it for the rest of the session.
			console.error('agg file drop failed:', error);
		});
	});
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
