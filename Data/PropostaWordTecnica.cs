using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Desenho = DocumentFormat.OpenXml.Drawing;
using DesenhoWord = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using Figura = DocumentFormat.OpenXml.Drawing.Pictures;

namespace HowdenAxiais.Poc.Data;

/// <summary>Uma curva lida da pasta de anexos, pronta para entrar no documento.</summary>
public sealed record ImagemDaCurva(byte[] Bytes, string Extensao);

/// <summary>
/// A parte TÉCNICA do documento.
///
/// A proposta técnica é o mesmo arquivo da comercial até a terceira página —
/// capa, índice e introdução —, e daí em diante troca de assunto: no lugar do
/// preço vêm os dados de cada ventilador e as curvas dele.
///
/// <para>
/// As páginas técnicas são ESCRITAS aqui, e não preenchidas, porque o modelo
/// que a equipe mandou não as tem: o índice dele cita "DATOS DEL VENTILADOR"
/// e "ALCANCE DEL SUMINISTRO", mas o corpo do arquivo vai da introdução ao
/// preço e termina nos repostos. O que dá para reaproveitar — e é reaproveitado
/// — são os ESTILOS do modelo: as tabelas usam a mesma faixa azul e as mesmas
/// fontes do resto do documento, e não uma aparência inventada aqui.
/// </para>
/// </summary>
internal sealed partial class Preenchimento
{
    /// <summary>A faixa azul dos cabeçalhos de tabela, copiada do modelo.</summary>
    private const string AzulDaFaixa = "00648C";

    private const int LarguraUtil = 9638;

    /// <summary>
    /// Escreve a proposta técnica no lugar da comercial: some tudo da "Oferta
    /// Comercial" para baixo, e entra um bloco por equipamento.
    /// </summary>
    public void Tecnica(Func<CurvaAnexada, ImagemDaCurva?> lerCurva)
    {
        _tecnica = true;

        Propriedades();
        CamposLigados();
        Capa();
        EnderecoDaBu();
        Contatos();
        Representante();
        Revisoes();
        Introducao();

        TrocarOComercialPeloTecnico(lerCurva);
        TirarRealce();

        _doc.MainDocumentPart!.Document.Save();
    }

    private void TrocarOComercialPeloTecnico(Func<CurvaAnexada, ImagemDaCurva?> lerCurva)
    {
        var titulo = Achar("Oferta Comercial");
        if (titulo is null) return;

        // tudo o que vem depois do título é da proposta comercial e sai; o
        // sectPr é o fim da seção e fica, senão o documento perde as margens,
        // o cabeçalho e o rodapé
        foreach (var elemento in _corpo.ChildElements
                     .SkipWhile(e => e != titulo)
                     .Skip(1)
                     .Where(e => e is not SectionProperties)
                     .ToList())
        {
            elemento.Remove();
        }

        Escrever(titulo, _t.OfertaTecnica);

        var fim = _corpo.GetFirstChild<SectionProperties>();
        var varios = _p.Itens.Count > 1;

        for (var i = 0; i < _p.Itens.Count; i++)
        {
            if (i > 0) Antes(fim, QuebraDePagina());
            Equipamento(fim, _p.Itens[i], i, varios, lerCurva);
        }

        Alcance(fim, varios);
        Informacoes(fim);
    }

