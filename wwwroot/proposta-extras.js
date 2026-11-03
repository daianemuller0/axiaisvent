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
