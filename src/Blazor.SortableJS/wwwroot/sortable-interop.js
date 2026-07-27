const sortableScriptMarker = "data-kebechet-sortablejs";
// Mirrors SortableInteropNames on the .NET side. These are a format contract between the component,
// which writes them into the DOM, and the selectors below, which read them back out.
const itemMarkerAttribute = "data-sortable-item";
const setDataTextAttribute = "data-sortable-text";
const undraggableClass = "kebechet-sortable-undraggable";
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

function indexOfChild(container, child) {
    return container && child ? Array.prototype.indexOf.call(container.children, child) : -1;
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
                if (!nextKeys.has(key)) {
                    state.sortable.option(key, pristineDefaults(Sortable)[key]);
                }
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

// SortableJS exposes no table of its own defaults, so read them off a throwaway instance built with
// no options at all. Resetting a cleared option to undefined would not do: SortableJS stores the
// value verbatim, and several of its code paths test the property rather than its truthiness.
let pristineDefaultsCache = null;
function pristineDefaults(Sortable) {
    if (pristineDefaultsCache) {
        return pristineDefaultsCache;
    }

    const probe = document.createElement("div");
    const instance = new Sortable(probe, {});
    pristineDefaultsCache = { ...instance.options };
    instance.destroy();
    return pristineDefaultsCache;
}

function buildOptions(state, defaultOptions, componentOptions) {
    const values = mergeDefined(defaultOptions, componentOptions);
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
        dataTransfer.setData("Text", values.setDataText ?? dragElement.getAttribute(setDataTextAttribute) ?? dragElement.textContent ?? "");
    };

    for (const eventName of eventNames) {
        options[`on${capitalize(eventName)}`] = event => {
            // SortableJS appends its fallback ghost (and may insert plugin clones) before
            // dispatching "start". "choose" runs before those child-list mutations, so this
            // snapshot contains only the DOM that Blazor last rendered.
            if (eventName === "choose") {
                captureSnapshots(state);
                dragToken++;
                activeDragQueue = Promise.resolve();
            }

            const payload = createPayload(eventName, event, state.id, values);

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

// Snapshot breadth is a correctness question first: rollback has to cover every list the drag can
// reach, including ancestors and descendants for nested trees. Only lists provably out of reach -
// a different named group, no containment relationship - are skipped. An unnamed group tells us
// nothing, so those are still snapshotted.
function captureSnapshots(sourceState) {
    dragSnapshots.clear();
    for (const state of instances.values()) {
        if (canReceiveDragFrom(sourceState, state)) {
            dragSnapshots.set(state.element, Array.from(state.element.children));
        }
    }
}

function canReceiveDragFrom(sourceState, candidateState) {
    if (candidateState === sourceState) {
        return true;
    }

    const source = sourceState.element;
    const candidate = candidateState.element;
    if (source.contains(candidate) || candidate.contains(source)) {
        return true;
    }

    const sourceGroup = groupNameOf(sourceState);
    const candidateGroup = groupNameOf(candidateState);
    if (!sourceGroup || !candidateGroup) {
        return true;
    }

    return sourceGroup === candidateGroup;
}

function groupNameOf(state) {
    const group = state.sortable?.options?.group;
    return typeof group === "string" ? group : group?.name;
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
    // who supplied both meant the code to decide.
    return {
        name: group.name,
        pull: state.decisions.hasPullDecision
            ? (to, from, dragged) => decideSynchronously(state, "DecidePull", groupRequest(to, from, dragged), true)
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



