using System.Collections.Concurrent;
using System.Data;
using DuckDB.NET.Data;

namespace HowdenAxiais.Poc.Data;

/// <summary>
/// Camada de dados no padrão recomendado pela equipe de melhoria (Edson):
/// DuckDB como MOTOR sobre arquivos Parquet numa pasta de rede.
///
/// Cada gravação (adição/edição) cria um pequeno arquivo .parquet novo dentro
/// da subpasta da entidade (ex.: companies/). Nada de "um único banco na rede"
/// (que com SQLite trava e fica lento). Na leitura, o DuckDB lê a pasta inteira
/// e CONSOLIDA: para cada id, mantém a versão mais recente (_ts) e ignora os
/// registros marcados como apagados (_deleted).
///
/// Como cada operação abre um DuckDB em memória (Data Source=:memory:) e só
/// toca os Parquet, não há arquivo de banco compartilhado sendo travado —
/// vários usuários podem gravar ao mesmo tempo, cada um no seu arquivinho.
/// </summary>
public sealed class ParquetStore
{
    public string Folder { get; }

    public ParquetStore(string folder)
    {
        Folder = Path.GetFullPath(folder);
        try
        {
            Directory.CreateDirectory(Folder);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Não foi possível acessar a pasta de dados '{Folder}'. " +
                "Verifique a chave \"Data:Folder\" no appsettings e se o caminho de rede " +
                "está acessível a partir desta máquina.", ex);
        }
    }

    private string EntityDir(string entity)
    {
        var dir = Path.Combine(Folder, entity);
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>
    /// Último carimbo entregue por <see cref="ProximoTs"/>.
    /// </summary>
    private static long _ultimoTs;

    /// <summary>Gravações desde a última compactação, por entidade.</summary>
    private static readonly ConcurrentDictionary<string, int> _desdeCompactacao = new();

    /// <summary>
    /// A partir de quantos arquivos vale a pena juntar tudo num só. Abaixo
    /// disso a leitura é rápida e compactar só daria trabalho.
    /// </summary>
    private const int LimiteDeArquivos = 200;

    /// <summary>
    /// O carimbo (_ts) de uma gravação, <b>estritamente crescente</b>.
    ///
    /// Não dá para usar <c>DateTime.UtcNow.Ticks</c> direto: no Windows o
    /// relógio do sistema só avança a cada ~15 ms, então duas gravações
    /// seguidas recebem o MESMO carimbo. E aí a consolidação da leitura
    /// (row_number() por _ts) empata, o desempate é arbitrário, e a versão
    /// velha do registro pode ganhar — na prática o dado recém-digitado some,
    /// volta e some de novo a cada leitura. (No Linux o relógio tem resolução
    /// de nanossegundos e o problema não aparece, que é por que ele passou
    /// despercebido aqui.)
    ///
    /// Aqui o relógio é só o piso: se ele não andou, o carimbo anda sozinho,
    /// +1 por gravação. Assim a ordem das gravações deste processo é sempre
    /// respeitada, por mais rápido que a pessoa digite.
    /// </summary>
    private static long ProximoTs()
    {
        while (true)
        {
            var anterior = Interlocked.Read(ref _ultimoTs);
            var agora = DateTime.UtcNow.Ticks;
            var proximo = agora > anterior ? agora : anterior + 1;
            if (Interlocked.CompareExchange(ref _ultimoTs, proximo, anterior) == anterior)
                return proximo;
        }
    }

    private static DuckDBConnection Open()
    {
        var conn = new DuckDBConnection("Data Source=:memory:");
        conn.Open();
        return conn;
    }

    private static void AddParam(IDbCommand cmd, object? value)
    {
        var p = cmd.CreateParameter();
        p.Value = value ?? DBNull.Value;
        cmd.Parameters.Add(p);
    }

    // Caminho no formato que o DuckDB entende (barras normais, mesmo no Windows).
    private static string Duck(string path) => path.Replace('\\', '/');

    /// <summary>Grava uma linha como um novo arquivo Parquet na subpasta da entidade.</summary>
    public void WriteRow(string entity, IReadOnlyList<KeyValuePair<string, object?>> row, bool deleted = false)
    {
        using var conn = Open();

        var colDefs = string.Join(", ", row.Select(kv => $"\"{kv.Key}\" VARCHAR")) + ", _ts BIGINT, _deleted BOOLEAN";
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = $"CREATE TABLE t ({colDefs});";
            cmd.ExecuteNonQuery();
        }

        var ts = ProximoTs();

        var placeholders = string.Join(", ", row.Select(_ => "?")) + ", ?, ?";
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = $"INSERT INTO t VALUES ({placeholders});";
            foreach (var kv in row) AddParam(cmd, kv.Value);
            AddParam(cmd, ts);
            AddParam(cmd, deleted);
            cmd.ExecuteNonQuery();
        }

        var dir = EntityDir(entity);
        // o nome do arquivo leva o MESMO carimbo: é o desempate da leitura
        var fileName = $"{ts:D19}_{Guid.NewGuid():N}.parquet";
        var full = Duck(Path.Combine(dir, fileName));
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = $"COPY t TO '{full}' (FORMAT PARQUET);";
            cmd.ExecuteNonQuery();
        }

        // Sem isto a pasta cresce para sempre e cada leitura fica mais lenta —
        // é o que fazia a tela travar depois de uma tarde de digitação.
        if (_desdeCompactacao.AddOrUpdate(entity, 1, (_, n) => n + 1) >= LimiteDeArquivos)
        {
            _desdeCompactacao[entity] = 0;
            Compactar(entity);
        }
    }

    /// <summary>
    /// Junta os arquivos de uma entidade num só, mantendo exatamente o que a
    /// leitura enxerga: a versão mais recente de cada id, marcas de apagado
    /// incluídas (elas ainda precisam vencer arquivos antigos de outra máquina).
    ///
    /// Cada gravação cria um arquivo novo — é o que permite vários usuários
    /// escreverem ao mesmo tempo sem travar nada. O preço é que a pasta só
    /// cresce, e a leitura, que abre todos, fica linearmente mais lenta: medido
    /// em disco local, ~28 ms com 98 arquivos e ~104 ms com 686. Numa pasta de
    /// rede cada arquivo custa muito mais, e a tela começa a engasgar.
    ///
    /// Só apaga os arquivos que listou ANTES de ler: se outra pessoa gravar no
    /// meio da compactação, o arquivo dela não estava na lista e sobrevive.
    /// </summary>
    public void Compactar(string entity)
    {
        var dir = EntityDir(entity);

        string[] antigos;
        try
        {
            antigos = Directory.GetFiles(dir, "*.parquet");
        }
        catch (IOException)
        {
            return;
        }

        if (antigos.Length < 2) return;

        // arquivo temporário fora da pasta da entidade: se algo falhar no meio,
        // um arquivo pela metade não entra no caminho da leitura
        var temporario = Path.Combine(Folder, $".compactar-{Guid.NewGuid():N}.tmp");
        var destino = Path.Combine(dir, $"{ProximoTs():D19}_compacto.parquet");

        try
        {
            var lista = string.Join(", ", antigos.Select(a => $"'{Duck(a)}'"));

            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = $@"
COPY (
    SELECT * EXCLUDE (_rn, filename)
    FROM (
        SELECT *, row_number() OVER (
            PARTITION BY id ORDER BY _ts DESC, filename DESC
        ) AS _rn
        FROM read_parquet([{lista}], union_by_name=true, filename=true)
    )
    WHERE _rn = 1
) TO '{Duck(temporario)}' (FORMAT PARQUET);";
                cmd.ExecuteNonQuery();
            }

            File.Move(temporario, destino);
        }
        catch (Exception)
        {
            // compactar é otimização: se não deu, a pasta segue como estava
            try { if (File.Exists(temporario)) File.Delete(temporario); } catch { /* ignora */ }
            return;
        }

        foreach (var antigo in antigos)
        {
            try { File.Delete(antigo); } catch { /* outro processo pode estar lendo */ }
        }
    }

    /// <summary>Compacta todas as entidades que já passaram do limite de arquivos.</summary>
    public void CompactarSePreciso()
    {
        foreach (var dir in Directory.EnumerateDirectories(Folder))
        {
            try
            {
                if (Directory.GetFiles(dir, "*.parquet").Length >= LimiteDeArquivos)
                    Compactar(Path.GetFileName(dir));
            }
            catch (IOException)
            {
                // pasta de rede indisponível no momento: fica para a próxima
            }
        }
    }

    /// <summary>
    /// Lê a entidade já consolidada: versão mais recente por id, sem os apagados.
    /// Retorna vazio se ainda não houver nenhum arquivo (primeira execução).
    /// </summary>
    public List<T> ReadLatest<T>(string entity, string selectCols, Func<IDataReader, T> map, string orderBy = "")
    {
        var dir = EntityDir(entity);
        if (!Directory.EnumerateFiles(dir, "*.parquet").Any())
            return new List<T>();

        var glob = Duck(Path.Combine(dir, "*.parquet"));
        using var conn = Open();

        // Evolução de esquema: arquivos antigos podem não ter colunas novas.
        // union_by_name junta esquemas diferentes entre arquivos, e as colunas
        // pedidas que não existirem em NENHUM arquivo viram NULL no SELECT.
        var presentes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var describe = conn.CreateCommand())
        {
            describe.CommandText = $"DESCRIBE SELECT * FROM read_parquet('{glob}', union_by_name=true);";
            using var dr = describe.ExecuteReader();
            while (dr.Read()) presentes.Add(dr.GetString(0));
        }

        var cols = string.Join(", ", selectCols.Split(',')
            .Select(c => c.Trim())
            .Select(c => presentes.Contains(c) ? c : $"NULL AS {c}"));

        var order = string.IsNullOrWhiteSpace(orderBy) ? "" : $" ORDER BY {orderBy}";
        // O desempate por nome de arquivo é o cinto de segurança: mesmo que dois
        // carimbos coincidam (duas MÁQUINAS gravando no mesmo milissegundo), a
        // leitura devolve sempre a mesma resposta em vez de oscilar. E como o
        // nome começa pelo carimbo, a ordem continua sendo a das gravações.
        var sql = $@"
SELECT {cols}
FROM (
    SELECT *, row_number() OVER (
        PARTITION BY id ORDER BY _ts DESC, filename DESC
    ) AS _rn
    FROM read_parquet('{glob}', union_by_name=true, filename=true)
)
WHERE _rn = 1 AND NOT _deleted{order};";

        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        using var r = cmd.ExecuteReader();
        var list = new List<T>();
        while (r.Read()) list.Add(map(r));
        return list;
    }

    public bool IsEmpty(string entity)
    {
        var dir = EntityDir(entity);
        return !Directory.EnumerateFiles(dir, "*.parquet").Any();
    }

    /// <summary>
    /// Apaga todos os Parquet da entidade (usado pela importação em modo "substituir").
    /// </summary>
    public void Clear(string entity)
    {
        var dir = EntityDir(entity);
        foreach (var f in Directory.EnumerateFiles(dir, "*.parquet"))
            File.Delete(f);
    }
}
