namespace HowdenAxiais.Poc.Data;

/// <summary>Uma tabela da parte elétrica: o cabeçalho e as linhas dela.</summary>
public sealed record QuadroEletrico(string Titulo, List<string> Linhas);

/// <summary>
/// A parte elétrica de um equipamento: a tabela do motor e, embaixo dela, a do
/// quadro do partidor.
///
/// Sai do ESCOPO, e não de um preenchimento à parte:
///
/// <list type="bullet">
/// <item>a tabela do motor segue o MOTOR: sem motor no escopo, ela não sai;</item>
/// <item>a tabela do quadro segue o PARTIDOR, e sai mesmo sem motor — quem
/// compra só o partidor, para usar com o motor que já tem, precisa do quadro
/// descrito na proposta;</item>
/// <item>com os dois, a linha do método de partida fecha a tabela do motor e o
/// quadro vem embaixo;</item>
/// <item>a partida direta não leva quadro próprio, como no modelo da equipe:
/// com ela, só a linha do método de partida.</item>
/// </list>
///
/// A tensão, a frequência e o grau de proteção vêm do motor e da opção do
/// partidor. É a mesma regra da potência: o documento não pode dizer 380 V se
/// o motor do sistema é 440 V.
/// </summary>
public static class DadosEletricos
{
    /// <summary>
    /// O que sai quando o motor não diz a tensão. É o padrão do modelo da
    /// equipe — não é chute, é o que eles escrevem à mão quando ainda não há
    /// motor escolhido.
    /// </summary>
    private const string TensaoPadrao = "380 V / 50 Hz";

    /// <summary>
    /// As tabelas da parte elétrica deste equipamento, na ordem em que entram
    /// no documento. Vazia quando o motor não está no escopo.
    /// </summary>
    public static List<QuadroEletrico> De(ItemProposta item, CustoDaProposta custo, Moeda moeda,
        TextosEletricos textos)
    {
        var motor = custo.MotorDe(item);
        var partidor = Partidor(item, custo, moeda);

        var tensao = TensaoEFrequencia(motor);
        var tipo = Tipo(partidor);

        var quadros = new List<QuadroEletrico>();

        // a tabela do motor segue o MOTOR, e a do quadro segue o PARTIDOR. São
        // duas vendas separadas: o cliente que compra só o partidor, com o
        // motor dele, tem direito ao quadro descrito na proposta
        if (item.ComMotor)
        {
            var doMotor = textos.LinhasDoMotor.Select(l => Preencher(l, tensao, "")).ToList();

            if (partidor.Length > 0)
                doMotor.Add(string.Format(textos.MetodoDeArranque, Metodo(tipo, partidor, textos)));

            quadros.Add(new(textos.Motores, doMotor));
        }

        if (Tabela(tipo, textos) is { } tabela)
        {
            quadros.Add(new(tabela.Titulo,
                tabela.Linhas.Select(l => Preencher(l, tensao, Protecao(partidor, l))).ToList()));
        }

        return quadros;
    }

    /// <summary>O partidor escolhido, ou vazio quando não há.</summary>
    public static string Partidor(ItemProposta item, CustoDaProposta custo, Moeda moeda) =>
        custo.LinhaDoPartidor(item, moeda) is { Ausencia: false } linha ? linha.Opcao.Trim() : "";

    /// <summary>
    /// "380 V / 50 Hz" — do motor escolhido, quando há um. A tensão vem como a
    /// equipe digitou ("220/380 V", "440"), então a unidade só é colada quando
    /// ela não está lá.
    /// </summary>
    private static string TensaoEFrequencia(Motor? motor)
    {
        if (motor is null) return TensaoPadrao;

        var v = Unidade(motor.Tensao, "V");
        var hz = Unidade(motor.Frequencia, "Hz");

        if (v.Length == 0 && hz.Length == 0) return TensaoPadrao;
        if (v.Length == 0) return hz;
        if (hz.Length == 0) return v;

        return $"{v} / {hz}";
    }

    /// <summary>
    /// O valor com a unidade, sem repeti-la quando a equipe já a digitou.
    /// </summary>
    private static string Unidade(string valor, string unidade)
    {
        var texto = valor.Trim();
        if (texto.Length == 0) return "";

        return texto.Contains(unidade, StringComparison.OrdinalIgnoreCase)
            ? texto
            : $"{texto} {unidade}";
    }

