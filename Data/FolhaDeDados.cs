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

    /// <summary>
    /// As listas de característica que a planilha preenche, e em que linha.
    ///
    /// A lista é achada por <b>palavra</b>, e não pelo nome inteiro: a equipe
    /// renomeia ("Contrarrecuo ou Freio", "Damper mariposa saída") e o nome
    /// exato deixaria a escolha cair no chão sem ninguém ver.
    /// </summary>
    public static readonly (string Palavra, string Rotulo, int Linha)[] ListasDaPlanilha =
    {
        ("difusor", "Difusor", LinhaDifusor),
        ("damper", "Damper", LinhaDamper),
        ("contrarrecuo", "Contrarrecuo ou freio", LinhaContrarrecuo),
    };

    /// <summary>A lista do cadastro que atende a uma palavra da folha.</summary>
    private static string? ListaDoCadastro(string palavra, IEnumerable<string> listas) =>
        listas.FirstOrDefault(l => Textos.Simples(l).Contains(palavra))
        ?? (palavra == "contrarrecuo"
            ? listas.FirstOrDefault(l => Textos.Simples(l).Contains("freio"))
            : null);

    /// <param name="Preenchidos">
    /// Os campos que a folha realmente trouxe, para a tela mostrar uma linha só.
    /// O que ela não traz já está dito no rótulo de cada campo — repetir a lista
    /// inteira a cada importação era só barulho.
    /// </param>
    public sealed record Resultado(int Equipamentos, List<string> Avisos, List<string> Preenchidos);

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

        var preenchidos = new List<string>();

        void Trazer(string campo, string celula, string valor, Action<string> guardar)
        {
            if (EmBranco(valor)) return;

            guardar(valor);
            preenchidos.Add($"{campo} ({celula})");
        }

        Trazer("Número da proposta", CelulaNumero, numero, v => proposta.Numero = v);
        Trazer("Cliente", CelulaCliente, cliente, v => proposta.Cliente = v);
        Trazer("Aos cuidados de", CelulaAosCuidados, aosCuidados, v => proposta.AosCuidados = v);
        Trazer("E-mail", CelulaEmail, email, v => proposta.Email = v);
        Trazer("Telefone", CelulaTelefone, telefone, v => proposta.Telefone = v);

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
            if (!EmBranco(arranjo))
            {
                var achado = ListasDaProposta.Arranjos
                    .FirstOrDefault(a => Parecido(arranjo, a));

                if (achado is not null) item.Arranjo = achado;
                else avisos.Add($"Arranjo (coluna {letra}): \"{arranjo}\" não é Teto nem Piso.");
            }

            // as listas de característica
            foreach (var (palavra, rotulo, linha) in ListasDaPlanilha)
            {
                var texto = Texto(ws, col, linha);
                if (EmBranco(texto)) continue;

                var lista = ListaDoCadastro(palavra, porGrupo.Keys);
                if (lista is null)
                {
                    avisos.Add($"{rotulo}: não há lista parecida no cadastro, então a coluna " +
                               $"{letra} ficou sem essa escolha.");
                    continue;
                }

                var escolha = Casar(texto, porGrupo[lista]);
                if (escolha is null)
                {
                    avisos.Add($"{lista} (coluna {letra}): não entendi \"{texto}\" — escolha na tela.");
                    continue;
                }

                item.Escolhas[lista] = escolha;
            }

            itens.Add(item);
        }

        if (itens.Count > 0)
        {
            proposta.Itens = itens;
            preenchidos.Add($"{itens.Count} equipamento(s), um por coluna");
        }
        else
        {
            avisos.Add($"Nenhuma coluna da linha {LinhaQuantidade} tinha um número — " +
                       "nenhum equipamento foi criado.");
        }

        return new Resultado(itens.Count, avisos, preenchidos);
    }

    /// <summary>
    /// Célula que não diz nada: vazia, ou com o texto que a folha usa como
    /// espaço para preencher — "Preencher", "Escolher", "N/A" — ou com a lista
    /// de opções ainda intacta ("sim / não", "não / admissão / descarga").
    /// </summary>
    private static bool EmBranco(string texto)
    {
        var t = Textos.Simples(texto);
        if (t.Length == 0) return true;

        if (t is "preencher" or "escolher" or "n/a" or "na" or "-" or "--") return true;

        // a folha deixa as opções escritas na célula até alguém escolher uma
        return t.Contains(" / ");
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
