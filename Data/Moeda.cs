namespace HowdenAxiais.Poc.Data;

/// <summary>
/// As três moedas em que a equipe cadastra preço. É a mesma escolha em toda a
/// tela de dados e na proposta — por isso vive aqui, e não dentro de um
/// componente.
/// </summary>
public enum Moeda { Usd, Clp, Brl }

public static class Moedas
{
    /// <summary>
    /// O real está fora de uso por enquanto: a equipe trabalha o custo em USD e
    /// em CLP. Ligar de volta é trocar este valor — os dados em R$ continuam
    /// gravados e as telas, planilhas e a conversão voltam a mostrar a coluna.
    /// </summary>
    public const bool UsaReal = false;

    public static readonly Moeda[] Todas = UsaReal
        ? new[] { Moeda.Usd, Moeda.Clp, Moeda.Brl }
        : new[] { Moeda.Usd, Moeda.Clp };

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