    /// <summary>Que tipo de partidor foi escolhido, pelo nome da opção.</summary>
    public enum TipoDePartidor { Nenhum, Dol, Yd, Ss, Vdf, Outro }

    /// <summary>
    /// Casa a opção do cadastro com o tipo. Por palavra, e não pelo texto
    /// inteiro, porque o nome da opção é de vocês e muda: "SOFTSTARTER IP65"
    /// e "Partidor SS Howden IP-55 Ric-02" são o mesmo quadro.
    /// </summary>
    public static TipoDePartidor Tipo(string opcao)
    {
        var o = Textos.Simples(opcao);
        if (o.Length == 0) return TipoDePartidor.Nenhum;

        // o VDF vem antes porque uma opção pode citar os dois ("VDF com
        // softstarter de retaguarda"), e aí o quadro é o do inversor
        if (Palavra(o, "vdf") || Palavra(o, "vfd") || o.Contains("variador")
            || o.Contains("inversor")) return TipoDePartidor.Vdf;

        if (Palavra(o, "ss") || o.Contains("soft")) return TipoDePartidor.Ss;

        if (o.Contains("estrela") || o.Contains("estrella") || o.Contains("triangulo")
            || Palavra(o, "yd")) return TipoDePartidor.Yd;

        if (Palavra(o, "dol") || o.Contains("direta") || o.Contains("directa"))
            return TipoDePartidor.Dol;

        return TipoDePartidor.Outro;
    }

    /// <summary>
    /// A sigla como PALAVRA, e não como pedaço de outra. Sem isto, "ss" casaria
    /// dentro de "classe" e todo partidor viraria softstarter.
    /// </summary>
    private static bool Palavra(string texto, string sigla) => texto
        .Split(' ', '-', '/', '.', ',', '(', ')')
        .Any(p => p == sigla);

    /// <summary>
    /// A descrição do método de partida. Para os quatro tipos do modelo, o
    /// texto do modelo; para o resto — um partidor especial, um nome que
    /// ninguém previu —, a opção como vocês a escreveram, que é melhor que
    /// calar.
    /// </summary>
    private static string Metodo(TipoDePartidor tipo, string opcao, TextosEletricos textos) =>
        tipo switch
        {
            TipoDePartidor.Dol => textos.Dol,
            TipoDePartidor.Yd => textos.Yd,
            TipoDePartidor.Ss => textos.Ss,
            TipoDePartidor.Vdf => textos.Vdf,
            _ => opcao,
        };

    /// <summary>
    /// A tabela do quadro. O DOL não tem: partida direta não leva quadro
    /// próprio no modelo da equipe.
    /// </summary>
    private static (string Titulo, string[] Linhas)? Tabela(TipoDePartidor tipo,
        TextosEletricos textos) => tipo switch
        {
            TipoDePartidor.Ss => (textos.TituloSs, textos.LinhasSs),
            TipoDePartidor.Yd => (textos.TituloYd, textos.LinhasYd),
            TipoDePartidor.Vdf => (textos.TituloVdf, textos.LinhasVdf),
            _ => null,
        };

    /// <summary>
    /// O grau de proteção do gabinete: o que a opção disser ("SOFTSTARTER
    /// IP54"), e o do modelo quando ela não diz.
    /// </summary>
    private static string Protecao(string opcao, string linhaDoModelo)
    {
        var achado = System.Text.RegularExpressions.Regex.Match(opcao, @"IP\s*-?\s*(\d{2})",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (achado.Success) return "IP" + achado.Groups[1].Value;

        // sem IP na opção, fica o do modelo — que é diferente em cada tabela
        // (IP65 no SS e no YD, IP54 no VDF), e por isso sai da própria linha
        return linhaDoModelo.Contains("sin puerta") || linhaDoModelo.Contains("sem porta")
            || linhaDoModelo.Contains("without inner")
            ? "IP54"
            : "IP65";
    }

    /// <summary>
    /// Troca os espaços reservados da linha. Linha sem espaço reservado passa
    /// inteira — é o caso da maioria delas.
    /// </summary>
    private static string Preencher(string linha, string tensao, string protecao) =>
        linha.Contains('{') ? string.Format(linha, tensao, protecao) : linha;
}
