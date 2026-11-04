using System.Security.Cryptography;
using System.Text;

namespace HowdenAxiais.Poc.Data;

/// <summary>
/// Os papéis de acesso. O papel decide o que cada pessoa vê no menu e o que
/// pode fazer:
///
/// <list type="bullet">
/// <item><b>ADM</b>: tudo — inclusive Configurações (os usuários), Identidade
/// Visual e a exclusão de propostas;</item>
/// <item><b>Cadastro</b>: Proposta, Base de Dados e Dados (os custos, motores e
/// características);</item>
/// <item><b>Comercial</b>: Proposta e Base de Dados.</item>
/// </list>
/// </summary>
public static class Papeis
{
    public const string Adm = "adm";
    public const string Cadastro = "cadastro";
    public const string Comercial = "comercial";

    public static readonly (string Chave, string Rotulo, string Descricao)[] Todos =
    {
        (Adm, "ADM", "tudo: Configurações, Identidade Visual, Dados e excluir propostas"),
        (Cadastro, "Cadastro", "Proposta, Base de Dados e Dados (custos, motores e características)"),
        (Comercial, "Comercial", "Proposta e Base de Dados"),
    };

    /// <summary>O nome do papel como aparece na tela; o que não for conhecido sai como veio.</summary>
    public static string Rotulo(string chave) =>
        Todos.FirstOrDefault(p => p.Chave == chave).Rotulo ?? chave;

    public static bool Conhecido(string chave) => Todos.Any(p => p.Chave == chave);
}

/// <summary>Uma pessoa com acesso ao sistema.</summary>
public sealed class Usuario
{
    public string Id { get; set; } = "";

    /// <summary>O que a pessoa digita para entrar. Único, sem diferenciar maiúsculas.</summary>
    public string Login { get; set; } = "";

    public string Nome { get; set; } = "";
    public string Papel { get; set; } = Papeis.Comercial;

    /// <summary>A senha nunca é guardada: só o resumo dela e o sal que o gerou.</summary>
    public string SenhaHash { get; set; } = "";
    public string SenhaSal { get; set; } = "";

    /// <summary>Usuário inativo não entra, mas continua cadastrado.</summary>
    public bool Ativo { get; set; } = true;

    public string CriadoEm { get; set; } = "";
}

/// <summary>
/// Os usuários do sistema (entidade "usuarios"), com senha em PBKDF2.
///
/// Fora desta lista continua valendo o acesso configurado em
/// <c>appsettings.json</c> (Auth:Usuario / Auth:Senha), que entra como ADM — é o
/// que permite abrir o sistema pela primeira vez e criar os demais, e o que
/// nunca deixa ninguém trancado do lado de fora.
/// </summary>
public sealed class UsuarioRepository
{
    private const string Entidade = "usuarios";
    private const int Iteracoes = 100_000;

    private readonly ParquetStore _store;
    public UsuarioRepository(ParquetStore store) => _store = store;

    public List<Usuario> Todos() => _store
        .ReadLatest(Entidade, "id, login, nome, papel, senhaHash, senhaSal, ativo, criadoEm",
            r => new Usuario
            {
                Id = S(r, 0), Login = S(r, 1), Nome = S(r, 2), Papel = S(r, 3),
                SenhaHash = S(r, 4), SenhaSal = S(r, 5), Ativo = S(r, 6) != "0",
                CriadoEm = S(r, 7),
            })
        .OrderBy(u => u.Nome, StringComparer.CurrentCultureIgnoreCase)
        .ToList();

    public Usuario? AcharPorLogin(string login) => Todos()
        .FirstOrDefault(u => u.Login.Equals(login.Trim(), StringComparison.OrdinalIgnoreCase));

    public void Salvar(Usuario u)
    {
        if (string.IsNullOrWhiteSpace(u.Id)) u.Id = Guid.NewGuid().ToString("n");
        if (string.IsNullOrWhiteSpace(u.CriadoEm)) u.CriadoEm = DateTime.Now.ToString("yyyy-MM-dd HH:mm");

        _store.WriteRow(Entidade, new KeyValuePair<string, object?>[]
        {
            new("id", u.Id), new("login", u.Login), new("nome", u.Nome), new("papel", u.Papel),
            new("senhaHash", u.SenhaHash), new("senhaSal", u.SenhaSal),
            new("ativo", u.Ativo ? "1" : "0"), new("criadoEm", u.CriadoEm),
        });
    }

    /// <summary>
    /// Cria um usuário. Devolve o motivo quando não dá — login repetido, senha
    /// curta, papel desconhecido —, ou <c>null</c> quando criou.
    /// </summary>
    public string? Criar(string login, string nome, string senha, string papel)
    {
        login = login.Trim();
        nome = nome.Trim();

        if (login.Length < 3 || login.Any(char.IsWhiteSpace))
            return "O usuário precisa ter pelo menos 3 caracteres, sem espaços.";
        if (nome.Length == 0) return "Informe o nome da pessoa.";
        if (senha.Length < 6) return "A senha precisa ter pelo menos 6 caracteres.";
        if (!Papeis.Conhecido(papel)) return "Escolha o papel de acesso.";
        if (AcharPorLogin(login) is not null) return $"Já existe um usuário \"{login}\".";

        var (hash, sal) = Resumir(senha);
        Salvar(new Usuario
        {
            Login = login, Nome = nome, Papel = papel, SenhaHash = hash, SenhaSal = sal,
        });
        return null;
    }

    /// <summary>Troca a senha. Devolve o motivo quando não dá, ou <c>null</c>.</summary>
    public string? DefinirSenha(Usuario u, string senha)
    {
        if (senha.Length < 6) return "A senha precisa ter pelo menos 6 caracteres.";

        (u.SenhaHash, u.SenhaSal) = Resumir(senha);
        Salvar(u);
        return null;
    }

    /// <summary>O usuário que bate com o login e a senha, ou nada. Só entra quem está ativo.</summary>
    public Usuario? Autenticar(string login, string senha)
    {
        var u = AcharPorLogin(login);
        if (u is null || !u.Ativo || u.SenhaHash.Length == 0) return null;

        var esperado = Convert.FromBase64String(u.SenhaHash);
        var calculado = Calcular(senha, Convert.FromBase64String(u.SenhaSal));

        return CryptographicOperations.FixedTimeEquals(esperado, calculado) ? u : null;
    }

    private static (string Hash, string Sal) Resumir(string senha)
    {
        var sal = RandomNumberGenerator.GetBytes(16);
        return (Convert.ToBase64String(Calcular(senha, sal)), Convert.ToBase64String(sal));
    }

    private static byte[] Calcular(string senha, byte[] sal) =>
        Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(senha), sal, Iteracoes,
            HashAlgorithmName.SHA256, 32);

    private static string S(System.Data.IDataReader r, int i) => r.IsDBNull(i) ? "" : r.GetString(i);
}
