using System.Text.RegularExpressions;

namespace HowdenAxiais.Poc.Data;

/// <summary>
/// Um dado técnico lido da seleção: o número como o programa escreveu e a
/// unidade que estava ao lado dele.
///
/// O valor fica como TEXTO, e não como número, de propósito. O relatório sai
/// em pt-BR num dia (",066", "3.560") e pode sair em inglês no outro ("0.066",
/// "3,560"), e as duas leituras são incompatíveis: ",066" e "0.066" são o
/// mesmo 0,066, mas "3.560" é três mil e quinhentos e sessenta de um lado e
/// três vírgula cinco do outro. Guardar o texto não perde nada e não inventa
/// nada; converter fica para quando a proposta técnica disser em que unidade
/// ela quer cada coisa.
/// </summary>
public sealed record DadoTecnico(string Valor, string Unidade)
{
    public string Inteiro => Unidade.Trim().Length > 0 ? $"{Valor} {Unidade}".Trim() : Valor.Trim();
    public bool Vazio => Valor.Trim().Length == 0;
}

/// <summary>
/// Lê o relatório de seleção do ventilador — o arquivo que o programa de
/// seleção exporta (.txt no Joy).
///
/// O relatório é texto corrido com frases fixas ("The Specified Duty is 15000
/// CFM, 7,00 in W.G. …"), então cada dado é achado pela frase que o carrega.
/// A UNIDADE é lida junto, e não suposta: o mesmo programa exporta densidade
/// em Lb/Ft3 numa seleção e em Kg/m3 noutra.
///
/// Nada aqui é convertido nem arredondado. O que a tela mostra é o que estava
/// escrito no arquivo, para a equipe conferir contra a seleção antes de a
/// proposta sair.
/// </summary>
public static class SelecaoTecnica
{
    /// <summary>
    /// Os campos da seleção, na ordem em que a equipe os lê. A chave é o que
    /// fica gravado na proposta; o rótulo é o que aparece na tela.
    /// </summary>
    public static readonly (string Chave, string Rotulo)[] Campos =
    {
        ("serie", "Série / modelo"),
        ("rotacao", "Rotação"),
        ("vazao", "Vazão"),
        ("pressao", "Pressão total"),
        ("densidade", "Densidade"),
        ("potencia", "Potência consumida"),
        ("eficiencia", "Eficiência"),
        ("angulo", "Ângulo das pás"),
    };

    public static string Rotulo(string chave) =>
        Campos.FirstOrDefault(c => c.Chave == chave).Rotulo ?? chave;

    public sealed record Resultado(
        Dictionary<string, DadoTecnico> Dados,
        List<string> NaoAchados);

    /// <summary>
    /// Lê o texto do relatório. O que não for achado NÃO entra no resultado e
    /// é listado à parte — a tela avisa, e a equipe preenche à mão.
    /// </summary>
    public static Resultado Ler(string texto)
    {
        var linhas = texto.Replace("\r\n", "\n").Split('\n');
        var dados = new Dictionary<string, DadoTecnico>();

        // "27-21 Series  2000 Half-Bladed, 3.560 Rpm"
        if (Linha(linhas, " Series ") is { } serie)
        {
            var semRotacao = Regex.Replace(serie, @",?\s*[\d.,]+\s*Rpm\b.*$", "",
                RegexOptions.IgnoreCase).Trim();

            if (semRotacao.Length > 0) dados["serie"] = new(semRotacao, "");

            if (Achar(serie, @"([\d.,]+)\s*(Rpm)\b") is { } rotacao) dados["rotacao"] = rotacao;
        }

        // "The Specified Duty is 15000 CFM, 7,00 in W.G. …"
        if (Linha(linhas, "Specified Duty") is { } duty)
        {
            if (Achar(duty, @"Specified Duty is\s+([\d.,]+)\s+([^\s,]+)") is { } vazao)
                dados["vazao"] = vazao;

            // a pressão vem logo depois da vazão, com a unidade ao lado; a
            // unidade pode ter espaço no meio ("in W.G.")
            if (Achar(duty, @"Specified Duty is\s+[\d.,]+\s+[^\s,]+,\s*([\d.,]+)\s+(in W\.G\.|[^\s,]+)")
                is { } pressao)
            {
                dados["pressao"] = pressao;
            }
        }

        // quando a seleção separa estática de total, é a TOTAL que vale, e ela
        // vem na própria frase: "The Total Pressure is 5,24 in W.G. at Density…"
        if (Linha(linhas, "The Total Pressure is") is { } total
            && Achar(total, @"Total Pressure is\s+([\d.,]+)\s+(in W\.G\.|[^\s,]+)") is { } pressaoTotal)
        {
            dados["pressao"] = pressaoTotal;
        }

        // "Density = ,066 Lb/Ft3"  ou  "Density = 1,080 Kg/m3"
        if (Linha(linhas, "Density") is { } densidade
            && Achar(densidade, @"Density\s*=\s*([\d.,]+)\s*([^\s.,]+)") is { } lida)
        {
            dados["densidade"] = lida;
        }

        // "The Calculated Power is 38,5 HP, The Efficiency is 42,9 %."
        if (Linha(linhas, "Calculated Power") is { } potencia)
        {
            if (Achar(potencia, @"Calculated Power is\s+([\d.,]+)\s+([^\s,]+)") is { } lidaPotencia)
                dados["potencia"] = lidaPotencia;
        }

        if (Linha(linhas, "Efficiency is") is { } eficiencia
            && Achar(eficiencia, @"Efficiency is\s+([\d.,]+)\s*(%)") is { } lidaEficiencia)
        {
            dados["eficiencia"] = lidaEficiencia;
        }

        // "The Tip Angle Should be 23,7 Degrees, Which is Index Number 5,3"
        if (Linha(linhas, "Tip Angle") is { } angulo
            && Achar(angulo, @"Tip Angle Should be\s+([\d.,]+)\s*(Degrees)") is { } lidoAngulo)
        {
            dados["angulo"] = new(lidoAngulo.Valor, "°");
        }

        var faltaram = Campos
            .Where(c => !dados.ContainsKey(c.Chave))
            .Select(c => c.Rotulo)
            .ToList();

        return new Resultado(dados, faltaram);
    }

    /// <summary>A primeira linha que contém o trecho.</summary>
    private static string? Linha(IEnumerable<string> linhas, string trecho) => linhas
        .FirstOrDefault(l => l.Contains(trecho, StringComparison.OrdinalIgnoreCase));

    /// <summary>O primeiro par valor+unidade que casa com o padrão.</summary>
    private static DadoTecnico? Achar(string linha, string padrao)
    {
        var m = Regex.Match(linha, padrao, RegexOptions.IgnoreCase);
        if (!m.Success) return null;

        var valor = m.Groups[1].Value.Trim();
        var unidade = m.Groups.Count > 2 ? m.Groups[2].Value.Trim().TrimEnd(',', '.') : "";

        // "in W.G." perde o ponto final no TrimEnd acima; devolve
        if (unidade.Equals("in W.G", StringComparison.OrdinalIgnoreCase)) unidade = "in W.G.";

        return valor.Length > 0 ? new DadoTecnico(valor, unidade) : null;
    }
}
