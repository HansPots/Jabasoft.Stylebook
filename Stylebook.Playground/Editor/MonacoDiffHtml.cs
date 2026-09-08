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

            window.setDiffContent = function (originalText, modifiedText) {
                if (originalModel) { originalModel.dispose(); }
                if (modifiedModel) { modifiedModel.dispose(); }
                originalModel = monaco.editor.createModel(originalText, "xml");
                modifiedModel = monaco.editor.createModel(modifiedText, "xml");
                diffEditor.setModel({ original: originalModel, modified: modifiedModel });
                modifiedModel.onDidChangeContent(function () {
                    window.chrome.webview.postMessage(JSON.stringify({ type: "change", value: modifiedModel.getValue() }));
                });
            };

            window.clearDiffContent = function () {
                if (diffEditor) { diffEditor.setModel(null); }
            };
        </script>
        </body>
        </html>
        """;
}
