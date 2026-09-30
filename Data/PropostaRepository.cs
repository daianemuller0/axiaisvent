using System.Text.Json;

namespace HowdenAxiais.Poc.Data;

/// <summary>
/// UM equipamento dentro da proposta: qual modelo, quantos, e o escopo dele.
///
/// A proposta tem uma lista deles porque a folha de dados da equipe é assim:
/// cada coluna a partir da D é um equipamento, e a linha 31 diz a quantidade.
/// Dois equipamentos iguais são UMA linha com quantidade 2; dois diferentes são
/// duas colunas, e aqui, dois itens.
/// </summary>
public sealed class ItemProposta
{
    public string Quantidade { get; set; } = "1";

    /// <summary>O id do modelo escolhido (ver <see cref="Equipamento.Chave"/>).</summary>
    public string EquipamentoId { get; set; } = "";

    /// <summary>Arranjo / instalação: "Teto" ou "Piso".</summary>
    public string Arranjo { get; set; } = "";

    /// <summary>A opção marcada em cada lista de característica.</summary>
    public Dictionary<string, string> Escolhas { get; set; } = new();

    // ---------- parte elétrica ----------

    /// <summary>Verdadeiro quando a proposta leva motor para este equipamento.</summary>
    public bool ComMotor { get; set; }

    /// <summary>
    /// O que já foi escolhido no motor, campo por campo ("Fabricante" → "WEG").
    /// A escolha é guardada campo a campo, e não só o id do motor, porque ela
    /// pode estar pela metade: a equipe vai apertando o filtro até sobrar um.
    /// </summary>
    public Dictionary<string, string> FiltroMotor { get; set; } = new();

    /// <summary>O id do motor, quando o filtro já chegou a um só.</summary>
    public string MotorId { get; set; } = "";

    /// <summary>Verdadeiro quando a proposta leva partidor.</summary>
    public bool ComPartidor { get; set; }

    /// <summary>A opção escolhida na lista de partidores.</summary>
    public string Partidor { get; set; } = "";

    /// <summary>Verdadeiro quando a proposta leva instrumentação.</summary>
    public bool ComInstrumentacao { get; set; }

    /// <summary>
    /// As instrumentações escolhidas — aqui pode ser mais de uma, ao contrário
    /// das outras listas, onde a escolha é uma só.
    /// </summary>
    public List<string> Instrumentacao { get; set; } = new();

    /// <summary>
    /// Preços digitados à mão, por linha da proposta ("motor", "partidor",
    /// "lista:Difusor"…). Todo preço puxado do cadastro pode ser trocado aqui:
    /// o cadastro é a regra, e esta é a exceção do caso concreto — inclusive
    /// quando o cadastro ainda não tem preço nenhum.
    /// </summary>
    public Dictionary<string, string> PrecosManuais { get; set; } = new();

    /// <summary>
    /// Os dados técnicos deste equipamento, lidos do relatório de seleção
    /// (ver <see cref="SelecaoTecnica"/>) e conferidos na tela.
    ///
    /// Ficam no ITEM, e não na proposta, porque cada equipamento tem a sua
    /// seleção: dois ventiladores da mesma proposta têm vazão, pressão e
    /// potência diferentes.
    /// </summary>
    public Dictionary<string, DadoTecnico> Tecnicos { get; set; } = new();

    /// <summary>O nome do arquivo de seleção de onde os dados vieram.</summary>
    public string ArquivoDaSelecao { get; set; } = "";

    /// <summary>
    /// A descrição deste equipamento na proposta comercial, quando a equipe
    /// escreveu a dela. Vazia, vale o rascunho que o sistema monta do escopo
    /// (ver <see cref="EscopoEmTexto"/>) — é a mesma regra do preço: puxa, mas
    /// dá para corrigir.
    /// </summary>
    public string Descricao { get; set; } = "";

    /// <summary>
    /// A coluna da planilha de onde este item veio ("D", "E"…), quando veio de
    /// uma. Serve para a tela dizer de onde cada coisa saiu.
    /// </summary>
    public string Coluna { get; set; } = "";

    public int Quantos => int.TryParse(Quantidade.Trim(), out var n) && n > 0 ? n : 1;
}

/// <summary>
/// Uma proposta: o cabeçalho do documento, os dados do cliente e o escopo do
/// ventilador.
///
/// O que fica gravado é a ESCOLHA, nunca o preço: moeda, modelo e a opção
/// marcada em cada lista de característica. O preço é lido do cadastro na hora
/// de mostrar — assim uma tabela de preço corrigida se reflete nas propostas
/// abertas, em vez de deixar cada uma com uma cópia velha.
/// </summary>
public sealed class Proposta
{
    public string Id { get; set; } = "";

    // ---------- cliente ----------
    public string Cliente { get; set; } = "";
    /// <summary>"Aos cuidados de" — o nome do contato na empresa cliente.</summary>
    public string AosCuidados { get; set; } = "";
    public string Pais { get; set; } = "Brasil";
    public string Email { get; set; } = "";
    public string Telefone { get; set; } = "";

    /// <summary>
    /// A cidade do cliente. Sai como "Ciudad:" no cabeçalho de todas as
    /// páginas da proposta comercial.
    /// </summary>
    public string Cidade { get; set; } = "";

    /// <summary>
    /// A referência do CLIENTE — o número que ele deu ao pedido dele. Sai como
    /// "Su referencia:" na proposta, ao lado da nossa ("Nuestra referencia",
    /// que é o número daqui).
    /// </summary>
    public string ReferenciaCliente { get; set; } = "";

