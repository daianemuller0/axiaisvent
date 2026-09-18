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
/// rótulo: um ventilador novo pode entrar em qualquer lugar da sequência. A
/// ordem é <b>única por tipo</b>, não por série — VAX e Joy convivem na mesma
/// lista, cada linha dizendo na coluna <see cref="Serie"/> de qual linha de
/// produto ela é.
/// </summary>
public sealed class ItemModelo
{
    public const string TipoVentilador = "Ventilador";
    public const string TipoCubo = "Cubo";

    /// <summary>As linhas de produto conhecidas, na ordem em que aparecem.</summary>
    public static readonly string[] Series = { "VAX", "Joy" };

    /// <summary>Posição da série na ordem preferida; desconhecida vai para o fim.</summary>
    public static int OrdemDaSerie(string serie) =>
        Array.IndexOf(Series, serie) is var i && i >= 0 ? i : Series.Length;

    public string Id { get; set; } = "";
    /// <summary>Linha de equipamento: VAX ou Joy. É a coluna da lista.</summary>
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
        .OrderBy(i => i.Tipo)
        .ThenBy(i => i.Ordem)
        .ThenBy(i => ItemModelo.OrdemDaSerie(i.Serie))
        .ToList();

    /// <summary>
    /// Todos os ventiladores (ou todos os cubos), das duas séries, na ordem
    /// gravada — é esta a lista que a tela mostra.
    /// </summary>
    public List<ItemModelo> Lista(string tipo) =>
        Todos().Where(i => i.Tipo == tipo).ToList();

    /// <summary>Os ventiladores (ou cubos) de uma série só, na ordem gravada.</summary>
    public List<ItemModelo> Lista(string serie, string tipo) =>
        Lista(tipo).Where(i => i.Serie == serie).ToList();

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

        // na ordem preferida das séries: é ela que decide quem entra primeiro na
        // lista única quando o banco nasce do zero
        foreach (var serie in equipamentos.Select(e => e.Serie).Distinct()
                     .OrderBy(ItemModelo.OrdemDaSerie).ThenBy(s => s))
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

                // a ordem é única no tipo inteiro (as duas séries na mesma lista),
                // por isso o próximo número olha todos os itens daquele tipo
                var proxima = existentes
                    .Where(i => i.Tipo == tipo)
                    .Select(i => i.Ordem)
                    .DefaultIfEmpty(0)
                    .Max();

                foreach (var rotulo in faltando)
                {
                    var novo = new ItemModelo
                    {
                        Serie = serie, Tipo = tipo, Rotulo = rotulo, Ordem = ++proxima,
                    };
                    Salvar(novo);
                    existentes.Add(novo);
                }
            }
        }
    }

    /// <summary>
    /// Deixa a ordem de cada tipo numa sequência 1..n <b>sem repetição</b>.
    ///
    /// Enquanto VAX e Joy eram duas listas separadas, a ordem era contada dentro
    /// de cada série — havia um ventilador nº 1 no VAX e outro no Joy. Juntando
    /// tudo numa lista só esses empates teriam que ser desempatados na hora de
    /// mostrar, e o ↑ ↓ ficaria imprevisível. Esta passagem os desfaz uma única
    /// vez: o critério de desempate é a série (VAX antes de Joy), que é como as
    /// duas listas apareciam antes.
    ///
    /// É idempotente: depois da primeira vez a ordem já é única, nada é regravado
    /// e nenhuma troca feita pela equipe é desfeita.
    /// </summary>
    public void NormalizarOrdem()
    {
        foreach (var grupo in Todos().GroupBy(i => i.Tipo))
        {
            var fila = grupo
                .OrderBy(i => i.Ordem)
                .ThenBy(i => ItemModelo.OrdemDaSerie(i.Serie))
                .ThenBy(i => Medida.Numero(i.Rotulo))
                .ThenBy(i => i.Rotulo)
                .ToList();

            for (var posicao = 0; posicao < fila.Count; posicao++)
            {
                if (fila[posicao].Ordem == posicao + 1) continue;
                fila[posicao].Ordem = posicao + 1;
                Salvar(fila[posicao]);
            }
        }
    }

    private static string S(System.Data.IDataReader r, int i) => r.IsDBNull(i) ? "" : r.GetString(i);

    private static int Int(string s) => int.TryParse(s, out var v) ? v : 0;
}
