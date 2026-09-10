namespace HowdenAxiais.Poc.Models;

/// <summary>
/// Vendedor / quem assina a proposta (tabela de contatos do modelo Word):
/// nome, cargo, área de atuação, telefones e e-mail.
/// </summary>
public class Vendedor
{
    public string Id { get; set; } = "";
    public string Nome { get; set; } = "";
    public string Cargo { get; set; } = "";
    public string Area { get; set; } = "";       // ex.: Industrial, Tunnel / Metro
    public string Fones { get; set; } = "";
    public string Email { get; set; } = "";
}
