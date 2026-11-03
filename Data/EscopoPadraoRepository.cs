using System.Text.Json;

namespace HowdenAxiais.Poc.Data;

/// <summary>
/// O escopo típico de uma série: o que normalmente entra em cada lista e na
/// parte elétrica. A equipe guarda uma vez, a partir de um equipamento bem
/// montado, e daí em diante um equipamento novo daquela série já nasce com
/// ele — confere e ajusta, em vez de marcar lista por lista.
/// </summary>
public sealed class EscopoPadrao
{
    public string Serie { get; set; } = "";
    public Dictionary<string, string> Escolhas { get; set; } = new();
    public bool ComMotor { get; set; }
    public bool ComPartidor { get; set; }
    public string Partidor { get; set; } = "";
    public bool ComInstrumentacao { get; set; }
    public List<string> Instrumentacao { get; set; } = new();
    public string Montagem { get; set; } = "";
    public string GuardadoPor { get; set; } = "";
    public string GuardadoEm { get; set; } = "";

    /// <summary>O padrão a partir de um equipamento da proposta.</summary>
    public static EscopoPadrao De(string serie, ItemProposta item, string quem) => new()
    {
        Serie = serie,
        Escolhas = new Dictionary<string, string>(item.Escolhas.Where(e => e.Value.Trim().Length > 0)),
        ComMotor = item.ComMotor,
        ComPartidor = item.ComPartidor,
        Partidor = item.Partidor,
        ComInstrumentacao = item.ComInstrumentacao,
        Instrumentacao = item.Instrumentacao.ToList(),
        Montagem = item.Montagem,
        GuardadoPor = quem,
        GuardadoEm = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
    };

    /// <summary>
    /// Leva o padrão para um equipamento. Só preenche o que ainda está em
    /// branco: o que veio da folha de dados ou foi marcado à mão vale mais que
    /// o padrão. Devolve quantas escolhas foram feitas.
    /// </summary>
    public int AplicarEm(ItemProposta item)
    {
        var feitas = 0;

        foreach (var (lista, opcao) in Escolhas)
        {
            if (item.Escolhas.TryGetValue(lista, out var atual) && atual.Trim().Length > 0) continue;
            item.Escolhas[lista] = opcao;
            feitas++;
        }

        // a parte elétrica só entra num equipamento que ainda não tem nada dela
        if (!item.ComMotor && !item.ComPartidor && !item.ComInstrumentacao)
        {
            if (ComMotor) { item.ComMotor = true; feitas++; }
            if (ComPartidor) { item.ComPartidor = true; item.Partidor = Partidor; feitas++; }
            if (ComInstrumentacao)
            {
                item.ComInstrumentacao = true;
                item.Instrumentacao = Instrumentacao.ToList();
                feitas++;
            }
        }

        if (item.Montagem.Trim().Length == 0 && Montagem.Length > 0)
        {
            item.Montagem = Montagem;
            feitas++;
        }

        return feitas;
    }
}

/// <summary>Os escopos padrão (entidade "escopos_padrao"), um por série.</summary>
public sealed class EscopoPadraoRepository
{
    private const string Entidade = "escopos_padrao";

    private readonly ParquetStore _store;
    public EscopoPadraoRepository(ParquetStore store) => _store = store;

    public List<EscopoPadrao> Todos() => _store
        .ReadLatest(Entidade, "id, conteudo", r => Ler(r.IsDBNull(1) ? "" : r.GetString(1)))
        .Where(p => p is not null)
        .Select(p => p!)
        .ToList();

    public EscopoPadrao? Da(string serie) =>
        Todos().FirstOrDefault(p => Textos.Igual(p.Serie, serie));

    public void Salvar(EscopoPadrao p) => _store.WriteRow(Entidade, new KeyValuePair<string, object?>[]
    {
        new("id", Textos.Simples(p.Serie)),
        new("conteudo", JsonSerializer.Serialize(p)),
    });

    private static EscopoPadrao? Ler(string json)
    {
        if (json.Trim().Length == 0) return null;
        try { return JsonSerializer.Deserialize<EscopoPadrao>(json); }
        catch (JsonException) { return null; }
    }
}
