using HowdenAxiais.Poc.Data;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;

namespace HowdenAxiais.Poc.Components;

/// <summary>
/// Base das três telas que trabalham na mesma proposta (Custo → Pricing → Proposta).
///
/// O rascunho vive no <see cref="Rascunho"/>, que é um serviço "scoped" — ou seja,
/// dura enquanto o circuito do Blazor estiver de pé. Como um F5 (ou abrir a URL
/// direto) começa um circuito novo, cada render também grava o rascunho no
/// localStorage do navegador e o restaura na primeira renderização.
/// </summary>
public abstract class PaginaProposta : ComponentBase, IDisposable
{
    private DotNetObjectReference<PaginaProposta>? _jsRef;

    [Inject] protected Rascunho R { get; set; } = default!;
    [Inject] protected IJSRuntime JS { get; set; } = default!;
    [Inject] protected ParametroRepository Parametros { get; set; } = default!;
    [Inject] protected ConfigRepository Config { get; set; } = default!;
    [CascadingParameter] protected Task<AuthenticationState>? AuthState { get; set; }

    protected string Usuario { get; private set; } = "";
    /// <summary>Padrões e modelos de e-mail configurados (tela E-mails e Padrões).</summary>
    protected Dictionary<string, string> Cfg { get; private set; } = new();
    private string _ultimoSalvo = "";

    protected override async Task OnInitializedAsync()
    {
        if (AuthState is not null)
        {
            var auth = await AuthState;
            Usuario = auth.User.Identity?.Name ?? "";
        }
        Cfg = Config.All();
    }

    protected override async Task OnAfterRenderAsync(bool primeiraVez)
    {
        if (primeiraVez)
        {
            var json = await JS.InvokeAsync<string>("appLoadDraft");
            if (R.Vazio) R.FromJson(json);
            if (R.Vazio) R.Novo(Parametros.All(), Usuario, Cfg);
            _ultimoSalvo = R.ToJson();

            // Escuta alterações feitas em OUTRAS guias do navegador.
            _jsRef = DotNetObjectReference.Create(this);
            await JS.InvokeVoidAsync("appWatchDraft", _jsRef);

            StateHasChanged();
            return;
        }

        var atual = R.ToJson();
        if (atual == _ultimoSalvo) return;
        _ultimoSalvo = atual;
        await JS.InvokeVoidAsync("appSaveDraft", atual);
    }

    /// <summary>Começa uma proposta do zero, limpando também o rascunho do navegador.</summary>
    protected async Task NovaProposta()
    {
        R.Novo(Parametros.All(), Usuario, Cfg);
        _ultimoSalvo = R.ToJson();
        await JS.InvokeVoidAsync("appSaveDraft", _ultimoSalvo);
    }

    /// <summary>
    /// Troca de BU emissora. Chile (HCHL) e Peru (HPU): proposta em espanhol,
    /// moeda local, SEM impostos (só valor líquido) e a tabela de custos do
    /// país; BUs do Brasil restauram PIS/COFINS/ISS e a tabela brasileira.
    /// As quantidades lançadas são preservadas.
    /// </summary>
    protected void AplicarBu(string bu)
    {
        var paisAntes = Data.Axiais.PaisDaBu(R.Proposta.Bu);
        R.Proposta.Bu = bu;
        var pais = Data.Axiais.PaisDaBu(bu);

        if (pais == "Brasil")
        {
            R.Params.PisPct = "1.65";
            R.Params.CofinsPct = "7.6";
            R.Params.IssPct = Data.Axiais.IssPadrao(bu);
            if (paisAntes != "Brasil") { R.Proposta.Moeda = "BRL"; R.Proposta.Idioma = "Português"; }
        }
        else
        {
            R.Params.PisPct = "0"; R.Params.CofinsPct = "0"; R.Params.IssPct = "0";
            R.Proposta.Idioma = "Español";
            R.Proposta.Moeda = Data.Axiais.MoedaPadrao(bu);
        }

        if (pais != paisAntes) R.TrocarTabela(Parametros.All(), pais);
    }

    /// <summary>Grava o rascunho agora.</summary>
    protected async Task SalvarRascunho()
    {
        _ultimoSalvo = R.ToJson();
        await JS.InvokeVoidAsync("appSaveDraft", _ultimoSalvo);
    }

