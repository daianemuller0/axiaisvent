using System.Globalization;
using ClosedXML.Excel;

namespace HowdenAxiais.Poc.Data;

/// <summary>
/// A planilha de custo previsto: o que a equipe de suprimentos usa para comprar
/// as peças e acompanhar o gasto do projeto.
///
/// Uma guia só, <see cref="Aba"/>, e uma linha por peça de cada equipamento da
/// proposta — o equipamento, cada item escolhido no escopo, o motor, o partidor
/// e a instrumentação. O arquivo se chama <c>&lt;projeto&gt;_CUSTO_PREVISTO.xlsx</c>.
///
/// <list type="bullet">
/// <item>PROJETO: o número do projeto pedido na hora de gerar, com o número do
/// equipamento atrás (<c>NB0982-01</c>, <c>NB0982-02</c>…) — mesmo com um
/// equipamento só;</item>
/// <item>TIPO DE CUSTO: sempre <see cref="TipoDeCusto"/>;</item>
/// <item>NOME_PRODUTO: o nome do item na seleção do programa; MATERIAL fica em
/// branco por enquanto;</item>
/// <item>QUANTIDADE: a do equipamento na proposta; CUSTO TOTAL: o custo da
/// peça (o do cadastro ou o digitado à mão) vezes essa quantidade, na moeda da
/// proposta — em branco quando a peça ainda não tem custo;</item>
/// <item>os seis meses: o mês em que a planilha foi gerada e os cinco
/// seguintes, em branco, para o controle do projeto;</item>
/// <item>PEDIDO: o número do pedido pedido na hora de gerar.</item>
/// </list>
///
/// CUSTO REALIZADO, TOTAL GASTO, TOTAL NO CUSTO e FORA DO CUSTO ficam em
/// branco — são de quem acompanha as compras. A coluna TOTAL soma os meses.
/// </summary>
public static class CustoPrevistoExcel
{
    public const string Aba = "CUSTO_PREVISTO";
    public const string TipoDeCusto = "MP+MO";

    /// <summary>Quantos meses a planilha mostra: o de hoje e os cinco seguintes.</summary>
    private const int Meses = 6;

    private static readonly CultureInfo Pt = new("pt-BR");

    public static string NomeDoArquivo(string projeto)
    {
        var limpo = string.Concat(projeto.Trim()
            .Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '-' : c));

