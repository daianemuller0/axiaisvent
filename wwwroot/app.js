// Utilitários chamados pelo Blazor via JS interop.
window.appPrint = () => window.print();

// Download de arquivo gerado no servidor (exportação da Base em Excel/CSV).
window.appDownload = (fileName, mime, base64) => {
    const a = document.createElement('a');
    a.href = `data:${mime};base64,${base64}`;
    a.download = fileName;
    document.body.appendChild(a);
    a.click();
    a.remove();
};

// Inicia o Blazor com a reconexão paciente. O padrão desiste depois de 8
// tentativas (cerca de 20 s) e manda recarregar a página — perdendo o que
// estava preenchido. Aqui ele continua tentando enquanto a aba estiver aberta:
// 1 s no começo, depois de 5 em 5 s. O servidor guarda a sessão por horas (ver
// BackendHost), então voltar à tela é só esperar a reconexão.
(function () {
    function iniciar() {
        if (!window.Blazor) { setTimeout(iniciar, 50); return; }

        window.Blazor.start({
            circuit: {
                reconnectionOptions: {
                    maxRetries: 10000,
                    retryIntervalMilliseconds: function (tentativas) {
                        return tentativas < 5 ? 1000 : 5000;
                    },
                },
            },
        });
    }
    iniciar();
})();

// Rola a página até um elemento (atalhos no alto de telas compridas).
window.appRolarPara = (id) => {
    const el = document.getElementById(id);
    if (el) el.scrollIntoView({ behavior: 'smooth', block: 'start' });
};

// Rascunho da proposta no navegador: o que a pessoa preenche é guardado aqui a
// cada alteração, para sobreviver a recarregar a página (o estado do servidor
// se perde nesse caso). Tudo em try/catch: navegação privada ou site bloqueado
// só significa "sem rascunho", nunca erro na tela.
window.appRascunho = {
    ler: (chave) => { try { return localStorage.getItem('axiais.rascunho.' + chave); } catch (e) { return null; } },
    gravar: (chave, valor) => { try { localStorage.setItem('axiais.rascunho.' + chave, valor); } catch (e) { } },
    limpar: (chave) => { try { localStorage.removeItem('axiais.rascunho.' + chave); } catch (e) { } },
};

// Atalhos da proposta: Ctrl+S salva de qualquer etapa. O campo que está sendo
// editado perde o foco antes, para o valor dele chegar ao servidor primeiro —
// sem isso o Ctrl+S gravaria a proposta sem o último campo digitado.
window.appAtalhos = {
    ligar: (dotnet) => {
        window.appAtalhos.desligar();
        const aoTeclar = (e) => {
            if ((e.ctrlKey || e.metaKey) && !e.altKey && (e.key === 's' || e.key === 'S')) {
                e.preventDefault();
                const ativo = document.activeElement;
                if (ativo && typeof ativo.blur === 'function') ativo.blur();
                setTimeout(() => dotnet.invokeMethodAsync('SalvarPeloTeclado'), 60);
            }
        };
        window.appAtalhos._aoTeclar = aoTeclar;
        document.addEventListener('keydown', aoTeclar);
    },
    desligar: () => {
        if (window.appAtalhos._aoTeclar) {
            document.removeEventListener('keydown', window.appAtalhos._aoTeclar);
            window.appAtalhos._aoTeclar = null;
        }
    },
};

// Guarda da proposta: um link do menu do app não sai direto da tela. O clique
// é segurado e repassado ao servidor, que sabe se há alteração não gravada:
// sem nada a perder, ele mesmo navega; com alteração, a tela mostra a barra
// Salvar / Descartar / Cancelar. A navegação dos links é do navegador (o menu
// não é interativo), por isso a guarda do servidor sozinha não a veria. Sem
// conexão com o servidor, o link segue normal — o rascunho do navegador ainda
// guarda o que foi digitado.
window.appGuarda = {
    ligar: (dotnet) => {
        window.appGuarda.desligar();
        const aoClicar = (e) => {
            if (e.defaultPrevented || e.button !== 0 || e.ctrlKey || e.metaKey || e.shiftKey || e.altKey) return;
            // a tela da proposta saiu da página sem desligar: não segura mais nada
            if (!document.querySelector('.prop-etapas')) { window.appGuarda.desligar(); return; }
            const a = e.target instanceof Element ? e.target.closest('a[href]') : null;
            if (!a || a.hasAttribute('download') || (a.getAttribute('target') || '_self') !== '_self') return;
            const destino = new URL(a.href, document.baseURI);
            if (destino.origin !== location.origin) return;
            // o link da própria proposta não troca de tela: nada se perde
            if (destino.pathname.replace(/\/+$/, '').toLowerCase() === '/axiais/proposta') return;
            e.preventDefault();
            dotnet.invokeMethodAsync('PedirParaSair', destino.href)
                .catch(() => { location.href = destino.href; });
        };
        window.appGuarda._aoClicar = aoClicar;
        document.addEventListener('click', aoClicar, true);
    },
    desligar: () => {
        if (window.appGuarda._aoClicar) {
            document.removeEventListener('click', window.appGuarda._aoClicar, true);
            window.appGuarda._aoClicar = null;
        }
    },
};

