// Copyright (c) 2026 Lars Brubaker, MatterHackers Inc.
//
// The providers' module: everything the clipboard, the file dialogs and the OS-information provider need
// the page for. The split from input.js is by owner, not by size - input.js is the canvas host, and every
// listener in it holds the one canvas element. Nothing here touches the canvas: the clipboard is on
// navigator, the file picker is a detached <input>, the screen is window.screen, and the clipboard's
// refresh is a window focus listener that the canvas host has no business knowing about.
//
// Everything here is asynchronous underneath and synchronous on the surface, because the managed
// interfaces on the other side (ISystemClipboard, IFileDialogProvider) are synchronous and cannot be
// changed. So the calls start work and return; results come back through the callbacks managed code hands
// over, the same way input.js delivers events. Failures go to setFaultReporter's callback, without which
// they would be visible only in the browser console.

let reportFault = null;

function fault(what, error) {
	const message = error && error.message ? error.message : String(error);

	if (reportFault) {
		reportFault(what, message);
	} else {
		console.error(`agg ${what} failed before a fault reporter was installed:`, error);
	}
}

/** Gives managed code somewhere to hear about failures that happen inside this module's promises. */
export function setFaultReporter(onFault) {
	reportFault = onFault;
}

// ---------------------------------------------------------------------------------------------------
// Clipboard
// ---------------------------------------------------------------------------------------------------

let clipboardWatch = null;

// Reading the clipboard is gated on the document being focused, and in some browsers on a user gesture as
// well, so a refused read is an ordinary outcome and not a fault - reporting it would file a crash report
// every time a user alt-tabbed. The managed cache ignores an empty answer for the same reason.
function refreshClipboard() {
	if (!navigator.clipboard || !navigator.clipboard.readText) {
		return;
	}

	navigator.clipboard.readText().then(
		(text) => {
			if (clipboardWatch) {
				clipboardWatch(text || '');
			}
		},
		() => {
			// Denied, unfocused, or unsupported. See above.
		});
}

/**
 * Starts feeding onText what the system clipboard holds: once now, and again whenever the page takes
 * focus - the only moment it can have changed behind this page's back, and the moment a browser is
 * willing to be asked.
 */
export function startClipboardWatch(onText) {
	clipboardWatch = onText;

	window.addEventListener('focus', refreshClipboard);

	refreshClipboard();
}

/**
 * Asks the browser to put text on the system clipboard. Fire and forget: managed code has already
 * recorded the value itself, so a refusal costs the in-app copy nothing.
 */
export function writeClipboardText(text) {
	if (!navigator.clipboard || !navigator.clipboard.writeText) {
		fault('clipboard write', new Error('navigator.clipboard.writeText is not available in this browser.'));
		return;
	}

	navigator.clipboard.writeText(text).catch((error) => fault('clipboard write', error));
}

// ---------------------------------------------------------------------------------------------------
// File dialogs
// ---------------------------------------------------------------------------------------------------

/**
 * Puts up the browser's file picker and hands each chosen file's bytes to onFile, then calls onComplete.
 *
 * The input is added to the document and then removed: Safari has historically not fired change for an
 * input that was never in the DOM. It is hidden rather than off-screen so no layout can be disturbed by it.
 *
 * Reading is sequential rather than Promise.all so that peak memory is one file's bytes plus what has
 * already been staged, not every file at once - these are meshes, and a multi-select of them is large.
 *
 * onComplete fires on cancel too, but only where the browser raises a cancel event (Chromium 113+, current
 * Firefox and Safari). An older engine simply goes quiet, which is why managed code must leave nothing
 * half-set while a pick is in flight.
 */
export function pickFiles(accept, multiple, onFile, onComplete) {
	const input = document.createElement('input');

	input.type = 'file';
	input.multiple = !!multiple;
	input.style.display = 'none';

	if (accept) {
		input.accept = accept;
	}

	const finish = () => {
		input.remove();
		onComplete();
	};

	input.addEventListener('cancel', finish);

	input.addEventListener('change', async () => {
		try {
			for (const file of Array.from(input.files || [])) {
				const buffer = await file.arrayBuffer();

				onFile({ name: file.name, bytes: new Uint8Array(buffer) });
			}
		} catch (error) {
			fault('file open', error);
		} finally {
			// Whatever happened, the caller is owed an answer - it is holding a staging directory open
			// waiting for one.
			finish();
		}
	});

	document.body.appendChild(input);

	// Note the constraint this inherits: a browser only opens a file picker for a click it considers user
	// driven. agg queues its input and delivers it on the next animation frame, which keeps the page's
	// sticky activation but not its transient activation - enough for Chromium and Firefox today. If a
	// browser tightens that, the picker will have to be opened from the DOM listener rather than from agg.
	input.click();
}

/** Hands bytes to the browser as a download, which is what saving a file means in a page. */
export function downloadFile(fileName, bytes) {
	let url = null;

	try {
		// The Blob copies the bytes, so the managed array is free the moment this returns.
		url = URL.createObjectURL(new Blob([bytes], { type: 'application/octet-stream' }));

		const anchor = document.createElement('a');

		anchor.href = url;
		anchor.download = fileName;
		anchor.style.display = 'none';

		document.body.appendChild(anchor);
		anchor.click();
		anchor.remove();
	} catch (error) {
		fault('file save', error);
	} finally {
		if (url) {
			// Not immediately: revoking before the browser has started reading the URL cancels the
			// download in some engines. One turn of the event loop is enough, and the object is otherwise
			// pinned for the life of the document.
			setTimeout(() => URL.revokeObjectURL(url), 0);
		}
	}
}

// ---------------------------------------------------------------------------------------------------
// Screen
// ---------------------------------------------------------------------------------------------------

/**
 * [screenWidthCssPixels, screenHeightCssPixels, devicePixelRatio, approximateMemoryGigabytes].
 *
 * One array rather than four calls because these describe one display at one moment. deviceMemory is
 * Chromium-only and deliberately coarse; 0 means the browser would not say.
 */
export function readScreenMetrics() {
	return [
		window.screen ? window.screen.width : 0,
		window.screen ? window.screen.height : 0,
		window.devicePixelRatio || 1,
		navigator.deviceMemory || 0,
	];
}