    public string Projeto { get; set; } = "";
    public string Data { get; set; } = "";
    public string DataFechamento { get; set; } = "";
    public string Fase { get; set; } = "";
    public string PreparadaPor { get; set; } = "Equipe Howden";
    public string Estado { get; set; } = "";
    public string ValidadeDias { get; set; } = "30";
    public string PrazoEntregaDias { get; set; } = "12";

    // ---------- como se vende ----------

    /// <summary>
    /// A condição de pagamento, escrita como sai na proposta. Vem do cadastro
    /// (<see cref="CondicaoPagamento"/>) ou é digitada na hora.
    /// </summary>
    public string CondicaoPagamento { get; set; } = "";

    /// <summary>
    /// O incoterm da entrega (EXW, FCA, FOB, CIF, DAP, DDP). A proposta
    /// imprime só a linha escolhida, e não a tabela inteira.
    ///
    /// Guarda o CÓDIGO quando é um dos da lista, e o texto inteiro quando a
    /// equipe digitou outro — a proposta precisa sair com o que foi escolhido,
    /// mesmo que não esteja na lista.
    /// </summary>
    public string Incoterm { get; set; } = "";

    /// <summary>
    /// Destino do incoterm, quando ele pede um ("Puerto de …", "UBICACIÓN de …").
    /// Vazio em EXW, FCA e FOB, que já nascem com o lugar de saída.
    /// </summary>
    public string IncotermDestino { get; set; } = "";

    /// <summary>Como o equipamento é entregue: "Armado" ou "Desarmado".</summary>
    public string Armado { get; set; } = "";

    /// <summary>
    /// Os dias de assessoria técnica, quando a proposta tem 5 ventiladores ou
    /// mais. Até 4, o modelo já traz o prazo pronto (3 dias para um, 5 de 1 a
    /// 4) e estes campos nem aparecem; de 5 em diante ele deixa "XX días" em
    /// branco, porque o prazo é negociado.
    /// </summary>
    public string DiasDeAssessoria { get; set; } = "";

    /// <summary>Destes, quantos são de assessoria mesmo (fora ida e volta).</summary>
    public string DiasDeAssessoriaEmCampo { get; set; } = "";

    // ---------- cabeçalho ----------
    public string Ano { get; set; } = "";
    /// <summary>Número da proposta. Em branco, é gerado ao gravar.</summary>
    public string Numero { get; set; } = "";
    public string Revisao { get; set; } = "00";
    /// <summary>BU: a empresa emissora.</summary>
    public string Bu { get; set; } = "";
    public string Idioma { get; set; } = "Português";
    public string VendaPara { get; set; } = "Cliente Final";
    public string Destino { get; set; } = "Nacional";
    public string PaisDestino { get; set; } = "Brasil";
    public string Categoria { get; set; } = "Other";
    public string Produto { get; set; } = "";
    public string MarketSegment { get; set; } = "";
    public string Portal { get; set; } = "Nenhum";

    /// <summary>O representante escolhido, pelo nome.</summary>
    public string Representante { get; set; } = "";

    /// <summary>
    /// O contato do representante, guardado junto com a proposta. Não aparece
    /// na tela — a equipe escolhe pelo nome —, mas vai para o documento.
    ///
    /// Fica gravado <b>além</b> do nome porque um representante pode sair da
    /// lista, e a proposta antiga não pode perder o contato de quem a
    /// atendeu. Enquanto ele estiver na lista, quem manda é a lista: corrigir
    /// um telefone lá vale para as propostas abertas.
    /// </summary>
    public string RepresentanteContato { get; set; } = "";

    /// <summary>O segundo representante, quando a venda é dividida (P6 do pricing).</summary>
    public string Representante2 { get; set; } = "";
    public string Representante2Contato { get; set; } = "";

    /// <summary>Regime do cliente (J9). Vazio fora do Brasil = "Sem benefício".</summary>
    public string Beneficio { get; set; } = "";

    /// <summary>Fiança ou seguro garantia (P3): "Sim" ou "Não".</summary>
    public string Fianca { get; set; } = "Não";

    // ---------- pricing ----------

    /// <summary>Risco adicional (D36). Padrão 2% para axiais NB.</summary>
    public string RiscoAdicional { get; set; } = "2";

    /// <summary>Margem de negociação (D46). Padrão 3%.</summary>
    public string MargemNegociacao { get; set; } = "3";

    /// <summary>"margem" (a partir da margem pedida) ou "preco" (a partir do preço-meta).</summary>
    public string ModoDoPreco { get; set; } = "margem";

    /// <summary>Margem pedida, em % (P25). Padrão 28%.</summary>
    public string MargemAlvo { get; set; } = "28";

    /// <summary>Preço-meta, quando o cálculo parte dele.</summary>
    public string PrecoMeta { get; set; } = "";

    // ---------- histórico ----------

    /// <summary>
    /// Quando a proposta foi enviada ao cliente (aaaa-mm-dd). Vazio = ainda em
    /// elaboração. É o que separa rascunho de proposta que saiu da casa.
    /// </summary>
    public string EnviadaEm { get; set; } = "";

    /// <summary>Quando a proposta foi criada no sistema, com hora.</summary>
    public string CriadaEm { get; set; } = "";

    public bool Enviada => EnviadaEm.Trim().Length > 0;

    /// <summary>O contato que vale: o da lista, quando o representante ainda está nela.</summary>
    public string ContatoDoRepresentante()
    {
        var daLista = ListasDaProposta.ContatoDoRepresentante(Representante);
        return daLista.Length > 0 ? daLista : RepresentanteContato;
    }

