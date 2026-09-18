namespace HowdenAxiais.Poc.Data;

/// <summary>
/// O código e o preço de uma peça do modelo: um <b>ventilador</b> (Fan Diameter)
/// ou um <b>cubo</b> (Fan Hub Diameter).
///
/// A LISTA de ventiladores e de cubos não mora aqui — ela sai da matriz de
/// equipamentos, que é a fonte do que existe. Aqui fica só o que se pendura
/// nela: código e preço. Assim não há duas listas para manter em sincronia, e
/// um rótulo que some da matriz simplesmente deixa de aparecer.
/// </summary>
public sealed class ItemModelo
{
    public const string TipoVentilador = "Ventilador";
    public const string TipoCubo = "Cubo";

    public string Id { get; set; } = "";
    /// <summary>Linha de equipamento: VAX ou Joy.</summary>
    public string Serie { get; set; } = "";
    /// <summary><see cref="TipoVentilador"/> ou <see cref="TipoCubo"/>.</summary>
    public string Tipo { get; set; } = "";
    /// <summary>O rótulo, igual ao da matriz: "2400", "18 1/4", "14\", S1000".</summary>
    public string Rotulo { get; set; } = "";
    /// <summary>Código, que entra na montagem do código do equipamento.</summary>
    public string Codigo { get; set; } = "";
    /// <summary>Preço, como a equipe digita. Vazio = sem preço.</summary>
    public string Preco { get; set; } = "";

    public static string MontarId(string serie, string tipo, string rotulo) =>
        $"{serie}-{tipo}-{rotulo}";
}

/// <summary>
/// Códigos e preços de ventiladores e cubos (entidade "itens_modelo"), sobre o
/// mesmo ParquetStore do resto do sistema.
/// </summary>
public sealed class ItemModeloRepository
{
    private const string Entidade = "itens_modelo";

    private readonly ParquetStore _store;
    public ItemModeloRepository(ParquetStore store) => _store = store;

    public List<ItemModelo> Todos() => _store
        .ReadLatest(Entidade, "id, serie, tipo, rotulo, codigo, preco", r => new ItemModelo
        {
            Id = S(r, 0), Serie = S(r, 1), Tipo = S(r, 2), Rotulo = S(r, 3),
            Codigo = S(r, 4), Preco = S(r, 5),
        })
        .ToList();

    public void Salvar(ItemModelo i)
    {
        if (string.IsNullOrWhiteSpace(i.Id)) i.Id = ItemModelo.MontarId(i.Serie, i.Tipo, i.Rotulo);
        _store.WriteRow(Entidade, new KeyValuePair<string, object?>[]
        {
            new("id", i.Id), new("serie", i.Serie), new("tipo", i.Tipo),
            new("rotulo", i.Rotulo), new("codigo", i.Codigo), new("preco", i.Preco),
        });
    }

    public void Apagar(string id) => _store.WriteRow(Entidade,
        new KeyValuePair<string, object?>[] { new("id", id) }, deleted: true);

    private static string S(System.Data.IDataReader r, int i) => r.IsDBNull(i) ? "" : r.GetString(i);
}
