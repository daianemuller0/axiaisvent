using HowdenAxiais.Poc.Data;

namespace HowdenAxiais.Poc.Components.Pages;

// A etapa Cliente e condições (item 12 do plano): primeiro o que se digita
// (o cliente e o que é desta proposta), depois as condições num resumo que se
// lê de uma vez — o que muda preço e impostos vem antes, em destaque — e os
// contatos Howden num cartão de leitura. Os campos e os binds são os mesmos
// de antes: muda a apresentação, e nada muda sem clique.
public partial class PropostaPage
{
    // =====================================================================
    // campos lembrados
    // =====================================================================

    /// <summary>
    /// Os campos que vieram lembrados — da última proposta do mesmo cliente
    /// (<see cref="TrazerDoCliente"/>) ou da última proposta de quem está
    /// logado (<see cref="NovaComPadroes"/>). A chave é o nome do campo em
    /// <see cref="CamposDoCliente"/> ("e-mail", "destino"…), mais "categoria",
    /// "produto", "segmento", "validade", "prazo" e "contato howden" dos
    /// padrões. Ficam em verde até alguém editar: um valor que ninguém
    /// digitou precisa estar à vista, e não só no aviso que some.
    /// </summary>
    private readonly HashSet<string> _lembrados = new();

    /// <summary>De onde cada campo lembrado veio ("PRP-2026-104"), para o título e a linha do desfazer.</summary>
    private readonly Dictionary<string, string> _deOndeVeio = new();

    /// <summary>Os lembrados que vieram dos padrões de quem está logado (a última proposta dele), e não do cliente.</summary>
    private readonly HashSet<string> _dosPadroes = new();

    /// <summary>A proposta de que <see cref="_lembrados"/> fala: abriu outra, a marca some.</summary>
    private Proposta? _lembradosDe;

    /// <summary>Quantos campos o último "trazer do cliente" mudou — zero diz "nada a trazer".</summary>
    private int _ultimosTrazidos;

    private void SincronizarLembrados()
    {
        if (ReferenceEquals(_lembradosDe, _proposta)) return;
        _lembradosDe = _proposta;
        _lembrados.Clear();
        _deOndeVeio.Clear();
        _dosPadroes.Clear();
        _ultimosTrazidos = 0;
    }

    /// <summary>Fim de <see cref="TrazerDoCliente"/>: marca o que veio e revê o modo das condições.</summary>
    private void MarcarLembrados(List<string> trazidos, Proposta de)
    {
        SincronizarLembrados();
        foreach (var campo in trazidos)
        {
            _lembrados.Add(campo);
            _deOndeVeio[campo] = de.Numero;
            _dosPadroes.Remove(campo);
        }

        _ultimosTrazidos = trazidos.Count;
        if (trazidos.Count == 0) return;

        // o que mudou o preço veio de outra proposta: pede confirmação de novo
        _condicoesConfirmadas = false;
        ReavaliarCondicoes();
    }

    /// <summary>
    /// Fim de <see cref="NovaComPadroes"/>: o que veio da última proposta de
    /// quem está logado. A proposta ainda não é a da tela — a marca fica
    /// presa a ela, e vale quando ela entrar. Só conta o que é diferente do
    /// padrão de uma proposta em branco: igual ao padrão, não há o que conferir.
    /// </summary>
    private void LembrarPadroes(Proposta nova, Proposta minha)
    {
        _lembradosDe = nova;
        _lembrados.Clear();
        _deOndeVeio.Clear();
        _dosPadroes.Clear();
        _ultimosTrazidos = 0;

        var branco = Nascer();

        void Se(string campo, string veio, string doBranco)
        {
            if (veio.Trim().Length == 0 || Textos.Igual(veio, doBranco)) return;
            _lembrados.Add(campo);
            _deOndeVeio[campo] = minha.Numero;
            _dosPadroes.Add(campo);
        }

        Se("BU", minha.Bu, branco.Bu);
        Se("idioma", minha.Idioma, branco.Idioma);
        Se("categoria", minha.Categoria, branco.Categoria);
        Se("produto", minha.Produto, branco.Produto);
        Se("segmento", minha.MarketSegment, branco.MarketSegment);
        Se("validade", minha.ValidadeDias, branco.ValidadeDias);
        Se("prazo", minha.PrazoEntregaDias, branco.PrazoEntregaDias);
        Se("moeda do custo", minha.MoedaCodigo, branco.MoedaCodigo);

        // o contato do quadro de quem está logado é dele, e não lembrança; o
        // da última proposta só vem quando ele não está no quadro
        if (ListasDaProposta.Contato(_usuario) is null) Se("contato howden", minha.ContatoNome, "");
    }

