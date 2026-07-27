import {
    itemMarkerAttribute,
    setDataTextAttribute,
    undraggableClass,
    indexOfChild,
    resolveQueueOwner
} from "./sortable-policy.js";

const sortableScriptMarker = "data-kebechet-sortablejs";
const sortableReadyTimeoutMilliseconds = 15000;
const sortablePollIntervalMilliseconds = 16;
const eventNames = [
    "choose", "unchoose", "start", "end", "add", "update", "sort", "remove",
    "filter", "move", "clone", "change", "select", "deselect", "spill"
];
const instances = new Map();
const dragSnapshots = new Map();
let activeDragQueue = null;
let dragToken = 0;
let hasDeferredSpill = false;
let hasDragStarted = false;

// SortableJS reads the return value of onMove, group.pull and group.put on the spot, so these
// cannot go through the asynchronous event queue like every other callback. `invokeMethod` is
// synchronous and exists only on WebAssembly; the component refuses to configure a decision on any
// other platform, so reaching here means the call is supported. A throw would be swallowed by
// SortableJS mid-drag, so failures fall back to SortableJS's own behaviour and are reported.
function decideSynchronously(state, methodName, request, fallback) {
    try {
        return state.dotNetReference.invokeMethod(methodName, request);
    } catch (error) {
        console.error(`Kebechet.Blazor.SortableJS ${methodName} failed.`, error);
        return fallback;
    }
}

function decisionRequest(event) {
    return {
        sourceId: event.from?.id ?? "",
        destinationId: event.to?.id ?? "",
        draggedIndex: indexOfChild(event.from, event.dragged ?? event.item),
        relatedIndex: indexOfChild(event.to, event.related),
        willInsertAfter: Boolean(event.willInsertAfter)
    };
}

export async function create(id, dotNetReference, defaultOptions, componentOptions, decisions) {
    const Sortable = await waitForSortable();
    const element = document.getElementById(id);
    if (!element) {
        throw new Error(`Cannot initialize SortableJS because element '${id}' was not found.`);
    }

    capturePristineDefaults(Sortable);

    const state = { id, element, dotNetReference, sortable: null, appliedKeys: new Set(), isDestroyed: false, decisions: decisions ?? {} };
    const options = buildOptions(state, defaultOptions, componentOptions);
    state.sortable = new Sortable(element, options);
    state.appliedKeys = new Set(Object.keys(options).filter(key => !key.startsWith("on")));
    instances.set(id, state);

    return {
        update(nextDefaults, nextOptions, nextDecisions) {
            state.decisions = nextDecisions ?? {};
            const normalized = buildOptions(state, nextDefaults, nextOptions);
            const nextKeys = new Set(Object.keys(normalized).filter(key => !key.startsWith("on")));

            // Clearing an option removes it from the normalized object rather than setting it to
            // anything, so applying only what is present leaves the previous value in force - set
            // IsDisabled and then clear it and the list stays disabled. Restore whatever SortableJS
            // itself would have used for keys that have gone away.
            for (const key of state.appliedKeys) {
                if (nextKeys.has(key)) {
                    continue;
                }

                // `option(key, undefined)` is a *getter* in SortableJS, so handing it a pristine
                // value that happens to be undefined - every plugin flag, scrollFn - silently sets
                // nothing and leaves the old value in force. Writing straight to the options object
                // is the only way to clear those, and it also avoids the option listeners, one of
                // which calls toLowerCase on a pristine multiDragKey of null.
                const pristine = pristineDefaults[key];
                if (pristine === undefined) {
                    state.sortable.options[key] = undefined;
                    continue;
                }

                state.sortable.option(key, pristine);
            }

            for (const [key, value] of Object.entries(normalized)) {
                if (nextKeys.has(key)) {
                    state.sortable.option(key, value);
                }
            }

            state.appliedKeys = nextKeys;
        },
        destroy() {
            if (!state.sortable) {
                return;
            }

            // Events already dispatched would otherwise reach a DotNetObjectReference that .NET has
            // disposed, which throws "There is no tracked object with id ..." from the JS side.
            state.isDestroyed = true;

            state.sortable.destroy();
            state.sortable = null;
            instances.delete(id);
            dragSnapshots.delete(element);
        }
    };
}

