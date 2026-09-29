using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace HowdenAxiais.Poc.Data;

/// <summary>
/// Gera a proposta comercial em Word, no formato do modelo em espanhol da
/// equipe (P_HSAXYZ0000-0 - ESP_AXIAL_UG).
///
/// <para>
/// O documento é montado do zero. O caminho melhor — e o que está combinado
/// para quando o arquivo chegar — é PREENCHER o .docx da equipe, como
/// <see cref="PricingExcel"/> faz com a planilha: assim vêm a capa, os logos,
/// o cabeçalho de cada página e os estilos de vocês, que uma impressão em PDF
/// não devolve. O que decide o conteúdo (quem preenche o quê, de onde sai cada
/// número) está separado em <see cref="TextosDaProposta"/> e no cálculo abaixo,
/// e não muda quando a troca acontecer.
/// </para>
///
/// <para>
/// Os preços saem do PRICING, e não do custo: é o preço de venda, já com
/// comissões e margem, e o total da tabela é o mesmo número que a tela mostra
/// e que a lista de propostas mostra — os três chamam
/// <see cref="CalculoPricing.Da"/>.
/// </para>
/// </summary>
public static class PropostaWord
{
    private static readonly System.Globalization.CultureInfo Br = new("pt-BR");

    /// <summary>O nome do arquivo, no padrão da equipe: P_PRP-2026-001-00.docx</summary>
    public static string NomeDoArquivo(Proposta p) => $"{Referencia(p)}.docx";

    /// <summary>A referência da proposta, que é o rodapé e o nome do arquivo.</summary>
    public static string Referencia(Proposta p) =>
        $"P_{Ou(p.Numero, "SEM-NUMERO")}-{Ou(p.Revisao, "00")}";

    /// <summary>
    /// A oferta técnica que o preço referencia. No modelo é "XXXXXX-TX"; aqui
    /// é a referência desta proposta com o -T de técnica.
    /// </summary>
    public static string ReferenciaTecnica(Proposta p) => Referencia(p) + "-T";

    public static byte[] Gerar(Proposta p, CustoDaProposta custo)
    {
        var moeda = p.Moeda;
        var pricing = CalculoPricing.Da(p, custo.Total(p));
        var precos = PrecoPorItem(p, custo, pricing.VendaLiquida);

        using var fluxo = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(fluxo, WordprocessingDocumentType.Document))
        {
            var principal = doc.AddMainDocumentPart();
            principal.Document = new Document(new Body());
            var corpo = principal.Document.Body!;

            Capa(corpo, p);
            Introducao(corpo, p);
            OfertaComercial(corpo, p, custo, precos, pricing, moeda);

            corpo.Append(Secao(principal, p));
            principal.Document.Save();
        }