        return $"{limpo}_CUSTO_PREVISTO.xlsx";
    }

    /// <summary>O número do projeto com o do equipamento: NB0982-01, NB0982-02…</summary>
    public static string CodigoDaLinha(string projeto, int equipamento) =>
        $"{projeto.Trim()}-{equipamento + 1:00}";

    /// <summary>Os cabeçalhos, na ordem das colunas — os meses dependem de quando se gera.</summary>
    public static List<string> Cabecalhos(DateTime hoje)
    {
        var cabecalhos = new List<string>
        {
            "PROJETO", "TIPO DE CUSTO", "NOME_PRODUTO", "MATERIAL", "QUANTIDADE",
            "CUSTO TOTAL", "CUSTO REALIZADO",
        };

        var primeiro = new DateTime(hoje.Year, hoje.Month, 1);
        for (var i = 0; i < Meses; i++)
            cabecalhos.Add(Pt.DateTimeFormat.GetMonthName(primeiro.AddMonths(i).Month).ToUpperInvariant());

        cabecalhos.AddRange(new[] { "TOTAL", "TOTAL GASTO", "TOTAL NO CUSTO", "FORA DO CUSTO", "PEDIDO" });
        return cabecalhos;
    }

    /// <summary>As peças de um equipamento: o nome e o custo UNITÁRIO de cada uma.</summary>
    public static List<(string Nome, decimal? Custo)> Pecas(
        ItemProposta item, CustoDaProposta custo, Moeda moeda)
    {
        var textos = TextosDoEscopo.Do(IdiomaDaProposta.Portugues);
        var pecas = new List<(string, decimal?)>();

        var modelo = custo.Modelo(item);
        pecas.Add((modelo?.Rotulo ?? "Ventilador",
            CustoDaProposta.Efetivo(item, "equipamento", custo.PrecoDoModelo(item, moeda))));

        foreach (var linha in custo.Itens(item, moeda))
            pecas.Add((EscopoDaHowden.Nome(linha.Lista, linha.Opcao, textos),
                CustoDaProposta.Efetivo(item, $"lista:{linha.Lista}", linha.Valor)));

        if (item.ComMotor)
        {
            var motor = custo.MotorDe(item);
            pecas.Add((motor is null ? "Motor elétrico" : $"Motor elétrico — {motor.Descricao}",
                CustoDaProposta.Efetivo(item, "motor", custo.PrecoDoMotor(item, moeda))));
        }

        if (custo.LinhaDoPartidor(item, moeda) is { } partidor)
            pecas.Add((partidor.Opcao.Trim(),
                CustoDaProposta.Efetivo(item, "partidor", partidor.Valor)));

        if (item.ComInstrumentacao)
        {
            foreach (var nome in item.Instrumentacao.Where(n => n.Trim().Length > 0))
                pecas.Add((nome.Trim(), CustoDaProposta.Efetivo(item, $"instr:{nome}",
                    custo.Resolver(item, custo.ListaDaInstrumentacao, nome, moeda).Valor)));
        }

        return pecas;
    }

    public static byte[] Gerar(Proposta p, CustoDaProposta custo, string projeto, string pedido,
        DateTime hoje)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add(Aba);

        var cabecalhos = Cabecalhos(hoje);
        for (var c = 0; c < cabecalhos.Count; c++) ws.Cell(1, c + 1).Value = cabecalhos[c];

        const int colQuantidade = 5, colCustoTotal = 6, primeiroMes = 8;
        var ultimoMes = primeiroMes + Meses - 1;
        var colTotal = ultimoMes + 1;
        var colPedido = cabecalhos.Count;

        var linha = 2;
        for (var e = 0; e < p.Itens.Count; e++)
        {
            var item = p.Itens[e];

            foreach (var (nome, unitario) in Pecas(item, custo, p.Moeda))
            {
                ws.Cell(linha, 1).Value = CodigoDaLinha(projeto, e);
                ws.Cell(linha, 2).Value = TipoDeCusto;
                ws.Cell(linha, 3).Value = nome;
                ws.Cell(linha, colQuantidade).Value = item.Quantos;
                if (unitario is { } u) ws.Cell(linha, colCustoTotal).Value = u * item.Quantos;

                ws.Cell(linha, colTotal).FormulaA1 =
                    $"SUM({ws.Cell(linha, primeiroMes).Address}:{ws.Cell(linha, ultimoMes).Address})";
                ws.Cell(linha, colPedido).Value = pedido.Trim();

                linha++;
            }
        }

        // aparência: cabeçalho em destaque, valores com duas casas, colunas legíveis
        var cab = ws.Range(1, 1, 1, cabecalhos.Count);
        cab.Style.Font.Bold = true;
        cab.Style.Fill.BackgroundColor = XLColor.FromHtml("#DCE6F1");
        cab.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        cab.Style.Alignment.WrapText = true;

        if (linha > 2)
        {
            ws.Range(2, colCustoTotal, linha - 1, colCustoTotal).Style.NumberFormat.Format = "#,##0.00";
            ws.Range(2, primeiroMes, linha - 1, colTotal).Style.NumberFormat.Format = "#,##0.00";
        }

        var tabela = ws.Range(1, 1, Math.Max(linha - 1, 1), cabecalhos.Count);
        tabela.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        tabela.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

        ws.Column(1).Width = 14;
        ws.Column(2).Width = 14;
        ws.Column(3).Width = 48;
        ws.Column(4).Width = 14;
        ws.Column(5).Width = 12;
        for (var c = colCustoTotal; c <= cabecalhos.Count; c++) ws.Column(c).Width = 15;

        ws.SheetView.FreezeRows(1);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}
