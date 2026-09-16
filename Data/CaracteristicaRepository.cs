namespace HowdenAxiais.Poc.Data;

/// <summary>
/// Um item de uma lista de característica do equipamento (solidez, base,
/// partidor, instrumentação…).
///
/// O <see cref="Codigo"/> é o que a equipe vai subir depois: juntando os
/// códigos individuais escolhidos em cada lista, monta-se o código do
/// equipamento. Por isso a ORDEM DOS GRUPOS importa — é a ordem em que os
/// pedaços entram nesse código.
/// </summary>
public sealed class Caracteristica
{
    public string Id { get; set; } = "";
    /// <summary>Nome da lista: "Solidez", "Base", "PARTIDORES"…</summary>
    public string Grupo { get; set; } = "";
    /// <summary>A opção, como a equipe escreve: "Com TRENÓ", "VDF IP65".</summary>
    public string Valor { get; set; } = "";
    /// <summary>Código do item. Vazio enquanto a equipe não subir os códigos.</summary>
    public string Codigo { get; set; } = "";
    /// <summary>Posição dentro do grupo (1 = primeiro da lista).</summary>
    public int Ordem { get; set; }

    public static string MontarId(string grupo, string valor) => $"{grupo}|{valor}";
}

/// <summary>
/// Cadastro das listas de características (entidade "caracteristicas"), sobre o
/// mesmo ParquetStore do resto do sistema.
/// </summary>
public sealed class CaracteristicaRepository
{
    private const string Entidade = "caracteristicas";

    private readonly ParquetStore _store;
    public CaracteristicaRepository(ParquetStore store) => _store = store;

    public List<Caracteristica> Todas() => _store
        .ReadLatest(Entidade, "id, grupo, valor, codigo, ordem", r => new Caracteristica
        {
            Id = S(r, 0), Grupo = S(r, 1), Valor = S(r, 2), Codigo = S(r, 3), Ordem = Int(S(r, 4)),
        })
        .OrderBy(c => CaracteristicasSeed.PosicaoDoGrupo(c.Grupo))
        .ThenBy(c => c.Grupo)
        .ThenBy(c => c.Ordem)
        .ToList();

    public void Salvar(Caracteristica c)
    {
        if (string.IsNullOrWhiteSpace(c.Id)) c.Id = Caracteristica.MontarId(c.Grupo, c.Valor);
        _store.WriteRow(Entidade, new KeyValuePair<string, object?>[]
        {
            new("id", c.Id), new("grupo", c.Grupo), new("valor", c.Valor),
            new("codigo", c.Codigo),
            // zeros à esquerda: o Parquet guarda texto e sem isso a 10 viria antes da 2
            new("ordem", c.Ordem.ToString("D4")),
        });
    }

    public void Apagar(string id) => _store.WriteRow(Entidade,
        new KeyValuePair<string, object?>[] { new("id", id) }, deleted: true);

    /// <summary>
    /// Carrega as listas de fábrica dos GRUPOS que ainda não existem. Por grupo,
    /// e não pela entidade inteira: uma lista nova entra sem tocar nas que a
    /// equipe já ajustou ou já codificou.
    /// </summary>
    public void SemearSeVazio()
    {
        var gruposExistentes = Todas().Select(c => c.Grupo).ToHashSet();

        foreach (var c in CaracteristicasSeed.Lista())
        {
            if (gruposExistentes.Contains(c.Grupo)) continue;
            Salvar(c);
        }
    }

    private static string S(System.Data.IDataReader r, int i) => r.IsDBNull(i) ? "" : r.GetString(i);

    private static int Int(string s) => int.TryParse(s, out var v) ? v : 0;
}

/// <summary>
/// As listas de fábrica, transcritas do documento de características do
/// equipamento. A ordem dos grupos é a do documento — e é a candidata natural
/// à ordem dos pedaços no código do equipamento.
/// </summary>
public static class CaracteristicasSeed
{
    /// <summary>Os grupos na ordem do documento.</summary>
    public static readonly string[] Grupos =
    {
        "Solidez",
        "# Estágios",
        "Base",
        "Lubrificação",
        "Contrarrecuo",
        "Polaridade e freq Motor",
        "Potencia Motor CV [kW]",
        "Forn. Motor e Flange",
        "Cone de entrada",
        "Silenciador entrada",
        "Silenciador descarga",
        "Difusor",
        "Conexao manga descarga",
        "PARTIDORES",
        "INSTRUMENTAÇÃO",
    };

