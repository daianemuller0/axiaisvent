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
        // a planilha da equipe é editada no pacote, sem regravar: assim as imagens,
        // os botões e as macros chegam como o modelo as tem
        using var pacote = new PacoteXlsm(caminhoDoModelo);
        var ws = pacote.AbaDe(Aba);

        // ---------- cabeçalho ----------
        ws.Texto("E3", proposta.Cliente);
        ws.Texto("E6", proposta.Numero);
        if (DadosExcel.Numero(proposta.Revisao) is { } rev) ws.Numero("E7", (double)rev);
        else ws.Texto("E7", proposta.Revisao);
        ws.Texto("E9", proposta.PreparadaPor);

        // sem estas três, a planilha calculava com o que estivesse no modelo —
        // e o preço dela saía diferente do da tela sem ninguém entender por quê
        ws.Texto("E8", ListasDaProposta.CodigoDaBu(proposta.Bu));
        ws.Texto("J3", Proposta.SegmentoDoPricing);
        // fora do Brasil o pricing não quer UF: quer "Chile", "Peru" ou "EXPORT"
        var estado = ListasDaProposta.EstadoDoPricing(proposta.Pais, proposta.Estado);
        ws.Texto("J6", estado.Length > 0 ? estado : "preencher");

        ws.Texto("J4", proposta.VendaPara);
        ws.Texto("J5", proposta.Destino);

        // a equipe pediu: reforma sempre "Não"
        ws.Texto("J7", "Não");

        // o prazo entra no custo da fiança (R48 conta os dias de cobertura)
        if (DadosExcel.Numero(proposta.PrazoEntregaDias) is { } prazo)
            ws.Numero("J8", (double)prazo);

        ws.Texto("J9", proposta.Beneficio.Length > 0 ? proposta.Beneficio : "Sem benefício");

        ws.Texto("P3", proposta.Fianca.Length > 0 ? proposta.Fianca : "Não");
        // na planilha o portal é Sim/Não (D49 usa isso); no sistema a equipe
        // escolhe QUAL portal, e "Nenhum" é o que vira "Não"
        ws.Texto("P4", Proposta.TemPortal(proposta.Portal) ? "Sim" : "Não");
        ws.Texto("P5", proposta.Representante);
        ws.Texto("P6", proposta.Representante2.Length > 0 ? proposta.Representante2 : "-");

        // padrões dos axiais
        ws.Texto("P7", "Axials");
        ws.Texto("P8", "PAXIAL4 - Axials Configured Fans");
        ws.Texto("P9", "Mining");

        // ---------- custo e pricing ----------
        // E33 é a soma das rubricas na planilha; como o custo vem inteiro do
        // sistema, ele é escrito no lugar da soma — e G33 recebe o mesmo valor,
        // senão o "risco de variação" (|G33 − E33|) viraria o custo inteiro
        // o pricing trabalha com duas casas: o custo entra arredondado
        ws.Numero("E33", (double)Math.Round(custoTotal, 2));
        ws.Numero("G33", (double)Math.Round(custoTotal, 2));

        ws.Numero("D36", (double)Proposta.Percentual(proposta.RiscoAdicional,
            CalculoPricing.RiscoAdicionalPadrao));
        ws.Numero("D46", (double)Proposta.Percentual(proposta.MargemNegociacao,
            CalculoPricing.MargemNegociacaoPadrao));

        ws.Numero("E40", (double)pricing.Markup);

        // a margem que o sistema está usando — a pedida na tela, e não a que o
        // modelo trazia (25%). Sem isto, mudar a margem aqui não mudava o arquivo
        ws.Numero("P25", (double)pricing.Margem);

        // ---------- moeda: a caixa "Moedas" da planilha ----------
        // A planilha calcula na moeda do custo e converte o preço para a moeda
        // escolhida com Q13 = venda (Q12) ÷ taxa da moeda. A escolha é o botão
        // marcado (e o número em BD_pricing!A1, a ligação dos botões: 1 = USD,
        // 2 = EUR, 3 = GBP, 4 = CLP) e as taxas são as células I35 (USD), I36
        // (EUR), I37 (GBP) e I38 (CLP). Vai a taxa EFETIVA do sistema (já com a
        // segurança da moeda) na linha da moeda do preço: a planilha converte
        // exatamente como a tela.
        var codigoDoCusto = proposta.Moeda.Codigo();
        var codigoDoPreco = proposta.MoedaVenda.Codigo();
        pacote.TrocarMoedaDosFormatos(codigoDoCusto);

        var (indice, taxa) = MoedaDaPlanilha(proposta);
        if (indice is int linha)
        {
            var celulaDaTaxa = linha switch { 1 => "I35", 2 => "I36", 3 => "I37", _ => "I38" };
            ws.Numero(celulaDaTaxa, (double)Math.Round(taxa, 6));
            // a taxa é "moeda do custo por 1 moeda do preço"
            ws.Formato(celulaDaTaxa, $"\"{codigoDoCusto}\" #,##0.0000");

            pacote.AbaDe("BD_pricing").Numero("A1", linha);
            pacote.MarcarMoeda(linha);

            // o bloco convertido (K13/Q13) só aparece em venda de exportação:
            // preço em outra moeda é, para a planilha, exatamente isso
            if (proposta.MoedaVenda != proposta.Moeda)
                ws.Texto("J5", "Exportação");

            // o valor convertido (Q13) saía com nove casas: fecha em uma
            ws.Arredondar("Q13", 1);
            ws.Formato("Q13", $"\"{codigoDoPreco}\" #,##0.0");
        }

        // a venda líquida está na moeda do custo
        ws.Formato("Q12", $"\"{codigoDoCusto}\" #,##0.0");
        ws.Formato("E51", $"\"{codigoDoCusto}\" #,##0.0");

        return pacote.Concluir();
    }

    /// <summary>
    /// A linha da moeda na caixa "Moedas" da planilha — USD 1, EUR 2, GBP 3,
    /// CLP 4 (a ordem de BD_pricing!A2:A5) — ou nada quando a planilha não tem
    /// a moeda (PEN e R$).
    /// </summary>
    public static int? IndiceDaMoeda(Moeda moeda) => moeda switch
    {
        Moeda.Usd => 1,
        Moeda.Eur => 2,
        Moeda.Clp => 4,
        _ => null,
    };

    /// <summary>
    /// Qual linha da caixa de moedas usar e com que taxa. Com conversão: a linha
    /// da moeda do preço, com a taxa efetiva. Sem conversão: a linha da moeda do
    /// custo, com taxa 1 — a planilha mostra o mesmo valor nas duas.
    /// </summary>
    public static (int? Indice, decimal Taxa) MoedaDaPlanilha(Proposta p) =>
        Cambio.Converte(p)
            ? (IndiceDaMoeda(p.MoedaVenda), Cambio.Efetivo(p))
            : (IndiceDaMoeda(p.Moeda), 1m);

    /// <summary>
    /// O nome do arquivo que a equipe baixa: P_PRP-2026-001-C00.xlsm — o mesmo
    /// da proposta comercial a que o pricing pertence (a extensão é que muda).
    /// </summary>
    public static string NomeDoArquivo(Proposta p) => $"{PropostaWord.Referencia(p)}.xlsm";
}
