namespace HowdenAxiais.Poc.Models;

/// <summary>
/// Proposta de serviço — equivale a uma pasta inteira da planilha antiga
/// (guias CUSTO + PRICING + PROPOSTA). Os campos são texto porque a camada
/// de dados (Parquet) grava tudo como VARCHAR; as conversões ficam no
/// motor de cálculo (<see cref="Data.Pricing"/>).
/// </summary>
public class Proposta
{
    public string Id { get; set; } = "";

    // ---- cadastro do cliente (guia PROPOSTA, coluna C) ----
    public string Cliente { get; set; } = "";
    public string Cidade { get; set; } = "";
    public string ContatoNome { get; set; } = "";
    public string ContatoEmail { get; set; } = "";
    public string ContatoTelefone { get; set; } = "";
    public string Projeto { get; set; } = "";
    public string Referencia { get; set; } = "";

    // ---- cabeçalho / identificação ----
    public string Numero { get; set; } = "";           // ex.: HCHLROU.AFM.000048
    public string Revisao { get; set; } = "0";
    public string Ano { get; set; } = DateTime.Today.Year.ToString();
    public string Data { get; set; } = DateTime.Today.ToString("yyyy-MM-dd");
    public string Bu { get; set; } = "HSA-SP";
    public string Idioma { get; set; } = "Português";
    public string Moeda { get; set; } = "BRL";
    public string ValidadeDias { get; set; } = "30";
    /// <summary>Sempre nasce em branco — quem monta a proposta digita o prazo.</summary>
    public string PrazoEntregaDias { get; set; } = "";
    public string PreparadaPor { get; set; } = "";
    public string RevisadaPor { get; set; } = "";
    public string Representante { get; set; } = "-";
    public string Representante2 { get; set; } = "-";
    public string Estado { get; set; } = "SP";
    /// <summary>Escopo do serviço, impresso ao lado de "ASSESSORIA TÉCNICA -".</summary>
    public string EscopoServico { get; set; } = "";
    /// <summary>
    /// Escopo detalhado (texto livre, digitado à mão): sai na proposta antes
    /// das notas — e só aparece se tiver algo escrito.
    /// </summary>
    public string EscopoDetalhado { get; set; } = "";
    /// <summary>
    /// Chaves das notas DESMARCADAS no tic (separadas por ";"): essas não saem
    /// no documento. Vazio = todas as notas automáticas saem.
    /// </summary>
    public string NotasDesmarcadas { get; set; } = "";
    public string Segmento { get; set; } = "Service";
    public string MarketSegment { get; set; } = "Mining";
    public string VendaPara { get; set; } = "Cliente Final";
    public string Destino { get; set; } = "Nacional";

    // ---- contato que assina o documento (cabeçalho impresso) ----
    public string AssinaNome { get; set; } = "";
    public string AssinaCargo { get; set; } = "";
    public string AssinaEmail { get; set; } = "";
    public string AssinaFones { get; set; } = "";

    // ---- conteúdo serializado ----
    public string ItensMoJson { get; set; } = "";
    public string ItensDespesaJson { get; set; } = "";
    public string PricingJson { get; set; } = "";

    // ---- resumo gravado (para as listagens) ----
    public string CustoTotal { get; set; } = "0";
    public string Total { get; set; } = "0";
    public string CriadaEm { get; set; } = "";
    public string Status { get; set; } = "Rascunho";

    /// <summary>
    /// Como o documento sai para o cliente: "ComDespesas" (despesas a custo +
    /// taxa administrativa, diferença embutida nas diárias) ou "SemDespesas"
    /// (despesas totalmente embutidas nas diárias).
    /// </summary>
    public string ModoApresentacao { get; set; } = "ComDespesas";
}

