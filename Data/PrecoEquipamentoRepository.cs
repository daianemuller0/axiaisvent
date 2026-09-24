namespace HowdenAxiais.Poc.Data;

/// <summary>
/// O preço de uma OPÇÃO (subitem de característica) para UM equipamento.
///
/// Existe porque acessório não tem preço único: o difusor de um ventilador de
/// 24" não custa o que custa o de 85". O preço solto que a opção já tinha
/// continua valendo como <b>padrão</b> — estas linhas são as exceções, e a
/// equipe preenche só onde o tamanho muda o valor.
///
/// A chave é a opção mais a combinação inteira (série + ventilador + cubo), que
/// é como o equipamento se identifica no resto do sistema.
/// </summary>
public sealed class PrecoEquipamento
{
    public string Id { get; set; } = "";

    /// <summary>A lista de característica: "Difusor".</summary>
    public string Grupo { get; set; } = "";
    /// <summary>A opção dentro dela: "Com".</summary>
    public string Valor { get; set; } = "";

    public string Serie { get; set; } = "";
    public string Diametro { get; set; } = "";
    public string Cubo { get; set; } = "";

    /// <summary>Preço em reais, como a equipe digita. Vazio = usa o da opção.</summary>
    public string Preco { get; set; } = "";
    public string PrecoUsd { get; set; } = "";
    public string PrecoClp { get; set; } = "";

    /// <summary>Verdadeiro quando não há nada preenchido — aí a linha não precisa existir.</summary>
    public bool Vazio =>
        Preco.Length == 0 && PrecoUsd.Length == 0 && PrecoClp.Length == 0;

    public static string MontarId(string grupo, string valor, string serie, string diametro, string cubo) =>
        $"{grupo}|{valor}|{serie}|{diametro}|{cubo}";
}

/// <summary>
/// Os preços por equipamento (entidade "precos_equipamento"), sobre o mesmo
/// ParquetStore do resto do sistema.
/// </summary>
public sealed class PrecoEquipamentoRepository
{
    private const string Entidade = "precos_equipamento";

    private readonly ParquetStore _store;
    public PrecoEquipamentoRepository(ParquetStore store) => _store = store;

    public List<PrecoEquipamento> Todos() => _store
        .ReadLatest(Entidade, "id, grupo, valor, serie, diametro, cubo, preco, precoUsd, precoClp",
            r => new PrecoEquipamento
            {
                Id = S(r, 0), Grupo = S(r, 1), Valor = S(r, 2),
                Serie = S(r, 3), Diametro = S(r, 4), Cubo = S(r, 5),
                Preco = S(r, 6), PrecoUsd = S(r, 7), PrecoClp = S(r, 8),
            })
        .ToList();

    /// <summary>Os preços de uma opção, indexados pela combinação.</summary>
    public Dictionary<string, PrecoEquipamento> Da(string grupo, string valor) => Todos()
        .Where(p => p.Grupo == grupo && p.Valor == valor)
        .ToDictionary(p => Chave(p.Serie, p.Diametro, p.Cubo), p => p);

    public static string Chave(string serie, string diametro, string cubo) =>
        $"{serie}|{diametro}|{cubo}";

    /// <summary>
    /// Grava — ou apaga, quando a linha ficou sem nenhum preço. Uma linha em
    /// branco não é informação: é o mesmo que usar o preço da opção.
    /// </summary>
    public void Salvar(PrecoEquipamento p)
    {
        if (string.IsNullOrWhiteSpace(p.Id))
            p.Id = PrecoEquipamento.MontarId(p.Grupo, p.Valor, p.Serie, p.Diametro, p.Cubo);

        if (p.Vazio)
        {
            Apagar(p.Id);
            return;
        }

        _store.WriteRow(Entidade, new KeyValuePair<string, object?>[]
        {
            new("id", p.Id), new("grupo", p.Grupo), new("valor", p.Valor),
            new("serie", p.Serie), new("diametro", p.Diametro), new("cubo", p.Cubo),
            new("preco", p.Preco), new("precoUsd", p.PrecoUsd), new("precoClp", p.PrecoClp),
        });
    }

    public void Apagar(string id) => _store.WriteRow(Entidade,
        new KeyValuePair<string, object?>[] { new("id", id) }, deleted: true);

    /// <summary>Apaga todos os preços de uma opção.</summary>
    public void LimparOpcao(string grupo, string valor)
    {
        foreach (var p in Todos().Where(p => p.Grupo == grupo && p.Valor == valor))
            Apagar(p.Id);
    }

    private static string S(System.Data.IDataReader r, int i) => r.IsDBNull(i) ? "" : r.GetString(i);
}
