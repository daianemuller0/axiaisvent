using System.Text.Json;
using System.Text.RegularExpressions;
using HowdenAxiais.Poc.Data;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Routing;
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
    /// aumentam o que o olho precisa varrer — quem quer, liga no "Mostrar
    /// ajuda" (menu Mais), e a escolha fica lembrada no navegador.
    /// </summary>
    private bool _compacto = true;

    /// <summary>O próximo número, para a dica do campo — calculado ao recarregar, e não a cada desenho.</summary>
    private string _proximoNumero = "";

    private static string NomeDaEtapa(int n) => Etapas.FirstOrDefault(e => e.Numero == n).Nome ?? "";

    private void IrParaEtapa(int n)
    {
        var antes = _etapa;
        _etapa = Math.Clamp(n, 1, Etapas.Length);
        _rolarTopo = true;

        // a etapa Começar mostra as gravadas: a lista vem com o que foi gravado
        // desde a última vez, e não como estava ao abrir a tela
        if (_etapa == 1 && antes != 1) AtualizarGravadas();
    }

    /// <summary>O id do começo do conteúdo do equipamento, logo abaixo da faixa fixa.</summary>
    private const string ConteudoDoEquipamento = "equip-conteudo";

    /// <summary>
    /// Abre um equipamento pela faixa fixa (ou pelo rodapé). Se a tela estava
    /// rolada lá embaixo, ela volta ao começo do conteúdo dele — e só nesse
    /// caso: com o começo à vista, nada se mexe (ver appRolarPara).
    /// </summary>
    private void AbrirEquipamento(int indice)
    {
        _aberto = indice;
        _apagandoItem = null;
        _rolarPara = ConteudoDoEquipamento;
    }

    /// <summary>Troca a aba do equipamento aberto (Escopo, Opcionais, Técnica), com a mesma regra de rolagem.</summary>
    private void TrocarAba(string aba)
    {
        _abaItem = aba;
        _rolarPara = ConteudoDoEquipamento;
    }

    // ---------- o caminho da etapa Equipamentos ----------

    /// <summary>Um passo do rodapé da etapa 3: um equipamento e uma aba dele.</summary>
    private sealed record Passo(int Item, string Aba);

    /// <summary>
    /// A aba Opcionais só entra no caminho do rodapé quando algum equipamento
    /// já tem opcional: numa proposta sem opcionais ela seria dois cliques a
    /// mais em cada equipamento. Sem ela no caminho, fica um link ao lado do botão.
    /// </summary>
    private bool OpcionaisNoCaminho => _proposta.Itens.Any(i =>
        i.Opcionais.Values.Any(v => v.Trim().Length > 0)
        || i.InstrumentacaoOpcional.Count > 0
        || i.MotorOpcionalId.Trim().Length > 0);

    /// <summary>Escopo → (Opcionais) → Técnica de cada equipamento, um depois do outro.</summary>
    private List<Passo> CaminhoDosEquipamentos()
    {
        var abas = OpcionaisNoCaminho
            ? new[] { "escopo", "opcionais", "tecnica" }
            : new[] { "escopo", "tecnica" };
        return Enumerable.Range(0, _proposta.Itens.Count)
            .SelectMany(i => abas.Select(a => new Passo(i, a)))
            .ToList();
    }

    /// <summary>A ordem de um passo — vale também para a aba Opcionais aberta à mão, fora do caminho.</summary>
    private static int OrdemDoPasso(Passo p) => p.Item * 3 + p.Aba switch
    {
        "escopo" => 0,
        "opcionais" => 1,
        _ => 2,
    };

    private Passo PassoAtual => new(PosicaoValida, _abaItem);

    /// <summary>O próximo passo do rodapé; nulo no fim (a Técnica do último equipamento), que é a vez do Preço.</summary>
    private Passo? ProximoPasso()
    {
        var aqui = OrdemDoPasso(PassoAtual);
        return CaminhoDosEquipamentos().FirstOrDefault(p => OrdemDoPasso(p) > aqui);
    }

    /// <summary>O passo anterior; nulo no Escopo do equipamento 1, que volta à etapa Cliente.</summary>
    private Passo? PassoAnterior()
    {
        var aqui = OrdemDoPasso(PassoAtual);
        return CaminhoDosEquipamentos().LastOrDefault(p => OrdemDoPasso(p) < aqui);
    }

    /// <summary>"Técnica do equip. 1", "Equip. 2" (o escopo de outro equipamento é ele inteiro).</summary>
    private string RotuloDoPasso(Passo p)
    {
        var numero = p.Item + 1;
        return p.Aba switch
        {
            "escopo" when p.Item != PosicaoValida => $"Equip. {numero}",
            "escopo" => $"Escopo do equip. {numero}",
            "opcionais" => $"Opcionais do equip. {numero}",
            _ => $"Técnica do equip. {numero}",
        };
    }

    private void IrAoPasso(Passo p)
    {
        if (p.Item != PosicaoValida) AbrirEquipamento(p.Item);
        TrocarAba(p.Aba);
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
                if (alvo == ConteudoDoEquipamento) await JS.InvokeVoidAsync("appChipAtivo");
            }

            if (_irAoCampo is { } campo)
            {
                _irAoCampo = null;
                // o campo pode não estar desenhado (o motor com o "Com"
                // desligado): aí a tela fica no topo da aba, sem erro
                if (!await JS.InvokeAsync<bool>("appIrAoCampo", campo))
                    await JS.InvokeVoidAsync("appTopo");
                if (_etapa == 3) await JS.InvokeVoidAsync("appChipAtivo");
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
    // moldura: topo, menus e o aviso de três níveis
    // =====================================================================

    /// <summary>"Cliente · Projeto" do subtítulo, numa linha só.</summary>
    private string ClienteEProjeto
    {
        get
        {
            var partes = new[] { _proposta.Cliente, _proposta.Projeto }
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
                .ToList();
            return partes.Count > 0 ? string.Join(" · ", partes) : "sem cliente ainda";
        }
    }

    /// <summary>"03/11": a data do chip de enviada (o ano está no número da proposta).</summary>
    private static string DiaEMes(string texto) =>
        DateTime.TryParse(texto, out var data) ? data.ToString("dd/MM") : texto;

    /// <summary>O menu Mais do topo está aberto.</summary>
    private bool _menuAberto;

    /// <summary>Qual botão "Gerar tudo" está com a seta aberta ("lado", e depois a etapa Documentos).</summary>
    private string? _gerarAberto;

    /// <summary>Um menu acabou de abrir: depois de desenhar, o foco vai para o primeiro item.</summary>
    private string? _menuParaLigar;

    /// <summary>Um item de menu foi escolhido: o foco volta ao botão que abriu o menu.</summary>
    private string? _focarDepois;

    private void AlternarMenu()
    {
        _gerarAberto = null;
        _menuAberto = !_menuAberto;
        _menuParaLigar = _menuAberto ? "menu-mais" : null;
    }

    private void AlternarGerar(string onde)
    {
        _menuAberto = false;
        _gerarAberto = _gerarAberto == onde ? null : onde;
        _menuParaLigar = _gerarAberto is null ? null : $"menu-gerar-{onde}";
    }

    /// <summary>Clicou fora, Esc ou Tab (ver appMenuPop no app.js): os menus fecham.</summary>
    [JSInvokable]
    public Task FecharMenus() => InvokeAsync(() =>
    {
        if (!_menuAberto && _gerarAberto is null) return;
        _menuAberto = false;
        _gerarAberto = null;
        StateHasChanged();
    });

    /// <summary>Um item do menu Mais: fecha o menu e faz.</summary>
    private void DoMenu(Action fazer)
    {
        _menuAberto = false;
        _gerarAberto = null;
        _focarDepois = "botao-mais";
        fazer();
    }

    private async Task DoMenu(Func<Task> fazer)
    {
        _menuAberto = false;
        _gerarAberto = null;
        _focarDepois = "botao-mais";
        await fazer();
    }

    /// <summary>Um documento avulso pela seta do Gerar tudo.</summary>
    private async Task DoMenuGerar(string onde, Func<Task<bool>> gerar)
    {
        _menuAberto = false;
        _gerarAberto = null;
        _focarDepois = $"seta-gerar-{onde}";
        await gerar();
    }

    /// <summary>Liga o teclado do menu que abriu e devolve o foco depois de escolher.</summary>
    private async Task LigarMenus()
    {
        try
        {
            if (_menuParaLigar is { } menu)
            {
                _menuParaLigar = null;
                if (_ref is not null) await JS.InvokeVoidAsync("appMenuPop.abrir", menu, _ref);
            }

            if (_focarDepois is { } botao)
            {
                _focarDepois = null;
                await JS.InvokeVoidAsync("appMenuPop.focar", botao);
            }
        }
        catch (Exception)
        {
            // sem o teclado do menu, o clique continua funcionando
        }
    }

    /// <summary>Pode ficar até alguém fechar: consequência, erro ou aviso com ação.</summary>
    private bool _avisoFica;

    /// <summary>O botão do aviso (ex.: "Marcar como enviada" depois do Gerar tudo).</summary>
    private (string Rotulo, Action Fazer)? _avisoAcao;

    /// <summary>O relógio do aviso de sucesso que some sozinho.</summary>
    private CancellationTokenSource? _avisoRelogio;

    /// <summary>Quanto tempo o aviso de sucesso fica na tela.</summary>
    private static readonly TimeSpan AvisoSome = TimeSpan.FromSeconds(6);

    /// <summary>
    /// O aviso do pé da coluna, em três níveis.
    /// <list type="bullet">
    /// <item>Sucesso (o padrão): some em 6 s — "Proposta gravada" não precisa
    /// de ninguém para fechar.</item>
    /// <item>Consequência (<paramref name="fica"/>): fica até alguém fechar ou
    /// até o próximo aviso — "Modelo trocado: o preço à mão do anterior foi
    /// descartado" diz algo que mudou sem a pessoa pedir. Todo aviso com
    /// <paramref name="acao"/> também fica.</item>
    /// <item>Erro (<paramref name="erro"/>): fica até alguém fechar. O Gerar
    /// tudo lê o _msg logo depois de cada gerador, e o erro nunca some antes.</item>
    /// </list>
    /// Um "desfazer" nunca mora só aqui: ele aparece também na linha afetada.
    /// </summary>
    private void Aviso(string texto, bool erro = false, bool fica = false,
        (string Rotulo, Action Fazer)? acao = null)
    {
        _avisoRelogio?.Cancel();
        _avisoRelogio = null;

        _msg = texto;
        _erro = erro;
        _avisoAcao = acao;
        _avisoFica = erro || fica || acao is not null;

        if (_avisoFica) return;

        var relogio = new CancellationTokenSource();
        _avisoRelogio = relogio;
        _ = SumirDepois(texto, relogio.Token);
    }

    private async Task SumirDepois(string texto, CancellationToken cancelar)
    {
        try
        {
            await Task.Delay(AvisoSome, cancelar);
            await InvokeAsync(() =>
            {
                // outro aviso entrou no lugar (ou alguém fechou): não é este que some
                if (cancelar.IsCancellationRequested || _msg != texto) return;

                _msg = null;
                _avisoAcao = null;
                // quem desenhou foi o relógio, não a pessoa: o cronômetro não conta
                _desenhoDoSistema = true;
                StateHasChanged();
            });
        }
        catch (Exception)
        {
            // cancelado, ou a tela já fechou: o aviso fica como está
        }
    }

    private void FecharAviso()
    {
        _avisoRelogio?.Cancel();
        _avisoRelogio = null;
        _msg = null;
        _avisoAcao = null;
    }

    private void FazerAcaoDoAviso()
    {
        var acao = _avisoAcao;
        FecharAviso();
        acao?.Fazer();
    }

    // =====================================================================
    // pendências
    // =====================================================================

    /// <summary>
    /// Uma coisa que falta para os documentos saírem completos, e onde ela se
    /// resolve: a etapa e, quando é de um equipamento, qual e em que aba.
    /// <para><paramref name="Alvo"/> é o id do campo na tela (f-cliente,
    /// f-modelo, f-custo-0-motor…): a pendência leva até ele, e não só à etapa —
    /// achar o campo numa aba comprida era metade do trabalho.</para>
    /// <para><paramref name="SoAviso"/> é o que sai em branco no documento mas
    /// não impede a proposta de estar pronta (a aplicação, a montagem, a
    /// referência do cliente). Fica fora da contagem, do selo da etapa e do
    /// "Nada pendente", num grupo recolhido: uma proposta preliminar pode ficar
    /// sem pendência, e a equipe não aprende a ignorar o painel.</para>
    /// </summary>
    private sealed record Pendencia(string Texto, int Etapa, int Item = -1, string Aba = "",
        string Alvo = "", bool SoAviso = false);

    /// <summary>O campo para onde a pendência leva, depois de desenhar (ver appIrAoCampo no app.js).</summary>
    private string? _irAoCampo;

    private void IrPara(Pendencia p)
    {
        IrParaEtapa(p.Etapa);
        if (p.Item >= 0)
        {
            _aberto = p.Item;
            _apagandoItem = null;
        }
        if (p.Aba.Length > 0) _abaItem = p.Aba;

        AntesDeIrAoCampo(p);

        // com um campo, a tela vai até ele em vez de voltar ao topo; se ele não
        // estiver desenhado (o motor com o "Com" desligado), fica no topo
        if (p.Alvo.Length == 0) return;
        _rolarTopo = false;
        _irAoCampo = p.Alvo;
    }

    /// <summary>O id do campo de custo de uma linha: o índice do equipamento evita id repetido na etapa Preço, que mostra todos.</summary>
    private static string AlvoDoCusto(int indice, string chave) => $"f-custo-{indice}-{Slug(chave)}";

    /// <summary>"lista:Cone de entrada" → "lista-Cone-de-entrada": o que não é letra nem número vira hífen.</summary>
    private static string Slug(string texto) =>
        new(texto.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray());

    /// <summary>
    /// Tudo o que os documentos deixariam em branco, de todas as etapas. Não
    /// impede de gerar — muita proposta sai para conferência antes de fechar —,
    /// mas fica à vista o tempo todo, em vez de aparecer só ao abrir o Word.
    /// Os avisos (<see cref="Pendencia.SoAviso"/>) vêm na mesma lista; quem
    /// conta separa.
    /// </summary>
    private List<Pendencia> Pendencias()
    {
        var lista = new List<Pendencia>();

        void Falta(bool falta, string texto, int etapa, string alvo)
        {
            if (falta) lista.Add(new Pendencia(texto, etapa, Alvo: alvo));
        }

        void SoAvisa(bool falta, string texto, int etapa, string alvo)
        {
            if (falta) lista.Add(new Pendencia(texto, etapa, Alvo: alvo, SoAviso: true));
        }

        Falta(_proposta.Cliente.Trim().Length == 0, "Cliente", 2, "f-cliente");
        Falta(_proposta.AosCuidados.Trim().Length == 0, "Aos cuidados de", 2, "f-aos-cuidados");
        Falta(_proposta.Cidade.Trim().Length == 0, "Cidade do cliente", 2, "f-cidade");
        Falta(EhBrasil && _proposta.Estado.Trim().Length == 0, "Estado / UF (ICMS do pricing)", 2, "f-uf");
        Falta(_proposta.CondicaoPagamento.Trim().Length == 0, "Condição de pagamento", 2, "f-pagamento");
        Falta(_proposta.Incoterm.Trim().Length == 0, "Entrega (incoterm)", 2, "f-incoterm");
        Falta(_proposta.Incoterm.Trim().Length > 0 && !_incotermLivre && PedeDestino
              && _proposta.IncotermDestino.Trim().Length == 0, RotuloDoDestino, 2, "f-destino-incoterm");
        Falta(_proposta.Armado.Trim().Length == 0, "Armado ou desarmado", 2, "f-armado");
        Falta(_proposta.ContatoNome.Trim().Length == 0, "Contato Howden", 2, "f-contato");

        // saem no cabeçalho de todas as páginas, mas a proposta preliminar
        // costuma ir sem elas: avisam, não pendem
        SoAvisa(_proposta.ReferenciaCliente.Trim().Length == 0, "Referência do cliente", 2, "f-referencia");
        SoAvisa(_proposta.Projeto.Trim().Length == 0, "Projeto", 2, "f-projeto");

        Falta(_proposta.Itens.Count == 0, "Nenhum equipamento no escopo", 3, "f-equipamento-novo");
        for (var i = 0; i < _proposta.Itens.Count; i++)
        {
            var indice = i;
            var item = _proposta.Itens[indice];
            var nome = $"Equip. {indice + 1}";

            void DoItem(string texto, string aba, string alvo, bool soAviso = false) =>
                lista.Add(new Pendencia($"{nome}: {texto}", 3, indice, aba, alvo, soAviso));

            // sem modelo, o resto continua sendo conferido (antes a conta parava
            // aqui, e o painel dizia "2 linhas sem custo" enquanto a etapa Preço
            // dizia 6). Só o custo e o motor esperam o modelo: é pelo diâmetro
            // que as listas se precificam, e sem série o catálogo de motores
            // aceita as duas linhas
            var temModelo = ModeloDe(item) is not null;

            if (!temModelo)
                DoItem("escolher o modelo", "escopo", "f-modelo");
            else if (item.ComMotor && MotorDe(item) is null)
                DoItem("motor ainda não definido", "escopo", "f-motor");

            if (item.ComPartidor && item.Partidor.Trim().Length == 0)
                DoItem("escolher o partidor", "escopo", "f-partidor");

            if (temModelo)
            {
                // a pendência leva à PRIMEIRA linha sem custo, na ordem da tela
                var semCusto = LinhasDePreco(item).Where(l => Efetivo(item, l.Chave, l.Puxado) is null).ToList();
                if (semCusto.Count > 0)
                    DoItem(Plural(semCusto.Count, "linha sem custo", "linhas sem custo"), "escopo",
                        AlvoDoCusto(indice, semCusto[0].Chave));
            }

            // saem em branco na técnica (Aplicación e Tipo de montaje), mas não
            // impedem a proposta de sair
            if (item.Aplicacao.Trim().Length == 0)
                DoItem("aplicação", "escopo", "f-aplicacao", soAviso: true);
            if (item.Arranjo.Trim().Length == 0 || item.Montagem.Trim().Length == 0)
                DoItem("arranjo/montagem", "escopo", "f-montagem", soAviso: true);

            PendenciasDoEscopo(lista, item, indice);

            if (item.Tecnicos.Count == 0)
                DoItem("dados da seleção", "tecnica", "f-selecao");

            if (item.Curvas.Count == 0)
                DoItem("curva de performance", "tecnica", "f-curva");

            PendenciasDaTecnica(lista, item, indice);
        }

        Falta(Cambio.FaltaTaxa(_proposta), "Taxa de câmbio", 4, "f-taxa");

        return lista;
    }

    /// <summary>
    /// As pendências do painel, por lugar: as da proposta (Cliente e
    /// condições, Preço) e as de cada equipamento ("Equip. 2 · 3"). Na ordem
    /// em que aparecem na lista, que é a ordem das etapas.
    /// </summary>
    private static List<(string Nome, int Item, List<Pendencia> Itens)> PorLugar(IEnumerable<Pendencia> pendencias) =>
        pendencias
            .GroupBy(p => p.Item >= 0 ? $"Equip. {p.Item + 1}" : NomeDaEtapa(p.Etapa))
            .Select(g => (g.Key, g.First().Item, g.ToList()))
            .ToList();

    /// <summary>O texto dentro do grupo do equipamento, sem repetir o "Equip. 2: " do título.</summary>
    private static string SemOEquipamento(Pendencia p)
    {
        var prefixo = $"Equip. {p.Item + 1}: ";
        return p.Item >= 0 && p.Texto.StartsWith(prefixo, StringComparison.Ordinal) ? p.Texto[prefixo.Length..] : p.Texto;
    }

    // ---------- ganchos da fase B ----------
    //
    // Cada pista da fase B escreve num arquivo próprio da classe parcial (ver
    // mapa-fase-b.md) e liga o que precisa por estes ganchos, sem mexer nas
    // linhas que chamam: assim duas pistas não editam o mesmo trecho. Sem
    // implementação, o compilador apaga a chamada.

    /// <summary>Pendências e avisos da aba Técnica de um equipamento. Dona: a pista da Técnica (item 09: diâmetro, altitude, ruído).</summary>
    partial void PendenciasDaTecnica(List<Pendencia> lista, ItemProposta item, int indice);

    /// <summary>Pendências e avisos da aba Escopo de um equipamento. Dona: a pista do Escopo (item 13: "k listas a decidir").</summary>
    partial void PendenciasDoEscopo(List<Pendencia> lista, ItemProposta item, int indice);

    /// <summary>Antes de ir ao campo de uma pendência. Dona: a pista do Cliente (item 12: a de condições abre a edição).</summary>
    partial void AntesDeIrAoCampo(Pendencia p);

    // Um gancho tem UMA pista dona (só uma implementação compila); quando duas
    // áreas precisam do mesmo momento, cada uma tem o seu gancho, chamados um
    // depois do outro no mesmo lugar.

    /// <summary>
    /// O primeiro modelo de um equipamento acabou de ser escolhido, logo depois
    /// do escopo padrão da série (TrocarModelo). Dona: a pista do Escopo — o
    /// escopo inicial do item 15 (que também decide o que fazer com o
    /// AplicarPadraoSeHouver, chamado logo antes).
    /// </summary>
    partial void DepoisDoPrimeiroModelo(ItemProposta item);

    /// <summary>Mesmo momento, para a pista da Técnica: o diâmetro e a altitude lembrados (item 09).</summary>
    partial void DadosTecnicosDoPrimeiroModelo(ItemProposta item);

    /// <summary>
    /// Fim de TrocarModelo, a cada troca (o primeiro modelo inclusive, depois
    /// dos dois ganchos acima). Dona: a pista do Motor — o motor fecha pelo
    /// modelo novo quando já está ligado (item 16).
    /// </summary>
    partial void DepoisDeTrocarModelo(ItemProposta item, bool primeiroModelo);

    /// <summary>A seleção acabou de entrar no equipamento (fim de AplicarSelecao). Dona: a pista do Motor (item 16).</summary>
    partial void DepoisDaSelecao(ItemProposta item);

    /// <summary>
    /// A dica só de leitura sob o campo de custo (Preco): "último à mão: CLP
    /// 1.340.000 · PRP-2026-104 · 12/09/2026". Nula, nada aparece. Dona: a pista
    /// do item 22.
    /// </summary>
    partial void DicaDoCusto(ItemProposta item, string chave, decimal? puxado, ref string? dica);

    /// <summary>
    /// Fecha o motor pela seleção e pelo histórico do mesmo cliente, quando
    /// dá (item 16). Só declarado aqui: a pista do Motor implementa e chama (do
    /// "Com" do motor e dos dois ganchos acima); outra pista pode chamar sem
    /// esperar — até a junção, a chamada não faz nada.
    /// </summary>
    partial void FecharMotorSeDer(ItemProposta item);

    private enum Selo { Neutro, Pendente, Ok }

    /// <summary>
    /// O selo de uma etapa na barra. Antes o visto aparecia em etapa vazia (o
    /// Preço com custo zero tinha visto); agora ele só vem com conteúdo — o
    /// cliente na etapa 2, um equipamento na 3, custo na 4 e, na 5, um
    /// equipamento e nada pendente em lugar nenhum. Sem conteúdo, a etapa fica
    /// sem selo: ainda não começou, não está "pendente".
    /// </summary>
    private Selo SeloDaEtapa(int etapa, int faltamNela, int faltamNoTotal)
    {
        var temConteudo = etapa switch
        {
            2 => _proposta.Cliente.Trim().Length > 0,
            3 or 5 => _proposta.Itens.Count > 0,
            4 => Total > 0m,
            _ => false,
        };

        if (!temConteudo) return Selo.Neutro;
        if (etapa == 5) return faltamNoTotal == 0 ? Selo.Ok : Selo.Neutro;
        return faltamNela > 0 ? Selo.Pendente : Selo.Ok;
    }

    /// <summary>Há o que gravar: o que está na tela é diferente do que está no banco.</summary>
    private bool TemAlteracao => Foto() != _baseSalva;

    /// <summary>"2 itens · 3 un.": as linhas da proposta e, somadas, as unidades.</summary>
    private string ItensEUnidades
    {
        get
        {
            var itens = _proposta.Itens.Count;
            if (itens == 0) return "—";
            return $"{itens} {(itens == 1 ? "item" : "itens")} · {CustoDaProposta.Quantidade(_proposta)} un.";
        }
    }

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
        Salvar(explicito: true);
        StateHasChanged();
    });

    /// <summary>
    /// Liga o Ctrl+S, a guarda dos links do menu e lê a preferência de ajuda.
    /// Sem eles, a tela funciona igual.
    /// </summary>
    private async Task LigarAtalhos()
    {
        try
        {
            _ref ??= DotNetObjectReference.Create(this);
            await JS.InvokeVoidAsync("appAtalhos.ligar", _ref);
            await JS.InvokeVoidAsync("appGuarda.ligar", _ref);

            var ajuda = await JS.InvokeAsync<string?>("appPreferencia.ler", "ajuda");
            if (ajuda == "mostrar")
            {
                _compacto = false;
                // não foi a pessoa que mexeu: o cronômetro não conta este desenho
                _desenhoDoSistema = true;
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
        _guardaDeSaida?.Dispose();
        _avisoRelogio?.Cancel();

        try
        {
            await JS.InvokeVoidAsync("appAtalhos.desligar");
            await JS.InvokeVoidAsync("appGuarda.desligar");
        }
        catch (Exception)
        {
            // a aba já fechou: não há o que desligar
        }

        _ref?.Dispose();
    }

    // =====================================================================
    // alteração não salva: nada se perde sem perguntar
    // =====================================================================

    /// <summary>
    /// O que a pessoa pediu e espera a resposta da barra "Há alterações não
    /// salvas…": começar outra, abrir, copiar, sair da tela. Nulo: sem barra.
    /// </summary>
    private Action? _depoisDeConfirmar;

    /// <summary>Para onde a pessoa ia quando a guarda segurou a saída da tela.</summary>
    private string? _saindoPara;

    /// <summary>A saída já foi respondida (Salvar ou Descartar): a guarda deixa passar.</summary>
    private bool _saidaLiberada;

    private IDisposable? _guardaDeSaida;

    /// <summary>
    /// Há o que perder: o que está na tela não foi gravado, e não é a proposta
    /// em branco como nasceu (essa não tem nada a perder).
    /// </summary>
    private bool PrecisaConfirmar
    {
        get
        {
            var atual = Foto();
            return atual != _baseSalva && atual != _padraoDaNova;
        }
    }

    /// <summary>
    /// Faz a ação já, ou — com alteração não salva — guarda a ação e mostra a
    /// barra. Antes, '+ Nova', abrir ou copiar outra jogavam fora o que não
    /// tinha sido gravado, sem aviso. Nunca grava sozinho: quem decide é a pessoa.
    /// </summary>
    private void Proteger(Action acao)
    {
        if (!PrecisaConfirmar)
        {
            acao();
            return;
        }

        _depoisDeConfirmar = acao;
        _saindoPara = null;
        LimparSucesso();
    }

    private string TextoDaGuarda
    {
        get
        {
            if (_proposta.Id.Length == 0 || _proposta.Numero.Trim().Length == 0)
                return "Há alterações não salvas nesta proposta nova.";

            var enviada = _proposta.Enviada && DateTime.TryParse(_proposta.EnviadaEm, out var quando)
                ? $", já enviada em {quando:dd/MM}"
                : "";
            return $"Há alterações não salvas em {_proposta.Numero}{enviada}.";
        }
    }

    private void FecharGuarda()
    {
        _depoisDeConfirmar = null;
        _saindoPara = null;
    }

    /// <summary>Salvar, na barra: grava e só então faz o que foi pedido.</summary>
    private void GuardaSalvar()
    {
        var acao = _depoisDeConfirmar;
        FecharGuarda();

        // não gravou (ex.: a revisão já existe): o erro fica à vista e nada mais acontece
        if (!Salvar(explicito: true)) return;

        var gravou = _msg;
        acao?.Invoke();

        // "gravada" e o que veio depois ("Proposta nova…") no mesmo aviso
        if (gravou is not null && _msg is not null && _msg != gravou && !_erro)
            Aviso($"{gravou} {_msg}");
    }

    /// <summary>
    /// Descartar, na barra: a proposta volta ao que está gravado, o rascunho do
    /// navegador some (senão ela voltaria ao abrir a tela) e a ação segue.
    /// </summary>
    private async Task GuardaDescartar()
    {
        var acao = _depoisDeConfirmar;
        var saindo = _saindoPara is not null;
        FecharGuarda();

        try
        {
            var chave = _proposta.Id.Length > 0 ? _proposta.Id : "novo";
            await JS.InvokeVoidAsync("appRascunho.limpar", chave);
            if (_chaveRascunho.Length > 0 && _chaveRascunho != chave)
                await JS.InvokeVoidAsync("appRascunho.limpar", _chaveRascunho);
        }
        catch (Exception)
        {
            // sem o navegador, o rascunho fica; ele só oferece de volta o que não foi gravado
        }

        // uma gravada volta ao que está no banco: a cópia (Duplicar) sai dela,
        // e não do que acabou de ser descartado
        if (_proposta.Id.Length > 0 && JsonSerializer.Deserialize<Proposta>(_baseSalva) is { } gravada)
        {
            _proposta = gravada;
            _revisaoAberta = gravada.Revisao;
            AjustarEscolhasLivres();
        }

        // saindo da tela, nenhum desenho daqui em diante escreve o rascunho de novo
        if (saindo) _rascunhoPronto = false;

        acao?.Invoke();
    }

    /// <summary>
    /// Saída da tela pelo próprio servidor (ex.: o botão Propostas). Os links do menu
    /// do app não passam por aqui — a navegação deles é do navegador — e chegam
    /// por <see cref="PedirParaSair"/>.
    /// </summary>
    private ValueTask AoSairDaTela(LocationChangingContext contexto)
    {
        if (_saidaLiberada || !PrecisaConfirmar) return ValueTask.CompletedTask;

        contexto.PreventNavigation();
        SegurarSaida(contexto.TargetLocation);
        return new ValueTask(InvokeAsync(StateHasChanged));
    }

    /// <summary>
    /// Um link do menu do app foi clicado (ver appGuarda no app.js). Sem nada a
    /// perder, sai na hora; com alteração não salva, mostra a barra.
    /// </summary>
    [JSInvokable]
    public Task PedirParaSair(string destino) => InvokeAsync(() =>
    {
        if (PrecisaConfirmar)
        {
            SegurarSaida(destino);
            StateHasChanged();
            return;
        }

        Sair(destino);
    });

    private void SegurarSaida(string destino)
    {
        _saindoPara = destino;
        _depoisDeConfirmar = () => Sair(destino);
        LimparSucesso();
    }

    /// <summary>
    /// A barra de alteração não salva entrou: um "gravada" ou "Nada mudou" de
    /// segundos atrás, embaixo, contradiria a barra. Consequência e erro ficam.
    /// </summary>
    private void LimparSucesso()
    {
        if (_msg is not null && !_avisoFica) FecharAviso();
    }

    private void Sair(string destino)
    {
        _saidaLiberada = true;
        Navegacao.NavigateTo(destino);
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
        DepoisDeGerarTudo(falhas.Count == 0);

        if (falhas.Count == 0)
        {
            // quem gera tudo costuma mandar ao cliente em seguida: o "Marcar
            // como enviada" fica no caminho, sem ir ao menu Mais
            var texto = $"Proposta {_proposta.Numero} salva e documentos baixados: {string.Join(", ", feitos)}.";
            if (_proposta.Enviada || NovaRevisaoPendente) Aviso(texto);
            else Aviso(texto, acao: ("Marcar como enviada", MarcarEnviada));
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

    // as quatro trocam a proposta da tela: com alteração não salva, a barra pergunta antes

    private void NovaEmBranco() => Proteger(() =>
    {
        Nova();
        IrParaEtapa(1);
    });

    private void NovaEContinuar() => Proteger(() =>
    {
        Nova();
        IrParaEtapa(2);
    });

    private void AbrirGravada(string id) => Proteger(() =>
    {
        Abrir(id);
        if (_proposta.Id == id) IrParaEtapa(2);
    });

    private void DuplicarGravada(string id) => Proteger(() =>
    {
        Abrir(id);
        if (_proposta.Id == id) DuplicarAgora();
    });

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

        Proteger(DuplicarAgora);
    }

    private void DuplicarAgora()
    {
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
            // o ruído é do ponto de operação, como a seleção; a altitude é da
            // mina, e o projeto da cópia é outro. O diâmetro, que é do modelo, fica
            item.RuidoDb = "";
            item.AltitudeMsnm = "";
            // RespondidasNaFolha e FiltroAuto vão junto, como o escopo e o
            // filtro do motor de que eles falam
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
        _origem = "copia";
        IrParaEtapa(2);

        Aviso($"Cópia da {origem}: confira referência, projeto e data de fechamento. A seleção e a " +
              "curva de cada equipamento ficaram de fora — suba as desta proposta (etapa Começar ou " +
              "aba Técnica do equipamento).", fica: true);
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
            $"{cliente.Nome} tem {Plural(cliente.Propostas, "proposta gravada", "propostas gravadas")}; a última é a " +
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

    /// <summary>Quando a equipe gravou na aba Conversão a taxa que entrou aqui (ISO).</summary>
    private string _taxaAtualizadaEm = "";

    /// <summary>A partir de quantos dias a taxa da aba Conversão pede conferência.</summary>
    private const int DiasDaTaxa = 7;

    private DateTime? TaxaAtualizadaEm =>
        DateTime.TryParse(_taxaAtualizadaEm, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var data) ? data : null;

    /// <summary>A taxa trazida tem mais de 7 dias: o câmbio pode ter andado desde então.</summary>
    private bool TaxaVelha => TaxaAtualizadaEm is { } data && (DateTime.Now - data).TotalDays > DiasDaTaxa;

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
        var taxas = Conversao.Ler();
        if (ConversaoRepository.Taxa(taxas.ClpPorUsd) is not { } clpPorUsd) return;

        var custo = _proposta.Moeda;
        var venda = _proposta.MoedaVenda;

        if (custo == Moeda.Clp && venda == Moeda.Usd) _proposta.CambioDirecao = "VendaEmCusto";
        else if (custo == Moeda.Usd && venda == Moeda.Clp) _proposta.CambioDirecao = "CustoEmVenda";
        else return;

        _proposta.TaxaCambio = clpPorUsd.ToString("0.####", Br);
        _taxaDoDia = true;
        _taxaAtualizadaEm = taxas.AtualizadaEm;
    }

    // =====================================================================
    // cronômetro
    // =====================================================================

    /// <summary>Uma pausa maior que isto (café, telefone) não conta como trabalho na proposta.</summary>
    private const double PausaQueNaoConta = 180;

    /// <summary>
    /// Desenhos mais próximos que isto são a mesma ação: o Gerar tudo, a
    /// importação e o que volta do navegador desenham a tela várias vezes por
    /// um clique só, e cada desenho contava como uma interação.
    /// </summary>
    private const double MesmaInteracao = 0.4;

    private DateTime _cronInicio = DateTime.Now;
    private DateTime? _cronUltimo;
    private double _cronSegundos;
    private int _cronInteracoes;

    /// <summary>
    /// Os segundos em cada etapa (a Equipamentos, por aba: "3-escopo",
    /// "3-opcionais", "3-tecnica"). É o que diz ONDE a proposta demora — o
    /// total sozinho não mostra se uma mudança de tela encurtou a etapa dela.
    /// </summary>
    private Dictionary<string, double> _segundosPorEtapa = new();

    /// <summary>A etapa que estava na tela no desenho anterior: o tempo até o próximo é dela.</summary>
    private string _chaveDoUltimo = "";

    /// <summary>Quantas vezes cada atalho da tela foi usado (ver <see cref="Contar"/>).</summary>
    private Dictionary<string, int> _contadores = new();

    /// <summary>
    /// O próximo desenho não foi a pessoa: foi um timer, o aviso que some
    /// sozinho, a preferência lida do navegador. Quem chama StateHasChanged por
    /// conta própria liga isto antes, e o cronômetro deixa esse desenho passar
    /// sem somar tempo nem interação.
    /// </summary>
    private bool _desenhoDoSistema;

    /// <summary>
    /// De onde a proposta partiu: "nova", "importada", "copia", "aberta" ou
    /// "revisao". A meta é medida na proposta nova ou importada; uma cópia ou
    /// uma revisão é muito mais rápida e puxaria a mediana para baixo.
    /// </summary>
    private string _origem = "nova";

    private string TempoNaTela
    {
        get
        {
            var minutos = (int)Math.Round(_cronSegundos / 60);
            return minutos < 1 ? "menos de 1 min" : $"{minutos} min";
        }
    }

    /// <summary>A etapa à vista, como é gravada: "1" a "5", e a 3 com a aba ("3-escopo").</summary>
    private string ChaveDaEtapa() => _etapa == 3 ? "3-" + _abaItem : _etapa.ToString();

    /// <summary>
    /// Soma um uso de um atalho da tela ("modelo:botao1", "escopo:desfeito"…).
    /// Vai junto com o tempo, na próxima geração de documento.
    /// </summary>
    private void Contar(string chave) =>
        _contadores[chave] = _contadores.GetValueOrDefault(chave) + 1;

    /// <summary>
    /// Chamado a cada desenho. O tempo desde o desenho anterior vai para a
    /// etapa que estava na tela; a interação só conta quando o desenho vem
    /// 400 ms ou mais depois do anterior (ver <see cref="MesmaInteracao"/>).
    /// </summary>
    private void TickCronometro()
    {
        if (_desenhoDoSistema)
        {
            _desenhoDoSistema = false;
            return;
        }

        var agora = DateTime.Now;

        if (_cronUltimo is { } ultimo)
        {
            var passou = (agora - ultimo).TotalSeconds;
            if (passou < PausaQueNaoConta)
            {
                _cronSegundos += passou;
                if (_chaveDoUltimo.Length > 0)
                    _segundosPorEtapa[_chaveDoUltimo] = _segundosPorEtapa.GetValueOrDefault(_chaveDoUltimo) + passou;
            }

            if (passou >= MesmaInteracao) _cronInteracoes++;
        }
        else
        {
            _cronInteracoes++;
        }

        _cronUltimo = agora;
        _chaveDoUltimo = ChaveDaEtapa();
    }

    private void ZerarCronometro()
    {
        _cronInicio = DateTime.Now;
        _cronUltimo = null;
        _cronSegundos = 0;
        _cronInteracoes = 0;
        _segundosPorEtapa = new();
        _chaveDoUltimo = "";
        _contadores = new();
    }

    private void RegistrarTempo(string evento)
    {
        try
        {
            var porEtapa = _segundosPorEtapa
                .Where(e => e.Value >= 0.5)
                .ToDictionary(e => e.Key, e => (int)Math.Round(e.Value));

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
                PorEtapa = JsonSerializer.Serialize(porEtapa),
                Contadores = JsonSerializer.Serialize(_contadores),
                Origem = _origem,
                Equipamentos = _proposta.Itens.Count,
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
    /// <para>Com <paramref name="destinoFixo"/> (a área de soltar da aba
    /// Técnica, item 19), tudo vai para aquele equipamento: nenhum nasce, o
    /// aberto continua aberto, a seleção nova substitui a anterior e a folha de
    /// dados é ignorada — ela cria e reordena equipamentos, e entra pela etapa
    /// Começar.</para>
    /// </summary>
    private async Task ImportarArquivos(InputFileChangeEventArgs e, int? destinoFixo = null)
    {
        if (destinoFixo is { } fixo && (fixo < 0 || fixo >= _proposta.Itens.Count))
        {
            Aviso("Esse equipamento não está mais na proposta.", erro: true);
            return;
        }

        _lendo = true;
        _importados = new();
        var folhaIgnorada = false;

        // o que veio da folha só se apaga quando uma folha nova pode chegar
        if (destinoFixo is null)
        {
            _avisos = new();
            _daPlanilha = new();
        }

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
                if (destinoFixo is not null)
                {
                    folhaIgnorada = true;
                    _importados.Add((nome, "ignorado: a folha de dados entra pela etapa Começar"));
                    continue;
                }

                var equipamentos = LerFolha(bytes);
                _origem = "importada";
                _importados.Add((nome, equipamentos > 0
                    ? $"folha de dados: {Plural(equipamentos, "equipamento", "equipamentos")}"
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
            var selecoesNoFixo = 0;

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

                // num equipamento só, a seleção é uma: a segunda apagaria a primeira
                if (destinoFixo is not null && selecoesNoFixo++ > 0)
                {
                    _importados.Add((nome, "ignorado: o equipamento já recebeu uma seleção neste envio"));
                    continue;
                }

                var indice = Destino(nome, i => i.Tecnicos.Count == 0 && i.ArquivoDaSelecao.Length == 0, destinoFixo);

                // a seleção nova substitui a anterior inteira: AplicarSelecao só
                // sobrescreve chaves, e um dado que só existia no arquivo antigo
                // ficaria lá, parecendo ter vindo do novo
                var substituiu = destinoFixo is not null && _proposta.Itens[indice].Tecnicos.Count > 0;
                if (substituiu) TirarSelecao(indice);

                var curvasDoWord = await AplicarSelecao(leitura, ext, bytes, nome, indice);
                _importados.Add((nome, $"seleção do equipamento {indice + 1}"
                    + (curvasDoWord > 0 ? $", com {Plural(curvasDoWord, "curva", "curvas")}" : "")
                    + (substituiu ? " (no lugar da anterior)" : "")));
            }

            foreach (var (nome, _, bytes) in curvas.OrderBy(c => c.Nome, StringComparer.OrdinalIgnoreCase))
            {
                var indice = Destino(nome, i => i.Curvas.Count == 0, destinoFixo);
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

            if (destinoFixo is { } aberto)
            {
                // na aba do equipamento não há a lista "arquivo → destino": o aviso diz
                var ignorados = _importados.Count(x => x.Destino.StartsWith("ignorado", StringComparison.Ordinal));
                var entraram = _importados.Count - ignorados;
                var texto = entraram > 0
                    ? $"{Plural(entraram, "arquivo entrou", "arquivos entraram")} no equipamento {aberto + 1}."
                    : $"Nenhum arquivo entrou no equipamento {aberto + 1}.";
                if (folhaIgnorada) texto += " A folha de dados entra pela etapa Começar.";
                else if (ignorados > 0) texto += $" {Plural(ignorados, "arquivo ignorado", "arquivos ignorados")}.";

                // algo ficou de fora: o aviso espera ser lido
                Aviso(texto, fica: ignorados > 0);
                return;
            }

            _aberto = 0;
            // o que foi para onde está logo abaixo do botão: o aviso só conta
            Aviso($"{Plural(lidos.Count, "arquivo importado", "arquivos importados")}.");
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
    /// o primeiro livre; senão um equipamento novo. Com
    /// <paramref name="destinoFixo"/>, é sempre ele — e nunca nasce equipamento.
    /// </summary>
    private int Destino(string nome, Func<ItemProposta, bool> livre, int? destinoFixo = null)
    {
        if (destinoFixo is { } fixo) return fixo;

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

    /// <summary>
    /// O modelo curto, para o chip da faixa do equipamento: "Joy 27 1/7" (série
    /// e diâmetro). O rótulo inteiro fica no title do chip.
    /// </summary>
    private string ModeloCurto(ItemProposta item) => ModeloDe(item) is { } m
        ? $"{m.Serie} {m.Diametro}".Trim()
        : "sem modelo";

    /// <summary>O title do chip: o rótulo inteiro do modelo e, quando veio da folha, a coluna.</summary>
    private string TituloDoChip(ItemProposta item) =>
        (ModeloDe(item)?.Rotulo ?? "sem modelo escolhido")
        + (item.Coluna.Length > 0 ? $" · coluna {item.Coluna} da folha de dados" : "");

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
        // o ruído vem do ponto de operação, que é de cada ventilador; a
        // altitude (a mesma mina), o diâmetro (o mesmo modelo), as listas que a
        // folha respondeu e os campos automáticos do motor ficam com o escopo
        copia.RuidoDb = "";

        _proposta.Itens.Insert(posicao + 1, copia);
        _faltouNaSelecao.Clear();
        _aberto = posicao + 1;
        _abaItem = "escopo";
        _apagandoItem = null;
        _rolarPara = ConteudoDoEquipamento;

        Aviso($"Equipamento {posicao + 1} duplicado como equipamento {posicao + 2}: confira o modelo e a " +
              "quantidade, e suba a seleção e a curva dele.", fica: true);
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
            ? $"Escopo padrão da série {serie} aplicado em {Plural(feitas, "linha", "linhas")} — o que já estava marcado ficou como estava."
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
            Aviso($"Escopo padrão da série {modelo.Serie} aplicado ({Plural(feitas, "linha", "linhas")}) — confira e ajuste o que for diferente.",
                fica: true);
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
