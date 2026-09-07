// Best-effort window controls for an app running in its own chromeless
// "--app" browser window (see the multi-window mechanism in Jabasoft's
// MainWindow.xaml.cs / OpenAppWindow). These run purely client-side (no
// server round trip) so the click stays inside the browser's "user
// activation" window - required for the Fullscreen API and safest for
// window.close() too.
//
// Only meaningful for an app that can actually run standalone in such a
// window (Stylebook, TabStudio, LocalAiStudio); an app hosted in a real OS
// window with native chrome (Jabasoft's own WPF shell) already has real
// minimize/maximize/close and shouldn't render this header block's
// .controls buttons at all.
//
// Ported from LocalAiStudio.Web's per-app wwwroot/js/window-controls.js -
// identical logic, moved here so non-Blazor apps (which have no CSS-
// isolation-scoped wwwroot of their own the way a Razor project does) can
// reference one shared copy instead of pasting it in again.
//
// Browsers deliberately do not expose an API to truly minimize a native OS
// window (that would be a security/UX foot-gun), so "minimize" can only ask
// the window to lose focus (window.blur()); most browsers ignore this for a
// window they consider "trusted". If you need a real minimize, it has to
// come from the native host process, not from here.
window.jabaSoftWindowControls = (function () {
    function minimize() {
        try {
            window.blur();
        } catch (e) {
            // No browser API can force a true OS-level minimize from script.
        }
    }

    function toggleFullscreen() {
        if (!document.fullscreenElement) {
            document.documentElement.requestFullscreen().catch(function () { });
        } else {
            document.exitFullscreen().catch(function () { });
        }
    }

    function close() {
        // Only works if the browser treats this as a window it's allowed to
        // close (true for windows started via "--app="); otherwise it's a
        // silent no-op.
        window.close();
    }

    function syncMaxButton() {
        var button = document.querySelector(".shell-header .controls .max");
        if (!button) {
            return;
        }

        var isFullscreen = !!document.fullscreenElement;
        button.textContent = isFullscreen ? "❑" : "□";
        button.setAttribute("aria-label", isFullscreen ? "Restore" : "Maximize");
    }

    document.addEventListener("fullscreenchange", syncMaxButton);

    return {
        minimize: minimize,
        toggleFullscreen: toggleFullscreen,
        close: close
    };
})();
