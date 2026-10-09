using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace HowdenAxiais.Poc.Data;

/// <summary>
/// O preenchimento do modelo em Word, seção por seção, na ordem da instrução
/// da equipe. Ver <see cref="PropostaWord"/> para o porquê.
///
/// <para>
/// <b>Em espanhol não se reescreve texto fixo.</b> O modelo já está em
/// espanhol e é ele que manda: a proposta em espanhol só recebe DADOS e perde
/// as alternativas que não valem. Em português e inglês o texto fixo é
/// traduzido (ver <see cref="TextosDaProposta"/>) — e é só nesse caminho que
/// existe risco de a tradução discordar do modelo.
/// </para>
/// </summary>
internal sealed partial class Preenchimento
{
    private readonly WordprocessingDocument _doc;
    private readonly Proposta _p;
    private readonly CustoDaProposta _custo;
    private readonly TextosDaProposta _t;
    private readonly TextosDoEscopo _te;
    private readonly TextosEletricos _tel;
    private readonly NotaDeComponente[] _notas;
    private readonly bool _espanhol;

    /// <summary>
    /// Qual das duas propostas está sendo gerada. Muda o subtítulo da capa e,
    /// daí em diante, o documento inteiro.
    /// </summary>
    private bool _tecnica;
    private readonly Body _corpo;
    private readonly Moeda _moeda;
    private readonly List<decimal> _precos;
    private readonly decimal _venda;

    public Preenchimento(WordprocessingDocument doc, Proposta p, CustoDaProposta custo)
    {
        _doc = doc;
        _p = p;
        _custo = custo;
        _moeda = p.Moeda;

        var idioma = TextosDaProposta.Ler(p.Idioma);
        _t = TextosDaProposta.Do(idioma);
        _te = TextosDoEscopo.Do(idioma);
        _tel = TextosEletricos.Do(idioma);
        // a placa de identificação do motor sai com os dados do motor escolhido
        _notas = NotasDosComponentes.Do(idioma, p.Itens
            .Where(i => i.ComMotor)
            .Select(custo.MotorDe)
            .OfType<Motor>()
            .ToList());
        _espanhol = idioma == IdiomaDaProposta.Espanhol;

        _corpo = doc.MainDocumentPart!.Document.Body!;
        _precos = PropostaWord.PrecosDeVenda(p, custo);
        _venda = CalculoPricing.Da(p, custo.Total(p)).VendaLiquida;
    }

    public void Preencher()
    {
        Propriedades();
        CamposLigados();
        Capa();
        EnderecoDaBu();
        Contatos();
        Representante();
        Revisoes();
        Precos();
        Impostos();
        Pagamento();
        Prazo();
        Entrega();
        Validade();
        Notas();
        Assessoria();
        Diarias();
        Fechamento();
        TirarRealce();

        _doc.MainDocumentPart!.Document.Save();
    }

    /// <summary>
    /// Tira o realce do documento inteiro — de qualquer cor.
    ///
    /// No modelo o grifo não é destaque: é a marca de "escolher", "preencher"
    /// ou "conferir" para quem monta a proposta à mão. O amarelo é o mais
    /// comum, mas há verde nas notas. Nenhum deles tem o que dizer depois de
    /// preenchido — e todos sairiam grifados na proposta do cliente.
    /// </summary>
    private void TirarRealce()
    {
        var partes = new List<OpenXmlPartRootElement?> { _doc.MainDocumentPart!.Document };
        partes.AddRange(_doc.MainDocumentPart.HeaderParts.Select(h => (OpenXmlPartRootElement?)h.Header));
        partes.AddRange(_doc.MainDocumentPart.FooterParts.Select(f => (OpenXmlPartRootElement?)f.Footer));

        foreach (var parte in partes.Where(p => p is not null))
        {
            foreach (var realce in parte!.Descendants<Highlight>().ToList()) realce.Remove();
            parte.Save();
        }
    }

    // ================= propriedades do documento =================

    /// <summary>
    /// As propriedades que a capa e o cabeçalho leem. Elas e o texto do campo
    /// têm de ser escritas juntas: a propriedade é o que fica valendo se
    /// alguém atualizar os campos no Word, e o texto é o que se vê ao abrir.
    /// </summary>
    private void Propriedades()
    {
        var props = _doc.PackageProperties;
        props.Title = PropostaWord.Referencia(_p);
        props.Category = _p.Projeto;
        props.Description = _p.AosCuidados;
        props.Keywords = _p.ReferenciaCliente;
        props.Creator = _p.PreparadaPor;

        if (_doc.ExtendedFilePropertiesPart?.Properties is { } extendidas)
        {
            extendidas.Company ??= new DocumentFormat.OpenXml.ExtendedProperties.Company();
            extendidas.Company.Text = _p.Cliente;
            extendidas.Save();
        }

        CapaEmXmlProprio();
    }

    /// <summary>
    /// Cidade e data moram numa parte XML à parte (CoverPageProperties), que é
    /// onde o Word guarda os campos de capa que não são propriedade padrão.
    /// </summary>
    private void CapaEmXmlProprio()
    {
        foreach (var parte in _doc.MainDocumentPart!.CustomXmlParts)
        {
            string xml;
            using (var leitura = parte.GetStream(FileMode.Open, FileAccess.Read))
            using (var leitor = new StreamReader(leitura))
                xml = leitor.ReadToEnd();

            if (!xml.Contains("CoverPageProperties")) continue;

            xml = TrocarTag(xml, "PublishDate", Hoje());
            xml = TrocarTag(xml, "CompanyAddress", _p.Cidade);

            using var escrita = parte.GetStream(FileMode.Create, FileAccess.Write);
            using var escritor = new StreamWriter(escrita);
            escritor.Write(xml);
        }
    }

    /// <summary>Troca o conteúdo de uma tag, inclusive quando ela está vazia (&lt;Tag/&gt;).</summary>
    private static string TrocarTag(string xml, string tag, string valor)
    {
        var seguro = System.Security.SecurityElement.Escape(valor) ?? "";

        var vazia = $"<{tag}/>";
        if (xml.Contains(vazia)) return xml.Replace(vazia, $"<{tag}>{seguro}</{tag}>");

        var abre = xml.IndexOf($"<{tag}>", StringComparison.Ordinal);
        var fecha = xml.IndexOf($"</{tag}>", StringComparison.Ordinal);
        if (abre < 0 || fecha < abre) return xml;

        return xml[..(abre + tag.Length + 2)] + seguro + xml[fecha..];
    }