    public string ContatoDoRepresentante2()
    {
        var daLista = ListasDaProposta.ContatoDoRepresentante(Representante2);
        return daLista.Length > 0 ? daLista : Representante2Contato;
    }

    /// <summary>
    /// A proposta passa por um portal de compras? No pricing isso é Sim/Não
    /// (célula P4) e vale 0,7% sobre a venda; aqui a equipe escolhe qual, e
    /// "Nenhum" é o único que quer dizer não.
    /// </summary>
    public static bool TemPortal(string portal) =>
        portal.Trim().Length > 0 && !Textos.Igual(portal, "Nenhum");

    /// <summary>
    /// O segmento de margem (J3). Ventilador axial é sempre NB — é o que faz o
    /// risco adicional nascer em 2% na própria planilha.
    /// </summary>
    public const string SegmentoDoPricing = "NB";

    /// <summary>Um percentual digitado pela equipe ("2,5" → 0,025).</summary>
    public static decimal Percentual(string texto, decimal padrao) =>
        DadosExcel.Numero(texto) is { } n ? n / 100m : padrao;

    // ---------- contato que sai no documento ----------
    public string ContatoNome { get; set; } = "";
    public string ContatoCargo { get; set; } = "";
    /// <summary>A área dele ("Pulp &amp; Paper / Steel", "UG Mining"…), quando tem.</summary>
    public string ContatoArea { get; set; } = "";
    public string ContatoEmail { get; set; } = "";
    public string ContatoTelefones { get; set; } = "";

    // O segundo contato do quadro da proposta. São dois no máximo, e o
    // segundo pode ficar vazio.
    public string Contato2Nome { get; set; } = "";
    public string Contato2Cargo { get; set; } = "";
    public string Contato2Area { get; set; } = "";
    public string Contato2Email { get; set; } = "";
    public string Contato2Telefones { get; set; } = "";

    // ---------- escopo do ventilador ----------
    /// <summary>"USD", "CLP" ou "BRL" — manda em todos os preços da proposta.</summary>
    public string MoedaCodigo { get; set; } = "USD";

    /// <summary>
    /// Os equipamentos da proposta, cada um com a sua quantidade e o seu escopo.
    ///
    /// Guardados como JSON numa coluna só: é uma lista de tamanho variável, e
    /// uma entidade à parte custaria uma leitura a mais em cada tela para
    /// nunca ser consultada sozinha.
    /// </summary>
    public List<ItemProposta> Itens { get; set; } = new();

    public Moeda Moeda => Moedas.Ler(MoedaCodigo);

    public string ItensComoTexto() => JsonSerializer.Serialize(Itens);

    /// <summary>
    /// Lê a lista gravada. Um banco anterior guardava um equipamento só, em
    /// colunas soltas — vira o primeiro item da lista, para nenhuma proposta
    /// antiga abrir vazia.
    /// </summary>
    public static List<ItemProposta> LerItens(string json, string equipamentoLegado,
        string arranjoLegado, string escolhasLegadas)
    {
        if (json.Trim().Length > 0)
        {
            try
            {
                var lista = JsonSerializer.Deserialize<List<ItemProposta>>(json);
                if (lista is not null) return lista;
            }
            catch (JsonException)
            {
                // JSON estragado não pode derrubar a tela: a proposta abre vazia
                // e a equipe refaz o escopo, que é o que dá para fazer aqui
            }
        }

        if (equipamentoLegado.Length == 0 && escolhasLegadas.Length == 0) return new();

        var escolhas = new Dictionary<string, string>();
        foreach (var linha in escolhasLegadas.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var partes = linha.Split('\t');
            if (partes.Length == 2) escolhas[partes[0]] = partes[1];
        }

        return new List<ItemProposta>
        {
            new()
            {
                Quantidade = "1", EquipamentoId = equipamentoLegado,
                Arranjo = arranjoLegado, Escolhas = escolhas,
            },
        };
    }

    /// <summary>Como a proposta aparece numa lista.</summary>
    public string Titulo =>
        (Numero.Length > 0 ? Numero : "(sem número)") +
        (Cliente.Length > 0 ? $" · {Cliente}" : "") +
        (Projeto.Length > 0 ? $" · {Projeto}" : "");
}

/// <summary>
/// As listas de escolha do cabeçalho. São as que apareceram no documento da
/// equipe; quando eles mandarem as listas completas, é aqui que crescem.
/// </summary>
public static class ListasDaProposta
{
    public static readonly string[] Paises =
        { "Brasil", "Chile", "Peru", "Colômbia", "México", "Argentina", "Estados Unidos", "Outro" };

    public static readonly string[] Fases =
        { "— Nenhuma —", "Orçamento preliminar", "Proposta firme", "Negociação", "Fechada", "Perdida" };

    /// <summary>
    /// A BU emissora (E8 do pricing). O <b>valor</b> é o código que a planilha
    /// espera — as fórmulas de ICMS e de moeda comparam com "HSA-SP", "HSA-ES"
    /// e "HCHL" —, e o rótulo é para a equipe saber qual é qual.
    /// </summary>
    public static readonly (string Codigo, string Rotulo)[] Bus =
    {
        ("HSA-SP", "HSA-SP — Itatiba (Brasil)"),
        ("HSA-ES", "HSA-ES — Serra (Brasil)"),
        ("HCHL", "HCHL — Chile"),
        ("HPU", "HPU — Peru"),
    };

