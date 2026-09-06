/* Applies whichever theme choice is stored in localStorage to this page's
   own <html> element, and exposes window.jabasoftTheme.set(...) for a
   toggle button to change it. Any JabaSoft app can include this file the
   same way it includes jabasoft-theme.css to support switching to
   vs-theme.css (or a future alternate theme) - each app's choice is
   stored per-origin (localStorage doesn't cross origins), which is
   expected: TabStudio and LocalAiStudio are separate apps with separate
   sessions, not one shared page. */
(function () {
    "use strict";

    var STORAGE_KEY = "jabasoft-theme";

    function applyStoredTheme() {
        var theme = null;
        try {
            theme = localStorage.getItem(STORAGE_KEY);
        } catch (e) {
            // localStorage unavailable (e.g. blocked) - just keep the default theme.
        }

        if (theme === "vs") {
            document.documentElement.setAttribute("data-theme", "vs");
        } else {
            document.documentElement.removeAttribute("data-theme");
        }
        syncControls();
    }

    // Keeps any Thema picker on the page (see settings-page.css's
    // .theme-picker - a radio per theme, marked with data-theme-option)
    // in sync with the actual applied theme. Called after every theme
    // change and exposed as .sync() for a Blazor page to call from
    // OnAfterRenderAsync, since a server-rendered re-render doesn't know
    // about this client-only, per-origin localStorage state.
    function syncControls() {
        var current = document.documentElement.getAttribute("data-theme") === "vs" ? "vs" : "lcars";
        var controls = document.querySelectorAll("[data-theme-option]");
        for (var i = 0; i < controls.length; i++) {
            controls[i].checked = controls[i].getAttribute("data-theme-option") === current;
        }
    }

    function setTheme(theme) {
        try {
            localStorage.setItem(STORAGE_KEY, theme);
        } catch (e) {
            // ignore - theme just won't persist across pages this session
        }
        applyStoredTheme();
    }

    window.jabasoftTheme = { apply: applyStoredTheme, set: setTheme, sync: syncControls };
    applyStoredTheme();
    document.addEventListener("DOMContentLoaded", syncControls);
})();
