namespace HowdenAxiais.Poc.Data;

/// <summary>O que um preço acompanha, quando ele não é um preço só.</summary>
public enum EixoDePreco
{
    /// <summary>O diâmetro do ventilador: todos os 45 pelo mesmo valor.</summary>
    FanDiameter,
    /// <summary>A potência do motor elétrico, em CV — o caso dos partidores.</summary>
    PotenciaMotor,
}

/// <summary>
/// O preço de uma opção para UM valor de referência.
///
/// A equipe foi direta sobre o acessório: "todos os 45 terão o mesmo preço,
/// independente se é Joy ou VAX e independente do cubo" — um difusor de 45"
/// custa o que custa, não importa em que cubo ele vai. E foi igualmente direta
/// sobre o partidor: ele acompanha a <b>potência do motor</b>, não o ventilador.
///
/// Por isso a referência é um texto, e quem diz o que ele significa é o eixo da
/// família: rótulo de Fan Diameter ou potência em CV.
///
/// A chave é a <b>família</b> (quem divide a mesma tabela) mais a referência.
/// </summary>
public sealed class PrecoReferencia
{
    public string Id { get; set; } = "";

    /// <summary>A família de preço — ver <see cref="FamiliaDePreco"/>.</summary>
    public string Familia { get; set; } = "";

    /// <summary>
    /// O valor de referência, como está no cadastro: "45" e "2400" (Fan
    /// Diameter) ou "7,5" e "150" (potência em CV).
    /// </summary>
    public string Referencia { get; set; } = "";

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

    public static string MontarId(string familia, string referencia) => $"{familia}|{referencia}";
}

/// <summary>
/// De qual tabela de preço por diâmetro uma opção se serve.
///
/// Duas listas podem dividir a MESMA tabela sem deixar de ser duas listas: o
/// silenciador de entrada e o de descarga têm códigos diferentes e preço igual,
/// e o mesmo vale para a conexão a manga da entrada e a da saída. Quem decide
/// isso é a família, não a lista.
/// </summary>
public sealed record FamiliaDePreco(
    string Chave, string Rotulo, EixoDePreco Eixo, bool UsaMedida, string Aviso)
{
    /// <summary>Como a coluna de referência se chama na tela e na planilha.</summary>
    public string RotuloDoEixo =>
        Eixo == EixoDePreco.PotenciaMotor ? "Potência (CV)" : "Fan Diameter";

    /// <summary>O texto do botão que abre a tabela.</summary>
    public string RotuloDoBotao =>
        Eixo == EixoDePreco.PotenciaMotor ? "por potência" : "por diâmetro";


    /// <summary>
    /// As listas cujo preço muda com o Fan Diameter — as que a equipe nomeou.
    /// Basta a palavra aparecer no nome da lista, então renomear "Damper
    /// mariposa" para "Damper mariposa saída" não quebra nada.
    ///
    /// Silenciador e conexão a manga têm regra própria (dividem tabela) e estão
    /// tratados antes desta lista.
    /// </summary>
    private static readonly string[] ListasPorDiametro = { "cone", "difusor", "damper" };

    /// <summary>
    /// As listas cujo preço acompanha a potência do motor elétrico. Partidor é
    /// equipamento elétrico: quem dimensiona um softstarter é o motor que ele
    /// vai partir, não o tamanho do ventilador.
    /// </summary>
    private static readonly string[] ListasPorPotencia = { "partidor" };

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
            return new("Conexão a manga", "Conexão a manga", EixoDePreco.FanDiameter, true,
                "Mesma tabela para a conexão a manga da entrada e a da saída — e o " +
                "diâmetro em mm da peça fica nesta mesma linha.");
        }

        // base: a base normal e a com trenó custam o mesmo, então é uma tabela
        // só para a lista inteira — as opções seguem separadas pelo código
        if (grupo.Contains("base", StringComparison.OrdinalIgnoreCase))
        {
            return new("Base", "Base", EixoDePreco.FanDiameter, false,
                "Mesma tabela para a base normal e a base com trenó.");
        }

        // silenciador: entrada e descarga dividem o preço, opção por opção
        // (L = 1,0D não custa o que custa L = 2,0D)
        if (grupo.StartsWith("Silenciador", StringComparison.OrdinalIgnoreCase))
        {
            return new($"Silenciador|{v}", $"Silenciador · {v}", EixoDePreco.FanDiameter, false,
                "Mesma tabela para o silenciador de entrada e o de descarga.");
        }

        var lista = grupo.ToLowerInvariant();

        if (ListasPorPotencia.Any(chave => lista.Contains(chave)))
        {
            return new($"{grupo}|{v}", $"{grupo} · {v}", EixoDePreco.PotenciaMotor, false,
                "O preço acompanha a potência do motor elétrico, em CV — as linhas saem " +
                "do catálogo de motores.");
        }

        if (!ListasPorDiametro.Any(chave => lista.Contains(chave))) return null;

        return new($"{grupo}|{v}", $"{grupo} · {v}", EixoDePreco.FanDiameter, false, "");
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
public sealed class PrecoReferenciaRepository
{
    private const string Entidade = "precos_diametro";
    private const string EntidadeMigracoes = "precos_diametro_migracoes";

