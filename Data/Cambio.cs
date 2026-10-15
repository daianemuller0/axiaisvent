namespace HowdenAxiais.Poc.Data;

/// <summary>
/// A conversão de moeda da proposta: o custo pode estar numa moeda e o preço
/// para o cliente em outra. É a mesma regra do sistema de Serviços:
///
/// <list type="bullet">
/// <item>o custo e todo o pricing são calculados na <b>moeda do custo</b>
/// (<see cref="Proposta.Moeda"/>); só o valor final, o que o cliente recebe,
/// é convertido para a <b>moeda do preço</b> (<see cref="Proposta.MoedaVenda"/>);</item>
/// <item>a taxa é digitada num de dois sentidos — "1 moeda do PREÇO em moeda do
/// CUSTO" (<c>VendaEmCusto</c>, o padrão) ou o contrário (<c>CustoEmVenda</c>) —
/// e é guardada, internamente, sempre no primeiro;</item>
/// <item>a <b>segurança da moeda</b> é um percentual tirado da taxa: com 1 USD =
/// 950 CLP e 5% de segurança a conversão usa 902,5 — o preço em USD sobe, e se
/// o dólar cair até 5% o valor em pesos não perde;</item>
/// <item>valor no preço = valor no custo ÷ taxa efetiva, com duas casas.</item>
/// </list>
///
/// Sem taxa informada a conversão não acontece (a tela avisa), e custo e preço
/// na mesma moeda nunca convertem.
/// </summary>
public static class Cambio
{
    /// <summary>O custo e o preço estão em moedas diferentes?</summary>
    public static bool Converte(Proposta p) => p.MoedaVenda != p.Moeda;

    /// <summary>
    /// A taxa na direção interna: 1 moeda do PREÇO = X da moeda do CUSTO,
    /// qualquer que tenha sido o sentido digitado. Zero quando não há taxa.
    /// </summary>
    public static decimal Normalizada(Proposta p)
    {
        var taxa = DadosExcel.Numero(p.TaxaCambio);
        if (taxa is null or <= 0m) return 0m;

        return p.CambioDirecao == "CustoEmVenda" ? 1m / taxa.Value : taxa.Value;
    }

    /// <summary>A segurança da moeda, em percentual (0 a 99).</summary>
    public static decimal Seguranca(Proposta p) =>
        Math.Clamp(DadosExcel.Numero(p.SegurancaCambioPct) ?? 0m, 0m, 99m);

    /// <summary>Falta digitar a taxa: há conversão a fazer, mas não há com quê.</summary>
    public static bool FaltaTaxa(Proposta p) => Converte(p) && Normalizada(p) <= 0m;

    /// <summary>
    /// A taxa que converte de verdade: a informada menos a segurança. 1 quando
    /// não há conversão — e também quando falta a taxa, para o valor sair como
    /// está em vez de dividido por zero.
    /// </summary>
    public static decimal Efetivo(Proposta p)
    {
        if (!Converte(p)) return 1m;

        var normalizada = Normalizada(p);
        if (normalizada <= 0m) return 1m;

        return normalizada * Math.Max(1m - Seguranca(p) / 100m, 0.01m);
    }

    /// <summary>Leva um valor da moeda do custo para a moeda do preço.</summary>
    public static decimal ParaVenda(Proposta p, decimal valorNoCusto)
    {
        var fx = Efetivo(p);
        return fx == 1m ? valorNoCusto : Math.Round(valorNoCusto / fx, 2);
    }
}
