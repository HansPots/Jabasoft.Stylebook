/* Thin wrapper around Monaco Editor, giving every JabaSoft app the same
   code-editor look/behavior without each app pulling in and wiring up
   Monaco itself. Loaded from a CDN (jsdelivr, npm-mirrored) rather than
   vendored locally - this repo family has no npm/bundler setup anywhere,
   and hand-assembling Monaco's AMD loader + worker files without one is
   its own project. Revisit if the CDN dependency becomes a real problem
   (offline dev, etc.) - vendoring via npm is the natural next step then.

   Usage: window.jabasoftEditor.create(containerOrId, { value, language })
   returns a Promise<monaco.editor.IStandaloneCodeEditor> - once you have
   it, use Monaco's own API directly (.getValue()/.setValue()/
   .onDidChangeModelContent(...)/.dispose()), no extra wrapping on top.

   Theming: two custom Monaco themes ("jabasoft-lcars", "jabasoft-vs")
   loosely matching jabasoft-theme.css/vs-theme.css's dark code-editor
   convention (Stylebook's own existing #css-editor: background #1b1b1b,
   text #d8d8d8) - Monaco's theme API needs literal colors, not CSS custom
   properties, so these are NOT pulled from the stylesheets automatically
   and must be kept in sync by hand if those change. A MutationObserver on
   <html>'s data-theme attribute keeps every created editor's theme in
   sync with LCARS/VS switches (same signal theme.js itself reacts to),
   without needing to hook into theme.js's own sync() and risk stacking
   wrappers across multiple editor instances. */
(function () {
    "use strict";

    var MONACO_VERSION = "0.52.2";
    var MONACO_BASE = "https://cdn.jsdelivr.net/npm/monaco-editor@" + MONACO_VERSION + "/min/vs";
    var loaderPromise = null;

    function loadMonaco() {
        if (loaderPromise) {
            return loaderPromise;
        }

        loaderPromise = new Promise(function (resolve, reject) {
            var script = document.createElement("script");
            script.src = MONACO_BASE + "/loader.js";
            script.onload = function () {
                window.require.config({ paths: { vs: MONACO_BASE } });
                window.require(["vs/editor/editor.main"], function () {
                    defineThemes(window.monaco);
                    resolve(window.monaco);
                });
            };
            script.onerror = reject;
            document.head.appendChild(script);
        });

        return loaderPromise;
    }

    function defineThemes(monaco) {
        monaco.editor.defineTheme("jabasoft-lcars", {
            base: "vs-dark",
            inherit: true,
            rules: [],
            colors: {
                "editor.background": "#1b1b1b",
                "editor.foreground": "#d8d8d8",
                "editorCursor.foreground": "#f5a94a",
                "editor.selectionBackground": "#8f5fc055",
            },
        });

        monaco.editor.defineTheme("jabasoft-vs", {
            base: "vs-dark",
            inherit: true,
            rules: [],
            colors: {
                "editor.background": "#1e1e1e",
                "editor.foreground": "#cccccc",
            },
        });
    }

    function currentThemeName() {
        return document.documentElement.getAttribute("data-theme") === "vs" ? "jabasoft-vs" : "jabasoft-lcars";
    }

    window.jabasoftEditor = {
        /**
         * options.path (optional): a file name/path, e.g. "Foo.cs" or
         * "styles/app.css" - if given, Monaco infers the language from its
         * extension (its own built-in mapping, no list to maintain here)
         * instead of needing an explicit options.language. Real file
         * content should generally pass this rather than a hardcoded
         * language, since a real file's actual name is already known.
         */
        create: function (container, options) {
            var el = typeof container === "string" ? document.getElementById(container) : container;
            options = options || {};

            return loadMonaco().then(function (monaco) {
                var model = options.path
                    ? monaco.editor.createModel(options.value || "", undefined, monaco.Uri.file(options.path))
                    : monaco.editor.createModel(options.value || "", options.language || "css");

                var editor = monaco.editor.create(el, {
                    model: model,
                    theme: currentThemeName(),
                    automaticLayout: true,
                    minimap: { enabled: false },
                    fontSize: 13,
                });

                var observer = new MutationObserver(function () {
                    monaco.editor.setTheme(currentThemeName());
                });
                observer.observe(document.documentElement, { attributes: true, attributeFilter: ["data-theme"] });

                return editor;
            });
        },
    };
})();
