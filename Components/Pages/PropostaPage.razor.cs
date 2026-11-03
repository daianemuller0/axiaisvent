using System.Text.Json;
using System.Text.RegularExpressions;
using HowdenAxiais.Poc.Data;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;

namespace HowdenAxiais.Poc.Components.Pages;

/// <summary>
/// A proposta em cinco etapas — Começar, Cliente e condições, Equipamentos,
/// Preço e Documentos —, com um painel fixo ao lado (números, pendências,
/// Salvar e Gerar tudo).
///
/// A página era uma coluna só com doze caixas abertas, e montar uma proposta
/// era rolar atrás da próxima caixa, digitar de novo o que a última proposta
/// do cliente já sabia e descobrir no Word o que tinha faltado. O que mora
/// aqui é o que encurta isso: as etapas, as pendências que levam ao campo, o
/// Gerar tudo, a cópia de proposta, a lembrança do cliente e dos padrões de
/// quem está logado, a taxa do dia, a importação de todos os arquivos de uma
/// vez, a sugestão do motor pela seleção, o escopo padrão por série e o
/// cronômetro que mede o tempo de cada proposta.
/// </summary>
public partial class PropostaPage : IAsyncDisposable
{
    [Inject] private ConversaoRepository Conversao { get; set; } = default!;
    [Inject] private TempoRepository Tempos { get; set; } = default!;
    [Inject] private EscopoPadraoRepository EscoposPadrao { get; set; } = default!;

    [CascadingParameter] private Task<AuthenticationState>? Autenticacao { get; set; }

    /// <summary>O nome de quem está logado — vira o "Preparada por" e acha o contato Howden.</summary>
    private string _usuario = "";

    protected override async Task OnInitializedAsync()
    {
        if (Autenticacao is not null)
            _usuario = (await Autenticacao).User.Identity?.Name?.Trim() ?? "";

        Recarregar();
        _proposta = NovaComPadroes();
        _padraoDaNova = Foto();
        _baseSalva = _padraoDaNova;
        _proximoNumero = Repo.ProximoNumero(_proposta.Ano);
    }

    // =====================================================================
    // etapas
    // =====================================================================

    private static readonly (int Numero, string Nome)[] Etapas =
    {
        (1, "Começar"), (2, "Cliente e condições"), (3, "Equipamentos"), (4, "Preço"),
        (5, "Documentos"),
    };

    private int _etapa = 1;

    /// <summary>A aba do equipamento aberto: "escopo", "opcionais" ou "tecnica".</summary>
    private string _abaItem = "escopo";

    /// <summary>Trocou de etapa: depois de desenhar, a tela volta ao topo.</summary>
    private bool _rolarTopo;

    /// <summary>Um elemento para onde rolar depois de desenhar (ex.: o custo previsto).</summary>
    private string? _rolarPara;

    /// <summary>Mostra os campos que quase nunca mudam (ano, BU, fase…).</summary>
    private bool _maisCampos;

    /// <summary>
    /// Os textos de ajuda escondidos. Começa assim: no dia a dia eles só
    /// aumentam o que o olho precisa varrer — quem quer, liga no "? Mostrar
    /// ajuda", e a escolha fica lembrada no navegador.
    /// </summary>
    private bool _compacto = true;

    /// <summary>O próximo número, para a dica do campo — calculado ao recarregar, e não a cada desenho.</summary>
    private string _proximoNumero = "";

    private static string NomeDaEtapa(int n) => Etapas.FirstOrDefault(e => e.Numero == n).Nome ?? "";

    private void IrParaEtapa(int n)
    {
        _etapa = Math.Clamp(n, 1, Etapas.Length);
        _rolarTopo = true;
    }

    private void AbrirEquipamento(int indice)
    {
        _aberto = indice;
        _apagandoItem = null;
    }

    private async Task Rolar()
    {
        try
        {
            if (_rolarTopo)
            {
                _rolarTopo = false;
                await JS.InvokeVoidAsync("appTopo");
            }

            if (_rolarPara is { } alvo)
            {
                _rolarPara = null;
                await JS.InvokeVoidAsync("appRolarPara", alvo);
            }
        }
        catch (Exception)
        {
            // rolar é conforto: nunca derruba a tela
        }
    }

    private async Task AlternarAjuda()
    {
        _compacto = !_compacto;
        try
        {
            await JS.InvokeVoidAsync("appPreferencia.gravar", "ajuda", _compacto ? "esconder" : "mostrar");
        }
        catch (Exception)
        {
            // sem guardar a escolha, ela vale só até fechar a tela
        }
    }

    // =====================================================================
    // pendências
    // =====================================================================

    /// <summary>
    /// Uma coisa que falta para os documentos saírem completos, e onde ela se
    /// resolve: a etapa e, quando é de um equipamento, qual e em que aba.
    /// </summary>
    private sealed record Pendencia(string Texto, int Etapa, int Item = -1, string Aba = "");

    private void IrPara(Pendencia p)
    {
        IrParaEtapa(p.Etapa);
        if (p.Item >= 0) _aberto = p.Item;
        if (p.Aba.Length > 0) _abaItem = p.Aba;
    }