// SortableJS publishes no table of its own defaults, so they are read off a throwaway instance built
// with no options at all.
//
// Captured when a list is created, never on demand. SortableJS's destroy() calls _onDrop(), which
// clears its module-global active-drag state regardless of which instance is being destroyed, so
// building and destroying this probe during a drag would abandon that drag: no drop, no end event,
// and the DOM left half-moved. Creation happens on a component's first render, where no drag can be
// in flight.
let pristineDefaults = {};
let hasPristineDefaults = false;
function capturePristineDefaults(Sortable) {
    if (hasPristineDefaults) {
        return;
    }

    const probe = document.createElement("div");
    const instance = new Sortable(probe, {});
    pristineDefaults = { ...instance.options };
    instance.destroy();
    hasPristineDefaults = true;
}

function buildOptions(state, defaultOptions, componentOptions) {
    const values = mergeDefined(defaultOptions, componentOptions);

    // The event handlers read this rather than closing over `values`. update() deliberately skips
    // every on* key - reassigning handlers mid-drag is not safe - so a handler built at creation
    // would keep the option values it was born with. Enabling ShouldRemoveOnSpill later then had
    // the plugin delete the row while the stale handler reported isSpillRemoval false, leaving the
    // item in the collection and Blazor rendering against a node that is no longer there.
    state.values = values;


    const options = {};
    assign(options, "group", mapGroup(state, values.group));
    assign(options, "sort", values.isSortingEnabled);
    assign(options, "disabled", values.isDisabled);
    assign(options, "store", mapStore(values.store));
    assign(options, "handle", values.handle);
    assign(options, "draggable", values.draggable ?? `> [${itemMarkerAttribute}]`);
    assign(options, "swapThreshold", values.swapThreshold);
    assign(options, "invertSwap", values.isInvertedSwapEnabled);
    assign(options, "invertedSwapThreshold", values.invertedSwapThreshold);
    assign(options, "removeCloneOnHide", values.isCloneRemovedOnHide);
    assign(options, "direction", mapDirection(values.direction));
    assign(options, "ghostClass", values.ghostClass);
    assign(options, "chosenClass", values.chosenClass);
    assign(options, "dragClass", values.dragClass);
    assign(options, "ignore", values.ignoredSelectors);
    // IsItemDraggable marks rejected rows with this class rather than asking the consumer to render
    // a marker and wire a selector themselves. Filtering on it unconditionally costs nothing when no
    // row carries it, and keeps the consumer's own filter working alongside.
    assign(options, "filter", values.filter ? `${values.filter}, .${undraggableClass}` : `.${undraggableClass}`);
    assign(options, "preventOnFilter", values.shouldPreventOnFilter);
    assign(options, "animation", values.animationDuration);
    assign(options, "easing", values.easing);
    assign(options, "dropBubble", values.shouldStopDropPropagation === undefined ? undefined : !values.shouldStopDropPropagation);
    assign(options, "dragoverBubble", values.shouldStopDragOverPropagation === undefined ? undefined : !values.shouldStopDragOverPropagation);
    assign(options, "dataIdAttr", values.dataIdAttribute);
    assign(options, "delay", values.delay);
    assign(options, "delayOnTouchOnly", values.isDelayOnTouchOnly);
    assign(options, "touchStartThreshold", values.touchStartThreshold);
    assign(options, "forceFallback", values.isFallbackForced);
    assign(options, "fallbackClass", values.fallbackClass);
    assign(options, "fallbackOnBody", values.isFallbackOnBody);
    assign(options, "fallbackTolerance", values.fallbackTolerance);
    assign(options, "fallbackOffset", values.fallbackOffset);
    assign(options, "supportPointer", values.isPointerSupported);
    assign(options, "emptyInsertThreshold", values.emptyInsertThreshold);
    assign(options, "scroll", mapScroll(values));
    assign(options, "forceAutoScrollFallback", values.isAutoScrollFallbackForced);
    assign(options, "scrollSensitivity", values.scrollSensitivity);
    assign(options, "scrollSpeed", values.scrollSpeed);
    assign(options, "bubbleScroll", values.shouldBubbleScroll);
    assign(options, "scrollFn", values.shouldContinueNativeScrolling === undefined
        ? undefined
        : () => values.shouldContinueNativeScrolling ? "continue" : undefined);
    assign(options, "revertOnSpill", values.shouldRevertOnSpill);
    assign(options, "removeOnSpill", values.shouldRemoveOnSpill);
    assign(options, "swap", values.isSwapEnabled);
    assign(options, "swapClass", values.swapClass);
    assign(options, "multiDrag", values.isMultiDragEnabled);
    assign(options, "selectedClass", values.selectedClass);
    assign(options, "multiDragKey", mapMultiDragKey(values.multiDragKey));
    assign(options, "avoidImplicitDeselect", values.shouldAvoidImplicitDeselect);
    options.setData = (dataTransfer, dragElement) => {
        dataTransfer.setData("Text", state.values.setDataText ?? dragElement.getAttribute(setDataTextAttribute) ?? dragElement.textContent ?? "");
    };

    for (const eventName of eventNames) {
        options[`on${capitalize(eventName)}`] = event => {
            // SortableJS appends its fallback ghost (and may insert plugin clones) before
            // dispatching "start". "choose" runs before those child-list mutations, so this
            // snapshot contains only the DOM that Blazor last rendered.
            const queueAction = resolveQueueOwner(eventName, hasDragStarted);
            if (eventName === "choose") {
                captureSnapshots();
            }

            if (eventName === "start") {
                hasDragStarted = true;
            }

            if (queueAction === "open") {
                dragToken++;
                hasDragStarted = false;

                // Continue a chain that is still draining rather than starting a fresh one. The
                // previous drag's handlers can still be in flight - a network round trip each on
                // Blazor Server - and a second chain would let this drag's mutations reach .NET
                // first. Both drags then apply against indexes taken from a list the other has
                // already changed, which duplicates one item and drops another.
                activeDragQueue = activeDragQueue ?? Promise.resolve();
            }

            // "end" also closes, but only after its own event has been enqueued, so it is handled
            // further down rather than here.
            if (queueAction === "close" && eventName === "unchoose") {
                closeDragQueue();
                dragSnapshots.clear();
            }

            const payload = createPayload(eventName, event, state.id, state.values);

            // "spill" is raised before the OnSpill plugin has finished with the DOM: with
            // removeOnSpill it goes on to delete the row after this callback returns. Restoring
            // synchronously re-inserts a node SortableJS then removes again, leaving Blazor to
            // render against a detached element ("Cannot read properties of null (removeChild)").
            // Deferring puts the restore after SortableJS is done.
            if (eventName === "spill") {
                hasDeferredSpill = true;
                setTimeout(() => {
                    restoreSnapshots();
                    enqueueEvent(state, payload);
                }, 0);
                return;
            }

            if (eventName === "add" || eventName === "update" || eventName === "remove") {
                restoreSnapshots();
            }

            if (eventName === "end" && hasDeferredSpill) {
                // "end" fires synchronously after "spill", so notifying .NET here would arrive
                // before the deferred spill and the mutation would be applied out of order.
                // Queue behind it, then release the snapshots.
                hasDeferredSpill = false;
                setTimeout(() => {
                    enqueueEvent(state, payload);
                    closeDragQueue();
                    dragSnapshots.clear();
                }, 0);
                return;
            }

            enqueueEvent(state, payload);
            if (eventName === "end") {
                closeDragQueue();
                dragSnapshots.clear();
            }

            // Everything above notifies .NET and discards the result. "move" is the one event whose
            // return value SortableJS acts on, so the observational callback still runs and the
            // decision, when configured, is asked for separately and synchronously.
            if (eventName === "move" && state.decisions.hasMoveDecision) {
                return mapMoveDecision(decideSynchronously(state, "DecideMove", decisionRequest(event), 0));
            }
        };
    }

    return options;
}