    /// <summary>
    /// Os estados que o pricing aceita (J6, lista_Estados): as UFs mais EXPORT,
    /// Chile e Peru, que é como a planilha marca venda fora do Brasil.
    /// </summary>
    public static readonly string[] Estados =
    {
        "EXPORT", "Chile", "Peru",
        "AC", "AL", "AM", "AP", "BA", "CE", "DF", "ES", "GO", "MA", "MG", "MS", "MT",
        "PA", "PB", "PE", "PI", "PR", "RJ", "RN", "RO", "RR", "RS", "SC", "SE", "SP", "TO",
    };

    /// <summary>
    /// O que vai na célula J6 do pricing — a chave dos impostos brasileiros.
    ///
    /// Só o Brasil tem UF: no Chile e no Peru a planilha espera o nome do país,
    /// e em qualquer outro lugar espera "EXPORT". É isso que zera ICMS, PIS e
    /// COFINS lá dentro, então quem decide é o <b>país</b>, e não o estado que
    /// alguém tenha deixado escolhido antes de trocar de país.
    /// </summary>
    public static string EstadoDoPricing(string pais, string estado)
    {
        if (Textos.Igual(pais, "Brasil")) return estado;

        if (Textos.Igual(pais, "Chile")) return "Chile";
        if (Textos.Igual(pais, "Peru") || Textos.Igual(pais, "Perú")) return "Peru";

        return "EXPORT";
    }

    /// <summary>Venda com impostos brasileiros? Só no Brasil.</summary>
    public static bool TemImpostosBrasileiros(string pais) => Textos.Igual(pais, "Brasil");

    /// <summary>
    /// Traz uma BU gravada antes desta lista para o código da planilha, para
    /// proposta antiga não gerar pricing com a BU errada.
    /// </summary>
    public static string CodigoDaBu(string valor)
    {
        var t = Textos.Simples(valor);
        if (t.Length == 0) return "HSA-SP";

        foreach (var (codigo, _) in Bus)
            if (Textos.Igual(codigo, valor)) return codigo;

        if (t.Contains("itatiba") || t.Contains("sao paulo") || t.Contains("sp")) return "HSA-SP";
        if (t.Contains("serra") || t.Contains("es")) return "HSA-ES";
        if (t.Contains("chile") || t.Contains("santiago")) return "HCHL";
        if (t.Contains("peru") || t.Contains("lima")) return "HPU";

        return "HSA-SP";
    }

    public static readonly string[] Idiomas = { "Português", "Espanhol", "Inglês" };

    /// <summary>Venda para (J4 do pricing).</summary>
    public static readonly string[] VendaPara = { "Industrialização", "Cliente Final", "Revenda" };

    /// <summary>Destino (J5 do pricing).</summary>
    public static readonly string[] Destinos =
    {
        "Nacional", "Exportação", "Exportação com Back to Back", "Back to Back 100% importado",
    };

    /// <summary>
    /// Regime do cliente (J9 do pricing). Só aparece quando o país é o Brasil —
    /// fora dele a proposta fica em "Sem benefício", e a equipe ainda pode
    /// trocar.
    /// </summary>
    public static readonly string[] Beneficios =
    {
        "Sem benefício", "não-contribuinte ICMS", "Benefício RECAP / REIDI", "Zona Franca de Manaus",
    };

    public static readonly string[] Categorias = { "Other", "Projeto", "Reposição", "Serviço" };

    public static readonly string[] Produtos = { "ROTH1 — Ventsim Software", "Ventilador axial", "Outro" };

    public static readonly string[] Segmentos =
        { "Mining", "Tunnel", "Power", "Industrial", "Oil & Gas", "Outro" };

    public static readonly string[] Portais = { "Nenhum", "Ariba", "Coupa", "SAP", "Outro" };

    public static readonly string[] Arranjos = { "Teto", "Piso" };

    /// <summary>
    /// Os representantes, com o contato de cada um. O contato NÃO aparece na
    /// tela — a equipe escolhe pelo nome —, mas vai junto para o documento, e
    /// é por isso que ele mora aqui e é guardado na proposta.
    ///
    /// A divisão em dois grupos é a da própria planilha da equipe.
    /// </summary>
    /// <param name="Comissao">
    /// A comissão dele, da tabela BD_pricing do pricing (colunas NB e AFM são
    /// iguais hoje, então uma só basta).
    /// </param>
    public sealed record Representante(string Grupo, string Nome, string Contato, decimal Comissao);

