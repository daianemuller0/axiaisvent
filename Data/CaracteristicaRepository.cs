namespace HowdenAxiais.Poc.Data;

/// <summary>
/// Um item de uma lista de característica do equipamento (solidez, base,
/// partidor, instrumentação…).
///
/// O <see cref="Codigo"/> é o que a equipe vai subir depois: juntando os
/// códigos individuais escolhidos em cada lista, monta-se o código do
/// equipamento. Por isso a ORDEM DOS GRUPOS importa — é a ordem em que os
/// pedaços entram nesse código.
/// </summary>
public sealed class Caracteristica
{
    public string Id { get; set; } = "";
    /// <summary>Nome da lista: "Solidez", "Base", "PARTIDORES"…</summary>
    public string Grupo { get; set; } = "";
    /// <summary>A opção, como a equipe escreve: "Com TRENÓ", "VDF IP65".</summary>
    public string Valor { get; set; } = "";
    /// <summary>Código do item. Vazio enquanto a equipe não subir os códigos.</summary>
    public string Codigo { get; set; } = "";
    /// <summary>Preço do subitem, como a equipe digita. Vazio = sem preço.</summary>
    public string Preco { get; set; } = "";
    /// <summary>Posição dentro do grupo (1 = primeiro da lista).</summary>
    public int Ordem { get; set; }

    public static string MontarId(string grupo, string valor) => $"{grupo}|{valor}";
}

/// <summary>
/// Uma LISTA de característica — o "item", na fala da equipe. Os valores dentro
/// dela são os "subitens".
///
/// A lista é entidade própria (e não só um campo dos subitens) por dois
/// motivos: dá para criar uma lista vazia e só depois preenchê-la, e a ORDEM
/// das listas fica guardada — é ela que vai definir a ordem dos pedaços no
/// código do equipamento.
/// </summary>
public sealed class GrupoCaracteristica
{
    /// <summary>O id é o próprio nome: a lista é identificada por ele.</summary>
    public string Id { get; set; } = "";
    public string Nome { get; set; } = "";
    public int Ordem { get; set; }
}

/// <summary>
/// Cadastro das listas de características (entidades "caracteristica_grupos"
/// para as listas e "caracteristicas" para os subitens), sobre o mesmo
/// ParquetStore do resto do sistema.
/// </summary>
public sealed class CaracteristicaRepository
{
    private const string Entidade = "caracteristicas";
    private const string EntidadeGrupos = "caracteristica_grupos";

    private readonly ParquetStore _store;
    public CaracteristicaRepository(ParquetStore store) => _store = store;

    public List<Caracteristica> Todas() => _store
        .ReadLatest(Entidade, "id, grupo, valor, codigo, ordem, preco", r => new Caracteristica
        {
            Id = S(r, 0), Grupo = S(r, 1), Valor = S(r, 2), Codigo = S(r, 3), Ordem = Int(S(r, 4)),
            Preco = S(r, 5),
        })
        .OrderBy(c => c.Grupo)
        .ThenBy(c => c.Ordem)
        .ToList();

    public void Salvar(Caracteristica c)
    {
        if (string.IsNullOrWhiteSpace(c.Id)) c.Id = Caracteristica.MontarId(c.Grupo, c.Valor);
        _store.WriteRow(Entidade, new KeyValuePair<string, object?>[]
        {
            new("id", c.Id), new("grupo", c.Grupo), new("valor", c.Valor),
            new("codigo", c.Codigo), new("preco", c.Preco),
            // zeros à esquerda: o Parquet guarda texto e sem isso a 10 viria antes da 2
            new("ordem", c.Ordem.ToString("D4")),
        });
    }

    public void Apagar(string id) => _store.WriteRow(Entidade,
        new KeyValuePair<string, object?>[] { new("id", id) }, deleted: true);

    // ---------------- as listas (itens) ----------------

    public List<GrupoCaracteristica> Grupos() => _store
        .ReadLatest(EntidadeGrupos, "id, nome, ordem", r => new GrupoCaracteristica
        {
            Id = S(r, 0), Nome = S(r, 1), Ordem = Int(S(r, 2)),
        })
        .OrderBy(g => g.Ordem)
        .ThenBy(g => g.Nome)
        .ToList();

