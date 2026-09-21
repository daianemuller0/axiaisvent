namespace HowdenAxiais.Poc.Data;

/// <summary>
/// Um motor do catálogo, como a equipe descreve na planilha dela: fabricante,
/// potência, frequência, rotação, número de polos, tipo de flange, padrão
/// (IEC/NEMA) e <b>frame</b> — a carcaça.
///
/// Antes esta lista era só a escada de frames. Virou o catálogo de motores, e o
/// frame passou a ser uma coluna. A escada continua existindo, agora
/// <i>derivada</i>: são os frames distintos na ordem em que aparecem na lista
/// (<see cref="MotorRepository.Escada"/>). Isso importa porque é ela que
/// responde "esse motor passa do máximo que cabe no cubo?" — comparar texto não
/// resolveria, e vários motores dividem a mesma carcaça.
/// </summary>
public sealed class Motor
{
    /// <summary>
    /// Chave própria, sem significado. Antes o id era "padrão-frame"; não serve
    /// mais, porque agora vários motores compartilham a mesma carcaça.
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    /// Linha de produto a que o motor atende: VAX, Joy — ou <b>vazio</b>, que
    /// quer dizer "serve às duas". A escada de carcaças continua sendo por
    /// padrão, não por série: o tamanho de uma carcaça é físico, é o mesmo nas
    /// duas linhas.
    /// </summary>
    public string Serie { get; set; } = "";

    public string Fabricante { get; set; } = "";
    /// <summary>Potência em CV, como a equipe digita ("7,5", "10").</summary>
    public string PotenciaCv { get; set; } = "";
    /// <summary>Frequência em Hz ("60", "50").</summary>
    public string Frequencia { get; set; } = "";
    /// <summary>Tensão, como a equipe escreve ("220/380 V", "440").</summary>
    public string Tensao { get; set; } = "";
    /// <summary>Rotação em rpm ("1750", "3500").</summary>
    public string Rotacao { get; set; } = "";
    /// <summary>Número de polos ("2", "4", "6").</summary>
    public string Polos { get; set; } = "";
    /// <summary>Tipo de flange ("B3", "B5", "C-Face"…).</summary>
    public string Flange { get; set; } = "";

    /// <summary>Padrão da carcaça: IEC ou NEMA.</summary>
    public string Padrao { get; set; } = "";
    /// <summary>A carcaça, como a equipe escreve: "225S/M", "364/5T".</summary>
    public string Frame { get; set; } = "";

    /// <summary>Posição na lista, dentro do padrão (1 = o primeiro/menor).</summary>
    public int Ordem { get; set; }
    /// <summary>Código do motor, que entra na montagem do código do equipamento.</summary>
    public string Codigo { get; set; } = "";
    /// <summary>Preço, como a equipe digita. Vazio = sem preço.</summary>
    public string Preco { get; set; } = "";
    /// <summary>Observações em texto livre — o que não coube nas outras colunas.</summary>
    public string Observacoes { get; set; } = "";

    /// <summary>Como o motor aparece numa mensagem: o frame, ou o que houver.</summary>
    public string Descricao
    {
        get
        {
            var partes = new[] { Serie, Fabricante, Frame, PotenciaCv.Length > 0 ? PotenciaCv + " CV" : "" }
                .Where(p => p.Length > 0)
                .ToList();
            return partes.Count > 0 ? string.Join(" ", partes) : "(motor sem descrição)";
        }
    }
}

/// <summary>
/// Cadastro dos motores, sobre o mesmo ParquetStore do resto do sistema.
/// </summary>
public sealed class MotorRepository
{
    // A pasta continua se chamando "frames" de propósito: é a que já existe no
    // compartilhamento de rede, com os dados gravados. O nome é interno.
    private const string Entidade = "frames";
    private const string EntidadeSemeados = "frames_semeados";

    /// <summary>Padrões conhecidos, na ordem em que aparecem na lista da equipe.</summary>
    public static readonly string[] Padroes = { "IEC", "NEMA" };

    private readonly ParquetStore _store;
    public MotorRepository(ParquetStore store) => _store = store;

    public List<Motor> Todos() => _store
        // "nome" é como a carcaça se chamava antes de a lista virar catálogo de
        // motores; bancos gravados naquela época ainda trazem a coluna antiga.
        .ReadLatest(Entidade,
            "id, padrao, frame, nome, ordem, codigo, preco, " +
            "fabricante, potenciaCv, frequencia, rotacao, polos, flange, " +
            "tensao, observacoes, serie",
            r => new Motor
            {
                Id = S(r, 0), Padrao = S(r, 1),
                Frame = S(r, 2).Length > 0 ? S(r, 2) : S(r, 3),
                Ordem = Int(S(r, 4)), Codigo = S(r, 5), Preco = S(r, 6),
                Fabricante = S(r, 7), PotenciaCv = S(r, 8), Frequencia = S(r, 9),
                Rotacao = S(r, 10), Polos = S(r, 11), Flange = S(r, 12),
                Tensao = S(r, 13), Observacoes = S(r, 14), Serie = S(r, 15),
            })
        .OrderBy(m => PosicaoDoPadrao(m.Padrao))
        .ThenBy(m => m.Ordem)
        .ToList();

