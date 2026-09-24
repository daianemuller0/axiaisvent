namespace HowdenAxiais.Poc.Data;

/// <summary>
/// O preço de uma OPÇÃO (subitem de característica) para UM modelo.
///
/// Existe porque acessório não tem preço único: o difusor de um ventilador de
/// 24" não custa o que custa o de 85". O preço solto que a opção já tinha
/// continua valendo como <b>padrão</b> — estas linhas são as exceções, e a
/// equipe preenche só onde o tamanho muda o valor.
///
/// A chave é a opção mais o <b>modelo inteiro</b>: série + ventilador + cubo +
/// FB/HB + nº de estágios, que é como o equipamento se identifica no resto do
/// sistema. Guardar só o trio não serviria — dois modelos podem dividir o mesmo
/// ventilador e cubo e mesmo assim ter difusores de preços diferentes.
/// </summary>
public sealed class PrecoEquipamento
{
    public string Id { get; set; } = "";

    /// <summary>A lista de característica: "Difusor".</summary>
    public string Grupo { get; set; } = "";
    /// <summary>A opção dentro dela: "Com".</summary>
    public string Valor { get; set; } = "";

    /// <summary>O id do modelo (<see cref="Equipamento.Chave"/>).</summary>
    public string Equipamento { get; set; } = "";

    /// <summary>Preço em reais, como a equipe digita. Vazio = usa o da opção.</summary>
    public string Preco { get; set; } = "";
    public string PrecoUsd { get; set; } = "";
    public string PrecoClp { get; set; } = "";

    /// <summary>Verdadeiro quando não há nada preenchido — aí a linha não precisa existir.</summary>
    public bool Vazio =>
        Preco.Length == 0 && PrecoUsd.Length == 0 && PrecoClp.Length == 0;

    public static string MontarId(string grupo, string valor, string equipamento) =>
        $"{grupo}|{valor}|{equipamento}";
}

/// <summary>
/// Os preços por equipamento (entidade "precos_equipamento"), sobre o mesmo
/// ParquetStore do resto do sistema.
/// </summary>
public sealed class PrecoEquipamentoRepository
{
    private const string Entidade = "precos_equipamento";
    private const string EntidadeMigracoes = "precos_equipamento_migracoes";

    private readonly ParquetStore _store;
    public PrecoEquipamentoRepository(ParquetStore store) => _store = store;

    public List<PrecoEquipamento> Todos() => _store
        .ReadLatest(Entidade, "id, grupo, valor, equipamento, preco, precoUsd, precoClp",
            r => new PrecoEquipamento
            {
                Id = S(r, 0), Grupo = S(r, 1), Valor = S(r, 2),
                Equipamento = S(r, 3),
                Preco = S(r, 4), PrecoUsd = S(r, 5), PrecoClp = S(r, 6),
            })
        .ToList();

    /// <summary>Os preços de uma opção, indexados pelo id do modelo.</summary>
    public Dictionary<string, PrecoEquipamento> Da(string grupo, string valor) => Todos()
        .Where(p => p.Grupo == grupo && p.Valor == valor)
        .GroupBy(p => p.Equipamento)
        .ToDictionary(g => g.Key, g => g.First());

    /// <summary>
    /// Grava — ou apaga, quando a linha ficou sem nenhum preço. Uma linha em
    /// branco não é informação: é o mesmo que usar o preço da opção.
    /// </summary>
    public void Salvar(PrecoEquipamento p)
    {
        if (string.IsNullOrWhiteSpace(p.Id))
            p.Id = PrecoEquipamento.MontarId(p.Grupo, p.Valor, p.Equipamento);

        if (p.Vazio)
        {
            Apagar(p.Id);
            return;
        }

        _store.WriteRow(Entidade, new KeyValuePair<string, object?>[]
        {
            new("id", p.Id), new("grupo", p.Grupo), new("valor", p.Valor),
            new("equipamento", p.Equipamento),
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

    /// <summary>Apaga tudo — usado quando a lista de modelos inteira é esvaziada.</summary>
    public void Limpar()
    {
        foreach (var p in Todos()) Apagar(p.Id);
    }

    /// <summary>Apaga os preços de um modelo que saiu da lista.</summary>
    public void LimparEquipamento(string equipamentoId)
    {
        foreach (var p in Todos().Where(p => p.Equipamento == equipamentoId))
            Apagar(p.Id);
    }

    /// <summary>
    /// O modelo mudou de identidade (mexeram no FB/HB ou nos estágios): leva os
    /// preços junto, senão eles ficariam apontando para um modelo que não existe
    /// mais e sumiriam da tela sem aviso.
    /// </summary>
    public void Remapear(string idAntigo, string idNovo)
    {
        if (idAntigo.Length == 0 || idAntigo == idNovo) return;

        foreach (var p in Todos().Where(p => p.Equipamento == idAntigo))
        {
            Apagar(p.Id);
            Salvar(new PrecoEquipamento
            {
                Grupo = p.Grupo, Valor = p.Valor, Equipamento = idNovo,
                Preco = p.Preco, PrecoUsd = p.PrecoUsd, PrecoClp = p.PrecoClp,
            });
        }
    }

    /// <summary>
    /// Banco anterior à identidade de cinco campos: as linhas guardavam série,
    /// ventilador e cubo em colunas separadas. Converte cada uma para o id do
    /// modelo correspondente, uma vez. O que não achar modelo é apagado — seria
    /// um preço órfão, invisível na tela.
    /// </summary>
    public void Converter(EquipamentoRepository equipamentos)
    {
        const string marca = "preco-por-id-do-modelo-v1";

        var aplicadas = _store
            .ReadLatest(EntidadeMigracoes, "id", r => r.IsDBNull(0) ? "" : r.GetString(0))
            .ToHashSet();
        if (aplicadas.Contains(marca)) return;

        var antigas = _store.ReadLatest(Entidade,
            "id, grupo, valor, serie, diametro, cubo, preco, precoUsd, precoClp",
            r => (Id: S(r, 0), Grupo: S(r, 1), Valor: S(r, 2),
                  Serie: S(r, 3), Diametro: S(r, 4), Cubo: S(r, 5),
                  Preco: S(r, 6), Usd: S(r, 7), Clp: S(r, 8)))
            .Where(l => l.Serie.Length > 0)
            .ToList();

        var modelos = equipamentos.Todos();

        foreach (var l in antigas)
        {
            Apagar(l.Id);

            var modelo = modelos.FirstOrDefault(e =>
                e.Serie == l.Serie && e.Diametro == l.Diametro && e.Cubo == l.Cubo);
            if (modelo is null) continue;

            Salvar(new PrecoEquipamento
            {
                Grupo = l.Grupo, Valor = l.Valor, Equipamento = modelo.Id,
                Preco = l.Preco, PrecoUsd = l.Usd, PrecoClp = l.Clp,
            });
        }

        _store.WriteRow(EntidadeMigracoes,
            new KeyValuePair<string, object?>[] { new("id", marca) });
    }

    private static string S(System.Data.IDataReader r, int i) => r.IsDBNull(i) ? "" : r.GetString(i);
}
