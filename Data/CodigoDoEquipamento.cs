namespace HowdenAxiais.Poc.Data;

/// <summary>
/// O código de um equipamento da proposta, como a equipe o escreve à mão:
///
/// <code>VP-301800HB1-TLF-0CG2-C1-2DSC-21</code>
///
/// <para>São sete grupos, separados por hífen:</para>
///
/// <list type="number">
/// <item>a linha e o tipo: <b>V</b> de VAX ou <b>A</b> de Joy, mais <b>P</b> de
/// padrão ou <b>E</b> de especial;</item>
/// <item>o código do ventilador, do cadastro de equipamentos;</item>
/// <item>base, lubrificador e contrarrecuo (ou freio);</item>
/// <item>o código do motor elétrico;</item>
/// <item>cone de entrada e silenciador de entrada;</item>
/// <item>silenciador de descarga, difusor, damper e conexão a manga na
/// descarga;</item>
/// <item>partidor e instrumentação.</item>
/// </list>
///
/// <para>
/// Cada posição é SEMPRE uma: o código de quem lê conta as posições, e um
/// grupo curto faz ler a peça errada. Por isso "Sem" vale <b>S</b>, e uma lista
/// que foi escolhida mas não tem código no cadastro sai como <b>?</b> em vez de
/// desaparecer — um código visivelmente furado é melhor que um código errado
/// que parece certo. A tela diz quais listas estão sem código.
/// </para>
/// </summary>
public static class CodigoDoEquipamento
{
    /// <summary>"Sem" — a peça não vai.</summary>
    public const string Sem = "S";

    /// <summary>A peça vai, mas o cadastro não diz com que código.</summary>
    public const string Falta = "?";

    /// <summary>
    /// As posições que saem das listas do escopo, na ordem do código. O nome é
    /// o da tela, para o aviso de "sem código" poder dizer qual lista é.
    /// </summary>
    public static readonly (string Nome, Func<string, bool> Casa)[] Posicoes =
    {
        ("Base", l => l.Contains("base")),
        ("Lubrificador", l => l.Contains("lubrific")),
        ("Contrarrecuo ou freio", l => l.Contains("contrarrecuo") || l.Contains("freio")),

        // "conexao" contém "cone": sem o segundo teste, a lista da manga
        // ocuparia a posição do cone de entrada
        ("Cone de entrada", l => l.Contains("cone") && !l.Contains("conexao")),
        ("Silenciador de entrada", l => Silenciador(l) && !Descarga(l)),

        ("Silenciador de descarga", l => Silenciador(l) && Descarga(l)),
        ("Difusor", l => l.Contains("difusor")),
        ("Damper", l => l.Contains("damper") || l.Contains("mariposa")),
        ("Conexão a manga na descarga", l => l.Contains("manga")),
    };

    private static bool Silenciador(string l) => l.Contains("silenciador");

    private static bool Descarga(string l) => l.Contains("descarga") || l.Contains("saida");

    /// <summary>Onde cada posição entra: o índice do grupo do código.</summary>
    private static readonly int[] GrupoDaPosicao = { 2, 2, 2, 4, 4, 5, 5, 5, 5 };

    public static string De(ItemProposta item, CustoDaProposta custo, Moeda moeda)
    {
        var modelo = custo.Modelo(item);

        // sete grupos, montados por índice para a ordem não depender de quem lê
        var grupos = new string[7];

        grupos[0] = Linha(modelo) + (item.Especial ? "E" : "P");
        grupos[1] = Pedaco(modelo?.Codigo);
        grupos[3] = item.ComMotor ? Pedaco(custo.MotorDe(item)?.Codigo) : Sem;

        for (var i = 0; i < Posicoes.Length; i++)
            grupos[GrupoDaPosicao[i]] += DaLista(item, custo, moeda, Posicoes[i].Casa);

        grupos[6] = Partidor(item, custo, moeda) + Instrumentacao(item, custo, moeda);

        return string.Join("-", grupos);
    }

    /// <summary>
    /// As listas que foram escolhidas e não têm código no cadastro — é o que a
    /// tela mostra para a equipe saber onde o "?" do código nasceu.
    /// </summary>
    public static List<string> SemCodigo(ItemProposta item, CustoDaProposta custo, Moeda moeda)
    {
        var faltam = new List<string>();

        foreach (var (nome, casa) in Posicoes)
            if (DaLista(item, custo, moeda, casa) == Falta) faltam.Add(nome);

        if (custo.Modelo(item) is { Codigo.Length: 0 }) faltam.Insert(0, "Ventilador");

        if (item.ComMotor && custo.MotorDe(item) is { Codigo.Length: 0 })
            faltam.Add("Motor elétrico");

        if (Partidor(item, custo, moeda) == Falta) faltam.Add("Partidor");
        if (Instrumentacao(item, custo, moeda).Contains(Falta)) faltam.Add("Instrumentação");

        return faltam;
    }

    /// <summary>V de VAX, A de Joy.</summary>
    private static string Linha(Equipamento? modelo)
    {
        var serie = Textos.Simples(modelo?.Serie ?? "");

        if (serie.Contains("vax")) return "V";
        if (serie.Contains("joy")) return "A";

        return Falta;
    }

    /// <summary>Um código do cadastro, ou "?" quando ele está em branco.</summary>
    private static string Pedaco(string? codigo) =>
        codigo is { } c && c.Trim().Length > 0 ? c.Trim() : Falta;

    /// <summary>
    /// A posição de uma lista do escopo. Vazio quando o cadastro não tem essa
    /// lista — aí a posição não existe para ninguém, e não há o que marcar.
    /// </summary>
    private static string DaLista(ItemProposta item, CustoDaProposta custo, Moeda moeda,
        Func<string, bool> casa)
    {
        var lista = custo.ListasDoEscopo.FirstOrDefault(l => casa(Textos.Simples(l)));
        if (lista is null) return "";

        var escolha = item.Escolhas.GetValueOrDefault(lista, "").Trim();

        // lista ainda não decidida vale como "não vai": é o que ela quer dizer
        // numa proposta em que ninguém a marcou
        if (escolha.Length == 0) return Sem;

        var linha = custo.Resolver(item, lista, escolha, moeda);

        if (linha.Codigo.Trim().Length > 0) return linha.Codigo.Trim();

        return linha.Ausencia ? Sem : Falta;
    }

    private static string Partidor(ItemProposta item, CustoDaProposta custo, Moeda moeda)
    {
        if (custo.LinhaDoPartidor(item, moeda) is not { Ausencia: false } linha) return Sem;

        return Pedaco(linha.Codigo);
    }

    private static string Instrumentacao(ItemProposta item, CustoDaProposta custo, Moeda moeda)
    {
        if (!item.ComInstrumentacao) return Sem;

        var nomes = item.Instrumentacao.Where(n => n.Trim().Length > 0).ToList();
        if (nomes.Count == 0) return Sem;

        // mais de um sensor: os códigos saem juntos, na ordem em que foram
        // marcados
        return string.Concat(nomes.Select(n =>
            Pedaco(custo.Resolver(item, custo.ListaDaInstrumentacao, n, moeda).Codigo)));
    }
}
