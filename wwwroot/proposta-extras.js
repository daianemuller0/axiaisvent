// Funções da proposta para a fase B (área para soltar, pasta da proposta no app
// desktop, ...). Carregado depois do app.js (Components/App.razor): o que é comum
// a toda a tela continua no app.js, e o que uma pista acrescenta mora aqui, sem
// conflito com a outra. Cada função fica em window.appXxx, como no app.js, e o
// que depende do navegador (localStorage, chrome.webview) vai em try/catch.

// item 08 (pista da Técnica): a curva em tamanho inteiro, num <dialog> modal
// da própria página. O clique na miniatura ([data-curva-inteira]) põe a imagem
// e abre; Esc fecha (é do próprio <dialog>), e também o X e o clique no fundo.
// Fica no navegador: abrir e fechar a imagem não precisa ir ao servidor. No
// WebView2 uma aba nova viraria uma janela solta, sem menu.
(function () {
    document.addEventListener('click', (e) => {
        const alvo = e.target instanceof Element ? e.target : null;
        if (!alvo) return;

        const fechar = alvo.closest('[data-fechar-dialogo]');
        if (fechar) { fechar.closest('dialog')?.close(); return; }

        // o clique no fundo cai no próprio <dialog> (o conteúdo ocupa o resto)
        if (alvo instanceof HTMLDialogElement && alvo.id === 'dialogo-curva') { alvo.close(); return; }

        const botao = alvo.closest('[data-curva-inteira]');
        if (!botao) return;
        const dialogo = document.getElementById('dialogo-curva');
        if (!dialogo || typeof dialogo.showModal !== 'function') return;
        const nome = botao.getAttribute('data-nome') || '';
        const img = dialogo.querySelector('img');
        img.src = botao.getAttribute('data-curva-inteira');
        img.alt = 'Imagem ampliada: ' + (nome || 'curva de performance');
        const titulo = dialogo.querySelector('.curva-dialogo-nome');
        if (titulo) titulo.textContent = nome;
        try { dialogo.showModal(); } catch (_) { /* já aberto */ }
    });
})();

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