    /// <summary>
    /// Os campos da capa e do cabeçalho, que são <i>content controls</i> ligados
    /// às propriedades. Escrever o texto e tirar a marca de "mostrando
    /// exemplo" é o que faz o campo deixar de sair cinza no documento.
    /// </summary>
    private void CamposLigados()
    {
        var valores = new Dictionary<string, string>
        {
            ["Empresa"] = _p.Cliente,
            ["Categoria"] = _p.Projeto,
            ["Comentários"] = _p.AosCuidados,
            ["Título"] = PropostaWord.Referencia(_p),
            ["Endereço da Empresa"] = _p.Cidade,
            ["Palavras-chave"] = _p.ReferenciaCliente,
            ["Data de Publicação"] = Hoje(),
        };

        var partes = new List<OpenXmlPartRootElement?> { _doc.MainDocumentPart!.Document };
        partes.AddRange(_doc.MainDocumentPart.HeaderParts.Select(h => (OpenXmlPartRootElement?)h.Header));
        partes.AddRange(_doc.MainDocumentPart.FooterParts.Select(f => (OpenXmlPartRootElement?)f.Footer));

        foreach (var parte in partes.Where(p => p is not null))
        {
            foreach (var sdt in parte!.Descendants<SdtElement>().ToList())
            {
                var alias = sdt.SdtProperties?.GetFirstChild<SdtAlias>()?.Val?.Value;
                if (alias is null || !valores.TryGetValue(alias, out var valor)) continue;

                // sem valor, o campo tem de SAIR. Ele está ligado a uma
                // propriedade do documento: vazia, o Word volta a mostrar o
                // exemplo — e a proposta sairia para o cliente escrito
                // "[Endereço da Empresa]"
                if (valor.Trim().Length == 0)
                {
                    Desembrulhar(sdt);
                    continue;
                }

                sdt.SdtProperties?.GetFirstChild<ShowingPlaceholder>()?.Remove();

                // o campo vazio é cinza claro porque está mostrando o exemplo;
                // preenchido, ele tem de ficar da cor do texto, como os outros
                // dados da capa
                foreach (var cinza in sdt.Descendants<Color>().ToList()) cinza.Remove();

                var textos = sdt.Descendants<Text>().ToList();
                if (textos.Count == 0) continue;

                textos[0].Text = valor;
                textos[0].Space = SpaceProcessingModeValues.Preserve;
                foreach (var sobra in textos.Skip(1)) sobra.Remove();
            }
            parte.Save();
        }
    }

    /// <summary>
    /// Tira o campo e deixa no lugar dele o que ele embrulhava, vazio: sem a
    /// ligação com a propriedade, o Word não tem mais o que mostrar ali.
    /// </summary>
    private static void Desembrulhar(SdtElement sdt)
    {
        var conteudo = sdt.ChildElements.FirstOrDefault(e =>
            e is SdtContentBlock or SdtContentRun or SdtContentCell or SdtContentRow);

        if (conteudo is null || sdt.Parent is null)
        {
            sdt.Remove();
            return;
        }

        foreach (var texto in conteudo.Descendants<Text>().ToList()) texto.Text = "";

        foreach (var filho in conteudo.ChildElements.ToList())
        {
            filho.Remove();
            sdt.Parent.InsertBefore(filho, sdt);
        }

        sdt.Remove();
    }

    // ================= capa =================

    /// <summary>E-mail, telefone e quem preparou — as três células soltas da capa.</summary>
    private void Capa()
    {
        var capa = Tabelas().First();

        // os valores primeiro: os rótulos são achados pelo texto em espanhol,
        // e traduzi-los antes apagaria a âncora
        Rotulo(capa, "E-mail:", _p.Email);
        Rotulo(capa, "Fono:", _p.Telefone);
        Rotulo(capa, "Preparada por:", _p.PreparadaPor);

        // o subtítulo da capa é "Oferta Comercial" — sem o "Técnica", que é da
        // proposta técnica, e é escrito também em espanhol
        if (capa.Descendants<Paragraph>()
                .FirstOrDefault(p => Texto(p).Trim() == "Oferta Técnica Comercial") is { } subtitulo)
        {
            Escrever(subtitulo, _tecnica ? _t.OfertaTecnica : _t.OfertaComercial);
        }

        Rotulos(capa);

        // o mesmo cabeçalho de cliente e referências se repete no alto de
        // todas as páginas, numa parte à parte do documento
        foreach (var parte in _doc.MainDocumentPart!.HeaderParts)
        {
            if (parte.Header is null) continue;
            Rotulos(parte.Header);
            parte.Header.Save();
        }
    }

    /// <summary>
    /// Os rótulos que ficam ao lado dos campos preenchidos. Não têm valor para
    /// escrever, mas mudam de língua — e são achados pelo texto, porque o
    /// mesmo rótulo aparece na capa e no cabeçalho de toda página.
    /// </summary>
    private void Rotulos(OpenXmlElement raiz)
    {
        if (_espanhol) return;

        var rotulos = new (string Espanhol, string Traduzido)[]
        {
            ("Cliente:", _t.Cliente), ("Al cuidado de:", _t.AosCuidados),
            ("Ciudad:", _t.Cidade), ("Su referencia:", _t.SuaReferencia),
            ("Proyecto:", _t.Projeto), ("Nuestra referencia:", _t.NossaReferencia),
            ("Fecha:", _t.Data), ("Contactos Howden", _t.ContatosHowden),
            ("E-mail:", _t.Email), ("Fono:", _t.Telefone), ("Preparada por:", _t.PreparadaPor),
            ("Nuestra Ref.:", _t.NossaRefCurta), ("Su Ref.:", _t.SuaRefCurta),
        };

        foreach (var p in raiz.Descendants<Paragraph>().ToList())
        {
            var texto = Texto(p).Trim();

            foreach (var (espanhol, traduzido) in rotulos)
            {
                if (texto != espanhol) continue;
                Escrever(p, traduzido);
                break;
            }
        }
    }

    /// <summary>Escreve o valor na célula seguinte à do rótulo.</summary>
    private void Rotulo(Table tabela, string rotuloEspanhol, string valor)
    {
        if (Celula(tabela, rotuloEspanhol) is not { } celula) return;

        if (celula.NextSibling<TableCell>() is { } seguinte)
            EscreverNaCelula(seguinte, new[] { valor });
    }

    // ================= contatos =================

    /// <summary>
    /// O endereço da BU. O modelo traz os quatro, um bloco por empresa, e a
    /// instrução manda apagar os outros três.
    /// </summary>
    private void EnderecoDaBu()
    {
        var marcas = new Dictionary<string, string>
        {
            ["HSA-SP"] = "Osvaldo Berto",
            ["HSA-ES"] = "Rua 4E",
            ["HCHL"] = "Cordillera",
            ["HPU"] = "Guillermo Marconi",
        };

        var escolhida = marcas.GetValueOrDefault(ListasDaProposta.CodigoDaBu(_p.Bu), "Osvaldo Berto");

        var celula = Tabelas().First().Descendants<TableCell>()
            .FirstOrDefault(c => Texto(c).Contains("Osvaldo Berto", StringComparison.Ordinal));
        if (celula is null) return;

        // os blocos são separados por parágrafo em branco; fica o da BU
        // escolhida e o do "Web:", que vale para todas
        var blocos = new List<List<Paragraph>> { new() };
        foreach (var p in celula.Elements<Paragraph>())
        {
            if (Texto(p).Trim().Length == 0) blocos.Add(new());
            else blocos[^1].Add(p);
        }

        foreach (var bloco in blocos)
        {
            var texto = string.Join(" ", bloco.Select(Texto));
            if (texto.Contains(escolhida, StringComparison.Ordinal)) continue;
            if (texto.Contains("Web:", StringComparison.Ordinal)) continue;

            foreach (var p in bloco) p.Remove();
        }

        // os parágrafos em branco que separavam os quatro endereços continuavam
        // lá, ocupando altura: a célula é centrada, então o endereço subia e
        // deixava de ficar na mesma altura da imagem do plantão. Fica um só,
        // antes do "Web:"
        var brancos = celula.Elements<Paragraph>()
            .Where(p => Texto(p).Trim().Length == 0)
            .ToList();

        var web = celula.Elements<Paragraph>()
            .FirstOrDefault(p => Texto(p).Contains("Web:", StringComparison.Ordinal));

        foreach (var branco in brancos) branco.Remove();

        if (web is not null && brancos.Count > 0)
            celula.InsertBefore((Paragraph)brancos[0].CloneNode(true), web);

        Centrar(celula);
    }