    /// <summary>O campo foi editado: deixa de ser "lembrado".</summary>
    private void Editou(string campo)
    {
        SincronizarLembrados();
        _lembrados.Remove(campo);
    }

    private bool EhLembrado(string campo)
    {
        SincronizarLembrados();
        return _lembrados.Contains(campo);
    }

    /// <summary>A classe de um campo do cartão Cliente: verde até ser editado.</summary>
    private string CelulaDoCliente(string campo) => EhLembrado(campo) ? "cell lembrado" : "cell";

    /// <summary>
    /// A classe de um campo das condições: verde até ser editado ou até o
    /// "confirmar" — conferido o resumo, o formulário não precisa repetir a marca.
    /// </summary>
    private string CelulaDaCondicao(string campo) =>
        EhLembrado(campo) && !_condicoesConfirmadas ? "cell lembrado" : "cell";

    /// <summary>"veio da PRP-2026-104" no título do campo lembrado; nulo, o atributo nem sai.</summary>
    private string? TituloDoLembrado(string campo) =>
        EhLembrado(campo) && _deOndeVeio.TryGetValue(campo, out var de) ? $"veio {Origem(de, _dosPadroes.Contains(campo))}" : null;

    /// <summary>"da PRP-2026-104", ou "da sua última proposta (PRP-…)" quando veio dos padrões de quem está logado.</summary>
    private static string Origem(string numero, bool dosPadroes) =>
        dosPadroes ? $"da sua última proposta ({numero})" : $"da {numero}";

    /// <summary>
    /// Fim de <see cref="DesfazerCliente"/>: os campos voltaram, a marca dos que
    /// vieram do cliente sai, e o cartão das condições se revê.
    /// </summary>
    private void DepoisDeDesfazerCliente(string numero)
    {
        SincronizarLembrados();
        foreach (var campo in _lembrados.Where(c => _deOndeVeio.GetValueOrDefault(c) == numero).ToList())
        {
            _lembrados.Remove(campo);
            _deOndeVeio.Remove(campo);
        }

        _ultimosTrazidos = 0;
        Contar("cliente:desfazer");
        ReavaliarCondicoes();
    }

    /// <summary>Quantos campos ainda verdes vieram desta proposta do cliente.</summary>
    private int LembradosDa(string numero)
    {
        SincronizarLembrados();
        return _lembrados.Count(c => _deOndeVeio.GetValueOrDefault(c) == numero);
    }

    // =====================================================================
    // projetos do mesmo cliente
    // =====================================================================

    /// <summary>
    /// Até 10 projetos distintos das propostas do MESMO cliente, os mais
    /// recentes primeiro: o projeto se repete em cerca de metade das
    /// propostas, e digitar de novo é onde nasce "Mina Norte" e "Mina norte".
    /// O cliente é comparado como em <see cref="Conhecido"/>; de outro cliente,
    /// nunca — o nome de um projeto alheio não pode ir parar num documento.
    /// </summary>
    private List<string> ProjetosDoCliente()
    {
        var chave = Textos.Simples(_proposta.Cliente);
        if (chave.Length == 0) return new();

        return _gravadas
            .Where(p => p.Id != _proposta.Id && Textos.Simples(p.Cliente) == chave && p.Projeto.Trim().Length > 0)
            .OrderByDescending(p => p.CriadaEm)
            .ThenByDescending(p => p.Numero)
            .Select(p => p.Projeto.Trim())
            .DistinctBy(Textos.Simples)
            .Take(10)
            .ToList();
    }

    /// <summary>O projeto mudou: se veio da lista do cliente, conta (o ganho do item 12 é este).</summary>
    private void MudouProjeto()
    {
        var digitado = Textos.Simples(_proposta.Projeto);
        if (digitado.Length > 0 && ProjetosDoCliente().Any(p => Textos.Simples(p) == digitado))
            Contar("cliente:projeto-da-lista");
    }

    // =====================================================================
    // condições: resumo ou formulário
    // =====================================================================

    /// <summary>O cartão das condições mostra o formulário (e não o resumo).</summary>
    private bool _editandoCondicoes;

    /// <summary>
    /// Os campos lembrados que mudam o preço foram conferidos ("confirmar").
    /// Vive só na tela: abrir a proposta de novo pede de novo — é a conferência
    /// de quem está montando agora.
    /// </summary>
    private bool _condicoesConfirmadas;

