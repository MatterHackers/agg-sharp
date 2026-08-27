// Copyright (c) 2026 Lars Brubaker, MatterHackers Inc.
//
// The persistence module: whole-file get/put/delete/list over one IndexedDB object store.
//
// A fourth module, by the same split-by-owner rule that separated peripherals.js from input.js (see that
// file's header). input.js owns the canvas, peripherals.js owns navigator and the document's detached
// elements - and nothing here touches either. What this owns is the origin's IndexedDB, which is the only
// thing in the host that outlives the page: the canvas, the listeners and the whole wasm heap are gone on
// reload, and these bytes are not. Folding it into peripherals.js would put the one durable thing in the
// host inside the module whose every other member is scoped to this document's lifetime.
//
// Whole files by key, and nothing else: no records, no indexes, no versioned schema. The managed side
// mirrors a directory tree, so a key is a relative path and a value is that file's bytes. That also keeps
// the backend swappable - when W7's workers unlock synchronous OPFS access handles, the replacement has
// to implement these four operations and nothing more.
//
// Everything here is a promise, which is exactly why the mirror exists in the first place: a page's only
// storage is asynchronous on the main thread, and the ISQLite/IStaticData contracts above it are
// synchronous. MEMFS is the synchronous working layer; this is where it is written down.

// One object store in one database. The store name is fixed because there is only ever one kind of thing
// in it; the DATABASE name is the caller's, which is how two configurations of the same app (a Debug root
// and a Release root) stay out of each other's data - see MirrorPolicy.DatabaseName.
const STORE_NAME = 'files';

// The one store this page opened, in two forms. The promise is what an operation issued before the open
// settles queues behind; the database is what every operation after that uses DIRECTLY, without a .then.
//
// That directness is not a micro-optimization, it is what gives the unload flush its best chance. A page
// that is going away gets one task on the event loop and its microtask checkpoint - no more - so a
// transaction created inside a `.then` may be created a turn too late to be sent to the browser's
// storage backend. Created synchronously inside the handler's own task, it is queued immediately.
let storePromise = null;
let openedDatabase = null;

/**
 * Opens (creating if needed) the database this page mirrors into. Returns immediately - the open is a
 * promise every later call awaits.
 */
export function openStore(databaseName) {
	storePromise = new Promise((resolve, reject) => {
		const request = indexedDB.open(databaseName, 1);

		request.onupgradeneeded = () => {
			const database = request.result;

			if (!database.objectStoreNames.contains(STORE_NAME)) {
				// Out-of-line keys: the value is a bare Uint8Array of file bytes and has nowhere to carry a
				// key path of its own.
				database.createObjectStore(STORE_NAME);
			}
		};

		request.onsuccess = () => {
			openedDatabase = request.result;

			resolve(openedDatabase);
		};

		request.onerror = () => reject(request.error);

		// Another tab of the same app holding an older version open. Cannot happen at version 1 with no
		// upgrade path, but a silent hang here would be indistinguishable from a slow disk, so it is named.
		request.onblocked = () => reject(new Error(`indexedDB.open('${databaseName}') is blocked by another tab`));
	});
}

// Runs one request against the store and settles with its result. Every operation below is a single
// request in its own transaction: the mirror pushes a handful of small files at a time, and a transaction
// per file means one failed write cannot roll back the others.
function request(mode, run) {
	if (openedDatabase) {
		// The open has already settled, so the transaction is created here and now - see the note on the
		// two forms of the store above.
		return issue(openedDatabase, mode, run);
	}

	if (!storePromise) {
		return Promise.reject(new Error('storageMirror: openStore has not been called'));
	}

	return storePromise.then((database) => issue(database, mode, run));
}

function issue(database, mode, run) {
	return new Promise((resolve, reject) => {
		const transaction = database.transaction(STORE_NAME, mode);
		const pending = run(transaction.objectStore(STORE_NAME));

		pending.onsuccess = () => resolve(pending.result);
		pending.onerror = () => reject(pending.error);

		// The request can succeed and the transaction still fail (quota, a closing connection), and that
		// failure arrives here rather than on the request.
		transaction.onabort = () => reject(transaction.error || new Error('storageMirror: transaction aborted'));
	});
}

/**
 * Reads the bytes stored under key, handing them to onBytes as `{ bytes }`. A key that is not there
 * settles without calling onBytes at all.
 *
 * The bytes come back through a callback rather than as the promise's value, and wrapped in an object
 * rather than bare, because that is what crosses the managed boundary: the interop generator marshals
 * neither an array as a promise result nor an array as a callback argument, but an object with a
 * Uint8Array property is exactly what the file picker in peripherals.js already hands over.
 */
export async function readEntry(key, onBytes) {
	const stored = await request('readonly', (store) => store.get(key));

	// undefined is IndexedDB's "no such key". Saying nothing is how that is reported.
	if (stored !== undefined) {
		onBytes({ bytes: stored });
	}
}

/** Stores bytes under key, replacing whatever was there. */
export async function writeEntry(key, bytes) {
	// Copied into a fresh Uint8Array rather than stored as handed over. Two reasons, both about what the
	// marshaller may have given us: it may be a view onto the wasm heap, which IndexedDB would read
	// asynchronously while managed code keeps allocating (a heap growth detaches every view onto it), and
	// it may be a plain Array of numbers, which structured-clones into an array of doubles. The copy is a
	// few kilobytes and settles both.
	await request('readwrite', (store) => store.put(new Uint8Array(bytes), key));
}

/** Removes key, if it is there. Removing a key that is not there is not an error. */
export async function deleteEntry(key) {
	await request('readwrite', (store) => store.delete(key));
}

/**
 * Reports every key in the store to onKey, once each, before settling. Same reason as readEntry's
 * callback: an array does not cross the managed boundary, and a string at a time does.
 */
export async function listKeys(onKey) {
	const keys = await request('readonly', (store) => store.getAllKeys());

	for (const key of keys || []) {
		onKey(String(key));
	}
}

/**
 * Calls onUnload when the page is hidden or going away, so the mirror gets one last chance to push what
 * it has.
 *
 * Both events, and what each is actually worth - MEASURED in Chrome 151, not assumed:
 *
 *   - visibilitychange to hidden (the tab was switched away from, the window was minimized): the page is
 *     still alive, so every write this starts commits normally. This is the one that earns its keep, and
 *     it earns it twice over, because a hidden page gets no animation frames - so the write-behind sweep,
 *     which runs on the frame-driven UI queue, has stopped. Without this hook a backgrounded tab would
 *     hold its last edits until the user came back and then, if the tab were closed from the taskbar,
 *     lose them.
 *
 *   - pagehide (the page is being navigated away from or closed): the handler runs and the writes are
 *     issued, and none of them commit. IndexedDB is asynchronous and a renderer being torn down does not
 *     wait for it - this was measured with a probe on both ends of the flush, and the transactions were
 *     started and never completed. It costs nothing to keep and it may be worth something in another
 *     engine or a bfcache navigation, but nothing should be built on it.
 *
 * So the loss window is bounded by the sweep interval, not by this. See BrowserStorageMirror.
 */
export function installUnloadFlush(onUnload) {
	const flush = () => {
		try {
			onUnload();
		} catch (error) {
			console.error('agg storage mirror flush failed:', error);
		}
	};

	addEventListener('pagehide', flush);
	addEventListener('visibilitychange', () => {
		if (document.visibilityState === 'hidden') {
			flush();
		}
	});
}