    // A geometria da linha do endereço, em pontos, medida no documento gerado
    // (a linha tem altura fixa no modelo: 3024 twips).
    private const double AlturaDaLinha = 151.2;
    private const double AlturaDaImagem = 107.4;
    private const double AlturaDaLinhaDeTexto = 10.4;

    /// <summary>
    /// Põe o texto do endereço e a imagem do plantão no MESMO eixo: cada um
    /// centrado na altura da linha, que é o que faz os dois se lerem lado a
    /// lado.
    ///
    /// Sozinha, a célula centrada não resolve. Quem manda na posição da imagem
    /// é a âncora dela, que é o parágrafo do endereço: o texto encosta no topo
    /// e a imagem desce a partir dele. Então são duas contas — quanto o texto
    /// desce, e quanto a imagem sobe em relação ao parágrafo que a ancora.
    /// </summary>
    private static void Centrar(TableCell celula)
    {
        var paragrafos = celula.Elements<Paragraph>().ToList();
        if (paragrafos.Count == 0) return;

        // o endereço é uma linha por parágrafo, menos o do logradouro, que é
        // longo e quebra em duas
        var linhasDeTexto = paragrafos.Count + 1;
        var alturaDoTexto = linhasDeTexto * AlturaDaLinhaDeTexto;

        var acimaDoTexto = (AlturaDaLinha - alturaDoTexto) / 2;
        Espaco(paragrafos[0], acimaDoTexto);

        // a imagem está ancorada ao segundo parágrafo; o deslocamento dela é
        // contado do alto DESSE parágrafo, e por isso é negativo — ela começa
        // acima de onde está ancorada
        var acimaDaImagem = (AlturaDaLinha - AlturaDaImagem) / 2;
        var altoDaAncora = acimaDoTexto + AlturaDaLinhaDeTexto;
        Deslocar(celula, acimaDaImagem - altoDaAncora);
    }

    /// <summary>Espaço antes do parágrafo, em pontos (o Word guarda em 1/20).</summary>
    private static void Espaco(Paragraph p, double pontos)
    {
        p.ParagraphProperties ??= new ParagraphProperties();
        p.ParagraphProperties.SpacingBetweenLines ??= new SpacingBetweenLines();
        p.ParagraphProperties.SpacingBetweenLines.Before =
            Math.Max(0, Math.Round(pontos * 20)).ToString("F0");
    }

    /// <summary>
    /// Sobe ou desce a imagem ancorada, em pontos. O Word mede em EMU, que são
    /// 12.700 por ponto.
    /// </summary>
    private static void Deslocar(TableCell celula, double pontos)
    {
        var emu = (long)Math.Round(pontos * 12700);

        foreach (var posicao in celula.Descendants<DocumentFormat.OpenXml.Drawing.Wordprocessing.VerticalPosition>())
        {
            posicao.RelativeFrom = DocumentFormat.OpenXml.Drawing.Wordprocessing
                .VerticalRelativePositionValues.Paragraph;
            posicao.RemoveAllChildren<DocumentFormat.OpenXml.Drawing.Wordprocessing.VerticalAlignment>();
            posicao.PositionOffset ??= new DocumentFormat.OpenXml.Drawing.Wordprocessing.PositionOffset();
            posicao.PositionOffset.Text = emu.ToString();
        }
    }

    /// <summary>
    /// Os contatos. O modelo tem os quinze em cinco linhas de três; a proposta
    /// leva até dois, então o primeiro par fica e as outras linhas somem.
    /// </summary>
    private void Contatos()
    {
        var capa = Tabelas().First();

        // o diretor de vendas já está numa linha de três, sozinho na célula da
        // direita: é essa a linha da proposta, e os escolhidos entram do lado
        // dele. As outras cinco linhas, com os quinze contatos, saem
        var doDiretor = capa.Elements<TableRow>()
            .FirstOrDefault(l => Texto(l).Contains("Geraldini", StringComparison.Ordinal));
        if (doDiretor is null) return;

        var dosContatos = capa.Elements<TableRow>()
            .Where(l => l != doDiretor
                        && Texto(l).Contains("@chartindustries.com", StringComparison.Ordinal))
            .ToList();

        var celulas = doDiretor.Elements<TableCell>().ToList();
        if (celulas.Count < 2) return;

        // a célula do diretor é o molde: é dela que vêm o negrito do nome, o
        // centralizado e o tamanho da letra
        var molde = celulas[^1];
        var escolhidos = Escolhidos().ToList();

        for (var i = 0; i < celulas.Count - 1; i++)
        {
            if (i < escolhidos.Count) ComoOMolde(celulas[i], molde, Linhas(escolhidos[i]));
            else EscreverNaCelula(celulas[i], new[] { "" });
        }

        foreach (var sobra in dosContatos) sobra.Remove();
    }

    /// <summary>
    /// Escreve numa célula com a formatação de outra: os parágrafos do molde
    /// são copiados para lá antes de receberem o texto.
    /// </summary>
    private static void ComoOMolde(TableCell celula, TableCell molde, IReadOnlyList<string> linhas)
    {
        foreach (var antigo in celula.Elements<Paragraph>().ToList()) antigo.Remove();

        foreach (var p in molde.Elements<Paragraph>())
            celula.Append((Paragraph)p.CloneNode(true));

        // o alinhamento vertical também é da célula, e não do parágrafo
        if (molde.TableCellProperties?.GetFirstChild<TableCellVerticalAlignment>() is { } alinhamento)
        {
            celula.TableCellProperties ??= new TableCellProperties();
            celula.TableCellProperties.GetFirstChild<TableCellVerticalAlignment>()?.Remove();
            celula.TableCellProperties.Append((TableCellVerticalAlignment)alinhamento.CloneNode(true));
        }

        EscreverNaCelula(celula, linhas);
    }

