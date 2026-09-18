namespace HowdenAxiais.Poc.Data;

/// <summary>
/// Um frame (carcaça) de motor, no padrão IEC ou NEMA.
///
/// A <see cref="Ordem"/> é o dado mais importante depois do nome: a lista não é
/// alfabética, é uma ESCADA DE TAMANHO (&lt; 112M → 355A/B no IEC; 254T →
/// 588/9T no NEMA). É ela que vai permitir responder "esse frame passa do
/// máximo que cabe no cubo?" — comparar texto não resolveria.
/// </summary>
public sealed class FrameMotor
{
    public string Id { get; set; } = "";
    /// <summary>Padrão do frame: IEC ou NEMA.</summary>
    public string Padrao { get; set; } = "";
    /// <summary>Nome do frame como a equipe escreve: "225S/M", "364/5T".</summary>
    public string Nome { get; set; } = "";
    /// <summary>Posição na escada de tamanho, dentro do padrão (1 = o menor).</summary>
    public int Ordem { get; set; }
    /// <summary>Código do frame, que entra na montagem do código do equipamento.</summary>
    public string Codigo { get; set; } = "";
    /// <summary>Preço do frame, como a equipe digita. Vazio = sem preço.</summary>
    public string Preco { get; set; } = "";

    public static string MontarId(string padrao, string nome) => $"{padrao}-{nome}";
}

/// <summary>
/// Cadastro dos frames de motor (entidade "frames"), sobre o mesmo ParquetStore
/// do resto do sistema.
/// </summary>
public sealed class FrameRepository
{
    private const string Entidade = "frames";

    /// <summary>Padrões conhecidos, na ordem em que aparecem na lista da equipe.</summary>
    public static readonly string[] Padroes = { "IEC", "NEMA" };

    private readonly ParquetStore _store;
    public FrameRepository(ParquetStore store) => _store = store;

    public List<FrameMotor> Todos() => _store
        .ReadLatest(Entidade, "id, padrao, nome, ordem, codigo, preco", r => new FrameMotor
        {
            Id = S(r, 0), Padrao = S(r, 1), Nome = S(r, 2), Ordem = Int(S(r, 3)),
            Codigo = S(r, 4), Preco = S(r, 5),
        })
        .OrderBy(f => PosicaoDoPadrao(f.Padrao))
        .ThenBy(f => f.Ordem)
        .ToList();

    public static int PosicaoDoPadrao(string padrao)
    {
        var i = Array.IndexOf(Padroes, padrao);
        return i >= 0 ? i : Padroes.Length;
    }

    public void Salvar(FrameMotor f)
    {
        if (string.IsNullOrWhiteSpace(f.Id)) f.Id = FrameMotor.MontarId(f.Padrao, f.Nome);
        _store.WriteRow(Entidade, new KeyValuePair<string, object?>[]
        {
            new("id", f.Id), new("padrao", f.Padrao), new("nome", f.Nome),
            // ordem com zeros à esquerda: o Parquet guarda texto, e sem isso a
            // posição 10 viria antes da 2.
            new("ordem", f.Ordem.ToString("D4")),
            new("codigo", f.Codigo), new("preco", f.Preco),
        });
    }

    public void Apagar(string id) => _store.WriteRow(Entidade,
        new KeyValuePair<string, object?>[] { new("id", id) }, deleted: true);

    /// <summary>Carrega a lista de fábrica na primeira execução (entidade vazia).</summary>
    public void SemearSeVazio()
    {
        if (!_store.IsEmpty(Entidade)) return;
        foreach (var f in FramesSeed.Lista()) Salvar(f);
    }

    private static string S(System.Data.IDataReader r, int i) => r.IsDBNull(i) ? "" : r.GetString(i);

    private static int Int(string s) => int.TryParse(s, out var v) ? v : 0;
}

/// <summary>
/// Lista de fábrica dos frames, na ordem de tamanho que a equipe usa —
/// 19 frames IEC e 15 NEMA.
/// </summary>
public static class FramesSeed
{
    private static readonly string[] Iec =
    {
        "< 112M", "112M", "132S", "132M", "132M/L", "160M", "160L", "180M", "180L",
        "200M", "200L", "225S/M", "250S/M", "280S/M", "315S/M", "315M/L", "315L",
        "355M/L", "355A/B",
    };

    private static readonly string[] Nema =
    {
        "254T", "254/6T", "284T", "284/6T", "324T", "324/6T", "326T", "364/5T",
        "404/5T", "444/5T", "445/7T", "447/9T", "504/5T", "586/7T", "588/9T",
    };

    public static List<FrameMotor> Lista()
    {
        var lista = new List<FrameMotor>();
        foreach (var (padrao, nomes) in new[] { ("IEC", Iec), ("NEMA", Nema) })
        {
            for (var i = 0; i < nomes.Length; i++)
            {
                lista.Add(new FrameMotor
                {
                    Id = FrameMotor.MontarId(padrao, nomes[i]),
                    Padrao = padrao,
                    Nome = nomes[i],
                    Ordem = i + 1,
                });
            }
        }
        return lista;
    }
}