    /// <summary>
    /// A pessoa abriu ou fechou o formulário (ou foi a um campo): daí em diante
    /// a tela não troca o modo sozinha, nem quando o cliente traz as condições.
    /// </summary>
    private bool _modoDasCondicoesEscolhido;

    /// <summary>A proposta de que o estado do cartão fala: abriu outra, ele se decide de novo.</summary>
    private Proposta? _condicoesDe;

    /// <summary>
    /// Chamado antes de desenhar a etapa e antes de qualquer ação nela: na
    /// primeira vez com uma proposta, o cartão decide se abre em resumo ou em
    /// edição (ver <see cref="AbrirCondicoesEmEdicao"/>).
    /// </summary>
    private void SincronizarCondicoes()
    {
        SincronizarLembrados();
        if (ReferenceEquals(_condicoesDe, _proposta)) return;

        _condicoesDe = _proposta;
        _condicoesConfirmadas = false;
        _modoDasCondicoesEscolhido = false;
        _editandoContato = 0;
        _trocandoContato = 0;
        _editandoCondicoes = AbrirCondicoesEmEdicao;
    }

    /// <summary>
    /// Abre já em edição quando falta condição (pagamento, incoterm, o lugar
    /// dele ou o armado) ou quando o cliente não tem histórico — proposta nova
    /// de cliente novo, sem nada lembrado: não há resumo a conferir, há
    /// formulário a preencher. Uma proposta gravada, aberta pela lista, começa
    /// no resumo.
    /// </summary>
    private bool AbrirCondicoesEmEdicao =>
        FaltaNasCondicoes
        || (_proposta.Id.Length == 0
            && Conhecido(_proposta.Cliente) is null
            && !_lembrados.Overlaps(ChavesDasCondicoes));

    /// <summary>
    /// O cliente trouxe (ou o desfazer devolveu) as condições: enquanto a
    /// pessoa não escolheu o modo, ele segue a mesma regra de quando abre.
    /// </summary>
    private void ReavaliarCondicoes()
    {
        SincronizarCondicoes();
        if (!_modoDasCondicoesEscolhido) _editandoCondicoes = AbrirCondicoesEmEdicao;
    }

    // as mesmas regras das pendências (Pendencias, etapa 2): o resumo mostra em âmbar o que lá pende
    private bool FaltaPagamento => _proposta.CondicaoPagamento.Trim().Length == 0;
    private bool FaltaIncoterm => _proposta.Incoterm.Trim().Length == 0;
    private bool FaltaLugarDoIncoterm => _proposta.Incoterm.Trim().Length > 0 && !_incotermLivre && PedeDestino
                                         && _proposta.IncotermDestino.Trim().Length == 0;
    private bool FaltaArmado => _proposta.Armado.Trim().Length == 0;
    private bool FaltaNasCondicoes => FaltaPagamento || FaltaIncoterm || FaltaLugarDoIncoterm || FaltaArmado;

    /// <summary>Os campos do formulário das condições (os ids da tela).</summary>
    private static readonly HashSet<string> CamposDasCondicoes = new()
    {
        "f-numero", "f-revisao", "f-data", "f-preparada", "f-idioma", "f-venda-para", "f-destino",
        "f-pais-destino", "f-representante", "f-validade", "f-prazo",
        "f-pagamento", "f-incoterm", "f-armado", "f-destino-incoterm",
    };

    /// <summary>Os que moram em "Mais campos": ir a um deles abre o bloco também.</summary>
    private static readonly HashSet<string> CamposDeMais = new()
    {
        "f-ano", "f-bu", "f-fase", "f-categoria", "f-produto", "f-segmento", "f-portal", "f-fianca",
        "f-beneficio", "f-representante2",
    };

    /// <summary>As chaves de <see cref="_lembrados"/> que aparecem no cartão das condições.</summary>
    private static readonly HashSet<string> ChavesDasCondicoes = new()
    {
        "destino", "país de destino", "venda para", "BU", "representante", "representante 2",
        "moeda do custo", "moeda do preço", "segurança do câmbio", "estado", "portal", "fiança",
        "benefício", "idioma", "validade", "prazo", "pagamento", "incoterm", "entrega",
    };

