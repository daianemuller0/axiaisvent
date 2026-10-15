namespace HowdenAxiais.Poc.Data;

/// <summary>
/// A conta de uma proposta, fora da tela.
///
/// Nasceu quando a lista de propostas passou a precisar do valor de cada uma:
/// repetir a conta lá daria dois resultados diferentes no dia em que uma das
/// duas mudasse. Aqui ela é uma só, e a tela da proposta é uma casca em volta
/// desta classe.
///
/// Recebe o cadastro já lido — quem chama é que decide quando ler o banco.
/// </summary>
public sealed class CustoDaProposta
{
    private readonly List<Equipamento> _equipamentos;
    private readonly List<Motor> _motores;
    private readonly List<PrecoReferencia> _precos;
    private readonly Dictionary<string, List<Caracteristica>> _porGrupo;

    public CustoDaProposta(
        List<Equipamento> equipamentos,
        List<Motor> motores,
        List<Caracteristica> caracteristicas,
        List<GrupoCaracteristica> grupos,
        List<PrecoReferencia> precos)
    {
        _equipamentos = equipamentos;
        _motores = motores;
        _precos = precos;
        _porGrupo = caracteristicas.GroupBy(c => c.Grupo).ToDictionary(g => g.Key, g => g.ToList());

        ListaDosPartidores = grupos.Select(g => g.Nome)
            .FirstOrDefault(n => Textos.Simples(n).Contains("partidor")) ?? "PARTIDORES";

        ListaDaInstrumentacao = grupos.Select(g => g.Nome)
            .FirstOrDefault(n => Textos.Simples(n).Contains("instrumenta")) ?? "INSTRUMENTAÇÃO";

        // o escopo do ventilador mostra as listas na ordem do cadastro, menos as
        // da parte elétrica, que têm bloco próprio
        ListasDoEscopo = grupos
            .OrderBy(g => g.Ordem)
            .Select(g => g.Nome)
            .Where(nome => !EhDaParteEletrica(nome))
            .ToList();
    }

    public List<string> ListasDoEscopo { get; }
    public string ListaDosPartidores { get; }
    public string ListaDaInstrumentacao { get; }

    public static bool EhDaParteEletrica(string lista)
    {
        var nome = Textos.Simples(lista);
        return nome.Contains("partidor") || nome.Contains("instrumenta");
    }

    /// <summary>As opções de uma lista que não são "sem" — as que se escolhe.</summary>
    public List<Caracteristica> OpcoesReais(string lista) => _porGrupo
        .GetValueOrDefault(lista, new())
        .Where(o => !FamiliaDePreco.EhAusencia(o.Valor))
        .ToList();

    public List<Caracteristica> Opcoes(string lista) => _porGrupo.GetValueOrDefault(lista, new());

    // ---------- o que cada item da proposta escolheu ----------

    public Equipamento? Modelo(ItemProposta item) =>
        _equipamentos.FirstOrDefault(e => e.Id == item.EquipamentoId);

    public string SerieDe(ItemProposta item) => Modelo(item)?.Serie ?? "";

    public List<Motor> Candidatos(ItemProposta item) =>
        EscopoProposta.MotoresPara(_motores, SerieDe(item), item.FiltroMotor);

