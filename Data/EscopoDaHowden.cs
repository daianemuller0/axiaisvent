namespace HowdenAxiais.Poc.Data;

/// <summary>
/// A lista de "INCLUSO NO ESCOPO DA HOWDEN" de um equipamento, montada a
/// partir da seleção dele.
///
/// O modelo da equipe traz o catálogo inteiro e quem preenche risca o que não
/// vai. Aqui é o contrário: a seleção do equipamento já diz o que vai, então o
/// sistema escreve só essas linhas — na ordem do catálogo, para o documento
/// sair na ordem que vocês estão acostumados a ler.
///
/// O catálogo é maior que as listas de características: "cámara anti-stall",
/// "cojinete monobloc", "acople elástico" e companhia não são escolhidos em
/// nenhuma lista do cadastro, então não há de onde o sistema os tirar. É por
/// isso que a tela deixa o texto editável e oferece o resto do catálogo ao
/// lado: o rascunho é o que o sistema SABE, e o complemento é de vocês.
/// </summary>
public static class EscopoDaHowden
{
    /// <summary>
    /// A ordem do catálogo no modelo. Vale para a lista do documento e para os
    /// botões de complemento na tela.
    /// </summary>
    public static readonly string[] Ordem =
    {
        "ventilador", "base", "treno", "difusor", "cone", "lubrificador",
        "damper", "atuador", "silenciadorEntrada", "silenciadorDescarga",
        "grelhaSaida", "mangaEntrada", "mangaDescarga", "colarEntrada",
        "colarDescarga", "transicaoEntrada", "transicaoDescarga", "caixaEntrada",
        "dutos", "bifurcacao", "antiStall", "acople", "monobloc", "backStop",
        "chumbadores", "freio", "motor",
    };

    /// <summary>
    /// A chave do catálogo que uma linha do escopo descreve, ou vazio quando o
    /// catálogo não tem linha para ela (aí vai o texto da própria opção).
    ///
    /// Casa pela LISTA e, quando a lista tem mais de um destino, pela opção:
    /// "Cone de entrada" vira cone ou conexão a manga conforme o que foi
    /// escolhido, e "Contrarrecuo" vira back stop ou freio.
    /// </summary>
    public static string Chave(string lista, string opcao)
    {
        var l = Textos.Simples(lista);
        var o = Textos.Simples(opcao);

        if (l.Contains("base")) return o.Contains("treno") ? "treno" : "base";
        if (l.Contains("lubrific")) return "lubrificador";

        // a lista do contrarrecuo tem três estados, e dois deles são peças
        // diferentes — quem manda é a opção
        if (l.Contains("contrarrecuo") || l.Contains("freio"))
            return o.Contains("freio") ? "freio" : "backStop";

        // a manga vem ANTES do cone de propósito: "conexao manga descarga"
        // contém "cone" dentro de "conexao", e a lista da manga cairia na
        // linha do cone de admissão
        if (l.Contains("manga"))
            return l.Contains("descarga") || l.Contains("saida") ? "mangaDescarga"
                                                                 : "mangaEntrada";

        // o cone leva manga quando é essa a opção: na folha de dados a conexão
        // da admissão é uma das escolhas da lista "Cone de entrada"
        if (l.Contains("cone")) return o.Contains("manga") ? "mangaEntrada" : "cone";

        if (l.Contains("silenciador"))
            return l.Contains("entrada") || l.Contains("admiss") ? "silenciadorEntrada"
                                                                 : "silenciadorDescarga";
        if (l.Contains("difusor")) return "difusor";
        if (l.Contains("damper") || l.Contains("mariposa")) return "damper";

        return "";
    }

    /// <summary>
    /// O nome de catálogo de uma peça, para ela ser chamada do mesmo jeito no
    /// escopo e nos itens opcionais. Sem lista (o motor, a instrumentação) ou
    /// sem linha no catálogo, fica o nome que vocês deram.
    /// </summary>
    public static string Nome(string lista, string opcao, TextosDoEscopo textos)
    {
        if (lista.Trim().Length == 0) return opcao.Trim();

        return Chave(lista, opcao) is { Length: > 0 } chave && textos.Catalogo.ContainsKey(chave)
            ? textos.Catalogo[chave]
            : $"{lista.Trim()} · {opcao.Trim()}";
    }

