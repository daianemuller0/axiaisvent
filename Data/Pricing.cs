using System.Globalization;
using HowdenAxiais.Poc.Models;

namespace HowdenAxiais.Poc.Data;

/// <summary>
/// Motor de cálculo da proposta de serviço — a tradução em C# das guias
/// CUSTO e PRICING da planilha.
///
/// Cadeia de preço (idêntica à planilha):
///   custo (MO + despesas)
///     → + risco                                  (PRICING E36/E38)
///     → ÷ (1 − provisões − margem alvo)          (PRICING E52, "montar preço a partir da margem")
///     = VENDA LÍQUIDA (valor sem impostos)       (PRICING Q18)
///     → ÷ (1 − PIS − COFINS − ISS)               (PRICING Q20)
///     = VALOR COM IMPOSTOS
///
/// O preço de cada item da proposta sai por participação no custo:
/// participação = custo do item ÷ custo total; valor do item = participação ×
/// valor com impostos (é o que a coluna L da guia CUSTO fazia).
/// </summary>
public static class Pricing
{
    // ---------- conversões tolerantes (aceita "1.234,56" e "1,234.56") ----------
    public static double Num(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return 0;
        s = s.Trim().Replace("R$", "").Replace("$", "").Replace("%", "").Replace(" ", "");
        if (s.Contains(',') && s.LastIndexOf(',') > s.LastIndexOf('.'))
            s = s.Replace(".", "").Replace(',', '.');
        else
            s = s.Replace(",", "");
        return double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0;
    }

    /// <summary>Lê um campo digitado em porcentagem ("3" = 3%) como fração (0,03).</summary>
    public static double Pct(string? s) => Num(s) / 100.0;

    public static int Inteiro(string? s)
    {
        var v = (int)Math.Round(Num(s));
        return v < 0 ? 0 : v;
    }

    /// <summary>Arredondamento para cima usado pelas classes VBA (RoundUp).</summary>
    public static double ParaCima(double v) => Math.Ceiling(Math.Round(v, 6));

    public static string Moeda(double v) => v.ToString("#,##0.00", CultureInfo.GetCultureInfo("pt-BR"));
    public static string Moeda0(double v) => v.ToString("#,##0", CultureInfo.GetCultureInfo("pt-BR"));
    public static string Porcento(double fracao) =>
        (fracao * 100).ToString("#,##0.00", CultureInfo.GetCultureInfo("pt-BR")) + "%";

    // ---------- custo ----------
    public static double CustoMO(ItemMO i, int tecnicos) =>
        Num(i.QtdDiaria) * Num(i.CustoHora) * Num(i.Horas) * (i.PorTecnico ? Math.Max(tecnicos, 1) : 1);

    public static double CustoDespesa(ItemDespesa d, int tecnicos) =>
        Num(d.Qtd) * Num(d.CustoUnitario) * (d.PorTecnico ? Math.Max(tecnicos, 1) : 1);

    // ---------- seguro garantia / carta de fiança (listas!Custo_Garantia) ----------
    /// <summary>Instrumentos de garantia e taxa anual (% a.a.) da planilha.</summary>
    public static readonly (string Nome, double TaxaAnualPct)[] GarantiaTipos =
    {
        ("Não", 0),
        ("HSA C. Fiança (Banco)", 5.0),
        ("HSA S. Garantia (Corretora)", 1.0),
        ("HCHL/HPU C. Fiança (Banco)", 3.75),
        ("HCHL/HPU S. Garantia (Corretora)", 1.75),
    };