    /// <summary>
    /// Tudo o que os documentos deixariam em branco, de todas as etapas. Não
    /// impede de gerar — muita proposta sai para conferência antes de fechar —,
    /// mas fica à vista o tempo todo, em vez de aparecer só ao abrir o Word.
    /// </summary>
    private List<Pendencia> Pendencias()
    {
        var lista = new List<Pendencia>();

        void Falta(bool falta, string texto, int etapa)
        {
            if (falta) lista.Add(new Pendencia(texto, etapa));
        }

        Falta(_proposta.Cliente.Trim().Length == 0, "Cliente", 2);
        Falta(_proposta.AosCuidados.Trim().Length == 0, "Aos cuidados de", 2);
        Falta(_proposta.Cidade.Trim().Length == 0, "Cidade do cliente", 2);
        Falta(EhBrasil && _proposta.Estado.Trim().Length == 0, "Estado / UF (ICMS do pricing)", 2);
        Falta(_proposta.CondicaoPagamento.Trim().Length == 0, "Condição de pagamento", 2);
        Falta(_proposta.Incoterm.Trim().Length == 0, "Entrega (incoterm)", 2);
        Falta(_proposta.Incoterm.Trim().Length > 0 && !_incotermLivre && PedeDestino
              && _proposta.IncotermDestino.Trim().Length == 0, RotuloDoDestino, 2);
        Falta(_proposta.Armado.Trim().Length == 0, "Armado ou desarmado", 2);
        Falta(_proposta.ContatoNome.Trim().Length == 0, "Contato Howden", 2);

        Falta(_proposta.Itens.Count == 0, "Nenhum equipamento no escopo", 3);
        for (var i = 0; i < _proposta.Itens.Count; i++)
        {
            var item = _proposta.Itens[i];
            var nome = $"Equip. {i + 1}";

            if (ModeloDe(item) is null)
            {
                lista.Add(new Pendencia($"{nome}: escolher o modelo", 3, i, "escopo"));
                continue;
            }

            if (item.ComMotor && MotorDe(item) is null)
                lista.Add(new Pendencia($"{nome}: motor ainda não definido", 3, i, "escopo"));

            if (item.ComPartidor && item.Partidor.Trim().Length == 0)
                lista.Add(new Pendencia($"{nome}: escolher o partidor", 3, i, "escopo"));

            var semCusto = LinhasDePreco(item).Count(l => Efetivo(item, l.Chave, l.Puxado) is null);
            if (semCusto > 0)
                lista.Add(new Pendencia($"{nome}: {semCusto} linha(s) sem custo", 3, i, "escopo"));

            if (item.Tecnicos.Count == 0)
                lista.Add(new Pendencia($"{nome}: dados da seleção", 3, i, "tecnica"));

            if (item.Curvas.Count == 0)
                lista.Add(new Pendencia($"{nome}: curva de performance", 3, i, "tecnica"));
        }

        Falta(Cambio.FaltaTaxa(_proposta), "Taxa de câmbio", 4);

        return lista;
    }

    /// <summary>Há o que gravar: o que está na tela é diferente do que está no banco.</summary>
    private bool TemAlteracao => Foto() != _baseSalva;

    private static bool MargemForaDoPadrao(CalculoPricing.Resultado p) =>
        Math.Abs(p.Margem - CalculoPricing.MargemAlvoPadrao) > 0.0005m;

    /// <summary>Os mesmos três avisos da etapa Preço, juntos: algum deles pede aprovação.</summary>
    private bool PrecisaAprovacao(CalculoPricing.Resultado p) =>
        MargemForaDoPadrao(p)
        || Math.Abs(Risco - CalculoPricing.RiscoAdicionalPadrao) > 0.00001m
        || Math.Abs(Negociacao - CalculoPricing.MargemNegociacaoPadrao) > 0.00001m;

    // =====================================================================
    // salvar, salvar e continuar, Ctrl+S, gerar tudo
    // =====================================================================

    private DotNetObjectReference<PropostaPage>? _ref;

    private void SalvarEContinuar()
    {
        if (Salvar()) IrParaEtapa(_etapa + 1);
    }

    /// <summary>Ctrl+S, de qualquer etapa (ver appAtalhos no app.js).</summary>
    [JSInvokable]
    public Task SalvarPeloTeclado() => InvokeAsync(() =>
    {
        Salvar();
        StateHasChanged();
    });

    /// <summary>Liga o Ctrl+S e lê a preferência de ajuda. Sem os dois, a tela funciona igual.</summary>
    private async Task LigarAtalhos()
    {
        try
        {
            _ref ??= DotNetObjectReference.Create(this);
            await JS.InvokeVoidAsync("appAtalhos.ligar", _ref);

            var ajuda = await JS.InvokeAsync<string?>("appPreferencia.ler", "ajuda");
            if (ajuda == "mostrar")
            {
                _compacto = false;
                StateHasChanged();
            }
        }
        catch (Exception)
        {
            // navegador sem armazenamento ou circuito caindo: segue sem atalho
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await JS.InvokeVoidAsync("appAtalhos.desligar");
        }
        catch (Exception)
        {
            // a aba já fechou: não há o que desligar
        }

        _ref?.Dispose();
    }

    /// <summary>Gerando os três de uma vez: o tempo vai uma vez só, como "gerar-tudo".</summary>
    private bool _gerandoTudo;

    /// <summary>
    /// Salva e baixa a proposta comercial, a técnica e o pricing, um depois do
    /// outro. O que não sair é contado no aviso — os outros saem mesmo assim.
    /// </summary>
    private async Task GerarTudo()
    {
        if (_proposta.Itens.Count == 0)
        {
            Aviso("A proposta ainda não tem equipamento nenhum.", erro: true);
            return;
        }

        if (!Salvar()) return;

        var feitos = new List<string>();
        var falhas = new List<string>();
        _gerandoTudo = true;

        try
        {
            async Task Um(Func<Task<bool>> gerar, string nome)
            {
                if (await gerar()) feitos.Add(nome);
                else falhas.Add($"{nome} ({_msg})");

                // um respiro entre um download e outro: o navegador descarta os
                // que chegam colados
                await Task.Delay(450);
            }

            await Um(GerarProposta, "comercial");
            await Um(GerarTecnica, "técnica");
            await Um(GerarPricing, "pricing");
        }
        finally
        {
            _gerandoTudo = false;
        }

        if (feitos.Count > 0) RegistrarTempo("gerar-tudo");

        if (falhas.Count == 0)
        {
            Aviso($"Proposta {_proposta.Numero} salva e documentos baixados: {string.Join(", ", feitos)}.");
            return;
        }

        Aviso((feitos.Count > 0 ? $"Baixados: {string.Join(", ", feitos)}. " : "")
              + $"Não saiu: {string.Join(" · ", falhas)}", erro: true);
    }

