namespace HowdenAxiais.Poc.Data;

/// <summary>
/// Transforma o escopo de um equipamento na lista que sai embaixo de
/// "Incluye:" na proposta comercial.
///
/// O resumo da tela e a proposta não dizem a mesma coisa. O resumo é um
/// conferidor: mostra a lista e a opção marcada em cada uma ("Cone de entrada
/// · Com Cone"), inclusive as que não levam nada. A proposta é uma descrição
/// para o cliente, e nela "Com Cone" ao lado de "Cone de entrada" é repetição.
///
/// O que fica de cada linha foi tirado do exemplo que a equipe marcou à mão:
///
/// <list type="bullet">
/// <item>o que não vai ("Sem Cone", "Não") não aparece;</item>
/// <item>a medida solta ("L = 1,00") sai, e fica a lista ("Silenciador entrada");</item>
/// <item>"Com" e "Sim" saem — quem está na lista, vai;</item>
/// <item>numa lista de escolha ("Contrarrecuo ou Freio") fica a ESCOLHA ("Freio");</item>
/// <item>motor, partidor e instrumentação saem pelo nome próprio, sem o rótulo.</item>
/// </list>
///
/// Isto é um RASCUNHO: a tela mostra o texto e a equipe corrige antes de gerar,
/// porque nenhuma regra acerta a descrição de todo equipamento.
/// </summary>
public static class EscopoEmTexto
{
    /// <summary>Listas que não descrevem o equipamento para o cliente.</summary>
    private static readonly string[] Fora = { "design" };

    /// <summary>Palavras que só marcam presença, e não descrevem nada.</summary>
    private static readonly string[] Vazias =
    {
        "com", "sim", "c/", "x",
        // ligação: não é o que a opção acrescenta ao rótulo
        "de", "da", "do", "das", "dos", "em", "na", "no", "para", "e", "a", "o",
    };

    /// <summary>
    /// A descrição de um equipamento: a linha do ventilador e o que ele inclui.
    /// </summary>
    public static (string Titulo, List<string> Inclui) De(
        ItemProposta item, CustoDaProposta custo, Moeda moeda, TextosDaProposta textos)
    {
        var modelo = custo.Modelo(item);
        var teto = Textos.Simples(item.Arranjo).Contains("teto");

        var titulo = string.Format(textos.LinhaDoVentilador,
            modelo?.Rotulo ?? "—",
            teto ? textos.Vertical : textos.Horizontal,
            teto ? textos.NoTeto : textos.NoPiso);

        var inclui = new List<string>();

        foreach (var linha in custo.Itens(item, moeda))
        {
            if (Linha(linha.Lista, linha.Opcao) is { Length: > 0 } texto) inclui.Add(texto);
        }

        if (custo.MotorDe(item) is { } motor)
            inclui.Add($"{motor.Fabricante} {motor.Frame} {motor.PotenciaCv} CV".Trim());

        if (custo.LinhaDoPartidor(item, moeda) is { } partidor && !partidor.Ausencia)
            inclui.Add(partidor.Opcao.Trim());

        if (item.ComInstrumentacao)
        {
            foreach (var nome in item.Instrumentacao.Where(n => n.Trim().Length > 0))
                inclui.Add(nome.Trim());
        }

        return (titulo, inclui);
    }

    /// <summary>A descrição inteira em texto, que é o que a tela deixa editar.</summary>
    public static string Texto(ItemProposta item, CustoDaProposta custo, Moeda moeda,
        TextosDaProposta textos)
    {
        var (titulo, inclui) = De(item, custo, moeda, textos);

        // sem nada escolhido, "Inclui:" sozinho só faz o cliente procurar a
        // lista que não existe
        if (inclui.Count == 0) return titulo;

        return string.Join("\n",
            new[] { titulo, textos.Inclui }.Concat(inclui.Select(i => "- " + i)));
    }