    public void SalvarGrupo(GrupoCaracteristica g)
    {
        if (string.IsNullOrWhiteSpace(g.Id)) g.Id = g.Nome;
        _store.WriteRow(EntidadeGrupos, new KeyValuePair<string, object?>[]
        {
            new("id", g.Id), new("nome", g.Nome), new("ordem", g.Ordem.ToString("D4")),
        });
    }

    /// <summary>Cria a lista se ela ainda não existir, no fim da ordem.</summary>
    public void GarantirGrupo(string nome)
    {
        nome = nome.Trim();
        if (nome.Length == 0) return;

        var grupos = Grupos();
        if (grupos.Any(g => g.Nome.Equals(nome, StringComparison.OrdinalIgnoreCase))) return;

        SalvarGrupo(new GrupoCaracteristica
        {
            Nome = nome,
            Ordem = grupos.Count == 0 ? 1 : grupos[^1].Ordem + 1,
        });
    }

    /// <summary>Apaga a lista E todos os subitens dela.</summary>
    public void ApagarGrupo(string nome)
    {
        foreach (var c in Todas().Where(c => c.Grupo == nome)) Apagar(c.Id);
        _store.WriteRow(EntidadeGrupos,
            new KeyValuePair<string, object?>[] { new("id", nome) }, deleted: true);
    }

    /// <summary>
    /// Renomeia a lista e leva junto os subitens — o nome do grupo faz parte do
    /// id deles, então cada um é apagado e regravado com o nome novo.
    /// </summary>
    public void RenomearGrupo(string de, string para)
    {
        para = para.Trim();
        if (para.Length == 0 || para == de) return;

        var antigo = Grupos().FirstOrDefault(g => g.Nome == de);
        var ordem = antigo?.Ordem ?? Grupos().Count + 1;

        foreach (var c in Todas().Where(c => c.Grupo == de))
        {
            Apagar(c.Id);
            Salvar(new Caracteristica
            {
                Grupo = para, Valor = c.Valor, Codigo = c.Codigo, Preco = c.Preco, Ordem = c.Ordem,
            });
        }

        _store.WriteRow(EntidadeGrupos,
            new KeyValuePair<string, object?>[] { new("id", de) }, deleted: true);
        SalvarGrupo(new GrupoCaracteristica { Nome = para, Ordem = ordem });
    }

    // ---------------- semeadura ----------------

    /// <summary>
    /// Carrega as listas de fábrica que ainda não existem. Por lista, e não pela
    /// entidade inteira: uma lista nova entra sem tocar nas que a equipe já
    /// ajustou ou já codificou.
    /// </summary>
    public void SemearSeVazio()
    {
        // 1) as listas. Bancos anteriores guardavam o grupo só dentro dos
        //    subitens — então qualquer grupo que já exista lá também vira lista.
        if (Grupos().Count == 0)
        {
            var nomes = CaracteristicasSeed.Grupos.ToList();
            foreach (var g in Todas().Select(c => c.Grupo).Distinct())
                if (!nomes.Contains(g)) nomes.Add(g);

            for (var i = 0; i < nomes.Count; i++)
                SalvarGrupo(new GrupoCaracteristica { Nome = nomes[i], Ordem = i + 1 });
        }

        // 2) os subitens de fábrica das listas que ainda estão vazias
        var comSubitens = Todas().Select(c => c.Grupo).ToHashSet();
        foreach (var c in CaracteristicasSeed.Lista())
        {
            if (comSubitens.Contains(c.Grupo)) continue;
            Salvar(c);
        }
    }

    private static string S(System.Data.IDataReader r, int i) => r.IsDBNull(i) ? "" : r.GetString(i);

    private static int Int(string s) => int.TryParse(s, out var v) ? v : 0;
}

/// <summary>
/// As listas de fábrica, transcritas do documento de características do
/// equipamento. A ordem dos grupos é a do documento — e é a candidata natural
/// à ordem dos pedaços no código do equipamento.
/// </summary>
public static class CaracteristicasSeed
{
    /// <summary>Os grupos na ordem do documento.</summary>
    public static readonly string[] Grupos =
    {
        "Solidez",
        "# Estágios",
        "Base",
        "Lubrificação",
        "Contrarrecuo",
        "Polaridade e freq Motor",
        "Potencia Motor CV [kW]",
        "Forn. Motor e Flange",
        "Cone de entrada",
        "Silenciador entrada",
        "Silenciador descarga",
        "Difusor",
        "Conexao manga descarga",
        "PARTIDORES",
        "INSTRUMENTAÇÃO",
    };

