namespace HowdenAxiais.Poc.Data;

/// <summary>
/// As três moedas em que a equipe cadastra preço. É a mesma escolha em toda a
/// tela de dados e na proposta — por isso vive aqui, e não dentro de um
/// componente.
/// </summary>
public enum Moeda { Usd, Clp, Brl }

public static class Moedas
{
    public static readonly Moeda[] Todas = { Moeda.Usd, Moeda.Clp, Moeda.Brl };

    public static string Rotulo(this Moeda m) => m switch
    {
        Moeda.Usd => "USD",
        Moeda.Clp => "CLP",
        _ => "R$",
    };

    /// <summary>Lê o código gravado ("USD", "CLP", "BRL"); o que não bater vira USD.</summary>
    public static Moeda Ler(string texto) => texto.Trim().ToUpperInvariant() switch
    {
        "CLP" => Moeda.Clp,
        "BRL" or "R$" => Moeda.Brl,
        _ => Moeda.Usd,
    };

    /// <summary>Como a moeda é gravada.</summary>
    public static string Codigo(this Moeda m) => m switch
    {
        Moeda.Usd => "USD",
        Moeda.Clp => "CLP",
        _ => "BRL",
    };
}
