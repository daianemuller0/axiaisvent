// Utilitários chamados pelo Blazor via JS interop.
window.appPrint = () => window.print();

window.appDownload = (fileName, mime, base64) => {
    const a = document.createElement('a');
    a.href = `data:${mime};base64,${base64}`;
    a.download = fileName;
    document.body.appendChild(a);
    a.click();
    a.remove();
};

// Rascunho da proposta guardado no navegador: o trabalho em andamento sobrevive
// a um F5 ou a abrir /axiais/pricing direto na barra de endereços.
const RASCUNHO = 'howden-axiais-rascunho';

window.appSaveDraft = (json) => {
    try { localStorage.setItem(RASCUNHO, json); } catch { /* modo privativo: ignora */ }
};

window.appLoadDraft = () => {
    try { return localStorage.getItem(RASCUNHO) || ''; } catch { return ''; }
};

window.appClearDraft = () => {
    try { localStorage.removeItem(RASCUNHO); } catch { /* ignora */ }
};

// Sincronização entre guias do navegador: o evento 'storage' dispara nas OUTRAS
// guias quando esta grava o rascunho — a página avisada recarrega o estado.
let draftRef = null;
window.appWatchDraft = (ref) => { draftRef = ref; };
window.addEventListener('storage', (e) => {
    if (e.key === RASCUNHO && e.newValue && draftRef) {
        draftRef.invokeMethodAsync('DraftAtualizado', e.newValue);
    }
});
