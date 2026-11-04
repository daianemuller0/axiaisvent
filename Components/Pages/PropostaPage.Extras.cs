using System.Globalization;
using HowdenAxiais.Poc.Data;

namespace HowdenAxiais.Poc.Components.Pages;

/// <summary>
/// O que a proposta faz fora das abas do equipamento (itens 19, 20, 21 e 22 do
/// plano). Hoje: a dica só de leitura do último custo digitado à mão (item 22).
/// </summary>
public partial class PropostaPage
{
    // =====================================================================
    // item 22: "último à mão" sob o campo de custo
    // =====================================================================

    private List<Proposta>? _gravadasDosCustos;
    private Dictionary<string, List<CustoAMao>> _custosAMao = new();

    /// <summary>
    /// Os custos à mão das propostas gravadas, pela chave da dica (linha,
    /// opção, diâmetro e moeda), do mais recente para o mais antigo. Montado
    /// uma vez por leitura das gravadas: o Preco(...) pede a cada desenho, em
    /// cada linha sem custo.
    /// </summary>
    private Dictionary<string, List<CustoAMao>> CustosAMaoGravados()
    {
        if (ReferenceEquals(_gravadasDosCustos, _gravadas)) return _custosAMao;

        _gravadasDosCustos = _gravadas;
        _custosAMao = CustosAMao.De(_gravadas, _custo)
            .Where(c => c.Identidade.Length > 0 && c.Diametro.Length > 0)
            .GroupBy(c => ChaveDaDica(c.Chave, c.Identidade, c.Diametro, c.Moeda))
            .ToDictionary(g => g.Key, g => g.ToList());
        return _custosAMao;
    }

    /// <summary>
    /// A mesma linha, a mesma opção, o mesmo diâmetro (pelo número: "2400" e
    /// "2.400" são o mesmo) e a mesma moeda. Outra moeda nunca aparece: um
    /// custo em USD não diz nada a uma proposta em CLP sem a taxa da época.
    /// </summary>
    private static string ChaveDaDica(string chave, string identidade, string diametro, string moeda) =>
        string.Join('\u001f', chave, identidade,
            Medida.Numero(diametro).ToString(CultureInfo.InvariantCulture), moeda.Trim().ToUpperInvariant());

    partial void DicaDoCusto(ItemProposta item, string chave, decimal? puxado, ref string? dica)
    {
        // só onde falta custo: com o do cadastro ou um à mão, a dica seria barulho
        if (puxado is not null || EhManual(item, chave)) return;
        if (ModeloDe(item) is not { } modelo || CustosAMao.Identidade(item, chave, _custo) is not { } identidade) return;

        var moeda = _proposta.MoedaCodigo;
        if (!CustosAMaoGravados().TryGetValue(ChaveDaDica(chave, identidade, modelo.Diametro, moeda), out var custos)) return;

        // a proposta aberta não conta: o "último" é de outra proposta (uma
        // revisão anterior desta é outra linha gravada, e conta)
        var ultimo = custos.FirstOrDefault(c => c.Proposta.Id != _proposta.Id);
        if (ultimo is null) return;

        var m = Moedas.Ler(ultimo.Moeda);
        var revisao = ultimo.Proposta.Revisao.Trim() is { Length: > 0 } r && r != "00" ? $" rev {r}" : "";
        // espaços inseparáveis antes de cada "·" e dentro do valor, e hífens
        // inseparáveis no número: na coluna estreita do custo a linha quebra
        // depois do ponto, e nunca entre a moeda e o valor nem no meio do "PRP-2026-104"
        const string nbsp = "\u00A0";
        var numero = ultimo.Proposta.Numero.Replace('-', '\u2011');
        dica = $"último à mão: {m.Rotulo()}{nbsp}{ultimo.Valor.ToString(Formato(m), Br)}{nbsp}· {numero}{revisao}"
               + (ultimo.Data is { } d ? $"{nbsp}· {d.ToString("dd/MM/yyyy", Br)}" : "");
    }
}
