(function () {
    "use strict";

    var appGroups = document.getElementById("app-groups");
    var componentList = document.getElementById("component-list");
    var previewFrame = document.getElementById("preview-frame");
    var previewTitle = document.getElementById("preview-title");
    var previewOpen = document.getElementById("preview-open");
    var homeBtn = document.getElementById("home-btn");
    var refreshBtn = document.getElementById("refresh-btn");
    var cssEditor = document.getElementById("css-editor");
    var saveCssBtn = document.getElementById("save-css-btn");
    var cssStatus = document.getElementById("css-status");
    var themePanel = document.getElementById("theme-panel");
    var componentPanel = document.getElementById("component-panel");
    var componentPanelTitle = document.getElementById("component-panel-title");
    var componentCssEditor = document.getElementById("component-css-editor");
    var componentCssStatus = document.getElementById("component-css-status");
    var saveComponentCssBtn = document.getElementById("save-component-css-btn");
    var aiInstructions = document.getElementById("ai-instructions");
    var generateCssBtn = document.getElementById("generate-css-btn");
    var materializeBtn = document.getElementById("materialize-btn");
    var materializeResult = document.getElementById("materialize-result");
    var deleteComponentBtn = document.getElementById("delete-component-btn");
    var tabPages = document.getElementById("tab-pages");
    var tabComponents = document.getElementById("tab-components");
    var tabSettings = document.getElementById("tab-settings");
    var settingsList = document.getElementById("settings-list");
    var settingsPanel = document.getElementById("settings-panel");
    var selectElementBtn = document.getElementById("select-element-btn");
    var showRegionsBtn = document.getElementById("show-regions-btn");
    var pickerPanel = document.getElementById("picker-panel");
    var pickerHtmlPreview = document.getElementById("picker-html-preview");
    var pickerParentBtn = document.getElementById("picker-parent-btn");
    var pickerNameInput = document.getElementById("picker-name-input");
    var pickerGroupSelect = document.getElementById("picker-group-select");
    var pickerSaveBtn = document.getElementById("picker-save-btn");
    var pickerCancelBtn = document.getElementById("picker-cancel-btn");
    var aiProviderSelect = document.getElementById("ai-provider");
    var aiServerUrlInput = document.getElementById("ai-server-url");
    var aiModelSelect = document.getElementById("ai-model");
    var aiRefreshModelsBtn = document.getElementById("ai-refresh-models-btn");
    var aiTestBtn = document.getElementById("ai-test-btn");
    var aiSettingsStatus = document.getElementById("ai-settings-status");
    var aiSettingsSaveBtn = document.getElementById("ai-settings-save-btn");
    var splitter1 = document.getElementById("splitter-1");
    var splitter2 = document.getElementById("splitter-2");
    var styleguideEl = document.querySelector(".styleguide");

    var lastApps = null;
    var currentApp = null;
    var currentPage = null;
    var currentPageButton = null;
    var currentComponentName = null;
    // What the main preview (previewFrame) currently shows - "page" (a
    // captured page, loaded via .src) or "component" (an isolated
    // component preview, loaded via .srcdoc - see editComponent). Placing
    // a component (see placeComponentInPage) only makes sense on top of
    // an actual page, so this also gates the "place" icon per component row.
    var previewMode = null;
    var PLACED_COMPONENT_ID = "stylebook-placed-component";
    var pickerActive = false;
    var pickerSelectedEl = null;
    var regionsActive = false;
    var savedModel = "";

    // Stylebook is embedded full-window in Jabasoft's shell just like
    // TabStudio/LocalAiStudio (see Jabasoft.App/appsettings.json's
    // Apps:Stylebook entry) - embed.js (_content/Shared.UI/embed.js)
    // detects that and exposes window.jabasoftEmbed.goHome().
    homeBtn.addEventListener("click", function () {
        window.jabasoftEmbed && window.jabasoftEmbed.goHome();
    });

    // ============================================================
    // Pagina's (unchanged behavior, see loadPage/renderApps/refreshPages)
    // ============================================================

    function loadPage(app, page, buttonEl) {
        var btn = buttonEl || currentPageButton;
        currentApp = app;
        currentPage = page;
        currentPageButton = btn;
        previewMode = "page";
        previewFrame.removeAttribute("srcdoc");
        previewFrame.src = page.file + "?t=" + Date.now();
        previewTitle.textContent = (app.displayName || "") + " — " + (page.label || page.path) + " (kopie)";
        previewOpen.href = (app.mainUrl || app.developmentUrl) + page.path;
        previewOpen.classList.remove("disabled");

        var buttons = appGroups.querySelectorAll("button.page-link");
        for (var i = 0; i < buttons.length; i++) {
            buttons[i].classList.remove("active");
        }
        if (btn) {
            btn.classList.add("active");
        }

        updatePlaceButtonsState();
    }

    // Shared collapsible-group widget: a clickable header (chevron + title)
    // toggling a body underneath. Used for both the Pagina's app groups and
    // the Componenten groups (Header/Footer/... - see renderComponents),
    // so the two lists behave and look identical.
    function createCollapsibleGroup(titleText, startExpanded) {
        var group = document.createElement("div");
        group.className = "collapsible-group";

        var header = document.createElement("button");
        header.type = "button";
        header.className = "collapsible-group-header";

        var chevron = document.createElement("span");
        chevron.className = "collapsible-group-chevron";
        chevron.textContent = "▸";
        header.appendChild(chevron);

        var title = document.createElement("span");
        title.className = "collapsible-group-title";
        title.textContent = titleText;
        header.appendChild(title);

        var body = document.createElement("div");
        body.className = "collapsible-group-body";
        body.hidden = !startExpanded;
        chevron.classList.toggle("open", !!startExpanded);

        header.addEventListener("click", function () {
            body.hidden = !body.hidden;
            chevron.classList.toggle("open", !body.hidden);
        });

        group.appendChild(header);
        group.appendChild(body);
        return { group: group, body: body, setExpanded: function (expanded) {
            body.hidden = !expanded;
            chevron.classList.toggle("open", expanded);
        } };
    }

    function renderApps(apps) {
        lastApps = apps;
        appGroups.innerHTML = "";
        var firstApp = null;
        var firstPage = null;
        var firstButton = null;
        var firstGroup = null;
        var appKeys = Object.keys(apps || {});

        if (appKeys.length === 0) {
            var empty = document.createElement("div");
            empty.className = "styleguide-nav-empty";
            empty.textContent = "Geen apps gevonden in appsettings.json.";
            appGroups.appendChild(empty);
            return;
        }

        appKeys.forEach(function (key) {
            var app = apps[key];
            var collapsible = createCollapsibleGroup(app.displayName || key, false);

            var pages = app.pages || [];
            if (pages.length === 0) {
                var noPages = document.createElement("div");
                noPages.className = "styleguide-nav-empty";
                noPages.textContent = "Geen pagina's geconfigureerd.";
                collapsible.body.appendChild(noPages);
            }

            pages.forEach(function (page) {
                var button = document.createElement("button");
                button.type = "button";
                button.className = "page-link";
                button.textContent = page.label || page.path;
                button.addEventListener("click", function () {
                    loadPage(app, page, button);
                });
                collapsible.body.appendChild(button);

                if (!firstApp) {
                    firstApp = app;
                    firstPage = page;
                    firstButton = button;
                    firstGroup = collapsible;
                }
            });

            appGroups.appendChild(collapsible.group);
        });

        // Collapsed by default (see the user's request: "eerst alleen de
        // applicaties") - only the group holding the page that's about to
        // auto-load below starts expanded, so the active page is visible
        // without an extra click.
        if (firstGroup) {
            firstGroup.setExpanded(true);
        }

        if (firstApp && firstPage) {
            loadPage(firstApp, firstPage, firstButton);
        }
    }

    function fetchAppsConfig() {
        return fetch("/api/apps-config")
            .then(function (response) {
                if (!response.ok) {
                    throw new Error("HTTP " + response.status);
                }
                return response.json();
            });
    }

    function refreshPages() {
        refreshBtn.disabled = true;
        previewTitle.textContent = "Pagina's kopiëren...";

        fetchAppsConfig()
            .then(function (apps) {
                renderApps(apps);
                return fetch("/api/capture-pages", { method: "POST" });
            })
            .then(function (response) {
                if (!response.ok) {
                    throw new Error("HTTP " + response.status);
                }
                return response.json();
            })
            .then(function (results) {
                var failed = (results || []).filter(function (r) { return !r.ok; });
                if (failed.length > 0) {
                    previewTitle.textContent = failed.length + " pagina('s) konden niet gekopieerd worden (staat de app aan?).";
                } else if (lastApps) {
                    renderApps(lastApps);
                }
            })
            .catch(function (err) {
                previewTitle.textContent = "Verversen mislukt: " + err;
            })
            .finally(function () {
                refreshBtn.disabled = false;
            });
    }

    function loadCss() {
        cssStatus.textContent = "Laden...";
        fetch("/api/theme-css")
            .then(function (response) {
                if (!response.ok) {
                    throw new Error("HTTP " + response.status);
                }
                return response.text();
            })
            .then(function (text) {
                cssEditor.value = text;
                cssStatus.textContent = "";
            })
            .catch(function (err) {
                cssStatus.textContent = "Kon jabasoft-theme.css niet laden: " + err;
            });
    }

    function saveCss() {
        cssStatus.textContent = "Opslaan...";
        saveCssBtn.disabled = true;
        fetch("/api/theme-css", {
            method: "PUT",
            headers: { "Content-Type": "text/plain" },
            body: cssEditor.value,
        })
            .then(function (response) {
                if (!response.ok) {
                    throw new Error("HTTP " + response.status);
                }
                cssStatus.textContent = "Opgeslagen. Voorbeeld wordt herladen...";
                previewFrame.src = previewFrame.src;
                setTimeout(function () { cssStatus.textContent = ""; }, 2000);
            })
            .catch(function (err) {
                cssStatus.textContent = "Opslaan mislukt: " + err;
            })
            .finally(function () {
                saveCssBtn.disabled = false;
            });
    }

    // ============================================================
    // Tabs: Pagina's / Componenten / Instellingen
    // ============================================================

    function deactivateAllTabs() {
        tabPages.classList.remove("active");
        tabComponents.classList.remove("active");
        tabSettings.classList.remove("active");
        appGroups.hidden = true;
        componentList.hidden = true;
        settingsList.hidden = true;
        themePanel.hidden = true;
        componentPanel.hidden = true;
        settingsPanel.hidden = true;
    }

    function showPagesTab() {
        deactivateAllTabs();
        tabPages.classList.add("active");
        appGroups.hidden = false;
        themePanel.hidden = false;
        selectElementBtn.disabled = false;
        showRegionsBtn.disabled = false;

        // Editing a component (see editComponent) points the main preview
        // at an isolated component instead of a page - "Selecteer element"/
        // "Plaatsen" only make sense on a real page, so coming back here
        // restores whichever page was last shown, instead of silently
        // leaving the component preview up while looking like a page tab.
        if (previewMode !== "page" && currentApp && currentPage) {
            loadPage(currentApp, currentPage);
        }
    }

    function showComponentsTab() {
        deactivateAllTabs();
        tabComponents.classList.add("active");
        componentList.hidden = false;
        // select-element/show-regions only make sense on a captured page,
        // not on a component's own isolated preview.
        selectElementBtn.disabled = true;
        showRegionsBtn.disabled = true;
        fetchComponents();
    }

    function showSettingsTab() {
        deactivateAllTabs();
        tabSettings.classList.add("active");
        settingsList.hidden = false;
        settingsPanel.hidden = false;
        selectElementBtn.disabled = true;
        showRegionsBtn.disabled = true;
        loadAiSettings();
    }

    tabPages.addEventListener("click", showPagesTab);
    tabComponents.addEventListener("click", showComponentsTab);
    tabSettings.addEventListener("click", showSettingsTab);

    // ============================================================
    // Componenten: lijst + eigen preview/CSS-editor/AI-generatie/materialize
    // ============================================================

    function fetchComponents() {
        componentList.innerHTML = "Laden...";
        fetch("/api/components")
            .then(function (r) { return r.json(); })
            .then(renderComponents)
            .catch(function (err) {
                componentList.textContent = "Kon componenten niet laden: " + err;
            });
    }

    // Groups always shown even when empty, in this fixed order, so the
    // structure is visible right away - any other group name found on a
    // component (or "Overig" for one with none) is appended after these,
    // alphabetically, with "Overig" always last since it's the catch-all.
    var FIXED_COMPONENT_GROUPS = ["Header", "Menu", "Content", "Actionrail", "Footer"];

    function renderComponents(components) {
        componentList.innerHTML = "";
        components = components || [];

        var byGroup = {};
        FIXED_COMPONENT_GROUPS.forEach(function (g) { byGroup[g] = []; });
        components.forEach(function (comp) {
            var g = comp.group || "Overig";
            if (!byGroup[g]) {
                byGroup[g] = [];
            }
            byGroup[g].push(comp);
        });

        var groupNames = Object.keys(byGroup).filter(function (g) {
            return FIXED_COMPONENT_GROUPS.indexOf(g) === -1 && g !== "Overig";
        }).sort();
        var orderedGroups = FIXED_COMPONENT_GROUPS.concat(groupNames);
        if (byGroup["Overig"]) {
            orderedGroups.push("Overig");
        }

        if (components.length === 0) {
            var empty = document.createElement("div");
            empty.className = "styleguide-nav-empty";
            empty.textContent = "Nog geen componenten. Gebruik \"Selecteer element\" op een pagina.";
            componentList.appendChild(empty);
        }

        orderedGroups.forEach(function (groupName) {
            var collapsible = createCollapsibleGroup(groupName, false);
            var comps = byGroup[groupName] || [];

            if (comps.length === 0) {
                var noComps = document.createElement("div");
                noComps.className = "styleguide-nav-empty";
                noComps.textContent = "Nog geen componenten in deze groep.";
                collapsible.body.appendChild(noComps);
            }

            comps.forEach(function (comp) {
                collapsible.body.appendChild(buildComponentRow(comp));
            });

            componentList.appendChild(collapsible.group);
        });

        updatePlaceButtonsState();
    }

    function buildComponentRow(comp) {
        var row = document.createElement("div");
        row.className = "component-row";

        var name = document.createElement("span");
        name.className = "component-name";
        name.textContent = comp.name;
        row.appendChild(name);

        var actions = document.createElement("div");
        actions.className = "component-row-actions";

        var editBtn = document.createElement("button");
        editBtn.type = "button";
        editBtn.className = "icon-btn component-edit-btn";
        editBtn.title = "Bewerken (toont het component in de hoofdpreview)";
        editBtn.textContent = "✎";
        editBtn.addEventListener("click", function () {
            var rows = componentList.querySelectorAll(".component-row");
            for (var i = 0; i < rows.length; i++) {
                rows[i].classList.remove("active");
            }
            row.classList.add("active");
            editComponent(comp.name);
        });
        actions.appendChild(editBtn);

        var placeBtn = document.createElement("button");
        placeBtn.type = "button";
        placeBtn.className = "icon-btn component-place-btn";
        placeBtn.title = "Plaatsen in de getoonde pagina (alleen preview, niet opgeslagen)";
        placeBtn.textContent = "➕";
        placeBtn.addEventListener("click", function () {
            placeComponentInPage(comp.name);
        });
        actions.appendChild(placeBtn);

        row.appendChild(actions);
        return row;
    }

    function editComponent(name) {
        currentComponentName = name;
        componentPanelTitle.textContent = name;
        componentPanel.hidden = false;
        materializeResult.textContent = "";
        componentCssStatus.textContent = "Laden...";

        Promise.all([
            fetch("/api/components/" + name + "/html?t=" + Date.now()).then(function (r) { return r.text(); }),
            fetch("/api/components/" + name + "/css?t=" + Date.now()).then(function (r) { return r.ok ? r.text() : ""; }),
        ]).then(function (results) {
            var html = results[0];
            var css = results[1];
            componentCssEditor.value = css;
            componentCssStatus.textContent = "";
            renderComponentPreview(name, html, css);
        }).catch(function (err) {
            componentCssStatus.textContent = "Kon component niet laden: " + err;
        });
    }

    // Shows a component in isolation in the main preview (previewFrame) -
    // replacing whatever page was shown there, per the user's choice: the
    // page preview disappears, only the component + edit tools remain
    // visible. Switching back to a page (loadPage) or another app entirely
    // replaces it again.
    function renderComponentPreview(name, html, css) {
        var doc =
            "<!DOCTYPE html><html><head>" +
            "<link rel=\"stylesheet\" href=\"/_content/Shared.UI/jabasoft-theme.css\" />" +
            "<link rel=\"stylesheet\" href=\"/_content/Shared.UI/vs-theme.css\" />" +
            "<script src=\"/_content/Shared.UI/theme.js\"><\/script>" +
            "<style>" + css + "</style>" +
            "</head><body>" + html + "</body></html>";

        previewMode = "component";
        previewFrame.removeAttribute("src");
        previewFrame.srcdoc = doc;
        previewTitle.textContent = "Component — " + name;
        previewOpen.classList.add("disabled");
        updatePlaceButtonsState();
    }

    // Inserts a component's current HTML+CSS at the end of the currently
    // shown page's live preview, purely for visual reference - nothing is
    // written back to the captured page file, so navigating away (or
    // refreshing) discards it. Re-placing (the same or another component)
    // replaces whatever was placed before, rather than stacking up copies.
    function placeComponentInPage(name) {
        if (previewMode !== "page" || !currentPage) {
            return;
        }

        var frameDoc = previewFrame.contentDocument;
        if (!frameDoc) {
            return;
        }

        Promise.all([
            fetch("/api/components/" + name + "/html?t=" + Date.now()).then(function (r) { return r.text(); }),
            fetch("/api/components/" + name + "/css?t=" + Date.now()).then(function (r) { return r.ok ? r.text() : ""; }),
        ]).then(function (results) {
            var html = results[0];
            var css = results[1];

            var existing = frameDoc.getElementById(PLACED_COMPONENT_ID);
            if (existing) {
                existing.remove();
            }

            var wrapper = frameDoc.createElement("div");
            wrapper.id = PLACED_COMPONENT_ID;
            wrapper.innerHTML = "<style>" + css + "</style>" + html;
            frameDoc.body.appendChild(wrapper);
            wrapper.scrollIntoView({ behavior: "smooth", block: "end" });
        });
    }

    function updatePlaceButtonsState() {
        var canPlace = previewMode === "page" && !!currentPage;
        var buttons = componentList.querySelectorAll(".component-place-btn");
        for (var i = 0; i < buttons.length; i++) {
            buttons[i].disabled = !canPlace;
        }
    }

    function saveComponentCss() {
        if (!currentComponentName) {
            return;
        }

        componentCssStatus.textContent = "Opslaan...";
        saveComponentCssBtn.disabled = true;
        fetch("/api/components/" + currentComponentName + "/css", {
            method: "PUT",
            headers: { "Content-Type": "text/plain" },
            body: componentCssEditor.value,
        })
            .then(function (response) {
                if (!response.ok) {
                    throw new Error("HTTP " + response.status);
                }
                componentCssStatus.textContent = "Opgeslagen.";
                fetch("/api/components/" + currentComponentName + "/html?t=" + Date.now())
                    .then(function (r) { return r.text(); })
                    .then(function (html) { renderComponentPreview(currentComponentName, html, componentCssEditor.value); });
                setTimeout(function () { componentCssStatus.textContent = ""; }, 2000);
            })
            .catch(function (err) {
                componentCssStatus.textContent = "Opslaan mislukt: " + err;
            })
            .finally(function () {
                saveComponentCssBtn.disabled = false;
            });
    }

    function generateComponentCss() {
        if (!currentComponentName) {
            return;
        }

        generateCssBtn.disabled = true;
        componentCssStatus.textContent = "AI denkt na...";
        fetch("/api/components/" + currentComponentName + "/generate-css", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ instructions: aiInstructions.value, currentCss: componentCssEditor.value }),
        })
            .then(function (r) { return r.json(); })
            .then(function (result) {
                if (result.success) {
                    componentCssEditor.value = result.css;
                    componentCssStatus.textContent = "Voorstel geladen - controleer en klik Opslaan.";
                    fetch("/api/components/" + currentComponentName + "/html?t=" + Date.now())
                        .then(function (r) { return r.text(); })
                        .then(function (html) { renderComponentPreview(currentComponentName, html, result.css); });
                } else {
                    componentCssStatus.textContent = "AI-generatie mislukt: " + result.errorMessage;
                }
            })
            .catch(function (err) {
                componentCssStatus.textContent = "AI-generatie mislukt: " + err;
            })
            .finally(function () {
                generateCssBtn.disabled = false;
            });
    }

    function materializeComponent() {
        if (!currentComponentName) {
            return;
        }

        materializeBtn.disabled = true;
        materializeResult.textContent = "Bezig...";
        fetch("/api/components/" + currentComponentName + "/materialize", { method: "POST" })
            .then(function (r) { return r.json(); })
            .then(function (result) {
                materializeResult.textContent =
                    "Aangemaakt: " + result.razorPath + " — plaats " + result.usageSnippet + " op de pagina waar je 'm wilt.";
            })
            .catch(function (err) {
                materializeResult.textContent = "Mislukt: " + err;
            })
            .finally(function () {
                materializeBtn.disabled = false;
            });
    }

    function deleteComponent() {
        if (!currentComponentName) {
            return;
        }

        if (!window.confirm("Component \"" + currentComponentName + "\" verwijderen? Dit kan niet ongedaan gemaakt worden.")) {
            return;
        }

        var name = currentComponentName;
        deleteComponentBtn.disabled = true;
        fetch("/api/components/" + name, { method: "DELETE" })
            .then(function (response) {
                if (!response.ok) {
                    throw new Error("HTTP " + response.status);
                }
                currentComponentName = null;
                componentPanel.hidden = true;
                fetchComponents();
            })
            .catch(function (err) {
                materializeResult.textContent = "Verwijderen mislukt: " + err;
            })
            .finally(function () {
                deleteComponentBtn.disabled = false;
            });
    }

    saveComponentCssBtn.addEventListener("click", saveComponentCss);
    generateCssBtn.addEventListener("click", generateComponentCss);
    materializeBtn.addEventListener("click", materializeComponent);
    deleteComponentBtn.addEventListener("click", deleteComponent);

    // ============================================================
    // "Selecteer element": click an element in the preview, name it,
    // save it as a component. The preview is same-origin (both the
    // captured page and this tool are served from Stylebook's own
    // origin), so this reaches straight into previewFrame.contentDocument
    // - no postMessage bridging needed.
    // ============================================================

    var PICKER_STYLE_ID = "jbs-picker-style";
    var PICKER_STYLE_CSS = ".jbs-picker-hover { outline: 2px solid #ff9700 !important; outline-offset: -2px; cursor: crosshair !important; }";
    var pickerStartingCss = "";

    // The whole point of pointing at a live element instead of typing HTML
    // by hand is to start from what it actually looks like there - so a new
    // component's CSS is seeded from the picked element's (and its
    // descendants') REAL computed styles, not left blank. This can't read
    // the page's actual authored CSS rules (captured pages pull in
    // cross-origin stylesheets - e.g. TabStudio's own origin - and
    // CSSStyleSheet.cssRules throws across origins without CORS), so
    // getComputedStyle is the only thing that reliably works regardless of
    // where the matching rule actually lives. A curated property list
    // keeps the result readable instead of dumping every computed
    // property (most of which are inherited browser defaults, not
    // anything the original author actually set).
    var CSS_SNAPSHOT_PROPERTIES = [
        "color", "background-color", "background-image", "background-position", "background-size", "background-repeat",
        "font-family", "font-size", "font-weight", "font-style", "line-height", "letter-spacing",
        "text-align", "text-transform", "text-decoration-line",
        "padding-top", "padding-right", "padding-bottom", "padding-left",
        "margin-top", "margin-right", "margin-bottom", "margin-left",
        "border-top-width", "border-right-width", "border-bottom-width", "border-left-width",
        "border-top-style", "border-right-style", "border-bottom-style", "border-left-style",
        "border-top-color", "border-right-color", "border-bottom-color", "border-left-color",
        "border-radius", "box-shadow",
        "display", "flex-direction", "flex-wrap", "align-items", "justify-content", "gap",
        "width", "min-width", "max-width", "height", "min-height", "max-height",
        "opacity", "cursor",
    ];

    function buildSnapshotSelector(el) {
        if (el.classList && el.classList.length > 0) {
            return "." + Array.prototype.slice.call(el.classList).join(".");
        }
        return el.tagName.toLowerCase();
    }

    // One rule per unique selector (first matching element wins) - elements
    // sharing a class are assumed to share styling, same as real CSS.
    function snapshotComputedCss(rootEl, win) {
        if (!rootEl || !win || !win.getComputedStyle) {
            return "";
        }

        var elements = [rootEl].concat(Array.prototype.slice.call(rootEl.querySelectorAll("*")));
        var seenSelectors = {};
        var blocks = [];

        elements.forEach(function (el) {
            var selector = buildSnapshotSelector(el);
            if (seenSelectors[selector]) {
                return;
            }
            seenSelectors[selector] = true;

            var style = win.getComputedStyle(el);
            var lines = [];
            CSS_SNAPSHOT_PROPERTIES.forEach(function (prop) {
                var value = style.getPropertyValue(prop);
                if (value) {
                    lines.push("    " + prop + ": " + value + ";");
                }
            });

            if (lines.length > 0) {
                blocks.push(selector + " {\n" + lines.join("\n") + "\n}");
            }
        });

        return blocks.join("\n\n");
    }

    function onPickerHover(e) {
        var doc = previewFrame.contentDocument;
        var prev = doc.querySelector(".jbs-picker-hover");
        if (prev && prev !== e.target) {
            prev.classList.remove("jbs-picker-hover");
        }
        e.target.classList.add("jbs-picker-hover");
    }

    // The hover outline (see onPickerHover) is a transient class added to
    // whatever's under the cursor - without stripping it first, a captured
    // component's saved HTML would permanently include "jbs-picker-hover"
    // on whichever element happened to be hovered right before the click.
    function stripPickerArtifacts(el) {
        el.classList.remove("jbs-picker-hover");
        var inner = el.querySelectorAll(".jbs-picker-hover");
        for (var i = 0; i < inner.length; i++) {
            inner[i].classList.remove("jbs-picker-hover");
        }
    }

    function onPickerClick(e) {
        e.preventDefault();
        e.stopPropagation();
        pickerSelectedEl = e.target;
        stripPickerArtifacts(pickerSelectedEl);
        pickerStartingCss = snapshotComputedCss(pickerSelectedEl, previewFrame.contentWindow);
        showPickerPanel();
    }

    function attachPicker() {
        var doc = previewFrame.contentDocument;
        if (!doc) {
            return;
        }

        if (!doc.getElementById(PICKER_STYLE_ID)) {
            var style = doc.createElement("style");
            style.id = PICKER_STYLE_ID;
            style.textContent = PICKER_STYLE_CSS;
            doc.head.appendChild(style);
        }

        doc.addEventListener("mouseover", onPickerHover, true);
        doc.addEventListener("click", onPickerClick, true);
    }

    function detachPicker() {
        var doc = previewFrame.contentDocument;
        if (!doc) {
            return;
        }

        doc.removeEventListener("mouseover", onPickerHover, true);
        doc.removeEventListener("click", onPickerClick, true);
        var hovered = doc.querySelector(".jbs-picker-hover");
        if (hovered) {
            hovered.classList.remove("jbs-picker-hover");
        }
    }

    function togglePicker() {
        pickerActive = !pickerActive;
        selectElementBtn.classList.toggle("active", pickerActive);
        if (pickerActive) {
            attachPicker();
        } else {
            detachPicker();
            hidePickerPanel();
        }
    }

    function showPickerPanel() {
        pickerHtmlPreview.textContent = truncate(pickerSelectedEl.outerHTML, 600);
        pickerNameInput.value = "";
        pickerGroupSelect.value = "Overig";
        pickerPanel.hidden = false;
    }

    function hidePickerPanel() {
        pickerPanel.hidden = true;
        pickerSelectedEl = null;
    }

    function truncate(text, max) {
        return text.length > max ? text.substring(0, max) + "…" : text;
    }

    pickerParentBtn.addEventListener("click", function () {
        if (pickerSelectedEl && pickerSelectedEl.parentElement) {
            pickerSelectedEl = pickerSelectedEl.parentElement;
            stripPickerArtifacts(pickerSelectedEl);
            pickerStartingCss = snapshotComputedCss(pickerSelectedEl, previewFrame.contentWindow);
            pickerHtmlPreview.textContent = truncate(pickerSelectedEl.outerHTML, 600);
        }
    });

    pickerCancelBtn.addEventListener("click", hidePickerPanel);

    pickerSaveBtn.addEventListener("click", function () {
        var name = pickerNameInput.value.trim();
        if (!name || !pickerSelectedEl) {
            return;
        }

        stripPickerArtifacts(pickerSelectedEl);
        pickerSaveBtn.disabled = true;
        fetch("/api/components", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({
                name: name,
                html: pickerSelectedEl.outerHTML,
                sourceApp: currentApp ? (currentApp.displayName || "") : "",
                sourcePath: currentPage ? currentPage.path : "",
                group: pickerGroupSelect.value,
                startingCss: pickerStartingCss,
            }),
        })
            .then(function (r) { return r.json(); })
            .then(function () {
                hidePickerPanel();
                togglePicker();
                showComponentsTab();
            })
            .catch(function (err) {
                pickerHtmlPreview.textContent = "Opslaan mislukt: " + err;
            })
            .finally(function () {
                pickerSaveBtn.disabled = false;
            });
    });

    selectElementBtn.addEventListener("click", togglePicker);

    // ============================================================
    // "Toon regio's": outline header/menu/content/footer/actionrail
    // directly in the preview, using the shared shell CSS classes every
    // captured page already has.
    // ============================================================

    var REGION_STYLE_ID = "jbs-region-style";
    var REGION_STYLE_CSS = [
        ".shell-header, .shell-menu, .shell-content, .shell-footer, .shell-actionrail { position: relative !important; }",
        ".shell-header::before, .shell-menu::before, .shell-content::before, .shell-footer::before, .shell-actionrail::before {",
        "  position: absolute; top: 0; left: 0; z-index: 99999; padding: 2px 6px;",
        "  font: bold 10px monospace; color: #fff; pointer-events: none;",
        "}",
        ".shell-header { outline: 2px dashed #e74c3c !important; }",
        ".shell-header::before { content: 'HEADER'; background: #e74c3c; }",
        ".shell-menu { outline: 2px dashed #3498db !important; }",
        ".shell-menu::before { content: 'MENU'; background: #3498db; }",
        ".shell-content { outline: 2px dashed #2ecc71 !important; }",
        ".shell-content::before { content: 'CONTENT'; background: #2ecc71; }",
        ".shell-footer { outline: 2px dashed #f39c12 !important; }",
        ".shell-footer::before { content: 'FOOTER'; background: #f39c12; }",
        ".shell-actionrail { outline: 2px dashed #9b59b6 !important; }",
        ".shell-actionrail::before { content: 'ACTIERAIL'; background: #9b59b6; }",
    ].join("\n");

    function applyRegions() {
        var doc = previewFrame.contentDocument;
        if (!doc) {
            return;
        }

        var existing = doc.getElementById(REGION_STYLE_ID);
        if (!existing) {
            var style = doc.createElement("style");
            style.id = REGION_STYLE_ID;
            style.textContent = REGION_STYLE_CSS;
            doc.head.appendChild(style);
        }
    }

    function removeRegions() {
        var doc = previewFrame.contentDocument;
        if (!doc) {
            return;
        }

        var existing = doc.getElementById(REGION_STYLE_ID);
        if (existing) {
            existing.remove();
        }
    }

    function toggleRegions() {
        regionsActive = !regionsActive;
        showRegionsBtn.classList.toggle("active", regionsActive);
        if (regionsActive) {
            applyRegions();
        } else {
            removeRegions();
        }
    }

    showRegionsBtn.addEventListener("click", toggleRegions);

    // Both picker and region-overlay inject into the preview document,
    // which is destroyed on every navigation (selecting a different page,
    // "Pagina's verversen", or the theme-CSS-save reload) - re-apply
    // whichever mode is still active once the new document is ready.
    previewFrame.addEventListener("load", function () {
        if (regionsActive) {
            applyRegions();
        }
        if (pickerActive) {
            attachPicker();
        }
    });

    // ============================================================
    // AI-instellingen (Provider/ServerUrl/Model - LM Studio by default)
    // ============================================================

    function loadAiSettings() {
        return fetch("/api/ai-connector")
            .then(function (r) { return r.json(); })
            .then(function (settings) {
                aiProviderSelect.value = settings.provider || "LmStudio";
                aiServerUrlInput.value = settings.serverUrl || "";
                savedModel = settings.model || "";
                aiModelSelect.innerHTML = "";
                if (savedModel) {
                    var opt = document.createElement("option");
                    opt.value = savedModel;
                    opt.textContent = savedModel;
                    aiModelSelect.appendChild(opt);
                }
            });
    }

    function refreshAiModels() {
        aiSettingsStatus.textContent = "Modellen ophalen...";
        fetch("/api/ai-models?provider=" + aiProviderSelect.value + "&serverUrl=" + encodeURIComponent(aiServerUrlInput.value))
            .then(function (r) { return r.json(); })
            .then(function (result) {
                aiModelSelect.innerHTML = "";
                (result.models || []).forEach(function (m) {
                    var opt = document.createElement("option");
                    opt.value = m;
                    opt.textContent = m;
                    aiModelSelect.appendChild(opt);
                });
                if (savedModel && result.models && result.models.indexOf(savedModel) !== -1) {
                    aiModelSelect.value = savedModel;
                }
                aiSettingsStatus.textContent = result.success ? (result.models.length + " model(len) gevonden.") : result.errorMessage;
            })
            .catch(function (err) {
                aiSettingsStatus.textContent = "Ophalen mislukt: " + err;
            });
    }

    function testAiConnection() {
        aiSettingsStatus.textContent = "Testen...";
        fetch("/api/ai-test-connection", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ provider: aiProviderSelect.value, serverUrl: aiServerUrlInput.value, model: aiModelSelect.value }),
        })
            .then(function (r) { return r.json(); })
            .then(function (result) {
                aiSettingsStatus.textContent = result.success ? ("Werkt: " + result.message) : ("Fout: " + result.message);
            })
            .catch(function (err) {
                aiSettingsStatus.textContent = "Test mislukt: " + err;
            });
    }

    function saveAiSettings() {
        savedModel = aiModelSelect.value || savedModel;
        fetch("/api/ai-connector", {
            method: "PUT",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ provider: aiProviderSelect.value, serverUrl: aiServerUrlInput.value, model: savedModel }),
        })
            .then(function (r) {
                aiSettingsStatus.textContent = r.ok ? "Opgeslagen." : "Opslaan mislukt.";
            })
            .catch(function (err) {
                aiSettingsStatus.textContent = "Opslaan mislukt: " + err;
            });
    }

    aiRefreshModelsBtn.addEventListener("click", refreshAiModels);
    aiTestBtn.addEventListener("click", testAiConnection);
    aiSettingsSaveBtn.addEventListener("click", saveAiSettings);

    refreshBtn.addEventListener("click", refreshPages);
    saveCssBtn.addEventListener("click", saveCss);

    // ============================================================
    // Splitters: drag to resize the nav/preview/right-panel columns.
    // The grid's outer columns are fixed px (nav, right panel) with the
    // preview column as 1fr between them - dragging a splitter just
    // adjusts the adjacent fixed column's px width, the 1fr column
    // absorbs the difference automatically.
    // ============================================================

    var navWidth = 280;
    var panelWidth = 380;

    function applyGridTemplate() {
        styleguideEl.style.gridTemplateColumns = navWidth + "px 6px 1fr 6px " + panelWidth + "px";
    }

    function setupSplitter(el, onDrag) {
        el.addEventListener("mousedown", function (e) {
            e.preventDefault();
            var startX = e.clientX;
            var startNav = navWidth;
            var startPanel = panelWidth;
            el.classList.add("dragging");
            document.body.classList.add("splitter-dragging");
            document.body.style.cursor = "col-resize";

            function onMove(ev) {
                onDrag(ev.clientX - startX, startNav, startPanel);
                applyGridTemplate();
            }

            function onUp() {
                el.classList.remove("dragging");
                document.body.classList.remove("splitter-dragging");
                document.body.style.cursor = "";
                document.removeEventListener("mousemove", onMove);
                document.removeEventListener("mouseup", onUp);
            }

            document.addEventListener("mousemove", onMove);
            document.addEventListener("mouseup", onUp);
        });
    }

    setupSplitter(splitter1, function (dx, startNav) {
        navWidth = Math.max(160, Math.min(500, startNav + dx));
    });

    setupSplitter(splitter2, function (dx, startNav, startPanel) {
        panelWidth = Math.max(260, Math.min(700, startPanel - dx));
    });

    // First open: capture copies right away (covers the case where none
    // exist yet) and load the CSS editor.
    refreshPages();
    loadCss();
})();