function createPayload(eventName, event, ownerId, values) {
    const sourceId = event.from?.id || ownerId;
    const destinationId = event.to?.id || ownerId;
    return {
        eventName,
        sourceId,
        destinationId,
        oldIndexes: readIndexes(event.oldIndicies, event.oldIndex),
        newIndexes: readIndexes(event.newIndicies, event.newIndex),
        isClone: event.pullMode === "clone",
        isSwap: Boolean(event.swapItem),
        isSpillRemoval: eventName === "spill" && values.shouldRemoveOnSpill === true
    };
}

function readIndexes(multipleIndexes, singleIndex) {
    if (Array.isArray(multipleIndexes) && multipleIndexes.length > 0) {
        return multipleIndexes.map(entry => entry.index).filter(Number.isInteger);
    }

    return Number.isInteger(singleIndex) ? [singleIndex] : [];
}

// Releasing the drag's queue only once its tail has settled keeps a trailing event ahead of
// anything a later, unrelated event might enqueue. The token guards against a drag that starts
// while the previous one is still draining, which would otherwise be released by the old drag.
function closeDragQueue() {
    const token = dragToken;
    const tail = activeDragQueue;
    tail?.finally(() => {
        if (dragToken === token) {
            activeDragQueue = null;
        }
    });
}