    // =====================================================================
    // começar: nova, abrir, copiar
    // =====================================================================

    private string _buscaGravadas = "";

    /// <summary>As gravadas que casam com a busca, as mais recentes primeiro.</summary>
    private IEnumerable<Proposta> GravadasDaBusca()
    {
        var termos = Textos.Simples(_buscaGravadas).Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return _gravadas
            .Where(p => termos.All(t => Textos.Simples($"{p.Numero} {p.Cliente} {p.Projeto}").Contains(t)))
            .OrderByDescending(p => p.CriadaEm)
            .ThenByDescending(p => p.Numero)
            .Take(8);
    }

    private void NovaEmBranco()
    {
        Nova();
        IrParaEtapa(1);
    }

    private void NovaEContinuar()
    {
        Nova();
        IrParaEtapa(2);
    }

    private void AbrirGravada(string id)
    {
        Abrir(id);
        if (_proposta.Id == id) IrParaEtapa(2);
    }

    private void DuplicarGravada(string id)
    {
        Abrir(id);
        if (_proposta.Id == id) Duplicar();
    }

    /// <summary>
    /// Uma proposta nova a partir da aberta: cliente, condições, equipamentos,
    /// escopo e preços à mão vêm junto; número, revisão, datas, referência do
    /// cliente e projeto começam de novo. A seleção e a curva de cada
    /// equipamento ficam de fora — são de outro projeto, e os arquivos moram na
    /// pasta da proposta antiga.
    /// </summary>
    private void Duplicar()
    {
        if (_proposta.Id.Length == 0)
        {
            Aviso("Esta proposta ainda não foi gravada: é só continuar nela.", erro: true);
            return;
        }

        var origem = _proposta.Numero;
        var copia = JsonSerializer.Deserialize<Proposta>(Foto());
        if (copia is null) return;

        copia.Id = "";
        copia.Numero = "";
        copia.Revisao = "00";
        copia.CriadaEm = "";
        copia.EnviadaEm = "";
        copia.Ano = DateTime.Now.Year.ToString();
        copia.Data = DateTime.Now.ToString("yyyy-MM-dd");
        copia.DataFechamento = "";
        copia.ReferenciaCliente = "";
        copia.Projeto = "";

        foreach (var item in copia.Itens)
        {
            item.Curvas = new();
            item.Tecnicos = new();
            item.ArquivoDaSelecao = "";
            item.Curva = "";
            item.CurvaNome = "";
            item.CurvaImagem = "";
            item.Coluna = "";
        }

        _proposta = copia;
        _revisaoAberta = "";
        // a cópia ainda não está no banco: tudo nela é alteração a gravar, e o
        // rascunho do navegador já a protege
        _baseSalva = "";
        _padraoDaNova = "";
        _sugestaoCliente = null;
        _faltouNaSelecao.Clear();
        _aberto = 0;
        _abaItem = "escopo";
        _apagando = false;
        _apagandoItem = null;
        AjustarEscolhasLivres();
        ZerarCronometro();
        IrParaEtapa(2);

        Aviso($"Cópia da {origem}: confira referência, projeto e data de fechamento. A seleção e a " +
              "curva de cada equipamento ficaram de fora — suba as desta proposta (etapa Começar ou " +
              "aba Técnica do equipamento).");
    }

    /// <summary>
    /// Uma proposta em branco, já com os padrões de quem está logado: ele é o
    /// "Preparada por" e, quando está no quadro, o contato Howden; da última
    /// proposta dele vêm BU, idioma, categoria, product, segmento, validade,
    /// prazo e moeda do custo — quem trabalha com um mercado só não escolhe
    /// isso a cada proposta.
    /// </summary>
    private Proposta NovaComPadroes()
    {
        var p = Nascer();

        if (_usuario.Length > 0 && !Textos.Igual(_usuario, "Equipe Howden")) p.PreparadaPor = _usuario;

        if (ListasDaProposta.Contato(_usuario) is { } eu)
        {
            p.ContatoNome = eu.Nome;
            p.ContatoCargo = eu.Cargo;
            p.ContatoArea = eu.Area;
            p.ContatoEmail = eu.Email;
            p.ContatoTelefones = eu.Telefones;
        }

        var minha = _gravadas
            .Where(x => Textos.Igual(x.PreparadaPor, p.PreparadaPor))
            .OrderByDescending(x => x.CriadaEm)
            .FirstOrDefault();

        if (minha is null) return p;

        p.Bu = Ou(minha.Bu, p.Bu);
        p.Idioma = Ou(minha.Idioma, p.Idioma);
        p.Categoria = Ou(minha.Categoria, p.Categoria);
        p.Produto = Ou(minha.Produto, p.Produto);
        p.MarketSegment = Ou(minha.MarketSegment, p.MarketSegment);
        p.ValidadeDias = Ou(minha.ValidadeDias, p.ValidadeDias);
        p.PrazoEntregaDias = Ou(minha.PrazoEntregaDias, p.PrazoEntregaDias);
        p.MoedaCodigo = Ou(minha.MoedaCodigo, p.MoedaCodigo);

        if (p.ContatoNome.Length == 0)
        {
            p.ContatoNome = minha.ContatoNome;
            p.ContatoCargo = minha.ContatoCargo;
            p.ContatoArea = minha.ContatoArea;
            p.ContatoEmail = minha.ContatoEmail;
            p.ContatoTelefones = minha.ContatoTelefones;
        }

        return p;
    }