// Troca de etapa: a etapa nova começa do alto.
window.appTopo = () => window.scrollTo(0, 0);

// Preferências pequenas de cada pessoa (ex.: mostrar ou esconder a ajuda).
window.appPreferencia = {
    ler: (chave) => { try { return localStorage.getItem('axiais.pref.' + chave); } catch (e) { return null; } },
    gravar: (chave, valor) => { try { localStorage.setItem('axiais.pref.' + chave, valor); } catch (e) { } },
};

// Menu do app recolhido (só os ícones). A escolha da pessoa fica guardada; sem
// escolha, o menu só recolhe abaixo de 1400 px — o notebook de 1366, onde os
// 256 px dele apertavam a proposta. A 1440 nunca recolhe sozinho. O menu não é
// interativo (é HTML do servidor): o botão chama appMenu.alternar() direto, e
// a navegação aprimorada do Blazor, que refaz o <body>, devolve a classe pelo
// evento enhancedload.
window.appMenu = {
    recolhido: () => {
        let escolha = null;
        try { escolha = window.appPreferencia.ler('menuRecolhido'); } catch (e) { }
        if (escolha === 'sim') return true;
        if (escolha === 'nao') return false;
        return window.innerWidth < 1400;
    },
    aplicar: () => {
        try {
            const recolhido = window.appMenu.recolhido();
            document.body.classList.toggle('menu-recolhido', recolhido);
            document.querySelectorAll('.menu-alternar').forEach((b) => {
                const texto = recolhido ? 'Expandir menu' : 'Recolher menu';
                b.title = texto;
                b.setAttribute('aria-label', texto);
                b.setAttribute('aria-expanded', recolhido ? 'false' : 'true');
            });
        } catch (e) { }
    },
    alternar: () => {
        try {
            const recolher = !document.body.classList.contains('menu-recolhido');
            window.appPreferencia.gravar('menuRecolhido', recolher ? 'sim' : 'nao');
            window.appMenu.aplicar();
        } catch (e) { }
    },
};
window.appMenu.aplicar();
window.addEventListener('resize', () => window.appMenu.aplicar());
(function ligarAoBlazor() {
    if (!window.Blazor || !window.Blazor.addEventListener) { setTimeout(ligarAoBlazor, 100); return; }
    try { window.Blazor.addEventListener('enhancedload', () => window.appMenu.aplicar()); } catch (e) { }
})();

// Teclado dos menus que abrem de um botão (Mais, a seta do Gerar tudo): o foco
// entra no primeiro item, as setas andam entre os itens, Esc fecha e devolve o
// foco ao botão, Tab e o clique fora fecham. Quem abre e fecha é o servidor
// (FecharMenus); aqui é só o foco e os avisos de "fechar".
window.appMenuPop = {
    abrir: (id, dotnet) => {
        window.appMenuPop.soltar();
        const menu = document.getElementById(id);
        if (!menu) return;
        const dono = menu.closest('.menu-dono');
        const botao = dono ? dono.querySelector('[aria-haspopup]') : null;
        const itens = () => [...menu.querySelectorAll('[role=menuitem]:not([disabled])')];
        const fechar = (devolverFoco) => {
            window.appMenuPop.soltar();
            dotnet.invokeMethodAsync('FecharMenus')
                .then(() => { if (devolverFoco && botao) botao.focus({ preventScroll: true }); })
                .catch(() => { });
        };
        const fora = (e) => {
            if (!document.body.contains(menu)) { window.appMenuPop.soltar(); return; }
            if (dono && dono.contains(e.target)) return;
            fechar(false);
        };
        const tecla = (e) => {
            const lista = itens();
            const i = lista.indexOf(document.activeElement);
            if (e.key === 'ArrowDown') { e.preventDefault(); lista[(i + 1) % lista.length]?.focus(); }
            else if (e.key === 'ArrowUp') { e.preventDefault(); lista[(i - 1 + lista.length) % lista.length]?.focus(); }
            else if (e.key === 'Home') { e.preventDefault(); lista[0]?.focus(); }
            else if (e.key === 'End') { e.preventDefault(); lista[lista.length - 1]?.focus(); }
            else if (e.key === 'Escape') { e.preventDefault(); fechar(true); }
            else if (e.key === 'Tab') { fechar(false); }
        };
        document.addEventListener('pointerdown', fora, true);
        menu.addEventListener('keydown', tecla);
        window.appMenuPop._atual = { fora, tecla, menu };
        const primeiro = itens()[0];
        if (primeiro) primeiro.focus({ preventScroll: true });
    },
    soltar: () => {
        const atual = window.appMenuPop._atual;
        if (!atual) return;
        document.removeEventListener('pointerdown', atual.fora, true);
        atual.menu.removeEventListener('keydown', atual.tecla);
        window.appMenuPop._atual = null;
    },
    // depois de escolher um item, o foco volta ao botão que abriu o menu
    focar: (id) => {
        window.appMenuPop.soltar();
        const el = document.getElementById(id);
        if (el) el.focus({ preventScroll: true });
    },
};
