using System.Text.Json;
using HowdenAxiais.Poc.Models;

namespace HowdenAxiais.Poc.Data;

/// <summary>
/// A proposta que está sendo montada agora. É um serviço "scoped": no Blazor
/// Server isso equivale ao circuito do usuário, então o rascunho sobrevive à
/// navegação entre Custo → Pricing → Proposta sem gravar nada no banco.
/// </summary>
public class Rascunho
{
    public Proposta Proposta { get; private set; } = new();
    public List<ItemMO> ItensMO { get; set; } = new();
    public List<ItemDespesa> ItensDespesa { get; set; } = new();
    public PricingParams Params { get; private set; } = new();

    /// <summary>Verdadeiro depois que a proposta foi gravada no banco.</summary>
    public bool Gravada => !string.IsNullOrWhiteSpace(Proposta.Id);

    /// <summary>Rascunho ainda não iniciado (nenhum item carregado).</summary>
    public bool Vazio => ItensMO.Count == 0 && ItensDespesa.Count == 0;

    private double Prazo => Pricing.Num(Proposta.PrazoEntregaDias);

    public Pricing.Documento Documento() => Pricing.Montar(ItensMO, ItensDespesa, Params, Prazo);
    public Pricing.Resultado Calculo() => Pricing.Calcular(ItensMO, ItensDespesa, Params, Prazo);