    private IEnumerable<ListasDaProposta.ContatoHowden> Escolhidos()
    {
        if (_p.ContatoNome.Trim().Length > 0)
            yield return new(_p.ContatoNome, _p.ContatoCargo, _p.ContatoArea,
                _p.ContatoTelefones, _p.ContatoEmail);

        if (_p.Contato2Nome.Trim().Length > 0)
            yield return new(_p.Contato2Nome, _p.Contato2Cargo, _p.Contato2Area,
                _p.Contato2Telefones, _p.Contato2Email);
    }

    private static string[] Linhas(ListasDaProposta.ContatoHowden c) => new[]
        {
            c.Nome, c.Cargo, c.Area, c.Telefones, c.Email,
        }
        .Where(l => l.Trim().Length > 0)
        .ToArray();

    /// <summary>
    /// O representante. O modelo lista todos os da América Latina; fica só o
    /// desta proposta, com o contato que está gravado nela.
    /// </summary>
    private void Representante()
    {
        var celula = Tabelas().First().Descendants<TableCell>()
            .FirstOrDefault(c => Texto(c).StartsWith("Sales Agent:", StringComparison.Ordinal));
        if (celula is null) return;

        if (_p.Representante.Trim().Length == 0)
        {
            // sem representante, a linha inteira sai
            celula.Ancestors<TableRow>().FirstOrDefault()?.Remove();
            return;
        }

        var linhas = new List<string> { _t.Representantes, Linha(_p.Representante, _p.ContatoDoRepresentante()) };

        if (_p.Representante2.Trim().Length > 0)
            linhas.Add(Linha(_p.Representante2, _p.ContatoDoRepresentante2()));

        EscreverNaCelula(celula, linhas);

        static string Linha(string nome, string contato) =>
            contato.Trim().Length > 0 ? $"{nome} — {contato}" : nome;
    }

    private void Revisoes()
    {
        var tabela = Tabelas().FirstOrDefault(t => Texto(t).StartsWith("Rev.", StringComparison.Ordinal));
        if (tabela is null) return;

        var cabecalho = tabela.Elements<TableRow>().First();
        FixoNasCelulas(cabecalho, _t.Revisao, _t.Executou, _t.Aprovou, _t.Descricao);

        var linha = tabela.Elements<TableRow>().Skip(1).FirstOrDefault();
        if (linha is null) return;

        var celulas = linha.Elements<TableCell>().ToList();
        if (celulas.Count < 4) return;

        var revisao = PropostaWord.Ou(_p.Revisao, "0");
        EscreverNaCelula(celulas[0], new[] { revisao });
        EscreverNaCelula(celulas[1], new[] { _p.PreparadaPor });

        // a coluna de quem executou nasceu do tamanho de "XX"; com um nome
        // dentro ela quebrava em quatro linhas. Alarga o bastante para caber
        // numa linha só, e o espaço sai da coluna da descrição, que é a larga
        Alargar(tabela, coluna: 1, texto: _p.PreparadaPor);
        EscreverNaCelula(celulas[3], new[]
        {
            revisao.TrimStart('0').Length == 0 ? _t.EmissaoInicial : _t.RevisaoDaOferta,
        });
    }

    /// <summary>
    /// Alarga uma coluna até o texto caber numa linha, tirando a diferença da
    /// última coluna da tabela.
    ///
    /// A largura de uma letra é uma ESTIMATIVA (a fonte não está aqui para
    /// medir), folgada de propósito: sobrar um pouco não incomoda ninguém, e
    /// faltar quebra a linha de novo.
    /// </summary>
    private static void Alargar(Table tabela, int coluna, string texto)
    {
        const int PorLetra = 120;   // twips, com folga
        const int Margem = 280;     // o respiro das duas bordas da célula

        var grade = tabela.GetFirstChild<TableGrid>();
        if (grade is null) return;

        var colunas = grade.Elements<GridColumn>().ToList();
        if (coluna >= colunas.Count || colunas.Count < 2) return;

        var atual = Numero(colunas[coluna].Width);
        var preciso = texto.Trim().Length * PorLetra + Margem;
        if (preciso <= atual) return;

        var ultima = colunas.Count - 1;
        var sobra = Numero(colunas[ultima].Width);
        var ganho = Math.Min(preciso - atual, sobra / 2);
        if (ganho <= 0) return;

        colunas[coluna].Width = (atual + ganho).ToString();
        colunas[ultima].Width = (sobra - ganho).ToString();

        // a largura vive em dois lugares: na grade da tabela e em cada célula.
        // Mexer só na grade deixa o Word decidindo pelo que está na célula
        var total = colunas.Sum(c => Numero(c.Width));
        if (total <= 0) return;

        foreach (var linha in tabela.Elements<TableRow>())
        {
            var celulas = linha.Elements<TableCell>().ToList();
            if (celulas.Count != colunas.Count) continue;

            for (var i = 0; i < celulas.Count; i++)
            {
                var props = celulas[i].TableCellProperties ??= new TableCellProperties();
                props.TableCellWidth = new TableCellWidth
                {
                    Type = TableWidthUnitValues.Pct,
                    Width = (Numero(colunas[i].Width) * 5000 / total).ToString(),
                };
            }
        }
    }

    private static int Numero(StringValue? valor) =>
        int.TryParse(valor?.Value, out var n) ? n : 0;

    // ================= preço =================

    private void Precos()
    {
        if (Achar("Introducción") is { } introducao) Fixo(introducao, _t.Introducao);
        if (Achar("Oferta Comercial") is { } comercial) Fixo(comercial, _t.OfertaComercial);

        // o título do preço vem com a instrução "(BORRAR SI FUERA ARRIENDO)"
        if (Achar("BORRAR SI FUERA ARRIENDO") is { } titulo) Escrever(titulo, _t.Preco);

        if (Achar("Oferta Técnica XXXXXX-TX") is { } referencia)
            Escrever(referencia, string.Format(_t.PrecosConformeTecnica, PropostaWord.ReferenciaTecnica(_p)));

        Introducao();
        TabelaDePrecos();

        // o bloco de aluguel e a tabela de reposição do modelo saem inteiros,
        // como a instrução dele manda; no lugar dela entram os opcionais DESTA
        // proposta, quando há algum
        var aviso = Achar("BORRAR SI FUERA VENTA");
        var fimDosOpcionais = Achar("Tales ítems deberán ser incluidos");
        if (aviso is not null && fimDosOpcionais is not null) ApagarEntre(aviso, fimDosOpcionais);
        if (aviso is not null) Escrever(aviso, _t.AvisoDaDescricao);

        if (aviso is not null) OpcionaisDaComercial(aviso);
    }

