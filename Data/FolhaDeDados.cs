using ClosedXML.Excel;

namespace HowdenAxiais.Poc.Data;

/// <summary>
/// A leitura da <b>folha de dados</b> da equipe: a planilha que já é preenchida
/// hoje, de onde a proposta puxa o que dá para puxar.
///
/// As posições são fixas porque o formulário é fixo — a planilha é um
/// formulário, não uma tabela. Cada coluna a partir da D é um equipamento, e a
/// linha 31 diz a quantidade: número quer dizer "existe, e são tantos"; a
/// palavra "preencher" (ou qualquer coisa que não seja número) quer dizer que
/// aquela coluna não tem equipamento.
/// </summary>
public static class FolhaDeDados
{
    // ---------- onde cada coisa mora ----------
    private const string CelulaNumero = "D4";
    private const string CelulaCliente = "D6";
    private const string CelulaAosCuidados = "D13";
    private const string CelulaEmail = "D14";
    private const string CelulaTelefone = "D15";

    private const int LinhaQuantidade = 31;
    private const int LinhaArranjo = 40;
    private const int LinhaDifusor = 42;
    private const int LinhaDamper = 44;
    private const int LinhaContrarrecuo = 51;

    /// <summary>Primeira e última coluna de equipamento: D até O, doze colunas.</summary>
    private const int PrimeiraColuna = 4;
    private const int UltimaColuna = 15;

    /// <summary>Os campos do cabeçalho que a planilha preenche, para a tela dizer.</summary>
    public static readonly Dictionary<string, string> CamposDaPlanilha = new()
    {
        ["Número da proposta"] = CelulaNumero,
        ["Cliente"] = CelulaCliente,
        ["Aos cuidados de"] = CelulaAosCuidados,
        ["E-mail"] = CelulaEmail,
        ["Telefone"] = CelulaTelefone,
    };

    /// <summary>As listas de característica que a planilha preenche, e em que linha.</summary>
    public static readonly (string Lista, int Linha)[] ListasDaPlanilha =
    {
        ("Difusor", LinhaDifusor),
        ("Damper mariposa", LinhaDamper),
        ("Contrarrecuo", LinhaContrarrecuo),
    };

    public sealed record Resultado(int Equipamentos, List<string> Avisos, List<string> Manuais);

    /// <summary>
    /// Lê a planilha para dentro da proposta. O que a folha não traz fica como
    /// estava — e volta na lista <c>Manuais</c>, para a tela dizer o que ainda
    /// precisa de mão.
    /// </summary>
    public static Resultado Ler(Stream arquivo, Proposta proposta,
        List<Caracteristica> caracteristicas, List<Equipamento> equipamentos)
    {
        using var wb = new XLWorkbook(arquivo);

        var avisos = new List<string>();
        var ws = Escolher(wb, avisos);

        // ---------- cabeçalho ----------
        var numero = Texto(ws, CelulaNumero);
        var cliente = Texto(ws, CelulaCliente);
        var aosCuidados = Texto(ws, CelulaAosCuidados);
        var email = Texto(ws, CelulaEmail);
        var telefone = Texto(ws, CelulaTelefone);

        if (numero.Length > 0) proposta.Numero = numero;
        if (cliente.Length > 0) proposta.Cliente = cliente;
        if (aosCuidados.Length > 0) proposta.AosCuidados = aosCuidados;
        if (email.Length > 0) proposta.Email = email;
        if (telefone.Length > 0) proposta.Telefone = telefone;

        foreach (var (campo, celula) in CamposDaPlanilha)
        {
            if (Texto(ws, celula).Length == 0)
                avisos.Add($"{celula} ({campo}) veio vazia — o campo ficou como estava.");
        }

        // ---------- os equipamentos, uma coluna cada ----------
        var porGrupo = caracteristicas.GroupBy(c => c.Grupo)
            .ToDictionary(g => g.Key, g => g.ToList());

        var itens = new List<ItemProposta>();

        for (var col = PrimeiraColuna; col <= UltimaColuna; col++)
        {
            var quantidade = Texto(ws, col, LinhaQuantidade);
            if (DadosExcel.Numero(quantidade) is not { } quantos || quantos <= 0) continue;

            var letra = XLHelper.GetColumnLetterFromNumber(col);
            var item = new ItemProposta
            {
                Quantidade = ((int)quantos).ToString(),
                Coluna = letra,
            };

            // arranjo: teto ou piso
            var arranjo = Texto(ws, col, LinhaArranjo);
            if (arranjo.Length > 0)
            {
                var achado = ListasDaProposta.Arranjos
                    .FirstOrDefault(a => Parecido(arranjo, a));

                if (achado is not null) item.Arranjo = achado;
                else avisos.Add($"Coluna {letra}, linha {LinhaArranjo}: \"{arranjo}\" não é Teto nem Piso.");
            }

            // as listas de característica
            foreach (var (lista, linha) in ListasDaPlanilha)
            {
                var texto = Texto(ws, col, linha);
                if (texto.Length == 0) continue;

                var opcoes = porGrupo.GetValueOrDefault(lista, new());
                if (opcoes.Count == 0)
                {
                    avisos.Add($"A lista \"{lista}\" não existe no cadastro — coluna {letra} ignorada.");
                    continue;
                }

                var escolha = Casar(texto, opcoes);
                if (escolha is null)
                {
                    avisos.Add($"Coluna {letra}, linha {linha} ({lista}): não entendi \"{texto}\" — escolha na tela.");
                    continue;
                }

                item.Escolhas[lista] = escolha;
            }

            itens.Add(item);
        }

        if (itens.Count > 0) proposta.Itens = itens;
        else avisos.Add($"Nenhuma coluna da linha {LinhaQuantidade} tinha um número — nenhum equipamento foi criado.");

        return new Resultado(itens.Count, avisos, Manuais(itens, caracteristicas));
    }

