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
        ("pa", "Tipo de pá"),
        ("rotacao", "Rotação"),
        ("vazao", "Vazão"),
        ("pressao", "Pressão"),
        ("pressaoTipo", "Pressão é"),
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
    ///
    /// Os dois programas de seleção escrevem de jeitos diferentes, e o formato
    /// é reconhecido pelo próprio conteúdo: o do Joy conta o resultado em
    /// frases ("The Specified Duty is 15000 CFM…"), o do VAX lista rótulo e
    /// valor em linhas alternadas ("Flow:" / "100,000.0 CFM").
    /// </summary>
    public static Resultado Ler(string texto)
    {
        var linhas = texto.Replace("\r\n", "\n").Split('\n');

        var dados = EhDoVax(linhas) ? DoVax(linhas) : DoJoy(linhas);

        var naoAchados = Campos
            .Where(c => !dados.ContainsKey(c.Chave))
            .Select(c => c.Rotulo)
            .ToList();

        return new Resultado(dados, naoAchados);
    }

    /// <summary>
    /// O relatório do VAX se reconhece pelos rótulos com dois pontos numa
    /// linha só — o do Joy não tem nenhum deles.
    /// </summary>
    private static bool EhDoVax(string[] linhas) => linhas
        .Any(l => l.Trim().Equals("Blade Angle:", StringComparison.OrdinalIgnoreCase)
                  || l.Trim().Equals("Blade Type:", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// O relatório do VAX: cada rótulo numa linha, o valor na linha seguinte.
    /// A unidade vem colada no valor ("880 RPM", "2.00 in wg").
    /// </summary>
    private static Dictionary<string, DadoTecnico> DoVax(string[] linhas)
    {
        var dados = new Dictionary<string, DadoTecnico>();

        var deParaVax = new (string Chave, string Rotulo)[]
        {
            ("serie", "Fan:"),
            ("pa", "Blade Type:"),
            ("rotacao", "Speed:"),
            ("vazao", "Flow:"),
            ("pressao", "Pressure"),
            ("densidade", "Density:"),
            ("potencia", "Power:"),
            ("angulo", "Blade Angle:"),
        };

        foreach (var (chave, rotulo) in deParaVax)
        {
            if (DepoisDoRotulo(linhas, rotulo) is not { } valor) continue;
            dados[chave] = Separar(valor);
        }

        // o rótulo da pressão diz qual das duas ela é: "Pressure (SP)" é a
        // estática, "(TP)" é a total
        if (Linha(linhas, "Pressure") is { } daPressao && TipoDaPressao(daPressao) is { } tipo)
            dados["pressaoTipo"] = new(tipo, "");

        return dados;
    }

    /// <summary>
    /// Qual pressão o texto anuncia: a total ou a estática. Nulo quando ele
    /// não diz — e aí a proposta sai só com "Presión", sem afirmar o que não
    /// está escrito em lugar nenhum.
    /// </summary>
    private static string? TipoDaPressao(string texto)
    {
        var t = texto.ToLowerInvariant();

        if (t.Contains("(tp)") || t.Contains("total pressure")) return Total;
        if (t.Contains("(sp)") || t.Contains("static pressure")) return Estatica;

        return null;
    }

    public const string Total = "Total";
    public const string Estatica = "Estática";

    /// <summary>
    /// A primeira linha com conteúdo depois da linha do rótulo. Um rótulo sem
    /// valor embaixo ("Tag:" seguido de linha em branco) não vale — pegar a
    /// linha seguinte daria o rótulo seguinte como se fosse o valor.
    /// </summary>
    private static string? DepoisDoRotulo(string[] linhas, string rotulo)
    {
        for (var i = 0; i < linhas.Length - 1; i++)
        {
            if (!linhas[i].TrimStart().StartsWith(rotulo, StringComparison.OrdinalIgnoreCase)) continue;

            var valor = linhas[i + 1].Trim();
            if (valor.Length == 0 || valor.EndsWith(':')) return null;

            return valor;
        }

        return null;
    }

    /// <summary>
    /// Separa o número da unidade.
    ///
    /// Só separa quando há ESPAÇO entre os dois ("880 RPM", "2.00 in wg").
    /// Sem o espaço, o texto é um nome e não um número com unidade: o modelo
    /// "7200-VAX-2700" começa com dígitos, e separar ali daria "7200" de valor
    /// e "-VAX-2700" de unidade.
    /// </summary>
    private static DadoTecnico Separar(string texto)
    {
        var m = Regex.Match(texto.Trim(), @"^([\d.,]+)\s+(.+)$");

        return m.Success
            ? new DadoTecnico(m.Groups[1].Value.Trim(), m.Groups[2].Value.Trim())
            : new DadoTecnico(texto.Trim(), "");
    }

    /// <summary>O relatório do Joy, contado em frases.</summary>
    private static Dictionary<string, DadoTecnico> DoJoy(string[] linhas)
    {
        var dados = new Dictionary<string, DadoTecnico>();

        // "27-21 Series  2000 Half-Bladed, 3.560 Rpm"
        if (Linha(linhas, " Series ") is { } serie)
        {
            var semRotacao = Regex.Replace(serie, @",?\s*[\d.,]+\s*Rpm\b.*$", "",
                RegexOptions.IgnoreCase).Trim();

            // o tipo de pá vem grudado na série aqui ("2000 Half-Bladed"), e
            // no VAX vem num campo próprio: sai daqui e vai para o mesmo campo
            var tipoDePa = Regex.Match(semRotacao, @"(Full|Half)[\s-]*Bladed", RegexOptions.IgnoreCase);
            if (tipoDePa.Success)
            {
                dados["pa"] = new(tipoDePa.Value.Trim(), "");
                semRotacao = semRotacao.Replace(tipoDePa.Value, "").TrimEnd(' ', ',', '-');
            }

            // o relatório alinha a série com espaços; o documento não precisa
            // deles
            semRotacao = Regex.Replace(semRotacao, @"\s{2,}", " ").Trim();

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
                if (TipoDaPressao(duty) is { } tipo) dados["pressaoTipo"] = new(tipo, "");
            }
        }

        // quando a seleção separa estática de total, é a TOTAL que vale, e ela
        // vem na própria frase: "The Total Pressure is 5,24 in W.G. at Density…"
        if (Linha(linhas, "The Total Pressure is") is { } total
            && Achar(total, @"Total Pressure is\s+([\d.,]+)\s+(in W\.G\.|[^\s,]+)") is { } pressaoTotal)
        {
            dados["pressao"] = pressaoTotal;
            dados["pressaoTipo"] = new(Total, "");
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

        return dados;
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
