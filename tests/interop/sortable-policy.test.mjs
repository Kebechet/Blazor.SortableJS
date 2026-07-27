import assert from "node:assert/strict";
import test from "node:test";

import {
    indexOfChild,
    resolveQueueOwner,
    itemMarkerAttribute
} from "../../src/Blazor.SortableJS/wwwroot/sortable-policy.js";

const itemMarker = itemMarkerAttribute;

function row(attributes = {}) {
    return { hasAttribute: name => name === itemMarker && attributes.isItem !== false };
}

function list(children) {
    return { children };
}

test("an index counts only real rows, never the fallback ghost", () => {
    // Arrange - SortableJS inserts its ghost into the list during a fallback drag, and it carries
    // the marker copied from the row it was cloned from.
    const first = row();
    const ghost = row();
    const target = row();
    globalThis.Sortable = { ghost, clone: null };

    // Act
    const index = indexOfChild(list([first, ghost, target]), target);

    // Assert - 1, the position in the .NET collection, not 2, the position in the DOM.
    assert.equal(index, 1);
});

test("an index skips the clone SortableJS leaves behind in clone mode", () => {
    // Arrange
    const clone = row();
    const target = row();
    globalThis.Sortable = { ghost: null, clone };

    // Act & Assert
    assert.equal(indexOfChild(list([clone, target]), target), 0);
});

test("an index ignores children that are not rows", () => {
    // Arrange
    const decoration = row({ isItem: false });
    const target = row();
    globalThis.Sortable = {};

    // Act & Assert
    assert.equal(indexOfChild(list([decoration, target]), target), 0);
});

test("an absent container or child yields no index", () => {
    globalThis.Sortable = {};
    assert.equal(indexOfChild(null, row()), -1);
    assert.equal(indexOfChild(list([]), null), -1);
    assert.equal(indexOfChild(list([row()]), row()), -1);
});

test("a click that never became a drag releases the queue", () => {
    // Arrange & Act & Assert - SortableJS guards "end" behind Sortable.active, so choose/unchoose
    // with no start is all a plain click produces. Leaving the queue open would put every list on
    // the page back behind one chain.
    assert.equal(resolveQueueOwner("choose", false), "open");
    assert.equal(resolveQueueOwner("unchoose", false), "close");
});

test("a real drag keeps the queue open past unchoose", () => {
    // "unchoose" precedes add/remove/sort during a drop, and releasing there would split a
    // cross-list add/remove pair across two queues.
    assert.equal(resolveQueueOwner("unchoose", true), "keep");
    assert.equal(resolveQueueOwner("end", true), "close");
});

test("other events leave the queue alone", () => {
    for (const eventName of ["start", "add", "remove", "sort", "update", "move", "spill"]) {
        assert.equal(resolveQueueOwner(eventName, true), "keep");
    }
});