    /// <summary>
    /// Pendência de condições (item 06) abre o formulário antes de ir ao
    /// campo; a do contato Howden abre a lista do quadro quando ele já tem
    /// um nome (vazio, a lista já está à vista). Dona: a pista do Cliente.
    /// </summary>
    partial void AntesDeIrAoCampo(Pendencia p)
    {
        if (p.Etapa != 2) return;
        SincronizarCondicoes();

        if (p.Alvo == "f-contato")
        {
            _editandoContato = 0;
            if (_proposta.ContatoNome.Trim().Length > 0) _trocandoContato = 1;
            return;
        }

        if (!CamposDasCondicoes.Contains(p.Alvo) && !CamposDeMais.Contains(p.Alvo)) return;
        _editandoCondicoes = true;
        _modoDasCondicoesEscolhido = true;
        if (CamposDeMais.Contains(p.Alvo)) _maisCampos = true;
    }

    private void AbrirCondicoes()
    {
        SincronizarCondicoes();
        _editandoCondicoes = true;
        _modoDasCondicoesEscolhido = true;
        Contar("cliente:editar-condicoes");
    }

    private void FecharCondicoes()
    {
        SincronizarCondicoes();
        _editandoCondicoes = false;
        _modoDasCondicoesEscolhido = true;
    }

    /// <summary>
    /// Um pedaço do resumo foi clicado: abre a edição já no campo dele. Os da
    /// etapa Preço (moedas, segurança) levam até lá; o Estado, ao cartão Cliente.
    /// </summary>
    private void IrAoPedaco(PedacoDaCondicao pedaco)
    {
        SincronizarCondicoes();
        Contar("cliente:pedaco");

        if (pedaco.Etapa != 2)
        {
            IrPara(new Pendencia("", pedaco.Etapa, Alvo: pedaco.Alvo));
            return;
        }

        if (CamposDasCondicoes.Contains(pedaco.Alvo) || CamposDeMais.Contains(pedaco.Alvo))
        {
            _editandoCondicoes = true;
            _modoDasCondicoesEscolhido = true;
            if (CamposDeMais.Contains(pedaco.Alvo)) _maisCampos = true;
        }

        _irAoCampo = pedaco.Alvo;
    }

    private void ConfirmarCondicoes()
    {
        _condicoesConfirmadas = true;
        Contar("cliente:confirmar");
    }

    /// <summary>
    /// Um pedaço do resumo das condições: o rótulo (nos chips), o valor como se
    /// lê, o campo para onde ele leva e as chaves de <see cref="_lembrados"/>
    /// que o deixam verde. <paramref name="Falta"/> é o que as pendências
    /// também cobram: sai em âmbar.
    /// </summary>
    private sealed record PedacoDaCondicao(string Rotulo, string Valor, string Alvo, string[] Chaves,
        bool Falta = false, int Etapa = 2, string Titulo = "");

    /// <summary>Verde no resumo: veio lembrado e ainda não foi confirmado.</summary>
    private bool PedacoLembrado(PedacoDaCondicao pedaco) =>
        !_condicoesConfirmadas && pedaco.Chaves.Any(EhLembrado);

    /// <summary>
    /// Os chips do alto: só o que muda preço ou impostos, cada um separado. Os
    /// que só pesam quando fogem do neutro (portal, fiança, benefício,
    /// representante 2) aparecem só então.
    /// </summary>
    private List<PedacoDaCondicao> ChipsDasCondicoes()
    {
        var p = _proposta;
        var chips = new List<PedacoDaCondicao>();

        var destino = Textos.Igual(p.Destino, "Nacional") && Textos.Igual(p.PaisDestino, "Brasil")
            ? p.Destino
            : $"{p.Destino} → {p.PaisDestino}";
        chips.Add(new("Destino", destino, "f-destino", new[] { "destino", "país de destino" }));
        chips.Add(new("Venda para", p.VendaPara, "f-venda-para", new[] { "venda para" }));

        var bu = ListasDaProposta.CodigoDaBu(p.Bu);
        chips.Add(new("BU", bu, "f-bu", new[] { "BU" },
            Titulo: ListasDaProposta.Bus.FirstOrDefault(b => b.Codigo == bu).Rotulo ?? bu));

        chips.Add(new("Representante", p.Representante.Trim().Length > 0 ? p.Representante : "nenhum",
            "f-representante", new[] { "representante" }));
        if (p.Representante2.Trim().Length > 0)
            chips.Add(new("Representante 2", p.Representante2, "f-representante2", new[] { "representante 2" }));

        // as moedas e a segurança do câmbio num chip só: as três moram juntas
        // na caixa da moeda, na etapa Preço, e é para lá que o chip leva
        var custo = p.Moeda.Rotulo();
        var venda = p.MoedaVenda.Rotulo();
        chips.Add(Cambio.Converte(p)
            ? new("Moedas", $"custo {custo} · preço {venda} · segurança {Cambio.Seguranca(p).ToString("0.##", Br)}%",
                "f-moeda-venda", new[] { "moeda do custo", "moeda do preço", "segurança do câmbio" },
                Etapa: 4, Titulo: "A moeda do preço, a taxa e a segurança do câmbio estão na etapa Preço")
            : new("Moedas", $"custo e preço em {custo}", "f-moeda-venda", new[] { "moeda do custo", "moeda do preço" },
                Etapa: 4, Titulo: "A moeda do preço está na etapa Preço"));

        if (EhBrasil)
            chips.Add(p.Estado.Trim().Length > 0
                ? new("Estado", p.Estado, "f-uf", new[] { "estado" }, Titulo: "A UF do ICMS fica no cartão Cliente")
                : new("Estado", "falta a UF", "f-uf", new[] { "estado" }, Falta: true));

        if (Proposta.TemPortal(p.Portal))
            chips.Add(new("Portal", p.Portal, "f-portal", new[] { "portal" }));
        if (Textos.Igual(p.Fianca, "Sim"))
            chips.Add(new("Fiança", "sim", "f-fianca", new[] { "fiança" }));
        if (EhBrasil && p.Beneficio.Trim().Length > 0 && !Textos.Igual(p.Beneficio, ListasDaProposta.Beneficios[0]))
            chips.Add(new("Benefício", p.Beneficio, "f-beneficio", new[] { "benefício" }));

        return chips;
    }

