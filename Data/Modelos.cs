namespace HowdenAxiais.Poc.Data;

/// <summary>
/// Acha os arquivos-modelo (Word e pricing) onde quer que o sistema esteja
/// rodando. No <c>dotnet run</c> eles ficam em <c>wwwroot/modelos</c>; no
/// aplicativo publicado (o VA da rede) o SDK os coloca em
/// <c>wwwroot/_content/HowdenAxiais.Poc/modelos</c>, porque lá este projeto é
/// uma referência do executável desktop. Procurar só no primeiro lugar era o
/// que fazia o VA publicado parar de gerar propostas e pricing.
/// </summary>
public static class Modelos
{
    private const string Prefixo = "wwwroot/";
    private const string PastaDoProjeto = "_content/HowdenAxiais.Poc/";

    /// <summary>O caminho do modelo, ou nada quando ele não existe em lugar nenhum.</summary>
    public static string? Achar(string raiz, string caminhoRelativo)
    {
        var relativo = caminhoRelativo.Replace('\\', '/');
        var semWwwroot = relativo.StartsWith(Prefixo) ? relativo[Prefixo.Length..] : relativo;

        var raizes = new[] { raiz, AppContext.BaseDirectory }.Distinct();
        var candidatos = raizes.SelectMany(r => new[]
        {
            Path.Combine(r, relativo),
            Path.Combine(r, Prefixo + PastaDoProjeto + semWwwroot),
        });

        return candidatos.Select(Path.GetFullPath).FirstOrDefault(File.Exists);
    }
}
