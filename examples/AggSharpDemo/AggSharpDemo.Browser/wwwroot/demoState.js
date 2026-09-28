// Copyright (c) 2026 Lars Brubaker, MatterHackers Inc.
//
// The GUI demo's saved state in localStorage (BrowserDemoStateStore). localStorage rather than the host's
// IndexedDB mirror because the state is one small string read once at start-up, and localStorage answers
// synchronously - the page is built before anything could await a promise. Every call is guarded: a
// browser with storage disabled (private mode, a full quota) just remembers nothing.

export function load(key) {
    try {
        return globalThis.localStorage.getItem(key);
    } catch {
        return null;
    }
}

export function save(key, value) {
    try {
        globalThis.localStorage.setItem(key, value);
    } catch {
    }
}

export function clear(key) {
    try {
        globalThis.localStorage.removeItem(key);
    } catch {
    }
}