    private readonly ParquetStore _store;
    public PrecoReferenciaRepository(ParquetStore store) => _store = store;

    public List<PrecoReferencia> Todos() => _store
        // "diametro" é o nome que a coluna tinha quando só havia um eixo; fica na
        // leitura para o que já está gravado continuar valendo
        .ReadLatest(Entidade, "id, familia, referencia, diametro, preco, precoUsd, precoClp, medida",
            r => new PrecoReferencia
            {
                Id = S(r, 0), Familia = S(r, 1),
                Referencia = S(r, 2).Length > 0 ? S(r, 2) : S(r, 3),
                Preco = S(r, 4), PrecoUsd = S(r, 5), PrecoClp = S(r, 6), Medida = S(r, 7),
            })
        .ToList();

    /// <summary>A tabela de uma família, indexada pela referência.</summary>
    public Dictionary<string, PrecoReferencia> Da(string familia) => Todos()
        .Where(p => p.Familia == familia)
        .GroupBy(p => p.Referencia)
        .ToDictionary(g => g.Key, g => g.First());

    /// <summary>Quantas referências já têm preço próprio nesta família.</summary>
    public int Quantos(string familia) => Todos().Count(p => p.Familia == familia);

    public void Salvar(PrecoReferencia p)
    {
        if (string.IsNullOrWhiteSpace(p.Id))
            p.Id = PrecoReferencia.MontarId(p.Familia, p.Referencia);

        if (p.Vazio)
        {
            Apagar(p.Id);
            return;
        }

        _store.WriteRow(Entidade, new KeyValuePair<string, object?>[]
        {
            new("id", p.Id), new("familia", p.Familia), new("referencia", p.Referencia),
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

    /// <summary>
    /// A opção mudou de nome e, com ela, a família — leva os preços junto, senão
    /// eles ficariam presos a uma família que ninguém mais abre.
    /// </summary>
    public void RenomearFamilia(string de, string para)
    {
        if (de.Length == 0 || para.Length == 0 || de == para) return;

        var destino = Da(para);

        foreach (var p in Todos().Where(p => p.Familia == de))
        {
            Apagar(p.Id);

            // a família de destino já pode ter preço (renomear para um nome que
            // divide tabela com outra opção): o que já está lá manda
            if (destino.ContainsKey(p.Referencia)) continue;

            Salvar(new PrecoReferencia
            {
                Familia = para, Referencia = p.Referencia,
                Preco = p.Preco, PrecoUsd = p.PrecoUsd, PrecoClp = p.PrecoClp,
                Medida = p.Medida,
            });
        }
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

                var id = PrecoReferencia.MontarId(familia.Chave, diametro);
                if (!jaFeitos.Add(id)) continue;

                Salvar(new PrecoReferencia
                {
                    Id = id, Familia = familia.Chave, Referencia = diametro,
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

    /// <summary>
    /// A base era uma tabela por opção ("Base|Com BASE", "Base|Com TRENÓ") antes
    /// de a equipe dizer que o trenó custa o mesmo da base normal. Junta o que
    /// estava gravado na família única "Base", uma vez.
    /// </summary>
    public void JuntarFamiliaDaBase()
    {
        const string marca = "familia-base-unica-v1";

        var aplicadas = _store
            .ReadLatest(EntidadeMigracoes, "id", r => r.IsDBNull(0) ? "" : r.GetString(0))
            .ToHashSet();
        if (aplicadas.Contains(marca)) return;

        var jaTem = Da("Base");

        foreach (var p in Todos().Where(p => p.Familia.StartsWith("Base|", StringComparison.Ordinal)))
        {
            Apagar(p.Id);

            // duas opções podiam ter valores diferentes; o primeiro é o que fica
            if (jaTem.ContainsKey(p.Referencia)) continue;

            var novo = new PrecoReferencia
            {
                Familia = "Base", Referencia = p.Referencia,
                Preco = p.Preco, PrecoUsd = p.PrecoUsd, PrecoClp = p.PrecoClp,
            };
            Salvar(novo);
            jaTem[p.Referencia] = novo;
        }

        _store.WriteRow(EntidadeMigracoes,
            new KeyValuePair<string, object?>[] { new("id", marca) });
    }

    private static string S(System.Data.IDataReader r, int i) => r.IsDBNull(i) ? "" : r.GetString(i);
}
