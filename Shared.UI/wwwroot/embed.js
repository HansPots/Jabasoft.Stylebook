/* Lets a page know whether it's running inside Jabasoft's shell (embedded
   in its content <iframe>) versus opened standalone in a real browser tab,
   and gives it a way to ask the shell to go back to Jabasoft's own home
   screen - the shell then has the app fill the entire window (no
   Jabasoft header/menu/action rail visible at all, see Jabasoft.App's
   shell.js/.css), so the only way back is a "Home" nav item the app adds
   to its own menu, shown only when jabasoftEmbed.isEmbedded() is true
   (see jabasoft-theme.css's .jbs-home-link rule). window.top being
   inaccessible would itself throw on a cross-origin embed, which is
   exactly the case here (app.jabasoft.local embedding this app's own
   origin) - the try/catch treats that as "embedded" too. */
(function () {
    "use strict";

    var embedded;
    try {
        embedded = window.self !== window.top;
    } catch (e) {
        embedded = true;
    }

    if (embedded) {
        document.documentElement.classList.add("jbs-embedded");
    }

    window.jabasoftEmbed = {
        isEmbedded: function () {
            return embedded;
        },
        goHome: function () {
            if (embedded) {
                window.parent.postMessage("jabasoft:home", "*");
            }
        },
    };
})();
