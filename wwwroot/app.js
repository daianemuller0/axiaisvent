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