    private static string Ou(string valor, string padrao) => valor.Trim().Length > 0 ? valor : padrao;

    // =====================================================================
    // o cliente que se lembra
    // =====================================================================

    /// <summary>Um cliente que já tem proposta gravada, com a mais recente dele.</summary>
    private sealed record ClienteConhecido(string Nome, Proposta Ultima, int Propostas);

    /// <summary>O aviso abaixo do cliente: o que foi trazido (com desfazer) ou o que dá para trazer.</summary>
    private sealed record SugestaoCliente(string Texto, bool Aplicada, ClienteConhecido Cliente);

    private List<ClienteConhecido> _clientes = new();
    private SugestaoCliente? _sugestaoCliente;

    /// <summary>A proposta antes de trazer os dados do cliente — é o "desfazer".</summary>
    private string? _antesDoCliente;

    /// <summary>
    /// A proposta nova como nasceu (com os padrões). Um campo ainda igual a ela
    /// é um campo que ninguém escolheu, e a lembrança do cliente pode preencher.
    /// </summary>
    private string _padraoDaNova = "";

    /// <summary>
    /// O que é do CLIENTE, e não da proposta: contato, onde fica, como compra.
    /// "Junto" leva o que acompanha o campo (o contato do representante, a
    /// direção do câmbio) para nunca misturar um com o outro de outra proposta.
    /// </summary>
    private static readonly (string Nome, Func<Proposta, string> Ler, Action<Proposta, string> Gravar,
        Action<Proposta, Proposta>? Junto)[] CamposDoCliente =
    {
        ("contato", p => p.AosCuidados, (p, v) => p.AosCuidados = v, null),
        ("e-mail", p => p.Email, (p, v) => p.Email = v, null),
        ("telefone", p => p.Telefone, (p, v) => p.Telefone = v, null),
        ("cidade", p => p.Cidade, (p, v) => p.Cidade = v, null),
        ("país", p => p.Pais, (p, v) => p.Pais = v, null),
        ("estado", p => p.Estado, (p, v) => p.Estado = v, null),
        ("BU", p => p.Bu, (p, v) => p.Bu = v, null),
        ("idioma", p => p.Idioma, (p, v) => p.Idioma = v, null),
        ("venda para", p => p.VendaPara, (p, v) => p.VendaPara = v, null),
        ("destino", p => p.Destino, (p, v) => p.Destino = v, null),
        ("país de destino", p => p.PaisDestino, (p, v) => p.PaisDestino = v, null),
        ("portal", p => p.Portal, (p, v) => p.Portal = v, null),
        ("representante", p => p.Representante, (p, v) => p.Representante = v,
            (de, para) => para.RepresentanteContato = de.RepresentanteContato),
        ("representante 2", p => p.Representante2, (p, v) => p.Representante2 = v,
            (de, para) => para.Representante2Contato = de.Representante2Contato),
        ("fiança", p => p.Fianca, (p, v) => p.Fianca = v, null),
        ("benefício", p => p.Beneficio, (p, v) => p.Beneficio = v, null),
        ("pagamento", p => p.CondicaoPagamento, (p, v) => p.CondicaoPagamento = v, null),
        ("incoterm", p => p.Incoterm, (p, v) => p.Incoterm = v,
            (de, para) => para.IncotermDestino = de.IncotermDestino),
        ("entrega", p => p.Armado, (p, v) => p.Armado = v, null),
        ("moeda do custo", p => p.MoedaCodigo, (p, v) => p.MoedaCodigo = v, null),
        ("moeda do preço", p => p.MoedaVendaCodigo, (p, v) => p.MoedaVendaCodigo = v,
            (de, para) => para.CambioDirecao = de.CambioDirecao),
        ("segurança do câmbio", p => p.SegurancaCambioPct, (p, v) => p.SegurancaCambioPct = v, null),
    };

    private void MontarClientes() => _clientes = _gravadas
        .Where(p => p.Cliente.Trim().Length > 0)
        .GroupBy(p => Textos.Simples(p.Cliente))
        .Select(g =>
        {
            var ultima = g.OrderByDescending(p => p.CriadaEm).ThenByDescending(p => p.Numero).First();
            return new ClienteConhecido(ultima.Cliente.Trim(), ultima, g.Count());
        })
        .OrderBy(c => c.Nome)
        .ToList();

    /// <summary>
    /// O cliente e a última proposta dele — sem contar a que está aberta: ela
    /// pode já ter sido gravada (um anexo grava a proposta nova), e lembrar o
    /// cliente a partir dela mesma não traria nada.
    /// </summary>
    private ClienteConhecido? Conhecido(string nome)
    {
        var chave = Textos.Simples(nome);
        if (chave.Length == 0) return null;

        var dele = _gravadas
            .Where(p => p.Id != _proposta.Id && Textos.Simples(p.Cliente) == chave)
            .ToList();
        if (dele.Count == 0) return null;

        var ultima = dele.OrderByDescending(p => p.CriadaEm).ThenByDescending(p => p.Numero).First();
        return new ClienteConhecido(ultima.Cliente.Trim(), ultima, dele.Count);
    }

    private void MudarCliente(string nome)
    {
        _proposta.Cliente = nome;
        LembrarCliente();
    }

