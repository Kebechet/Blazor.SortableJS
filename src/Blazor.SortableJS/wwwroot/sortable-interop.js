const sortableScriptMarker = "data-kebechet-sortablejs";
const sortableReadyTimeoutMilliseconds = 15000;
const sortablePollIntervalMilliseconds = 16;
const eventNames = [
    "choose", "unchoose", "start", "end", "add", "update", "sort", "remove",
    "filter", "move", "clone", "change", "select", "deselect", "spill"
];
const instances = new Map();
const dragSnapshots = new Map();
let eventQueue = Promise.resolve();
let hasDeferredSpill = false;

export async function create(id, dotNetReference, defaultOptions, componentOptions) {
    const Sortable = await waitForSortable();
    const element = document.getElementById(id);
    if (!element) {
        throw new Error(`Cannot initialize SortableJS because element '${id}' was not found.`);
    }

    const state = { id, element, dotNetReference, sortable: null, appliedKeys: new Set(), isDestroyed: false };
    const options = buildOptions(state, defaultOptions, componentOptions);
    state.sortable = new Sortable(element, options);
    state.appliedKeys = new Set(Object.keys(options).filter(key => !key.startsWith("on")));
    instances.set(id, state);

    return {
        update(nextDefaults, nextOptions) {
            const normalized = buildOptions(state, nextDefaults, nextOptions);
            for (const [key, value] of Object.entries(normalized)) {
                if (!key.startsWith("on")) {
                    state.sortable.option(key, value);
                    state.appliedKeys.add(key);
                }
            }
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

function buildOptions(state, defaultOptions, componentOptions) {
    const values = mergeDefined(defaultOptions, componentOptions);
    const options = {};
    assign(options, "group", mapGroup(values.group));
    assign(options, "sort", values.isSortingEnabled);
    assign(options, "disabled", values.isDisabled);
    assign(options, "store", mapStore(values.store));
    assign(options, "handle", values.handle);
    assign(options, "draggable", values.draggable ?? "> [data-sortable-item]");
    assign(options, "swapThreshold", values.swapThreshold);
    assign(options, "invertSwap", values.isInvertedSwapEnabled);
    assign(options, "invertedSwapThreshold", values.invertedSwapThreshold);
    assign(options, "removeCloneOnHide", values.isCloneRemovedOnHide);
    assign(options, "direction", mapDirection(values.direction));
    assign(options, "ghostClass", values.ghostClass);
    assign(options, "chosenClass", values.chosenClass);
    assign(options, "dragClass", values.dragClass);
    assign(options, "ignore", values.ignoredSelectors);
    assign(options, "filter", values.filter);
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
        dataTransfer.setData("Text", values.setDataText ?? dragElement.getAttribute("data-sortable-text") ?? dragElement.textContent ?? "");
    };

    for (const eventName of eventNames) {
        options[`on${capitalize(eventName)}`] = event => {
            // SortableJS appends its fallback ghost (and may insert plugin clones) before
            // dispatching "start". "choose" runs before those child-list mutations, so this
            // snapshot contains only the DOM that Blazor last rendered.
            if (eventName === "choose") {
                captureSnapshots();
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
                    dragSnapshots.clear();
                }, 0);
                return;
            }

            enqueueEvent(state, payload);
            if (eventName === "end") {
                dragSnapshots.clear();
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

// The queue defers execution, so a component can be disposed between an event being queued and
// the queue reaching it. Checking `isDestroyed` only in the handler is too early: re-check here,
// or the invoke lands on a DotNetObjectReference .NET has already released.
function enqueueEvent(state, payload) {
    eventQueue = eventQueue
        .then(() => state.isDestroyed ? undefined : state.dotNetReference.invokeMethodAsync("HandleEventAsync", payload))
        .catch(error => console.error("Kebechet.Blazor.SortableJS event callback failed.", error));
}

function mapGroup(group) {
    if (!group) {
        return undefined;
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
    return { name: group.name, pull, put, revertClone: group.shouldRevertClone };
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