    /// <summary>
    /// O resto, em texto corrido, na ordem em que sai no documento: número,
    /// revisão, data, quem preparou, língua, validade e prazo, e depois
    /// pagamento, incoterm e armado. O que falta aparece no lugar, em âmbar.
    /// </summary>
    private List<PedacoDaCondicao> TextoDasCondicoes()
    {
        var p = _proposta;
        var pedacos = new List<PedacoDaCondicao>
        {
            new("Número", p.Numero.Trim().Length > 0 ? p.Numero : "número ao gravar", "f-numero", Array.Empty<string>()),
            new("Revisão", $"rev {p.Revisao.Trim()}", "f-revisao", Array.Empty<string>()),
            new("Data", p.Data.Trim().Length > 0 ? DataCurta(p.Data) : "sem data", "f-data", Array.Empty<string>()),
        };

        if (p.PreparadaPor.Trim().Length > 0)
            pedacos.Add(new("Preparada por", $"preparada por {p.PreparadaPor.Trim()}", "f-preparada", Array.Empty<string>()));

        pedacos.Add(new("Idioma", p.Idioma, "f-idioma", new[] { "idioma" }, Titulo: "Idioma da proposta"));
        pedacos.Add(new("Validade", $"validade {p.ValidadeDias.Trim()} d", "f-validade", new[] { "validade" },
            Titulo: "Validade (dias)"));
        pedacos.Add(new("Prazo", $"prazo {p.PrazoEntregaDias.Trim()} d", "f-prazo", new[] { "prazo" },
            Titulo: "Prazo de entrega (dias)"));

        if (FaltaPagamento)
        {
            pedacos.Add(new("Pagamento", "falta a condição de pagamento", "f-pagamento", new[] { "pagamento" }, Falta: true));
        }
        else
        {
            var pagamento = p.CondicaoPagamento.Trim();
            var curto = pagamento.Length > 44 ? pagamento[..42].TrimEnd(' ', ',', ';') + "…" : pagamento;
            pedacos.Add(new("Pagamento", $"Pagamento: {curto}", "f-pagamento", new[] { "pagamento" }, Titulo: pagamento));
        }

        if (FaltaIncoterm)
            pedacos.Add(new("Incoterm", "falta o incoterm", "f-incoterm", new[] { "incoterm" }, Falta: true));
        else if (FaltaLugarDoIncoterm)
            pedacos.Add(new("Incoterm", $"{p.Incoterm} — falta o {RotuloDoDestino.ToLowerInvariant()}",
                "f-destino-incoterm", new[] { "incoterm" }, Falta: true));
        else
        {
            var lugar = ListasDaProposta.SoOLugar(p.IncotermDestino);
            pedacos.Add(new("Incoterm", lugar.Length > 0 ? $"{p.Incoterm} {lugar}" : p.Incoterm, "f-incoterm",
                new[] { "incoterm" }, Titulo: ListasDaProposta.LinhaDoIncoterm(p.Incoterm, p.IncotermDestino)));
        }

        pedacos.Add(FaltaArmado
            ? new("Entrega", "armado ou desarmado?", "f-armado", new[] { "entrega" }, Falta: true)
            : new("Entrega", p.Armado, "f-armado", new[] { "entrega" }, Titulo: ListasDaProposta.TextoDoArmado(p.Armado)));

        return pedacos;
    }

