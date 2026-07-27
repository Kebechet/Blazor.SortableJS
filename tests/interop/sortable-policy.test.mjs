import assert from "node:assert/strict";
import test from "node:test";

import {
    indexOfChild,
    resolveQueueOwner,
    canReleaseQueue,
    itemMarkerAttribute
} from "../../src/Blazor.SortableJS/wwwroot/sortable-policy.js";

const itemMarker = itemMarkerAttribute;

function row(attributes = {}) {
    return { hasAttribute: name => name === itemMarker && attributes.isItem !== false };
}

function list(children) {
    return { children };
}

test("IndexOfChild_FallbackGhostInTheList_CountsOnlyRealRows", () => {
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

test("IndexOfChild_CloneInTheList_SkipsTheClone", () => {
    // Arrange
    const clone = row();
    const target = row();
    globalThis.Sortable = { ghost: null, clone };

    // Act & Assert
    assert.equal(indexOfChild(list([clone, target]), target), 0);
});

test("IndexOfChild_NonRowChild_IsIgnored", () => {
    // Arrange
    const decoration = row({ isItem: false });
    const target = row();
    globalThis.Sortable = {};

    // Act & Assert
    assert.equal(indexOfChild(list([decoration, target]), target), 0);
});

test("IndexOfChild_AbsentContainerOrChild_YieldsNoIndex", () => {
    globalThis.Sortable = {};
    assert.equal(indexOfChild(null, row()), -1);
    assert.equal(indexOfChild(list([]), null), -1);
    assert.equal(indexOfChild(list([row()]), row()), -1);
});

test("ResolveQueueOwner_ClickThatNeverBecameADrag_ReleasesTheQueue", () => {
    // Arrange & Act & Assert - SortableJS guards "end" behind Sortable.active, so choose/unchoose
    // with no start is all a plain click produces. Leaving the queue open would put every list on
    // the page back behind one chain.
    assert.equal(resolveQueueOwner("choose", false), "open");
    assert.equal(resolveQueueOwner("unchoose", false), "close");
});

test("ResolveQueueOwner_RealDrag_KeepsTheQueueOpenPastUnchoose", () => {
    // "unchoose" precedes add/remove/sort during a drop, and releasing there would split a
    // cross-list add/remove pair across two queues.
    assert.equal(resolveQueueOwner("unchoose", true), "keep");
    assert.equal(resolveQueueOwner("end", true), "close");
});

test("ResolveQueueOwner_UnrelatedEvent_LeavesTheQueueAlone", () => {
    for (const eventName of ["start", "add", "remove", "sort", "update", "move", "spill"]) {
        assert.equal(resolveQueueOwner(eventName, true), "keep");
    }
});

test("QueueRelease_TimerFromTheDragThatScheduledIt_Releases", () => {
    assert.equal(canReleaseQueue(4, 4), true);
});

test("QueueRelease_TimerOutlivedByANewDrag_DoesNotRelease", () => {
    // The spill path defers its release through a timer that reads the queue state when it fires.
    // Without this guard a late timer would release a queue the next drag is still using.
    assert.equal(canReleaseQueue(4, 5), false);
});