    /// <summary>
    /// O cliente digitado (ou vindo da folha) já tem proposta gravada?
    ///
    /// Numa proposta NOVA, traz da última dele o que ainda está em branco ou no
    /// padrão — nunca o que alguém já escolheu — e avisa, com desfazer. Numa
    /// proposta já gravada, só oferece: trocar o que está gravado tem de ser
    /// pedido.
    /// </summary>
    private void LembrarCliente()
    {
        _sugestaoCliente = null;
        if (Conhecido(_proposta.Cliente) is not { } cliente) return;

        if (_proposta.Id.Length == 0)
        {
            _antesDoCliente = Foto();
            var trazidos = TrazerDoCliente(cliente.Ultima, soOQueNinguemEscolheu: true);
            if (trazidos.Count == 0) return;

            _sugestaoCliente = new SugestaoCliente(
                $"Preenchido com a última proposta de {cliente.Nome} ({cliente.Ultima.Numero}): " +
                $"{string.Join(", ", trazidos)}.", true, cliente);
            return;
        }

        _sugestaoCliente = new SugestaoCliente(
            $"{cliente.Nome} tem {cliente.Propostas} proposta(s) gravada(s); a última é a " +
            $"{cliente.Ultima.Numero}.", false, cliente);
    }

    private void AplicarCliente()
    {
        if (_sugestaoCliente is not { Aplicada: false } sugestao) return;

        _antesDoCliente = Foto();
        var trazidos = TrazerDoCliente(sugestao.Cliente.Ultima, soOQueNinguemEscolheu: false);

        _sugestaoCliente = sugestao with
        {
            Aplicada = true,
            Texto = trazidos.Count > 0
                ? $"Trazido da {sugestao.Cliente.Ultima.Numero}: {string.Join(", ", trazidos)}."
                : "Nada a trazer: os campos já estão iguais aos da última proposta.",
        };
    }

    /// <summary>Volta os campos do cliente ao que eram antes — o resto da proposta fica como está.</summary>
    private void DesfazerCliente()
    {
        if (_antesDoCliente is null || JsonSerializer.Deserialize<Proposta>(_antesDoCliente) is not { } antes)
            return;

        foreach (var (_, ler, gravar, junto) in CamposDoCliente)
        {
            gravar(_proposta, ler(antes));
            junto?.Invoke(antes, _proposta);
        }

        _proposta.IncotermDestino = antes.IncotermDestino;
        _proposta.TaxaCambio = antes.TaxaCambio;
        AjustarEscolhasLivres();

        _sugestaoCliente = null;
        _antesDoCliente = null;
    }

    /// <summary>Copia da proposta do cliente; devolve os nomes do que mudou.</summary>
    private List<string> TrazerDoCliente(Proposta de, bool soOQueNinguemEscolheu)
    {
        var padrao = _padraoDaNova.Length > 0
            ? JsonSerializer.Deserialize<Proposta>(_padraoDaNova) ?? new Proposta()
            : new Proposta();

        var trazidos = new List<string>();

        foreach (var (nome, ler, gravar, junto) in CamposDoCliente)
        {
            var novo = ler(de);
            var atual = ler(_proposta);
            if (novo.Trim().Length == 0 || novo == atual) continue;

            // alguém já escolheu este campo: a lembrança não passa por cima
            if (soOQueNinguemEscolheu && atual.Trim().Length > 0 && atual != ler(padrao)) continue;

            gravar(_proposta, novo);
            junto?.Invoke(de, _proposta);
            trazidos.Add(nome);
        }

        // o lugar do incoterm vem com ele; com o mesmo incoterm e o lugar em
        // branco, vem sozinho
        if (Textos.Igual(_proposta.Incoterm, de.Incoterm) && _proposta.IncotermDestino.Trim().Length == 0)
            _proposta.IncotermDestino = de.IncotermDestino;

        AjustarEscolhasLivres();
        PreencherTaxaDoDia();
        return trazidos;
    }

    // =====================================================================
    // câmbio do dia
    // =====================================================================

    /// <summary>A taxa veio da aba Conversão (e não foi digitada aqui).</summary>
    private bool _taxaDoDia;

    private void EscolherMoedaVenda(Moeda moeda)
    {
        _proposta.MoedaVendaCodigo = moeda.Codigo();
        PreencherTaxaDoDia();
    }

    /// <summary>
    /// Custo e preço entre CLP e USD sem taxa digitada: usa a taxa da aba
    /// Conversão, que a equipe atualiza. As outras moedas não têm taxa
    /// cadastrada, e a tela continua pedindo.
    /// </summary>
    private void PreencherTaxaDoDia()
    {
        if (!Cambio.Converte(_proposta) || _proposta.TaxaCambio.Trim().Length > 0) return;
        if (ConversaoRepository.Taxa(Conversao.Ler().ClpPorUsd) is not { } clpPorUsd) return;

        var custo = _proposta.Moeda;
        var venda = _proposta.MoedaVenda;

        if (custo == Moeda.Clp && venda == Moeda.Usd) _proposta.CambioDirecao = "VendaEmCusto";
        else if (custo == Moeda.Usd && venda == Moeda.Clp) _proposta.CambioDirecao = "CustoEmVenda";
        else return;

        _proposta.TaxaCambio = clpPorUsd.ToString("0.####", Br);
        _taxaDoDia = true;
    }

    // =====================================================================
    // cronômetro
    // =====================================================================

    /// <summary>Uma pausa maior que isto (café, telefone) não conta como trabalho na proposta.</summary>
    private const double PausaQueNaoConta = 180;

    private DateTime _cronInicio = DateTime.Now;
    private DateTime? _cronUltimo;
    private double _cronSegundos;
    private int _cronInteracoes;

    private string TempoNaTela
    {
        get
        {
            var minutos = (int)Math.Round(_cronSegundos / 60);
            return minutos < 1 ? "menos de 1 min" : $"{minutos} min";
        }
    }