    public static int PosicaoDoPadrao(string padrao)
    {
        var i = Array.IndexOf(Padroes, padrao);
        return i >= 0 ? i : Padroes.Length;
    }

    /// <summary>
    /// A escada de tamanho de um padrão: os frames distintos, na ordem da lista.
    /// Vários motores na mesma carcaça contam uma vez só, pela primeira
    /// aparição — é a posição dela que a regra do cubo compara.
    /// </summary>
    public static List<string> Escada(IEnumerable<Motor> motores, string padrao) => motores
        .Where(m => m.Padrao == padrao && m.Frame.Length > 0)
        .OrderBy(m => m.Ordem)
        .Select(m => m.Frame)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    public void Salvar(Motor m)
    {
        if (string.IsNullOrWhiteSpace(m.Id)) m.Id = Guid.NewGuid().ToString("N");

        _store.WriteRow(Entidade, new KeyValuePair<string, object?>[]
        {
            new("id", m.Id), new("padrao", m.Padrao), new("frame", m.Frame),
            // ordem com zeros à esquerda: o Parquet guarda texto, e sem isso a
            // posição 10 viria antes da 2.
            new("ordem", m.Ordem.ToString("D4")),
            new("codigo", m.Codigo), new("preco", m.Preco),
            new("fabricante", m.Fabricante), new("potenciaCv", m.PotenciaCv),
            new("frequencia", m.Frequencia), new("rotacao", m.Rotacao),
            new("polos", m.Polos), new("flange", m.Flange),
            new("tensao", m.Tensao), new("observacoes", m.Observacoes),
            new("serie", m.Serie),
        });
    }

    public void Apagar(string id) => _store.WriteRow(Entidade,
        new KeyValuePair<string, object?>[] { new("id", id) }, deleted: true);

    /// <summary>
    /// Carrega a escada de fábrica uma única vez — como motores com só a
    /// carcaça preenchida, para a equipe completar ou trocar pela lista dela.
    /// A marca é o que permite apagar tudo e subir a sua: sem ela, o critério
    /// seria "a tabela está vazia, então carrega", e a limpeza voltaria atrás
    /// sozinha na abertura seguinte.
    /// </summary>
    public void SemearSeVazio()
    {
        if (!_store.IsEmpty(EntidadeSemeados)) return;
        if (_store.IsEmpty(Entidade))
            foreach (var m in MotoresSeed.Lista()) Salvar(m);

        Marcar();
    }

    /// <summary>Apaga só os motores de um padrão (IEC ou NEMA).</summary>
    public void LimparPadrao(string padrao)
    {
        foreach (var m in Todos().Where(m => m.Padrao == padrao)) Apagar(m.Id);
        Marcar();
    }

    /// <summary>Apaga exatamente estes motores (o que o filtro de texto mostrou).</summary>
    public void LimparEstes(IEnumerable<string> ids)
    {
        foreach (var id in ids) Apagar(id);
        Marcar();
    }

    /// <summary>Apaga a lista inteira e marca a de fábrica como já carregada.</summary>
    public void Limpar()
    {
        _store.Clear(Entidade);
        Marcar();
    }

    private void Marcar() => _store.WriteRow(EntidadeSemeados,
        new KeyValuePair<string, object?>[] { new("id", "frames") });

    private static string S(System.Data.IDataReader r, int i) => r.IsDBNull(i) ? "" : r.GetString(i);

    private static int Int(string s) => int.TryParse(s, out var v) ? v : 0;
}

/// <summary>
/// Lista de fábrica das carcaças, na ordem de tamanho que a equipe usa —
/// 19 IEC e 15 NEMA. Entram como motores com só o frame preenchido: o resto
/// (fabricante, potência, polos…) é da planilha da equipe.
/// </summary>
public static class MotoresSeed
{
    private static readonly string[] Iec =
    {
        "< 112M", "112M", "132S", "132M", "132M/L", "160M", "160L", "180M", "180L",
        "200M", "200L", "225S/M", "250S/M", "280S/M", "315S/M", "315M/L", "315L",
        "355M/L", "355A/B",
    };

    private static readonly string[] Nema =
    {
        "254T", "254/6T", "284T", "284/6T", "324T", "324/6T", "326T", "364/5T",
        "404/5T", "444/5T", "445/7T", "447/9T", "504/5T", "586/7T", "588/9T",
    };

    public static List<Motor> Lista()
    {
        var lista = new List<Motor>();
        foreach (var (padrao, frames) in new[] { ("IEC", Iec), ("NEMA", Nema) })
        {
            for (var i = 0; i < frames.Length; i++)
            {
                lista.Add(new Motor
                {
                    // id estável: a semeadura roda uma vez só, mas se um banco
                    // antigo já tem estas linhas, elas casam pelo mesmo id
                    Id = $"{padrao}-{frames[i]}",
                    Padrao = padrao,
                    Frame = frames[i],
                    Ordem = i + 1,
                });
            }
        }
        return lista;
    }
}