    /// <summary>
    /// A lista é uma CÓPIA FIEL de <c>BD_pricing!A68:E101</c> da planilha de
    /// pricing — nomes, contatos e comissões.
    ///
    /// Os nomes têm de bater letra por letra: o pricing acha a comissão com
    /// <c>MATCH(P5, lista_representantes, 0)</c>, e um acento ou um espaço a
    /// mais cai no <c>IFERROR</c>, que devolve a primeira linha da tabela — a do
    /// "-", com comissão zero. O preço sairia diferente do da tela, em silêncio.
    /// É por isso que "Maurício" tem acento e "Wander (Wanseve )" tem o espaço
    /// antes do parêntese: é assim que está lá.
    /// </summary>
    public static readonly Representante[] Representantes =
    {
        new("Brasil", "Douglas (Mezza & Baga)", "Douglas M. Matavelli por (11) 97144-3085 ou e-mail: douglas.matavelli@howden.com",
            0.03m),
        new("Brasil", "Alexandre (Artman)", "Alexandre B. Pereira por (91) 98883-8142 / (16) 99429-1786 ou e-mail: alexandre.pereira@artman.net.br",
            0.03m),
        new("Brasil", "Gerson (Lizan)", "",
            0.03m),
        new("Brasil", "Ivars (Dzelme & Leite Ltda)", "Ivars Janis Dzelme por (81) 3221-0250 / (81) 99946-0506 ou e-mail: ivars@hotlink.com.br",
            0.03m),
        new("Brasil", "Júlio (Doulus)", "Júlio Augusto Afro por (27) 3314-1000 / (27) 98122-1177 ou e-mail: howden@doulus.com.br",
            0.03m),
        new("Brasil", "Maurício (Livimat)", "Mauricio A. de Araujo por (21) 99908-1687 ou e-mail: Livimat.comercial@outlook.com",
            0.03m),
        new("Brasil", "Ricardo (Sesbras)", "Ricardo V. F. Martins por (21) 2532-7404 / (21) 99764-5297 ou e-mail: aviabras@aviabras.com.br",
            0.05m),
        new("Brasil", "Sander (Provent)", "",
            0.05m),
        new("Brasil", "Thais (InTec)", "InTec – Engª Thais Werner de Lima por (71) 3289-3611 / (71) 9 9961-9278 ou e-mail: intec@inovacaotecnologia.com.br",
            0.03m),
        new("Brasil", "Wander (Wanseve )", "Wander S. da Silva por (16) 3627-6499 / (16) 9 9228 2928 ou e-mail: wanseve@uol.com.br",
            0.03m),
        new("Brasil", "Adolpho (Atric)", "Adolpho Procópio Rossi Neto por (11) 99976-1952 ou e-mail: rossi@atric.com.br",
            0.05m),
        new("Exterior", "ASESORIA Y EQUIPO < =USD 500K", "Pablo Santamarina por +502 24285468, 24285478, 23658515, 23658669 ou e-mail: aseqsa@gmail.com",
            0.1m),
        new("Exterior", "ASESORIA Y EQUIPO < USD 1MM", "Pablo Santamarina por +502 24285468, 24285478, 23658515, 23658669 ou e-mail: aseqsa@gmail.com",
            0.075m),
        new("Exterior", "ASESORIA Y EQUIPO > USD 1MM", "Pablo Santamarina por +502 24285468, 24285478, 23658515, 23658669 ou e-mail: aseqsa@gmail.com",
            0.05m),
        new("Exterior", "FERRUNION", "Gilmer Vasquez por: +51 1 4754560 ou e-mail: gsvasquez@ferrunion.net",
            0.05m),
        new("Exterior", "H&T", "Jorge Gonzalo Hernández Cabeza por +56 2 29970179 / +56 9 98871135 ou e-mail: jhernandez@ghis.cl",
            0.05m),
        new("Exterior", "HCA", "Angelo Ramirez por +56 9 4478 3695 / +56 2 5725-7371 o e-mail: angelo@hcamineria.cl",
            0.06m),
        new("Exterior", "HRI S.A.", "Rury Harms Orrego por +56 2 2592 3500 ou e-mail: rharms@hri.cl",
            0.05m),
        new("Exterior", "IPT Colômbia < =EUR 1,25MM", "Ricardo Morales Castro por: +57 3125866426 / 3206737171 ou email: rmorales@iptcolombia.com",
            0.035m),
        new("Exterior", "IPT Colômbia < =EUR 1MM", "Ricardo Morales Castro por: +57 3125866426 / 3206737171 ou email: rmorales@iptcolombia.com",
            0.05m),
        new("Exterior", "IPT Colômbia < =EUR 500K", "Ricardo Morales Castro por: +57 3125866426 / 3206737171 ou email: rmorales@iptcolombia.com",
            0.08m),
        new("Exterior", "IPT Colômbia < =EUR 750K", "Ricardo Morales Castro por: +57 3125866426 / 3206737171 ou email: rmorales@iptcolombia.com",
            0.065m),
        new("Exterior", "IPT Colômbia >EUR 1,25MM", "Ricardo Morales Castro por: +57 3125866426 / 3206737171 ou email: rmorales@iptcolombia.com",
            0.03m),
        new("Exterior", "SIMINCO", "Alejandro Cadavid L. por +57 323 460 0551 o e-mail comercial@siminco.com.co o Carlos Contreras U. por +57 311 588 4883 o e-mail coordinadortecnico@siminco.com.co",
            0.05m),
        new("Exterior", "TEJADA", "Luis Felipe Tejada por :+57 315-505-5397 ou e-mail:Tejadaingenieros@tejadaingenieros.com",
            0.05m),
        new("Exterior", "Turbomaquinarias <= EUR 100 k", "Carlos Daniel Weihmuller por email: cweihmuller@turbomaquinarias.com",
            0.04m),
        new("Exterior", "Turbomaquinarias <= EUR 2,5 M", "Carlos Daniel Weihmuller por email: cweihmuller@turbomaquinarias.com",
            0.025m),
        new("Exterior", "Turbomaquinarias <= EUR 500 k", "Carlos Daniel Weihmuller por email: cweihmuller@turbomaquinarias.com",
            0.03m),
        new("Exterior", "Turbomaquinarias <= EUR 7,0 M", "Carlos Daniel Weihmuller por email: cweihmuller@turbomaquinarias.com",
            0.02m),
        new("Exterior", "Turbomaquinarias > EUR 7,0 M", "Carlos Daniel Weihmuller por email: cweihmuller@turbomaquinarias.com",
            0.01m),
    };

    /// <summary>O contato de um representante, pelo nome. Vazio se não achar.</summary>
    public static string ContatoDoRepresentante(string nome) => Representantes
        .FirstOrDefault(r => r.Nome == nome)?.Contato ?? "";

    /// <summary>A comissão de um representante. Sem representante, zero.</summary>
    public static decimal ComissaoDoRepresentante(string nome) => Representantes
        .FirstOrDefault(r => r.Nome == nome)?.Comissao ?? 0m;