    /// <summary>
    /// As partes da descrição que vale — o título e a lista.
    ///
    /// Quando a equipe escreveu a dela, é o TEXTO DELA que é lido de volta em
    /// partes: a primeira linha é o título, e cada linha que começa com "-" é
    /// um item. É o que permite editar na tela e o documento sair com os
    /// marcadores do modelo, em vez de um parágrafo só com traços dentro.
    /// </summary>
    public static (string Titulo, List<string> Inclui) Partes(ItemProposta item,
        CustoDaProposta custo, Moeda moeda, TextosDaProposta textos)
    {
        if (item.Descricao.Trim().Length == 0) return De(item, custo, moeda, textos);

        var linhas = item.Descricao.Trim().Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToList();

        if (linhas.Count == 0) return De(item, custo, moeda, textos);

        var titulo = linhas[0];
        var inclui = linhas.Skip(1)
            .Where(l => !EhOInclui(l, textos))
            .Select(l => l.StartsWith('-') ? l[1..].Trim() : l)
            .ToList();

        return (titulo, inclui);
    }

    /// <summary>A linha "Inclui:" em qualquer das três línguas.</summary>
    private static bool EhOInclui(string linha, TextosDaProposta textos) =>
        Textos.Igual(linha, textos.Inclui)
        || Textos.Igual(linha, "Incluye:")
        || Textos.Igual(linha, "Inclui:")
        || Textos.Igual(linha, "Includes:");

    /// <summary>
    /// A descrição que vale: a que a equipe escreveu, quando escreveu; o
    /// rascunho, quando não. Mesma regra do preço — puxa, mas dá para corrigir.
    /// </summary>
    public static string Efetivo(ItemProposta item, CustoDaProposta custo, Moeda moeda,
        TextosDaProposta textos) =>
        item.Descricao.Trim().Length > 0 ? item.Descricao.Trim() : Texto(item, custo, moeda, textos);

    /// <summary>Uma linha do escopo, ou vazio quando ela não entra.</summary>
    private static string Linha(string lista, string opcao)
    {
        if (FamiliaDePreco.EhAusencia(opcao)) return "";
        if (Fora.Any(f => Textos.Simples(lista).Contains(f))) return "";

        // "Contrarrecuo ou Freio" é uma escolha ENTRE duas coisas: o rótulo não
        // descreve o que foi vendido, só a opção descreve
        if (Textos.Simples(lista).Contains(" ou ")) return opcao.Trim();

        var extra = Sobra(lista, opcao);

        // nada além do rótulo: "Cone de entrada / Com Cone" vira "Cone de entrada"
        if (extra.Length == 0) return lista.Trim();

        // a opção diz algo que o rótulo não diz ("Com TRENÓ"): os dois ficam
        return $"{lista.Trim()} {opcao.Trim()}";
    }

    /// <summary>
    /// O que a opção diz e o rótulo não. Não conta o que só marca presença
    /// ("Com", "Sim") nem medida ("L = 1,00", "02x"), que é o que a equipe
    /// riscou no exemplo.
    /// </summary>
    private static string Sobra(string lista, string opcao)
    {
        var noRotulo = Palavras(lista);

        var sobra = Palavras(opcao)
            .Where(p => !Vazias.Contains(p))
            .Where(p => !Medida(p))
            .Where(p => !JaEstaNoRotulo(p, noRotulo));

        return string.Join(" ", sobra);
    }

    private static List<string> Palavras(string texto) => Textos.Simples(texto)
        .Split(' ', StringSplitOptions.RemoveEmptyEntries)
        .Select(p => p.Trim('.', ',', ';', ':', '-', '(', ')'))
        .Where(p => p.Length > 0)
        .ToList();

    /// <summary>
    /// A palavra já está no rótulo — inclusive abreviada. A equipe escreve
    /// "com LUBRIF. automático" numa lista chamada "Lubrificador automático":
    /// "lubrif" é a mesma palavra, e repeti-la na proposta não diz nada.
    /// </summary>
    private static bool JaEstaNoRotulo(string palavra, List<string> rotulo) => rotulo
        .Any(r => r == palavra
            || (palavra.Length >= 4 && r.StartsWith(palavra, StringComparison.Ordinal))
            || (r.Length >= 4 && palavra.StartsWith(r, StringComparison.Ordinal)));

    /// <summary>
    /// "L", "=", "02x", "1,0D" — medida, e não descrição. É o comprimento do
    /// silenciador e a quantidade do lubrificador, que a equipe riscou do
    /// exemplo: quem lê a proposta quer saber que TEM silenciador.
    /// </summary>
    private static bool Medida(string palavra) =>
        palavra.Length <= 1
        || char.IsDigit(palavra[0])
        || !palavra.Any(char.IsLetter);
}
