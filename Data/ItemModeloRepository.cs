namespace HowdenAxiais.Poc.Data;

/// <summary>
/// Um <b>ventilador</b> (Fan Diameter) ou um <b>cubo</b> (Fan Hub Diameter),
/// com seu código, preço e posição.
///
/// Esta é a LISTA MANDANTE: são estas linhas que formam as linhas e as colunas
/// da matriz de equipamentos. Foi o que permitiu editar o rótulo, criar, apagar
/// e reordenar — enquanto a lista era derivada da matriz, ela não tinha onde
/// guardar nada disso.
///
/// Quem manda na ORDEM é o campo <see cref="Ordem"/>, não o valor numérico do
/// rótulo: um ventilador novo pode entrar em qualquer lugar da sequência.
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
    /// <summary>Posição na lista, dentro da série e do tipo (1 = primeiro).</summary>
    public int Ordem { get; set; }

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
        .ReadLatest(Entidade, "id, serie, tipo, rotulo, codigo, preco, ordem", r => new ItemModelo
        {
            Id = S(r, 0), Serie = S(r, 1), Tipo = S(r, 2), Rotulo = S(r, 3),
            Codigo = S(r, 4), Preco = S(r, 5), Ordem = Int(S(r, 6)),
        })
        .OrderBy(i => i.Serie).ThenBy(i => i.Tipo).ThenBy(i => i.Ordem)
        .ToList();

    /// <summary>Os ventiladores (ou cubos) de uma série, na ordem gravada.</summary>
    public List<ItemModelo> Lista(string serie, string tipo) =>
        Todos().Where(i => i.Serie == serie && i.Tipo == tipo).ToList();

    public void Salvar(ItemModelo i)
    {
        if (string.IsNullOrWhiteSpace(i.Id)) i.Id = ItemModelo.MontarId(i.Serie, i.Tipo, i.Rotulo);
        _store.WriteRow(Entidade, new KeyValuePair<string, object?>[]
        {
            new("id", i.Id), new("serie", i.Serie), new("tipo", i.Tipo),
            new("rotulo", i.Rotulo), new("codigo", i.Codigo), new("preco", i.Preco),
            // zeros à esquerda: o Parquet guarda texto e sem isso a 10 viria antes da 2
            new("ordem", i.Ordem.ToString("D4")),
        });
    }

    public void Apagar(string id) => _store.WriteRow(Entidade,
        new KeyValuePair<string, object?>[] { new("id", id) }, deleted: true);

    /// <summary>
    /// Cria os ventiladores e cubos que a matriz de equipamentos tem mas a lista
    /// ainda não — é o que traz um banco antigo para cá, e o que faz um rótulo
    /// novo vindo do Excel aparecer. A ordem inicial é a numérica do rótulo; a
    /// partir daí quem manda é o que a equipe ajustar.
    /// </summary>
    public void SemearDaMatriz(List<Equipamento> equipamentos)
    {
        var existentes = Todos();

        foreach (var serie in equipamentos.Select(e => e.Serie).Distinct())
        {
            foreach (var (tipo, rotulos) in new[]
                     {
                         (ItemModelo.TipoVentilador, equipamentos.Where(e => e.Serie == serie)
                             .Select(e => e.Diametro)),
                         (ItemModelo.TipoCubo, equipamentos.Where(e => e.Serie == serie)
                             .Select(e => e.Cubo)),
                     })
            {
                var naLista = existentes
                    .Where(i => i.Serie == serie && i.Tipo == tipo)
                    .Select(i => i.Rotulo)
                    .ToHashSet();

                var faltando = rotulos.Distinct().Where(r => !naLista.Contains(r))
                    .OrderBy(Medida.Numero).ThenBy(r => r)
                    .ToList();
                if (faltando.Count == 0) continue;

                var proxima = existentes
                    .Where(i => i.Serie == serie && i.Tipo == tipo)
                    .Select(i => i.Ordem)
                    .DefaultIfEmpty(0)
                    .Max();

                foreach (var rotulo in faltando)
                {
                    Salvar(new ItemModelo
                    {
                        Serie = serie, Tipo = tipo, Rotulo = rotulo, Ordem = ++proxima,
                    });
                }
            }
        }
    }

    private static string S(System.Data.IDataReader r, int i) => r.IsDBNull(i) ? "" : r.GetString(i);

    private static int Int(string s) => int.TryParse(s, out var v) ? v : 0;
}
