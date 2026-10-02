using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace HowdenAxiais.Poc.Data;

/// <summary>
/// Gera a proposta comercial PREENCHENDO o modelo em Word da equipe
/// (<c>wwwroot/modelos/proposta-comercial.docx</c>), do mesmo jeito que
/// <see cref="PricingExcel"/> preenche a planilha de pricing: o arquivo que
/// sai é o de vocês — capa, logos, estilos, cabeçalho e rodapé —, e daqui só
/// entram os dados.
///
/// <para>
/// O modelo é escrito para ser preenchido À MÃO, e por isso traz alternativas
/// com a instrução de apagar o resto: quatro endereços de BU, quinze contatos,
/// seis incoterms, três blocos de assessoria, quatro moedas de diária, um
/// bloco de venda e um de aluguel. Gerar a proposta é escolher e APAGAR — é o
/// que este arquivo faz, na ordem em que a instrução da equipe pede.
/// </para>
///
/// <para>
/// A capa e o cabeçalho de todas as páginas não são texto solto: são
/// <i>content controls</i> ligados às propriedades do documento (Empresa,
/// Categoria, Título, Comentários, Palavras-chave, Endereço da Empresa e Data
/// de Publicação). Preencher é mexer nas duas pontas — a propriedade e o texto
/// que o Word mostra —, senão o documento abre com o campo cinza de exemplo.
/// </para>
/// </summary>
public static class PropostaWord
{
    public const string CaminhoDoModelo = "wwwroot/modelos/proposta-comercial.docx";

    private static readonly System.Globalization.CultureInfo Br = new("pt-BR");

    /// <summary>O nome do arquivo, no padrão da equipe: P_PRP-2026-001-00.docx</summary>
    public static string NomeDoArquivo(Proposta p) => $"{Referencia(p)}.docx";

    /// <summary>A referência da proposta — o que o modelo chama de "Nuestra referencia".</summary>
    public static string Referencia(Proposta p) =>
        $"P_{Ou(p.Numero, "SEM-NUMERO")}-{Ou(p.Revisao, "00")}";

    /// <summary>
    /// A oferta técnica que o preço referencia. No modelo é "XXXXXX-TX"; aqui
    /// é a referência desta proposta com o -T de técnica.
    /// </summary>
    public static string ReferenciaTecnica(Proposta p) => Referencia(p) + "-T";

    public static byte[] Gerar(Proposta p, CustoDaProposta custo, string caminhoDoModelo)
    {
        var fluxo = new MemoryStream();
        using (var modelo = File.OpenRead(caminhoDoModelo)) modelo.CopyTo(fluxo);
        fluxo.Position = 0;

        using (var doc = WordprocessingDocument.Open(fluxo, true))
        {
            new Preenchimento(doc, p, custo).Preencher();
        }

        return fluxo.ToArray();
    }

    /// <summary>
    /// O preço de venda de cada item — o mesmo rateio que o documento usa, para
    /// a tela não mostrar um número e o Word sair com outro.
    /// </summary>
    public static List<decimal> PrecosDeVenda(Proposta p, CustoDaProposta custo) =>
        Ratear(p, custo, CalculoPricing.Da(p, custo.Total(p)).VendaLiquida);

    /// <summary>
    /// O fator que leva o custo ao preço: a venda líquida da proposta dividida
    /// pelo custo fechado dela.
    ///
    /// É o "multiplicar o custo pelo fator" da equipe. Os opcionais ficam fora
    /// do preço fechado, então não há pricing próprio para eles — o que há é a
    /// mesma margem, as mesmas comissões e os mesmos impostos que a proposta
    /// já fechou, aplicados ao custo do opcional.
    /// </summary>
    public static decimal Fator(Proposta p, CustoDaProposta custo)
    {
        var total = custo.Total(p);
        if (total <= 0m) return 0m;

        return CalculoPricing.Da(p, total).VendaLiquida / total;
    }

    /// <summary>O preço de um opcional: o custo dele vezes o fator.</summary>
    public static decimal PrecoDoOpcional(Proposta p, CustoDaProposta custo, decimal custoDele) =>
        Math.Round(custoDele * Fator(p, custo), 2);

    /// <summary>
    /// Reparte o preço de venda entre os equipamentos.
    ///
    /// O pricing dá um número só para a proposta inteira — markup, comissões e
    /// impostos são da venda, e não de uma peça. Com mais de um equipamento,
    /// esse total é repartido na PROPORÇÃO DO CUSTO de cada um, que é o único
    /// rateio que não inventa margem diferente por item.
    ///
    /// A sobra do arredondamento vai para o último item, para a soma da coluna
    /// bater com o TOTAL PRECIO NETO — centavo de diferença numa proposta é
    /// pergunta de cliente.
    /// </summary>
    private static List<decimal> Ratear(Proposta p, CustoDaProposta custo, decimal venda)
    {
        var custos = p.Itens.Select(i => custo.Subtotal(i, p.Moeda)).ToList();
        if (custos.Count == 0) return new List<decimal>();

        var total = custos.Sum();

        // sem custo nenhum, divide igual: é o melhor palpite, e a tela já avisa
        // que falta preço
        var precos = total <= 0m
            ? custos.Select(_ => Math.Round(venda / custos.Count, 2)).ToList()
            : custos.Select(c => Math.Round(venda * c / total, 2)).ToList();

        precos[^1] += venda - precos.Sum();
        return precos;
    }

    internal static string Dinheiro(decimal valor, Moeda moeda) =>
        $"{moeda.Rotulo()} {valor.ToString("N2", Br)}";

    internal static string Ou(string valor, string padrao) =>
        valor.Trim().Length > 0 ? valor.Trim() : padrao;
}