    /// <summary>Cada desenho depois de um clique, campo ou arquivo é uma interação.</summary>
    private void TickCronometro()
    {
        var agora = DateTime.Now;

        if (_cronUltimo is { } ultimo)
        {
            var passou = (agora - ultimo).TotalSeconds;
            if (passou < PausaQueNaoConta) _cronSegundos += passou;
        }

        _cronUltimo = agora;
        _cronInteracoes++;
    }

    private void ZerarCronometro()
    {
        _cronInicio = DateTime.Now;
        _cronUltimo = null;
        _cronSegundos = 0;
        _cronInteracoes = 0;
    }

    private void RegistrarTempo(string evento)
    {
        try
        {
            Tempos.Registrar(new TempoProposta
            {
                PropostaId = _proposta.Id,
                Numero = _proposta.Numero,
                Revisao = _proposta.Revisao.Trim(),
                Usuario = _usuario,
                Inicio = _cronInicio.ToString("yyyy-MM-ddTHH:mm:ss"),
                Fim = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"),
                SegundosAtivos = (int)Math.Round(_cronSegundos),
                Interacoes = _cronInteracoes,
                Evento = evento,
            });
        }
        catch (Exception)
        {
            // o cronômetro mede; nunca impede um documento de sair
        }
    }

    // =====================================================================
    // todos os arquivos de uma vez
    // =====================================================================

    /// <summary>O que a última importação fez com cada arquivo.</summary>
    private List<(string Arquivo, string Destino)> _importados = new();

    private static readonly string[] ExtensoesDeCurva = { ".pdf", ".png", ".jpg", ".jpeg" };

    /// <summary>
    /// Folha de dados, seleções e curvas num envio só. A folha vai primeiro,
    /// porque é ela que cria os equipamentos; cada seleção vai para um
    /// equipamento e cada curva também — pelo número no nome do arquivo quando
    /// ele diz ("eq2", "item 3"), senão para o primeiro que ainda não tem.
    /// Sem equipamento livre, nasce um.
    /// </summary>
    private async Task ImportarArquivos(InputFileChangeEventArgs e)
    {
        _lendo = true;
        _importados = new();
        _avisos = new();
        _daPlanilha = new();

        try
        {
            var lidos = new List<(string Nome, string Ext, byte[] Bytes)>();
            foreach (var arquivo in e.GetMultipleFiles(40))
            {
                await using var fluxo = arquivo.OpenReadStream(25 * 1024 * 1024);
                using var memoria = new MemoryStream();
                await fluxo.CopyToAsync(memoria);
                lidos.Add((arquivo.Name, Path.GetExtension(arquivo.Name).ToLowerInvariant(), memoria.ToArray()));
            }

            lidos = lidos.OrderBy(l => l.Nome, StringComparer.OrdinalIgnoreCase).ToList();
            var clienteAntes = _proposta.Cliente;

            foreach (var (nome, _, bytes) in lidos.Where(l => l.Ext is ".xlsx" or ".xlsm"))
            {
                var equipamentos = LerFolha(bytes);
                _importados.Add((nome, equipamentos > 0
                    ? $"folha de dados: {equipamentos} equipamento(s)"
                    : "folha de dados, sem quantidade na linha 31"));
            }

            // o cliente da folha é lembrado AGORA: o primeiro anexo grava a
            // proposta, e o que ainda está no padrão precisa ser completado antes
            if (!Textos.Igual(clienteAntes, _proposta.Cliente))
            {
                LembrarCliente();
                clienteAntes = _proposta.Cliente;
            }

            var curvas = lidos.Where(l => ExtensoesDeCurva.Contains(l.Ext)).ToList();

            foreach (var (nome, ext, bytes) in lidos.Where(l => l.Ext is ".docx" or ".txt" or ".doc"))
            {
                if (ext == ".doc")
                {
                    _importados.Add((nome, "ignorado: Word antigo (.doc) — salve como .docx"));
                    continue;
                }

                var texto = ext == ".docx"
                    ? TextoDoWord.Ler(new MemoryStream(bytes))
                    : new StreamReader(new MemoryStream(bytes)).ReadToEnd();
                var leitura = SelecaoTecnica.Ler(texto);

                // um .txt que não é seleção é a tabela de performance: vira curva
                if (leitura.Dados.Count == 0)
                {
                    if (ext == ".txt") curvas.Add((nome, ext, bytes));
                    else _importados.Add((nome, "ignorado: não parece um relatório de seleção"));
                    continue;
                }

                var indice = Destino(nome, i => i.Tecnicos.Count == 0 && i.ArquivoDaSelecao.Length == 0);
                var curvasDoWord = await AplicarSelecao(leitura, ext, bytes, nome, indice);
                _importados.Add((nome, $"seleção do equipamento {indice + 1}"
                    + (curvasDoWord > 0 ? $", com {curvasDoWord} curva(s)" : "")));
            }

            foreach (var (nome, _, bytes) in curvas.OrderBy(c => c.Nome, StringComparer.OrdinalIgnoreCase))
            {
                var indice = Destino(nome, i => i.Curvas.Count == 0);
                await AnexarCurva(bytes, nome, indice);
                _importados.Add((nome, $"curva do equipamento {indice + 1}"));
            }

            foreach (var (nome, _, _) in lidos.Where(l =>
                         l.Ext is not (".xlsx" or ".xlsm" or ".docx" or ".txt" or ".doc")
                         && !ExtensoesDeCurva.Contains(l.Ext)))
            {
                _importados.Add((nome, "ignorado: tipo de arquivo que a proposta não usa"));
            }

            if (!Textos.Igual(clienteAntes, _proposta.Cliente)) LembrarCliente();

            _aberto = 0;
            Aviso($"{lidos.Count} arquivo(s) lido(s) — confira para onde foi cada um, logo abaixo do botão.");
        }
        catch (Exception ex)
        {
            Aviso($"Não consegui ler os arquivos: {ex.Message}", erro: true);
        }
        finally
        {
            _lendo = false;
        }
    }

