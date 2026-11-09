using System.Globalization;

namespace HowdenAxiais.Poc.Data;

/// <summary>
/// Quanto tempo de trabalho uma proposta levou até virar documento.
///
/// É o cronômetro embutido: a tela soma o tempo entre uma interação e outra
/// (pausas longas não contam) e, a cada documento gerado, grava uma linha
/// aqui. É com estes números, e não com estimativa, que dá para mostrar quanto
/// o sistema encurtou a montagem de uma proposta.
/// </summary>
public sealed class TempoProposta
{
    public string Id { get; set; } = "";
    public string PropostaId { get; set; } = "";
    public string Numero { get; set; } = "";
    public string Revisao { get; set; } = "";
    public string Usuario { get; set; } = "";

    /// <summary>Quando a pessoa começou a mexer nesta proposta (ISO, hora local).</summary>
    public string Inicio { get; set; } = "";

    /// <summary>Quando o documento foi gerado (ISO, hora local).</summary>
    public string Fim { get; set; } = "";

    /// <summary>Segundos de trabalho, sem as pausas.</summary>
    public int SegundosAtivos { get; set; }

    /// <summary>Quantas vezes a tela respondeu a um clique, campo ou arquivo.</summary>
    public int Interacoes { get; set; }

    /// <summary>O que foi gerado: "gerar-tudo", "comercial", "tecnica" ou "pricing".</summary>
    public string Evento { get; set; } = "";

    /// <summary>
    /// Os segundos de trabalho em cada etapa, em JSON ({"2": 140, "3-escopo": 310…}).
    /// A etapa Equipamentos vem separada por aba, porque é ela que pesa. Vazio
    /// nas linhas gravadas antes da medição por etapa.
    /// </summary>
    public string PorEtapa { get; set; } = "";

    /// <summary>
    /// Quantas vezes cada atalho foi usado, em JSON ({"modelo:botao1": 2…}). É
    /// o que mostra se uma sugestão da tela acerta ou se a pessoa a desfaz.
    /// </summary>
    public string Contadores { get; set; } = "";

    /// <summary>De onde a proposta partiu: "nova", "importada", "copia", "aberta" ou "revisao".</summary>
    public string Origem { get; set; } = "";

    /// <summary>Quantos equipamentos (linhas) a proposta tinha ao gerar. 0 nas linhas antigas.</summary>
    public int Equipamentos { get; set; }

    public Dictionary<string, int> PorEtapaComoMapa => Mapa(PorEtapa);

    public Dictionary<string, int> ContadoresComoMapa => Mapa(Contadores);

    /// <summary>O texto JSON como dicionário; texto vazio ou estragado vira dicionário vazio.</summary>
    public static Dictionary<string, int> Mapa(string json)
    {
        if (json.Trim().Length == 0) return new();
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, int>>(json) ?? new();
        }
        catch (System.Text.Json.JsonException)
        {
            return new();
        }
    }

    public DateTime? FimComoData =>
        DateTime.TryParse(Fim, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
}

/// <summary>Os tempos gravados (entidade "tempos_proposta"), uma linha por documento gerado.</summary>
public sealed class TempoRepository
{
    private const string Entidade = "tempos_proposta";

    private readonly ParquetStore _store;
    public TempoRepository(ParquetStore store) => _store = store;

    // As quatro últimas colunas vieram depois: os arquivos antigos não as têm,
    // e a leitura (ParquetStore.LerDoDisco) devolve NULL no lugar — vira texto
    // vazio e 0 aqui. Por isso elas ficam sempre no FIM, aqui e na gravação.
    public List<TempoProposta> Todos() => _store
        .ReadLatest(Entidade,
            "id, propostaId, numero, revisao, usuario, inicio, fim, segundosAtivos, interacoes, evento, " +
            "porEtapa, contadores, origem, equipamentos",
            r => new TempoProposta
            {
                Id = S(r, 0), PropostaId = S(r, 1), Numero = S(r, 2), Revisao = S(r, 3),
                Usuario = S(r, 4), Inicio = S(r, 5), Fim = S(r, 6),
                SegundosAtivos = Int(S(r, 7)), Interacoes = Int(S(r, 8)), Evento = S(r, 9),
                PorEtapa = S(r, 10), Contadores = S(r, 11), Origem = S(r, 12),
                Equipamentos = Int(S(r, 13)),
            })
        .OrderByDescending(t => t.Fim)
        .ToList();

    public void Registrar(TempoProposta t)
    {
        if (t.Id.Length == 0) t.Id = Guid.NewGuid().ToString("n");

        _store.WriteRow(Entidade, new KeyValuePair<string, object?>[]
        {
            new("id", t.Id), new("propostaId", t.PropostaId), new("numero", t.Numero),
            new("revisao", t.Revisao), new("usuario", t.Usuario),
            new("inicio", t.Inicio), new("fim", t.Fim),
            new("segundosAtivos", t.SegundosAtivos.ToString(CultureInfo.InvariantCulture)),
            new("interacoes", t.Interacoes.ToString(CultureInfo.InvariantCulture)),
            new("evento", t.Evento),
            new("porEtapa", t.PorEtapa), new("contadores", t.Contadores),
            new("origem", t.Origem),
            new("equipamentos", t.Equipamentos.ToString(CultureInfo.InvariantCulture)),
        });
    }

    /// <summary>
    /// O tempo de cada proposta: a PRIMEIRA geração de documento de cada
    /// número e revisão. Gerar de novo depois (para corrigir uma vírgula) não
    /// pode contar como outra proposta montada.
    /// </summary>
    public static List<TempoProposta> PrimeiraDeCada(IEnumerable<TempoProposta> tempos) => tempos
        .Where(t => t.Numero.Length > 0)
        .GroupBy(t => (t.Numero, t.Revisao.Trim()))
        .Select(g => g.OrderBy(t => t.Fim).First())
        .OrderByDescending(t => t.Fim)
        .ToList();

    private static string S(System.Data.IDataReader r, int i) => r.IsDBNull(i) ? "" : r.GetString(i);

    private static int Int(string s) => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;
}