// Every registered list, deliberately.
//
// Narrowing this by group name is unsound: PutMode.Enabled maps to `put: true`, and SortableJS's
// own check returns true for `true` before it ever compares names, so two lists with different
// names accept each other. Group arrays and the synchronous predicates cross names too. What is
// left to exclude - a list whose put is explicitly disabled - is not worth the risk, because a list
// wrongly skipped is absent from the snapshot, rollback can only restore what it snapshotted, and
// the DOM then stays diverged from the model with nothing left to correct it.
function captureSnapshots() {
    dragSnapshots.clear();
    for (const state of instances.values()) {
        dragSnapshots.set(state.element, Array.from(state.element.children));
    }
}

function restoreSnapshots() {
    for (const [element, snapshot] of dragSnapshots) {
        // A list can be torn down mid-drag - on a docs page several stories mount and unmount
        // together. Re-inserting nodes into a detached container leaves Blazor holding elements
        // whose parent is gone, which surfaces as "Cannot read properties of null (removeChild)".
        if (!element.isConnected) {
            continue;
        }

        const expected = new Set(snapshot);
        for (const child of Array.from(element.children)) {
            if (!expected.has(child)) {
                child.remove();
            }
        }

        snapshot.forEach((child, index) => {
            const current = element.children[index] ?? null;
            if (current !== child) {
                element.insertBefore(child, current);
            }
        });
    }
}

// Ordering has to hold within a drag - an "add" that lands before the matching "remove" corrupts
// the model - but not between unrelated lists. A single module-wide chain gave the stronger
// guarantee at the cost of head-of-line blocking: one slow handler stalled events on every other
// list on the page, which on Blazor Server means a network round trip each.
//
// A pointer only performs one drag at a time, so a chain that lives for the duration of a drag is
// exactly as strong where it matters. Outside a drag, each list gets its own chain.
//
// The queue also defers execution, so a component can be disposed between an event being queued and
// the queue reaching it. Checking `isDestroyed` only in the handler is too early: re-check here, or
// the invoke lands on a DotNetObjectReference .NET has already released.
function enqueueEvent(state, payload) {
    const invoke = () => state.isDestroyed
        ? undefined
        : state.dotNetReference.invokeMethodAsync("HandleEventAsync", payload);
    const onFailed = error => console.error("Kebechet.Blazor.SortableJS event callback failed.", error);

    if (activeDragQueue) {
        activeDragQueue = activeDragQueue.then(invoke).catch(onFailed);
        return;
    }

    state.eventQueue = (state.eventQueue ?? Promise.resolve()).then(invoke).catch(onFailed);
}

// Mirrors SortableMoveDecision. Reject is false, and the two placement overrides are the -1/1 that
// SortableJS reads as "insert before" and "insert after"; anything else leaves it to decide.
function mapMoveDecision(decision) {
    switch (decision) {
        case 1: return false;
        case 2: return -1;
        case 3: return 1;
        default: return undefined;
    }
}

