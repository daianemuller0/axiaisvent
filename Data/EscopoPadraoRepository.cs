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
    /// O que o escopo trazido precisa saber do cadastro e do equipamento para
    /// não pôr lixo: se a opção ainda existe, quais listas a folha de dados já
    /// respondeu e se a montagem pode vir junto (só do outro equipamento desta
    /// proposta e do padrão da série — a montagem de outro cliente é de outra
    /// mina).
    /// </summary>
    public sealed record Regras(
        Func<string, string, bool> OpcaoExiste,
        Func<string, bool> PartidorExiste,
        Func<string, bool> InstrumentacaoExiste,
        IReadOnlyCollection<string> RespondidasNaFolha,
        bool ComMontagem);

    /// <summary>
    /// Leva este escopo para um equipamento, completando só o que está em
    /// branco: o que veio da folha de dados ou foi marcado à mão vale mais.
    /// <list type="bullet">
    /// <item>as listas que a folha respondeu ficam como a folha deixou, mesmo
    /// quando ela disse "-" ou algo que o sistema não entendeu;</item>
    /// <item>uma escolha cuja opção saiu do cadastro é jogada fora e volta em
    /// <paramref name="ignoradas"/> — trazê-la daria uma linha sem preço e
    /// sem código que ninguém pediu;</item>
    /// <item>a parte elétrica só entra num equipamento que ainda não tem nada
    /// dela; a montagem, só com <see cref="Regras.ComMontagem"/> e em
    /// branco.</item>
    /// </list>
    /// Devolve as chaves que mudaram (ver <see cref="Ler"/>): é com elas que o
    /// "desfazer" devolve SÓ o que foi trazido, sem levar junto a quantidade, a
    /// seleção ou um preço digitado depois.
    /// </summary>
    public List<string> CompletarEm(ItemProposta item, Regras regras, List<string> ignoradas)
    {
        var mudadas = new List<string>();

        foreach (var (lista, opcao) in Escolhas)
        {
            if (opcao.Trim().Length == 0) continue;
            if (regras.RespondidasNaFolha.Any(r => Textos.Igual(r, lista))) continue;
            if (item.Escolhas.TryGetValue(lista, out var atual) && atual.Trim().Length > 0) continue;

            if (!regras.OpcaoExiste(lista, opcao))
            {
                ignoradas.Add($"{lista}: {opcao}");
                continue;
            }

            item.Escolhas[lista] = opcao;
            mudadas.Add(ChaveDaLista(lista));
        }

        if (!item.ComMotor && !item.ComPartidor && !item.ComInstrumentacao)
        {
            if (ComMotor)
            {
                item.ComMotor = true;
                mudadas.Add(ChaveComMotor);
            }

            if (ComPartidor)
            {
                // um partidor que saiu do cadastro não liga o "Com" vazio: ele
                // viraria uma pendência que ninguém criou
                if (Partidor.Length > 0 && !regras.PartidorExiste(Partidor))
                {
                    ignoradas.Add($"partidor: {Partidor}");
                }
                else
                {
                    item.ComPartidor = true;
                    item.Partidor = Partidor;
                    mudadas.Add(ChaveComPartidor);
                    mudadas.Add(ChavePartidor);
                }
            }

            if (ComInstrumentacao)
            {
                var existem = Instrumentacao.Where(regras.InstrumentacaoExiste).ToList();
                ignoradas.AddRange(Instrumentacao.Except(existem).Select(n => $"instrumentação: {n}"));

                if (existem.Count > 0 || Instrumentacao.Count == 0)
                {
                    item.ComInstrumentacao = true;
                    item.Instrumentacao = existem;
                    mudadas.Add(ChaveComInstrumentacao);
                    mudadas.Add(ChaveInstrumentacao);
                }
            }
        }

        if (regras.ComMontagem && item.Montagem.Trim().Length == 0 && Montagem.Trim().Length > 0)
        {
            item.Montagem = Montagem;
            mudadas.Add(ChaveMontagem);
        }

        return mudadas;
    }

    // ---------- as chaves do que um escopo trazido muda ----------
    // Uma chave por coisa que dá para devolver ao que era: cada lista, cada
    // flag da parte elétrica, a montagem e as peças juntadas ao alcance. O
    // valor vai como texto para o "desfazer" comparar e devolver do mesmo jeito.

    public const string ChaveComMotor = "ComMotor";
    public const string ChaveComPartidor = "ComPartidor";
    public const string ChavePartidor = "Partidor";
    public const string ChaveComInstrumentacao = "ComInstrumentacao";
    public const string ChaveInstrumentacao = "Instrumentacao";
    public const string ChaveMontagem = "Montagem";
    public const string ChaveEscopoExtra = "EscopoExtra";
    private const string PrefixoDaLista = "lista:";

    public static string ChaveDaLista(string lista) => PrefixoDaLista + lista;

    /// <summary>A lista de uma chave "lista:Base", ou nulo quando a chave é de outra coisa.</summary>
    public static string? ListaDaChave(string chave) =>
        chave.StartsWith(PrefixoDaLista, StringComparison.Ordinal) ? chave[PrefixoDaLista.Length..] : null;

    /// <summary>O valor de uma chave no equipamento, como texto.</summary>
    public static string Ler(ItemProposta item, string chave) => chave switch
    {
        ChaveComMotor => item.ComMotor ? "sim" : "",
        ChaveComPartidor => item.ComPartidor ? "sim" : "",
        ChavePartidor => item.Partidor,
        ChaveComInstrumentacao => item.ComInstrumentacao ? "sim" : "",
        ChaveInstrumentacao => JsonSerializer.Serialize(item.Instrumentacao),
        ChaveMontagem => item.Montagem,
        ChaveEscopoExtra => JsonSerializer.Serialize(item.EscopoExtra),
        _ => ListaDaChave(chave) is { } lista ? item.Escolhas.GetValueOrDefault(lista, "") : "",
    };

    /// <summary>Devolve uma chave ao valor guardado por <see cref="Ler"/>.</summary>
    public static void Gravar(ItemProposta item, string chave, string valor)
    {
        switch (chave)
        {
            case ChaveComMotor: item.ComMotor = valor.Length > 0; break;
            case ChaveComPartidor: item.ComPartidor = valor.Length > 0; break;
            case ChavePartidor: item.Partidor = valor; break;
            case ChaveComInstrumentacao: item.ComInstrumentacao = valor.Length > 0; break;
            case ChaveInstrumentacao:
                item.Instrumentacao = JsonSerializer.Deserialize<List<string>>(valor) ?? new();
                break;
            case ChaveMontagem: item.Montagem = valor; break;
            case ChaveEscopoExtra:
                item.EscopoExtra = JsonSerializer.Deserialize<List<string>>(valor) ?? new();
                break;
            default:
                if (ListaDaChave(chave) is not { } lista) break;
                // em branco é sem a chave: é assim que a lista "a decidir" é gravada
                if (valor.Length == 0) item.Escolhas.Remove(lista);
                else item.Escolhas[lista] = valor;
                break;
        }
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
