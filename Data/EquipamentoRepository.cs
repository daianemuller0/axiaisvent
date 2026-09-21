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
    /// <summary>
    /// Código do equipamento — o da COMBINAÇÃO, não o do ventilador nem o do
    /// cubo. Quem vende é o par: o 3000 no cubo 1800 e o 3000 no cubo 2100 são
    /// dois equipamentos diferentes, com código e preço próprios.
    /// </summary>
    public string Codigo { get; set; } = "";
    /// <summary>
    /// Preço em reais, como a equipe digita. Vazio = sem preço.
    /// É o campo antigo <c>preco</c>: o que já estava gravado continua aqui.
    /// </summary>
    public string Preco { get; set; } = "";
    /// <summary>Preço em dólar.</summary>
    public string PrecoUsd { get; set; } = "";
    /// <summary>Preço em peso chileno.</summary>
    public string PrecoClp { get; set; } = "";

    /// <summary>FB/HB, como a equipe escreve. Vazio = ainda não definido.</summary>
    public string FbHb { get; set; } = "";
    /// <summary>Número de estágios do equipamento: "1" ou "2".</summary>
    public string Estagios { get; set; } = "";

    /// <summary>
    /// O maior frame IEC que cabe nesta combinação. Vazio = usar o limite do
    /// cubo (a tabela "Motor máximo por cubo"), que é o cadastro mais grosso.
    /// </summary>
    public string FrameMaxIec { get; set; } = "";
    /// <summary>O maior frame NEMA que cabe nesta combinação.</summary>
    public string FrameMaxNema { get; set; } = "";

    /// <summary>O frame máximo de um padrão, ou vazio se não houver.</summary>
    public string FrameMaximo(string padrao) =>
        padrao.Equals("NEMA", StringComparison.OrdinalIgnoreCase) ? FrameMaxNema : FrameMaxIec;

    /// <summary>Os estagiamentos possíveis, como a equipe preenche.</summary>
    public static readonly string[] Estagiamentos = { "1", "2" };

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
    /// <summary>Marcas das correções já aplicadas, para não repetirem.</summary>
    private const string EntidadeMigracoes = "equipamentos_migracoes";
    private const string EntidadeBlocos = "equipamentos_blocos";

    private readonly ParquetStore _store;
    public EquipamentoRepository(ParquetStore store) => _store = store;

    public List<Equipamento> Todos() => _store
        .ReadLatest(Entidade,
            "id, serie, diametro, cubo, rpmMax, codigo, preco, fbHb, estagios, " +
            "precoUsd, precoClp, frameMaxIec, frameMaxNema",
            r => new Equipamento
            {
                Id = S(r, 0), Serie = S(r, 1), Diametro = S(r, 2),
                Cubo = S(r, 3), RpmMax = Int(S(r, 4)),
                Codigo = S(r, 5), Preco = S(r, 6),
                FbHb = S(r, 7), Estagios = S(r, 8),
                PrecoUsd = S(r, 9), PrecoClp = S(r, 10),
                FrameMaxIec = S(r, 11), FrameMaxNema = S(r, 12),
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
            new("codigo", e.Codigo), new("preco", e.Preco),
            new("fbHb", e.FbHb), new("estagios", e.Estagios),
            new("precoUsd", e.PrecoUsd), new("precoClp", e.PrecoClp),
            new("frameMaxIec", e.FrameMaxIec), new("frameMaxNema", e.FrameMaxNema),
        });
    }

    public void Apagar(string id) => _store.WriteRow(Entidade,
        new KeyValuePair<string, object?>[] { new("id", id) }, deleted: true);

    /// <summary>
    /// Rótulos de cubo lidos errado numa transcrição e corrigidos depois.
    /// O rótulo faz parte do id, então renomear é apagar e regravar — sem isso
    /// a semeadura criaria um bloco novo e o cubo apareceria duas vezes.
    /// </summary>
    private static readonly (string Serie, string De, string Para)[] CorrecoesDeCubo =
    {
        // A foto sugeria "S2200"; a planilha do Joy diz S2000.
        ("Joy", "21\", S2200", "21\", S2000"),
    };

    /// <summary>Aplica as correções de rótulo. Não faz nada quando já está certo.</summary>
    public void CorrigirRotulos()
    {
        var todos = Todos();
        foreach (var (serie, de, para) in CorrecoesDeCubo)
        {
            foreach (var e in todos.Where(x => x.Serie == serie && x.Cubo == de))
            {
                Apagar(e.Id);
                Salvar(new Equipamento
                {
                    Serie = e.Serie, Diametro = e.Diametro, Cubo = para, RpmMax = e.RpmMax,
                    Codigo = e.Codigo, Preco = e.Preco,
                    FbHb = e.FbHb, Estagios = e.Estagios,
                    PrecoUsd = e.PrecoUsd, PrecoClp = e.PrecoClp,
                    FrameMaxIec = e.FrameMaxIec, FrameMaxNema = e.FrameMaxNema,
                });
            }
        }
    }

    /// <summary>
    /// Blocos cuja transcrição saiu errada (linhas deslocadas na leitura da foto)
    /// e que precisam ser refeitos a partir da tabela de fábrica. Cada um roda
    /// UMA vez — o id fica gravado, então um ajuste posterior da equipe naquele
    /// bloco não é desfeito na abertura seguinte.
    /// </summary>
    private static readonly (string Id, string Serie, string Cubo)[] BlocosRefeitos =
    {
        ("joy-21-s2000-v2", "Joy", "21\", S2000"),
        ("joy-26-s2000-v2", "Joy", "26\", S2000"),
        ("joy-30-s2000-v2", "Joy", "30\", S2000"),
    };

    private HashSet<string> MigracoesAplicadas() => _store
        .ReadLatest(EntidadeMigracoes, "id", r => r.IsDBNull(0) ? "" : r.GetString(0))
        .ToHashSet();

    private void RefazerBlocos()
    {
        var aplicadas = MigracoesAplicadas();

        foreach (var (id, serie, cubo) in BlocosRefeitos)
        {
            if (aplicadas.Contains(id)) continue;

            foreach (var e in Todos().Where(x => x.Serie == serie && x.Cubo == cubo))
                Apagar(e.Id);

            foreach (var e in EquipamentosSeed.Todas().Where(x => x.Serie == serie && x.Cubo == cubo))
                Salvar(e);

            _store.WriteRow(EntidadeMigracoes,
                new KeyValuePair<string, object?>[] { new("id", id) });
        }
    }

    /// <summary>
    /// Carrega a tabela de fábrica dos BLOCOS que ainda não entraram neste banco
    /// — um bloco é uma coluna da planilha (série + cubo).
    ///
    /// Essa granularidade é de propósito: as tabelas chegam aos poucos, um bloco
    /// de cubo por vez. Semeando por bloco, um cubo novo entra num banco que já
    /// tem os outros sem encostar no que está gravado.
    ///
    /// Cada bloco é semeado <b>uma única vez</b>, e a marca disso fica gravada.
    /// Antes o critério era "o bloco não está no banco, então carrega" — e aí
    /// apagar um cubo, ou mudá-lo de série, esvaziava o bloco e a abertura
    /// seguinte o trazia de volta, desfazendo o que a equipe tinha feito. Com a
    /// marca, o que foi apagado fica apagado.
    ///
    /// Num banco anterior a esta marcação os blocos que já estão lá são apenas
    /// marcados, sem regravar nada.
    /// </summary>
    public void SemearSeVazio()
    {
        CorrigirRotulos();
        RefazerBlocos();

        var jaSemeados = _store
            .ReadLatest(EntidadeBlocos, "id", r => r.IsDBNull(0) ? "" : r.GetString(0))
            .ToHashSet();

        var blocosExistentes = Todos()
            .Select(e => (e.Serie, e.Cubo))
            .ToHashSet();

        foreach (var bloco in EquipamentosSeed.Todas().GroupBy(e => (e.Serie, e.Cubo)))
        {
            var marca = bloco.Key.Serie + "|" + bloco.Key.Cubo;
            if (jaSemeados.Contains(marca)) continue;

            if (!blocosExistentes.Contains(bloco.Key))
                foreach (var e in bloco) Salvar(e);

            _store.WriteRow(EntidadeBlocos,
                new KeyValuePair<string, object?>[] { new("id", marca) });
        }
    }

    /// <summary>
    /// Apaga TODAS as combinações e deixa a tabela de fábrica marcada como já
    /// carregada — sem isso, a próxima abertura traria tudo de volta e a
    /// limpeza não teria servido para nada. É o que a equipe usa para subir a
    /// tabela dela no lugar da nossa.
    /// </summary>
    public void Limpar()
    {
        _store.Clear(Entidade);

        foreach (var bloco in EquipamentosSeed.Todas()
                     .Select(e => e.Serie + "|" + e.Cubo).Distinct())
        {
            _store.WriteRow(EntidadeBlocos,
                new KeyValuePair<string, object?>[] { new("id", bloco) });
        }

        // as correções de rótulo e os blocos refeitos não têm mais o que corrigir
        foreach (var (id, _, _) in BlocosRefeitos)
            _store.WriteRow(EntidadeMigracoes,
                new KeyValuePair<string, object?>[] { new("id", id) });
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
    /// <summary>Todas as combinações de fábrica, das duas linhas.</summary>
    public static IEnumerable<Equipamento> Todas() => Vax().Concat(Joy());

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
    /// A ordem dos blocos é a da planilha: cubos crescentes, cada um cobrindo
    /// ventiladores maiores que o anterior.
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
        ("21\", S2000", new[]
        {
            ("23 1/4", 3600), ("25 1/4", 3600), ("27 1/7", 3600), ("29 1/4", 3200),
            ("32", 3000), ("34", 2900), ("36", 2700), ("38", 2500), ("42 1/4", 2300),
            ("45", 2100),
        }),
        ("26\", S1000", new[]
        {
            ("34", 2200), ("36", 2200), ("38", 2100), ("42 1/4", 2000), ("45", 2000),
            ("48", 1900), ("54", 1800), ("60", 1800), ("66", 1500), ("72", 1400),
            ("78", 1300), ("85", 1200),
        }),
        ("26\", S2000", new[]
        {
            ("34", 2200), ("36", 2100), ("38", 2000), ("42 1/4", 1900), ("45", 1900),
            ("48", 1800), ("54", 1800), ("60", 1500), ("66", 1300), ("72", 1200),
        }),
        ("30\", S2000", new[]
        {
            ("42 1/4", 1800), ("45", 1800), ("48", 1800), ("54", 1800), ("60", 1800),
            ("66", 1200), ("72", 1200), ("78", 1200),
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