    /// <summary>
    /// Para qual equipamento vai um arquivo: o que o nome aponta ("eq2",
    /// "equipamento 3", "item 1", "fan 2"), quando ele ainda está livre; senão
    /// o primeiro livre; senão um equipamento novo.
    /// </summary>
    private int Destino(string nome, Func<ItemProposta, bool> livre)
    {
        var aponta = Regex.Match(Path.GetFileNameWithoutExtension(nome),
            @"(?:eq|equip\w*|item|ventilador|vent|fan)\s*[-_ ]?\s*(\d{1,2})\b", RegexOptions.IgnoreCase);

        if (aponta.Success && int.TryParse(aponta.Groups[1].Value, out var n)
            && n >= 1 && n <= _proposta.Itens.Count && livre(_proposta.Itens[n - 1]))
        {
            return n - 1;
        }

        for (var i = 0; i < _proposta.Itens.Count; i++)
            if (livre(_proposta.Itens[i])) return i;

        _proposta.Itens.Add(new ItemProposta());
        return _proposta.Itens.Count - 1;
    }

    // =====================================================================
    // equipamentos: filtro do modelo, duplicar, escopo padrão, motor sugerido
    // =====================================================================

    /// <summary>O que foi digitado no filtro do modelo (encurta a lista de ~100 modelos).</summary>
    private string _filtroModelo = "";

    /// <summary>Os modelos que casam com o filtro — e sempre o escolhido, para a lista não perdê-lo.</summary>
    private IEnumerable<Equipamento> ModelosDoFiltro(ItemProposta item)
    {
        var termos = Textos.Simples(_filtroModelo).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (termos.Length == 0) return _equipamentos;

        return _equipamentos.Where(e => e.Id == item.EquipamentoId
            || termos.All(t => Textos.Simples($"{e.Rotulo} {e.Codigo}").Contains(t)));
    }

    /// <summary>
    /// Um equipamento igual ao aberto, logo depois dele: escopo, parte
    /// elétrica, opcionais e preços à mão vêm junto; seleção e curva não —
    /// são de cada ventilador.
    /// </summary>
    private void DuplicarEquipamento()
    {
        if (_proposta.Itens.Count == 0) return;

        var posicao = PosicaoValida;
        var copia = JsonSerializer.Deserialize<ItemProposta>(JsonSerializer.Serialize(_proposta.Itens[posicao]));
        if (copia is null) return;

        copia.Quantidade = "1";
        copia.Coluna = "";
        copia.Curvas = new();
        copia.Tecnicos = new();
        copia.ArquivoDaSelecao = "";
        copia.Curva = "";
        copia.CurvaNome = "";
        copia.CurvaImagem = "";

        _proposta.Itens.Insert(posicao + 1, copia);
        _faltouNaSelecao.Clear();
        _aberto = posicao + 1;
        _abaItem = "escopo";
        _apagandoItem = null;

        Aviso($"Equipamento {posicao + 1} duplicado como equipamento {posicao + 2}: confira o modelo e a " +
              "quantidade, e suba a seleção e a curva dele.");
    }

    /// <summary>Os escopos padrão, lidos ao recarregar.</summary>
    private List<EscopoPadrao> _padroes = new();

    private EscopoPadrao? PadraoDaSerie(string serie) =>
        _padroes.FirstOrDefault(p => Textos.Igual(p.Serie, serie));

    private void AplicarPadrao(ItemProposta item, string serie)
    {
        if (PadraoDaSerie(serie) is not { } padrao) return;

        var feitas = padrao.AplicarEm(item);
        Aviso(feitas > 0
            ? $"Escopo padrão da série {serie} aplicado em {feitas} linha(s) — o que já estava marcado ficou como estava."
            : "Nada a aplicar: as listas deste equipamento já estão marcadas.");
    }

    private void GuardarPadrao(ItemProposta item, string serie)
    {
        if (!item.Escolhas.Values.Any(v => v.Trim().Length > 0)
            && !item.ComMotor && !item.ComPartidor && !item.ComInstrumentacao)
        {
            Aviso("Marque o escopo deste equipamento antes de guardá-lo como padrão.", erro: true);
            return;
        }

        EscoposPadrao.Salvar(EscopoPadrao.De(serie, item, _usuario.Length > 0 ? _usuario : "equipe"));
        _padroes = EscoposPadrao.Todos();
        Aviso($"Escopo guardado como padrão da série {serie}: o próximo equipamento desta série já nasce com ele.");
    }

    /// <summary>
    /// O primeiro modelo escolhido num equipamento traz o escopo padrão da
    /// série, completando o que ainda está em branco (o que veio da folha de
    /// dados fica).
    /// </summary>
    private void AplicarPadraoSeHouver(ItemProposta item)
    {
        if (ModeloDe(item) is not { } modelo || PadraoDaSerie(modelo.Serie) is not { } padrao) return;

        var feitas = padrao.AplicarEm(item);
        if (feitas > 0)
            Aviso($"Escopo padrão da série {modelo.Serie} aplicado ({feitas} linha(s)) — confira e ajuste o que for diferente.");
    }

    /// <summary>O que a seleção diz do motor: a potência mínima, e os polos e a frequência quando dá para saber.</summary>
    private sealed record SugestaoDeMotor(decimal CvMinimo, int? Polos, int? Hz, string Texto);

