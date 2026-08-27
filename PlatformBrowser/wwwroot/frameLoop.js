// Copyright (c) 2026 Lars Brubaker, MatterHackers Inc.
//
// The browser owns the frame clock: there is no wgpuSurfacePresent, the canvas is composited when the
// requestAnimationFrame callback returns. The managed side hands us the tick method and we call it once
// per animation frame until it is stopped.

let frameHandle = 0;

export function startFrameLoop(onFrame) {
	stopFrameLoop();

	const tick = () => {
		onFrame();

		// Re-scheduled after the callback, not before, so a managed call that stops the loop cannot leave
		// one more frame already queued.
		if (frameHandle !== 0) {
			frameHandle = requestAnimationFrame(tick);
		}
	};

	frameHandle = requestAnimationFrame(tick);
}

export function stopFrameLoop() {
	if (frameHandle !== 0) {
		cancelAnimationFrame(frameHandle);
		frameHandle = 0;
	}
}