/// <summary>Item de mão de obra (guia CUSTO, linhas 8–17).</summary>
public class ItemMO
{
    public string Servico { get; set; } = "";
    public string Obs { get; set; } = "";
    public string Horas { get; set; } = "8";
    public string CustoHora { get; set; } = "0";
    public string QtdDiaria { get; set; } = "0";
    /// <summary>Quando verdadeiro, o custo é multiplicado pela quantidade de técnicos.</summary>
    public bool PorTecnico { get; set; } = true;
    /// <summary>
    /// Multiplicador sobre a hora-base (planilha: 2º turno = 1,5×; sáb/dom/fer = 2×).
    /// Zero ou vazio = valor independente, não acompanha a hora-base.
    /// </summary>
    public string Mult { get; set; } = "0";
}

/// <summary>Item de despesa (guia CUSTO, linhas 22–31).</summary>
public class ItemDespesa
{
    public string Despesa { get; set; } = "";
    public string Obs { get; set; } = "";
    public string Qtd { get; set; } = "0";
    public string CustoUnitario { get; set; } = "0";
    public bool PorTecnico { get; set; } = true;
}

/// <summary>Parâmetros da guia PRICING (riscos, provisões, comissões, margem e impostos).</summary>
public class PricingParams
{
    public string QtdTecnicos { get; set; } = "1";

    /// <summary>Hora-base do 1º turno (CUSTO E8): os itens com multiplicador acompanham este valor.</summary>
    public string HoraBase { get; set; } = "320";

    // riscos
    public string RiscoPct { get; set; } = "5";           // PRICING D37/E36 (risco de variação)

    // comissões (PRICING L36:T59 + BD_pricing) — % sobre a venda líquida
    public string PprPct { get; set; } = "1";             // PPR (código C01)
    public string SalesDirPct { get; set; } = "0.4";      // Sales Director (ELG)
    public string SalesIndPct { get; set; } = "0";        // Sales Industrial
    public string DsrFatorPct { get; set; } = "64.11";    // encargos DSR sobre as comissões internas de Sales
    public string Rep1Pct { get; set; } = "0";            // comissão do representante 1 (vem do cadastro)
    public string Rep2Pct { get; set; } = "0";            // comissão do representante 2

    // provisões sobre a venda líquida
    public string MargemNegociacaoPct { get; set; } = "3";// PRICING D47
    public string PmSachPct { get; set; } = "1";          // PRICING D48
    public string GarantiaProjetoPct { get; set; } = "2"; // PRICING D49
    public string PortalPct { get; set; } = "0";          // PRICING D50 (0,7% quando há portal)

    // seguro garantia / carta de fiança (PRICING M46:T59 + listas Custo_Garantia)
    public string FiancaTipo { get; set; } = "HSA C. Fiança (Banco)"; // padrão da planilha (P3 = Sim, 5% a.a.)
    public string FiancaPctVenda { get; set; } = "30";    // % da venda coberta (evento de pagamento N49)
    public string FiancaDias { get; set; } = "";          // vazio = acompanha o prazo de entrega (R49)

    // margem alvo (Project Margin) — "montar preço a partir da margem"
    public string MargemAlvoPct { get; set; } = "50";     // padrão do segmento Service (BD_pricing)

    // impostos (serviço: PIS, COFINS e ISS)
    public string PisPct { get; set; } = "1.65";
    public string CofinsPct { get; set; } = "7.6";
    public string IssPct { get; set; } = "7";             // 2% Itatiba + 5% outro município

    /// <summary>Taxa administrativa mostrada sobre as despesas na apresentação ao cliente.</summary>
    public string TaxaAdmPct { get; set; } = "40";

    // ---- ferramentas de preço ----
    /// <summary>Valor total da proposta anterior (renovação/recotação).</summary>
    public string PropAnteriorValor { get; set; } = "";
    /// <summary>% a mais desejado sobre a proposta anterior.</summary>
    public string PropAnteriorPct { get; set; } = "5";
    /// <summary>Meta de valor final: o sistema mostra a margem resultante.</summary>
    public string MetaValor { get; set; } = "";
    /// <summary>Diária normal cravada pela ferramenta de proposta anterior (ajuste cosmético de centavos).</summary>
    public string DiariaTravada { get; set; } = "";
    /// <summary>Total cravado pela ferramenta de meta de valor (apara o resíduo de arredondamento).</summary>
    public string TotalTravado { get; set; } = "";
}