    /// <summary>
    /// O rascunho: as linhas do catálogo que a seleção do equipamento pede,
    /// mais o partidor e a instrumentação, que saem com o nome que vocês deram
    /// a eles porque o catálogo não os nomeia.
    /// </summary>
    public static List<string> De(ItemProposta item, CustoDaProposta custo, Moeda moeda,
        TextosDoEscopo textos)
    {
        // o ventilador em si não está em lista nenhuma: ele é o equipamento
        var chaves = new HashSet<string> { "ventilador" };
        var livres = new List<string>();

        foreach (var linha in custo.Itens(item, moeda))
        {
            if (FamiliaDePreco.EhAusencia(linha.Opcao)) continue;

            // o design (padrão ou especial) não é peça: não entra no escopo
            if (EhDesign(linha.Lista)) continue;

            if (Chave(linha.Lista, linha.Opcao) is { Length: > 0 } chave) chaves.Add(chave);
            else livres.Add($"{linha.Lista.Trim()} {linha.Opcao.Trim()}".Trim());
        }

        var motor = custo.MotorDe(item);
        if (motor is not null) chaves.Add("motor");

        // as peças que vocês juntaram à mão: entram pela chave e saem na
        // ordem do catálogo, junto com as que a seleção escolheu
        foreach (var chave in item.EscopoExtra)
            if (textos.Catalogo.ContainsKey(chave)) chaves.Add(chave);

        var lista = Ordem.Where(chaves.Contains)
            .Select(c => c == "motor" ? ComOMotor(textos.Catalogo[c], motor) : textos.Catalogo[c])
            .ToList();

        lista.AddRange(livres);

        if (custo.LinhaDoPartidor(item, moeda) is { Ausencia: false } partidor)
            lista.Add(partidor.Opcao.Trim());

        if (item.ComInstrumentacao)
            lista.AddRange(item.Instrumentacao.Where(n => n.Trim().Length > 0).Select(n => n.Trim()));

        // pintura e embalagem vão em todo equipamento
        lista.AddRange(textos.Padrao);

        return lista;
    }

    /// <summary>
    /// "DESIGN Padrão" / "DESIGN Especial", como a lista do design saía no
    /// rascunho antigo. Só essas: uma linha digitada à mão que comece com
    /// "design" (um desenho, por exemplo) é da equipe e fica.
    /// </summary>
    private static bool LinhaDoDesign(string linha) =>
        Textos.Simples(linha) is "design padrao" or "design especial"
            or "design standard" or "design special";

    /// <summary>A lista do design, que a equipe não quer no escopo.</summary>
    private static bool EhDesign(string lista) => Textos.Simples(lista).Contains("design");

    /// <summary>
    /// A linha do motor com o motor escolhido ao lado: "Motor elétrico — 250M
    /// 150 CV".
    ///
    /// O catálogo do modelo diz só "Motor Eléctrico", mas quem lê a proposta
    /// quer saber QUAL motor vai — o frame e a potência. O FABRICANTE não sai:
    /// a Howden não informa quem fabrica o motor.
    /// </summary>
    private static string ComOMotor(string linha, Motor? motor)
    {
        if (motor is null) return linha;

        var descricao = motor.NoDocumento;

        return descricao.Length > 0 ? $"{linha} — {descricao}" : linha;
    }

    /// <summary>O rascunho em texto, que é o que a tela deixa editar.</summary>
    public static string Rascunho(ItemProposta item, CustoDaProposta custo, Moeda moeda,
        TextosDoEscopo textos) =>
        string.Join("\n", De(item, custo, moeda, textos).Select(l => "- " + l));

    /// <summary>
    /// A lista que vale: a de vocês, quando escreveram; o rascunho, quando não.
    /// Mesma regra do preço e da descrição comercial. Pintura e embalagem entram
    /// sempre, mesmo na lista escrita à mão.
    /// </summary>
    public static List<string> Efetivo(ItemProposta item, CustoDaProposta custo, Moeda moeda,
        TextosDoEscopo textos)
    {
        if (item.EscopoIncluso.Trim().Length == 0) return De(item, custo, moeda, textos);

        // o texto da equipe pode ser de antes destas regras: tira o design que
        // ele ainda carregue e garante o que é padrão em todo escopo
        var linhas = Linhas(item.EscopoIncluso)
            .Where(l => !LinhaDoDesign(l))
            .ToList();

        foreach (var padrao in textos.Padrao)
            if (!linhas.Any(l => Textos.Igual(l, padrao))) linhas.Add(padrao);

        return linhas;
    }

    /// <summary>
    /// Um texto de tela virando lista de marcadores. Aceita com e sem o traço
    /// na frente, porque na tela a equipe digita dos dois jeitos.
    /// </summary>
    public static List<string> Linhas(string texto) => texto.Replace("\r", "")
        .Split('\n')
        .Select(l => l.Trim())
        .Where(l => l.Length > 0)
        .Select(l => l.StartsWith('-') ? l[1..].Trim() : l)
        .Where(l => l.Length > 0)
        .ToList();
}