    /// <summary>"da PRP-2026-104" — de onde vieram os pedaços verdes do resumo, para a linha do "confirmar".</summary>
    private string OrigemDosVerdes(IEnumerable<PedacoDaCondicao> pedacos)
    {
        // uma origem por número: a última do cliente costuma ser também a de
        // quem está logado, e aí ela é dita uma vez só, como a do cliente
        var origens = pedacos
            .Where(PedacoLembrado)
            .SelectMany(x => x.Chaves)
            .Where(EhLembrado)
            .Where(c => _deOndeVeio.GetValueOrDefault(c, "").Length > 0)
            .GroupBy(c => _deOndeVeio[c])
            .OrderBy(g => g.All(_dosPadroes.Contains))
            .Select(g => Origem(g.Key, g.All(_dosPadroes.Contains)))
            .ToList();

        return string.Join(" e ", origens);
    }

    // =====================================================================
    // contatos Howden: cartão de leitura, "trocar" e "editar"
    // =====================================================================

    /// <summary>O contato (1 ou 2) com o formulário aberto pelo "editar"; 0, nenhum.</summary>
    private int _editandoContato;

    /// <summary>O contato (1 ou 2) com a lista do quadro aberta pelo "trocar"; 0, nenhum.</summary>
    private int _trocandoContato;

    private void TrocarContato(int qual)
    {
        SincronizarCondicoes();
        _editandoContato = 0;
        _trocandoContato = _trocandoContato == qual ? 0 : qual;
        if (_trocandoContato != 0) Contar("contato:trocar");
    }

    private void EditarContato(int qual)
    {
        SincronizarCondicoes();
        _trocandoContato = 0;
        _editandoContato = _editandoContato == qual ? 0 : qual;
        if (_editandoContato == 0) return;
        if (qual == 1) Editou("contato howden");
        Contar("contato:editar");
    }

    private void FecharContato()
    {
        _trocandoContato = 0;
        _editandoContato = 0;
        if (_proposta.Contato2Nome.Trim().Length == 0) _mostrarContato2 = false;
    }

    /// <summary>A escolha na lista do quadro: os campos vêm juntos e a lista fecha.</summary>
    private void EscolherDoQuadro(string nome, int qual)
    {
        EscolherContato(nome, qual);
        if (qual == 1) Editou("contato howden");
        _trocandoContato = 0;
        if (qual == 2 && _proposta.Contato2Nome.Trim().Length == 0) _mostrarContato2 = false;
    }

    private (string Nome, string Cargo, string Area, string Email, string Telefones) DadosDoContato(int qual) =>
        qual == 1
            ? (_proposta.ContatoNome, _proposta.ContatoCargo, _proposta.ContatoArea, _proposta.ContatoEmail, _proposta.ContatoTelefones)
            : (_proposta.Contato2Nome, _proposta.Contato2Cargo, _proposta.Contato2Area, _proposta.Contato2Email, _proposta.Contato2Telefones);

    /// <summary>O "editar" do contato: os mesmos campos de antes, um a um.</summary>
    private void MudarContato(int qual, string campo, string valor)
    {
        var p = _proposta;
        switch (campo, qual)
        {
            case ("nome", 1): p.ContatoNome = valor; break;
            case ("cargo", 1): p.ContatoCargo = valor; break;
            case ("area", 1): p.ContatoArea = valor; break;
            case ("email", 1): p.ContatoEmail = valor; break;
            case ("telefones", 1): p.ContatoTelefones = valor; break;
            case ("nome", 2): p.Contato2Nome = valor; break;
            case ("cargo", 2): p.Contato2Cargo = valor; break;
            case ("area", 2): p.Contato2Area = valor; break;
            case ("email", 2): p.Contato2Email = valor; break;
            case ("telefones", 2): p.Contato2Telefones = valor; break;
        }
    }

    private void AcrescentarContato2()
    {
        _mostrarContato2 = true;
        _editandoContato = 0;
        _trocandoContato = 2;
    }

    private void TirarContato2()
    {
        EscolherContato("", 2);
        _mostrarContato2 = false;
        FecharContato();
    }
}