    /// <summary>
    /// "Alcance de Suministro": o que vai, a documentação, o que não vai, e o
    /// texto do ventilador.
    ///
    /// A ordem é a do modelo. A documentação fica entre o incluso e o excluído
    /// porque ela É parte do que a Howden entrega, e os comentários ficam
    /// depois do excluído porque são mais exclusões — as que valem para
    /// qualquer proposta. Com mais de um equipamento, as listas do incluso
    /// saem uma por ventilador, e as do excluído também; a documentação e os
    /// comentários são da proposta e saem uma vez.
    /// </summary>
    private void Alcance(OpenXmlElement? fim, bool varios)
    {
        Antes(fim, QuebraDePagina());
        Antes(fim, TituloDeSecao(_te.Secao));

        for (var i = 0; i < _p.Itens.Count; i++)
        {
            Antes(fim, Titulo(PorVentilador(_te.Incluso, i, varios)));
            foreach (var linha in EscopoDaHowden.Efetivo(_p.Itens[i], _custo, _moeda, _te))
                Antes(fim, Marcador(linha));
        }

        Antes(fim, Titulo(_te.Documentacao));
        foreach (var linha in _te.LinhasDaDocumentacao) Antes(fim, Marcador(linha));
        Antes(fim, Paragrafo(_te.NotaDaDocumentacao));

        // sem nada escrito, o título do excluído sairia com a lista vazia
        // embaixo — e uma lista vazia num contrato é pior que seção nenhuma
        for (var i = 0; i < _p.Itens.Count; i++)
        {
            var linhas = EscopoDaHowden.Linhas(_p.Itens[i].EscopoExcluido);
            if (linhas.Count == 0) continue;

            Antes(fim, Titulo(PorVentilador(_te.Excluido, i, varios)));
            foreach (var linha in linhas) Antes(fim, Marcador(linha));
        }

        Antes(fim, Titulo(_te.Comentarios));
        Antes(fim, Paragrafo(_te.AberturaDosComentarios));
        foreach (var linha in _te.LinhasDosComentarios) Antes(fim, Marcador(linha));

        Ventiladores(fim);
        Componentes(fim);
    }

    /// <summary>
    /// As notas de componente, embaixo do texto do ventilador. Saem em toda
    /// proposta, sem olhar o escopo — ver <see cref="NotasDosComponentes"/>.
    /// </summary>
    private void Componentes(OpenXmlElement? fim)
    {
        foreach (var nota in _notas)
        {
            Antes(fim, Titulo(nota.Titulo));
            CorpoDaNota(fim, nota.Linhas);
        }
    }

    /// <summary>
    /// O corpo de uma nota, lendo a convenção de
    /// <see cref="NotasDosComponentes"/>: "|" é linha de tabela, "-" é item de
    /// lista, "--" é item de segundo nível e o resto é parágrafo.
    ///
    /// As linhas de tabela são juntadas até aparecer algo que não é tabela —
    /// é assim que a nota do motor consegue ter duas tabelas separadas por
    /// parágrafos sem precisar numerá-las.
    /// </summary>
    private void CorpoDaNota(OpenXmlElement? fim, string[] linhas)
    {
        var juntando = new List<string[]>();

        void Fechar()
        {
            if (juntando.Count == 0) return;

            Antes(fim, TabelaDaNota(juntando));
            Antes(fim, Vazio());
            juntando.Clear();
        }

        foreach (var linha in linhas)
        {
            if (linha.StartsWith('|'))
            {
                juntando.Add(linha[1..].Split('|').Select(c => c.Trim()).ToArray());
                continue;
            }

            Fechar();

            if (linha.StartsWith("-- ")) Antes(fim, Marcador(linha[3..].Trim(), nivel: 2));
            else if (linha.StartsWith("- ")) Antes(fim, Marcador(linha[2..].Trim()));
            else Antes(fim, Paragrafo(linha));
        }

        Fechar();
    }

    /// <summary>
    /// Uma tabela de dentro de uma nota. A primeira linha é o cabeçalho — é
    /// ela que sai na faixa azul e que diz quantas colunas a tabela tem.
    /// </summary>
    private static Table TabelaDaNota(List<string[]> linhas)
    {
        var colunas = linhas[0].Length;

        var quadros = linhas
            .Select((celulas, i) => Enumerable.Range(0, colunas)
                .Select(c => new Quadro(c < celulas.Length ? celulas[c] : "", Faixa: i == 0))
                .ToList())
            .ToList();

        return Tabela(quadros, colunas);
    }

