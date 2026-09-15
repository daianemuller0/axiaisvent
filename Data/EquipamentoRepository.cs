using System.Globalization;
using System.Text.RegularExpressions;

namespace HowdenAxiais.Poc.Data;

/// <summary>
/// Uma combinação válida de equipamento: o ventilador cabendo num cubo, com o
/// limite de rotação daquela combinação.
///
/// Na tabela original (Tabela VAX-JOY), é cada célula pintada de amarelo: o
/// cruzamento "Fan Diameter × Fan Hub Diameter". O número dentro da célula é a
/// coluna V-Belt — o teto de rotação.
///
/// Diâmetro e cubo são TEXTO, não número, porque as duas linhas descrevem de
/// jeitos diferentes: o VAX usa milímetros redondos (2400, cubo 1800) e o Joy
/// usa polegadas com fração e o modelo do cubo (18 1/4, cubo 14", S1000). Para
/// ordenar na tela, <see cref="Medida.Numero"/> extrai o valor numérico do
/// rótulo.
/// </summary>
public sealed class Equipamento
{
    public string Id { get; set; } = "";
    /// <summary>Linha de equipamento: VAX ou Joy.</summary>
    public string Serie { get; set; } = "";
    /// <summary>Fan Diameter, como aparece na tabela ("2400", "18 1/4").</summary>
    public string Diametro { get; set; } = "";
    /// <summary>Fan Hub Diameter, como aparece na tabela ("1800", "14\", S1000").</summary>
    public string Cubo { get; set; } = "";
    /// <summary>Rotação máxima (V-Belt), em rpm. Acima disso, alerta.</summary>
    public int RpmMax { get; set; }

    public static string MontarId(string serie, string diametro, string cubo) =>
        $"{serie}-{diametro}-{cubo}";
}

/// <summary>
/// Leitura do valor numérico de um rótulo de medida, só para ordenar a tela:
/// "2400" → 2400; "18 1/4" → 18,25; "17 1/2\", S1000" → 17,5.
/// </summary>
public static class Medida
{
    private static readonly Regex Padrao =
        new(@"^(\d+(?:[.,]\d+)?)(?:\s+(\d+)\s*/\s*(\d+))?", RegexOptions.Compiled);

    public static double Numero(string rotulo)
    {
        if (string.IsNullOrWhiteSpace(rotulo)) return 0;

        var m = Padrao.Match(rotulo.Trim());
        if (!m.Success) return 0;

        var valor = double.Parse(m.Groups[1].Value.Replace(',', '.'), CultureInfo.InvariantCulture);

        // parte fracionária no formato das polegadas: "18 1/4"
        if (m.Groups[2].Success &&
            double.TryParse(m.Groups[2].Value, out var num) &&
            double.TryParse(m.Groups[3].Value, out var den) && den != 0)
        {
            valor += num / den;
        }

        return valor;
    }
}

/// <summary>
/// Os equipamentos e a relação ventilador × cubo, sobre o mesmo ParquetStore do
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
            Id = S(r, 0), Serie = S(r, 1), Diametro = S(r, 2),
            Cubo = S(r, 3), RpmMax = Int(S(r, 4)),
        })
        .OrderBy(e => e.Serie)
        .ThenBy(e => Medida.Numero(e.Diametro))
        .ThenBy(e => Medida.Numero(e.Cubo))
        .ToList();

    public void Salvar(Equipamento e)
    {
        if (string.IsNullOrWhiteSpace(e.Id)) e.Id = Equipamento.MontarId(e.Serie, e.Diametro, e.Cubo);
        _store.WriteRow(Entidade, new KeyValuePair<string, object?>[]
        {
            new("id", e.Id), new("serie", e.Serie),
            new("diametro", e.Diametro), new("cubo", e.Cubo),
            new("rpmMax", e.RpmMax.ToString()),
        });
    }

    public void Apagar(string id) => _store.WriteRow(Entidade,
        new KeyValuePair<string, object?>[] { new("id", id) }, deleted: true);

    /// <summary>
    /// Carrega a tabela de fábrica de cada linha que ainda não existe no banco.
    /// É por SÉRIE, e não pela entidade inteira: assim uma linha nova (o Joy)
    /// entra num banco que já tem a outra (o VAX), sem tocar no que está lá.
    /// </summary>
    public void SemearSeVazio()
    {
        var existentes = Todos().Select(e => e.Serie).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var (serie, tabela) in EquipamentosSeed.Todas())
        {
            if (existentes.Contains(serie)) continue;
            foreach (var e in tabela) Salvar(e);
        }
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
    ///  1. a combinação ventilador × cubo existe? (a célula amarela da tabela)
    ///  2. a rotação pedida passa do teto de V-Belt daquela combinação?
    /// </summary>
    public static Resultado Verificar(List<Equipamento> equipamentos, string serie,
        string diametro, string cubo, int rpm)
    {
        var achado = equipamentos.FirstOrDefault(e =>
            e.Serie == serie && e.Diametro == diametro && e.Cubo == cubo);

        if (achado is null)
        {
            var cubos = equipamentos
                .Where(e => e.Serie == serie && e.Diametro == diametro)
                .Select(e => e.Cubo)
                .ToList();

            var alternativa = cubos.Count == 0
                ? "Esse ventilador não existe na tabela."
                : $"Para o ventilador {diametro}, os cubos possíveis são: {string.Join(" · ", cubos)}.";

            return new Resultado(Situacao.ForaDeFaixa,
                $"O ventilador {diametro} não cabe no cubo {cubo}. {alternativa}", null);
        }

        if (rpm > achado.RpmMax)
        {
            return new Resultado(Situacao.AcimaDaRotacao,
                $"Rotação de {rpm} rpm acima do limite: o {serie} {diametro} no cubo {cubo} " +
                $"admite no máximo {achado.RpmMax} rpm (V-Belt).", achado);
        }

        return new Resultado(Situacao.Ok,
            $"Combinação válida: {rpm} rpm dentro do limite de {achado.RpmMax} rpm (V-Belt).", achado);
    }
}