    /// <summary>
    /// Aplica a hora-base aos serviços com multiplicador (planilha: E9=E8×1,5;
    /// E10=E8×2…). Itens com multiplicador zero não são tocados, e qualquer
    /// valor pode ser editado depois, item a item, só nesta proposta.
    /// </summary>
    public void AplicarHoraBase()
    {
        var baseHora = Pricing.Num(Params.HoraBase);
        if (baseHora <= 0) return;
        foreach (var i in ItensMO)
        {
            var m = Pricing.Num(i.Mult);
            if (m > 0) i.CustoHora = (baseHora * m).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    /// <summary>Começa uma proposta nova, já com os itens padrão da tabela de custos.</summary>
    public void Novo(List<Parametro> parametros, string usuario,
        IReadOnlyDictionary<string, string>? padroes = null)
    {
        Proposta = new Proposta
        {
            Data = DateTime.Today.ToString("yyyy-MM-dd"),
            Ano = DateTime.Today.Year.ToString(),
            PreparadaPor = usuario,
        };
        Params = new PricingParams();

        // Padrões configurados em "E-mails e Padrões" (valor vazio = de fábrica).
        string? V(string chave) => padroes is not null &&
            padroes.TryGetValue(chave, out var v) && !string.IsNullOrWhiteSpace(v) ? v : null;
        Proposta.Bu = V("padrao.bu") ?? Proposta.Bu;
        Proposta.Estado = V("padrao.estado") ?? Proposta.Estado;
        Proposta.Segmento = V("padrao.segmento") ?? Proposta.Segmento;
        Proposta.Idioma = V("padrao.idioma") ?? Proposta.Idioma;
        Proposta.Moeda = V("padrao.moeda") ?? Proposta.Moeda;
        Proposta.ValidadeDias = V("padrao.validade") ?? Proposta.ValidadeDias;
        Proposta.PrazoEntregaDias = V("padrao.prazo") ?? Proposta.PrazoEntregaDias;
        Proposta.ModoApresentacao = V("padrao.modo") ?? Proposta.ModoApresentacao;
        Proposta.NotasDesmarcadas = V("notas.padrao.desmarcadas") ?? "";
        Params.TaxaAdmPct = V("padrao.taxaAdm") ?? Params.TaxaAdmPct;
        Params.RiscoPct = V("padrao.risco") ?? Params.RiscoPct;
        Params.FiancaTipo = V("padrao.fiancaTipo") ?? Params.FiancaTipo;
        Params.FiancaPctVenda = V("padrao.fiancaPct") ?? Params.FiancaPctVenda;
        Params.PropAnteriorPct = V("padrao.percAMais") ?? Params.PropAnteriorPct;
        Params.IssPct = Axiais.IssPadrao(Proposta.Bu);
        Params.MargemAlvoPct = Axiais.MargemPadrao(Proposta.Segmento, padroes);

        // BU do Chile/Peru: proposta em espanhol, na moeda local e SEM impostos
        // (só valor líquido) — e a tabela de custos é a do país.
        var pais = Axiais.PaisDaBu(Proposta.Bu);
        if (pais != "Brasil")
        {
            Proposta.Idioma = "Español";
            Proposta.Moeda = Axiais.MoedaPadrao(Proposta.Bu);
            Params.PisPct = "0"; Params.CofinsPct = "0"; Params.IssPct = "0";
        }
        parametros = parametros.Where(p => (string.IsNullOrWhiteSpace(p.Pais) ? "Brasil" : p.Pais) == pais).ToList();
        ItensMO = parametros.Where(p => p.Tipo == "MO").Select(p => new ItemMO
        {
            Servico = p.Descricao, Obs = p.Obs, Horas = p.Horas,
            CustoHora = p.Valor, QtdDiaria = "0", PorTecnico = p.PorTecnico == "Sim",
            Mult = string.IsNullOrWhiteSpace(p.Mult) ? "0" : p.Mult,
        }).ToList();
        ItensDespesa = parametros.Where(p => p.Tipo == "DESPESA").Select(p => new ItemDespesa
        {
            Despesa = p.Descricao, Obs = p.Obs, Qtd = "0",
            CustoUnitario = p.Valor, PorTecnico = p.PorTecnico == "Sim",
        }).ToList();

        // Tabela de custos antiga (sem a coluna de multiplicador): os
        // multiplicadores obrigatórios são re-identificados pelo nome.
        MigrarMultiplicadores();
    }

    /// <summary>
    /// Troca a tabela de custos para a do país informado (mudança de BU),
    /// preservando as quantidades já lançadas: os itens são casados por
    /// posição, já que as três tabelas têm a mesma estrutura.
    /// </summary>
    public void TrocarTabela(List<Parametro> parametros, string pais)
    {
        var doPais = parametros
            .Where(p => (string.IsNullOrWhiteSpace(p.Pais) ? "Brasil" : p.Pais) == pais)
            .OrderBy(p => p.Ordem).ToList();

        var mo = doPais.Where(p => p.Tipo == "MO").ToList();
        for (var i = 0; i < ItensMO.Count && i < mo.Count; i++)
        {
            ItensMO[i].Servico = mo[i].Descricao;
            ItensMO[i].Obs = mo[i].Obs;
            ItensMO[i].Horas = mo[i].Horas;
            ItensMO[i].CustoHora = mo[i].Valor;
            ItensMO[i].PorTecnico = mo[i].PorTecnico == "Sim";
            ItensMO[i].Mult = string.IsNullOrWhiteSpace(mo[i].Mult) ? "0" : mo[i].Mult;
        }
        var desp = doPais.Where(p => p.Tipo == "DESPESA").ToList();
        for (var i = 0; i < ItensDespesa.Count && i < desp.Count; i++)
        {
            ItensDespesa[i].Despesa = desp[i].Descricao;
            ItensDespesa[i].Obs = desp[i].Obs;
            ItensDespesa[i].CustoUnitario = desp[i].Valor;
            ItensDespesa[i].PorTecnico = desp[i].PorTecnico == "Sim";
        }
        MigrarMultiplicadores();
    }

    /// <summary>Carrega uma proposta gravada de volta para edição.</summary>
    public void Carregar(Proposta p)
    {
        Proposta = p;
        ItensMO = Des<List<ItemMO>>(p.ItensMoJson) ?? new();
        ItensDespesa = Des<List<ItemDespesa>>(p.ItensDespesaJson) ?? new();
        Params = Des<PricingParams>(p.PricingJson) ?? new PricingParams();
        MigrarMultiplicadores();
    }

    /// <summary>
    /// Rascunhos e propostas gravados antes do multiplicador da hora-base não
    /// carregam essa marcação — sem ela, as regras de diária (sáb/dom = 2×,
    /// HE = 1,5×/2×) e a ferramenta de proposta anterior não reconhecem as
    /// linhas. Re-identifica pelo nome do serviço.
    /// </summary>
    private void MigrarMultiplicadores()
    {
        foreach (var i in ItensMO)
        {
            if (Pricing.Num(i.Mult) > 0) continue;
            var m = Pricing.MultPeloNome(i.Servico, i.Obs);
            if (m != "") i.Mult = m;
        }
    }

    /// <summary>Copia o estado atual para o objeto que vai ser gravado.</summary>
    public Proposta ParaGravar()
    {
        var doc = Documento();
        Proposta.ItensMoJson = JsonSerializer.Serialize(ItensMO);
        Proposta.ItensDespesaJson = JsonSerializer.Serialize(ItensDespesa);
        Proposta.PricingJson = JsonSerializer.Serialize(Params);
        Proposta.CustoTotal = doc.Calculo.CustoTotal.ToString(System.Globalization.CultureInfo.InvariantCulture);
        Proposta.Total = doc.Total.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return Proposta;
    }

    // ---- persistência no navegador (sobrevive a F5 e a abrir a página direto) ----
    private sealed record Estado(Proposta Proposta, List<ItemMO> MO, List<ItemDespesa> Despesas, PricingParams Params);

    public string ToJson() =>
        JsonSerializer.Serialize(new Estado(Proposta, ItensMO, ItensDespesa, Params));

    public bool FromJson(string? json)
    {
        var e = Des<Estado>(json ?? "");
        if (e is null || e.Proposta is null) return false;
        Proposta = e.Proposta;
        ItensMO = e.MO ?? new();
        ItensDespesa = e.Despesas ?? new();
        Params = e.Params ?? new PricingParams();
        MigrarMultiplicadores();
        return true;
    }

    private static T? Des<T>(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return default;
        try { return JsonSerializer.Deserialize<T>(json); } catch { return default; }
    }
}