    public static int PosicaoDoGrupo(string grupo)
    {
        var i = Array.IndexOf(Grupos, grupo);
        return i >= 0 ? i : Grupos.Length;
    }

    private static readonly string[] Silenciador =
    {
        "Sem", "L = 1,0D", "L = 1,5D", "L = 2,0D",
        "L = 1,0D C/NUCLEO", "L = 1,5D C/NUCLEO", "L = 2,0D C/NUCLEO",
    };

    public static List<Caracteristica> Lista()
    {
        var tabela = new (string Grupo, string[] Valores)[]
        {
            ("Solidez", new[] { "FB", "HB" }),

            ("# Estágios", new[] { "1STG", "2STG" }),

            ("Base", new[] { "SEM Base", "Com BASE", "Com TRENÓ" }),

            ("Lubrificação", new[] { "SEM lubrif. automatico", "com LUBRIF. automatico" }),

            ("Contrarrecuo", new[] { "Sem Contrarrecuo", "Freio" }),

            ("Polaridade e freq Motor", new[]
            {
                "3000 rpm / 50 Hz", "1500 rpm / 50 Hz", "1000 rpm / 50 Hz", "750 rpm / 50 Hz",
                "600 rpm / 50 Hz", "500 rpm / 50 Hz", "428,6 rpm / 50 Hz",
                "3600 rpm / 60 Hz", "1800 rpm / 60 Hz", "1200 rpm / 60 Hz", "900 rpm / 60 Hz",
                "720 rpm / 60 Hz", "600 rpm / 60 Hz", "514,3 rpm / 60 Hz",
            }),

            ("Potencia Motor CV [kW]", new[]
            {
                "< 5 [3,7]", "5 [3,7]", "5,5 [4,1]", "6 [4,5]", "7,5 [5,5]", "10 [7,5]",
                "12,5 [9,2]", "15 [11]", "20 [15]", "25 [18,5]", "30 [22]", "40 [30]",
                "50 [37]", "60 [45]", "75 [55]", "100 [75]", "125 [90]", "150 [110]",
                "175 [132]", "200 [150]", "250 [185]", "300 [220]", "350 [260]", "380 [285]",
                "400 [300]", "430 [320]", "450 [330]", "480 [360]", "500 [370]", "550 [400]",
            }),

            ("Forn. Motor e Flange", new[]
            {
                "WEG FC", "WEG FF", "OMEC", "ABLE", "ABB", "SIEMENS", "WOLONG",
            }),

            ("Cone de entrada", new[] { "Sem", "Com Cone", "Com Conexão para Manga" }),

            ("Silenciador entrada", Silenciador),
            ("Silenciador descarga", Silenciador),

            ("Difusor", new[] { "Sem", "Com" }),

            ("Conexao manga descarga", new[] { "Sem", "Com Conexão para Manga" }),

            ("PARTIDORES", new[]
            {
                "NENHUM", "DOL IP65", "ESTRELA-TRIANGULO IP65", "SOFTSTARTER IP54",
                "SOFTSTARTER IP65", "VDF IP54", "VDF IP65", "ESPECIAL",
            }),

            ("INSTRUMENTAÇÃO", new[]
            {
                "NENHUM",
                "SENSOR DE VIBRAÇÃO",
                "SENSOR DE PRESSÃO DIFERENCIAL",
                "SENSOR DE VAZÃO",
                "SENSORES VIBRAÇÃO + PRESSÃO DIFFERENCIAL",
                "SENSORES VIBRAÇÃO + VAZÃO",
                "SENSORES PRESSÃO DIFFERENCIAL+ VAZÃO",
                "SENSORES VIBRAÇÃO + PRESSÃO DIFFERENCIAL+ VAZÃO",
            }),
        };

        var lista = new List<Caracteristica>();
        foreach (var (grupo, valores) in tabela)
        {
            for (var i = 0; i < valores.Length; i++)
            {
                lista.Add(new Caracteristica
                {
                    Id = Caracteristica.MontarId(grupo, valores[i]),
                    Grupo = grupo,
                    Valor = valores[i],
                    Codigo = "",
                    Ordem = i + 1,
                });
            }
        }
        return lista;
    }
}