    public static int PosicaoDoGrupo(string grupo)
    {
        var i = Array.IndexOf(Grupos, grupo);
        return i >= 0 ? i : Grupos.Length;
    }

    private static readonly string[] Silenciador =
    {
        "Sem", "L = 1,0D", "L = 1,5D", "L = 2,0D",
        "L = 1,0D C/NUCLEO", "L = 1,5D C/NUCLEO", "L = 2,0D C/NUCLEO",
    };

    public static List<Caracteristica> Lista()
    {
        var tabela = new (string Grupo, string[] Valores)[]
        {
            ("Solidez", new[] { "FB", "HB" }),

            ("# Estágios", new[] { "1STG", "2STG" }),

            ("Base", new[] { "SEM Base", "Com BASE", "Com TRENÓ" }),

            ("Lubrificação", new[] { "SEM lubrif. automatico", "com LUBRIF. automatico" }),

            ("Contrarrecuo", new[] { "Sem Contrarrecuo", "Freio" }),

            ("Polaridade e freq Motor", new[]
            {
                "3000 rpm / 50 Hz", "1500 rpm / 50 Hz", "1000 rpm / 50 Hz", "750 rpm / 50 Hz",
                "600 rpm / 50 Hz", "500 rpm / 50 Hz", "428,6 rpm / 50 Hz",
                "3600 rpm / 60 Hz", "1800 rpm / 60 Hz", "1200 rpm / 60 Hz", "900 rpm / 60 Hz",
                "720 rpm / 60 Hz", "600 rpm / 60 Hz", "514,3 rpm / 60 Hz",
            }),

            ("Potencia Motor CV [kW]", new[]
            {
                "< 5 [3,7]", "5 [3,7]", "5,5 [4,1]", "6 [4,5]", "7,5 [5,5]", "10 [7,5]",
                "12,5 [9,2]", "15 [11]", "20 [15]", "25 [18,5]", "30 [22]", "40 [30]",
                "50 [37]", "60 [45]", "75 [55]", "100 [75]", "125 [90]", "150 [110]",
                "175 [132]", "200 [150]", "250 [185]", "300 [220]", "350 [260]", "380 [285]",
                "400 [300]", "430 [320]", "450 [330]", "480 [360]", "500 [370]", "550 [400]",
            }),

            ("Forn. Motor e Flange", new[]
            {
                "WEG FC", "WEG FF", "OMEC", "ABLE", "ABB", "SIEMENS", "WOLONG",
            }),

            ("Cone de entrada", new[] { "Sem", "Com Cone", "Com Conexão para Manga" }),

            ("Silenciador entrada", Silenciador),
            ("Silenciador descarga", Silenciador),

            ("Difusor", new[] { "Sem", "Com" }),

            ("Conexao manga descarga", new[] { "Sem", "Com Conexão para Manga" }),

            ("PARTIDORES", new[]
            {
                "NENHUM", "DOL IP65", "ESTRELA-TRIANGULO IP65", "SOFTSTARTER IP54",
                "SOFTSTARTER IP65", "VDF IP54", "VDF IP65", "ESPECIAL",
            }),

            ("INSTRUMENTAÇÃO", new[]
            {
                "NENHUM",
                "SENSOR DE VIBRAÇÃO",
                "SENSOR DE PRESSÃO DIFERENCIAL",
                "SENSOR DE VAZÃO",
                "SENSORES VIBRAÇÃO + PRESSÃO DIFFERENCIAL",
                "SENSORES VIBRAÇÃO + VAZÃO",
                "SENSORES PRESSÃO DIFFERENCIAL+ VAZÃO",
                "SENSORES VIBRAÇÃO + PRESSÃO DIFFERENCIAL+ VAZÃO",
            }),
        };

        var lista = new List<Caracteristica>();
        foreach (var (grupo, valores) in tabela)
        {
            for (var i = 0; i < valores.Length; i++)
            {
                lista.Add(new Caracteristica
                {
                    Id = Caracteristica.MontarId(grupo, valores[i]),
                    Grupo = grupo,
                    Valor = valores[i],
                    Codigo = "",
                    Ordem = i + 1,
                });
            }
        }
        return lista;
    }
}
