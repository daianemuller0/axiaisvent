namespace HowdenAxiais.Poc.Data;

/// <summary>
/// As moedas do sistema. Três delas (USD, CLP e R$) são as em que a equipe
/// cadastra custo; PEN e EUR existem só como moeda do PREÇO ao cliente, para
/// onde a conversão de moeda da proposta leva o valor. É a mesma escolha em
/// toda a tela de dados e na proposta — por isso vive aqui, e não dentro de um
/// componente.
/// </summary>
public enum Moeda { Usd, Clp, Brl, Pen, Eur }

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

    /// <summary>
    /// As moedas em que o PREÇO ao cliente pode sair: CLP, USD, PEN, R$ e EUR.
    /// Diferente de <see cref="Todas"/>, que são as do CUSTO (o cadastro só
    /// tem USD e CLP), aqui o real entra — o preço pode ser convertido para ele
    /// mesmo com o R$ fora do cadastro.
    /// </summary>
    public static readonly Moeda[] DePreco =
        { Moeda.Clp, Moeda.Usd, Moeda.Pen, Moeda.Brl, Moeda.Eur };

    public static string Rotulo(this Moeda m) => m switch
    {
        Moeda.Usd => "USD",
        Moeda.Clp => "CLP",
        Moeda.Pen => "PEN",
        Moeda.Eur => "EUR",
        _ => "R$",
    };

    /// <summary>Lê o código gravado ("USD", "CLP", "BRL", "PEN", "EUR"); o que não bater vira USD.</summary>
    public static Moeda Ler(string texto) => texto.Trim().ToUpperInvariant() switch
    {
        "CLP" => Moeda.Clp,
        "PEN" => Moeda.Pen,
        "EUR" => Moeda.Eur,
        "BRL" or "R$" => Moeda.Brl,
        _ => Moeda.Usd,
    };

    /// <summary>Como a moeda é gravada.</summary>
    public static string Codigo(this Moeda m) => m switch
    {
        Moeda.Usd => "USD",
        Moeda.Clp => "CLP",
        Moeda.Pen => "PEN",
        Moeda.Eur => "EUR",
        _ => "BRL",
    };
}