    /// <summary>
    /// Soma das diárias lançadas (DIARIAS NORMAIS 1º/2º turno + DIARIAS EXTRAS)
    /// preenche sozinha a quantidade das despesas por dia: locação de carro,
    /// combustível e refeições = dias; HOSPEDAGEM = dias − 1 (são as noites —
    /// sempre uma a menos que a alimentação). Continua editável depois.
    /// </summary>
    protected void SincronizarDiarias()
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        var dias = R.ItensMO
            .Where(i => (i.Servico ?? "").TrimStart().ToUpperInvariant().StartsWith("DIARIAS"))
            .Sum(i => Data.Pricing.Num(i.QtdDiaria));
        var texto = dias.ToString("0.##", inv);
        var noites = Math.Max(dias - 1, 0).ToString("0.##", inv);
        foreach (var d in R.ItensDespesa)
            if (Data.Pricing.EhDespesaDiaria(d.Despesa))
                d.Qtd = (d.Despesa ?? "").ToUpperInvariant().Contains("HOSPEDAGEM") ? noites : texto;
    }

    /// <summary>
    /// Proposta aberta do banco que teve o NÚMERO (CRM) ou a REVISÃO alterados
    /// vira cadastro NOVO ao gravar: ganha id e data próprios, e o registro
    /// antigo fica intacto no banco. Retorna true quando isso aconteceu.
    /// </summary>
    protected bool NovoCadastroSeMudouNumeroOuRevisao(List<Models.Proposta> gravadas)
    {
        var p = R.Proposta;
        if (string.IsNullOrWhiteSpace(p.Id)) return false;
        var antiga = gravadas.FirstOrDefault(x => x.Id == p.Id);
        if (antiga is null) return false;
        if (antiga.Numero == p.Numero && antiga.Revisao == p.Revisao) return false;

        p.Id = Data.Axiais.NovoIdProposta();
        p.CriadaEm = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        return true;
    }

    // ================= tic das notas =================

    /// <summary>Mensagem do botão "salvar seleção como padrão".</summary>
    protected string MsgNotas { get; private set; } = "";

    /// <summary>Liga/desliga o tic de uma nota (desligada = não sai no documento).</summary>
    protected void AlternarNota(string chave, bool marcada)
    {
        var fora = Data.Axiais.NotasDesmarcadas(R.Proposta);
        if (marcada) fora.Remove(chave); else fora.Add(chave);
        R.Proposta.NotasDesmarcadas = string.Join(";", fora.OrderBy(x => x));
        MsgNotas = "";
    }

    /// <summary>
    /// Grava a seleção atual dos tics como padrão: toda proposta nova já nasce
    /// com as mesmas notas desmarcadas.
    /// </summary>
    protected void SalvarNotasPadrao()
    {
        Config.Set("notas.padrao.desmarcadas", R.Proposta.NotasDesmarcadas);
        if (string.IsNullOrWhiteSpace(R.Proposta.NotasDesmarcadas))
            Cfg.Remove("notas.padrao.desmarcadas");
        else
            Cfg["notas.padrao.desmarcadas"] = R.Proposta.NotasDesmarcadas;
        MsgNotas = "Padrão salvo ✓ — propostas novas já nascem com esta seleção.";
    }

    // ================= ferramentas de preço =================

    /// <summary>O documento como apresentado ao cliente (para ler a diária normal).</summary>
    private Data.Pricing.Documento Apresentado() =>
        Data.Pricing.Apresentar(R.Documento(), R.Proposta.ModoApresentacao,
            Data.Pricing.Num(R.Params.TaxaAdmPct), Data.Pricing.Num(R.Params.DiariaTravada),
            Data.Pricing.Num(R.Params.TotalTravado));

    /// <summary>Diária normal (com impostos) da proposta atual, como sai para o cliente.</summary>
    protected double DiariaAtual => Data.Pricing.DiariaNormalApresentada(Apresentado());

    /// <summary>Meta de DIÁRIA: diária normal da proposta anterior × (1 + % a mais).</summary>
    protected double MetaAnterior =>
        Data.Pricing.Num(R.Params.PropAnteriorValor) * (1 + Data.Pricing.Pct(R.Params.PropAnteriorPct));

    /// <summary>Quanto falta na diária normal para superar a da proposta anterior.</summary>
    protected double FaltaParaAnterior
    {
        get
        {
            var alvo = MetaAnterior;
            if (alvo <= 0) return 0;
            var falta = alvo - DiariaAtual;
            return falta > 0 ? falta : 0;
        }
    }

    /// <summary>
    /// Injeta em OUTROS o custo necessário para a DIÁRIA NORMAL (com impostos)
    /// superar a diária da proposta anterior + % a mais. Como as demais linhas
    /// são múltiplos fixos da diária normal (sáb/dom = 2×; HE = 1,5×/2×), todas
    /// acompanham. Retorna a mensagem para exibir ao usuário.
    /// </summary>
    private Models.ItemDespesa GarantirOutros()
    {
        var outros = R.ItensDespesa.FirstOrDefault(d => Data.Pricing.EhOutros(d.Despesa));
        if (outros is null)
        {
            outros = new Models.ItemDespesa { Despesa = "OUTROS", Obs = "ADMINISTRATIVA", PorTecnico = false };
            R.ItensDespesa.Add(outros);
        }
        if (Data.Pricing.Num(outros.Qtd) < 1) outros.Qtd = "1";
        return outros;
    }

    /// <summary>
    /// Ajusta o custo de OUTROS (centavos, para cima ou para baixo) até a diária
    /// normal apresentada cravar no alvo; aplica o pino cosmético. Retorna a
    /// diária final.
    /// </summary>
    private double BuscarDiariaExata(double alvo, Models.ItemDespesa outros)
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        double Unit() => Data.Pricing.Num(outros.CustoUnitario);
        void SetUnit(double v) => outros.CustoUnitario = Math.Max(v, 0).ToString("0.##", inv);

        R.Params.DiariaTravada = "";              // solta o pino durante a busca
        for (var i = 0; i < 40; i++)
        {
            var dia = DiariaAtual;
            var erro = alvo - dia;
            if (Math.Abs(erro) <= 0.05) break;

            var u = Unit();
            if (u <= 0 && erro < 0) break;        // não dá para reduzir além de zero

            SetUnit(u + 10);
            var sensibilidade = (DiariaAtual - dia) / 10.0;
            SetUnit(u);
            if (sensibilidade <= 1e-9) break;

            SetUnit(u + erro / sensibilidade);
        }

        if (Math.Abs(DiariaAtual - alvo) <= 1.0)
            R.Params.DiariaTravada = alvo.ToString("0.00", inv);
        return DiariaAtual;
    }

    /// <summary>Grava a margem que fecha o total na meta e a trava do total.</summary>
    private void AplicarMargemParaTotal(double meta)
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        var m = Data.Pricing.MargemParaMeta(R.Calculo(), R.Params, meta,
            Data.Pricing.Num(R.Proposta.PrazoEntregaDias));
        R.Params.MargemAlvoPct = (m * 100).ToString("0.######", inv);
        R.Params.TotalTravado = meta.ToString("0.00", inv);
    }

    /// <summary>
    /// Ajusta a MARGEM (GM) até a diária normal apresentada cravar no alvo —
    /// o caminho quando não há custo em OUTROS para tirar ("tem que trabalhar
    /// na margem"). Aplica o pino cosmético no final.
    /// </summary>
    private double BuscarDiariaPorMargem(double alvo)
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        double M() => Data.Pricing.Num(R.Params.MargemAlvoPct);
        void SetM(double v) => R.Params.MargemAlvoPct = v.ToString("0.######", inv);

        R.Params.DiariaTravada = "";
        for (var i = 0; i < 40; i++)
        {
            var dia = DiariaAtual;
            var erro = alvo - dia;
            if (Math.Abs(erro) <= 0.05) break;

            var m = M();
            SetM(m + 0.1);
            var sensibilidade = (DiariaAtual - dia) / 0.1;
            SetM(m);
            if (Math.Abs(sensibilidade) <= 1e-9) break;

            SetM(m + erro / sensibilidade);
        }

        if (Math.Abs(DiariaAtual - alvo) <= 1.0)
            R.Params.DiariaTravada = alvo.ToString("0.00", inv);
        return DiariaAtual;
    }

    /// <summary>Editar a margem à mão solta as travas de diária e de total.</summary>
    protected void MargemEditada()
    {
        R.Params.DiariaTravada = "";
        R.Params.TotalTravado = "";
    }

    /// <summary>
    /// Com os multiplicadores obrigatórios (2º turno = 1,5×, sáb/dom = 2×, HE
    /// idem), o total é consequência direta da diária normal — a razão entre
    /// eles é fixa. Este aviso mostra o par compatível para cada meta.
    /// </summary>
    private string AvisoConflito(double diaAlvo, double totalAlvo)
    {
        var doc = Apresentado();
        var dia = Data.Pricing.DiariaNormalApresentada(doc);
        if (dia <= 0 || doc.Total <= 0) return "";
        // parte que escala com a diária vs. parte fixa (despesas + deslocamento)
        var fixo = doc.TotalDespesas + doc.Deslocamento;
        var escala = doc.Total - fixo;
        var totalNecessario = escala / dia * diaAlvo + fixo;
        var diariaPossivel = escala > 0 ? (totalAlvo - fixo) * dia / escala : 0;
        return $" ⚠ Com os multiplicadores fixos (2º turno 1,5× · sáb/dom 2×) o total é consequência da diária: para diária R$ {Data.Pricing.Moeda(diaAlvo)} o total fecha em R$ {Data.Pricing.Moeda(totalNecessario)}; para total R$ {Data.Pricing.Moeda(totalAlvo)} a diária fecha em R$ {Data.Pricing.Moeda(diariaPossivel)}. Use um desses pares.";
    }

    /// <summary>
    /// Botão "fechar a diária": crava SÓ a diária normal na meta da proposta
    /// anterior, ajustando OUTROS. Solta a trava do total — o valor final passa
    /// a ser consequência.
    /// </summary>
    protected string FecharDiaria()
    {
        var alvo = Math.Round(MetaAnterior, 2);
        if (alvo <= 0) return "Informe a diária normal da proposta anterior.";

        R.Params.DiariaTravada = "";
        R.Params.TotalTravado = "";
        if (DiariaAtual <= 0) return "Lance as diárias normais (1º turno) antes de ajustar.";

        var outros = GarantirOutros();
        var unitInicial = Data.Pricing.Num(outros.CustoUnitario);
        var diaFinal = BuscarDiariaExata(alvo, outros);

        // OUTROS não alcançou (chegou a zero e a diária continua acima da meta):
        // o fechamento é obrigatório — trabalha na MARGEM até cravar.
        var margemMexeu = false;
        if (Math.Abs(diaFinal - alvo) > 0.05)
        {
            diaFinal = BuscarDiariaPorMargem(alvo);
            margemMexeu = true;
        }

        if (Math.Abs(diaFinal - alvo) > 0.05)
            return $"Diária ficou em R$ {Data.Pricing.Moeda(diaFinal)} (meta R$ {Data.Pricing.Moeda(alvo)}) — não foi possível cravar nem pela margem.";

        var variacao = (Data.Pricing.Num(outros.CustoUnitario) - unitInicial)
                     * Math.Max(Data.Pricing.Num(outros.Qtd), 1)
                     * (outros.PorTecnico ? Math.Max(Data.Pricing.Inteiro(R.Params.QtdTecnicos), 1) : 1);
        var como = margemMexeu
            ? $"Margem ajustada para {Data.Pricing.Porcento(R.Calculo().ProjectMargin)}"
            : (variacao >= 0 ? $"OUTROS +R$ {Data.Pricing.Moeda(variacao)}" : $"OUTROS −R$ {Data.Pricing.Moeda(-variacao)}");
        return $"{como} → diária normal CRAVADA em R$ {Data.Pricing.Moeda(diaFinal)} ✓ · total resultante R$ {Data.Pricing.Moeda(Apresentado().Total)}";
    }

    /// <summary>Margem que resulta da meta de valor informada (função "chegar no valor").</summary>
    protected double MargemDaMeta =>
        Data.Pricing.MargemParaMeta(R.Calculo(), R.Params, Data.Pricing.Num(R.Params.MetaValor),
            Data.Pricing.Num(R.Proposta.PrazoEntregaDias));

    /// <summary>
    /// Botão "fechar o valor final": crava SÓ o total na meta de valor, via
    /// margem (GM). Solta a trava da diária — ela passa a ser consequência.
    /// </summary>
    protected string FecharTotal()
    {
        var meta = Math.Round(Data.Pricing.Num(R.Params.MetaValor), 2);
        if (meta <= 0) return "Informe a meta de valor final.";
        if (R.Calculo().CustoComRisco <= 0) return "Lance algum custo antes de usar a meta.";

        R.Params.DiariaTravada = "";
        AplicarMargemParaTotal(meta);

        var margem = Data.Pricing.Porcento(R.Calculo().ProjectMargin);
        return $"Margem ajustada para {margem} — total CRAVADO em R$ {Data.Pricing.Moeda(Apresentado().Total)} ✓" +
               $" · diária resultante R$ {Data.Pricing.Moeda(DiariaAtual)}";
    }

    /// <summary>
    /// Botão "fechar os dois": crava o total via margem (GM) e confere se a
    /// diária resultante bate na meta. Com os multiplicadores obrigatórios a
    /// razão diária ↔ total é fixa — quando as metas não casam, o aviso mostra
    /// o par exato que fecha.
    /// </summary>
    protected string FecharAmbos()
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        var diaAlvo = Math.Round(MetaAnterior, 2);
        var meta = Math.Round(Data.Pricing.Num(R.Params.MetaValor), 2);
        if (diaAlvo <= 0 && meta <= 0) return "Preencha a diária anterior (+ % a mais) e o valor final desejado.";
        if (diaAlvo <= 0) return "Para fechar os dois, informe também a diária normal da proposta anterior.";
        if (meta <= 0) return "Para fechar os dois, informe também o valor final desejado.";
        if (R.Calculo().CustoComRisco <= 0) return "Lance algum custo antes de usar as metas.";
        if (DiariaAtual <= 0) return "Lance as diárias normais (1º turno) antes de ajustar.";

        R.Params.DiariaTravada = "";
        AplicarMargemParaTotal(meta);
        var dia = DiariaAtual;

        // Diária resultante pertinho da meta (≤ R$ 1): o pino crava a diária
        // exata — todas as linhas derivam dela pelos multiplicadores — e o
        // resíduo de centavos é aparado no deslocamento para o total bater.
        if (Math.Abs(dia - diaAlvo) <= 1.0)
        {
            R.Params.DiariaTravada = diaAlvo.ToString("0.00", inv);
            var doc = Apresentado();
            if (Math.Abs(doc.Total - meta) <= 0.01 &&
                Math.Abs(Data.Pricing.DiariaNormalApresentada(doc) - diaAlvo) <= 0.005)
            {
                var margem = Data.Pricing.Porcento(R.Calculo().ProjectMargin);
                return $"Diária CRAVADA em R$ {Data.Pricing.Moeda(diaAlvo)} e total CRAVADO em R$ {Data.Pricing.Moeda(doc.Total)} ✓ (margem {margem})";
            }
            R.Params.DiariaTravada = "";
        }

        return $"Total CRAVADO em R$ {Data.Pricing.Moeda(Apresentado().Total)}, mas a diária resultante é R$ {Data.Pricing.Moeda(dia)} (meta R$ {Data.Pricing.Moeda(diaAlvo)})." +
               AvisoConflito(diaAlvo, meta);
    }

    /// <summary>
    /// Chamado pelo JavaScript quando OUTRA guia do navegador altera o rascunho:
    /// aplica o novo estado e re-renderiza — as duas guias ficam 100% espelhadas.
    /// </summary>
    [JSInvokable]
    public Task DraftAtualizado(string json)
    {
        if (json == _ultimoSalvo) return Task.CompletedTask;
        if (R.FromJson(json))
        {
            _ultimoSalvo = json;
            return InvokeAsync(StateHasChanged);
        }
        return Task.CompletedTask;
    }

    public void Dispose() => _jsRef?.Dispose();
}
