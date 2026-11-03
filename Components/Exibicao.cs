using System.Globalization;
using HowdenAxiais.Poc.Data;

namespace HowdenAxiais.Poc.Components;

/// <summary>
/// Como números e nomes APARECEM na tela da proposta — e só isso. Nada daqui
/// é gravado: as chaves das escolhas e dos preços à mão ("lista:{grupo}"), o
/// mapa do pricing e os documentos continuam com o nome e o número como estão
/// no cadastro. PricingExcel e os Word não usam estes helpers.
/// </summary>
public static class Exibicao
{
    public static readonly CultureInfo Br = new("pt-BR");

    /// <summary>
    /// O formato de um valor só de leitura: peso chileno sem centavos (um
    /// "CLP 30.500.000,00" só alonga a linha), as outras moedas com dois.
    /// </summary>
    public static string Formato(Moeda m) => m == Moeda.Clp ? "N0" : "N2";

    /// <summary>
    /// O valor dentro do campo de custo, que é editável: separador de milhar e
    /// até duas casas, em qualquer moeda. Não usa o N0 do CLP de propósito —
    /// um custo de cadastro com centavos apareceria arredondado e, ao sair do
    /// campo, viraria "custo à mão" por causa de centavos.
    /// </summary>
    public static string NoCampo(decimal? valor) => valor?.ToString("#,##0.##", Br) ?? "";

    /// <summary>
    /// O número digitado é o mesmo que o campo mostrava? O campo mostra duas
    /// casas; um cadastro com três (1.234,567) aparece como 1.234,57, e
    /// confirmar esse mesmo texto não pode virar um custo à mão. Arredonda
    /// "meio para cima", como o ToString do campo — o Math.Round padrão (meio
    /// para o par) levaria 1.234,565 a 1.234,56, e o campo mostra 1.234,57.
    /// </summary>
    public static bool MesmoValor(decimal digitado, decimal? puxado) =>
        puxado is { } p
        && Math.Round(digitado, 2, MidpointRounding.AwayFromZero) == Math.Round(p, 2, MidpointRounding.AwayFromZero);

    /// <summary>"1 item", "3 itens": no lugar do "item(ns)".</summary>
    public static string Plural(int n, string singular, string plural) =>
        $"{n.ToString("N0", Br)} {(n == 1 ? singular : plural)}";

    /// <summary>
    /// As palavras que o cadastro tem sem acento (os nomes vieram de planilhas
    /// antigas), e como elas se escrevem.
    /// </summary>
    private static readonly Dictionary<string, string> Acentos = new(StringComparer.OrdinalIgnoreCase)
    {
        ["conexao"] = "conexão",
        ["instrumentacao"] = "instrumentação",
        ["lubrificacao"] = "lubrificação",
        ["eletrica"] = "elétrica",
        ["eletrico"] = "elétrico",
        ["pressao"] = "pressão",
        ["vazao"] = "vazão",
        ["vibracao"] = "vibração",
        ["potencia"] = "potência",
        ["frequencia"] = "frequência",
        ["estagios"] = "estágios",
        ["automatico"] = "automático",
    };

    /// <summary>
    /// O nome de uma lista do cadastro como se lê: "PARTIDORES" vira
    /// "Partidores", "INSTRUMENTAÇÃO" vira "Instrumentação" e "Conexao manga
    /// descarga" vira "Conexão manga descarga". Um nome todo em maiúsculas
    /// passa a minúsculas; um que já tem minúsculas guarda as maiúsculas de
    /// dentro (CV, kW). Só para mostrar — a chave gravada é o nome original.
    /// </summary>
    public static string Exibir(string nome)
    {
        var texto = (nome ?? "").Trim();
        if (texto.Length == 0) return texto;

        if (!texto.Any(char.IsLower)) texto = texto.ToLower(Br);

        var palavras = texto.Split(' ');
        for (var i = 0; i < palavras.Length; i++)
        {
            var palavra = palavras[i];
            if (palavra.Length == 0 || !Acentos.TryGetValue(palavra, out var certa)) continue;
            palavras[i] = char.IsUpper(palavra[0]) ? char.ToUpper(certa[0], Br) + certa[1..] : certa;
        }

        texto = string.Join(' ', palavras);
        return char.ToUpper(texto[0], Br) + texto[1..];
    }
}