    public List<string> OpcoesDoCampo(ItemProposta item,
        (string Rotulo, Func<Motor, string> Valor) campo) =>
        EscopoProposta.MotoresPara(_motores, SerieDe(item), item.FiltroMotor, campo.Rotulo)
            .Select(campo.Valor)
            .Where(v => v.Length > 0)
            .Distinct()
            .OrderBy(v => Medida.Numero(v))
            .ThenBy(v => v, StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>O motor escolhido: existe quando o filtro chegou a um só.</summary>
    public Motor? MotorDe(ItemProposta item)
    {
        if (!item.ComMotor) return null;

        var candidatos = Candidatos(item);
        if (candidatos.Count == 1) return candidatos[0];

        return item.MotorId.Length > 0
            ? candidatos.FirstOrDefault(m => m.Id == item.MotorId)
            : null;
    }

    // ---------- preços ----------

    public decimal? PrecoDoModelo(ItemProposta item, Moeda moeda) => Modelo(item) is { } modelo
        ? DadosExcel.Numero(EscopoProposta.PrecoDoEquipamento(modelo, moeda))
        : null;

    public decimal? PrecoDoMotor(ItemProposta item, Moeda moeda) => MotorDe(item) is { } motor
        ? DadosExcel.Numero(EscopoProposta.PrecoDoMotor(motor, moeda))
        : null;

    /// <param name="potencia">
    /// A potência que manda no preço de quem se precifica por ela — o partidor.
    /// Vazia, vale a do motor do escopo fechado. Serve aos OPCIONAIS: um
    /// partidor opcional de um equipamento cujo motor também é opcional não
    /// tem potência no escopo fechado para consultar, e sem isto ficaria sem
    /// preço.
    /// </param>
    public ItemDoEscopo Resolver(ItemProposta item, string grupo, string valor, Moeda moeda,
        string potencia = "")
    {
        var opcao = Opcoes(grupo).FirstOrDefault(o => o.Valor == valor)
            ?? new Caracteristica { Grupo = grupo, Valor = valor };

        var cv = potencia.Trim().Length > 0 ? potencia.Trim() : MotorDe(item)?.PotenciaCv ?? "";

        return EscopoProposta.Resolver(opcao, Modelo(item), moeda, _precos, cv);
    }

    /// <summary>As linhas do escopo de um equipamento, na ordem das listas.</summary>
    public List<ItemDoEscopo> Itens(ItemProposta item, Moeda moeda) => ListasDoEscopo
        .Where(l => item.Escolhas.ContainsKey(l) && item.Escolhas[l].Length > 0)
        .Select(l => Resolver(item, l, item.Escolhas[l], moeda))
        .ToList();

    /// <summary>
    /// A linha do partidor, que se precifica pela potência do motor já escolhido.
    /// </summary>
    public ItemDoEscopo? LinhaDoPartidor(ItemProposta item, Moeda moeda) =>
        item.ComPartidor && item.Partidor.Length > 0
            ? Resolver(item, ListaDosPartidores, item.Partidor, moeda)
            : null;

    /// <summary>
    /// Todas as linhas de preço de um equipamento: a chave e o que o cadastro
    /// puxou. Quem soma, quem desenha e quem avisa olham esta mesma lista.
    /// </summary>
    public List<(string Chave, decimal? Puxado)> LinhasDePreco(ItemProposta item, Moeda moeda)
    {
        var linhas = new List<(string, decimal?)> { ("equipamento", PrecoDoModelo(item, moeda)) };

        linhas.AddRange(Itens(item, moeda).Select(i => ($"lista:{i.Lista}", i.Valor)));

        if (item.ComMotor) linhas.Add(("motor", PrecoDoMotor(item, moeda)));
        if (LinhaDoPartidor(item, moeda) is { } partidor) linhas.Add(("partidor", partidor.Valor));

        if (item.ComInstrumentacao)
        {
            linhas.AddRange(item.Instrumentacao.Select(nome =>
                ($"instr:{nome}", Resolver(item, ListaDaInstrumentacao, nome, moeda).Valor)));
        }

        return linhas;
    }

    // ---------- itens opcionais ----------

    /// <summary>
    /// Uma linha de item opcional: o que é, quanto custa e de onde o preço
    /// veio. A <paramref name="Chave"/> é a do preço à mão, e começa com
    /// "opc:" para nunca colidir com a do escopo fechado.
    /// </summary>
    /// <param name="Lista">A lista do cadastro, ou vazio no motor e na instrumentação.</param>
    public sealed record LinhaOpcional(string Chave, string Lista, string Opcao, decimal? Valor,
        string Origem)
    {
        /// <summary>O nome como ele sai no documento, do mesmo catálogo do escopo.</summary>
        public string Nome(TextosDoEscopo textos) => EscopoDaHowden.Nome(Lista, Opcao, textos);
    }

    /// <summary>
    /// Os itens opcionais de um equipamento: as listas do escopo escolhidas em
    /// <see cref="ItemProposta.Opcionais"/>, mais o motor e a instrumentação
    /// opcionais.
    ///
    /// O CUSTO sai exatamente das mesmas regras do escopo fechado — o mesmo
    /// <see cref="Resolver"/>, logo a mesma tabela por Fan Diameter, o mesmo
    /// preço solto da opção, o mesmo catálogo de motores. O que muda é só o
    /// destino: eles NÃO entram em <see cref="LinhasDePreco"/>, e por isso não
    /// entram no pricing. O preço de venda deles é o custo vezes o fator da
    /// proposta (ver <see cref="PropostaWord.PrecoDoOpcional"/>).
    /// </summary>
    public List<LinhaOpcional> Opcionais(ItemProposta item, Moeda moeda)
    {
        var linhas = new List<LinhaOpcional>();

        // o motor que manda no preço do partidor opcional: o opcional, quando
        // há um — eles vão juntos —, e o do escopo fechado quando não há
        var potencia = (MotorOpcionalDe(item) ?? MotorDe(item))?.PotenciaCv ?? "";

        foreach (var lista in ListasDoEscopo.Append(ListaDosPartidores))
        {
            var opcao = item.Opcionais.GetValueOrDefault(lista, "").Trim();
            if (opcao.Length == 0 || FamiliaDePreco.EhAusencia(opcao)) continue;

            var resolvida = Resolver(item, lista, opcao, moeda, potencia);
            linhas.Add(new($"opc:{lista}", lista, opcao, resolvida.Valor, resolvida.Origem));
        }

        if (MotorOpcionalDe(item) is { } motor)
        {
            linhas.Add(new("opc:motor", "", motor.NoDocumento is { Length: > 0 } d ? $"Motor elétrico — {d}" : "Motor elétrico",
                DadosExcel.Numero(EscopoProposta.PrecoDoMotor(motor, moeda)),
                "catálogo de motores"));
        }

        foreach (var nome in item.InstrumentacaoOpcional.Where(n => n.Trim().Length > 0))
        {
            var resolvida = Resolver(item, ListaDaInstrumentacao, nome, moeda, potencia);
            linhas.Add(new($"opc:instr:{nome}", "", nome, resolvida.Valor, resolvida.Origem));
        }

        return linhas;
    }

    /// <summary>O motor opcional escolhido, se houver.</summary>
    public Motor? MotorOpcionalDe(ItemProposta item) => item.MotorOpcionalId.Length > 0
        ? _motores.FirstOrDefault(m => m.Id == item.MotorOpcionalId)
        : null;

    /// <summary>O custo UNITÁRIO de um opcional: o do cadastro, ou o da mão.</summary>
    public decimal CustoDoOpcional(ItemProposta item, LinhaOpcional linha) =>
        Efetivo(item, linha.Chave, linha.Valor) ?? 0m;

    /// <summary>
    /// O custo somado dos opcionais de um equipamento, já vezes a quantidade —
    /// mesma conta do <see cref="Subtotal"/> do escopo fechado.
    ///
    /// São dois ventiladores, são dois dampers: o opcional acompanha a
    /// quantidade do equipamento a que pertence, como qualquer peça dele.
    /// </summary>
    public decimal CustoDosOpcionais(ItemProposta item, Moeda moeda) =>
        Opcionais(item, moeda).Sum(l => CustoDoOpcional(item, l)) * item.Quantos;

    /// <summary>Opcionais que foram escolhidos e continuam sem preço.</summary>
    public int OpcionaisSemPreco(Proposta proposta) => proposta.Itens.Sum(item =>
        Opcionais(item, proposta.Moeda).Count(l => Efetivo(item, l.Chave, l.Valor) is null));

    /// <summary>
    /// O preço que vale: o digitado à mão, quando houver; o do cadastro, quando
    /// não. É a regra geral da proposta — puxa, mas sempre dá para corrigir.
    /// </summary>
    public static decimal? Efetivo(ItemProposta item, string chave, decimal? puxado) =>
        item.PrecosManuais.TryGetValue(chave, out var texto) && texto.Trim().Length > 0
            ? DadosExcel.Numero(texto)
            : puxado;

    public decimal Unitario(ItemProposta item, Moeda moeda) =>
        LinhasDePreco(item, moeda).Sum(l => Efetivo(item, l.Chave, l.Puxado) ?? 0m);

    public decimal Subtotal(ItemProposta item, Moeda moeda) =>
        Unitario(item, moeda) * item.Quantos;

    /// <summary>O custo total da proposta — o que vai para a célula E33 do pricing.</summary>
    public decimal Total(Proposta proposta) =>
        proposta.Itens.Sum(item => Subtotal(item, proposta.Moeda));

    /// <summary>Linhas escolhidas que continuam sem preço nenhum.</summary>
    public int FaltaPreco(Proposta proposta) => proposta.Itens.Sum(item =>
        LinhasDePreco(item, proposta.Moeda).Count(l => Efetivo(item, l.Chave, l.Puxado) is null));

    /// <summary>
    /// O código do equipamento, nos sete grupos que a equipe escreve à mão —
    /// ver <see cref="CodigoDoEquipamento"/>. Mora lá porque a tela, a proposta
    /// comercial e a técnica precisam todas do mesmo, e um código montado em
    /// três lugares vira três códigos.
    /// </summary>
    public string Codigo(ItemProposta item, Moeda moeda) =>
        CodigoDoEquipamento.De(item, this, moeda);

    /// <summary>Quantos equipamentos a proposta tem, somando as quantidades.</summary>
    public static int Quantidade(Proposta proposta) => proposta.Itens.Sum(i => i.Quantos);
}