    /// <summary>Folga sobre a potência consumida da seleção ao sugerir o motor.</summary>
    private const decimal FolgaDoMotor = 0.10m;

    /// <summary>
    /// A sugestão de motor a partir da seleção do equipamento: potência
    /// consumida + 10% de folga, em CV; e, quando a rotação do ventilador é a de
    /// um motor direto (até 6% abaixo da síncrona), os polos e a frequência. Sem
    /// isso, a frequência vem do país de destino.
    /// </summary>
    private SugestaoDeMotor? SugestaoDoMotor(ItemProposta item)
    {
        if (!item.Tecnicos.TryGetValue("potencia", out var potencia)) return null;
        if (DadosExcel.Numero(potencia.Valor) is not { } valor || valor <= 0m) return null;

        var unidade = Textos.Simples(potencia.Unidade);
        decimal? cv =
            unidade.Contains("kw") ? valor * 1.35962m :
            unidade.Contains("hp") ? valor * 1.01387m :
            unidade.Contains("cv") ? valor :
            null;
        if (cv is null) return null;

        var minimo = Math.Ceiling(cv.Value * (1m + FolgaDoMotor));
        var texto = $"Pela seleção: {potencia.Inteiro} consumidos → motor de pelo menos {minimo.ToString("N0", Br)} CV (10% de folga)";

        var doPais = FrequenciaDoPais(_proposta.PaisDestino.Trim().Length > 0 ? _proposta.PaisDestino : _proposta.Pais);
        int? polos = null;
        int? hz = doPais;

        if (item.Tecnicos.TryGetValue("rotacao", out var rotacao)
            && DadosExcel.Numero(rotacao.Valor) is { } rpm && rpm > 0m)
        {
            var possiveis = new List<(int Hz, int Polos)>();
            foreach (var f in new[] { 50, 60 })
            {
                foreach (var p in new[] { 2, 4, 6, 8, 10, 12 })
                {
                    var sincrona = 120m * f / p;
                    if (rpm <= sincrona && rpm >= sincrona * 0.94m) possiveis.Add((f, p));
                }
            }

            var escolhido = possiveis.Count == 1
                ? possiveis[0]
                : possiveis.FirstOrDefault(x => x.Hz == doPais);

            if (escolhido.Polos > 0)
            {
                polos = escolhido.Polos;
                hz = escolhido.Hz;
                texto += $" · {rotacao.Inteiro} → {polos} polos a {hz} Hz";
            }
        }

        if (polos is null && hz is int pais) texto += $" · {pais} Hz pelo país de destino";

        return new SugestaoDeMotor(minimo, polos, hz, texto + ".");
    }

    private static int? FrequenciaDoPais(string pais) => Textos.Simples(pais) switch
    {
        "chile" or "argentina" => 50,
        "brasil" or "peru" or "colombia" or "mexico" or "estados unidos" => 60,
        _ => null,
    };

    /// <summary>
    /// Leva a sugestão para o filtro do motor: frequência e polos primeiro, e a
    /// menor potência do catálogo que cobre a pedida por último — escolhida
    /// entre os motores que sobraram.
    /// </summary>
    private void AplicarSugestaoDoMotor(ItemProposta item)
    {
        if (SugestaoDoMotor(item) is not { } sugestao) return;

        item.FiltroMotor.Clear();
        item.MotorId = "";

        var campos = EscopoProposta.CamposDoMotor;
        var campoHz = campos.First(c => c.Rotulo.StartsWith("Frequ", StringComparison.Ordinal));
        var campoPolos = campos.First(c => c.Rotulo.StartsWith("Nº de polos", StringComparison.Ordinal));
        var campoCv = campos.First(c => c.Rotulo.StartsWith("Potência", StringComparison.Ordinal));

        if (sugestao.Hz is int hz
            && OpcoesDoCampo(item, campoHz).FirstOrDefault(o => NumerosDe(o).Contains(hz)) is { } opcaoHz)
        {
            FiltrarMotor(item, campoHz.Rotulo, opcaoHz);
        }

        if (sugestao.Polos is int polos
            && OpcoesDoCampo(item, campoPolos).FirstOrDefault(o => DadosExcel.Numero(o) == polos) is { } opcaoPolos)
        {
            FiltrarMotor(item, campoPolos.Rotulo, opcaoPolos);
        }

        var potencia = OpcoesDoCampo(item, campoCv)
            .Select(o => (Opcao: o, Cv: DadosExcel.Numero(o)))
            .Where(x => x.Cv >= sugestao.CvMinimo)
            .OrderBy(x => x.Cv)
            .FirstOrDefault();
        if (potencia.Opcao is not null) FiltrarMotor(item, campoCv.Rotulo, potencia.Opcao);

        var sobraram = Candidatos(item).Count;
        Aviso(sobraram switch
        {
            0 => "Nenhum motor do catálogo atende à sugestão — ajuste o filtro à mão.",
            1 => "Motor escolhido pela seleção — confira o código e o custo.",
            _ => $"{sobraram} motores atendem à sugestão — escolha o fabricante ou a tensão para fechar.",
        }, erro: sobraram == 0);
    }

    private static IEnumerable<int> NumerosDe(string texto) => Regex.Matches(texto, @"\d{1,4}")
        .Select(m => int.Parse(m.Value, System.Globalization.CultureInfo.InvariantCulture));

    // =====================================================================
    // pequenos
    // =====================================================================

    /// <summary>O segundo contato do quadro só aparece quando alguém pede (ou já tem um).</summary>
    private bool _mostrarContato2;

    private void MudarTaxa(string texto)
    {
        _proposta.TaxaCambio = texto;
        _taxaDoDia = false;
    }
}