        return fluxo.ToArray();
    }

    // ---------------- páginas ----------------

    private static void Capa(Body corpo, Proposta p)
    {
        corpo.Append(Titulo("Howden South America", 32));
        corpo.Append(Titulo("Oferta Técnica Comercial", 26));
        corpo.Append(Vazio());

        corpo.Append(DuasColunas(new[]
        {
            ("Cliente:", p.Cliente, "Proyecto:", p.Projeto),
            ("Al cuidado de:", p.AosCuidados, "Nuestra referencia:", Referencia(p)),
            ("Ciudad:", p.Cidade, "Fecha:", Hoje()),
            ("E-mail:", p.Email, "Preparada por:", p.PreparadaPor),
            ("Fono:", p.Telefone, "", ""),
            ("Su referencia:", p.ReferenciaCliente, "", ""),
        }));

        corpo.Append(Vazio());
        corpo.Append(Titulo("Contactos Howden", 22));
        corpo.Append(Vazio());

        foreach (var linha in ListasDaProposta.EnderecoDaBu(p.Bu).Split('\n'))
            corpo.Append(Paragrafo(linha));

        corpo.Append(Vazio());
        corpo.Append(Paragrafo("Web: Howden | Chart Industries"));
        corpo.Append(Vazio());

        foreach (var contato in Contatos(p)) Contato(corpo, contato);

        Contato(corpo, ListasDaProposta.DiretorDeVendas);

        // o contato do representante vai embaixo do quadro, como no modelo
        if (p.ContatoDoRepresentante() is { Length: > 0 } representante)
        {
            corpo.Append(Vazio());
            corpo.Append(Paragrafo($"{p.Representante} por: {representante}"));
        }

        corpo.Append(QuebraDePagina());
    }

    private static void Introducao(Body corpo, Proposta p)
    {
        corpo.Append(Titulo("1  Introducción", 26));
        corpo.Append(Vazio());

        corpo.Append(Paragrafo($"Estimado/a {p.AosCuidados},"));
        corpo.Append(Vazio());

        corpo.Append(Paragrafo(
            $"Howden tiene la satisfacción de presentar a {p.Cliente} su propuesta de diseño, " +
            $"suministro y servicios para ventilador de {Ou(p.MarketSegment, "aplicación")}."));
        corpo.Append(Vazio());

        corpo.Append(Paragrafo(TextosDaProposta.Introducao));
        corpo.Append(Vazio());

        corpo.Append(Paragrafo("NOTA:", negrito: true));
        corpo.Append(Paragrafo(TextosDaProposta.NotaKyc));
        corpo.Append(QuebraDePagina());
    }

    private static void OfertaComercial(Body corpo, Proposta p, CustoDaProposta custo,
        IReadOnlyList<decimal> precos, CalculoPricing.Resultado pricing, Moeda moeda)
    {
        corpo.Append(Titulo("2  Oferta Comercial", 26));
        corpo.Append(Vazio());

        // ---- PRECIO ----
        corpo.Append(Titulo("PRECIO", 22));
        corpo.Append(Paragrafo(
            $"Precios en acuerdo a descripción de la Oferta Técnica {ReferenciaTecnica(p)}."));

        corpo.Append(TabelaDePrecos(p, custo, precos, pricing, moeda));
        corpo.Append(Paragrafo(TextosDaProposta.AvisoDaDescricao, negrito: true));
        corpo.Append(Vazio());

        // ---- IMPUESTOS ----
        corpo.Append(Titulo("IMPUESTOS", 22));
        foreach (var linha in TextosDaProposta.Impuestos) corpo.Append(Marcador(linha));
        corpo.Append(Vazio());

        // ---- CONDICIONES DE PAGO ----
        corpo.Append(Titulo("CONDICIONES DE PAGO", 22));
        corpo.Append(Marcador(Ou(p.CondicaoPagamento, "— a definir —")));
        corpo.Append(Vazio());

        // ---- PLAZO DE ENTREGA ----
        corpo.Append(Titulo("PLAZO DE ENTREGA", 22));
        corpo.Append(Paragrafo(TextosDaProposta.PlazoDeEntrega(Ou(p.PrazoEntregaDias, "--"))));
        corpo.Append(QuebraDePagina());

        // ---- CONDICIONES DE ENTREGA ----
        corpo.Append(Titulo("CONDICIONES DE ENTREGA (INCOTERMS 2020)", 22));
        corpo.Append(Paragrafo(p.Incoterm.Length > 0
            ? ListasDaProposta.LinhaDoIncoterm(p.Incoterm, p.IncotermDestino)
            : "— a definir —"));
        corpo.Append(Vazio());

        if (ListasDaProposta.TextoDoArmado(p.Armado) is { Length: > 0 } armado)
            corpo.Append(Marcador(armado));

        corpo.Append(QuebraDePagina());

        // ---- VALIDEZ ----
        corpo.Append(Titulo("VALIDEZ", 22));
        corpo.Append(Paragrafo(TextosDaProposta.Validez(Ou(p.ValidadeDias, "15"))));
        corpo.Append(QuebraDePagina());

        // ---- NOTAS ----
        corpo.Append(Titulo("NOTAS", 22));

        var n = 0;
        foreach (var nota in TextosDaProposta.Notas)
            corpo.Append(Numerada(++n, string.Format(nota, Hoje())));

        corpo.Append(Numerada(++n, "Asesoría Técnica:"));
        foreach (var bloco in TextosDaProposta
                     .Asesoria(CustoDaProposta.Quantidade(p), p.DiasDeAssessoria, p.DiasDeAssessoriaEmCampo)
                     .Split("\n\n"))
        {
            corpo.Append(Paragrafo("“" + bloco + "”", recuo: 720));
        }

        corpo.Append(QuebraDePagina());

        // ---- ASESORÍA TÉCNICA DE CAMPO ----
        corpo.Append(Titulo("ASESORÍA TÉCNICA DE CAMPO (SOLO PARA VENTILADORES)", 22));
        corpo.Append(Paragrafo("Precios válidos para contratación con los ítems ofertados:"));

        var (util, feriado) = TextosDaProposta.Diaria(moeda);
        corpo.Append(Paragrafo(
            $"Precios día útil das 08:00 hasta 17:00: {Dinheiro(util, moeda)} sin imposto y sin expensas."));
        corpo.Append(Paragrafo(
            $"Precios día Sábados, Domingos y Feriados: {Dinheiro(feriado, moeda)} sin imposto y sin expensas."));
        corpo.Append(Vazio());

        corpo.Append(Paragrafo("Notas Generales:", negrito: true));
        foreach (var nota in TextosDaProposta.NotasGeraisDaAsesoria) corpo.Append(Marcador(nota));

        corpo.Append(Vazio());
        corpo.Append(Paragrafo(TextosDaProposta.ObsDaAsesoria));
        corpo.Append(Vazio());

        corpo.Append(Titulo("SISTEMA DE GESTIÓN INTEGRADO", 22));
        corpo.Append(Paragrafo(TextosDaProposta.SistemaDeGestion));
        corpo.Append(Vazio());

        corpo.Append(Titulo("REPUESTOS", 22));
        corpo.Append(Paragrafo(TextosDaProposta.Repuestos));
    }

    // ---------------- preço ----------------

    /// <summary>
    /// O preço de venda de cada item.
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
    public static List<decimal> PrecosDeVenda(Proposta p, CustoDaProposta custo) =>
        PrecoPorItem(p, custo, CalculoPricing.Da(p, custo.Total(p)).VendaLiquida);

    private static List<decimal> PrecoPorItem(Proposta p, CustoDaProposta custo, decimal venda)
    {
        var custos = p.Itens.Select(i => custo.Subtotal(i, p.Moeda)).ToList();
        var total = custos.Sum();

        if (custos.Count == 0) return new List<decimal>();

        // sem custo nenhum, divide igual: é o melhor palpite, e a tela já avisa
        // que falta preço
        var precos = total <= 0m
            ? custos.Select(_ => Math.Round(venda / custos.Count, 2)).ToList()
            : custos.Select(c => Math.Round(venda * c / total, 2)).ToList();

        precos[^1] += venda - precos.Sum();
        return precos;
    }

    private static Table TabelaDePrecos(Proposta p, CustoDaProposta custo,
        IReadOnlyList<decimal> precos, CalculoPricing.Resultado pricing, Moeda moeda)
    {
        var tabela = Tabela(new[] { 6, 6, 58, 15, 15 }, comBordas: true);

        tabela.Append(Linha(true,
            ("ITEM", 6), ("CTD", 6), ("PRODUCTO*", 58), ("VALOR NETO UNITARIO", 15),
            ("VALOR NETO TOTAL", 15)));

        for (var i = 0; i < p.Itens.Count; i++)
        {
            var item = p.Itens[i];
            var descricao = EscopoEmTexto.Efetivo(item, custo, moeda);
            var totalDoItem = precos[i];
            var unitario = item.Quantos > 0 ? totalDoItem / item.Quantos : totalDoItem;

            tabela.Append(Linha(false,
                ((i + 1).ToString("D2"), 6),
                (item.Quantos.ToString(), 6),
                (descricao, 58),
                (Dinheiro(unitario, moeda), 15),
                (Dinheiro(totalDoItem, moeda), 15)));
        }

        tabela.Append(Linha(true,
            ("", 6), ("", 6), ("TOTAL PRECIO NETO", 58),
            ("", 15), (Dinheiro(pricing.VendaLiquida, moeda), 15)));

        return tabela;
    }

    private static string Dinheiro(decimal valor, Moeda moeda) =>
        $"{moeda.Rotulo()} {valor.ToString("N2", Br)}";

    // ---------------- contatos ----------------

    /// <summary>Os contatos escolhidos, sem os vazios.</summary>
    private static IEnumerable<ListasDaProposta.ContatoHowden> Contatos(Proposta p)
    {
        if (p.ContatoNome.Trim().Length > 0)
            yield return new(p.ContatoNome, p.ContatoCargo, p.ContatoArea,
                p.ContatoTelefones, p.ContatoEmail);

        if (p.Contato2Nome.Trim().Length > 0)
            yield return new(p.Contato2Nome, p.Contato2Cargo, p.Contato2Area,
                p.Contato2Telefones, p.Contato2Email);
    }

    private static void Contato(Body corpo, ListasDaProposta.ContatoHowden c)
    {
        corpo.Append(Paragrafo(c.Nome, negrito: true, centro: true));
        if (c.Cargo.Length > 0) corpo.Append(Paragrafo(c.Cargo, centro: true));
        if (c.Area.Length > 0) corpo.Append(Paragrafo(c.Area, centro: true));
        if (c.Telefones.Length > 0) corpo.Append(Paragrafo(c.Telefones, centro: true));
        if (c.Email.Length > 0) corpo.Append(Paragrafo(c.Email, centro: true));
        corpo.Append(Vazio());
    }

    // ---------------- peças do Word ----------------

    private static string Hoje() => DateTime.Now.ToString("dd/MM/yyyy");

    private static string Ou(string valor, string padrao) =>
        valor.Trim().Length > 0 ? valor.Trim() : padrao;

    private static Paragraph Vazio() => new(new Run(new Text("")));

    private static Paragraph Paragrafo(string texto, bool negrito = false, bool centro = false,
        int recuo = 0)
    {
        var props = new ParagraphProperties(
            new SpacingBetweenLines { After = "60", Line = "259", LineRule = LineSpacingRuleValues.Auto });

        if (centro) props.Append(new Justification { Val = JustificationValues.Center });
        if (recuo > 0) props.Append(new Indentation { Left = recuo.ToString() });

        return new Paragraph(props, Texto(texto, negrito));
    }

    private static Paragraph Titulo(string texto, int tamanho)
    {
        var run = new Run(new RunProperties(
            new Bold(),
            new Color { Val = "1F3864" },
            new FontSize { Val = tamanho.ToString() }), new Text(texto) { Space = SpaceProcessingModeValues.Preserve });

        return new Paragraph(
            new ParagraphProperties(new SpacingBetweenLines { Before = "240", After = "120" }),
            run);
    }

    private static Paragraph Marcador(string texto) =>
        new(new ParagraphProperties(
                new SpacingBetweenLines { After = "60" },
                new Indentation { Left = "360", Hanging = "180" }),
            Texto("•  " + texto, false));

    private static Paragraph Numerada(int n, string texto) =>
        new(new ParagraphProperties(
                new SpacingBetweenLines { After = "120" },
                new Indentation { Left = "360", Hanging = "360" }),
            Texto($"{n}) {texto}", false));

    /// <summary>
    /// Um texto que pode ter quebras de linha. O Word não quebra sozinho no
    /// "\n": cada quebra é um &lt;w:br/&gt;, e é o que faz a descrição do item
    /// aparecer em linhas na célula da tabela.
    /// </summary>
    private static Run Texto(string texto, bool negrito)
    {
        var run = new Run();
        if (negrito) run.Append(new RunProperties(new Bold()));

        var primeiro = true;
        foreach (var linha in texto.Split('\n'))
        {
            if (!primeiro) run.Append(new Break());
            run.Append(new Text(linha) { Space = SpaceProcessingModeValues.Preserve });
            primeiro = false;
        }

        return run;
    }

    private static Paragraph QuebraDePagina() =>
        new(new Run(new Break { Type = BreakValues.Page }));

    /// <summary>O bloco do cabeçalho da capa: rótulo e valor, em duas colunas.</summary>
    private static Table DuasColunas((string, string, string, string)[] linhas)
    {
        var tabela = Tabela(new[] { 15, 35, 20, 30 }, comBordas: false);

        foreach (var (r1, v1, r2, v2) in linhas)
        {
            tabela.Append(new TableRow(
                Celula(r1, 15, negrito: true), Celula(v1, 35),
                Celula(r2, 20, negrito: true), Celula(v2, 30)));
        }

        return tabela;
    }

    private static TableRow Linha(bool cabecalho, params (string Texto, int Largura)[] celulas)
    {
        var linha = new TableRow();
        foreach (var (texto, largura) in celulas)
            linha.Append(Celula(texto, largura, negrito: cabecalho, fundo: cabecalho ? "1F3864" : null,
                branco: cabecalho));

        return linha;
    }

    private static TableCell Celula(string texto, int largura, bool negrito = false,
        string? fundo = null, bool branco = false)
    {
        var props = new TableCellProperties(
            new TableCellWidth { Width = (largura * 50).ToString(), Type = TableWidthUnitValues.Pct });

        if (fundo is not null)
            props.Append(new Shading { Fill = fundo, Val = ShadingPatternValues.Clear });

        var run = new Run();
        var estilo = new RunProperties();
        if (negrito) estilo.Append(new Bold());
        if (branco) estilo.Append(new Color { Val = "FFFFFF" });
        if (estilo.HasChildren) run.Append(estilo);

        var primeiro = true;
        foreach (var l in texto.Split('\n'))
        {
            if (!primeiro) run.Append(new Break());
            run.Append(new Text(l) { Space = SpaceProcessingModeValues.Preserve });
            primeiro = false;
        }

        return new TableCell(props, new Paragraph(
            new ParagraphProperties(new SpacingBetweenLines { After = "40" }), run));
    }

    /// <summary>
    /// Uma tabela com as larguras das colunas em porcentagem.
    ///
    /// A GRADE (w:tblGrid) não é enfeite: sem ela o arquivo é inválido, e o
    /// Word e o LibreOffice se recusam a abrir o documento inteiro.
    /// </summary>
    private static Table Tabela(int[] larguras, bool comBordas)
    {
        var props = new TableProperties(
            new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct });

        if (comBordas) props.Append(Bordas());

        var grade = new TableGrid();
        foreach (var largura in larguras)
            grade.Append(new GridColumn { Width = (LarguraDaPagina * largura / 100).ToString() });

        return new Table(props, grade);
    }

    /// <summary>A largura útil da página, em twips: A4 menos as duas margens.</summary>
    private const int LarguraDaPagina = 9638;

    /// <summary>
    /// As bordas da tabela. A ORDEM é parte do formato — topo, esquerda,
    /// baixo, direita, e só então as de dentro. Fora dessa ordem o arquivo é
    /// inválido, mesmo com todas as bordas presentes.
    /// </summary>
    private static TableBorders Bordas() => new(
        new TopBorder { Val = BorderValues.Single, Size = 4, Color = "8EA9DB" },
        new LeftBorder { Val = BorderValues.Single, Size = 4, Color = "8EA9DB" },
        new BottomBorder { Val = BorderValues.Single, Size = 4, Color = "8EA9DB" },
        new RightBorder { Val = BorderValues.Single, Size = 4, Color = "8EA9DB" },
        new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4, Color = "8EA9DB" },
        new InsideVerticalBorder { Val = BorderValues.Single, Size = 4, Color = "8EA9DB" });

    // ---------------- cabeçalho e rodapé de todas as páginas ----------------

    /// <summary>
    /// A seção do documento, que é onde moram o cabeçalho e o rodapé que se
    /// repetem em toda página — no modelo eles trazem o cliente, as duas
    /// referências e a data, e o rodapé traz a referência e "Página X de Y".
    /// </summary>
    private static SectionProperties Secao(MainDocumentPart principal, Proposta p)
    {
        var cabecalho = principal.AddNewPart<HeaderPart>();
        cabecalho.Header = new Header(DuasColunas(new[]
        {
            ("Cliente:", p.Cliente, "Nuestra Ref.:", Referencia(p)),
            ("Ciudad:", p.Cidade, "Su Ref.:", p.ReferenciaCliente),
            ("Proyecto:", p.Projeto, "Fecha:", Hoje()),
        }),
        // o Word não aceita um cabeçalho que termina em tabela: precisa de um
        // parágrafo depois dela
        new Paragraph());
        cabecalho.Header.Save();

        var rodape = principal.AddNewPart<FooterPart>();
        rodape.Footer = new Footer(new Paragraph(
            new ParagraphProperties(new Justification { Val = JustificationValues.Center }),
            new Run(new Text($"{Referencia(p)} - ESP_AXIAL_UG") { Space = SpaceProcessingModeValues.Preserve }),
            new Run(new Text("     Página ") { Space = SpaceProcessingModeValues.Preserve }),
            Campo("PAGE"),
            new Run(new Text(" de ") { Space = SpaceProcessingModeValues.Preserve }),
            Campo("NUMPAGES")));
        rodape.Footer.Save();

        return new SectionProperties(
            new HeaderReference { Type = HeaderFooterValues.Default, Id = principal.GetIdOfPart(cabecalho) },
            new FooterReference { Type = HeaderFooterValues.Default, Id = principal.GetIdOfPart(rodape) },
            new PageMargin { Top = 1134, Bottom = 1134, Left = 1134, Right = 1134, Header = 567, Footer = 567 });
    }

    /// <summary>Um campo do Word (PAGE, NUMPAGES) — é o Word que conta as páginas.</summary>
    private static Run Campo(string nome) => new(
        new FieldChar { FieldCharType = FieldCharValues.Begin },
        new FieldCode(" " + nome + " ") { Space = SpaceProcessingModeValues.Preserve },
        new FieldChar { FieldCharType = FieldCharValues.Separate },
        new Text("1"),
        new FieldChar { FieldCharType = FieldCharValues.End });
}