    /// <summary>
    /// O texto do ventilador, escolhido pela série do equipamento: o do VAX ou
    /// o do Joy. Numa proposta com equipamentos das duas linhas saem os dois,
    /// na ordem em que aparecem no escopo, e nunca o mesmo duas vezes.
    /// </summary>
    private void Ventiladores(OpenXmlElement? fim)
    {
        var jaSaiu = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in _p.Itens)
        {
            var serie = _custo.SerieDe(item).Trim();
            if (serie.Length == 0 || !jaSaiu.Add(serie)) continue;

            var vax = Textos.Simples(serie).Contains("vax");

            Antes(fim, Titulo(vax ? _te.TituloVax : _te.TituloJoy));
            foreach (var paragrafo in vax ? _te.TextoVax : _te.TextoJoy)
                Antes(fim, Paragrafo(paragrafo));
        }
    }

    /// <summary>
    /// "Informaciones Adicionales": os anexos, os documentos do cliente e as
    /// normas. É tudo da proposta, e não do equipamento, então sai uma vez, em
    /// página própria como no modelo.
    /// </summary>
    private void Informacoes(OpenXmlElement? fim)
    {
        Antes(fim, QuebraDePagina());
        Antes(fim, TituloDeSecao(_te.Informacoes));

        Antes(fim, Titulo(_te.Anexos));
        Antes(fim, Paragrafo(_te.AberturaDosAnexos));
        foreach (var linha in _te.LinhasDosAnexos) Antes(fim, Marcador(linha));

        Antes(fim, Paragrafo(_te.DocumentosDoCliente, negrito: true));
        foreach (var linha in _te.LinhasDosDocumentosDoCliente) Antes(fim, Marcador(linha));

        Antes(fim, Titulo(_te.Estandares));
        Antes(fim, Paragrafo(_te.AberturaDosEstandares));
        foreach (var linha in _te.LinhasDosEstandares) Antes(fim, Marcador(linha));
    }

    /// <summary>
    /// O cabeçalho de um bloco que existe por equipamento. Com um só, o nome
    /// seco; com vários, o nome e qual ventilador — igual ao bloco dos dados.
    /// </summary>
    private string PorVentilador(string nome, int indice, bool varios) =>
        varios ? $"{nome} — {_t.Ventilador} {indice + 1:D2}" : nome;

    /// <summary>Um equipamento: os dados, os materiais, as normas e as curvas.</summary>
    private void Equipamento(OpenXmlElement? fim, ItemProposta item, int indice, bool varios,
        Func<CurvaAnexada, ImagemDaCurva?> lerCurva)
    {
        var cabecalho = varios
            ? $"{_t.DadosDoVentilador} — {_t.Ventilador} {indice + 1:D2}"
            : _t.DadosDoVentilador;

        Antes(fim, Titulo(cabecalho));
        Antes(fim, Tabela(DuasColunas(
            (_t.CaracteristicasGerais, _t.ReferenciaDoCliente),
            DadosDoVentilador.De(item, _custo.Modelo(item), _custo.MotorDe(item), _t)
                .Select(l => (l.Rotulo, l.Valor))
                .ToList())));

        // nestas duas a faixa azul JÁ é o nome da seção, como no modelo — um
        // título por cima dela sairia escrito duas vezes
        Antes(fim, Vazio());
        Antes(fim, Tabela(DuasColunas((_t.Materiais, ""),
            _t.LinhasDosMateriais.Select(l => (l[0], l[1])).ToList())));

        Antes(fim, Vazio());
        Antes(fim, Tabela(UmaColuna(_t.Normas, _t.LinhasDasNormas)));

        // as curvas ficam na página seguinte à dos dados, como a equipe pediu
        if (item.Curvas.Count > 0)
        {
            Antes(fim, QuebraDePagina());
            Antes(fim, Titulo(_t.CurvaDePerformance));

            foreach (var curva in item.Curvas)
            {
                if (lerCurva(curva) is not { } arquivo) continue;
                Antes(fim, Imagem(arquivo));
            }
        }

        Eletrica(fim, item, indice, varios);
    }

    /// <summary>
    /// A parte elétrica: a tabela do motor e, embaixo dela, a do quadro do
    /// partidor. Vai em página própria, depois da curva.
    ///
    /// Quem decide se ela existe é o escopo, não esta função: sem motor,
    /// <see cref="DadosEletricos.De"/> devolve vazio e a página não é criada —
    /// nem a quebra que a abriria, que é o que faria sobrar uma folha em
    /// branco no fim do bloco do equipamento.
    /// </summary>
    private void Eletrica(OpenXmlElement? fim, ItemProposta item, int indice, bool varios)
    {
        var quadros = DadosEletricos.De(item, _custo, _moeda, _tel);
        if (quadros.Count == 0) return;

        Antes(fim, QuebraDePagina());
        Antes(fim, Titulo(PorVentilador(_tel.Secao, indice, varios)));

        for (var i = 0; i < quadros.Count; i++)
        {
            // a faixa azul já é o nome da tabela, como no modelo
            if (i > 0) Antes(fim, Vazio());
            Antes(fim, Tabela(UmaColuna(quadros[i].Titulo, quadros[i].Linhas)));
        }
    }

    // ================= peças =================

    private void Antes(OpenXmlElement? fim, OpenXmlElement novo)
    {
        if (fim is null) _corpo.AppendChild(novo);
        else _corpo.InsertBefore(novo, fim);
    }

    /// <summary>Um título de seção, com o estilo do próprio modelo.</summary>
    private static Paragraph Titulo(string texto) => new(
        new ParagraphProperties(
            new ParagraphStyleId { Val = "HSA-TTULO2" },
            new SpacingBetweenLines { Before = "240", After = "120" }),
        new Run(new Text(texto) { Space = SpaceProcessingModeValues.Preserve }));

    /// <summary>
    /// Um título de seção do nível de "Introducción" e "Oferta Técnica", com o
    /// estilo do modelo. O modelo não numera os títulos, e por isso aqui
    /// também não: um "4" escrito à mão brigaria com os vizinhos sem número.
    /// </summary>
    private static Paragraph TituloDeSecao(string texto) => new(
        new ParagraphProperties(
            new ParagraphStyleId { Val = "HSA-Ttulo1" },
            new SpacingBetweenLines { Before = "0", After = "240" }),
        new Run(new Text(texto) { Space = SpaceProcessingModeValues.Preserve }));

    /// <summary>
    /// Um item de lista. O marcador vai no texto, com recuo pendente, para a
    /// segunda linha de um item longo alinhar com a primeira em vez de voltar
    /// para baixo do marcador.
    /// </summary>
    private static Paragraph Marcador(string texto, int nivel = 1) => new(
        // a ordem dentro do pPr é a do esquema do OOXML — spacing, ind e só
        // então jc. Fora de ordem o Word abre, mas o documento é inválido
        new ParagraphProperties(
            new SpacingBetweenLines { Before = "0", After = "40", Line = "240",
                LineRule = LineSpacingRuleValues.Auto },
            new Indentation { Left = (397 * nivel).ToString(), Hanging = "227" },
            new Justification { Val = JustificationValues.Left }),
        new Run(new Text((nivel > 1 ? "\u2013  " : "\u2022  ") + texto)
        { Space = SpaceProcessingModeValues.Preserve }));

    /// <summary>Um parágrafo de texto corrido, com a fonte do próprio modelo.</summary>
    private static Paragraph Paragrafo(string texto, bool negrito = false)
    {
        var run = new Run(new Text(texto) { Space = SpaceProcessingModeValues.Preserve });
        if (negrito) run.PrependChild(new RunProperties(new Bold()));

        return new Paragraph(
            new ParagraphProperties(
                new SpacingBetweenLines { Before = "120", After = "120" }),
            run);
    }

    private static Paragraph Vazio() => new();

    private static Paragraph QuebraDePagina() =>
        new(new Run(new Break { Type = BreakValues.Page }));

    /// <param name="Faixa">true na linha de cabeçalho, que sai em azul com letra branca.</param>
    private sealed record Quadro(string Texto, bool Faixa = false, int Colunas = 1);

    private static List<List<Quadro>> DuasColunas((string, string)? cabecalho,
        List<(string Rotulo, string Valor)> linhas)
    {
        var tabela = new List<List<Quadro>>();

        if (cabecalho is { } c)
            tabela.Add(new() { new(c.Item1, Faixa: true), new(c.Item2, Faixa: true) });

        foreach (var (rotulo, valor) in linhas)
            tabela.Add(new() { new(rotulo), new(valor) });

        return tabela;
    }

    private static List<List<Quadro>> UmaColuna(string cabecalho, IEnumerable<string> linhas)
    {
        var tabela = new List<List<Quadro>>
        {
            new() { new(cabecalho, Faixa: true, Colunas: 2) },
        };

        foreach (var linha in linhas) tabela.Add(new() { new(linha, Colunas: 2) });
        return tabela;
    }

    /// <summary>
    /// Monta a tabela com a cara das do modelo: faixa azul no cabeçalho, letra
    /// branca, e linhas finas entre as células.
    /// </summary>
    private static Table Tabela(List<List<Quadro>> linhas, int colunas = 2)
    {
        var props = new TableProperties(
            new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
            new TableBorders(
                new TopBorder { Val = BorderValues.Single, Size = 4, Color = "BFBFBF" },
                new LeftBorder { Val = BorderValues.None },
                new BottomBorder { Val = BorderValues.Single, Size = 4, Color = "BFBFBF" },
                new RightBorder { Val = BorderValues.None },
                new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4, Color = "BFBFBF" },
                new InsideVerticalBorder { Val = BorderValues.None }));

        var larguras = Larguras(colunas);

        var grade = new TableGrid(larguras
            .Select(l => (OpenXmlElement)new GridColumn { Width = l.ToString() })
            .ToArray());

        var tabela = new Table(props, grade);

        foreach (var linha in linhas)
        {
            var tr = new TableRow();
            var coluna = 0;

            foreach (var quadro in linha)
            {
                // a largura da célula é a soma das colunas que ela ocupa, para
                // uma célula que abre a faixa azul inteira não sair do tamanho
                // da primeira
                var ocupa = Math.Min(quadro.Colunas, colunas - coluna);
                var largura = larguras.Skip(coluna).Take(ocupa).Sum();

                var tcPr = new TableCellProperties(
                    new TableCellWidth
                    {
                        Type = TableWidthUnitValues.Pct,
                        Width = (5000L * largura / LarguraUtil).ToString(),
                    });

                if (ocupa > 1) tcPr.Append(new GridSpan { Val = ocupa });

                if (quadro.Faixa)
                    tcPr.Append(new Shading { Val = ShadingPatternValues.Clear, Fill = AzulDaFaixa });

                tr.Append(new TableCell(tcPr, Linha(quadro)));
                coluna += ocupa;
            }

            tabela.Append(tr);
        }

        return tabela;
    }

    /// <summary>
    /// A largura de cada coluna. A primeira leva metade da página e o resto
    /// divide a outra metade: é a cara das tabelas do modelo, em que o rótulo
    /// é largo e o valor e a unidade são estreitos.
    /// </summary>
    private static int[] Larguras(int colunas)
    {
        if (colunas <= 1) return new[] { LarguraUtil };

        var larguras = new int[colunas];
        larguras[0] = LarguraUtil / 2;

        for (var i = 1; i < colunas; i++) larguras[i] = LarguraUtil / 2 / (colunas - 1);

        return larguras;
    }

    /// <summary>O parágrafo de dentro da célula; "\n" vira quebra de linha.</summary>
    private static Paragraph Linha(Quadro quadro)
    {
        // e no rPr a ordem é rFonts, b, color, sz — por isso o negrito e a cor
        // entram ANTES do tamanho, e não no fim
        var formato = new RunProperties(new RunFonts { Ascii = "Arial", HighAnsi = "Arial" });

        if (quadro.Faixa)
        {
            formato.Append(new Bold());
            formato.Append(new Color { Val = "FFFFFF" });
        }

        formato.Append(new FontSize { Val = "18" });

        var run = new Run(formato);
        var primeiro = true;

        foreach (var parte in quadro.Texto.Split('\n'))
        {
            if (!primeiro) run.Append(new Break());
            run.Append(new Text(parte) { Space = SpaceProcessingModeValues.Preserve });
            primeiro = false;
        }

        // à esquerda, e não justificado: o documento é justificado por padrão,
        // e numa célula estreita isso espalha as palavras de ponta a ponta
        // ("LIMPEZA:      Padrão      SA      2      ½      onde      aplicável.")
        return new Paragraph(
            new ParagraphProperties(
                new SpacingBetweenLines { Before = "20", After = "20" },
                new Justification { Val = JustificationValues.Left }),
            run);
    }

    /// <summary>
    /// A curva, do tamanho da largura útil da página. A altura acompanha a
    /// proporção da imagem, para o gráfico não sair achatado.
    /// </summary>
    private Paragraph Imagem(ImagemDaCurva arquivo)
    {
        var bytes = arquivo.Bytes;

        var parte = _doc.MainDocumentPart!.AddImagePart(
            arquivo.Extensao.ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => ImagePartType.Jpeg,
                ".gif" => ImagePartType.Gif,
                ".bmp" => ImagePartType.Bmp,
                _ => ImagePartType.Png,
            });

        using (var fluxo = new MemoryStream(bytes)) parte.FeedData(fluxo);

        var id = _doc.MainDocumentPart.GetIdOfPart(parte);

        using var mapa = SkiaSharp.SKBitmap.Decode(bytes);

        // 1 cm são 360.000 EMU. A largura é a da mancha da página; a altura
        // segue a proporção da imagem, para o gráfico não sair achatado, mas
        // com um teto: sem ele uma curva quadrada ocuparia a folha inteira e
        // empurraria o título dela para a página anterior
        var largura = 15L * 360_000;
        var altura = mapa is { Width: > 0 } ? largura * mapa.Height / mapa.Width : largura;

        const long Teto = 11L * 360_000;
        if (altura > Teto)
        {
            largura = largura * Teto / altura;
            altura = Teto;
        }

        var desenho = new Drawing(new DesenhoWord.Inline(
            new DesenhoWord.Extent { Cx = largura, Cy = altura },
            new DesenhoWord.DocProperties { Id = (uint)(900 + _figuras++), Name = "curva" },
            new Desenho.Graphic(new Desenho.GraphicData(
                new Figura.Picture(
                    new Figura.NonVisualPictureProperties(
                        new Figura.NonVisualDrawingProperties { Id = 0, Name = "curva" },
                        new Figura.NonVisualPictureDrawingProperties()),
                    new Figura.BlipFill(
                        new Desenho.Blip { Embed = id },
                        new Desenho.Stretch(new Desenho.FillRectangle())),
                    new Figura.ShapeProperties(
                        new Desenho.Transform2D(
                            new Desenho.Offset { X = 0, Y = 0 },
                            new Desenho.Extents { Cx = largura, Cy = altura }),
                        new Desenho.PresetGeometry(new Desenho.AdjustValueList())
                        { Preset = Desenho.ShapeTypeValues.Rectangle })))
            { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" })));

        return new Paragraph(
            new ParagraphProperties(new Justification { Val = JustificationValues.Center }),
            new Run(desenho));
    }

    private int _figuras;
}