    /// <summary>"OUTROS" é a linha de acerto de valores: nunca sai na proposta ao cliente.</summary>
    public static bool EhOutros(string? nome) =>
        (nome ?? "").Trim().Equals("OUTROS", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Despesas "por dia de trabalho": a quantidade acompanha a soma das
    /// diárias lançadas (hospedagem, carro, combustível e refeições).
    /// </summary>
    public static bool EhDespesaDiaria(string? nome)
    {
        var n = (nome ?? "").ToUpperInvariant();
        return n.Contains("HOSPEDAGEM") || n.Contains("LOCA") || n.Contains("COMBUST") || n.Contains("REFEI");
    }

    /// <summary>
    /// Despesas de deslocamento (táxi + passagem aérea): no modo sem despesas,
    /// saem numa linha própria abaixo do total, a custo + taxa administrativa.
    /// </summary>
    public static bool EhDeslocamento(string? nome)
    {
        var n = (nome ?? "").ToUpperInvariant();
        return n.Contains("TAXI") || n.Contains("TÁXI") || n.Contains("PASSAGEM");
    }

    /// <summary>Linha de DIÁRIA da assessoria (normais, 2º turno, extras sáb/dom).</summary>
    private static bool EhLinhaDiaria(LinhaMO l) =>
        l.QtdDiaria > 0 && (l.Servico ?? "").TrimStart().ToUpperInvariant().StartsWith("DIARIAS");

    /// <summary>Linha de HORA EXTRA: o lançamento é em HORAS, não em diárias.</summary>
    public static bool EhHoraExtra(string? servico) =>
        (servico ?? "").TrimStart().ToUpperInvariant().StartsWith("HORAS EXTRAS");

    /// <summary>
    /// Multiplicador obrigatório re-identificado pelo NOME do serviço — para
    /// bases e propostas gravadas ANTES da coluna de multiplicador existir:
    /// 2º turno = 1,5×; sáb/dom/fer = 2×; HE semana = 1,5×; HE sáb/dom = 2×.
    /// Vazio = linha sem multiplicador (equipamentos, treinamentos…).
    /// </summary>
    public static string MultPeloNome(string? servico, string? obs)
    {
        var s = (servico ?? "").TrimStart().ToUpperInvariant();
        var o = (obs ?? "").ToUpperInvariant();
        var fds = s.Contains("SAB") || s.Contains("DOM") || s.Contains("FER");
        if (s.StartsWith("DIARIAS NORMAIS")) return o.Contains("2O") || o.Contains("2°") ? "1.5" : "1";
        if (s.Contains("DIARIAS EXTRAS") && fds) return "2";
        if (s.Contains("HORAS EXTRAS")) return fds ? "2" : "1.5";
        return "";
    }

    /// <summary>
    /// Margem (Project Margin) que resulta se o valor final com impostos for
    /// exatamente a meta informada — a função "chegar no valor".
    /// </summary>
    public static double MargemParaMeta(Resultado c, PricingParams p, double meta, double prazoDias = 0)
    {
        if (meta <= 0 || c.CustoComRisco <= 0) return 0;
        var denImp = 1 - Pct(p.PisPct) - Pct(p.CofinsPct) - Pct(p.IssPct);
        var vendaLiquida = meta * denImp;
        if (vendaLiquida <= 0) return 0;
        var provisoes = ComissoesFrac(p) + Pct(p.MargemNegociacaoPct) + Pct(p.PmSachPct)
                      + Pct(p.GarantiaProjetoPct) + Pct(p.PortalPct);
        return 1 - provisoes - FiancaFrac(p, prazoDias) - c.CustoComRisco / vendaLiquida;
    }

    public static double TaxaGarantia(string tipo) =>
        GarantiaTipos.FirstOrDefault(g => g.Nome == tipo).TaxaAnualPct / 100.0;

    /// <summary>
    /// Comissão total (fração), como a tabela de comissões da planilha:
    /// PPR + Sales Director + Sales Industrial + DSR×(comissões de Sales) + Rep1 + Rep2.
    /// </summary>
    public static double ComissoesFrac(PricingParams p)
    {
        var sales = Pct(p.SalesDirPct) + Pct(p.SalesIndPct);
        return Pct(p.PprPct) + sales + Pct(p.DsrFatorPct) * sales + Pct(p.Rep1Pct) + Pct(p.Rep2Pct);
    }

    /// <summary>
    /// Fração da venda consumida pela fiança: % coberto × dias × taxa anual ÷ 365.
    /// Dias em branco (ou zero) = acompanha o PRAZO DE ENTREGA, como na planilha
    /// (cobertura R49 = prazo − evento de pagamento).
    /// </summary>
    public static double FiancaFrac(PricingParams p, double prazoDias = 0)
    {
        var taxa = TaxaGarantia(p.FiancaTipo);
        if (taxa <= 0) return 0;
        var dias = Num(p.FiancaDias);
        if (dias <= 0) dias = prazoDias;
        return Pct(p.FiancaPctVenda) * dias * taxa / 365.0;
    }

    /// <summary>
    /// Emulação exata da macro WILGERAPRECO da planilha: markup parte de 1,5 e
    /// sobe/desce em degraus (0,1 → 0,05 → 0,01 → … → 0,000005) até a Project
    /// Margin cruzar o alvo. Na planilha: venda = markup × custo com riscos ÷
    /// (1 − provisões − k) e PM(markup) = (1 − provTotal) × (1 − 1/markup),
    /// onde provTotal = provisões + fração da fiança.
    /// </summary>
    private static double VendaEscadaPlanilha(double custoRisco, double provTotal, double alvo)
    {
        double PM(double m) => (1 - provTotal) * (1 - 1 / m);

        var mup = 1.5;
        if (Math.Abs(PM(mup) - alvo) > 1e-12)
        {
            double[] sobe = { 0.1, 0.01, 0.001, 0.0001, 0.00001 };
            double[] desce = { 0.05, 0.005, 0.0005, 0.00005, 0.000005 };
            for (var i = 0; i < sobe.Length; i++)
            {
                while (PM(mup) < alvo) mup += sobe[i];
                while (PM(mup) > alvo) mup -= desce[i];
            }
        }
        return mup * custoRisco / (1 - provTotal);
    }

    // ---------- resultado ----------
    public sealed record Resultado(
        double TotalMO, double TotalDespesas, double CustoTotal,
        double Risco, double CustoComRisco, double Fianca,
        double Comissoes, double MargemNegociacao, double PmSach, double GarantiaProjeto, double Portal,
        double VendaLiquida, double ComPisCofins, double ComImpostos,
        double Pis, double Cofins, double Iss,
        double FatorLiquido, double FatorPisCofins, double FatorComImpostos,
        double Markup, double ProjectMargin, double ContributionMargin,
        double ComissoesFracTotal, double Dsr);

    public static Resultado Calcular(IEnumerable<ItemMO> mo, IEnumerable<ItemDespesa> desp, PricingParams p, double prazoDias = 0)
    {
        var tec = Math.Max(Inteiro(p.QtdTecnicos), 1);
        var totalMO = mo.Sum(i => CustoMO(i, tec));
        var totalDesp = desp.Sum(d => CustoDespesa(d, tec));
        var custo = totalMO + totalDesp;

        var risco = custo * Pct(p.RiscoPct);
        var custoRisco = custo + risco;

        var comissoesFrac = ComissoesFrac(p);
        var provisoes = comissoesFrac + Pct(p.MargemNegociacaoPct) + Pct(p.PmSachPct)
                      + Pct(p.GarantiaProjetoPct) + Pct(p.PortalPct);
        var margem = Pct(p.MargemAlvoPct);

        // Venda líquida a partir da margem alvo. A fiança é proporcional à própria
        // venda (fração k), então entra no denominador — mesma conta que a planilha
        // fecha por iteração: venda×(1−prov−margem) = custoRisco + venda×k.
        var k = FiancaFrac(p, prazoDias);
        var den = 1 - provisoes - margem - k;
        var vendaLiquida = den > 0.0001 ? custoRisco / den : 0;

        // Paridade com o botão "Calcular Preço" da planilha (macro WILGERAPRECO):
        // ela acha o markup por busca em degraus e PARA no primeiro degrau que
        // cruza a margem alvo — não no valor exato. Reproduzimos os mesmos
        // degraus para o preço bater centavo a centavo com a planilha.
        if (vendaLiquida > 0 && den > 0.0001 && margem > 0)
            vendaLiquida = VendaEscadaPlanilha(custoRisco, provisoes + k, margem);

        var fianca = vendaLiquida * k;

        var pis = Pct(p.PisPct);
        var cofins = Pct(p.CofinsPct);
        var iss = Pct(p.IssPct);
        var denImp = 1 - pis - cofins - iss;

        // Total cravado pela ferramenta de meta: apara resíduo de arredondamento
        // da margem e da busca em degraus (≤ 50 centavos) para bater em ponto.
        var travado = Num(p.TotalTravado);
        if (travado > 0 && denImp > 0.0001 && Math.Abs(vendaLiquida / denImp - travado) <= 0.5)
            vendaLiquida = travado * denImp;

        var comImpostos = denImp > 0.0001 ? vendaLiquida / denImp : 0;
        var comPisCofins = comImpostos * (1 - iss);

        var salesFrac = Pct(p.SalesDirPct) + Pct(p.SalesIndPct);
        var comissoes = vendaLiquida * comissoesFrac;
        var mn = vendaLiquida * Pct(p.MargemNegociacaoPct);
        var pmSach = vendaLiquida * Pct(p.PmSachPct);
        var garantia = vendaLiquida * Pct(p.GarantiaProjetoPct);
        var portal = vendaLiquida * Pct(p.PortalPct);

        var pm = vendaLiquida > 0
            ? (vendaLiquida - (custoRisco + fianca + comissoes + mn + pmSach + garantia + portal)) / vendaLiquida
            : 0;
        var cm = vendaLiquida > 0
            ? (vendaLiquida - (custoRisco + fianca + mn + pmSach + garantia + portal)) / vendaLiquida
            : 0;
        var markup = custoRisco > 0 ? (vendaLiquida * (1 - provisoes) - fianca) / custoRisco : 0;

        return new Resultado(
            totalMO, totalDesp, custo,
            risco, custoRisco, fianca,
            comissoes, mn, pmSach, garantia, portal,
            vendaLiquida, comPisCofins, comImpostos,
            comImpostos * pis, comImpostos * cofins, comImpostos * iss,
            custo > 0 ? vendaLiquida / custo : 0,
            custo > 0 ? comPisCofins / custo : 0,
            custo > 0 ? comImpostos / custo : 0,
            markup, pm, cm,
            comissoesFrac, vendaLiquida * Pct(p.DsrFatorPct) * salesFrac);
    }

    // ---------- itens já precificados (o que sai impresso na proposta) ----------
    public sealed record LinhaMO(string Servico, string Obs, double Horas, double QtdDiaria,
        double Custo, double Participacao, double ValorTotal, double ValorDiaria, double ValorHora,
        double Mult = 0);

    public sealed record LinhaDespesa(string Despesa, string Obs, double Qtd,
        double Custo, double Participacao, double ValorUnitario, double ValorTotal);

    public sealed record Complementar(string Descricao, string Obs, double Qtd, double Valor);

    public sealed record Documento(
        List<LinhaMO> MO, List<LinhaDespesa> Despesas, List<Complementar> Complementares,
        double TotalMO, double TotalDespesas, double Total, Resultado Calculo,
        double Deslocamento = 0,
        double DescontoDia = 0,    // modo aberto: despesas do dia descontadas da diária
        double TaxaAdmPct = 0);    // taxa adm usada na apresentação (p/ rótulos)

    /// <summary>
    /// Distribui o preço de venda entre os itens (proporcional ao custo) e monta
    /// as linhas da proposta, com os mesmos arredondamentos das classes VBA:
    /// valor total do item para cima em reais inteiros; diária = total ÷ qtd.
    /// </summary>
    public static Documento Montar(List<ItemMO> mo, List<ItemDespesa> desp, PricingParams p, double prazoDias = 0)
    {
        var calc = Calcular(mo, desp, p, prazoDias);
        var tec = Math.Max(Inteiro(p.QtdTecnicos), 1);
        var custoTotal = calc.CustoTotal;

        var linhasMO = new List<LinhaMO>();
        foreach (var i in mo)
        {
            var custo = CustoMO(i, tec);
            if (custo <= 0) continue;
            var part = custoTotal > 0 ? custo / custoTotal : 0;
            var total = ParaCima(part * calc.ComImpostos);
            var qtdDiaria = Num(i.QtdDiaria);
            var horas = Num(i.Horas);
            var diaria = qtdDiaria > 0 ? Math.Round(total / qtdDiaria, 2) : 0;
            var hora = horas > 0 ? Math.Round(diaria / horas, 2) : 0;
            linhasMO.Add(new LinhaMO(i.Servico, i.Obs, horas, qtdDiaria, custo, part, total, diaria, hora, Num(i.Mult)));
        }

        var linhasDesp = new List<LinhaDespesa>();
        foreach (var d in desp)
        {
            var custo = CustoDespesa(d, tec);
            if (custo <= 0) continue;
            var part = custoTotal > 0 ? custo / custoTotal : 0;
            var total = ParaCima(part * calc.ComImpostos);
            var qtd = Num(d.Qtd) * (d.PorTecnico ? tec : 1);
            var unit = qtd > 0 ? ParaCima(total / qtd) : 0;
            linhasDesp.Add(new LinhaDespesa(d.Despesa, d.Obs, qtd, custo, part, unit, total));
        }

        var totalMO = linhasMO.Sum(l => l.ValorTotal);
        var totalDesp = linhasDesp.Sum(l => l.ValorTotal);

        return new Documento(linhasMO, linhasDesp, Complementares(linhasMO, linhasDesp),
            totalMO, totalDesp, totalMO + totalDesp, calc);
    }

    /// <summary>
    /// Versão do documento APRESENTADA ao cliente. O pricing interno não muda —
    /// só a distribuição entre as tabelas:
    ///
    ///  - "ComDespesas": as despesas saem a custo + taxa administrativa (ex.: 30%);
    ///    a diferença até o preço real delas é embutida nas diárias da assessoria.
    ///  - "SemDespesas": a tabela de despesas some e todo o valor vai para as diárias.
    ///
    /// O TOTAL C/ IMPOSTOS é exatamente o mesmo nas duas formas.
    /// </summary>
    public static Documento Apresentar(Documento doc, string modo, double taxaAdmPct,
        double diariaTravada = 0, double totalTravado = 0)
    {
        // Sem linhas de assessoria não há onde embutir — mantém como está.
        if (doc.MO.Count == 0) return doc;

        // A DIÁRIA FECHADA manda: o layout base é sempre o "sem despesas"
        // (tudo embutido nas diárias, deslocamento numa linha à parte). O modo
        // "com despesas" é derivado DELA logo abaixo.
        var admFrac = 1 + taxaAdmPct / 100.0;
        var deslocFechado = doc.Despesas
            .Where(d => EhDeslocamento(d.Despesa))
            .Sum(d => ParaCima(d.Custo * admFrac));
        var deslocamento = deslocFechado;

        var desp = new List<LinhaDespesa>();
        if (modo != "SemDespesas")
        {
            // Tabela de despesas a custo + taxa adm ("OUTROS" nunca sai — é
            // diluído nas diárias).
            desp = doc.Despesas.Where(d => !EhOutros(d.Despesa)).Select(d =>
            {
                var custoUnit = d.Qtd > 0 ? d.Custo / d.Qtd : d.Custo;
                var unit = ParaCima(custoUnit * admFrac);
                var total = unit * Math.Max(d.Qtd, 1);
                return new LinhaDespesa(d.Despesa, d.Obs, d.Qtd, d.Custo, d.Participacao, unit, total);
            }).ToList();
        }

        var alvoMO = doc.Total - deslocFechado;
        if (alvoMO <= 0 || doc.TotalMO <= 0) return doc;

        // Os multiplicadores são LEI sobre a diária normal apresentada:
        // 2º turno = 1,5× a diária do 1º turno, sáb/dom/feriado = 2×, e o mesmo
        // vale para as horas extras sobre a hora normal. Todo o valor que sobe
        // das despesas (inclusive o excedente do deslocamento) entra na diária
        // normal — e as derivadas acompanham exatamente pelo multiplicador.
        // As linhas sem multiplicador (equipamentos, terceiros…) mantêm o valor
        // distribuído por participação no custo.
        var comMult = doc.MO.Where(l => l.Mult > 0 && l.QtdDiaria > 0).ToList();
        var pesoW = comMult.Sum(l => l.Mult * Math.Max(l.Horas, 1) * l.QtdDiaria);
        var livres = doc.MO.Where(l => !(l.Mult > 0 && l.QtdDiaria > 0)).Sum(l => l.ValorTotal);
        var pool = alvoMO - livres;

        var mo = new List<LinhaMO>();
        if (pesoW > 0 && pool > 0)
        {
            // hora normal: pool ÷ pesos, arredondada para cima no centavo
            var horaNormal = Math.Ceiling(pool / pesoW * 100) / 100;

            // Diária cravada pelas ferramentas de preço: a hora normal é derivada
            // do valor travado (tolerância de R$ 1 contra travas antigas) — e como
            // TODAS as linhas saem da hora normal, os multiplicadores continuam
            // exatos sobre a diária cravada.
            var normalRef = doc.MO.FirstOrDefault(l => l.Mult is > 0.99 and < 1.01 && l.QtdDiaria > 0);
            if (diariaTravada > 0 && normalRef is not null)
            {
                var horasNormal = Math.Max(normalRef.Horas, 1);
                var naturalDia = Math.Round(horaNormal * horasNormal, 2);
                if (Math.Abs(naturalDia - diariaTravada) <= 1.0)
                    horaNormal = diariaTravada / horasNormal;
            }

            foreach (var l in doc.MO)
            {
                if (l.Mult > 0 && l.QtdDiaria > 0)
                {
                    var diaria = Math.Round(l.Mult * horaNormal * Math.Max(l.Horas, 1), 2);
                    if (diariaTravada > 0 && l.Mult is > 0.99 and < 1.01 &&
                        Math.Abs(diaria - diariaTravada) <= 0.02)
                        diaria = diariaTravada;
                    var total = Math.Round(diaria * l.QtdDiaria, 2);
                    var horaLinha = Math.Round(diaria / Math.Max(l.Horas, 1), 2);
                    mo.Add(new LinhaMO(l.Servico, l.Obs, l.Horas, l.QtdDiaria, l.Custo, l.Participacao, total, diaria, horaLinha, l.Mult));
                }
                else
                {
                    mo.Add(l);
                }
            }
        }
        else
        {
            // fallback: distribuição proporcional ao custo (nenhuma linha com multiplicador)
            double acumulado = 0;
            for (var i = 0; i < doc.MO.Count; i++)
            {
                var l = doc.MO[i];
                double total = i == doc.MO.Count - 1
                    ? alvoMO - acumulado
                    : ParaCima(alvoMO * (l.ValorTotal / doc.TotalMO));
                acumulado += total;
                var diaria = l.QtdDiaria > 0 ? Math.Round(total / l.QtdDiaria, 2) : 0;
                var hora = l.Horas > 0 ? Math.Round(diaria / l.Horas, 2) : 0;
                mo.Add(new LinhaMO(l.Servico, l.Obs, l.Horas, l.QtdDiaria, l.Custo, l.Participacao, total, diaria, hora, l.Mult));
            }
        }

        // Modo "com despesas abertas + taxa": a diária mostrada = DIÁRIA FECHADA
        // menos o PREÇO DE VENDA do dia de despesas — a soma dos valores
        // unitários mostrados de hospedagem, locação de carro, combustível e
        // refeições (já com a taxa adm). Ex.: fechada 9.000 − (560+224+42+140)
        // = 8.034. O total com impostos continua o mesmo nos dois modos.
        var descontoDia = 0.0;
        if (modo != "SemDespesas" && desp.Count > 0)
        {
            deslocamento = 0;   // táxi + passagem ficam na tabela de despesas
            descontoDia = desp.Where(d => EhDespesaDiaria(d.Despesa)).Sum(d => d.ValorUnitario);
            if (descontoDia > 0)
            {
                for (var i = 0; i < mo.Count; i++)
                {
                    var l = mo[i];
                    if (!EhLinhaDiaria(l)) continue;
                    var diaria = Math.Round(l.ValorDiaria - descontoDia, 2);
                    if (diariaTravada > 0 && l.Mult is > 0.99 and < 1.01 &&
                        Math.Abs(diaria - diariaTravada) <= 1.0)
                        diaria = diariaTravada;
                    mo[i] = l with
                    {
                        ValorDiaria = diaria,
                        ValorTotal = Math.Round(diaria * l.QtdDiaria, 2),
                        ValorHora = Math.Round(diaria / Math.Max(l.Horas, 1), 2),
                    };
                }
            }
        }

        var totalMO = mo.Sum(l => l.ValorTotal);
        var totalDesp = desp.Sum(d => d.ValorTotal);
        var totalGeral = totalMO + totalDesp + deslocamento;

        // O TOTAL apresentado crava no valor com impostos EXATO do pricing
        // (o Q20 da planilha) — ou na meta travada, quando houver. O resíduo
        // dos arredondamentos por linha (até R$ 10) NUNCA mexe nas linhas da
        // assessoria — os multiplicadores são lei. Ele é aparado no
        // deslocamento (quando houver) ou na maior despesa mostrada.
        var alvoTotal = totalTravado > 0 ? totalTravado : Math.Round(doc.Calculo.ComImpostos, 2);
        if (alvoTotal > 0)
        {
            var diff = Math.Round(alvoTotal - totalGeral, 2);
            if (diff == 0 || Math.Abs(diff) > 10)
            {
                // Resíduo grande (ex.: a noite de hotel a menos no desconto da
                // diária aberta): o total mostra o valor exato do pricing mesmo
                // assim — igual a planilha sempre fez (H34 = Q20).
                totalGeral = alvoTotal;
            }
            else if (deslocamento > 0 && deslocamento + diff > 0)
            {
                deslocamento = Math.Round(deslocamento + diff, 2);
                totalGeral = alvoTotal;
            }
            else
            {
                var alvoDesp = desp.Where(d => d.ValorTotal > 0)
                    .OrderByDescending(d => Math.Abs(d.Qtd - 1) < 0.001 ? 1 : 0)
                    .ThenByDescending(d => d.ValorTotal)
                    .FirstOrDefault();
                if (alvoDesp is not null && alvoDesp.ValorTotal + diff > 0)
                {
                    var idx = desp.IndexOf(alvoDesp);
                    var novoTotal = Math.Round(alvoDesp.ValorTotal + diff, 2);
                    var unit = alvoDesp.Qtd > 0 ? Math.Round(novoTotal / alvoDesp.Qtd, 2) : novoTotal;
                    desp[idx] = alvoDesp with { ValorTotal = novoTotal, ValorUnitario = unit };
                    totalDesp = Math.Round(totalDesp + diff, 2);
                }
                // Sem onde absorver (proposta só de assessoria): o total mostra o
                // valor exato mesmo assim — igual a planilha sempre fez (H34 = Q20),
                // com as linhas arredondadas para cima em reais inteiros.
                totalGeral = alvoTotal;
            }
        }

        // HORA EXTRA é lançada em HORAS: para o cliente, a linha mostra o total
        // de horas na coluna HORAS e ZERO em QTD. DIARIA (5 horas extras =
        // 5 horas e 0 dias) — sem "valor 1 diária". O valor total não muda.
        for (var i = 0; i < mo.Count; i++)
        {
            var l = mo[i];
            if (!EhHoraExtra(l.Servico) || l.QtdDiaria <= 0) continue;
            mo[i] = l with { Horas = l.Horas * l.QtdDiaria, QtdDiaria = 0, ValorDiaria = 0 };
        }

        return new Documento(mo, desp, Complementares(mo, desp), totalMO, totalDesp,
            totalGeral, doc.Calculo, deslocamento, descontoDia, taxaAdmPct);
    }

    /// <summary>
    /// Resumo de impostos derivado do TOTAL APRESENTADO — para o rodapé bater
    /// zerado com o "TOTAL C/ IMPOSTOS" do documento, usando as mesmas frações
    /// de PIS/COFINS/ISS do cálculo interno.
    /// </summary>
    public static (double SemImpostos, double ComPisCofins) ResumoDoTotal(Documento doc)
    {
        var c = doc.Calculo;
        if (c.ComImpostos <= 0 || doc.Total <= 0) return (doc.Total, doc.Total);
        return (Math.Round(doc.Total * c.VendaLiquida / c.ComImpostos, 2),
                Math.Round(doc.Total * c.ComPisCofins / c.ComImpostos, 2));
    }

    /// <summary>Linha da tabela "DIÁRIAS ADICIONAIS" impressa no fim da proposta.</summary>
    public sealed record DiariaAdicional(string Servico, string Obs, double Valor);

    /// <summary>
    /// Tabela "DIÁRIAS ADICIONAIS": os preços padrão derivados da diária normal
    /// apresentada (2º turno = 1,5×; sáb/dom/fer = 2×; HE semana = 1,5× a hora;
    /// HE sáb/dom/fer = 2× a hora). Sai SEMPRE na proposta, mesmo sem essas
    /// linhas lançadas — se o serviço estourar um sábado ou um 2º turno, o
    /// cliente já tem o preço fechado.
    /// </summary>
    public static List<DiariaAdicional> DiariasAdicionais(Documento apresentado)
    {
        // Sempre a DIÁRIA CHEIA: no modo com despesas abertas, é a diária
        // mostrada + as despesas do dia (hospedagem, carro, combustível,
        // refeições) — de volta aos mesmos 9.000 da diária fechada. As
        // derivadas (1,5× / 2×) saem dela.
        var d = Math.Round(DiariaNormalApresentada(apresentado) + apresentado.DescontoDia, 2);
        if (d <= 0) return new();
        var hora = Math.Round(d / 8.0, 2);

        // A linha do 2º turno só aparece se a proposta tiver diária de noite.
        var tem2Turno = apresentado.MO.Any(l => l.QtdDiaria > 0 && l.Horas >= 2 &&
            l.Mult is > 1.4 and < 1.6 &&
            (l.Servico + " " + l.Obs).ToUpperInvariant().Contains("TURNO"));

        var lista = new List<DiariaAdicional>
        {
            new("DIARIA ADICIONAL", "1o. TURNO — 8 HORAS (DIAS UTEIS DE SEG. A SEX.)", d),
        };
        if (tem2Turno)
            lista.Add(new("DIARIA ADICIONAL: 2o. TURNO NOITE", "", Math.Round(1.5 * d, 2)));
        lista.Add(new("DIARIA EXTRA SAB, DOM E FER", "", Math.Round(2 * d, 2)));
        lista.Add(new("HORA EXTRA SUP. 8h/DIA", "", Math.Round(1.5 * hora, 2)));
        lista.Add(new("HORA EXTRA SAB, DOM E FER", "", Math.Round(2 * hora, 2)));
        return lista;
    }

    /// <summary>Diária normal (1×, com impostos) como sai na proposta apresentada.</summary>
    public static double DiariaNormalApresentada(Documento apresentado)
    {
        var normal = apresentado.MO.FirstOrDefault(l => l.Mult is > 0.99 and < 1.01 && l.QtdDiaria > 0);
        if (normal is not null) return normal.ValorDiaria;
        var outra = apresentado.MO.FirstOrDefault(l => l.Mult > 0 && l.QtdDiaria > 0);
        if (outra is not null) return Math.Round(outra.ValorDiaria / outra.Mult, 2);

        // Proposta antiga sem multiplicadores gravados: re-identifica pelo
        // nome do serviço (senão a diária normal "sumiria" — e com ela a
        // tabela de diárias adicionais no modo sem despesas).
        foreach (var l in apresentado.MO)
        {
            if (l.QtdDiaria <= 0) continue;
            var m = Num(MultPeloNome(l.Servico, l.Obs));
            if (m > 0) return Math.Round(l.ValorDiaria / m, 2);
        }
        return 0;
    }

    /// <summary>
    /// "INFORMAÇÕES COMPLEMENTARES — NÃO INCLUSO": diária adicional e horas
    /// extras, na mesma regra da ClasseInfoComplementares do VBA.
    /// </summary>
    public static List<Complementar> Complementares(List<LinhaMO> mo, List<LinhaDespesa> desp)
    {
        // Diária adicional = diárias normais + despesas que se repetem por dia.
        var diaria = mo.Where(l => l.Servico.StartsWith("DIARIAS NORMAIS", StringComparison.OrdinalIgnoreCase))
                       .Sum(l => l.ValorDiaria);
        string[] porDia = { "HOSPEDAGEM", "LOCAÇÃO DE CARRO", "COMBUSTIVEL", "COMBUSTÍVEL", "REFEIÇÕES" };
        diaria += desp.Where(d => porDia.Contains(d.Despesa.Trim().ToUpperInvariant()))
                      .Sum(d => d.ValorUnitario);

        // Hora normal = diária do 1º turno ÷ 8.
        var primeiro = mo.FirstOrDefault(l =>
            l.Servico.StartsWith("DIARIAS NORMAIS", StringComparison.OrdinalIgnoreCase) &&
            l.Obs.Contains("1o", StringComparison.OrdinalIgnoreCase));
        var valorHora = primeiro is null ? 0 : primeiro.ValorDiaria / 8;

        return new List<Complementar>
        {
            new("DIARIA ADICIONAL", "DAS 8:00 AS 17:00 (DIAS UTEIS DE SEG. A SEX.)", 1, ParaCima(diaria)),
            new("HORA EXTRA", "SUPERIOR A 8/H DIA (DIAS UTEIS DE SEG. A SEX.)", 1, ParaCima(valorHora * 1.5)),
            new("HORA EXTRA", "SAB., DOM. E FERIADOS", 1, ParaCima(valorHora * 2)),
        };
    }
}
