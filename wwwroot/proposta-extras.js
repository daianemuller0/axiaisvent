// Funções da proposta para a fase B (área para soltar, pasta da proposta no app
// desktop, ...). Carregado depois do app.js (Components/App.razor): o que é comum
// a toda a tela continua no app.js, e o que uma pista acrescenta mora aqui, sem
// conflito com a outra. Cada função fica em window.appXxx, como no app.js, e o
// que depende do navegador (localStorage, chrome.webview) vai em try/catch.