/// <summary>
/// Tabelas de fábrica, transcritas da planilha "Tabela VAX-JOY" (aba Página 1).
/// Cada bloco é um Fan Hub Diameter; os pares são "ventilador → rotação máxima
/// da coluna V-Belt".
/// </summary>
public static class EquipamentosSeed
{
    public static IEnumerable<(string Serie, List<Equipamento> Tabela)> Todas()
    {
        yield return ("VAX", Vax());
        yield return ("Joy", Joy());
    }

    /// <summary>
    /// VAX — medidas em milímetros.
    ///
    /// Conferência: dentro de um mesmo cubo, rotação × diâmetro é praticamente
    /// constante (1800 ≈ 10.695.000; 2100 ≈ 11.459.000; 2700 e 3150 ≈
    /// 10.314.000) — foi assim que o alinhamento das linhas foi validado.
    /// </summary>
    public static List<Equipamento> Vax() => Montar("VAX", new (string, (string, int)[])[]
    {
        ("1800", new[]
        {
            ("2400", 4456), ("2500", 4278), ("2800", 3820), ("3000", 3565), ("3200", 3342),
            ("3400", 3146), ("3600", 2971), ("3800", 2815), ("4200", 2546), ("4500", 2377),
            ("4800", 2228),
        }),
        ("2100", new[]
        {
            ("2800", 4093), ("3000", 3820), ("3200", 3581), ("3400", 3370), ("3600", 3183),
            ("3800", 3016), ("4200", 2728), ("4500", 2546), ("4800", 2387), ("5400", 2122),
        }),
        ("2700", new[]
        {
            ("3600", 2865), ("3800", 2714), ("4200", 2456), ("4500", 2292), ("4800", 2149),
            ("5400", 1910), ("6000", 1719), ("6600", 1563), ("7200", 1432),
        }),
        ("3150", new[]
        {
            ("4500", 2292), ("4800", 2149), ("5400", 1910), ("6000", 1719), ("6600", 1563),
            ("7200", 1432), ("7800", 1322), ("8400", 1228),
        }),
    });

    /// <summary>
    /// Joy — mesma estrutura do VAX, mudando a descrição do cubo (diâmetro em
    /// polegadas + modelo, ex.: 14", S1000) e o modelo do equipamento
    /// (polegadas com fração, ex.: 18 1/4).
    ///
    /// PARCIAL: transcrito da foto, que corta na coluna N. Faltam os cubos à
    /// direita do 21", S2200 e, com eles, os ventiladores de 54 a 85.
    /// </summary>
    public static List<Equipamento> Joy() => Montar("Joy", new (string, (string, int)[])[]
    {
        ("14\", S1000", new[]
        {
            ("18 1/4", 3600), ("21 1/4", 3600), ("23 1/4", 3600), ("25 1/4", 3600),
            ("27 1/7", 3600), ("29 1/4", 3600), ("32", 3600), ("34", 3200), ("36", 3000),
        }),
        ("17 1/2\", S1000", new[]
        {
            ("21 1/4", 3600), ("23 1/4", 3600), ("25 1/4", 3600), ("27 1/7", 3200),
            ("29 1/4", 3000), ("32", 2800), ("34", 2600), ("36", 2400), ("38", 2200),
            ("42 1/4", 2000), ("45", 1800),
        }),
        ("21\", S2200", new[]
        {
            ("25 1/4", 3600), ("27 1/7", 3600), ("29 1/4", 3600), ("32", 3200),
            ("34", 3000), ("36", 2900), ("38", 2700), ("42 1/4", 2500), ("45", 2300),
            ("48", 2100),
        }),
    });

    private static List<Equipamento> Montar(string serie, (string Cubo, (string Diametro, int Rpm)[] Linhas)[] tabela) =>
        (from bloco in tabela
         from linha in bloco.Linhas
         select new Equipamento
         {
             Id = Equipamento.MontarId(serie, linha.Diametro, bloco.Cubo),
             Serie = serie,
             Diametro = linha.Diametro,
             Cubo = bloco.Cubo,
             RpmMax = linha.Rpm,
         }).ToList();
}
