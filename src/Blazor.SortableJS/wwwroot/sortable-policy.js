// Decision logic for the interop module: which rows count, which lists a drag can reach, and what
// an event does to the event queue. Kept apart from sortable-interop.js because none of it touches
// SortableJS or the interop channel - it works on plain element-shaped objects - so it can be
// reasoned about, and tested, without a browser.

// Mirrors SortableInteropNames on the .NET side. These are a format contract between the component,
// which writes them into the DOM, and the selectors that read them back out.
export const itemMarkerAttribute = "data-sortable-item";
export const setDataTextAttribute = "data-sortable-text";
export const undraggableClass = "kebechet-sortable-undraggable";

/**
 * Finds a row's index among its list's real rows.
 *
 * Not a plain indexOf over children. During a drag SortableJS inserts its fallback ghost and, in
 * clone mode, a clone into the list, and both carry the item marker copied from the row they came
 * from. Counting them would shift every index past them by one, so a caller would be handed the
 * wrong neighbour - or resolve nothing at all - with no sign the index was bogus. Indexes must line
 * up with the .NET collection, which knows only about real rows.
 */
export function indexOfChild(container, child) {
    if (!container || !child) {
        return -1;
    }

    const ghost = globalThis.Sortable?.ghost;
    const clone = globalThis.Sortable?.clone;
    let index = -1;
    for (const element of container.children) {
        if (element === ghost || element === clone || !element.hasAttribute(itemMarkerAttribute)) {
            continue;
        }

        index++;
        if (element === child) {
            return index;
        }
    }

    return -1;
}

/**
 * Decides what an event does to the drag-scoped event queue: "open", "close" or "keep".
 *
 * SortableJS dispatches "choose" on every mousedown but guards "end" behind Sortable.active, so a
 * click that never becomes a drag produces choose and unchoose and no end at all. Closing only on
 * "end" would leave the queue open for the rest of the page's life, funnelling every list's events
 * back through one chain - the head-of-line blocking the per-drag queue exists to avoid.
 *
 * Closing on "unchoose" is only safe before "start". During a real drag "unchoose" precedes
 * add/remove/sort, and releasing there would split a cross-list pair across two queues.
 */
export function resolveQueueOwner(eventName, hasStarted) {
    if (eventName === "choose") {
        return "open";
    }

    if (eventName === "unchoose") {
        return hasStarted ? "keep" : "close";
    }

    return eventName === "end" ? "close" : "keep";
}
