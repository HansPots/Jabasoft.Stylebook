namespace Stylebook.Playground.Editor;

/// <summary>
/// The HTML page hosted in MonacoDiffView (WebView2) - Monaco's own diff
/// editor (createDiffEditor), loaded from a CDN rather than bundled
/// locally (see the "05-Code-Editor.md" note from the previous project:
/// same choice, simpler to build, this machine has internet access
/// anyway). XAML has no dedicated Monaco language, so 'xml' is used -
/// close enough for real highlighting without writing a custom tokenizer.
/// Talks to MainWindow.xaml.cs over WebView2's postMessage bridge: a
/// {"type":"ready"} message once Monaco has actually loaded (CDN script
/// loading is async, so C# must wait for this before calling
/// setDiffContent), and a {"type":"change","value":"..."} message every
/// time the MODIFIED (right-hand, editable) side changes - the ORIGINAL
/// (left-hand) side is never editable, same "fixed reference point"
/// design as the TextBox pair this replaces.
/// </summary>
public static class MonacoDiffHtml
{
    public const string Content = """
        <!doctype html>
        <html>
        <head>
        <meta charset="utf-8" />
        <style>
            html, body, #container { margin: 0; padding: 0; height: 100%; width: 100%; overflow: hidden; background: #1e1e1e; }
        </style>
        </head>
        <body>
        <div id="container"></div>
        <script src="https://cdn.jsdelivr.net/npm/monaco-editor@0.50.0/min/vs/loader.js"></script>
        <script>
            require.config({ paths: { vs: "https://cdn.jsdelivr.net/npm/monaco-editor@0.50.0/min/vs" } });

            var diffEditor = null;
            var originalModel = null;
            var modifiedModel = null;

            require(["vs/editor/editor.main"], function () {
                diffEditor = monaco.editor.createDiffEditor(document.getElementById("container"), {
                    automaticLayout: true,
                    theme: "vs-dark",
                    renderSideBySide: true,
                    originalEditable: false
                });
                window.chrome.webview.postMessage(JSON.stringify({ type: "ready" }));
            });

            var changeDebounceTimer = null;

            window.setDiffContent = function (originalText, modifiedText) {
                if (originalModel) { originalModel.dispose(); }
                if (modifiedModel) { modifiedModel.dispose(); }
                originalModel = monaco.editor.createModel(originalText, "xml");
                modifiedModel = monaco.editor.createModel(modifiedText, "xml");
                diffEditor.setModel({ original: originalModel, modified: modifiedModel });
                modifiedModel.onDidChangeContent(function () {
                    // Eén paste-over-selectie (of gewoon vlot typen) kan
                    // meerdere keren achter elkaar vuren - zonder debounce
                    // stuurt elke tussenstap een eigen bericht, en elke
                    // tussenstap kan ook nog eens ongeldige XAML zijn.
                    // Alleen het EINDRESULTAAT (150ms rust) doorsturen.
                    if (changeDebounceTimer) { clearTimeout(changeDebounceTimer); }
                    changeDebounceTimer = setTimeout(function () {
                        window.chrome.webview.postMessage(JSON.stringify({ type: "change", value: modifiedModel.getValue() }));
                    }, 150);
                });
            };

            window.clearDiffContent = function () {
                if (diffEditor) { diffEditor.setModel(null); }
            };

            // WPF staat maar EEN markup-extension per attribuutwaarde toe -
            // een Margin/Padding/CornerRadius met 2 of 4 komma-waarden kan
            // dus nooit een los token krijgen op de plek van 1 van die
            // waarden zonder de hele XAML ongeldig te maken. Blind invoegen/
            // vervangen (zoals hieronder, de FALLBACK) produceert dan kapotte
            // XAML. THICKNESS_ATTRS/CORNERRADIUS_SIDES + de twee helpers
            // hieronder herkennen dat geval en herschrijven het HELE
            // attribuut naar de losse Parts-vorm (theming:MarginParts.Left=
            // "...", enz. - zie Stylebook.Components.Theming) in plaats van
            // te weigeren of iets kapots te produceren. Werkt op zowel de
            // 2-waarden (horizontaal,verticaal) als 4-waarden vorm.
            var THICKNESS_SIDES = ["Left", "Top", "Right", "Bottom"];
            var CORNERRADIUS_SIDES = ["TopLeft", "TopRight", "BottomRight", "BottomLeft"];
            var THEMING_XMLNS = 'xmlns:theming="clr-namespace:Stylebook.Components.Theming;assembly=Stylebook.Components"';

            // Zoekt op de regel van de (start van de) selectie een
            // Margin/Padding/CornerRadius="..."-attribuut waar de selectie
            // binnen de waarde valt. Retourneert null als er niets past
            // (andere regel-indeling dan verwacht, attribuut niet 2 of 4
            // waarden, ...) - dan valt replaceSelectionWithToken terug op
            // de simpele vervanging.
            function findEnclosingThicknessAttribute(model, selection) {
                if (selection.startLineNumber !== selection.endLineNumber) { return null; }
                var lineNumber = selection.startLineNumber;
                var lineContent = model.getLineContent(lineNumber);
                var regex = /(Margin|Padding|CornerRadius)="([^"]*)"/g;
                var match;
                while ((match = regex.exec(lineContent)) !== null) {
                    var valueStartCol = match.index + match[1].length + 2 + 1; // na `Naam="`, 1-based kolom
                    var valueEndCol = valueStartCol + match[2].length;
                    if (selection.startColumn < valueStartCol || selection.endColumn > valueEndCol) { continue; }

                    var parts = match[2].split(",");
                    if (parts.length !== 2 && parts.length !== 4) { continue; }

                    return {
                        lineNumber: lineNumber,
                        matchStartCol: match.index + 1,
                        matchEndCol: match.index + 1 + match[0].length,
                        attrName: match[1],
                        parts: parts,
                        cursorOffsetInValue: selection.startColumn - valueStartCol,
                    };
                }
                return null;
            }

            // Zorgt dat xmlns:theming ergens in het document staat (nodig
            // voor elk theming:XParts-attribuut) - zoekt het root-element
            // (de eerste öpenende tag) en voegt de declaratie daar toe als
            // 'ie nog ontbreekt. Retourneert een edit-object of null als
            // niets hoeft te gebeuren.
            function buildThemingNamespaceEdit(model) {
                var fullText = model.getValue();
                if (fullText.indexOf("Stylebook.Components.Theming") !== -1) { return null; }
                var rootMatch = /<([A-Za-z_][\w.]*)/.exec(fullText);
                if (!rootMatch) { return null; }
                var insertOffset = rootMatch.index + rootMatch[0].length;
                var pos = model.getPositionAt(insertOffset);
                return {
                    range: new monaco.Range(pos.lineNumber, pos.column, pos.lineNumber, pos.column),
                    text: "\n        " + THEMING_XMLNS,
                    forceMoveMarkers: true,
                };
            }

            // Vervangt de huidige selectie in het VOORSTEL (rechterkant)
            // door een {DynamicResource TokenNaam}-referentie - of voegt 'm
            // in op de cursorpositie als er niets geselecteerd is (een
            // "selectie" van lengte 0 is in Monaco gewoon een geldige range).
            // Zit de selectie binnen een Margin/Padding/CornerRadius met
            // meerdere waarden, dan wordt in plaats daarvan het HELE
            // attribuut herschreven naar de Parts-vorm (zie hierboven) -
            // welke van de N waarden precies geselecteerd was bepaalt alleen
            // WELKE zijde het token krijgt, dus een iets onnauwkeurige
            // selectie (ergens in dat ene getal) is hier geen probleem meer.
            // executeEdits triggert modifiedModel's eigen onDidChangeContent
            // hierboven vanzelf, dus dit synchroniseert automatisch terug
            // naar _proposedXaml zoals elke andere wijziging.
            window.replaceSelectionWithToken = function (tokenName) {
                if (!diffEditor) { return; }
                var modifiedEditor = diffEditor.getModifiedEditor();
                var model = modifiedEditor.getModel();
                var selection = modifiedEditor.getSelection();

                var enclosing = findEnclosingThicknessAttribute(model, selection);
                if (enclosing) {
                    var offsets = [];
                    var pos = 0;
                    for (var i = 0; i < enclosing.parts.length; i++) {
                        offsets.push({ start: pos, end: pos + enclosing.parts[i].length });
                        pos += enclosing.parts[i].length + 1;
                    }
                    var partIndex = offsets.length - 1;
                    for (var j = 0; j < offsets.length; j++) {
                        if (enclosing.cursorOffsetInValue >= offsets[j].start && enclosing.cursorOffsetInValue <= offsets[j].end) {
                            partIndex = j;
                            break;
                        }
                    }

                    var isCornerRadius = enclosing.attrName === "CornerRadius";
                    var partsClass = enclosing.attrName + "Parts";
                    var sideNames = isCornerRadius ? CORNERRADIUS_SIDES : THICKNESS_SIDES;
                    // 2-waarden vorm (horizontaal,verticaal) geldt voor 2 zijden tegelijk.
                    var affectedSides = enclosing.parts.length === 2
                        ? (partIndex === 0 ? [0, 2] : [1, 3])
                        : [partIndex];

                    // Was de vervangen waarde negatief (bv. de "-18" in een
                    // naar-buiten-getrokken Margin)? Een token levert altijd
                    // een positieve grootte op - dus zet in dat geval ook de
                    // bijbehorende *Negative-vlag mee (alleen zinvol voor
                    // Margin/Padding, CornerRadius kent geen negatieve
                    // waarde), zie MarginParts/PaddingParts.
                    var wasNegative = !isCornerRadius && /^\s*-/.test(enclosing.parts[partIndex]);

                    var finalParts = enclosing.parts.length === 2
                        ? [enclosing.parts[0], enclosing.parts[1], enclosing.parts[0], enclosing.parts[1]]
                        : enclosing.parts.slice();
                    affectedSides.forEach(function (sideIndex) {
                        finalParts[sideIndex] = "{DynamicResource " + tokenName + "}";
                    });

                    var lineContent = model.getLineContent(enclosing.lineNumber);
                    var indentMatch = /^\s*/.exec(lineContent);
                    var indent = indentMatch ? indentMatch[0] : "";
                    var fragments = [];
                    for (var k = 0; k < 4; k++) {
                        var raw = finalParts[k].trim();
                        if (raw === "0") { continue; } // weggelaten zijde = 0, zie CornerRadiusParts/MarginParts/PaddingParts
                        fragments.push("theming:" + partsClass + "." + sideNames[k] + '="' + raw + '"');
                        if (wasNegative && affectedSides.indexOf(k) !== -1) {
                            fragments.push("theming:" + partsClass + "." + sideNames[k] + 'Negative="True"');
                        }
                    }
                    var replacementText = fragments.length > 0 ? fragments.join("\n" + indent) : "";

                    var edits = [{
                        range: new monaco.Range(enclosing.lineNumber, enclosing.matchStartCol, enclosing.lineNumber, enclosing.matchEndCol),
                        text: replacementText,
                        forceMoveMarkers: true,
                    }];
                    var nsEdit = buildThemingNamespaceEdit(model);
                    if (nsEdit) { edits.push(nsEdit); }

                    modifiedEditor.executeEdits("replace-with-token-parts", edits);
                    modifiedEditor.focus();
                    return;
                }

                modifiedEditor.executeEdits("replace-with-token", [
                    { range: selection, text: "{DynamicResource " + tokenName + "}", forceMoveMarkers: true }
                ]);
                modifiedEditor.focus();
            };
        </script>
        </body>
        </html>
        """;
}
