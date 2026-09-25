namespace HowdenAxiais.Poc.Data;

/// <summary>
/// O preço de uma opção para um <b>Fan Diameter</b>.
///
/// A equipe foi direta: "todos os 45 terão o mesmo preço, independente se é Joy
/// ou VAX e independente do cubo". Então a referência do preço de acessório não
/// é o modelo montado, é o diâmetro do ventilador — um difusor de 45" custa o
/// que custa, não importa em que cubo ele vai.
///
/// A chave é a <b>família</b> (quem divide a mesma tabela) mais o rótulo do
/// diâmetro.
/// </summary>
public sealed class PrecoDiametro
{
    public string Id { get; set; } = "";

    /// <summary>A família de preço — ver <see cref="FamiliaDePreco"/>.</summary>
    public string Familia { get; set; } = "";

    /// <summary>O rótulo do Fan Diameter, como está no cadastro: "45", "2400".</summary>
    public string Diametro { get; set; } = "";

    /// <summary>Preço em reais, como a equipe digita. Vazio = usa o preço da opção.</summary>
    public string Preco { get; set; } = "";
    public string PrecoUsd { get; set; } = "";
    public string PrecoClp { get; set; } = "";

    /// <summary>
    /// O diâmetro em milímetros da peça — só a conexão a manga usa, e é dado
    /// técnico, não preço: a linha sobrevive só com ele preenchido.
    /// </summary>
    public string Medida { get; set; } = "";

    public bool Vazio =>
        Preco.Length == 0 && PrecoUsd.Length == 0 && PrecoClp.Length == 0 && Medida.Length == 0;

    public static string MontarId(string familia, string diametro) => $"{familia}|{diametro}";
}

/// <summary>
/// De qual tabela de preço por diâmetro uma opção se serve.
///
/// Duas listas podem dividir a MESMA tabela sem deixar de ser duas listas: o
/// silenciador de entrada e o de descarga têm códigos diferentes e preço igual,
/// e o mesmo vale para a conexão a manga da entrada e a da saída. Quem decide
/// isso é a família, não a lista.
/// </summary>
public sealed record FamiliaDePreco(string Chave, string Rotulo, bool UsaMedida, string Aviso)
{
    /// <summary>
    /// As listas cujo preço muda com o Fan Diameter — as que a equipe nomeou.
    /// Basta a palavra aparecer no nome da lista, então renomear "Damper
    /// mariposa" para "Damper mariposa saída" não quebra nada.
    ///
    /// Silenciador e conexão a manga têm regra própria (dividem tabela) e estão
    /// tratados antes desta lista.
    /// </summary>
    private static readonly string[] ListasPorDiametro = { "base", "cone", "difusor", "damper" };

    /// <summary>
    /// A família de uma opção, ou nulo quando ela não tem tabela por diâmetro:
    ///
    /// - "Sem", "NENHUM", "Não": não é item vendido;
    /// - lubrificação, contrarrecuo, partidores, instrumentação: <b>preço único</b>,
    ///   o mesmo para todo tamanho de equipamento — quem manda é o preço da
    ///   própria opção, e uma tabela por diâmetro só daria 36 lugares para
    ///   digitar o mesmo número.
    /// </summary>
    public static FamiliaDePreco? De(string grupo, string valor)
    {
        var v = valor.Trim();
        if (v.Length == 0 || EhAusencia(v)) return null;

        // conexão a manga: a da entrada (dentro de "Cone de entrada") e a da
        // saída são a mesma peça e o mesmo preço
        if (v.Contains("manga", StringComparison.OrdinalIgnoreCase))
        {
            return new("Conexão a manga", "Conexão a manga", true,
                "Mesma tabela para a conexão a manga da entrada e a da saída — e o " +
                "diâmetro em mm da peça fica nesta mesma linha.");
        }

        // silenciador: entrada e descarga dividem o preço, opção por opção
        // (L = 1,0D não custa o que custa L = 2,0D)
        if (grupo.StartsWith("Silenciador", StringComparison.OrdinalIgnoreCase))
        {
            return new($"Silenciador|{v}", $"Silenciador · {v}", false,
                "Mesma tabela para o silenciador de entrada e o de descarga.");
        }

        var lista = grupo.ToLowerInvariant();
        if (!ListasPorDiametro.Any(chave => lista.Contains(chave))) return null;

        return new($"{grupo}|{v}", $"{grupo} · {v}", false, "");
    }

