// Funções da proposta para a fase B (área para soltar, pasta da proposta no app
// desktop, ...). Carregado depois do app.js (Components/App.razor): o que é comum
// a toda a tela continua no app.js, e o que uma pista acrescenta mora aqui, sem
// conflito com a outra. Cada função fica em window.appXxx, como no app.js, e o
// que depende do navegador (localStorage, chrome.webview) vai em try/catch.

// ---- item 20: a pasta da proposta no app desktop ----
// O app desktop (WebView2) grava a comercial, a técnica, o pricing e o custo
// previsto direto em <raiz>\PRP-2026-104 rev 00 e conta à página o que fez
// (PastaDasPropostas.cs). Aqui: saber se a tela está dentro do app, repassar
// esse retorno ao servidor e mandar os dois pedidos da página — o destino do
// próximo custo previsto e "abrir a pasta". No navegador comum não existe
// window.chrome.webview: ligar devolve false e nada muda.
window.appPasta = (function () {
    let ref = null;
    let ouvindo = false;
    const webview = () => {
        try { return (window.chrome && window.chrome.webview) || null; } catch (e) { return null; }
    };
    const mandar = (mensagem) => {
        try { const w = webview(); if (w) w.postMessage(mensagem); } catch (e) { }
    };
    return {
        // devolve se a tela está no app desktop; o ref é o da página aberta
        ligar: (dotnet) => {
            try {
                const w = webview();
                if (!w) return false;
                ref = dotnet;
                if (!ouvindo) {
                    ouvindo = true;
                    w.addEventListener('message', (e) => {
                        const d = e && e.data;
                        if (!d || typeof d !== 'object' || !ref) return;
                        if (!('erroPasta' in d) && !('salvoEm' in d)) return;
                        try { ref.invokeMethodAsync('MensagemDaPasta', JSON.stringify(d)).catch(() => { }); } catch (x) { }
                    });
                }
                return true;
            } catch (e) {
                return false;
            }
        },
        // o nome do custo previsto não tem o número: o app guarda este destino por 30 s
        destino: (numero, rev) => mandar({ pasta: numero, rev: rev }),
        abrir: (numero, rev) => mandar({ abrirPasta: numero, rev: rev }),
    };
})();
