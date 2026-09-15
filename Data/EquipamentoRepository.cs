namespace HowdenAxiais.Poc.Data;

/// <summary>
/// Uma combinação válida de equipamento: o diâmetro do ventilador cabendo num
/// diâmetro de cubo, com o limite de rotação daquela combinação.
///
/// Na tabela original (Tabela VAX-JOY), é cada célula pintada de amarelo: o
/// cruzamento "Fan Diameter × Fan Hub Diameter". O número dentro da célula é a
/// coluna V-Belt — o teto de rotação.
/// </summary>
public sealed class Equipamento
{
    public string Id { get; set; } = "";
    /// <summary>Linha de equipamento: VAX ou Joy.</summary>
    public string Serie { get; set; } = "";
    /// <summary>Fan Diameter, em mm.</summary>
    public int Diametro { get; set; }
    /// <summary>Fan Hub Diameter, em mm.</summary>
    public int Cubo { get; set; }
    /// <summary>Rotação máxima (V-Belt), em rpm. Acima disso, alerta.</summary>
    public int RpmMax { get; set; }

    public static string MontarId(string serie, int diametro, int cubo) =>
        $"{serie}-{diametro}-{cubo}";
}

/// <summary>
/// Os equipamentos e a relação diâmetro × cubo, sobre o mesmo ParquetStore do
/// resto do sistema (entidade "equipamentos").
///
/// Só as combinações VÁLIDAS ficam gravadas — a ausência da linha é o "não
/// cabe". É o que deixa a regra simples de consultar: achou, cabe; não achou,
/// não cabe.
/// </summary>
public sealed class EquipamentoRepository
{
    private const string Entidade = "equipamentos";

    private readonly ParquetStore _store;
    public EquipamentoRepository(ParquetStore store) => _store = store;

    public List<Equipamento> Todos() => _store
        .ReadLatest(Entidade, "id, serie, diametro, cubo, rpmMax", r => new Equipamento
        {
            Id = S(r, 0), Serie = S(r, 1), Diametro = Int(S(r, 2)),
            Cubo = Int(S(r, 3)), RpmMax = Int(S(r, 4)),
        })
        .OrderBy(e => e.Serie)
        .ThenBy(e => e.Diametro)
        .ThenBy(e => e.Cubo)
        .ToList();

    public void Salvar(Equipamento e)
    {
        if (string.IsNullOrWhiteSpace(e.Id)) e.Id = Equipamento.MontarId(e.Serie, e.Diametro, e.Cubo);
        _store.WriteRow(Entidade, new KeyValuePair<string, object?>[]
        {
            new("id", e.Id), new("serie", e.Serie),
            new("diametro", e.Diametro.ToString()), new("cubo", e.Cubo.ToString()),
            new("rpmMax", e.RpmMax.ToString()),
        });
    }

    public void Apagar(string id) => _store.WriteRow(Entidade,
        new KeyValuePair<string, object?>[] { new("id", id) }, deleted: true);

    /// <summary>Carrega a tabela de fábrica na primeira execução (banco vazio).</summary>
    public void SemearSeVazio()
    {
        if (!_store.IsEmpty(Entidade)) return;
        foreach (var e in EquipamentosSeed.Vax()) Salvar(e);
    }

    private static string S(System.Data.IDataReader r, int i) => r.IsDBNull(i) ? "" : r.GetString(i);

    private static int Int(string s) => int.TryParse(s, out var v) ? v : 0;
}

/// <summary>
/// A regra de seleção, em forma de consulta: dada a combinação e a rotação
/// pedida, diz se pode e o que avisar.
/// </summary>
public static class RegraRotacao
{
    public enum Situacao { Ok, ForaDeFaixa, AcimaDaRotacao }

    public sealed record Resultado(Situacao Situacao, string Mensagem, Equipamento? Equipamento);

    /// <summary>
    /// Duas checagens, nesta ordem:
    ///  1. a combinação diâmetro × cubo existe? (a célula amarela da tabela)
    ///  2. a rotação pedida passa do teto de V-Belt daquela combinação?
    /// </summary>
    public static Resultado Verificar(List<Equipamento> equipamentos, string serie,
        int diametro, int cubo, int rpm)
    {
        var achado = equipamentos.FirstOrDefault(e =>
            e.Serie == serie && e.Diametro == diametro && e.Cubo == cubo);

        if (achado is null)
        {
            var cubos = equipamentos
                .Where(e => e.Serie == serie && e.Diametro == diametro)
                .Select(e => e.Cubo.ToString())
                .ToList();

            var alternativa = cubos.Count == 0
                ? "Esse diâmetro não existe na tabela."
                : $"Para o ventilador de {diametro} mm, os cubos possíveis são: {string.Join(", ", cubos)}.";

            return new Resultado(Situacao.ForaDeFaixa,
                $"O ventilador de {diametro} mm não cabe no cubo de {cubo} mm. {alternativa}", null);
        }

        if (rpm > achado.RpmMax)
        {
            return new Resultado(Situacao.AcimaDaRotacao,
                $"Rotação de {rpm} rpm acima do limite: o {serie} de {diametro} mm no cubo de " +
                $"{cubo} mm admite no máximo {achado.RpmMax} rpm (V-Belt).", achado);
        }

        return new Resultado(Situacao.Ok,
            $"Combinação válida: {rpm} rpm dentro do limite de {achado.RpmMax} rpm (V-Belt).", achado);
    }
}

/// <summary>
/// Tabela de fábrica, transcrita da planilha "Tabela VAX-JOY" (aba Página 1).
/// Cada bloco é um Fan Hub Diameter; os pares são "diâmetro do ventilador →
/// rotação máxima V-Belt".
///
/// Conferência: dentro de um mesmo cubo, rotação × diâmetro é praticamente
/// constante (1800 ≈ 10.695.000; 2100 ≈ 11.459.000; 2700 e 3150 ≈ 10.314.000)
/// — foi assim que o alinhamento das linhas foi validado na transcrição.
/// </summary>
public static class EquipamentosSeed
{
    public static List<Equipamento> Vax()
    {
        var tabela = new (int Cubo, (int Diametro, int Rpm)[] Linhas)[]
        {
            (1800, new[]
            {
                (2400, 4456), (2500, 4278), (2800, 3820), (3000, 3565), (3200, 3342),
                (3400, 3146), (3600, 2971), (3800, 2815), (4200, 2546), (4500, 2377),
                (4800, 2228),
            }),
            (2100, new[]
            {
                (2800, 4093), (3000, 3820), (3200, 3581), (3400, 3370), (3600, 3183),
                (3800, 3016), (4200, 2728), (4500, 2546), (4800, 2387), (5400, 2122),
            }),
            (2700, new[]
            {
                (3600, 2865), (3800, 2714), (4200, 2456), (4500, 2292), (4800, 2149),
                (5400, 1910), (6000, 1719), (6600, 1563), (7200, 1432),
            }),
            (3150, new[]
            {
                (4500, 2292), (4800, 2149), (5400, 1910), (6000, 1719), (6600, 1563),
                (7200, 1432), (7800, 1322), (8400, 1228),
            }),
        };

        return (from bloco in tabela
                from linha in bloco.Linhas
                select new Equipamento
                {
                    Id = Equipamento.MontarId("VAX", linha.Diametro, bloco.Cubo),
                    Serie = "VAX",
                    Diametro = linha.Diametro,
                    Cubo = bloco.Cubo,
                    RpmMax = linha.Rpm,
                }).ToList();
    }
}
