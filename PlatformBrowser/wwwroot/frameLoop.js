// Copyright (c) 2026 Lars Brubaker, MatterHackers Inc.
//
// The browser owns the frame clock: there is no wgpuSurfacePresent, the canvas is composited when the
// requestAnimationFrame callback returns. The managed side hands us the tick method and we call it once
// per animation frame until it is stopped.
//
// A hidden page gets no animation frames at all, and the managed tick is not only what paints - it is what
// drains UiThread's idle queue and advances its intervals. So a backgrounded tab would stop mirroring its
// storage (see storageMirror.js), stop taking session snapshots, and strand any job parked on
// UiThread.YieldToFrame until the user looked at it again. The heartbeat below keeps that clock running
// while the page is hidden.

// How often the heartbeat ticks a hidden page. A ceiling on the rate rather than a promise of it: a
// backgrounded page's timers are clamped to about a second in Chrome and Firefox, and Chrome's intensive
// throttling then cuts a page hidden for around five minutes to roughly one timer wakeup a MINUTE - and a
// tab it goes on to freeze outright gets none at all. So this is what we ask for, not what we get, and
// nothing may be built on the beat arriving at any particular rate.
// It is deliberately far coarser than a frame - nothing is on screen to paint, and what it has to keep
// moving are the second-scale sweeps (the storage mirror, the snapshot writer) plus whatever is waiting on
// a yield.
const hiddenTickMs = 250;

let frameHandle = 0;
let heartbeatHandle = 0;
let frameTick = null;

export function startFrameLoop(onFrame) {
	stopFrameLoop();

	frameTick = onFrame;

	const tick = () => {
		onFrame();

		// Re-scheduled after the callback, not before, so a managed call that stops the loop cannot leave
		// one more frame already queued.
		if (frameHandle !== 0) {
			frameHandle = requestAnimationFrame(tick);
		}
	};

	frameHandle = requestAnimationFrame(tick);

	// A loop started while the page is already hidden - a tab restored in the background, say - has to be
	// beating from the start; it will never get the animation frame that would otherwise begin it.
	syncHeartbeat();
}

export function stopFrameLoop() {
	if (frameHandle !== 0) {
		cancelAnimationFrame(frameHandle);
		frameHandle = 0;
	}

	frameTick = null;

	stopHeartbeat();
}

/**
 * Runs the heartbeat exactly when the page is hidden and a loop is running: a hidden page is served no
 * animation frames, and the outstanding request - still pending, simply not being served - resumes the
 * loop on its own when the page comes back.
 *
 * The handover is not instantaneous in either direction: an animation frame already queued may still be
 * served around a visibilitychange, so a beat and a frame can land in the same moment. That costs an extra
 * tick and nothing else - a tick drains queues and paints only if something asked for a repaint, so
 * running one more of them is harmless.
 */
function syncHeartbeat() {
	if (frameTick !== null
		&& document.hidden) {
		startHeartbeat();
	} else {
		stopHeartbeat();
	}
}

function startHeartbeat() {
	if (heartbeatHandle !== 0) {
		return;
	}

	const beat = () => {
		heartbeatHandle = 0;

		try {
			// The tick itself, not the rAF `tick` above: touching frameHandle here would schedule a second
			// animation frame on top of the one already pending, and the loop would double up on return.
			frameTick();
		} finally {
			// Re-armed after the tick rather than run on an interval, so a tick slower than the beat cannot
			// stack beats behind itself - and a throwing one still leaves the page's clock running.
			//
			// Through syncHeartbeat rather than by re-arming directly: a tick that restarted the loop (a
			// window handing off to another one) has already armed a beat of its own, and startHeartbeat's
			// guard is what keeps this one from orphaning that handle and leaving two heartbeats running.
			syncHeartbeat();
		}
	};

	heartbeatHandle = setTimeout(beat, hiddenTickMs);
}

function stopHeartbeat() {
	if (heartbeatHandle !== 0) {
		clearTimeout(heartbeatHandle);
		heartbeatHandle = 0;
	}
}

// On the document, which is what fires visibilitychange - it reaches the window only by bubbling.
document.addEventListener('visibilitychange', syncHeartbeat);
