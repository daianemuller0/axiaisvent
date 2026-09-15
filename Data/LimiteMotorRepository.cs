namespace HowdenAxiais.Poc.Data;

/// <summary>
/// O maior motor que cabe dentro de um cubo, por padrão de frame.
///
/// É a linha "Maximum Internal Motor" da planilha: para cada Fan Hub Diameter,
/// um frame máximo em IEC e outro em NEMA.
/// </summary>
public sealed class LimiteMotor
{
    public string Id { get; set; } = "";
    /// <summary>Linha de equipamento: VAX ou Joy.</summary>
    public string Serie { get; set; } = "";
    /// <summary>Fan Hub Diameter, com o mesmo rótulo usado nos equipamentos.</summary>
    public string Cubo { get; set; } = "";
    /// <summary>Padrão do frame: IEC ou NEMA.</summary>
    public string Padrao { get; set; } = "";
    /// <summary>Nome do maior frame que cabe ("225S/M", "364/5T").</summary>
    public string Frame { get; set; } = "";

    public static string MontarId(string serie, string cubo, string padrao) =>
        $"{serie}-{cubo}-{padrao}";
}

/// <summary>
/// Limites de motor por cubo (entidade "limites_motor"), sobre o mesmo
/// ParquetStore do resto do sistema.
/// </summary>
public sealed class LimiteMotorRepository
{
    private const string Entidade = "limites_motor";

    private readonly ParquetStore _store;
    public LimiteMotorRepository(ParquetStore store) => _store = store;

    public List<LimiteMotor> Todos() => _store
        .ReadLatest(Entidade, "id, serie, cubo, padrao, frame", r => new LimiteMotor
        {
            Id = S(r, 0), Serie = S(r, 1), Cubo = S(r, 2), Padrao = S(r, 3), Frame = S(r, 4),
        })
        .ToList();

    public void Salvar(LimiteMotor l)
    {
        if (string.IsNullOrWhiteSpace(l.Id)) l.Id = LimiteMotor.MontarId(l.Serie, l.Cubo, l.Padrao);
        _store.WriteRow(Entidade, new KeyValuePair<string, object?>[]
        {
            new("id", l.Id), new("serie", l.Serie), new("cubo", l.Cubo),
            new("padrao", l.Padrao), new("frame", l.Frame),
        });
    }

    public void Apagar(string id) => _store.WriteRow(Entidade,
        new KeyValuePair<string, object?>[] { new("id", id) }, deleted: true);

    /// <summary>
    /// Carrega os limites de fábrica que ainda não existem, um a um — mesma
    /// ideia da semeadura por bloco dos equipamentos: o que a equipe ajustar
    /// à mão não é sobrescrito, e um limite novo entra sem tocar nos outros.
    /// </summary>
    public void SemearSeVazio()
    {
        var existentes = Todos().Select(l => l.Id).ToHashSet();
        foreach (var l in LimitesSeed.Lista())
        {
            if (existentes.Contains(l.Id)) continue;
            Salvar(l);
        }
    }

    private static string S(System.Data.IDataReader r, int i) => r.IsDBNull(i) ? "" : r.GetString(i);
}

/// <summary>
/// A regra do motor: o frame escolhido cabe no cubo?
///
/// A comparação é por POSIÇÃO na escada de tamanho do padrão (ver
/// <see cref="FrameMotor.Ordem"/>), nunca por nome — "315L" e "315M/L" não se
/// ordenam por texto.
/// </summary>
public static class RegraMotor
{
    public enum Situacao { Ok, SemLimite, FrameDesconhecido, AcimaDoLimite }

    public sealed record Resultado(Situacao Situacao, string Mensagem);

