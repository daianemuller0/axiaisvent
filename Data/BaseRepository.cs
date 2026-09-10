using System.Text.Json;

namespace HowdenAxiais.Poc.Data;

/// <summary>Uma linha da base: o id de controle, a ordem e os valores das colunas.</summary>
public sealed class LinhaBase
{
    public string Id { get; set; } = "";
    public int Ordem { get; set; }
    public List<string> Valores { get; set; } = new();

    public string Valor(int col) => col >= 0 && col < Valores.Count ? Valores[col] : "";

    public void Definir(int col, string valor)
    {
        while (Valores.Count <= col) Valores.Add("");
        Valores[col] = valor;
    }
}

/// <summary>
/// A base de dados da planilha, sobre o mesmo ParquetStore do resto do sistema.
///
/// As colunas são as da planilha que o usuário subir — não dá para fixá-las no
/// código. Então a base guarda duas coisas:
///  - o CABEÇALHO (nomes das colunas, em ordem) na entidade "base_colunas";
///  - as LINHAS na entidade "base", com as colunas nomeadas c0, c1, c2… — nomes
///    técnicos de propósito, para que qualquer título de planilha (acento,
///    espaço, aspas, repetido) funcione sem quebrar o Parquet/DuckDB.
/// </summary>
public sealed class BaseRepository
{
    private const string EntidadeLinhas = "base";
    private const string EntidadeColunas = "base_colunas";
    private const string IdColunas = "colunas";

    private readonly ParquetStore _store;
    public BaseRepository(ParquetStore store) => _store = store;

    // ---------------- cabeçalho ----------------

    /// <summary>Nomes das colunas, na ordem da planilha. Vazio = ainda não subiram nada.</summary>
    public List<string> Colunas()
    {
        var json = _store.ReadLatest(EntidadeColunas, "id, valor",
                r => new { Id = S(r, 0), Valor = S(r, 1) })
            .Where(x => x.Id == IdColunas)
            .Select(x => x.Valor)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(json)) return new List<string>();
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch (JsonException)
        {
            return new List<string>();
        }
    }

    public void SalvarColunas(IEnumerable<string> colunas) => _store.WriteRow(EntidadeColunas,
        new KeyValuePair<string, object?>[]
        {
            new("id", IdColunas),
            new("valor", JsonSerializer.Serialize(colunas.ToList())),
        });

    // ---------------- linhas ----------------

    public List<LinhaBase> Linhas()
    {
        var colunas = Colunas();
        if (colunas.Count == 0) return new List<LinhaBase>();

        var cols = "id, ordem" + string.Concat(Enumerable.Range(0, colunas.Count).Select(i => $", c{i}"));

        return _store.ReadLatest(EntidadeLinhas, cols, r =>
        {
            var linha = new LinhaBase { Id = S(r, 0), Ordem = Int(S(r, 1)) };
            for (var i = 0; i < colunas.Count; i++) linha.Valores.Add(S(r, i + 2));
            return linha;
        }, "ordem");
    }

    public void Salvar(LinhaBase linha, int totalColunas)
    {
        if (string.IsNullOrWhiteSpace(linha.Id)) linha.Id = Guid.NewGuid().ToString("N");

        var campos = new List<KeyValuePair<string, object?>>
        {
            new("id", linha.Id),
            // ordem gravada com zeros à esquerda: o Parquet guarda texto e a
            // leitura ordena por texto — sem isso a linha 10 viria antes da 2.
            new("ordem", linha.Ordem.ToString("D6")),
        };
        for (var i = 0; i < totalColunas; i++) campos.Add(new($"c{i}", linha.Valor(i)));

        _store.WriteRow(EntidadeLinhas, campos);
    }

    public void Apagar(string id) => _store.WriteRow(EntidadeLinhas,
        new KeyValuePair<string, object?>[] { new("id", id) }, deleted: true);

    /// <summary>Grava a base inteira de uma vez (o que a importação faz).</summary>
    public void SubstituirTudo(List<string> colunas, List<LinhaBase> linhas)
    {
        _store.Clear(EntidadeLinhas);
        SalvarColunas(colunas);

        var ordem = 0;
        foreach (var linha in linhas)
        {
            linha.Id = Guid.NewGuid().ToString("N");
            linha.Ordem = ordem++;
            Salvar(linha, colunas.Count);
        }
    }

    /// <summary>Acrescenta linhas ao fim da base atual (importação em modo "acrescentar").</summary>
    public void Acrescentar(List<LinhaBase> linhas)
    {
        var colunas = Colunas();
        var atuais = Linhas();
        var ordem = atuais.Count == 0 ? 0 : atuais.Max(l => l.Ordem) + 1;
        foreach (var linha in linhas)
        {
            linha.Id = Guid.NewGuid().ToString("N");
            linha.Ordem = ordem++;
            Salvar(linha, colunas.Count);
        }
    }

    public void LimparTudo()
    {
        _store.Clear(EntidadeLinhas);
        _store.Clear(EntidadeColunas);
    }

    private static string S(System.Data.IDataReader r, int i) => r.IsDBNull(i) ? "" : r.GetString(i);

    private static int Int(string s) => int.TryParse(s, out var v) ? v : 0;
}
