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

// A altura das faixas fixas no alto da proposta (o topo do app, a barra de
// etapas e, na etapa Equipamentos, a faixa do equipamento), em px: a soma do
// --faixas-fixas do app.css, que já desconta a faixa quando ela não está na
// tela. O valor da variável é um calc() de somas em px, e é assim que ele é lido.
window.appFaixasFixas = () => {
    try {
        const proposta = document.querySelector('.proposta');
        const valor = proposta ? getComputedStyle(proposta).getPropertyValue('--faixas-fixas') : '';
        const somas = (valor.match(/-?\d+(\.\d+)?(?=px)/g) || []).map(Number);
        if (somas.length) return somas.reduce((a, b) => a + b, 0);
    } catch (e) { }
    return 140;
};

// Rola até um elemento, sem pôr foco em nada. Só rola quando o começo dele
// está fora da vista: acima das faixas fixas (trocou de equipamento com a tela
// rolada lá embaixo) ou na parte de baixo da tela (o custo previsto, no fim da
// etapa Documentos, que abre um formulário logo abaixo). Com o começo à vista
// no alto, nada se mexe — trocar de aba no alto da tela não dá um pulo.
window.appRolarPara = (id) => {
    const el = document.getElementById(id);
    if (!el) return;
    const topo = el.getBoundingClientRect().top;
    if (topo >= window.appFaixasFixas() && topo < window.innerHeight * 0.6) return;
    // o scroll-margin-top do app.css deixa o começo logo abaixo das faixas
    el.scrollIntoView({ behavior: 'smooth', block: 'start' });
};

// Com muitos equipamentos os chips da faixa rolam na horizontal: o do
// equipamento aberto não pode ficar escondido fora da faixa.
window.appChipAtivo = () => {
    const area = document.querySelector('.equip-chips');
    const chip = area ? area.querySelector('.equip-chip.ativo') : null;
    if (!chip) return;
    const a = area.getBoundingClientRect(), c = chip.getBoundingClientRect();
    // a folga de 36 px passa da borda esmaecida (32 px, app.css)
    if (c.left < a.left + 32) area.scrollLeft -= a.left - c.left + 36;
    else if (c.right > a.right - 32) area.scrollLeft += c.right - a.right + 36;
};

// Os chips da faixa do equipamento, quando não cabem: as classes mais-esq e
// mais-dir esmaecem a borda do lado em que há mais chips (app.css), e a roda
// do mouse rola a faixa na horizontal — sem isso, só o Shift+roda chegava lá.
// A faixa nasce e some com a etapa 3: o MutationObserver acha a nova.
(function () {
    let pedido = 0;
    const marcar = () => {
        pedido = 0;
        const area = document.querySelector('.equip-chips');
        if (!area) return;
        if (!area._ligada) {
            area._ligada = true;
            area.addEventListener('scroll', marcar, { passive: true });
            area.addEventListener('wheel', (e) => {
                if (area.scrollWidth <= area.clientWidth || Math.abs(e.deltaX) >= Math.abs(e.deltaY)) return;
                e.preventDefault();
                area.scrollLeft += e.deltaY;
            }, { passive: false });
        }
        const resto = area.scrollWidth - area.clientWidth - area.scrollLeft;
        area.classList.toggle('mais-dir', resto > 1);
        area.classList.toggle('mais-esq', area.scrollLeft > 1);
    };
    const agendar = () => { if (!pedido) pedido = requestAnimationFrame(marcar); };
    new MutationObserver(agendar).observe(document.documentElement, { childList: true, subtree: true });
    window.addEventListener('resize', agendar);
})();

// Leva até o campo de uma pendência: o campo no meio da tela, o foco no
// primeiro input, select ou textarea dele (sem rolar de novo) e um anel âmbar
// por 1,8 s, para o olho achar. Devolve falso quando o campo não está
// desenhado (ex.: o motor com o "Com" desligado) — aí a tela fica no topo.
window.appIrAoCampo = (id) => {
    const el = document.getElementById(id);
    if (!el) return false;
    el.scrollIntoView({ block: 'center' });
    const campo = el.matches('input, select, textarea') ? el : el.querySelector('input, select, textarea');
    if (campo && typeof campo.focus === 'function') campo.focus({ preventScroll: true });
    // o mesmo campo pedido duas vezes seguidas: a animação recomeça
    el.classList.remove('realce');
    void el.offsetWidth;
    el.classList.add('realce');
    clearTimeout(el._realce);
    el._realce = setTimeout(() => el.classList.remove('realce'), 1800);
    return true;
};

// Arquivo solto fora de uma área de soltar (.dropzone) não faz nada. Sem isto,
// no WebView2 o navegador abre o arquivo no lugar da tela, e o circuito do
// Blazor cai no meio da proposta. Dentro da área, o InputFile que fica por
// cima dela recebe o arquivo do jeito normal (o mesmo OnChange do clique).
// Texto arrastado para um campo (ou dentro de uma observação) passa: só
// arquivo e link soltos fora da área são segurados.
(function () {
    const naArea = (e) => e.target instanceof Element && e.target.closest('.dropzone');
    const temArquivo = (e) => !!e.dataTransfer && Array.from(e.dataTransfer.types || []).includes('Files');
    const editavel = (e) => e.target instanceof Element && e.target.closest('input, textarea, [contenteditable]');
    const segurar = (e) => {
        if (naArea(e) || (!temArquivo(e) && editavel(e))) return;
        e.preventDefault();
        if (e.dataTransfer) e.dataTransfer.dropEffect = 'none';
    };
    document.addEventListener('dragover', segurar);
    document.addEventListener('drop', segurar);

    // o realce da área enquanto o arquivo passa por cima: a contagem de
    // entradas e saídas segue os filhos dela (o InputFile, o ícone, o texto)
    const limpar = () => document.querySelectorAll('.dropzone.arrastando').forEach((z) => {
        z.classList.remove('arrastando');
        z._entradas = 0;
    });
    document.addEventListener('dragenter', (e) => {
        const zona = naArea(e);
        if (!zona) return;
        zona._entradas = (zona._entradas || 0) + 1;
        zona.classList.add('arrastando');
    });
    document.addEventListener('dragleave', (e) => {
        const zona = naArea(e);
        if (!zona) return;
        zona._entradas = Math.max(0, (zona._entradas || 1) - 1);
        if (zona._entradas === 0) zona.classList.remove('arrastando');
    });
    document.addEventListener('drop', limpar, true);
    document.addEventListener('dragend', limpar, true);
})();

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
