using ClosedXML.Excel;

namespace HowdenAxiais.Poc.Data;

/// <summary>
/// Preenche a planilha de pricing da equipe com o que a proposta já sabe.
///
/// O arquivo gerado é a <b>planilha deles</b>, com as sete abas, as fórmulas e
/// as macros intactas — só as células de entrada são escritas. Isso é de
/// propósito: o Excel continua sendo a fonte da verdade do pricing, e o sistema
/// entra para não redigitar o que já está na proposta. Os números que a tela
/// mostra são a prévia da mesma conta.
/// </summary>
public static class PricingExcel
{
    /// <summary>Onde mora o modelo dentro do projeto.</summary>
    public const string CaminhoDoModelo = "wwwroot/modelos/pricing.xlsm";

    public const string Aba = "PRICING";

    /// <summary>
    /// O mapa de campos, como a equipe passou. Fica aqui em um lugar só porque
    /// é o contrato entre o sistema e a planilha — se ela mudar de linha, é
    /// esta tabela que muda.
    /// </summary>
    public static readonly (string Celula, string Campo)[] Mapa =
    {
        ("E3", "Cliente"), ("E6", "Número da proposta"), ("E7", "Revisão"),
        ("E8", "BU emissora"), ("E9", "Preparada por"),
        ("J3", "Segmento (NB nos axiais)"), ("J4", "Venda para"), ("J5", "Destino"),
        ("J6", "Estado"), ("J7", "Reforma (sempre Não)"), ("J8", "Prazo de entrega"),
        ("J9", "Benefícios"),
        ("P3", "Fiança/Seguro Garantia"), ("P4", "Portal"),
        ("P5", "Representante"), ("P6", "Representante 2"),
        ("P7", "Categories"), ("P8", "Product"), ("P9", "Market Segments"),
        ("E33", "Custo total"), ("G33", "Custo total (sem variação por rubrica)"),
        ("D36", "Risco adicional"), ("D46", "Margem de negociação"), ("E40", "Markup"),
    };

    /// <summary>Gera o arquivo, em memória, pronto para download.</summary>
    public static byte[] Gerar(Proposta proposta, decimal custoTotal,
        CalculoPricing.Resultado pricing, string caminhoDoModelo)
    {
        using var wb = new XLWorkbook(caminhoDoModelo);
        var ws = wb.Worksheet(Aba);

        // ---------- cabeçalho ----------
        ws.Cell("E3").Value = proposta.Cliente;
        ws.Cell("E6").Value = proposta.Numero;
        ws.Cell("E7").Value = DadosExcel.Numero(proposta.Revisao) is { } rev
            ? (XLCellValue)(double)rev
            : proposta.Revisao;
        ws.Cell("E9").Value = proposta.PreparadaPor;

        // sem estas três, a planilha calculava com o que estivesse no modelo —
        // e o preço dela saía diferente do da tela sem ninguém entender por quê
        ws.Cell("E8").Value = ListasDaProposta.CodigoDaBu(proposta.Bu);
        ws.Cell("J3").Value = Proposta.SegmentoDoPricing;
        // fora do Brasil o pricing não quer UF: quer "Chile", "Peru" ou "EXPORT"
        var estado = ListasDaProposta.EstadoDoPricing(proposta.Pais, proposta.Estado);
        ws.Cell("J6").Value = estado.Length > 0 ? estado : "preencher";

        ws.Cell("J4").Value = proposta.VendaPara;
        ws.Cell("J5").Value = proposta.Destino;

        // a equipe pediu: reforma sempre "Não"
        ws.Cell("J7").Value = "Não";

        // o prazo entra no custo da fiança (R48 conta os dias de cobertura)
        if (DadosExcel.Numero(proposta.PrazoEntregaDias) is { } prazo)
            ws.Cell("J8").Value = (double)prazo;

        ws.Cell("J9").Value = proposta.Beneficio.Length > 0 ? proposta.Beneficio : "Sem benefício";

        ws.Cell("P3").Value = proposta.Fianca.Length > 0 ? proposta.Fianca : "Não";
        // na planilha o portal é Sim/Não (D49 usa isso); no sistema a equipe
        // escolhe QUAL portal, e "Nenhum" é o que vira "Não"
        ws.Cell("P4").Value = Proposta.TemPortal(proposta.Portal) ? "Sim" : "Não";
        ws.Cell("P5").Value = proposta.Representante;
        ws.Cell("P6").Value = proposta.Representante2.Length > 0 ? proposta.Representante2 : "-";

        // padrões dos axiais
        ws.Cell("P7").Value = "Axials";
        ws.Cell("P8").Value = "PAXIAL4 - Axials Configured Fans";
        ws.Cell("P9").Value = "Mining";

        // ---------- custo e pricing ----------
        // E33 é a soma das rubricas na planilha; como o custo vem inteiro do
        // sistema, ele é escrito no lugar da soma — e G33 recebe o mesmo valor,
        // senão o "risco de variação" (|G33 − E33|) viraria o custo inteiro
        ws.Cell("E33").Value = (double)custoTotal;
        ws.Cell("G33").Value = (double)custoTotal;

        ws.Cell("D36").Value = (double)Proposta.Percentual(proposta.RiscoAdicional,
            CalculoPricing.RiscoAdicionalPadrao);
        ws.Cell("D46").Value = (double)Proposta.Percentual(proposta.MargemNegociacao,
            CalculoPricing.MargemNegociacaoPadrao);

        ws.Cell("E40").Value = (double)pricing.Markup;

        using var saida = new MemoryStream();
        wb.SaveAs(saida);
        return saida.ToArray();
    }

    /// <summary>O nome do arquivo que a equipe baixa.</summary>
    public static string NomeDoArquivo(Proposta p)
    {
        var numero = p.Numero.Length > 0 ? p.Numero : "proposta";
        var revisao = p.Revisao.Length > 0 ? $"-rev{p.Revisao}" : "";
        return $"pricing-{numero}{revisao}.xlsm";
    }
}