    /// <param name="Area">A área dele. Vazia nos que atendem qualquer uma.</param>
    public sealed record ContatoHowden(string Nome, string Cargo, string Area, string Telefones, string Email);

    /// <summary>
    /// Os vendedores que podem assinar a proposta — a lista do quadro
    /// "Contactos Howden" do modelo. A proposta leva <b>até dois</b>.
    ///
    /// O diretor de vendas (<see cref="DiretorDeVendas"/>) não está aqui: ele
    /// sai sempre, ao lado dos escolhidos.
    /// </summary>
    public static readonly ContatoHowden[] Contatos =
    {
        new("André Carvalho", "Key Account", "Pulp & Paper / Steel",
            "+55 11 4487-6279 / +55 11 99452 5152", "andre.carvalho@chartindustries.com"),
        new("Bruno Patricio de Castro", "Key Account Sales", "CCUS & Hydrogen",
            "+55 11 4487-6250 / +55 11 9 8787 1188", "bruno.castro@chartindustries.com"),
        new("Douglas M. Matavelli", "Key Account Manager", "Water",
            "+55 11 97144 3085", "douglas.matavelli@chartindustries.com"),
        new("Emerson Barbosa", "Key Account Manager", "Oil & Gas",
            "+55 11 99959 7724", "emerson.barbosa@chartindustries.com"),
        new("José Ovídio Moura", "Key Account", "Cement",
            "+55 11 9 6858 2183", "jose.moura@chartindustries.com"),
        new("José Carlos L. Pereira", "Key Account", "Tunnel / Metro",
            "+55 11 99443 0060", "Jose.pereira@chartindustries.com"),
        new("Paulo Agostinho", "Key Account", "Fertilizer / Ethanol / Aluminum / Refrigeration",
            "+55 11 98145 1988", "paulo.agostinho@chartindustries.com"),
        new("Rafael Ribeiro de Toledo", "Key Account Manager", "Metal Processing & Power Plants",
            "+56 9 8285 9721 (Chile) / +55 11 9 7183 3687 (Brazil)", "rafael.toledo@chartindustries.com"),
        new("Thiago César Veiga", "Key Account Manager", "UG Mining",
            "+55 11 4487 6250 / +55 11 97144 3083", "thiago.veiga@chartindustries.com"),
        new("Elmer Calle Chumacero", "Sales Engineer", "",
            "+51 989012650", "elmer.calle@chartindustries.com"),
        new("Manuel Gutierrez", "Sales Engineer", "",
            "+51 951175126", "manuel.gutierrez@chartindustries.com"),
        new("Daniel Nuñez", "Sales Engineer", "",
            "+51 953887645", "daniel.nunez@chartindustries.com"),
        new("Rodrigo Ugas", "Sales Engineer", "",
            "+56 2 3275 3400 / +56 9 3388 5096", "rodrigo.ugas@chartindustries.com"),
        new("Manuel Gajardo", "Sales Engineer", "",
            "+56 9 2644 2972", "manuel.gajardo@chartindustries.com"),
        new("Wagner Ortíz", "Sales Engineer", "",
            "+51 972 459 337", "wagner.ortiz@chartindustries.com"),
    };

    /// <summary>Acha um contato pelo nome. Nulo se ele não está mais na lista.</summary>
    public static ContatoHowden? Contato(string nome) =>
        Contatos.FirstOrDefault(c => Textos.Igual(c.Nome, nome));

    /// <summary>
    /// O diretor de vendas, que sai em toda proposta — está fixo no quadro de
    /// contatos do modelo, ao lado dos dois escolhidos.
    /// </summary>
    public static readonly ContatoHowden DiretorDeVendas = new(
        "Edson Luis Geraldini", "Director de Ventas", "",
        "+55 11 4487 6252 / +55 11 98193-6392", "edson.geraldini@chartindustries.com");

    /// <summary>
    /// O endereço de cada BU, como sai na proposta. A proposta imprime só o da
    /// BU escolhida — no modelo em branco os quatro aparecem grifados, e a
    /// escolha apaga os outros três.
    /// </summary>
    public static readonly Dictionary<string, string> EnderecosDasBus = new()
    {
        ["HSA-SP"] =
            "Howden South America Ventiladores e Compressores Indústria e Comércio Ltda,\n" +
            "Av. Osvaldo Berto, 475, Distrito Industrial Alfredo Rela, 13255-405 – Itatiba - SP,\n" +
            "Brasil.",
        ["HSA-ES"] =
            "Howden South America Ventiladores e Compressores Indústria e Comércio Ltda.\n" +
            "Rua 4E, 135 - Bairro Civit II – Distrito de Carapina, 29168-082 – Município de Serra –\n" +
            "ES, Brasil.",
        ["HCHL"] =
            "Howden Chile SpA,\n" +
            "Calle Cordillera 575 Pudahuel, Código Postal:903 1167, Santiago, Chile",
        ["HPU"] =
            "Howden Perú SRL\n" +
            "Calle Guillermo Marconi, 368 Oficina 301 – San Isidro –CP 150131\n" +
            "Lima- Peru.",
    };

    /// <summary>O endereço da BU escolhida; o de Itatiba quando a BU não é conhecida.</summary>
    public static string EnderecoDaBu(string bu) =>
        EnderecosDasBus.TryGetValue(CodigoDaBu(bu), out var e) ? e : EnderecosDasBus["HSA-SP"];

    // ---------------- entrega ----------------

    /// <param name="Destino">
    /// true quando o incoterm precisa que alguém diga o lugar — a proposta
    /// escreve "Puerto de DESTINO (PAÍS DE DESTINO)" e esse pedaço é nosso.
    /// </param>
    public sealed record IncotermDaProposta(string Codigo, string Texto, bool Destino);