    /// <summary>
    /// A tabela de itens opcionais da proposta comercial, no lugar da tabela de
    /// reposição do modelo.
    ///
    /// É desenhada com a MESMA cara da tabela de preço do modelo — faixa azul
    /// com letra branca centrada, linhas cinzas separando todas as células,
    /// coluna de item estreita e a de valor à direita —, porque as duas ficam
    /// uma embaixo da outra na mesma página e qualquer diferença salta aos
    /// olhos.
    ///
    /// O preço de cada um é o custo vezes o fator da proposta: eles não entram
    /// no preço fechado, então não há pricing próprio para eles — o que há é a
    /// mesma margem, comissão e imposto que a proposta já fechou.
    /// </summary>
    private void OpcionaisDaComercial(OpenXmlElement depois)
    {
        var varios = _p.Itens.Count > 1;
        var linhas = new List<List<CelulaDePreco>>();
        var total = 0m;

        for (var i = 0; i < _p.Itens.Count; i++)
        {
            foreach (var opcional in _custo.Opcionais(_p.Itens[i], _moeda))
            {
                // vezes a quantidade do equipamento, como no escopo fechado:
                // dois ventiladores levam dois dampers
                var custo = _custo.CustoDoOpcional(_p.Itens[i], opcional) * _p.Itens[i].Quantos;
                var preco = PropostaWord.PrecoDoOpcional(_p, _custo, custo);
                total += preco;

                // com um equipamento só, dizer de quem é o opcional é ruído;
                // com vários, é o que diz em qual ventilador ele entra
                var nome = varios
                    ? $"{_t.Ventilador} {i + 1:D2} — {opcional.Nome(_te)}"
                    : opcional.Nome(_te);

                linhas.Add(new()
                {
                    new((linhas.Count + 1).ToString("D2"), JustificationValues.Center),
                    new(_p.Itens[i].Quantos.ToString(), JustificationValues.Center),
                    new(nome, JustificationValues.Left),
                    // centrado como na tabela de preço, e não à direita: as duas
                    // ficam uma embaixo da outra e a coluna é a mesma
                    new(PropostaWord.Dinheiro(preco, _moeda), JustificationValues.Center),
                });
            }
        }

        // nenhum opcional: a seção não entra, e o documento não ganha um título
        // com uma tabela vazia embaixo
        if (linhas.Count == 0) return;

        var titulo = Titulo(_t.Opcionais);
        _corpo.InsertAfter(titulo, depois);
        _corpo.InsertAfter(TabelaDeOpcionais(linhas, total), titulo);
    }

    /// <param name="Alinhamento">Como o texto fica na célula.</param>
    private sealed record CelulaDePreco(string Texto, JustificationValues Alinhamento,
        bool Faixa = false, bool Negrito = false, int Colunas = 1);

    /// <summary>As colunas da tabela de opcionais, nas medidas da tabela de preço.</summary>
    private static readonly int[] ColunasDoOpcional = { 709, 735, 7091, 1321 };

    private Table TabelaDeOpcionais(List<List<CelulaDePreco>> linhas, decimal total)
    {
        var tabela = new Table(
            new TableProperties(
                new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct }),
            new TableGrid(ColunasDoOpcional
                .Select(l => (OpenXmlElement)new GridColumn { Width = l.ToString() })
                .ToArray()));

        // no modelo todo cabeçalho é centrado, inclusive o da coluna larga
        var cabecalho = _t.ColunasDosOpcionais
            .Select(c => new CelulaDePreco(c, JustificationValues.Center, Faixa: true))
            .ToList();

        var fecho = new List<CelulaDePreco>
        {
            new(_t.TotalDaProposta, JustificationValues.Right, Negrito: true, Colunas: 3),
            new(PropostaWord.Dinheiro(total, _moeda), JustificationValues.Left, Negrito: true),
        };

        foreach (var linha in new[] { cabecalho }.Concat(linhas).Append(fecho))
            tabela.Append(LinhaDeOpcional(linha));