    public static Resultado Verificar(List<LimiteMotor> limites, List<FrameMotor> frames,
        string serie, string cubo, string padrao, string frameEscolhido)
    {
        if (string.IsNullOrWhiteSpace(frameEscolhido))
            return new(Situacao.SemLimite, "Escolha um frame para verificar o motor.");

        var limite = limites.FirstOrDefault(l =>
            l.Serie == serie && l.Cubo == cubo && l.Padrao == padrao);

        if (limite is null || limite.Frame.Length == 0)
        {
            return new(Situacao.SemLimite,
                $"Ainda não há motor máximo cadastrado para o cubo {cubo} em {padrao} — " +
                "preencha na tabela abaixo.");
        }

        var escada = frames.Where(f => f.Padrao == padrao).OrderBy(f => f.Ordem).ToList();
        var posMaximo = escada.FindIndex(f => f.Nome == limite.Frame);
        var posEscolhido = escada.FindIndex(f => f.Nome == frameEscolhido);

        if (posMaximo < 0)
        {
            return new(Situacao.FrameDesconhecido,
                $"O motor máximo do cubo {cubo} está cadastrado como \"{limite.Frame}\", que não " +
                $"está na lista de frames {padrao} — sem a posição dele na escada não dá para " +
                "comparar. Inclua esse frame na aba Motores ou corrija o limite.");
        }

        if (posEscolhido < 0)
        {
            return new(Situacao.FrameDesconhecido,
                $"O frame \"{frameEscolhido}\" não está na lista {padrao}.");
        }

        if (posEscolhido > posMaximo)
        {
            return new(Situacao.AcimaDoLimite,
                $"Motor {frameEscolhido} não cabe no cubo {cubo}: o maior frame {padrao} que entra " +
                $"nesse cubo é {limite.Frame}.");
        }

        return new(Situacao.Ok,
            $"Motor {frameEscolhido} cabe no cubo {cubo} — o máximo em {padrao} é {limite.Frame}.");
    }
}

/// <summary>
/// Limites de fábrica, da linha "Maximum Internal Motor Frame" (a faixa verde)
/// das abas VAX e JOY da planilha.
///
/// Na planilha o valor é uma célula mesclada sobre 60Hz e 50Hz — o frame máximo
/// é o mesmo nas duas frequências —, por isso o limite aqui é por
/// (série, cubo, padrão), sem frequência.
///
/// ATENÇÃO: vários destes rótulos NÃO existem na lista de frames da equipe
/// (180M/L, 286T, 355S/M, 315S/M/L, 444/5TSC, 504/5TSC). São gravados como
/// estão na planilha; o sistema aponta a divergência em vez de escondê-la.
/// </summary>
public static class LimitesSeed
{
    public static List<LimiteMotor> Lista()
    {
        var tabela = new (string Serie, string Cubo, string Iec, string Nema)[]
        {
            ("VAX", "1800", "225S/M", "364/5T"),
            ("VAX", "2100", "250S/M", "404/5T"),
            ("VAX", "2700", "315S/M", "504/5T"),
            ("VAX", "3150", "355S/M", "586/7T"),

            ("Joy", "14\", S1000",     "180M/L",   "286T"),
            ("Joy", "17 1/2\", S1000", "180M/L",   "286T"),
            ("Joy", "21\", S2000",     "250S/M",   "404/5T"),
            ("Joy", "26\", S1000",     "280S/M",   "444/5TSC"),
            ("Joy", "26\", S2000",     "280S/M",   "444/5TSC"),
            ("Joy", "30\", S2000",     "315S/M/L", "504/5TSC"),
        };

        var lista = new List<LimiteMotor>();
        foreach (var (serie, cubo, iec, nema) in tabela)
        {
            foreach (var (padrao, frame) in new[] { ("IEC", iec), ("NEMA", nema) })
            {
                if (frame.Length == 0) continue;
                lista.Add(new LimiteMotor
                {
                    Id = LimiteMotor.MontarId(serie, cubo, padrao),
                    Serie = serie, Cubo = cubo, Padrao = padrao, Frame = frame,
                });
            }
        }
        return lista;
    }
}