    /// <summary>
    /// Os incoterms do modelo (INCOTERMS 2020). A proposta imprime a linha do
    /// escolhido e apaga as outras cinco; quem precisar de outro digita.
    /// </summary>
    public static readonly IncotermDaProposta[] Incoterms =
    {
        new("EXW", "Fábrica Howden en Itatiba (SP, Brasil) o sub-proveedor en la región de São Paulo (SP, Brasil).", false),
        new("FCA", "Fábrica Howden en Itatiba (SP, Brasil) o sub-proveedor en la región de São Paulo (SP, Brasil).", false),
        new("FOB", "Puerto de Santos (SP, Brasil).", false),
        new("CIF", "Puerto de {0}.", true),
        new("DAP", "UBICACIÓN de {0}.", true),
        new("DDP", "UBICACIÓN de {0}.", true),
    };

    /// <summary>
    /// A linha da entrega como ela sai na proposta: "EXW – Fábrica Howden…".
    ///
    /// Fora da lista, o que a equipe digitou sai inteiro — é por isso que dá
    /// para digitar: nem toda venda cabe nos seis do modelo.
    /// </summary>
    public static string LinhaDoIncoterm(string incoterm, string destino)
    {
        var escolhido = Incoterms.FirstOrDefault(i => Textos.Igual(i.Codigo, incoterm));
        if (escolhido is null) return incoterm.Trim();

        var texto = escolhido.Destino
            ? string.Format(escolhido.Texto,
                SoOLugar(destino) is { Length: > 0 } lugar ? lugar : "DESTINO (PAÍS DE DESTINO)")
            : escolhido.Texto;

        return $"{escolhido.Codigo} – {texto}";
    }

    /// <summary>
    /// O lugar sem o "Puerto de" / "UBICACIÓN de" que a frase do modelo já
    /// traz — digitado de novo, a proposta sairia com "Puerto de Puerto de
    /// Valparaíso". Só o começo exato é cortado, para não mutilar um lugar que
    /// se chame assim (Puerto Montt continua inteiro).
    /// </summary>
    public static string SoOLugar(string destino)
    {
        var texto = destino.Trim();

        foreach (var repetido in new[]
                 {
                     "puerto de ", "ubicación de ", "ubicacion de ",
                     "porto de ", "local de ", "port of ", "location of ",
                 })
            if (texto.StartsWith(repetido, StringComparison.OrdinalIgnoreCase))
                return texto[repetido.Length..].Trim();

        return texto;
    }

    /// <summary>
    /// Armado ou desarmado, com a descrição que sai logo abaixo do incoterm.
    /// </summary>
    public static readonly (string Codigo, string Texto)[] Armados =
    {
        ("Armado",
         "Equipo armado en base de acero carbono, base común para ventilador y motor eléctrico. " +
         "El montaje del equipo en sitio no está incluido en este suministro."),
        ("Desarmado",
         "Equipo desarmado, en parte estática y girante separadas. " +
         "El montaje del equipo en sitio no está incluido en este suministro."),
    };

    /// <summary>A descrição do armado escolhido; vazia quando nada foi escolhido.</summary>
    public static string TextoDoArmado(string armado) => Armados
        .FirstOrDefault(a => Textos.Igual(a.Codigo, armado)).Texto ?? "";
}

/// <summary>
/// As propostas (entidade "propostas"), sobre o mesmo ParquetStore do resto do
/// sistema.
/// </summary>
public sealed class PropostaRepository
{
    private const string Entidade = "propostas";

    private static readonly string[] Campos =
    {
        "id", "cliente", "aosCuidados", "pais", "email", "telefone", "projeto",
        "data", "dataFechamento", "fase", "preparadaPor", "estado",
        "validadeDias", "prazoEntregaDias",
        "ano", "numero", "revisao", "bu", "idioma", "vendaPara", "destino",
        "paisDestino", "categoria", "produto", "marketSegment", "portal",
        "contatoNome", "contatoCargo", "contatoEmail", "contatoTelefones",
        "representante", "representanteContato", "representante2", "representante2Contato",
        "beneficio", "fianca",
        "riscoAdicional", "margemNegociacao", "modoDoPreco", "margemAlvo", "precoMeta",
        "enviadaEm", "criadaEm",
        "moeda", "equipamento", "arranjo", "escolhas", "itens",
        // colunas novas entram sempre NO FIM: o esquema do Parquet é por
        // arquivo, e é a leitura por nome que faz o arquivo velho devolver
        // vazio nelas em vez de errar
        "cidade", "referenciaCliente",
        "condicaoPagamento", "incoterm", "incotermDestino", "armado",
        "contatoArea",
        "contato2Nome", "contato2Cargo", "contato2Area", "contato2Email", "contato2Telefones",
        "diasDeAssessoria", "diasDeAssessoriaEmCampo",
    };

    private readonly ParquetStore _store;
    public PropostaRepository(ParquetStore store) => _store = store;

