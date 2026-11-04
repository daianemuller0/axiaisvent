namespace HowdenAxiais.Poc.Data;

/// <summary>
/// Os arquivos que a proposta carrega — hoje a curva de performance de cada
/// equipamento.
///
/// Eles ficam em <c>&lt;pasta de dados&gt;/anexos/&lt;proposta&gt;/</c>, e não
/// dentro do banco. Um Parquet é reescrito inteiro a cada gravação: guardar um
/// PDF de meio mega ali dentro faria cada "Salvar" carregar o arquivo de novo,
/// e o banco cresceria sem parar. A proposta guarda só o NOME do arquivo.
/// </summary>
public sealed class Anexos
{
    private readonly ParquetStore _store;
    public Anexos(ParquetStore store) => _store = store;

    private string Pasta(string proposta)
    {
        var pasta = Path.Combine(_store.Folder, "anexos", Seguro(proposta));
        Directory.CreateDirectory(pasta);
        return pasta;
    }

    /// <summary>
    /// Guarda o arquivo com um nome novo e devolve esse nome.
    ///
    /// O nome gravado é sorteado, e não o que o usuário mandou: dois
    /// equipamentos podem subir "curva.pdf", e o segundo não pode apagar o
    /// primeiro. O nome original fica na proposta, para a tela mostrar.
    /// </summary>
    public async Task<string> Guardar(string proposta, Stream conteudo, string nomeOriginal)
    {
        var extensao = Path.GetExtension(nomeOriginal);
        var nome = Guid.NewGuid().ToString("n") + extensao;

        await using var destino = File.Create(Path.Combine(Pasta(proposta), nome));
        await conteudo.CopyToAsync(destino);

        return nome;
    }

    public string? Caminho(string proposta, string arquivo)
    {
        if (arquivo.Trim().Length == 0) return null;

        var caminho = Path.Combine(Pasta(proposta), Seguro(arquivo));
        return File.Exists(caminho) ? caminho : null;
    }

    public long Tamanho(string proposta, string arquivo) =>
        Caminho(proposta, arquivo) is { } c ? new FileInfo(c).Length : 0;

    public void Apagar(string proposta, string arquivo)
    {
        if (Caminho(proposta, arquivo) is { } caminho) File.Delete(caminho);
    }

    /// <summary>
    /// Copia os anexos de uma proposta para outra. A revisão nova nasce com
    /// outro id, e as curvas que ela herdou moram na pasta da revisão
    /// anterior: sem a cópia, abririam quebradas e ficariam fora da técnica.
    /// </summary>
    public void CopiarTodos(string de, string para)
    {
        if (de.Trim().Length == 0 || para.Trim().Length == 0 || de == para) return;

        var origem = Path.Combine(_store.Folder, "anexos", Seguro(de));
        if (!Directory.Exists(origem)) return;

        var destino = Pasta(para);
        foreach (var arquivo in Directory.GetFiles(origem))
        {
            var alvo = Path.Combine(destino, Path.GetFileName(arquivo));
            if (!File.Exists(alvo)) File.Copy(arquivo, alvo);
        }
    }

    /// <summary>
    /// Só o nome do arquivo, sem pasta: o nome vem de fora, e ".." ou uma
    /// barra no meio dele sairiam da pasta da proposta.
    /// </summary>
    private static string Seguro(string nome) => Path.GetFileName(nome.Trim());
}