    /// <summary>
    /// O que a planilha NÃO traz e continua sendo de mão — o que a tela mostra
    /// depois de importar, para ninguém mandar uma proposta pela metade.
    /// </summary>
    private static List<string> Manuais(List<ItemProposta> itens, List<Caracteristica> caracteristicas)
    {
        var manuais = new List<string>
        {
            "Cabeçalho: País, Projeto, datas, Fase, BU, idioma, venda para, destino, " +
            "categoria, produto, segmento e portal",
            "Contato do documento (nome, cargo, e-mail e telefones)",
            "Moeda da proposta",
            "O modelo de cada equipamento — a folha diz a quantidade, não qual ventilador",
        };

        var daPlanilha = ListasDaPlanilha.Select(l => l.Lista).ToHashSet();
        var outras = caracteristicas
            .Select(c => c.Grupo)
            .Distinct()
            .Where(g => !daPlanilha.Contains(g))
            .ToList();

        if (outras.Count > 0)
            manuais.Add("Listas que a folha não traz: " + string.Join(", ", outras));

        return manuais;
    }

    /// <summary>
    /// Casa o texto da planilha com uma opção da lista. Primeiro pelo nome;
    /// depois pelo sentido — "sem"/"não" é a opção de ausência, e "com"/"sim" é
    /// a opção real, quando só existe uma.
    /// </summary>
    public static string? Casar(string texto, List<Caracteristica> opcoes)
    {
        var alvo = Textos.Simples(texto);
        if (alvo.Length == 0) return null;

        // 1) o próprio nome da opção
        var exata = opcoes.FirstOrDefault(o => Textos.Simples(o.Valor) == alvo);
        if (exata is not null) return exata.Valor;

        var contida = opcoes.FirstOrDefault(o =>
            Textos.Simples(o.Valor).Contains(alvo) || alvo.Contains(Textos.Simples(o.Valor)));

        // "sem" casaria com "sem contrarrecuo" por conter, o que é certo; mas
        // também casaria com qualquer coisa curta demais, então só vale de 3
        // letras para cima
        if (contida is not null && alvo.Length >= 3) return contida.Valor;

        // 2) o sentido
        var ausencia = opcoes.FirstOrDefault(o => FamiliaDePreco.EhAusencia(o.Valor));
        var reais = opcoes.Where(o => !FamiliaDePreco.EhAusencia(o.Valor)).ToList();

        if (EhNao(alvo)) return ausencia?.Valor;
        if (EhSim(alvo)) return reais.Count == 1 ? reais[0].Valor : null;

        return null;
    }

    private static bool EhNao(string t) =>
        t is "sem" or "nao" or "n" or "no" or "none" or "nenhum" or "-" or "x";

    private static bool EhSim(string t) =>
        t is "com" or "sim" or "s" or "yes" or "com item";

    /// <summary>Compara sem acento, sem caixa e sem espaço sobrando.</summary>
    private static bool Parecido(string a, string b) => Textos.Igual(a, b);

    /// <summary>
    /// Qual aba ler. A folha costuma ter mais de uma (instruções, listas,
    /// revisões), e a primeira nem sempre é a do formulário — então vale a
    /// primeira que tenha alguma das células do cabeçalho preenchida, ou algum
    /// número na linha das quantidades.
    /// </summary>
    private static IXLWorksheet Escolher(XLWorkbook wb, List<string> avisos)
    {
        var abas = wb.Worksheets.ToList();

        foreach (var aba in abas)
        {
            var temCabecalho = CamposDaPlanilha.Values.Any(c => Texto(aba, c).Length > 0);

            var temQuantidade = Enumerable.Range(PrimeiraColuna, UltimaColuna - PrimeiraColuna + 1)
                .Any(col => DadosExcel.Numero(Texto(aba, col, LinhaQuantidade)) is > 0);

            if (!temCabecalho && !temQuantidade) continue;

            if (aba != abas[0])
                avisos.Add($"Li a aba \"{aba.Name}\" — é a que tem os campos do formulário.");

            return aba;
        }

        avisos.Add($"Nenhuma aba tinha as células esperadas. O arquivo tem: " +
                   string.Join(", ", abas.Select(a => $"\"{a.Name}\"")) +
                   $". Confira se o cliente está mesmo em {CelulaCliente} e a quantidade na " +
                   $"linha {LinhaQuantidade}.");

        return abas[0];
    }

    /// <summary>
    /// O texto de uma célula.
    ///
    /// Numa folha de formulário é comum o campo ser um bloco de células
    /// mescladas: o valor mora só na primeira do bloco, e as outras leem vazio.
    /// Por isso, célula vazia que faz parte de uma mesclagem devolve o valor da
    /// mesclagem — é o que a pessoa vê na tela do Excel.
    /// </summary>
    private static string Texto(IXLWorksheet ws, string celula) => Texto(ws.Cell(celula));

    private static string Texto(IXLWorksheet ws, int coluna, int linha) =>
        Texto(ws.Cell(linha, coluna));

    private static string Texto(IXLCell celula)
    {
        var direto = celula.GetFormattedString().Trim();
        if (direto.Length > 0) return direto;

        return celula.IsMerged()
            ? celula.MergedRange().FirstCell().GetFormattedString().Trim()
            : "";
    }
}