    public List<Proposta> Todas() => _store
        .ReadLatest(Entidade, string.Join(", ", Campos), r => new Proposta
        {
            Id = S(r, 0), Cliente = S(r, 1), AosCuidados = S(r, 2), Pais = S(r, 3),
            Email = S(r, 4), Telefone = S(r, 5), Projeto = S(r, 6),
            Data = S(r, 7), DataFechamento = S(r, 8), Fase = S(r, 9),
            PreparadaPor = S(r, 10), Estado = S(r, 11),
            ValidadeDias = S(r, 12), PrazoEntregaDias = S(r, 13),
            Ano = S(r, 14), Numero = S(r, 15), Revisao = S(r, 16), Bu = S(r, 17),
            Idioma = S(r, 18), VendaPara = S(r, 19), Destino = S(r, 20),
            PaisDestino = S(r, 21), Categoria = S(r, 22), Produto = S(r, 23),
            MarketSegment = S(r, 24), Portal = S(r, 25),
            ContatoNome = S(r, 26), ContatoCargo = S(r, 27),
            ContatoEmail = S(r, 28), ContatoTelefones = S(r, 29),
            Representante = S(r, 30), RepresentanteContato = S(r, 31),
            Representante2 = S(r, 32), Representante2Contato = S(r, 33),
            Beneficio = S(r, 34), Fianca = Ou(S(r, 35), "Não"),
            RiscoAdicional = Ou(S(r, 36), "2"), MargemNegociacao = Ou(S(r, 37), "3"),
            ModoDoPreco = Ou(S(r, 38), "margem"), MargemAlvo = Ou(S(r, 39), "28"),
            PrecoMeta = S(r, 40),
            EnviadaEm = S(r, 41), CriadaEm = S(r, 42),
            MoedaCodigo = S(r, 43),
            Itens = Proposta.LerItens(S(r, 47), S(r, 44), S(r, 45), S(r, 46)),
            Cidade = S(r, 48), ReferenciaCliente = S(r, 49),
            CondicaoPagamento = S(r, 50), Incoterm = S(r, 51),
            IncotermDestino = S(r, 52), Armado = S(r, 53),
            ContatoArea = S(r, 54),
            Contato2Nome = S(r, 55), Contato2Cargo = S(r, 56), Contato2Area = S(r, 57),
            Contato2Email = S(r, 58), Contato2Telefones = S(r, 59),
            DiasDeAssessoria = S(r, 60), DiasDeAssessoriaEmCampo = S(r, 61),
        })
        .OrderByDescending(p => p.Numero)
        .ToList();

    public Proposta? Achar(string id) => Todas().FirstOrDefault(p => p.Id == id);

    public void Salvar(Proposta p)
    {
        if (string.IsNullOrWhiteSpace(p.Id)) p.Id = Guid.NewGuid().ToString("n");
        if (string.IsNullOrWhiteSpace(p.CriadaEm)) p.CriadaEm = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        if (string.IsNullOrWhiteSpace(p.Numero)) p.Numero = ProximoNumero(p.Ano);

        var valores = new object?[]
        {
            p.Id, p.Cliente, p.AosCuidados, p.Pais, p.Email, p.Telefone, p.Projeto,
            p.Data, p.DataFechamento, p.Fase, p.PreparadaPor, p.Estado,
            p.ValidadeDias, p.PrazoEntregaDias,
            p.Ano, p.Numero, p.Revisao, p.Bu, p.Idioma, p.VendaPara, p.Destino,
            p.PaisDestino, p.Categoria, p.Produto, p.MarketSegment, p.Portal,
            p.ContatoNome, p.ContatoCargo, p.ContatoEmail, p.ContatoTelefones,
            p.Representante, p.RepresentanteContato,
            p.Representante2, p.Representante2Contato,
            p.Beneficio, p.Fianca,
            p.RiscoAdicional, p.MargemNegociacao, p.ModoDoPreco, p.MargemAlvo, p.PrecoMeta,
            p.EnviadaEm, p.CriadaEm,
            // as três colunas do formato antigo continuam sendo gravadas vazias:
            // o esquema do Parquet é por arquivo, e tirá-las não apagaria as que
            // já estão lá
            p.MoedaCodigo, "", "", "", p.ItensComoTexto(),
            p.Cidade, p.ReferenciaCliente,
            p.CondicaoPagamento, p.Incoterm, p.IncotermDestino, p.Armado,
            p.ContatoArea,
            p.Contato2Nome, p.Contato2Cargo, p.Contato2Area,
            p.Contato2Email, p.Contato2Telefones,
            p.DiasDeAssessoria, p.DiasDeAssessoriaEmCampo,
        };

        _store.WriteRow(Entidade, Campos
            .Select((c, i) => new KeyValuePair<string, object?>(c, valores[i]))
            .ToArray());
    }

    public void Apagar(string id) => _store.WriteRow(Entidade,
        new KeyValuePair<string, object?>[] { new("id", id) }, deleted: true);

    /// <summary>
    /// O próximo número do ano: PRP-2026-001, -002… A contagem olha o que já
    /// está gravado, então dois usuários gravando ao mesmo tempo podem pedir o
    /// mesmo número — a equipe corrige à mão, e isso é melhor do que travar a
    /// gravação numa pasta de rede.
    /// </summary>
    public string ProximoNumero(string ano)
    {
        var doAno = ano.Trim().Length > 0 ? ano.Trim() : DateTime.Now.Year.ToString();
        var prefixo = $"PRP-{doAno}-";

        var ultimo = Todas()
            .Select(p => p.Numero)
            .Where(n => n.StartsWith(prefixo, StringComparison.OrdinalIgnoreCase))
            .Select(n => int.TryParse(n[prefixo.Length..], out var v) ? v : 0)
            .DefaultIfEmpty(0)
            .Max();

        return prefixo + (ultimo + 1).ToString("D3");
    }

    private static string S(System.Data.IDataReader r, int i) => r.IsDBNull(i) ? "" : r.GetString(i);

    /// <summary>O que veio do banco, ou o padrão quando a coluna é nova e está vazia.</summary>
    private static string Ou(string valor, string padrao) => valor.Length > 0 ? valor : padrao;
}
