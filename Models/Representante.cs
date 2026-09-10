namespace HowdenAxiais.Poc.Models;

/// <summary>
/// Representante comercial (BD_pricing A67:E99 da planilha): nome, região,
/// percentual de comissão e o texto de contato impresso na proposta.
/// </summary>
public class Representante
{
    public string Id { get; set; } = "";
    public string Nome { get; set; } = "";
    public string Local { get; set; } = "";          // Brasil / Exterior
    public string ComissaoPct { get; set; } = "0";   // em %, ex.: "3"
    public string Contato { get; set; } = "";        // "Fulano por (11) 1234 ou e-mail: x@y"
}
