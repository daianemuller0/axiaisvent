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
