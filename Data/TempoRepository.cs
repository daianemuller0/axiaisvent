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

    public DateTime? FimComoData =>
        DateTime.TryParse(Fim, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
}

/// <summary>Os tempos gravados (entidade "tempos_proposta"), uma linha por documento gerado.</summary>
public sealed class TempoRepository
{
    private const string Entidade = "tempos_proposta";

    private readonly ParquetStore _store;
    public TempoRepository(ParquetStore store) => _store = store;

    public List<TempoProposta> Todos() => _store
        .ReadLatest(Entidade,
            "id, propostaId, numero, revisao, usuario, inicio, fim, segundosAtivos, interacoes, evento",
            r => new TempoProposta
            {
                Id = S(r, 0), PropostaId = S(r, 1), Numero = S(r, 2), Revisao = S(r, 3),
                Usuario = S(r, 4), Inicio = S(r, 5), Fim = S(r, 6),
                SegundosAtivos = Int(S(r, 7)), Interacoes = Int(S(r, 8)), Evento = S(r, 9),
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
