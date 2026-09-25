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
    public string Projeto { get; set; } = "";
    public string Data { get; set; } = "";
    public string DataFechamento { get; set; } = "";
    public string Fase { get; set; } = "";
    public string PreparadaPor { get; set; } = "Equipe Howden";
    public string Estado { get; set; } = "";
    public string ValidadeDias { get; set; } = "30";
    public string PrazoEntregaDias { get; set; } = "12";

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

    // ---------- contato que sai no documento ----------
    public string ContatoNome { get; set; } = "";
    public string ContatoCargo { get; set; } = "";
    public string ContatoEmail { get; set; } = "";
    public string ContatoTelefones { get; set; } = "";

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

    public static readonly string[] Bus =
        { "HSA — Itatiba (Brasil)", "HSA — Santiago (Chile)", "HSA — Lima (Peru)" };

    public static readonly string[] Idiomas = { "Português", "Espanhol", "Inglês" };

    public static readonly string[] VendaPara = { "Cliente Final", "EPC", "Distribuidor", "Interno" };

    public static readonly string[] Destinos = { "Nacional", "Exportação" };

    public static readonly string[] Categorias = { "Other", "Projeto", "Reposição", "Serviço" };

    public static readonly string[] Produtos = { "ROTH1 — Ventsim Software", "Ventilador axial", "Outro" };

    public static readonly string[] Segmentos =
        { "Mining", "Tunnel", "Power", "Industrial", "Oil & Gas", "Outro" };

    public static readonly string[] Portais = { "Nenhum", "Ariba", "Coupa", "SAP", "Outro" };

    public static readonly string[] Arranjos = { "Teto", "Piso" };

    /// <summary>Os contatos que assinam a proposta.</summary>
    public static readonly (string Nome, string Cargo, string Email, string Telefones)[] Contatos =
    {
        ("Rodrigo Ugas", "Key Account Manager", "Rodrigo.Ugas@chartindustries.com",
         "+56 9 3947 3380 / +56 2 3275 3400"),
    };
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
        "moeda", "equipamento", "arranjo", "escolhas", "itens",
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
            MoedaCodigo = S(r, 30),
            Itens = Proposta.LerItens(S(r, 34), S(r, 31), S(r, 32), S(r, 33)),
        })
        .OrderByDescending(p => p.Numero)
        .ToList();

    public Proposta? Achar(string id) => Todas().FirstOrDefault(p => p.Id == id);

    public void Salvar(Proposta p)
    {
        if (string.IsNullOrWhiteSpace(p.Id)) p.Id = Guid.NewGuid().ToString("n");
        if (string.IsNullOrWhiteSpace(p.Numero)) p.Numero = ProximoNumero(p.Ano);

        var valores = new object?[]
        {
            p.Id, p.Cliente, p.AosCuidados, p.Pais, p.Email, p.Telefone, p.Projeto,
            p.Data, p.DataFechamento, p.Fase, p.PreparadaPor, p.Estado,
            p.ValidadeDias, p.PrazoEntregaDias,
            p.Ano, p.Numero, p.Revisao, p.Bu, p.Idioma, p.VendaPara, p.Destino,
            p.PaisDestino, p.Categoria, p.Produto, p.MarketSegment, p.Portal,
            p.ContatoNome, p.ContatoCargo, p.ContatoEmail, p.ContatoTelefones,
            // as três colunas do formato antigo continuam sendo gravadas vazias:
            // o esquema do Parquet é por arquivo, e tirá-las não apagaria as que
            // já estão lá
            p.MoedaCodigo, "", "", "", p.ItensComoTexto(),
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
}
