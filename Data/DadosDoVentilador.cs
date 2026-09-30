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
    }

    /// <summary>O que o modelo traz pronto e não depende da proposta.</summary>
    public const string RegimeDeTrabalho = "Heavy Duty Continuo";

    /// <summary>O arranjo é sempre 4 nos axiais da equipe.</summary>
    public const string Arranjo = "4";

    public static List<Linha> De(ItemProposta item, Equipamento? modelo, Motor? motor)
    {
        var t = item.Tecnicos;

        return new List<Linha>
        {
            new("Cantidad de ventiladores", item.Quantos.ToString(), "escopo"),
            new("Modelo Howden", modelo?.Rotulo ?? "", "escopo"),
            new("Aplicación", item.Aplicacao.Trim(), "escopo"),
            new("Ángulo de las aspas", Dado(t, "angulo"), "seleção"),
            new("Tipo de montaje", Montagem(item), "escopo"),
            new("Diámetro del ventilador (mm)", "", ""),
            new("Régimen de trabajo", RegimeDeTrabalho, ""),
            new("Altitud (m.s.n.m)", "", ""),
            new("Densidad", Dado(t, "densidade"), "seleção"),
            new("Caudal del ventilador", Dado(t, "vazao"), "seleção"),
            new("Presión", Dado(t, "pressao"), "seleção"),
            new("Eficiencia", Dado(t, "eficiencia"), "seleção"),
            new("Velocidad de giro", Dado(t, "rotacao"), "seleção"),
            new("Consumo de potencia", Dado(t, "potencia"), "seleção"),
            new("Ruido a 1 m de distancia", "", ""),
            new("Motor Eléctrico", DoMotor(motor), "parte elétrica"),
        };
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

    private static string Dado(Dictionary<string, DadoTecnico> tecnicos, string chave) =>
        tecnicos.TryGetValue(chave, out var d) ? d.Inteiro : "";
}
