namespace HowdenAxiais.Poc.Data;

/// <summary>
/// Identidade visual, guardada como data URI na entidade "branding": o logo da
/// página inicial (tela de login) e o logo do sistema (barra lateral).
///
/// São dois logos separados porque ficam em fundos diferentes — a barra lateral
/// é escura e a página inicial tem o seu próprio desenho — e a equipe pode
/// querer uma versão de cada. Sem logo enviado, cada lugar mostra o wordmark
/// padrão da Howden.
/// </summary>
public sealed class BrandingRepository
{
    private const string Entidade = "branding";
    private const string LogoPaginaInicialId = "logoPaginaInicial";
    private const string LogoSistemaId = "logoSistema";

    /// <summary>
    /// Avisa quem está com a tela aberta (a barra lateral, por exemplo) que um
    /// logo mudou. É estático de propósito: a troca vale para todo mundo, e a
    /// barra lateral das outras pessoas também se atualiza.
    /// </summary>
    public static event Action? Mudou;

    private readonly ParquetStore _store;
    public BrandingRepository(ParquetStore store) => _store = store;

    private string? Get(string id) => _store
        .ReadLatest(Entidade, "id, valor", r => (
            Id: r.IsDBNull(0) ? "" : r.GetString(0),
            Valor: r.IsDBNull(1) ? "" : r.GetString(1)))
        .Where(x => x.Id == id)
        .Select(x => string.IsNullOrWhiteSpace(x.Valor) ? null : x.Valor)
        .FirstOrDefault();

    private void Set(string id, string valor, bool apagar = false)
    {
        _store.WriteRow(Entidade,
            new KeyValuePair<string, object?>[] { new("id", id), new("valor", valor) },
            deleted: apagar);
        Mudou?.Invoke();
    }

    public string? GetLogoPaginaInicial() => Get(LogoPaginaInicialId);
    public void SaveLogoPaginaInicial(string dataUri) => Set(LogoPaginaInicialId, dataUri);
    public void ClearLogoPaginaInicial() => Set(LogoPaginaInicialId, "", apagar: true);

    public string? GetLogoSistema() => Get(LogoSistemaId);
    public void SaveLogoSistema(string dataUri) => Set(LogoSistemaId, dataUri);
    public void ClearLogoSistema() => Set(LogoSistemaId, "", apagar: true);
}