        return tabela;
    }

    private static TableRow LinhaDeOpcional(List<CelulaDePreco> celulas)
    {
        var tr = new TableRow();
        var coluna = 0;

        foreach (var celula in celulas)
        {
            var ocupa = Math.Min(celula.Colunas, ColunasDoOpcional.Length - coluna);
            var largura = ColunasDoOpcional.Skip(coluna).Take(ocupa).Sum();

            // a ordem dentro do tcPr é a do esquema: tcW, gridSpan, tcBorders,
            // shd, vAlign
            var tcPr = new TableCellProperties(new TableCellWidth
            {
                Type = TableWidthUnitValues.Pct,
                Width = (5000L * largura / ColunasDoOpcional.Sum()).ToString(),
            });

            if (ocupa > 1) tcPr.Append(new GridSpan { Val = ocupa });

            // bordas por célula, e não da tabela: é como o modelo desenha a
            // tabela de preço, e é o que dá as linhas verticais entre colunas
            tcPr.Append(new TableCellBorders(
                Borda<TopBorder>(), Borda<LeftBorder>(), Borda<BottomBorder>(), Borda<RightBorder>()));

            if (celula.Faixa)
                tcPr.Append(new Shading { Val = ShadingPatternValues.Clear, Fill = AzulDaFaixa });

            tcPr.Append(new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center });

            var formato = new RunProperties(
                new RunFonts { Ascii = "Arial", HighAnsi = "Arial" });

            // a ordem do rPr é rFonts, b, color, sz
            if (celula.Faixa || celula.Negrito) formato.Append(new Bold());
            if (celula.Faixa) formato.Append(new Color { Val = "FFFFFF" });

            formato.Append(new FontSize { Val = "18" });

            var paragrafo = new Paragraph(
                new ParagraphProperties(
                    new SpacingBetweenLines { Line = "220", LineRule = LineSpacingRuleValues.AtLeast },
                    new Justification { Val = celula.Alinhamento }),
                new Run(formato,
                    new Text(celula.Texto) { Space = SpaceProcessingModeValues.Preserve }));

            tr.Append(new TableCell(tcPr, paragrafo));
            coluna += ocupa;
        }

        return tr;
    }

    /// <summary>A linha cinza fina que o modelo usa entre as células.</summary>
    private static T Borda<T>() where T : BorderType, new() =>
        new() { Val = BorderValues.Single, Size = 4, Color = "808080" };

    private void Introducao()
    {
        if (Achar("Estimado/a") is { } saudacao)
            Escrever(saudacao, string.Format(_t.Saudacao, PropostaWord.Ou(_p.AosCuidados, "—")));

        if (Achar("Howden tiene la satisfacción") is { } apresentacao)
        {
            Escrever(apresentacao, string.Format(_t.Apresentacao,
                PropostaWord.Ou(_p.Cliente, "—"),
                PropostaWord.Ou(_p.MarketSegment, _espanhol ? "aplicación" : "aplicação")));
        }

        if (Achar("más de 160 años") is { } sobre) Fixo(sobre, _t.SobreAHowden);
        if (Achar("NOTA:") is { } nota) Fixo(nota, _t.Nota);
        if (Achar("(KYC)") is { } kyc) Fixo(kyc, _t.TextoKyc);
        if (Achar("CONTROL DE REVISIONES") is { } revisoes) Fixo(revisoes, _t.ControleDeRevisoes);
    }

    private void TabelaDePrecos()
    {
        var tabela = Tabelas().FirstOrDefault(t => Texto(t).StartsWith("ITEM", StringComparison.Ordinal));
        if (tabela is null) return;

        var linhas = tabela.Elements<TableRow>().ToList();
        FixoNasCelulas(linhas[0], _t.Item, _t.Quantidade, _t.Produto, _t.ValorUnitario, _t.ValorTotal);

        var total = linhas[^1];
        FixoNasCelulas(total, _t.TotalDaProposta);
        EscreverNaCelula(total.Elements<TableCell>().Last(),
            new[] { PropostaWord.Dinheiro(_venda, _moeda) });

        // todas as linhas de item saem de uma cópia da primeira: é ela que tem
        // o parágrafo de lista do "Incluye:"
        var modelo = (TableRow)linhas[1].CloneNode(true);

        for (var i = 0; i < _p.Itens.Count; i++)
        {
            var linha = (TableRow)modelo.CloneNode(true);
            PreencherItem(linha, i);
            tabela.InsertBefore(linha, total);
        }

        foreach (var antiga in linhas.Skip(1).Take(linhas.Count - 2)) antiga.Remove();
    }

    private void PreencherItem(TableRow linha, int indice)
    {
        var item = _p.Itens[indice];
        var celulas = linha.Elements<TableCell>().ToList();
        if (celulas.Count < 5) return;

        var totalDoItem = indice < _precos.Count ? _precos[indice] : 0m;
        var unitario = item.Quantos > 0 ? totalDoItem / item.Quantos : totalDoItem;

        EscreverNaCelula(celulas[0], new[] { (indice + 1).ToString("D2") });
        EscreverNaCelula(celulas[1], new[] { item.Quantos.ToString() });
        Descricao(celulas[2], item);
        EscreverNaCelula(celulas[3], new[] { PropostaWord.Dinheiro(unitario, _moeda) });
        EscreverNaCelula(celulas[4], new[] { PropostaWord.Dinheiro(totalDoItem, _moeda) });
    }

    /// <summary>
    /// A descrição do equipamento: a linha do ventilador, o "Incluye:" e um
    /// parágrafo de lista por item do escopo — reaproveitando os parágrafos do
    /// modelo, que é de onde vêm os marcadores e o recuo.
    /// </summary>
    private void Descricao(TableCell celula, ItemProposta item)
    {
        var (titulo, itens) = EscopoEmTexto.Partes(item, _custo, _moeda, _t);

        var paragrafos = celula.Elements<Paragraph>().ToList();
        if (paragrafos.Count == 0) return;

        Escrever(paragrafos[0], titulo);

        // o código vai numa linha própria, logo abaixo do título: ele é gerado
        // e não faz parte do texto que a equipe edita, senão congelaria junto
        // com a descrição e deixaria de acompanhar o escopo
        if (_custo.Codigo(item, _moeda) is { Length: > 0 } codigo)
        {
            var pCodigo = (Paragraph)paragrafos[0].CloneNode(true);
            Escrever(pCodigo, $"{_t.Codigo}: {codigo}");
            celula.InsertAfter(pCodigo, paragrafos[0]);
        }

        var pInclui = paragrafos.Count > 1 ? paragrafos[1] : null;
        var pItem = paragrafos.Count > 2 ? paragrafos[2] : null;

        if (itens.Count == 0)
        {
            foreach (var sobra in paragrafos.Skip(1)) sobra.Remove();
            return;
        }

        if (pInclui is not null) Escrever(pInclui, _t.Inclui);

        if (pItem is not null)
        {
            foreach (var texto in itens)
            {
                var novo = (Paragraph)pItem.CloneNode(true);
                Escrever(novo, texto);
                celula.InsertBefore(novo, pItem);
            }
            pItem.Remove();
        }

        foreach (var sobra in paragrafos.Skip(3)) sobra.Remove();
    }

    // ================= impostos, pagamento, prazo e entrega =================

    private void Impostos()
    {
        if (Achar("Impuestos") is { } titulo) Fixo(titulo, _t.Impostos);
        if (Achar("Impuestos o retenciones") is { } semImpostos) Fixo(semImpostos, _t.SemImpostos);

        // o modelo traz seis Tariff Codes, cada um com a instrução de apagar os
        // outros; ventilador é o 84.14.59.90
        var doVentilador = Achar("84.14.59.90");
        var ultimo = Ultimo("8418.99.00");
        if (doVentilador is not null && ultimo is not null) ApagarEntre(doVentilador, ultimo);
        if (doVentilador is not null) Escrever(doVentilador, _t.TariffCode);
    }

    private void Pagamento()
    {
        if (Achar("Condiciones de Pago") is { } titulo) Fixo(titulo, _t.CondicoesDePagamento);

        // o modelo traz as faixas por valor da proposta, para quem preenche
        // escolher; fica só a condição desta proposta
        var faixa = Achar("Propuestas con valor");
        var escolhida = Achar("100% – 30 días de la retirada");
        var fim = Ultimo("aprobación del data book");

        if (escolhida is null || fim is null) return;

        ApagarEntre(escolhida, fim);
        faixa?.Remove();

        Escrever(escolhida, PropostaWord.Ou(_p.CondicaoPagamento, "—"));
    }

    private void Prazo()
    {
        if (Achar("Plazo de Entrega") is { } titulo) Fixo(titulo, _t.PrazoDeEntrega);

        if (Achar("ver tabla abajo") is { } prazo)
            Escrever(prazo, string.Format(_t.TextoDoPrazo, PropostaWord.Ou(_p.PrazoEntregaDias, "--")));

        // a tabela de prazos é a "tabla abajo" que o modelo citava: com o prazo
        // escrito, ela não tem mais a quem servir
        Tabelas().FirstOrDefault(t => Texto(t).Contains("Pré documentados", StringComparison.Ordinal))?.Remove();
    }

    private void Entrega()
    {
        if (Achar("INCOTERMS 2020") is { } titulo) Fixo(titulo, _t.CondicoesDeEntrega);

        Achar("BORRAR CUADRO INCOTERMS")?.Remove();

        var codigos = ListasDaProposta.Incoterms.Select(i => i.Codigo).ToArray();
        var paragrafos = codigos
            .Select(c => Achar(c + " –") ?? Achar(c + "–"))
            .ToList();

        var escolhido = Array.FindIndex(codigos, c => Textos.Igual(c, _p.Incoterm));
        var linha = LinhaDaEntrega(escolhido);

        // fica um parágrafo só: o do incoterm escolhido, ou o primeiro quando a
        // equipe digitou um que não está na lista
        var fica = escolhido >= 0 ? escolhido : 0;
        for (var i = 0; i < paragrafos.Count; i++)
        {
            if (paragrafos[i] is null) continue;
            if (i == fica) Escrever(paragrafos[i]!, linha);
            else paragrafos[i]!.Remove();
        }

        Armado();
    }

    /// <summary>A linha da entrega, na língua da proposta.</summary>
    private string LinhaDaEntrega(int escolhido)
    {
        if (escolhido < 0) return PropostaWord.Ou(_p.Incoterm, "—");

        var modelo = ListasDaProposta.Incoterms[escolhido];
        var texto = _t.TextosDosIncoterms[escolhido];

        if (modelo.Destino)
        {
            // o "Puerto de" / "Porto de" já está na frase: digitado de novo no
            // campo do destino, sairia duas vezes
            var destino = ListasDaProposta.SoOLugar(_p.IncotermDestino);
            texto = string.Format(texto, destino.Length > 0 ? destino : "DESTINO");
        }

        return $"{modelo.Codigo} – {texto}";
    }

    private void Armado()
    {
        var armado = Achar("Equipo armado en base de acero");
        var desarmado = Achar("Equipo desarmado, en parte estática");

        var querArmado = Textos.Igual(_p.Armado, "Armado");
        var querDesarmado = Textos.Igual(_p.Armado, "Desarmado");

        if (querArmado)
        {
            desarmado?.Remove();
            if (armado is not null) Fixo(armado, _t.TextoArmado);
        }
        else if (querDesarmado)
        {
            armado?.Remove();
            if (desarmado is not null) Fixo(desarmado, _t.TextoDesarmado);
        }
        // sem escolha os dois ficam, como no modelo: é o que avisa que falta
        // decidir, em vez de a proposta sair calada
    }

    private void Validade()
    {
        if (Achar("Validez") is { } titulo) Fixo(titulo, _t.Validade);

        if (Achar("La validez de nuestra oferta") is { } texto)
        {
            var dias = PropostaWord.Ou(_p.ValidadeDias, "15");
            Escrever(texto, string.Format(_t.TextoDaValidade, dias, _t.PorExtenso(dias)));
        }
    }

    // ================= notas e assessoria =================

    private void Notas()
    {
        if (Achar("Notas") is { } titulo) Fixo(titulo, _t.Notas);

        var ancoras = new[]
        {
            "base económica de día", "utiliza estándares brasileños",
            "consideran solamente las especificaciones", "cantidad total cotizada",
            "Revisiones adicionales", "Exclusión del beneficio cesante", "Fuerza mayor",
        };

        for (var i = 0; i < ancoras.Length; i++)
        {
            if (Achar(ancoras[i]) is not { } nota) continue;

            // a primeira nota leva a data da proposta, então é sempre escrita
            if (i == 0) Escrever(nota, string.Format(_t.TextosDasNotas[0], Hoje()));
            else Fixo(nota, _t.TextosDasNotas[i]);
        }

    }

    /// <summary>
    /// A nota da assessoria técnica SAI da proposta comercial.
    ///
    /// O modelo traz três blocos — um ventilador, de 1 a 4, e de 5 em diante —
    /// para quem monta à mão escolher um e apagar os outros. A equipe decidiu
    /// que o descritivo não entra na proposta comercial: o que o cliente
    /// precisa ver sobre assessoria é a seção "Assessoria Técnica de Campo",
    /// com as diárias, que continua.
    ///
    /// Sai tudo: a nota 8, a instrução de escolha e os três blocos.
    /// </summary>
    private void Assessoria()
    {
        Achar("Asesoría Técnica:")?.Remove();
        Achar("Verificar as quantidades de assistência")?.Remove();

        foreach (var ancora in new[]
        {
            "Un ventilador o un conjunto girante",
            "De 1 a 4 ventiladores",
            "De 5 a mais ventiladores",
        })
        {
            var (cabecalho, paragrafos) = Bloco(ancora);
            cabecalho?.Remove();
            foreach (var p in paragrafos) p.Remove();
        }
    }

    /// <summary>Um bloco da assessoria: o cabeçalho e os três parágrafos dele.</summary>
    private (Paragraph? Cabecalho, List<Paragraph> Paragrafos) Bloco(string ancora)
    {
        var cabecalho = Achar(ancora);
        if (cabecalho is null) return (null, new());

        var paragrafos = new List<Paragraph>();
        var atual = cabecalho.NextSibling();

        while (atual is not null && paragrafos.Count < 3)
        {
            if (atual is Paragraph p && Texto(p).Trim().Length > 0) paragrafos.Add(p);
            atual = atual.NextSibling();
        }

        return (cabecalho, paragrafos);
    }

    /// <summary>
    /// As diárias de assessoria de campo. O modelo traz CLP, USD, EUR e BRL,
    /// um bloco cada; fica o da moeda da proposta.
    /// </summary>
    private void Diarias()
    {
        if (Achar("Asesoría Técnica De Campo") is { } titulo) Fixo(titulo, _t.AssessoriaDeCampo);

        var moedas = new[] { "CLP", "USD", "EUR", "BRL" };
        var nossa = _moeda.Codigo();

        foreach (var codigo in moedas)
        {
            var diaUtil = Achar($"{codigo} 1.800.000,00") ?? Achar($"{codigo} 2.015,00")
                ?? Achar($"{codigo} 1.725,00") ?? Achar($"{codigo} 6.000,00");
            var feriado = Achar($"{codigo} 3.600.000,00") ?? Achar($"{codigo} 4.030,00")
                ?? Achar($"{codigo} 3.450,00") ?? Achar($"{codigo} 12.000,00");

            var rotulo = RotuloDaMoeda(codigo);

            if (codigo == nossa)
            {
                var (util, dobro) = Diaria(_moeda);
                if (diaUtil is not null)
                    Escrever(diaUtil, $"{_t.DiaUtil} {PropostaWord.Dinheiro(util, _moeda)} {_t.SemImpostoSemDespesas}");
                if (feriado is not null)
                    Escrever(feriado, $"{_t.DiaDeFolga} {PropostaWord.Dinheiro(dobro, _moeda)} {_t.SemImpostoSemDespesas}");
                continue;
            }

            diaUtil?.Remove();
            feriado?.Remove();
            rotulo?.Remove();
        }

        // o "Precios válidos…" aparece uma vez por bloco; fica o de cima
        foreach (var repetido in Paragrafos()
                     .Where(p => Texto(p).Contains("Precios válidos para contratación", StringComparison.Ordinal))
                     .Skip(1)
                     .ToList())
        {
            repetido.Remove();
        }

        if (Achar("Precios válidos para contratación") is { } validos) Fixo(validos, _t.PrecosValidos);
    }

    /// <summary>O parágrafo que é só o código da moeda ("CLP", "USD"…).</summary>
    private Paragraph? RotuloDaMoeda(string codigo) => Paragrafos()
        .FirstOrDefault(p => Texto(p).Trim() == codigo);

    /// <summary>
    /// A diária de assessoria de campo, por moeda — os valores do modelo. O
    /// dia de sábado, domingo e feriado é o dobro do dia útil.
    /// </summary>
    private static (decimal Util, decimal Feriado) Diaria(Moeda moeda) => moeda switch
    {
        Moeda.Clp => (1_800_000m, 3_600_000m),
        Moeda.Brl => (6_000m, 12_000m),
        _ => (2_015m, 4_030m),
    };

    private void Fechamento()
    {
        // a lista vem antes do título: traduzir o título primeiro apagaria a
        // âncora em espanhol que acha o começo da lista
        if (!_espanhol) ListaDasNotasGerais();

        if (Achar("Notas Generales:") is { } notasGerais) Fixo(notasGerais, _t.NotasGerais);

        if (Achar("Solicitamos al Cliente la convocatoria") is { } obs) Fixo(obs, _t.ObsDaAssessoria);
        if (Achar("Sistema de Gestión Integrado") is { } sistema) Fixo(sistema, _t.SistemaDeGestao);
        if (Achar("Howden es certificada") is { } certificada) Fixo(certificada, _t.TextoDoSistemaDeGestao);
        if (Achar("Repuestos") is { } reposicao) Fixo(reposicao, _t.Reposicao);
        if (Achar("consideran la adquisición de los repuestos") is { } texto)
            Fixo(texto, _t.TextoDaReposicao);
    }

    private void ListaDasNotasGerais()
    {
        var inicio = Achar("Notas Generales:");
        var fim = Achar("Solicitamos al Cliente la convocatoria");
        if (inicio is null || fim is null) return;

        var entre = Entre(inicio, fim).OfType<Paragraph>()
            .Where(p => Texto(p).Trim().Length > 0)
            .ToList();

        if (entre.Count == 0) return;

        // no modelo a lista mistura marcador, submarcador, negrito e letra
        // miúda, porque ela foi escrita item a item. A nossa lista é uma só, e
        // todos os itens saem do MOLDE do primeiro — senão um item apareceria
        // em negrito e outro em corpo 6, do jeito que o modelo os deixou
        var molde = (Paragraph)entre[0].CloneNode(true);

        for (var i = 0; i < entre.Count; i++)
        {
            if (i >= _t.TextosDasNotasGerais.Length)
            {
                entre[i].Remove();
                continue;
            }

            var novo = (Paragraph)molde.CloneNode(true);
            Escrever(novo, _t.TextosDasNotasGerais[i]);
            entre[i].Parent?.InsertBefore(novo, entre[i]);
            entre[i].Remove();
        }
    }

    // ================= ferramentas =================

    private string Hoje() => DateTime.Now.ToString("dd/MM/yyyy");

    private List<Paragraph> Paragrafos() => _corpo.Elements<Paragraph>().ToList();

    private List<Table> Tabelas() => _corpo.Elements<Table>().ToList();

    /// <summary>O primeiro parágrafo de primeiro nível que contém o trecho.</summary>
    private Paragraph? Achar(string trecho) => Paragrafos()
        .FirstOrDefault(p => Texto(p).Contains(trecho, StringComparison.Ordinal));

    /// <summary>O último — para trechos que o modelo repete.</summary>
    private Paragraph? Ultimo(string trecho) => Paragrafos()
        .LastOrDefault(p => Texto(p).Contains(trecho, StringComparison.Ordinal));

    /// <summary>A célula cujo texto é o rótulo, dentro de uma tabela.</summary>
    private static TableCell? Celula(Table tabela, string rotulo) => tabela.Descendants<TableCell>()
        .FirstOrDefault(c => Texto(c).Trim() == rotulo.Trim());

    private static string Texto(OpenXmlElement e)
    {
        var partes = new List<string>();
        foreach (var n in e.Descendants())
        {
            if (n is Text t) partes.Add(t.Text);
            else if (n is TabChar) partes.Add(" ");
        }
        return string.Join("", partes);
    }

    /// <summary>
    /// Escreve o texto no parágrafo, ficando com a formatação do primeiro run
    /// que tinha texto. O "\n" vira quebra de linha dentro do parágrafo.
    /// </summary>
    private static void Escrever(Paragraph p, string texto)
    {
        var modelo = p.Elements<Run>().FirstOrDefault(r => r.Elements<Text>().Any())
                     ?? p.Elements<Run>().FirstOrDefault();

        var formato = modelo?.RunProperties?.CloneNode(true) as RunProperties;

        p.RemoveAllChildren<Run>();
        p.RemoveAllChildren<Hyperlink>();

        // alguns parágrafos do modelo têm um campo ligado no meio do texto (o
        // nome do cliente na apresentação, a data na primeira nota). Reescrito
        // o parágrafo, o campo sobraria ANTES da frase nova, e a proposta
        // sairia com o valor repetido: "29/09/2026Los precios indicados…"
        foreach (var campo in p.ChildElements.OfType<SdtElement>().ToList()) campo.Remove();

        var run = new Run();
        if (formato is not null) run.Append(formato);

        var primeiro = true;
        foreach (var linha in texto.Split('\n'))
        {
            if (!primeiro) run.Append(new Break());
            run.Append(new Text(linha) { Space = SpaceProcessingModeValues.Preserve });
            primeiro = false;
        }

        p.Append(run);
    }

    /// <summary>Texto fixo: em espanhol o modelo já está certo e não se mexe.</summary>
    private void Fixo(Paragraph p, string texto)
    {
        if (!_espanhol) Escrever(p, texto);
    }

    private void FixoNaCelula(TableCell celula, string texto)
    {
        if (!_espanhol) EscreverNaCelula(celula, new[] { texto });
    }

    private void FixoNasCelulas(TableRow linha, params string[] textos)
    {
        if (_espanhol) return;

        var celulas = linha.Elements<TableCell>().ToList();
        for (var i = 0; i < celulas.Count && i < textos.Length; i++)
            EscreverNaCelula(celulas[i], new[] { textos[i] });
    }

    /// <summary>
    /// Escreve as linhas numa célula, reaproveitando os parágrafos que já estão
    /// lá — é deles que vêm o estilo, o negrito do nome e o alinhamento.
    /// </summary>
    private static void EscreverNaCelula(TableCell celula, IReadOnlyList<string> linhas)
    {
        var paragrafos = celula.Elements<Paragraph>().ToList();
        if (paragrafos.Count == 0)
        {
            celula.Append(new Paragraph());
            paragrafos = celula.Elements<Paragraph>().ToList();
        }

        for (var i = 0; i < linhas.Count; i++)
        {
            if (i < paragrafos.Count)
            {
                Escrever(paragrafos[i], linhas[i]);
                continue;
            }

            var novo = (Paragraph)paragrafos[^1].CloneNode(true);
            Escrever(novo, linhas[i]);
            celula.Append(novo);
        }

        // sobra de parágrafo some, menos o primeiro: uma célula do Word não
        // pode ficar sem nenhum
        foreach (var sobra in paragrafos.Skip(Math.Max(linhas.Count, 1))) sobra.Remove();
    }

    /// <summary>Tudo que está entre dois elementos de primeiro nível, sem eles.</summary>
    private List<OpenXmlElement> Entre(OpenXmlElement de, OpenXmlElement ate)
    {
        var achou = false;
        var lista = new List<OpenXmlElement>();

        foreach (var filho in _corpo.ChildElements)
        {
            if (filho == de) { achou = true; continue; }
            if (filho == ate) break;
            if (achou) lista.Add(filho);
        }

        return lista;
    }

    /// <summary>Apaga tudo entre os dois, inclusive o de baixo.</summary>
    private void ApagarEntre(OpenXmlElement de, OpenXmlElement ate)
    {
        foreach (var elemento in Entre(de, ate)) elemento.Remove();
        ate.Remove();
    }
}
