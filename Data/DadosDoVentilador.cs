namespace HowdenAxiais.Poc.Data;

/// <summary>
/// A tabela "DATOS DEL VENTILADOR" da proposta técnica, montada a partir do
/// que a proposta já tem: o escopo, a seleção e o motor.
///
/// Mora aqui, e não na tela, porque quem desenha a tabela na tela e quem a
/// escreve no documento precisam da MESMA lista — foi assim que o custo e o
/// pricing pararam de discordar entre a tela e a planilha.
/// </summary>
public static class DadosDoVentilador
{
    /// <param name="Origem">
    /// De onde o valor saiu, para a tela poder dizer. Vazio quando o valor é
    /// fixo do modelo.
    /// </param>
    public sealed record Linha(string Rotulo, string Valor, string Origem)
    {
        public bool Falta => Valor.Trim().Length == 0;

        /// <summary>
        /// Que linha é esta ("angulo", "vazao", "diametro"…), para a tela saber
        /// qual campo editar sem depender do rótulo, que muda com o idioma do
        /// documento. Fica fora do construtor posicional: o gerador do Word só lê
        /// <see cref="Rotulo"/> e <see cref="Valor"/> e não precisa saber dela.
        /// </summary>
        public string Chave { get; init; } = "";
    }

    /// <summary>
    /// As chaves das linhas que vêm do relatório de seleção, na ordem da
    /// tabela. A tela as recolhe numa linha de resumo quando todas foram lidas.
    /// </summary>
    public static readonly string[] DaSelecao =
        { "angulo", "densidade", "vazao", "pressao", "eficiencia", "rotacao", "potencia" };

    /// <summary>O que o modelo traz pronto e não depende da proposta.</summary>
    public const string RegimeDeTrabalho = "Heavy Duty Continuo";

    /// <summary>O arranjo é sempre 4 nos axiais da equipe.</summary>
    public const string Arranjo = "4";

    /// <param name="codigo">
    /// O código do equipamento (ver <see cref="CodigoDoEquipamento"/>). Vem de
    /// fora porque montá-lo pede o cadastro inteiro, e esta classe só conhece a
    /// proposta.
    /// </param>
    public static List<Linha> De(ItemProposta item, Equipamento? modelo, Motor? motor,
        TextosDaProposta textos, string codigo = "")
    {
        var t = item.Tecnicos;
        var r = textos.RotulosDoVentilador;

        // o diâmetro, a altitude e o ruído são digitados na aba Técnica (antes
        // saíam em branco e eram completados no Word, a cada geração)
        var valores = new (string Chave, string Valor, string Origem)[]
        {
            ("quantidade", item.Quantos.ToString(), "escopo"),
            ("modelo", modelo?.Rotulo ?? "", "escopo"),
            ("codigo", codigo, "escopo"),
            ("aplicacao", item.Aplicacao.Trim(), "escopo"),
            ("angulo", Dado(t, "angulo"), "seleção"),
            ("montagem", Montagem(item), "escopo"),
            ("diametro", item.DiametroMm.Trim(), "à mão"),
            ("regime", RegimeDeTrabalho, ""),
            ("altitude", item.AltitudeMsnm.Trim(), "à mão"),
            ("densidade", Dado(t, "densidade"), "seleção"),
            ("vazao", Dado(t, "vazao"), "seleção"),
            ("pressao", Dado(t, "pressao"), "seleção"),
            ("eficiencia", Dado(t, "eficiencia"), "seleção"),
            ("rotacao", Dado(t, "rotacao"), "seleção"),
            ("potencia", Dado(t, "potencia"), "seleção"),
            ("ruido", Ruido(item.RuidoDb), "à mão"),
            ("motor", DoMotor(motor), "parte elétrica"),
        };

        return valores
            .Select((v, i) => new Linha(
                r[i].Length > 0 ? r[i] : RotuloDaPressao(t, textos), v.Valor, v.Origem) { Chave = v.Chave })
            .ToList();
    }

    /// <summary>
    /// "Presión total" ou "Presión estática", conforme a SELEÇÃO disser — o
    /// relatório do Joy dá a total e o do VAX dá a estática, e o documento não
    /// pode chamar as duas da mesma coisa.
    ///
    /// Quando a seleção não diz qual é, fica só "Presión": afirmar uma das
    /// duas sem o arquivo dizer seria inventar.
    /// </summary>
    private static string RotuloDaPressao(Dictionary<string, DadoTecnico> tecnicos,
        TextosDaProposta textos)
    {
        var tipo = Dado(tecnicos, "pressaoTipo").Trim();
        if (tipo.Length == 0) return textos.Pressao;

        if (Textos.Igual(tipo, SelecaoTecnica.Estatica)) return textos.PressaoEstatica;
        if (Textos.Igual(tipo, SelecaoTecnica.Total)) return textos.PressaoTotal;

        return $"{textos.Pressao} {tipo}";
    }

    /// <summary>
    /// A linha do tipo de montagem: "Horizontal ao piso, arranjo 4".
    ///
    /// São três pedaços de lugares diferentes — a montagem e o arranjo do
    /// escopo, e o número 4, que é fixo nos axiais.
    /// </summary>
    public static string Montagem(ItemProposta item)
    {
        if (item.Montagem.Trim().Length == 0) return "";

        var onde = Textos.Simples(item.Arranjo) switch
        {
            "teto" => " ao teto",
            "piso" => " ao piso",
            _ => "",
        };

        return $"{item.Montagem.Trim()}{onde}, arranjo {Arranjo}";
    }

    /// <summary>
    /// O motor como a proposta técnica o escreve: "25HP / 2 Polos / 50Hz / 380V".
    ///
    /// O que falta no cadastro do motor simplesmente não aparece, em vez de
    /// sair uma barra solta sem nada do lado.
    /// </summary>
    public static string DoMotor(Motor? motor)
    {
        if (motor is null) return "";

        var partes = new[]
        {
            Com(motor.PotenciaCv, "HP"),
            Com(motor.Polos, " Polos"),
            Com(motor.Frequencia, "Hz"),
            Com(motor.Tensao, "V"),
        };

        return string.Join(" / ", partes.Where(p => p.Length > 0));
    }

    /// <summary>
    /// O valor com a sua unidade. A unidade não é repetida quando a equipe já
    /// a digitou junto do número ("220/380 V" não vira "220/380 VV").
    /// </summary>
    private static string Com(string valor, string unidade)
    {
        var limpo = valor.Trim();
        if (limpo.Length == 0) return "";

        return limpo.EndsWith(unidade.Trim(), StringComparison.OrdinalIgnoreCase)
            ? limpo
            : limpo + unidade;
    }

    /// <summary>
    /// O ruído com a unidade: o rótulo do documento ("Ruido a 1 m de distancia")
    /// não diz dB(A), ao contrário do diâmetro e da altitude, que trazem a
    /// unidade no rótulo. Só um número ganha a unidade — o que a equipe
    /// escreveu com unidade ou com texto ("85 dB(A)", "&lt; 85") sai como está.
    /// </summary>
    public static string Ruido(string valor)
    {
        var limpo = valor.Trim();
        if (limpo.Length == 0) return "";

        return limpo.All(c => char.IsDigit(c) || c is '.' or ',' or ' ')
            ? limpo + " dB(A)"
            : limpo;
    }

    private static string Dado(Dictionary<string, DadoTecnico> tecnicos, string chave) =>
        tecnicos.TryGetValue(chave, out var d) ? d.Inteiro : "";
}
