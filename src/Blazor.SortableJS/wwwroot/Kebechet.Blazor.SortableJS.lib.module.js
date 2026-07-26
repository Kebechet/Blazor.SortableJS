// Blazor JS initializer. Blazor auto-discovers "{PackageId}.lib.module.js" and runs the exported
// hooks on startup, so consumers never add a <script> tag manually. We inject the SortableJS
// bundle, which defines the global `Sortable` constructor the interop module builds on.
//
// The bundle is vendored and pinned rather than pulled from a CDN: a CDN reference breaks
// reordering offline (fatal in a MAUI WebView), lets an already-shipped build change behaviour
// without a release, and counts as downloading executable code at runtime under App Store
// guideline 2.5.2.

export function beforeWebStart(options, extensions) {
    injectSortable();
}

export function beforeStart(options, extensions) {
    injectSortable();
}

function injectSortable() {
    const marker = "data-kebechet-sortablejs";
    if (document.querySelector(`script[${marker}]`)) {
        return;
    }

    const script = document.createElement("script");
    script.src = "_content/Kebechet.Blazor.SortableJS/Sortable.min.js";
    script.setAttribute(marker, "");
    document.head.appendChild(script);
}