function mapGroup(state, group) {
    const hasPredicate = state.decisions.hasPutDecision || state.decisions.hasPullDecision;
    if (!group && !hasPredicate) {
        return undefined;
    }

    // A predicate needs a group object to hang off, even when the consumer configured no group.
    if (!group) {
        return {
            pull: state.decisions.hasPullDecision
                ? (to, from, dragged, event) => decideSynchronously(state, "DecidePull", groupRequest(to, from, dragged), true)
                : true,
            put: state.decisions.hasPutDecision
                ? (to, from, dragged, event) => decideSynchronously(state, "DecidePut", groupRequest(to, from, dragged), true)
                : true
        };
    }

    const pull = group.pullMode === 1
        ? false
        : group.pullMode === 2
            ? "clone"
            : group.pullMode === 3
                ? group.pullGroups
                : true;
    const put = group.putMode === 1
        ? false
        : group.putMode === 2
            ? group.putGroups
            : true;
    // A configured predicate wins over the fixed mode: it is strictly more specific, and a consumer
    // who supplied both meant the code to decide. A permitted pull answers with the configured mode
    // rather than plain true, because returning true where the mode is "clone" downgrades the whole
    // transfer to a move: SortableJS reports pullMode true, so nothing is recognised as a clone, the
    // CloneFunction never runs, and the original is taken out of the source list.
    return {
        name: group.name,
        pull: state.decisions.hasPullDecision
            ? (to, from, dragged) => decideSynchronously(state, "DecidePull", groupRequest(to, from, dragged), true) && pull
            : pull,
        put: state.decisions.hasPutDecision
            ? (to, from, dragged) => decideSynchronously(state, "DecidePut", groupRequest(to, from, dragged), true)
            : put,
        revertClone: group.shouldRevertClone
    };
}

// The group callbacks receive Sortable instances rather than an event, so the ids and the dragged
// row's position have to be read off them directly.
function groupRequest(to, from, dragged) {
    const fromElement = from?.el;
    return {
        sourceId: fromElement?.id ?? "",
        destinationId: to?.el?.id ?? "",
        draggedIndex: indexOfChild(fromElement, dragged),
        relatedIndex: -1,
        willInsertAfter: false
    };
}

function mapStore(store) {
    if (!store?.key) {
        return undefined;
    }

    return {
        get() {
            try {
                return JSON.parse(localStorage.getItem(store.key) ?? "[]");
            } catch {
                return [];
            }
        },
        set(sortable) {
            try {
                localStorage.setItem(store.key, JSON.stringify(sortable.toArray()));
            } catch {
            }
        }
    };
}

function mapDirection(direction) {
    return direction === 1 ? "vertical" : direction === 2 ? "horizontal" : undefined;
}

function mapMultiDragKey(key) {
    return key === 0 ? "Alt" : key === 1 ? "Control" : key === 2 ? "Meta" : key === 3 ? "Shift" : undefined;
}

function mapScroll(values) {
    if (values.scrollContainerSelector) {
        return document.querySelector(values.scrollContainerSelector) ?? values.isAutoScrollEnabled;
    }

    return values.isAutoScrollEnabled;
}

function mergeDefined(defaultOptions, componentOptions) {
    return deepMerge(compact(defaultOptions), compact(componentOptions));
}

function compact(value) {
    if (!value || typeof value !== "object" || Array.isArray(value)) {
        return value;
    }

    return Object.fromEntries(
        Object.entries(value)
            .filter(([, item]) => item !== null && item !== undefined)
            .map(([key, item]) => [key, compact(item)]));
}

function deepMerge(base, override) {
    const result = { ...(base ?? {}) };
    for (const [key, value] of Object.entries(override ?? {})) {
        result[key] = value && typeof value === "object" && !Array.isArray(value)
            ? deepMerge(result[key], value)
            : value;
    }

    return result;
}

function assign(target, key, value) {
    if (value !== undefined && value !== null) {
        target[key] = value;
    }
}

function capitalize(value) {
    return value.charAt(0).toUpperCase() + value.slice(1);
}

async function waitForSortable() {
    if (globalThis.Sortable) {
        return globalThis.Sortable;
    }

    const script = document.querySelector(`script[${sortableScriptMarker}]`);
    return new Promise((resolve, reject) => {
        let isFinished = false;
        const startedAt = performance.now();
        const finish = callback => {
            if (!isFinished) {
                isFinished = true;
                callback();
            }
        };
        const check = () => {
            if (globalThis.Sortable) {
                finish(() => resolve(globalThis.Sortable));
                return;
            }

            if (performance.now() - startedAt >= sortableReadyTimeoutMilliseconds) {
                finish(() => reject(new Error("Timed out waiting for the injected SortableJS bundle.")));
                return;
            }

            setTimeout(check, sortablePollIntervalMilliseconds);
        };

        script?.addEventListener("load", check, { once: true });
        script?.addEventListener("error", () => finish(() => reject(new Error("The injected SortableJS bundle failed to load."))), { once: true });
        check();
    });
}