    /// <summary>"Sem", "SEM Base", "NENHUM", "Não" — a opção de não levar o item.</summary>
    public static bool EhAusencia(string valor)
    {
        var v = valor.Trim();
        return v.StartsWith("sem", StringComparison.OrdinalIgnoreCase)
            || v.StartsWith("nenhum", StringComparison.OrdinalIgnoreCase)
            || v.Equals("não", StringComparison.OrdinalIgnoreCase)
            || v.Equals("nao", StringComparison.OrdinalIgnoreCase)
            || v.Equals("n/a", StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Os preços por diâmetro (entidade "precos_diametro"), sobre o mesmo
/// ParquetStore do resto do sistema.
/// </summary>
public sealed class PrecoDiametroRepository
{
    private const string Entidade = "precos_diametro";
    private const string EntidadeMigracoes = "precos_diametro_migracoes";

    private readonly ParquetStore _store;
    public PrecoDiametroRepository(ParquetStore store) => _store = store;

    public List<PrecoDiametro> Todos() => _store
        .ReadLatest(Entidade, "id, familia, diametro, preco, precoUsd, precoClp, medida",
            r => new PrecoDiametro
            {
                Id = S(r, 0), Familia = S(r, 1), Diametro = S(r, 2),
                Preco = S(r, 3), PrecoUsd = S(r, 4), PrecoClp = S(r, 5), Medida = S(r, 6),
            })
        .ToList();

    /// <summary>A tabela de uma família, indexada pelo rótulo do diâmetro.</summary>
    public Dictionary<string, PrecoDiametro> Da(string familia) => Todos()
        .Where(p => p.Familia == familia)
        .GroupBy(p => p.Diametro)
        .ToDictionary(g => g.Key, g => g.First());

    /// <summary>Quantos diâmetros já têm preço próprio nesta família.</summary>
    public int Quantos(string familia) => Todos().Count(p => p.Familia == familia);

    public void Salvar(PrecoDiametro p)
    {
        if (string.IsNullOrWhiteSpace(p.Id))
            p.Id = PrecoDiametro.MontarId(p.Familia, p.Diametro);

        if (p.Vazio)
        {
            Apagar(p.Id);
            return;
        }

        _store.WriteRow(Entidade, new KeyValuePair<string, object?>[]
        {
            new("id", p.Id), new("familia", p.Familia), new("diametro", p.Diametro),
            new("preco", p.Preco), new("precoUsd", p.PrecoUsd), new("precoClp", p.PrecoClp),
            new("medida", p.Medida),
        });
    }

    public void Apagar(string id) => _store.WriteRow(Entidade,
        new KeyValuePair<string, object?>[] { new("id", id) }, deleted: true);

    /// <summary>Esvazia a tabela de uma família.</summary>
    public void LimparFamilia(string familia)
    {
        foreach (var p in Todos().Where(p => p.Familia == familia)) Apagar(p.Id);
    }

    public void Limpar()
    {
        foreach (var p in Todos()) Apagar(p.Id);
    }

    /// <summary>
    /// Traz o que estava gravado por MODELO para a tabela por diâmetro.
    ///
    /// A versão anterior guardava um preço por equipamento montado (série +
    /// ventilador + cubo + FB/HB + estágios). Aqui cada um desses vira o preço
    /// do diâmetro dele; quando dois modelos do mesmo diâmetro tinham preços
    /// diferentes, o primeiro é o que fica — não há como adivinhar qual a equipe
    /// queria, e ela vê o resultado na tela.
    /// </summary>
    public void Converter(EquipamentoRepository equipamentos)
    {
        const string marca = "preco-por-diametro-v1";

        var aplicadas = _store
            .ReadLatest(EntidadeMigracoes, "id", r => r.IsDBNull(0) ? "" : r.GetString(0))
            .ToHashSet();
        if (aplicadas.Contains(marca)) return;

        // as duas formas antigas: colunas soltas de série/ventilador/cubo e,
        // depois, o id do modelo
        var antigas = _store.ReadLatest("precos_equipamento",
            "id, grupo, valor, equipamento, diametro, preco, precoUsd, precoClp",
            r => (Id: S(r, 0), Grupo: S(r, 1), Valor: S(r, 2), Modelo: S(r, 3),
                  Diametro: S(r, 4), Preco: S(r, 5), Usd: S(r, 6), Clp: S(r, 7)))
            .ToList();

        if (antigas.Count > 0)
        {
            var porId = equipamentos.Todos()
                .GroupBy(e => e.Id)
                .ToDictionary(g => g.Key, g => g.First().Diametro);

            var jaFeitos = new HashSet<string>();

            foreach (var a in antigas)
            {
                var familia = FamiliaDePreco.De(a.Grupo, a.Valor);
                if (familia is null) continue;

                var diametro = a.Diametro.Length > 0
                    ? a.Diametro
                    : porId.GetValueOrDefault(a.Modelo, "");
                if (diametro.Length == 0) continue;

                var id = PrecoDiametro.MontarId(familia.Chave, diametro);
                if (!jaFeitos.Add(id)) continue;

                Salvar(new PrecoDiametro
                {
                    Id = id, Familia = familia.Chave, Diametro = diametro,
                    Preco = a.Preco, PrecoUsd = a.Usd, PrecoClp = a.Clp,
                });

                // a linha antiga sai: ela não tem mais tela nem leitura
                _store.WriteRow("precos_equipamento",
                    new KeyValuePair<string, object?>[] { new("id", a.Id) }, deleted: true);
            }
        }

        _store.WriteRow(EntidadeMigracoes,
            new KeyValuePair<string, object?>[] { new("id", marca) });
    }

    private static string S(System.Data.IDataReader r, int i) => r.IsDBNull(i) ? "" : r.GetString(i);
}
